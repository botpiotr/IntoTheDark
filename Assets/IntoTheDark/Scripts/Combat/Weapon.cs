using UnityEngine;

namespace IntoTheDark.Combat
{
    /// <summary>
    /// Arme tenue en main droite. Le WeaponHolder la sort, la range et transmet le clic gauche.
    /// Les animations propres à l'arme bougent son enfant "model" ; le holder, lui, bouge la racine.
    /// </summary>
    public abstract class Weapon : MonoBehaviour
    {
        [SerializeField, Tooltip("Partie animée de l'arme (coup, recul, rechargement)")] protected Transform model;

        protected WeaponHolder Holder { get; private set; }

        public virtual void Init(WeaponHolder holder)
        {
            Holder = holder;
            if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        }

        /// <summary>Clic gauche (appui).</summary>
        public abstract void Use();

        /// <summary>Clic gauche maintenu (après l'appui). Par défaut : rien.</summary>
        public virtual void UseHeld() { }

        /// <summary>L'arme vient d'être sortie (elle est maintenant active).</summary>
        public virtual void OnEquipped() { }

        /// <summary>L'arme va être rangée (elle sera désactivée).</summary>
        public virtual void OnUnequipped() { }
    }
}
