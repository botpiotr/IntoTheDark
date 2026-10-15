using UnityEngine;

namespace IntoTheDark.Cycle
{
    /// <summary>
    /// Grondement sourd généré par le code (aucun fichier son) : presque inaudible au calme,
    /// il monte avec les signes avant-coureurs, gronde pendant les tempêtes et domine l'apocalypse.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientRumble : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float calmVolume = 0.04f;
        [SerializeField, Range(0f, 1f)] private float warningVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float stormVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float apocalypseVolume = 0.85f;
        [SerializeField, Range(0.3f, 1.5f)] private float apocalypsePitch = 0.7f;
        [SerializeField, Min(0.1f), Tooltip("Vitesse de variation du volume (par seconde)")] private float fadeSpeed = 0.5f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.clip = CreateRumble(8f);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
        }

        private void Update()
        {
            var cycle = CycleManager.Instance;
            float target = calmVolume;
            float pitch = 1f;

            if (cycle != null)
            {
                switch (cycle.Phase)
                {
                    case CyclePhase.Storm: target = stormVolume; break;
                    case CyclePhase.Apocalypse: target = apocalypseVolume; pitch = apocalypsePitch; break;
                }
                target = Mathf.Max(target, Mathf.Lerp(calmVolume, warningVolume, cycle.WarningIntensity));
            }

            source.volume = Mathf.MoveTowards(source.volume, target, fadeSpeed * Time.deltaTime);
            source.pitch = Mathf.MoveTowards(source.pitch, pitch, 0.2f * Time.deltaTime);
        }

        /// <summary>Bruit brun filtré, bouclé sans coupure audible.</summary>
        private static AudioClip CreateRumble(float seconds)
        {
            const int rate = 44100;
            int fade = rate / 2;
            int total = Mathf.CeilToInt(rate * seconds) + fade;
            var raw = new float[total];
            var random = new System.Random(7);

            float brown = 0f, low1 = 0f, low2 = 0f, peak = 1e-6f;
            for (int i = 0; i < total; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                brown = (brown + 0.02f * white) / 1.02f;
                low1 += (brown - low1) * 0.04f;
                low2 += (low1 - low2) * 0.04f;
                // Lente pulsation pour un grondement qui "respire"
                float swell = 0.75f + 0.25f * Mathf.Sin(i * (2f * Mathf.PI / (rate * 2.7f)));
                raw[i] = low2 * swell;
                peak = Mathf.Max(peak, Mathf.Abs(raw[i]));
            }

            // Fondu enchaîné entre la fin et le début pour boucler proprement.
            int length = total - fade;
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float sample = raw[i];
                if (i < fade)
                {
                    float t = (float)i / fade;
                    sample = raw[i] * t + raw[length + i] * (1f - t);
                }
                data[i] = sample / peak * 0.9f;
            }

            var clip = AudioClip.Create("AmbientRumble", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
