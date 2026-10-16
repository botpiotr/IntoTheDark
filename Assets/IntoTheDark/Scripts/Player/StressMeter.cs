using System;
using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Jauge de stress (logique pure, testable) : monte dans le noir, redescend lentement dans la lumière.
    /// </summary>
    [Serializable]
    public class StressMeter
    {
        [SerializeField, Min(1f)] private float max = 100f;
        [SerializeField, Min(0f), Tooltip("Points gagnés par seconde dans le noir")] private float riseInDarkness = 6f;
        [SerializeField, Min(0f), Tooltip("Points perdus par seconde dans la lumière")] private float fallInLight = 8f;

        private float value;

        public float Value => value;
        public float Max => max;
        public float Normalized => value / max;
        public bool IsMaxed => value >= max;

        public StressMeter() { }

        public StressMeter(float max, float riseInDarkness, float fallInLight)
        {
            this.max = Mathf.Max(1f, max);
            this.riseInDarkness = Mathf.Max(0f, riseInDarkness);
            this.fallInLight = Mathf.Max(0f, fallInLight);
        }

        public void Tick(float deltaTime, bool isLit)
        {
            Add(isLit ? -fallInLight * deltaTime : riseInDarkness * deltaTime);
        }

        public void Add(float amount) => value = Mathf.Clamp(value + amount, 0f, max);

        public void Reset() => value = 0f;
    }
}
