using UnityEngine;

namespace IntoTheDark.Combat
{
    /// <summary>
    /// Réaction visuelle à un coup, comme les créatures : flash blanc et petit bond en l'air et en arrière.
    /// Au plus une réaction par intervalle.
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        [SerializeField, Tooltip("Partie qui bondit et flashe")] private Transform visual;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.15f;
        [SerializeField, ColorUsage(false, true)] private Color flashColor = new Color(2.5f, 2.5f, 2.5f);
        [SerializeField] private float hopHeight = 0.3f;
        [SerializeField, Tooltip("Recul en mètres")] private float knockback = 0.25f;
        [SerializeField, Min(0.05f)] private float hopDuration = 0.3f;
        [SerializeField, Min(0f), Tooltip("Au plus une réaction par intervalle (s)")] private float minInterval = 0.5f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private Renderer[] renderers;
        private Color[] baseEmissions;
        private MaterialPropertyBlock block;
        private Vector3 basePosition;
        private Vector3 knockDirection;
        private float flashTimer;
        private float hopTimer;
        private float lastReaction = float.NegativeInfinity;

        private void Awake()
        {
            if (visual == null) visual = transform;
            basePosition = visual.localPosition;
            renderers = visual.GetComponentsInChildren<Renderer>();
            baseEmissions = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                baseEmissions[i] = material != null && material.HasProperty(EmissionColorId) ? material.GetColor(EmissionColorId) : Color.black;
            }
            block = new MaterialPropertyBlock();
        }

        /// <returns>false si la réaction précédente est trop récente.</returns>
        public bool Play(Vector3 source)
        {
            if (Time.time - lastReaction < minInterval) return false;
            lastReaction = Time.time;
            flashTimer = flashDuration;
            hopTimer = hopDuration;
            knockDirection = visual.position - source;
            knockDirection.y = 0f;
            knockDirection = knockDirection.sqrMagnitude > 0.001f ? visual.parent != null
                ? visual.parent.InverseTransformDirection(knockDirection.normalized)
                : knockDirection.normalized : Vector3.zero;
            return true;
        }

        private void Update()
        {
            if (flashTimer > -Time.deltaTime)
            {
                flashTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(flashTimer / flashDuration);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].GetPropertyBlock(block);
                    block.SetColor(EmissionColorId, Color.Lerp(baseEmissions[i], flashColor, k));
                    renderers[i].SetPropertyBlock(block);
                }
            }

            if (hopTimer > 0f)
            {
                hopTimer = Mathf.Max(0f, hopTimer - Time.deltaTime);
                float t = 1f - hopTimer / hopDuration;
                float up = Mathf.Sin(t * Mathf.PI) * hopHeight;
                float back = Mathf.Sin(t * Mathf.PI) * knockback; // recule puis revient : le mannequin est fixé au sol
                visual.localPosition = basePosition + Vector3.up * up + knockDirection * back;
            }
        }
    }
}
