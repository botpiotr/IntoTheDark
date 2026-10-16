using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Effets de post-traitement du stress, à partir de 30 % : aberration chromatique croissante,
    /// vision brouillée au maximum, et flash noir et blanc (0,5 s) au moment où le maximum est atteint.
    /// Les bords noirs et les flashs noirs sont gérés par ScreenFeedback.
    /// </summary>
    [RequireComponent(typeof(PlayerStress))]
    public class StressEffects : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Stress à partir duquel les effets apparaissent")]
        private float effectsStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float maxAberration = 0.6f;
        [SerializeField, Min(0.1f), Tooltip("Secondes pour que le flou s'installe au stress maximum")]
        private float blurFadeIn = 1.5f;
        [SerializeField, Min(0.1f)] private float blurFadeOut = 2.5f;
        [SerializeField, Tooltip("Secondes entre deux inversions noir / blanc pendant le choc")]
        private float shockFlickerInterval = 0.07f;

        private PlayerStress stress;
        private ChromaticAberration aberration;
        private Volume blurVolume;
        private Volume shockVolume;
        private ColorAdjustments shockColor;
        private float blur;
        private float shockTimer;

        private void Awake()
        {
            stress = GetComponent<PlayerStress>();

            var stressProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            aberration = stressProfile.Add<ChromaticAberration>(true);
            CreateVolume("StressVolume", stressProfile, 10f, 1f);

            var blurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var depth = blurProfile.Add<DepthOfField>(true);
            depth.mode.Override(DepthOfFieldMode.Gaussian);
            depth.gaussianStart.Override(0.3f);
            depth.gaussianEnd.Override(4f);
            depth.gaussianMaxRadius.Override(1.5f);
            depth.highQualitySampling.Override(true);
            blurVolume = CreateVolume("StressBlurVolume", blurProfile, 11f, 0f);

            var shockProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            shockColor = shockProfile.Add<ColorAdjustments>(true);
            shockColor.saturation.Override(-100f);
            shockColor.contrast.Override(90f);
            shockColor.postExposure.Override(0f);
            shockVolume = CreateVolume("StressShockVolume", shockProfile, 12f, 0f);
        }

        private void OnEnable() => stress.Shocked += OnShocked;
        private void OnDisable() => stress.Shocked -= OnShocked;

        private void OnShocked(float duration) => shockTimer = duration;

        private Volume CreateVolume(string volumeName, VolumeProfile profile, float priority, float weight)
        {
            var go = new GameObject(volumeName);
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = weight;
            volume.profile = profile;
            return volume;
        }

        private void Update()
        {
            float s = stress.Normalized;
            aberration.intensity.Override(Mathf.Clamp01((s - effectsStart) / (1f - effectsStart)) * maxAberration);

            bool maxed = stress.IsMaxed;
            blur = Mathf.MoveTowards(blur, maxed ? 1f : 0f, Time.deltaTime / (maxed ? blurFadeIn : blurFadeOut));
            blurVolume.weight = blur;

            // Choc : image en noir et blanc, qui alterne brutalement entre surexposée et sombre.
            if (shockTimer > 0f)
            {
                shockTimer -= Time.unscaledDeltaTime;
                bool white = Mathf.FloorToInt(Time.unscaledTime / shockFlickerInterval) % 2 == 0;
                shockColor.postExposure.Override(white ? 1.5f : -1.5f);
                shockVolume.weight = 1f;
            }
            else
            {
                shockVolume.weight = 0f;
            }
        }
    }
}
