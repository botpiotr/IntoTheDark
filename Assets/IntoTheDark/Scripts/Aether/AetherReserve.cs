using System;
using UnityEngine;

namespace IntoTheDark.Aether
{
    /// <summary>
    /// Réserve d'éther d'un gisement. Logique pure, testable.
    /// Valeurs fractionnaires permises (la lampe peut prendre 0,5 éther). Le calcul interne est en
    /// double précision : la régénération continue ajoute de toutes petites quantités à chaque image.
    /// </summary>
    [Serializable]
    public class AetherReserve
    {
        private const float Epsilon = 1e-4f;

        [SerializeField, Min(0f)] private float capacity = 500f;
        [SerializeField, Min(0f)] private float startAmount = 500f;
        [SerializeField, Min(0f), Tooltip("Éther produit par cycle, en continu (augmentable par des améliorations)")]
        private float regenPerCycle = 1f;

        private double amount;
        private bool initialized;

        public event Action<float> Changed;

        public float Capacity => capacity;
        public float Amount { get { EnsureInitialized(); return (float)amount; } }
        public float Normalized => capacity > 0f ? Amount / capacity : 0f;
        public bool IsEmpty => Amount <= Epsilon;
        /// <summary>Éther produit par cycle (augmentable par des améliorations).</summary>
        public float RegenPerCycle
        {
            get => regenPerCycle;
            set => regenPerCycle = Mathf.Max(0f, value);
        }

        public AetherReserve() { }

        public AetherReserve(float capacity, float startAmount, float regenPerCycle)
        {
            this.capacity = Mathf.Max(0f, capacity);
            this.startAmount = Mathf.Clamp(startAmount, 0f, this.capacity);
            this.regenPerCycle = Mathf.Max(0f, regenPerCycle);
            EnsureInitialized();
        }

        /// <summary>Prend exactement cette quantité, ou rien si elle n'est pas disponible.</summary>
        public bool TryConsume(float quantity)
        {
            EnsureInitialized();
            if (quantity <= 0f) return true;
            if (amount + Epsilon < quantity) return false;
            SetAmount(amount - quantity);
            return true;
        }

        /// <summary>Prend jusqu'à cette quantité et renvoie ce qui a été réellement pris.</summary>
        public float Take(float quantity)
        {
            EnsureInitialized();
            double taken = System.Math.Min(System.Math.Max(quantity, 0.0), amount);
            if (taken > 0.0) SetAmount(amount - taken);
            return (float)taken;
        }

        public void Add(float quantity)
        {
            EnsureInitialized();
            if (quantity > 0f) SetAmount(amount + quantity);
        }

        /// <summary>Régénération d'un cycle complet.</summary>
        public void Regenerate() => Add(regenPerCycle);

        /// <summary>Régénération continue : la part de cycle écoulée (0..1) produit autant d'éther.</summary>
        public void Regenerate(float cycleFraction)
        {
            EnsureInitialized();
            if (cycleFraction > 0f) SetAmount(amount + (double)regenPerCycle * cycleFraction);
        }

        private void SetAmount(double value)
        {
            amount = System.Math.Min(System.Math.Max(value, 0.0), capacity);
            Changed?.Invoke((float)amount);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            amount = Mathf.Clamp(startAmount, 0f, capacity);
            initialized = true;
        }
    }
}
