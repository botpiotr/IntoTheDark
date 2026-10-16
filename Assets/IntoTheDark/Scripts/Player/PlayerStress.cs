using System;
using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Stress du joueur. Chaque coup reçu ajoute 40 % du maximum, quelle que soit la situation.
    /// En atteignant le maximum : choc (le joueur est figé 0,5 s, flash noir et blanc géré par StressEffects).
    /// Au maximum, il draine la vie. Remis à zéro à la réapparition.
    /// </summary>
    [RequireComponent(typeof(PlayerLightState))]
    public class PlayerStress : MonoBehaviour
    {
        [SerializeField] private StressMeter meter = new StressMeter();
        [SerializeField, Min(0f), Tooltip("Vie perdue par seconde quand le stress est au maximum")]
        private float healthDrainAtMax = 1.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Part du stress max ajoutée à chaque coup reçu")]
        private float stressPerHit = 0.4f;
        [SerializeField, Min(0f), Tooltip("Durée du choc en atteignant le stress maximum (joueur figé)")]
        private float shockDuration = 0.5f;

        [Header("État (lecture seule)")]
        [SerializeField] private float current;

        private PlayerLightState lightState;
        private PlayerHealth health;
        private PlayerMovement movement;
        private bool wasMaxed;

        public float Normalized => meter.Normalized;
        public bool IsMaxed => meter.IsMaxed;
        public event Action<float> Changed;
        /// <summary>Le stress vient d'atteindre le maximum (durée du choc).</summary>
        public event Action<float> Shocked;
        public float ShockDuration => shockDuration;

        private void Awake()
        {
            lightState = GetComponent<PlayerLightState>();
            health = GetComponent<PlayerHealth>();
            movement = GetComponent<PlayerMovement>();
            if (health != null)
            {
                health.Revived += OnRevived;
                health.Damaged += OnDamaged;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Revived -= OnRevived;
                health.Damaged -= OnDamaged;
            }
        }

        private void OnRevived()
        {
            meter.Reset();
            wasMaxed = false;
        }

        private void OnDamaged(float amount) => meter.Add(meter.Max * stressPerHit);

        private void Update()
        {
            if (health != null && health.IsDead) return;

            meter.Tick(Time.deltaTime, lightState.IsLit);

            bool maxed = meter.IsMaxed;
            if (maxed && !wasMaxed)
            {
                if (movement != null) movement.Freeze(shockDuration);
                Shocked?.Invoke(shockDuration);
            }
            wasMaxed = maxed;
            if (meter.IsMaxed && health != null)
                health.Drain(healthDrainAtMax * Time.deltaTime);

            current = meter.Value;
            Changed?.Invoke(meter.Normalized);
        }

        /// <summary>Choc ponctuel (coup reçu, projectile…).</summary>
        public void AddStress(float amount) => meter.Add(amount);
    }
}
