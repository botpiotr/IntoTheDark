using IntoTheDark.Lighting;
using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Poignet gauche, en bas à gauche de la vue, avec une montre à deux petits tubes lumineux
    /// en travers du bras : vie (rouge) et stress (violet). Indicateurs diégétiques.
    /// - Coup reçu : le tube de vie flashe et le poignet est secoué.
    /// - Vie basse : le tube de vie bat comme un cœur, de plus en plus vite.
    /// - Stress : le tube palpite de plus en plus vite et le bras tremble à partir de 30 % ;
    ///   au maximum, le tube clignote nerveusement.
    /// </summary>
    public class WristGauges : MonoBehaviour
    {
        [Header("Références (remplies par le générateur)")]
        [SerializeField, Tooltip("Partie secouée (le bras entier)")] private Transform shakeRoot;
        [SerializeField, Tooltip("Pivot du liquide de vie, étiré sur son axe X")] private Transform healthFluid;
        [SerializeField] private Renderer healthRenderer;
        [SerializeField, Tooltip("Pivot du liquide de stress, étiré sur son axe X")] private Transform stressFluid;
        [SerializeField] private Renderer stressRenderer;

        [Header("Vie")]
        [SerializeField, Range(0f, 1f), Tooltip("En dessous : battement de cœur")] private float lowHealth = 0.35f;
        [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.4f;
        [SerializeField] private float hitFlashIntensity = 5f;
        [SerializeField, Tooltip("Amplitude de la secousse au coup reçu (m)")] private float hitShake = 0.02f;
        [SerializeField, Min(0.05f)] private float hitShakeDuration = 0.35f;

        [Header("Stress")]
        [SerializeField, Range(0f, 1f), Tooltip("À partir de là, le bras tremble")] private float stressEffectsStart = 0.3f;
        [SerializeField, Tooltip("Amplitude du tremblement au stress maximum (m)")] private float stressTremor = 0.006f;

        [Header("Affichage")]
        [SerializeField, Tooltip("Vitesse à laquelle les niveaux rejoignent la valeur réelle")] private float followSpeed = 4f;

        private PlayerHealth health;
        private PlayerStress stress;
        private EmissionControl healthGlow;
        private EmissionControl stressGlow;
        private Vector3 shakeBase;
        private float shownHealth = 1f;
        private float shownStress;
        private float hitTimer;
        private float heartPhase;
        private float stressPhase;

        private void Awake()
        {
            health = GetComponentInParent<PlayerHealth>();
            stress = GetComponentInParent<PlayerStress>();
            if (shakeRoot == null) shakeRoot = transform;
            shakeBase = shakeRoot.localPosition;
            healthGlow = new EmissionControl(healthRenderer);
            stressGlow = new EmissionControl(stressRenderer);
        }

        private void OnEnable() { if (health != null) health.Damaged += OnDamaged; }
        private void OnDisable() { if (health != null) health.Damaged -= OnDamaged; }

        private void OnDamaged(float amount) => hitTimer = hitFlashDuration;

        private void Update()
        {
            float dt = Time.deltaTime;
            float h = health != null ? health.Normalized : 1f;
            float s = stress != null ? stress.Normalized : 0f;
            shownHealth = Mathf.MoveTowards(shownHealth, h, followSpeed * dt);
            shownStress = Mathf.MoveTowards(shownStress, s, followSpeed * dt);
            SetLevel(healthFluid, healthRenderer, shownHealth);
            SetLevel(stressFluid, stressRenderer, shownStress);

            // Vie : flash au coup, battement de cœur quand elle est basse.
            hitTimer = Mathf.Max(0f, hitTimer - dt);
            float flash = hitTimer / hitFlashDuration;
            float healthGlowValue = 1f;
            if (h < lowHealth)
            {
                float danger = 1f - h / lowHealth;                 // 0 → 1 en approchant de la mort
                heartPhase += dt * Mathf.Lerp(1.2f, 3f, danger);   // battements par seconde
                float beat = HeartBeat(heartPhase % 1f);
                healthGlowValue = Mathf.Lerp(0.7f, 2.5f, beat);
            }
            healthGlow.SetIntensity(Mathf.Max(healthGlowValue, flash * flash * hitFlashIntensity));

            // Stress : palpitation de plus en plus rapide, clignotement nerveux au maximum.
            float stressLevel = Mathf.InverseLerp(stressEffectsStart, 1f, s);
            stressPhase += dt * Mathf.Lerp(0.5f, 4f, stressLevel) * Mathf.PI * 2f;
            float stressGlowValue = Mathf.Lerp(1f, 0.8f + 1.2f * (0.5f + 0.5f * Mathf.Sin(stressPhase)), stressLevel);
            if (stress != null && stress.IsMaxed)
                stressGlowValue = Random.value < 0.35f ? 0.6f : 2.8f;
            stressGlow.SetIntensity(stressGlowValue);

            // Secousse au coup reçu + tremblement de stress.
            float shake = hitShake * Mathf.Clamp01(hitTimer / Mathf.Min(hitShakeDuration, hitFlashDuration));
            float tremor = stressTremor * stressLevel * stressLevel;
            shakeRoot.localPosition = shakeBase + Random.insideUnitSphere * (shake + tremor);
        }

        /// <summary>Niveau du liquide ; jauge vide : plus aucun liquide, le tube reste noir.</summary>
        private static void SetLevel(Transform fluid, Renderer renderer, float value)
        {
            if (fluid == null) return;
            value = Mathf.Clamp01(value);
            if (renderer != null) renderer.enabled = value > 0.01f;
            var scale = fluid.localScale;
            scale.x = Mathf.Max(0.01f, value);
            fluid.localScale = scale;
        }

        /// <summary>Double battement (« boum-boum ») sur une période de 0 à 1.</summary>
        private static float HeartBeat(float t)
        {
            float first = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.05f, 2f));
            float second = 0.7f * Mathf.Exp(-Mathf.Pow((t - 0.3f) / 0.05f, 2f));
            return Mathf.Clamp01(first + second);
        }
    }
}
