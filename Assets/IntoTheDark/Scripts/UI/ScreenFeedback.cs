using IntoTheDark.Player;
using UnityEngine;
using UnityEngine.UI;

namespace IntoTheDark.UI
{
    /// <summary>
    /// Retours à l'écran, sans jauge :
    /// - flash rouge léger à chaque coup reçu ;
    /// - bords rouges qui se resserrent quand la vie baisse (centre toujours transparent),
    ///   et qui pulsent comme un cœur quand elle est basse ;
    /// - bords noirs qui se resserrent avec le stress, qui pulsent quand il est élevé,
    ///   avec des flashs noirs au maximum.
    /// </summary>
    public class ScreenFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerStress stress;

        [Header("Coup reçu")]
        [SerializeField, Range(0f, 1f)] private float hitFlashAlpha = 0.4f;
        [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.35f;
        [SerializeField] private Color hitColor = new Color(0.75f, 0.02f, 0.02f);
        [SerializeField, Range(0f, 1f), Tooltip("Flash d'un coup amplifié par le stress (> 80 %)")]
        private float strongHitFlashAlpha = 0.8f;
        [SerializeField, Min(0.05f)] private float strongHitFlashDuration = 0.6f;

        [Header("Vie basse : bords rouges")]
        [SerializeField] private Color woundColor = new Color(0.45f, 0f, 0f);
        [SerializeField, Range(0f, 1f)] private float woundMaxAlpha = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("En dessous : l'écran pulse en rouge")] private float lowHealth = 0.35f;
        [SerializeField, Range(0f, 1f)] private float woundPulseAlpha = 0.35f;
        [SerializeField, Tooltip("Resserrement supplémentaire au battement")] private float woundPulseClosure = 0.25f;

        [Header("Stress : bords noirs")]
        [SerializeField, Range(0f, 1f), Tooltip("Stress à partir duquel les bords noirs apparaissent")]
        private float stressEffectsStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float stressMaxAlpha = 0.85f;
        [SerializeField, Tooltip("Intervalle entre deux flashs noirs au stress maximum (s)")]
        private Vector2 flashInterval = new Vector2(0.5f, 1.8f);
        [SerializeField, Min(0.01f)] private float flashDuration = 0.18f;
        [SerializeField, Range(0f, 1f), Tooltip("Au-dessus : l'écran pulse en noir")] private float stressPulseStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float stressPulseMaxAlpha = 0.6f;
        [SerializeField, Tooltip("Resserrement supplémentaire à chaque pulsation")] private float stressPulseMaxClosure = 0.45f;

        [Header("Resserrement")]
        [SerializeField, Tooltip("Échelle des bords quand ils sont absents (repoussés hors de l'écran)")]
        private float openScale = 1.7f;
        [SerializeField, Tooltip("Échelle des bords au plus fort (vision resserrée)")]
        private float closedScale = 0.95f;

        private Image woundEdge;
        private Image stressEdge;
        private Image hitFlash;
        private float hitTimer;
        private float hitDuration = 1f;
        private float hitAlpha;
        private float flashTimer;
        private float flashLeft;
        private float heartPhase;
        private float stressPhase;

        private void Awake()
        {
            if (health == null) health = GetComponent<PlayerHealth>();
            if (stress == null) stress = GetComponent<PlayerStress>();

            var canvas = UiFactory.CreateCanvas("ScreenFeedback", 50);
            canvas.transform.SetParent(transform, false);
            var edgeSprite = CreateEdgeSprite(256);

            woundEdge = CreateEdge(canvas.transform, "WoundEdge", edgeSprite);
            stressEdge = CreateEdge(canvas.transform, "StressEdge", edgeSprite);
            hitFlash = UiFactory.CreatePanel(canvas.transform, Color.clear, "HitFlash");
            hitFlash.raycastTarget = false;

            flashTimer = Random.Range(flashInterval.x, flashInterval.y);
        }

        private void OnEnable() { if (health != null) health.Damaged += OnDamaged; }
        private void OnDisable() { if (health != null) health.Damaged -= OnDamaged; }

        private void OnDamaged(float amount)
        {
            bool strong = health != null && health.LastHitAmplified;
            hitDuration = strong ? strongHitFlashDuration : hitFlashDuration;
            hitAlpha = strong ? strongHitFlashAlpha : hitFlashAlpha;
            hitTimer = hitDuration;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Vie basse
            float wound = health != null ? 1f - health.Normalized : 0f;
            wound = Mathf.Pow(Mathf.Clamp01(wound), 1.2f);
            float woundAlpha = woundMaxAlpha * wound;
            float woundClosure = wound;
            float h = health != null ? health.Normalized : 1f;
            if (h < lowHealth && health != null && !health.IsDead)
            {
                float danger = 1f - h / lowHealth;
                heartPhase += dt * Mathf.Lerp(1.2f, 3f, danger);
                float beat = HeartBeat(heartPhase % 1f) * Mathf.Lerp(0.5f, 1f, danger);
                woundAlpha += woundPulseAlpha * beat;
                woundClosure += woundPulseClosure * beat;
            }
            SetEdge(woundEdge, woundColor, woundAlpha, woundClosure);

            // Stress, avec flashs noirs au maximum
            float s = stress != null ? stress.Normalized : 0f;
            float t = Mathf.InverseLerp(stressEffectsStart, 1f, s);
            float stressAmount = t * t;
            float stressAlpha = stressMaxAlpha * stressAmount;
            if (s > stressPulseStart)
            {
                // Pulsation franche (montée rapide, retombée lente), nette dès 30 % et plus forte ensuite.
                float level = Mathf.InverseLerp(stressPulseStart, 1f, s);
                stressPhase += dt * Mathf.Lerp(0.7f, 2.2f, level);
                float p = stressPhase % 1f;
                float pulse = p < 0.15f ? p / 0.15f : Mathf.Pow(1f - (p - 0.15f) / 0.85f, 2f);
                pulse *= Mathf.Lerp(0.6f, 1f, level);
                stressAlpha = Mathf.Max(stressAlpha, 0.25f) + stressPulseMaxAlpha * pulse;
                stressAmount = Mathf.Max(stressAmount, 0.35f) + stressPulseMaxClosure * pulse;
            }
            if (stress != null && stress.IsMaxed)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f)
                {
                    flashLeft = flashDuration;
                    flashTimer = Random.Range(flashInterval.x, flashInterval.y);
                }
            }
            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                stressAlpha = 1f;
                stressAmount = 1.08f; // resserrement brutal le temps du flash
            }
            SetEdge(stressEdge, Color.black, stressAlpha, stressAmount);

            // Flash rouge du coup reçu
            hitTimer = Mathf.Max(0f, hitTimer - dt);
            float hit = hitTimer / hitDuration;
            hitFlash.color = new Color(hitColor.r, hitColor.g, hitColor.b, hitAlpha * hit * hit);
        }

        /// <summary>Double battement (« boum-boum ») sur une période de 0 à 1.</summary>
        private static float HeartBeat(float t)
        {
            float first = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.06f, 2f));
            float second = 0.7f * Mathf.Exp(-Mathf.Pow((t - 0.3f) / 0.06f, 2f));
            return Mathf.Clamp01(first + second);
        }

        private void SetEdge(Image edge, Color color, float alpha, float closure)
        {
            edge.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            float scale = Mathf.LerpUnclamped(openScale, closedScale, closure);
            edge.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private static Image CreateEdge(Transform parent, string name, Sprite sprite)
        {
            var image = UiFactory.CreatePanel(parent, Color.clear, name);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Dégradé radial : transparent au centre, opaque sur les bords et dans les coins.</summary>
        private static Sprite CreateEdgeSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
