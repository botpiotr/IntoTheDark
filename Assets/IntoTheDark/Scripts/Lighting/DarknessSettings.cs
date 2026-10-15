using IntoTheDark.Cycle;
using UnityEngine;

namespace IntoTheDark.Lighting
{
    /// <summary>
    /// Réglages globaux des ténèbres (shader IntoTheDark/DarknessLit) et envoi des zones
    /// de lumière actives au shader. Un seul par scène. Modifiable en direct, même hors Play.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DarknessSettings : MonoBehaviour
    {
        public const int MaxZones = 16; // doit correspondre au shader

        [SerializeField, Tooltip("Couleur du brouillard, presque noire")]
        private Color fogColor = new Color(0.012f, 0.011f, 0.014f);

        [SerializeField, Min(0.05f), Tooltip("Largeur du fondu au bord des zones de lumière (m)")]
        private float edgeWidth = 0.6f;

        [Header("Liseré à la limite de la lumière")]
        [SerializeField, Min(0.005f), Tooltip("Demi-épaisseur du liseré (m)")]
        private float edgeLineWidth = 0.06f;
        [SerializeField, Range(0f, 2f)] private float edgeLineIntensity = 0.35f;

        [Header("Zones vues de loin")]
        [SerializeField, Min(0f), Tooltip("Au-delà de cette distance du bord d'une zone, sa partie éclairée commence à disparaître (m)")]
        private float outsideViewStart = 0f;
        [SerializeField, Min(0f), Tooltip("Au-delà de cette distance, on ne voit plus que la lumière des sources (foyer, halo) (m)")]
        private float outsideViewEnd = 1.5f;

        [Header("Texture du brouillard")]
        [SerializeField, Min(0.01f)] private float noiseScale = 0.5f;
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.15f;
        [SerializeField, Min(0f)] private float driftSpeed = 0.15f;

        [Header("Cycle : tempêtes et apocalypse")]
        [SerializeField, Tooltip("Teinte des ténèbres pendant l'apocalypse")]
        private Color apocalypseFogColor = new Color(0.04f, 0.004f, 0.006f);
        [SerializeField, Range(0f, 1f), Tooltip("Agitation du brouillard ajoutée par les signes avant-coureurs et les tempêtes")]
        private float agitationNoiseBoost = 0.35f;
        [SerializeField, Min(0f)] private float agitationDriftBoost = 4f;
        [SerializeField, Min(0.1f), Tooltip("Secondes pour changer d'ambiance")] private float ambienceTransition = 2f;

        [Header("Éditeur")]
        [SerializeField, Tooltip("Hors Play, appliquer les ténèbres dans la vue Scene (sinon tout est visible)")]
        private bool previewInEditMode;

        private static readonly int FogColorId = Shader.PropertyToID("_DarknessFogColor");
        private static readonly int ParamsId = Shader.PropertyToID("_DarknessParams");
        private static readonly int NoiseId = Shader.PropertyToID("_DarknessNoise");
        private static readonly int ViewId = Shader.PropertyToID("_DarknessView");
        private static readonly int ZonesId = Shader.PropertyToID("_LightZones");
        private static readonly int ZoneCountId = Shader.PropertyToID("_LightZoneCount");
        private static readonly int ZoneColorsId = Shader.PropertyToID("_LightZoneColors");

        private readonly Vector4[] zoneBuffer = new Vector4[MaxZones];
        private readonly Vector4[] colorBuffer = new Vector4[MaxZones];

        private float apocalypseBlend;   // 0..1
        private float agitation;         // 0..1

        public Color FogColor => fogColor;
        public Color CurrentFogColor => Color.Lerp(fogColor, apocalypseFogColor, apocalypseBlend);

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        private void Update()
        {
            UpdateAmbience(Time.deltaTime);
            Apply();
        }

        private void UpdateAmbience(float dt)
        {
            var cycle = Application.isPlaying ? CycleManager.Instance : null;
            float targetApocalypse = 0f, targetAgitation = 0f;
            if (cycle != null)
            {
                targetApocalypse = cycle.Phase == CyclePhase.Apocalypse ? 1f : 0f;
                float phaseAgitation = cycle.Phase == CyclePhase.Apocalypse ? 1f
                                     : cycle.Phase == CyclePhase.Storm ? 0.6f : 0f;
                targetAgitation = Mathf.Max(phaseAgitation, cycle.WarningIntensity);
            }
            float step = dt / ambienceTransition;
            apocalypseBlend = Mathf.MoveTowards(apocalypseBlend, targetApocalypse, step);
            agitation = Mathf.MoveTowards(agitation, targetAgitation, step);

            var camera = Camera.main;
            if (camera != null && Application.isPlaying)
                camera.backgroundColor = CurrentFogColor;
        }

        private void OnDisable()
        {
            // Sans réglages actifs, tout redevient visible (évite une scène noire en édition).
            Shader.SetGlobalVector(ParamsId, new Vector4(edgeWidth, 0f, edgeLineWidth, 0f));
        }

        public void Apply()
        {
            bool enabledNow = Application.isPlaying || previewInEditMode;

            Shader.SetGlobalColor(FogColorId, CurrentFogColor);
            Shader.SetGlobalVector(ParamsId, new Vector4(edgeWidth, enabledNow ? 1f : 0f, edgeLineWidth, edgeLineIntensity));
            float noise = Mathf.Clamp01(noiseStrength + agitationNoiseBoost * agitation);
            float drift = driftSpeed * (1f + agitationDriftBoost * agitation);
            Shader.SetGlobalVector(NoiseId, new Vector4(noiseScale, noise, drift, 0f));
            Shader.SetGlobalVector(ViewId, new Vector4(outsideViewStart, Mathf.Max(outsideViewStart + 0.01f, outsideViewEnd), 0f, 0f));

            var zones = LightZone.Active;
            int count = Mathf.Min(zones.Count, MaxZones);
            for (int i = 0; i < MaxZones; i++)
            {
                if (i < count)
                {
                    Vector3 c = zones[i].Center;
                    zoneBuffer[i] = new Vector4(c.x, c.y, c.z, zones[i].Radius);
                    Color edge = zones[i].EdgeColor.linear;
                    colorBuffer[i] = new Vector4(edge.r, edge.g, edge.b, 1f);
                }
                else
                {
                    zoneBuffer[i] = Vector4.zero;
                    colorBuffer[i] = Vector4.zero;
                }
            }
            Shader.SetGlobalVectorArray(ZonesId, zoneBuffer);
            Shader.SetGlobalVectorArray(ZoneColorsId, colorBuffer);
            Shader.SetGlobalFloat(ZoneCountId, count);
        }
    }
}
