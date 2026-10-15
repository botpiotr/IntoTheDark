using System;
using IntoTheDark.Cycle;
using IntoTheDark.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IntoTheDark.Lighting
{
    /// <summary>
    /// Lampe tempête portée au bout d'un bâton. Elle crée une zone de lumière autour du joueur
    /// et fonctionne à l'éther. La jauge est sur la lampe elle-même : un tube d'éther lumineux
    /// dont le niveau baisse. Charge faible : la flamme vacille, la lumière rétrécit, le tube clignote.
    /// Dans la lumière d'un phare allumé, elle ne consomme rien et se remplit en puisant dans
    /// le gisement du phare : 1 éther pour 15 s de lumière (0,5 si elle est à moitié pleine).
    /// Pendant l'apocalypse, elle s'éteint et ne peut pas être rallumée.
    /// Touche L (croix haut à la manette) : allumer / éteindre.
    /// </summary>
    public class StormLantern : MonoBehaviour
    {
        [SerializeField] private Light lanternLight;
        [SerializeField, Tooltip("Zone qui repousse les ténèbres, active quand la lampe est allumée")]
        private LightZone lightZone;
        [SerializeField, Tooltip("Verre de la lampe, qui brille quand elle est allumée")]
        private Renderer[] glowRenderers = Array.Empty<Renderer>();
        [SerializeField] private bool startLit = true;

        [Header("Charge d'éther")]
        [SerializeField] private LanternCharge charge = new LanternCharge();
        [SerializeField, Tooltip("Pivot (en bas) du liquide de la jauge, étiré en hauteur selon la charge")]
        private Transform gaugeFluid;
        [SerializeField] private Renderer gaugeRenderer;
        [SerializeField, Min(0f), Tooltip("Éther puisé au phare par seconde de lumière rechargée (1/15 = 1 éther pour 15 s)")]
        private float aetherPerLightSecond = 1f / 15f;
        [SerializeField, Range(0f, 1f), Tooltip("Lumière restante quand la charge est presque vide")]
        private float minStrengthLight = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("Rayon restant quand la charge est presque vide")]
        private float minStrengthRadius = 0.55f;

        [Header("Vacillement")]
        [SerializeField, Range(0f, 0.5f)] private float flickerAmount = 0.08f;
        [SerializeField] private float flickerSpeed = 5f;
        [SerializeField, Tooltip("Vacillement multiplié par cette valeur quand la charge est faible")]
        private float lowChargeFlickerBoost = 4f;

        [Header("Debug")]
        [SerializeField] private bool debugKeys = true;

        private PlayerInputHandler input;
        private EmissionControl[] glows;
        private EmissionControl gaugeGlow;
        private float baseIntensity;
        private float baseRadius;
        private float noiseSeed;

        public bool IsLit { get; private set; }
        public LanternCharge Charge => charge;
        public bool IsRecharging { get; private set; }
        public event Action<bool> LitChanged;

        private void Awake()
        {
            input = GetComponentInParent<PlayerInputHandler>();
            if (lanternLight == null) lanternLight = GetComponentInChildren<Light>();
            if (lightZone == null) lightZone = GetComponentInChildren<LightZone>();

            baseIntensity = lanternLight != null ? lanternLight.intensity : 0f;
            baseRadius = lightZone != null ? lightZone.Radius : 0f;
            noiseSeed = UnityEngine.Random.value * 100f;

            glows = new EmissionControl[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
                glows[i] = new EmissionControl(glowRenderers[i]);
            gaugeGlow = new EmissionControl(gaugeRenderer);

            SetLit(startLit && !charge.IsEmpty);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            bool apocalypse = CycleManager.Instance != null && CycleManager.Instance.Phase == CyclePhase.Apocalypse;

            if (input != null && input.ToggleLampPressed)
                SetLit(!IsLit);

            if (debugKeys)
                HandleDebugKeys();

            if (apocalypse && IsLit)
                SetLit(false); // l'apocalypse éteint les lampes

            var beacon = Beacon.FindCovering(transform.position);
            IsRecharging = beacon != null && !charge.IsFull;
            if (beacon != null)
                RechargeFrom(beacon, dt);     // dans la lumière d'un phare : aucune consommation
            else if (IsLit)
                charge.Burn(dt);

            if (IsLit && charge.IsEmpty)
                SetLit(false); // à sec : la lampe s'éteint

            UpdateVisuals();
        }

        /// <summary>Remplit la lampe en puisant l'éther du phare, au prorata de ce qui manque.</summary>
        private void RechargeFrom(Beacon beacon, float dt)
        {
            if (charge.IsFull) return;

            float wanted = Mathf.Min(dt / charge.RechargeDuration, 1f - charge.Value);
            if (aetherPerLightSecond <= 0f)
            {
                charge.Refill(wanted);
                return;
            }

            float cost = wanted * charge.Capacity * aetherPerLightSecond;
            float paid = beacon.DrawAether(cost);
            charge.Refill(paid / (aetherPerLightSecond * charge.Capacity));
        }

        public void SetLit(bool lit)
        {
            if (lit && charge.IsEmpty)
                lit = false;
            if (lit && CycleManager.Instance != null && CycleManager.Instance.Phase == CyclePhase.Apocalypse)
                lit = false;

            IsLit = lit;
            if (lanternLight != null) lanternLight.enabled = lit;
            if (lightZone != null) lightZone.enabled = lit;
            if (!lit && glows != null)
                foreach (var glow in glows)
                    glow.SetIntensity(0f);

            LitChanged?.Invoke(lit);
        }

        private void UpdateVisuals()
        {
            float strength = charge.Strength;

            if (IsLit)
            {
                float flicker = flickerAmount * Mathf.Lerp(lowChargeFlickerBoost, 1f, strength);
                float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed) - 0.5f;
                float flame = (1f + noise * 2f * flicker) * Mathf.Lerp(minStrengthLight, 1f, strength);

                if (lanternLight != null)
                    lanternLight.intensity = baseIntensity * flame;
                if (lightZone != null)
                    lightZone.Radius = baseRadius * Mathf.Lerp(minStrengthRadius, 1f, strength);
                foreach (var glow in glows)
                    glow.SetIntensity(flame);
            }

            // Jauge : le niveau suit la charge ; elle clignote quand la charge est faible.
            if (gaugeFluid != null)
            {
                var scale = gaugeFluid.localScale;
                scale.y = Mathf.Max(0.02f, charge.Value);
                gaugeFluid.localScale = scale;
            }
            float pulse = charge.IsLow ? 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(Time.time * 5f)) : 1f;
            gaugeGlow.SetIntensity(charge.IsEmpty ? 0.15f : pulse);
        }

        private void HandleDebugKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Pavé numérique + / - : intensité de la lumière
            if (keyboard.numpadPlusKey.wasPressedThisFrame) SetBaseIntensity(baseIntensity * 1.25f);
            if (keyboard.numpadMinusKey.wasPressedThisFrame) SetBaseIntensity(baseIntensity / 1.25f);
        }

        private void SetBaseIntensity(float value)
        {
            baseIntensity = Mathf.Clamp(value, 0.5f, 1000f);
            Debug.Log($"[StormLantern] Intensité : {baseIntensity:0.0}");
        }
    }
}
