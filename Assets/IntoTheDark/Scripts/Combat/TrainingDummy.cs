using IntoTheDark.Core;
using UnityEngine;

namespace IntoTheDark.Combat
{
    /// <summary>
    /// Mannequin d'entraînement : encaisse les coups sans jamais casser et réagit comme une créature
    /// (flash blanc, petit bond), au plus une fois toutes les 0,5 s.
    /// </summary>
    [RequireComponent(typeof(HitReaction))]
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [Header("Lecture seule")]
        [SerializeField] private float totalDamage;
        [SerializeField] private float lastHit;

        private HitReaction reaction;

        public float TotalDamage => totalDamage;

        private void Awake() => reaction = GetComponent<HitReaction>();

        public void TakeDamage(float amount, Vector3 source)
        {
            if (amount <= 0f || !reaction.Play(source)) return;
            lastHit = amount;
            totalDamage += amount;
        }
    }
}
