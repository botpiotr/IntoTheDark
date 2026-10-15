using System;
using UnityEngine;

namespace IntoTheDark.Lighting
{
    /// <summary>
    /// Charge d'éther de la lampe, de 0 (vide) à 1 (pleine). Logique pure, testable.
    /// Provisoire : elle se vide avec le temps. Au jalon 3, la lampe prélèvera 5 éther
    /// en début de cycle, comme prévu au cahier des charges.
    /// </summary>
    [Serializable]
    public class LanternCharge
    {
        [SerializeField, Min(1f), Tooltip("Capacité : secondes de lumière d'une charge pleine (augmentée par les améliorations)")]
        private float burnDuration = 15f;
        [SerializeField, Min(0.1f), Tooltip("Secondes pour remplir une charge vide près d'un phare")]
        private float rechargeDuration = 8f;
        [SerializeField, Range(0.01f, 1f), Tooltip("En dessous : la lampe faiblit et la jauge clignote")]
        private float lowThreshold = 0.2f;
        [SerializeField, Range(0f, 1f)] private float startCharge = 1f;

        private float value;
        private bool initialized;

        public float Value { get { EnsureInitialized(); return value; } }

        /// <summary>
        /// Capacité en secondes de lumière. L'augmenter (amélioration) garde le même nombre
        /// de secondes restantes : la jauge descend d'autant.
        /// </summary>
        public float Capacity
        {
            get => burnDuration;
            set
            {
                float newCapacity = Mathf.Max(1f, value);
                float secondsLeft = Value * burnDuration;
                burnDuration = newCapacity;
                this.value = Mathf.Clamp01(secondsLeft / burnDuration);
            }
        }

        /// <summary>Secondes de lumière restantes.</summary>
        public float SecondsLeft => Value * burnDuration;
        public bool IsEmpty => Value <= 0f;
        public bool IsLow => Value < lowThreshold;

        /// <summary>1 tant que la charge est correcte, puis descend vers 0 sous le seuil bas.</summary>
        public float Strength => IsLow ? Value / lowThreshold : 1f;

        public LanternCharge() { }

        public LanternCharge(float burnDuration, float rechargeDuration, float lowThreshold, float startCharge)
        {
            this.burnDuration = Mathf.Max(1f, burnDuration);
            this.rechargeDuration = Mathf.Max(0.1f, rechargeDuration);
            this.lowThreshold = Mathf.Clamp(lowThreshold, 0.01f, 1f);
            this.startCharge = Mathf.Clamp01(startCharge);
            EnsureInitialized();
        }

        public bool IsFull => Value >= 1f;
        public float RechargeDuration => rechargeDuration;

        /// <summary>La lampe brûle pendant deltaTime secondes.</summary>
        public void Burn(float deltaTime)
        {
            EnsureInitialized();
            value = Mathf.Max(0f, value - deltaTime / burnDuration);
        }

        /// <summary>Ajoute une part de charge (0..1) ; renvoie ce qui a été réellement ajouté.</summary>
        public float Refill(float fraction)
        {
            EnsureInitialized();
            float added = Mathf.Clamp(fraction, 0f, 1f - value);
            value += added;
            return added;
        }

        /// <param name="burning">La lampe est allumée.</param>
        /// <param name="nearLitBeacon">Dans la lumière d'un phare allumé : recharge gratuite, et aucune consommation.</param>
        public void Tick(float deltaTime, bool burning, bool nearLitBeacon)
        {
            EnsureInitialized();
            if (nearLitBeacon)
                value = Mathf.Min(1f, value + deltaTime / rechargeDuration);
            else if (burning)
                value = Mathf.Max(0f, value - deltaTime / burnDuration);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            value = startCharge;
            initialized = true;
        }
    }
}
