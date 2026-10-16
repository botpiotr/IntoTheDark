using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Santé du joueur. Baisse quand les créatures attaquent (et au stress maximum).
    /// Au-dessus de 80 % de stress, les coups reçus font double dégâts.
    /// Elle ne remonte pas toute seule : un bâtiment de soin viendra plus tard (Heal).
    /// À 0 : mort (GameSession décide de la réapparition ou du game over).
    /// Debug : F9 tue le joueur, F10 rend toute la vie.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Range(0f, 1f), Tooltip("Au-dessus de ce stress, les coups font plus de dégâts")]
        private float stressDamageThreshold = 0.8f;
        [SerializeField, Min(1f)] private float stressDamageMultiplier = 2f;

        [Header("Debug")]
        [SerializeField] private Key debugKillKey = Key.F9;
        [SerializeField] private Key debugHealKey = Key.F10;

        [Header("État (lecture seule)")]
        [SerializeField] private float current;
        [SerializeField] private bool isDead;

        /// <summary>(valeur, maximum)</summary>
        public event Action<float, float> Changed;
        public event Action Died;
        /// <summary>Coup reçu (quantité). Pas déclenché par les pertes continues (stress).</summary>
        public event Action<float> Damaged;
        public event Action Revived;

        public float Current => current;
        public float Max => maxHealth;
        public float Normalized => current / maxHealth;
        public bool IsDead => isDead;
        /// <summary>Le dernier coup reçu a-t-il été amplifié par le stress ? (lu pendant l'événement Damaged)</summary>
        public bool LastHitAmplified { get; private set; }

        private PlayerStress stress;

        private void Awake()
        {
            current = maxHealth;
            stress = GetComponent<PlayerStress>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[debugKillKey].wasPressedThisFrame)
                TakeDamage(current);
            if (keyboard != null && keyboard[debugHealKey].wasPressedThisFrame)
                Heal(maxHealth);
        }

        /// <summary>Coup reçu : retour visuel (flash rouge).</summary>
        public void TakeDamage(float amount)
        {
            if (isDead || amount <= 0f) return;
            LastHitAmplified = stress != null && stress.Normalized > stressDamageThreshold;
            if (LastHitAmplified) amount *= stressDamageMultiplier;
            Damaged?.Invoke(amount);
            Lose(amount);
        }

        /// <summary>Perte continue, sans flash (stress au maximum…).</summary>
        public void Drain(float amount)
        {
            if (isDead || amount <= 0f) return;
            Lose(amount);
        }

        private void Lose(float amount)
        {
            SetCurrent(current - amount);
            if (current <= 0f)
            {
                isDead = true;
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f) return;
            SetCurrent(current + amount);
        }

        public void Revive()
        {
            isDead = false;
            SetCurrent(maxHealth);
            Revived?.Invoke();
        }

        private void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 0f, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }
    }
}
