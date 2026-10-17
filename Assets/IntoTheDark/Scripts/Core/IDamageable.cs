using UnityEngine;

namespace IntoTheDark.Core
{
    /// <summary>Tout ce qui peut recevoir des coups (créatures, mannequin d'entraînement…).</summary>
    public interface IDamageable
    {
        /// <param name="source">Position d'où vient le coup (recul à l'opposé).</param>
        void TakeDamage(float amount, Vector3 source);
    }
}
