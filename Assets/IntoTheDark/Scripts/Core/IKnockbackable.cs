using UnityEngine;

namespace IntoTheDark.Core
{
    /// <summary>Ce qui peut être repoussé par un coup (créatures).</summary>
    public interface IKnockbackable
    {
        /// <param name="push">Déplacement horizontal total (direction × distance, en mètres).</param>
        void Knockback(Vector3 push);
    }
}
