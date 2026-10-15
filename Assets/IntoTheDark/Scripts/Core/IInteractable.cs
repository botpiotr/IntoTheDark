using UnityEngine;

namespace IntoTheDark.Core
{
    /// <summary>
    /// Tout ce qu'on peut utiliser avec la touche E : allumer un phare, ramasser, utiliser…
    /// Le joueur vise l'objet et appuie sur E ; aucun indicateur à l'écran.
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
