using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Santé du joueur. Baisse quand les créatures attaquent (jalon 4), remonte vite dans la lumière
    /// d'un phare allumé. À 0 : mort (GameSession décide de la réapparition ou du game over).
    /// Debug : F9 tue le joueur.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f), Tooltip("Points rendus par seconde dans la lumière d'un phare allumé")]
        private float beaconRegenPerSecond = 20f;

        [Header("Debug")]
        [SerializeField] private Key debugKillKey = Key.F9;

        [Header("État (lecture seule)")]
        [SerializeField] private float current;
        [SerializeField] private bool isDead;

        private PlayerLightState lightState;

        /// <summary>(valeur, maximum)</summary>
        public event Action<float, float> Changed;
        public event Action Died;
        public event Action Revived;

        public float Current => current;
        public float Max => maxHealth;
        public float Normalized => current / maxHealth;
        public bool IsDead => isDead;

        private void Awake()
        {
            current = maxHealth;
            lightState = GetComponent<PlayerLightState>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[debugKillKey].wasPressedThisFrame)
                TakeDamage(current);

            if (!isDead && lightState != null && lightState.InBeaconLight && current < maxHealth)
                Heal(beaconRegenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (isDead || amount <= 0f) return;
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
