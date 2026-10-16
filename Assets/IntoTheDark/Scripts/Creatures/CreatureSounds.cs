using UnityEngine;

namespace IntoTheDark.Creatures
{
    /// <summary>
    /// Sons de créature générés par le code (aucun fichier) : grognement, sifflement, aboiement.
    /// Chaque son a quelques variantes, créées une seule fois et partagées.
    /// </summary>
    public static class CreatureSounds
    {
        private const int Rate = 44100;
        private const int Variants = 3;

        private static AudioClip[] growls, hisses, barks;

        public static AudioClip Growl() => Pick(ref growls, i => CreateGrowl(i));
        public static AudioClip Hiss() => Pick(ref hisses, i => CreateHiss(i));
        public static AudioClip Bark() => Pick(ref barks, i => CreateBark(i));

        private static AudioClip Pick(ref AudioClip[] cache, System.Func<int, AudioClip> create)
        {
            if (cache == null)
            {
                cache = new AudioClip[Variants];
                for (int i = 0; i < Variants; i++) cache[i] = create(i);
            }
            return cache[Random.Range(0, Variants)];
        }

        /// <summary>Grognement : grave, rugueux, qui monte et retombe.</summary>
        private static AudioClip CreateGrowl(int variant)
        {
            var rng = new System.Random(100 + variant);
            float duration = 1.1f + 0.3f * variant;
            int n = (int)(Rate * duration);
            var data = new float[n];
            float phase = 0f, noise = 0f;
            float baseFreq = 70f + 12f * variant;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float freq = baseFreq * (1f + 0.25f * Mathf.Sin(t * Mathf.PI)) + 6f * Mathf.Sin(i * 0.0021f);
                phase += freq / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                noise += ((float)rng.NextDouble() * 2f - 1f - noise) * 0.08f;
                float rough = 0.6f + 0.4f * Mathf.Sin(i * 2f * Mathf.PI * 23f / Rate); // râle
                data[i] = (saw * 0.6f + noise * 0.8f) * rough * Envelope(t, 0.15f, 0.35f);
            }
            return Finish("Growl", data);
        }

        /// <summary>Sifflement : souffle aigu, tendu.</summary>
        private static AudioClip CreateHiss(int variant)
        {
            var rng = new System.Random(200 + variant);
            float duration = 0.9f + 0.25f * variant;
            int n = (int)(Rate * duration);
            var data = new float[n];
            float previous = 0f, low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float white = (float)rng.NextDouble() * 2f - 1f;
                float high = white - previous; // passe-haut grossier
                previous = white;
                low += (high - low) * 0.5f;
                float wobble = 0.8f + 0.2f * Mathf.Sin(i * 2f * Mathf.PI * (6f + variant) / Rate);
                data[i] = low * wobble * Envelope(t, 0.08f, 0.45f);
            }
            return Finish("Hiss", data);
        }

        /// <summary>Aboiement : deux ou trois jappements rauques et brefs.</summary>
        private static AudioClip CreateBark(int variant)
        {
            var rng = new System.Random(300 + variant);
            int barksCount = 2 + variant % 2;
            float barkLength = 0.16f, gap = 0.1f;
            int n = (int)(Rate * (barksCount * (barkLength + gap)));
            var data = new float[n];
            float phase = 0f, noise = 0f;
            for (int b = 0; b < barksCount; b++)
            {
                int start = (int)(Rate * b * (barkLength + gap));
                int length = (int)(Rate * barkLength);
                float top = 520f + 60f * variant - 40f * b;
                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = (float)i / length;
                    float freq = Mathf.Lerp(top, top * 0.45f, t); // chute rapide de la hauteur
                    phase += freq / Rate;
                    float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * phase)) * 0.5f + Mathf.Sin(2f * Mathf.PI * phase) * 0.5f;
                    noise += ((float)rng.NextDouble() * 2f - 1f - noise) * 0.3f;
                    data[start + i] = (square * 0.7f + noise * 0.5f) * Envelope(t, 0.05f, 0.6f);
                }
            }
            return Finish("Bark", data);
        }

        private static float Envelope(float t, float attack, float release)
        {
            if (t < attack) return t / attack;
            if (t > 1f - release) return Mathf.Clamp01((1f - t) / release);
            return 1f;
        }

        private static AudioClip Finish(string name, float[] data)
        {
            float peak = 1e-6f;
            foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.85f;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
