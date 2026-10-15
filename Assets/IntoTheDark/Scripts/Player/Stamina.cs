using System;
using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Endurance du joueur. Logique pure, sans MonoBehaviour, pour être testable.
    /// Une fois vidée, l'endurance est "épuisée" et ne sert plus tant qu'elle n'est pas
    /// remontée au-dessus d'un seuil.
    /// </summary>
    [Serializable]
    public class Stamina
    {
        [SerializeField, Min(1f)] private float max = 100f;
        [SerializeField, Min(0f), Tooltip("Points récupérés par seconde")] private float regenPerSecond = 20f;
        [SerializeField, Min(0f), Tooltip("Secondes sans effort avant la récupération")] private float regenDelay = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Part à récupérer pour sortir de l'épuisement")] private float recoverThreshold = 0.25f;

        private float current;
        private float delayTimer;
        private bool initialized;

        /// <summary>(valeur actuelle, maximum)</summary>
        public event Action<float, float> Changed;

        public float Max => max;
        public float Current { get { EnsureInitialized(); return current; } }
        public float Normalized => Current / max;
        public bool IsExhausted { get; private set; }

        public Stamina() { }

        public Stamina(float max, float regenPerSecond, float regenDelay, float recoverThreshold = 0.25f)
        {
            this.max = Mathf.Max(1f, max);
            this.regenPerSecond = Mathf.Max(0f, regenPerSecond);
            this.regenDelay = Mathf.Max(0f, regenDelay);
            this.recoverThreshold = Mathf.Clamp01(recoverThreshold);
            EnsureInitialized();
        }

        /// <summary>Coût ponctuel (saut, coup) : payé en entier ou pas du tout.</summary>
        public bool TryConsume(float amount)
        {
            EnsureInitialized();
            if (IsExhausted || current < amount)
                return false;

            SetCurrent(current - amount);
            delayTimer = regenDelay;
            return true;
        }

        /// <summary>Coût continu (course). Renvoie false quand il n'y a plus d'endurance.</summary>
        public bool Drain(float amount)
        {
            EnsureInitialized();
            if (IsExhausted)
                return false;

            SetCurrent(current - amount);
            delayTimer = regenDelay;

            if (current <= 0f)
            {
                IsExhausted = true;
                return false;
            }
            return true;
        }

        public void Tick(float deltaTime)
        {
            EnsureInitialized();

            if (delayTimer > 0f)
            {
                delayTimer -= deltaTime;
                return;
            }

            if (current < max)
                SetCurrent(current + regenPerSecond * deltaTime);

            if (IsExhausted && current >= max * recoverThreshold)
                IsExhausted = false;
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            current = max;
            initialized = true;
        }

        private void SetCurrent(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, max);
            bool changed = !Mathf.Approximately(clamped, current);
            current = clamped;
            if (changed)
                Changed?.Invoke(current, max);
        }
    }
}
