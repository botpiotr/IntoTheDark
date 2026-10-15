using IntoTheDark.Core;
using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Touche E : utilise l'objet visé au centre de la vue, à portée de main.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, Tooltip("Origine de la visée (rempli automatiquement si vide)")]
        private Transform cameraRoot;
        [SerializeField, Min(0.5f)] private float reach = 2.5f;
        [SerializeField, Min(0f), Tooltip("Tolérance de visée")] private float aimRadius = 0.15f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private PlayerInputHandler input;

        /// <summary>Objet actuellement visé (null si rien).</summary>
        public IInteractable Target { get; private set; }

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            if (cameraRoot == null)
                cameraRoot = transform.Find("CameraRoot");
        }

        private void Update()
        {
            Target = FindTarget();
            if (Target != null && input.InteractPressed && Target.CanInteract(gameObject))
                Target.Interact(gameObject);
        }

        private IInteractable FindTarget()
        {
            if (cameraRoot == null)
                return null;

            var ray = new Ray(cameraRoot.position, cameraRoot.forward);
            if (!Physics.SphereCast(ray, aimRadius, out RaycastHit hit, reach, interactionMask, QueryTriggerInteraction.Collide))
                return null;

            return hit.collider.GetComponentInParent<IInteractable>();
        }
    }
}
