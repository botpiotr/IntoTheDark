using System;
using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDark.Cycle
{
    public enum CyclePhase
    {
        Calm,
        Storm,
        Apocalypse,
    }

    /// <summary>Durées d'un cycle (secondes).</summary>
    [Serializable]
    public class CycleTimings
    {
        [Min(1f), Tooltip("Temps de calme total dans un cycle, hors tempêtes")]
        public float calmDuration = 900f;
        [Min(0)] public int minStorms = 0;
        [Min(0)] public int maxStorms = 2;
        [Min(1f)] public float minStormDuration = 60f;
        [Min(1f)] public float maxStormDuration = 120f;
        [Min(1f)] public float apocalypseDuration = 120f;
        [Min(0f), Tooltip("Durée des signes avant-coureurs avant une tempête ou l'apocalypse")]
        public float warningLead = 12f;
        [Range(0f, 0.9f), Tooltip("Part du calme garantie sans tempête en début de cycle")]
        public float quietStartFraction = 0.15f;

        /// <summary>Durées du jeu : 15 min de calme, 0 à 2 tempêtes de 1 à 2 min, 2 min d'apocalypse.</summary>
        public static CycleTimings Normal() => new CycleTimings();

        /// <summary>Durées de test : cycle d'environ 2 minutes, avec au moins une tempête.</summary>
        public static CycleTimings Test() => new CycleTimings
        {
            calmDuration = 90f,
            minStorms = 1,
            maxStorms = 2,
            minStormDuration = 10f,
            maxStormDuration = 15f,
            apocalypseDuration = 20f,
            warningLead = 5f,
            quietStartFraction = 0.15f,
        };
    }

    public readonly struct PhaseSpan
    {
        public readonly CyclePhase Phase;
        public readonly float Start;
        public readonly float End;

        public PhaseSpan(CyclePhase phase, float start, float end)
        {
            Phase = phase;
            Start = start;
            End = end;
        }

        public float Duration => End - Start;
    }

    /// <summary>
    /// Déroulé d'un cycle : calme ponctué de tempêtes placées au hasard, puis apocalypse.
    /// Logique pure, testable.
    /// </summary>
    public sealed class CycleSchedule
    {
        private readonly List<PhaseSpan> spans;

        public IReadOnlyList<PhaseSpan> Spans => spans;
        public float Duration => spans.Count > 0 ? spans[spans.Count - 1].End : 0f;
        public int StormCount { get; }

        private CycleSchedule(List<PhaseSpan> spans, int stormCount)
        {
            this.spans = spans;
            StormCount = stormCount;
        }

        public static CycleSchedule Build(CycleTimings timings, System.Random random)
        {
            int minStorms = Mathf.Max(0, timings.minStorms);
            int maxStorms = Mathf.Max(minStorms, timings.maxStorms);
            int stormCount = random.Next(minStorms, maxStorms + 1);

            float minStorm = Mathf.Max(1f, timings.minStormDuration);
            float maxStorm = Mathf.Max(minStorm, timings.maxStormDuration);
            var stormDurations = new float[stormCount];
            for (int i = 0; i < stormCount; i++)
                stormDurations[i] = Mathf.Lerp(minStorm, maxStorm, (float)random.NextDouble());

            // Calme réparti en (tempêtes + 1) plages, la première commençant par un moment tranquille garanti.
            float calm = Mathf.Max(1f, timings.calmDuration);
            float quiet = calm * Mathf.Clamp(timings.quietStartFraction, 0f, 0.9f);
            float free = calm - quiet;
            var weights = new float[stormCount + 1];
            float weightSum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 0.2f + (float)random.NextDouble();
                weightSum += weights[i];
            }

            var spans = new List<PhaseSpan>();
            float time = 0f;
            for (int i = 0; i <= stormCount; i++)
            {
                float gap = free * weights[i] / weightSum + (i == 0 ? quiet : 0f);
                spans.Add(new PhaseSpan(CyclePhase.Calm, time, time + gap));
                time += gap;

                if (i < stormCount)
                {
                    spans.Add(new PhaseSpan(CyclePhase.Storm, time, time + stormDurations[i]));
                    time += stormDurations[i];
                }
            }

            float apocalypse = Mathf.Max(1f, timings.apocalypseDuration);
            spans.Add(new PhaseSpan(CyclePhase.Apocalypse, time, time + apocalypse));

            return new CycleSchedule(spans, stormCount);
        }

        /// <summary>Index de la plage qui contient l'instant donné (la dernière si on est au-delà).</summary>
        public int IndexAt(float time)
        {
            for (int i = 0; i < spans.Count; i++)
                if (time < spans[i].End)
                    return i;
            return spans.Count - 1;
        }
    }
}
