using System;
using System.Collections.Generic;
using IntoTheDark.Aether;
using IntoTheDark.Core;
using IntoTheDark.Cycle;
using UnityEngine;

namespace IntoTheDark.Lighting
{
    /// <summary>
    /// Phare : grande zone de lumière qui repousse les ténèbres, alimentée par le gisement d'éther
    /// sur lequel il est posé. Allumé, il consomme en continu (10 éther par cycle à la base,
    /// répartis sur la durée réelle du cycle) ; gisement vide, il s'éteint.
    /// Touche E dessus : allumer / éteindre.
    /// Réserve basse : le foyer pâlit et vacille (la zone éclairée ne rétrécit pas).
    /// Apocalypse : sa puissance et sa zone chutent fortement.
    /// Une jauge graduée sur le fût donne une estimation de la réserve.
    /// </summary>
    public class Beacon : MonoBehaviour, IInteractable
    {
        [SerializeField] private Light beaconLight;
        [SerializeField] private LightZone lightZone;
        [SerializeField, Tooltip("Foyer lumineux")] private Renderer[] glowRenderers = Array.Empty<Renderer>();
        [SerializeField, Tooltip("Halo visible de loin")] private GameObject halo;
        [SerializeField] private bool startLit = true;

        [Header("Éther")]
        [SerializeField, Tooltip("Gisement sur lequel le phare est posé (vide = phare gratuit)")]
        private AetherDeposit deposit;
        [SerializeField, Min(0f), Tooltip("Éther consommé par cycle complet, en continu")]
        private float costPerCycle = 10f;
        [SerializeField, Min(0f), Tooltip("Réserve minimale pour pouvoir le rallumer")]
        private float minReserveToLight = 1f;
        [SerializeField, Range(0.01f, 1f), Tooltip("En dessous : le foyer pâlit et vacille")]
        private float lowReserveThreshold = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("Éclat restant quand la réserve est presque vide")]
        private float minBrightness = 0.35f;
        [SerializeField, Tooltip("Pivot (en bas) du liquide de la jauge, étiré selon la réserve")]
        private Transform gaugeFluid;
        [SerializeField] private Renderer gaugeRenderer;

        [Header("Apocalypse")]
        [SerializeField, Range(0.05f, 1f)] private float apocalypseRadiusFactor = 0.4f;
        [SerializeField, Range(0.05f, 1f)] private float apocalypseIntensityFactor = 0.35f;
        [SerializeField, Min(0.1f), Tooltip("Secondes pour passer d'un état à l'autre")]
        private float apocalypseTransition = 3f;

        [Header("Vacillement")]
        [SerializeField, Range(0f, 0.3f)] private float flickerAmount = 0.03f;
        [SerializeField] private float flickerSpeed = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Vacillement ajouté pendant les signes avant-coureurs et les tempêtes")]
        private float warningFlicker = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("Vacillement ajouté quand la réserve est basse")]
        private float lowReserveFlicker = 0.2f;

        private static readonly List<Beacon> all = new List<Beacon>();

        private EmissionControl[] glows;
        private EmissionControl gaugeGlow;
        private float baseIntensity;
        private float baseRadius;
        private Vector3 baseHaloScale;
        private float noiseSeed;
        private float apocalypse;      // 0..1, lissé
        private float sputterTimer;    // allumage raté faute d'éther

        public static IReadOnlyList<Beacon> All => all;

        public bool IsLit { get; private set; }
        public LightZone Zone => lightZone;
        public AetherDeposit Deposit => deposit;
        /// <summary>Consommation par cycle (diminuable par des améliorations).</summary>
        public float CostPerCycle
        {
            get => costPerCycle;
            set => costPerCycle = Mathf.Max(0f, value);
        }
        public float ReserveNormalized => deposit != null ? deposit.Reserve.Normalized : 1f;
        public event Action<bool> LitChanged;

        /// <summary>Un phare allumé couvre-t-il ce point ?</summary>
        public static bool AnyLitBeaconCovers(Vector3 point) => FindCovering(point) != null;

        /// <summary>Premier phare allumé dont la zone couvre ce point (null si aucun).</summary>
        public static Beacon FindCovering(Vector3 point)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i].Covers(point))
                    return all[i];
            return null;
        }

        public bool Covers(Vector3 point) => IsLit && lightZone != null && lightZone.Contains(point);

        /// <summary>Phare allumé le plus proche (null si aucun phare n'est allumé).</summary>
        public static Beacon FindNearestLit(Vector3 from)
        {
            Beacon best = null;
            float bestDistance = float.MaxValue;
            foreach (var beacon in all)
            {
                if (!beacon.IsLit) continue;
                float d = (beacon.transform.position - from).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = beacon;
                }
            }
            return best;
        }

        /// <summary>Point de réapparition au sol, à quelques pas du phare.</summary>
        public Vector3 RespawnPoint
        {
            get
            {
                Vector3 beside = transform.position - transform.forward * 2.5f;
                if (Physics.Raycast(beside + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                    return hit.point + Vector3.up * 0.05f;
                return beside;
            }
        }

        /// <summary>
        /// Puise de l'éther dans le gisement du phare (recharge de la lampe).
        /// Renvoie la quantité obtenue ; sans gisement, l'éther est gratuit.
        /// </summary>
        public float DrawAether(float quantity)
        {
            if (deposit == null) return quantity;
            return deposit.Reserve.Take(quantity);
        }

        private void Awake()
        {
            if (beaconLight == null) beaconLight = GetComponentInChildren<Light>();
            if (lightZone == null) lightZone = GetComponentInChildren<LightZone>();

            baseIntensity = beaconLight != null ? beaconLight.intensity : 0f;
            baseRadius = lightZone != null ? lightZone.Radius : 0f;
            baseHaloScale = halo != null ? halo.transform.localScale : Vector3.one;
            noiseSeed = UnityEngine.Random.value * 100f;

            glows = new EmissionControl[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
                glows[i] = new EmissionControl(glowRenderers[i]);
            gaugeGlow = new EmissionControl(gaugeRenderer);

            SetLit(startLit);
        }

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        private bool HasFuelToLight => deposit == null || deposit.Reserve.Amount >= minReserveToLight;

        /// <summary>Consommation continue, étalée sur la durée réelle du cycle.</summary>
        private void Consume(float dt)
        {
            if (deposit == null || costPerCycle <= 0f) return;
            float fraction = CycleManager.CycleFraction(dt, deposit.FallbackCycleDuration);
            deposit.Reserve.Take(costPerCycle * fraction);
            if (deposit.Reserve.IsEmpty)
                SetLit(false); // à sec : le phare s'éteint
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            if (IsLit)
            {
                SetLit(false);
                return;
            }

            if (HasFuelToLight)
                SetLit(true);
            else
                sputterTimer = 0.8f; // pas assez d'éther : le foyer crachote sans s'allumer
        }

        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (beaconLight != null) beaconLight.enabled = lit;
            if (lightZone != null) lightZone.enabled = lit;
            if (halo != null) halo.SetActive(lit);
            if (!lit && glows != null)
                foreach (var glow in glows)
                    glow.SetIntensity(0f);

            LitChanged?.Invoke(lit);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (IsLit)
                Consume(dt);

            var cycle = CycleManager.Instance;
            CyclePhase phase = cycle != null ? cycle.Phase : CyclePhase.Calm;

            apocalypse = Mathf.MoveTowards(apocalypse, phase == CyclePhase.Apocalypse ? 1f : 0f, dt / apocalypseTransition);
            if (lightZone != null)
                lightZone.Radius = baseRadius * Mathf.Lerp(1f, apocalypseRadiusFactor, apocalypse);

            float reserve = ReserveNormalized;
            float reserveStrength = reserve < lowReserveThreshold ? reserve / lowReserveThreshold : 1f;
            float brightness = Mathf.Lerp(minBrightness, 1f, reserveStrength)
                             * Mathf.Lerp(1f, apocalypseIntensityFactor, apocalypse);

            float agitation = cycle != null
                ? Mathf.Max(cycle.WarningIntensity, phase == CyclePhase.Storm ? 0.6f : 0f)
                : 0f;
            float amplitude = flickerAmount + warningFlicker * agitation + lowReserveFlicker * (1f - reserveStrength);
            float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed * (1f + 3f * agitation)) - 0.5f;
            float flame = Mathf.Max(0f, brightness * (1f + noise * 2f * amplitude));

            if (IsLit)
            {
                if (beaconLight != null) beaconLight.intensity = baseIntensity * flame;
                foreach (var glow in glows) glow.SetIntensity(flame);
                if (halo != null) halo.transform.localScale = baseHaloScale * Mathf.Lerp(0.6f, 1f, brightness);
            }
            else if (sputterTimer > 0f)
            {
                sputterTimer -= dt;
                float spark = UnityEngine.Random.value < 0.35f ? UnityEngine.Random.Range(0.1f, 0.5f) : 0f;
                foreach (var glow in glows) glow.SetIntensity(sputterTimer > 0f ? spark : 0f);
            }

            UpdateGauge(reserve);
        }

        private void UpdateGauge(float reserve)
        {
            if (gaugeFluid != null)
            {
                var scale = gaugeFluid.localScale;
                scale.y = Mathf.Max(0.01f, reserve);
                gaugeFluid.localScale = scale;
            }

            float pulse = reserve < lowReserveThreshold ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 3f)) : 1f;
            gaugeGlow.SetIntensity(reserve <= 0f ? 0.1f : pulse);
        }
    }
}
