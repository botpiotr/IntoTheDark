using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Déplacement à la première personne : marche, course (endurance), saut, accroupi.
    /// Le pivot de l'objet est aux pieds du joueur.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Références")]
        [SerializeField, Tooltip("Objet enfant qui porte la caméra (rempli automatiquement si vide)")]
        private Transform cameraRoot;

        [Header("Vitesses (m/s)")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float crouchSpeed = 1.8f;
        [SerializeField, Tooltip("Réactivité au sol (plus haut = plus nerveux)")] private float acceleration = 12f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.3f;

        [Header("Saut et gravité")]
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -20f;

        [Header("Accroupi")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField, Tooltip("Mètres par seconde")] private float crouchTransitionSpeed = 6f;
        [SerializeField, Tooltip("Distance entre le haut de la tête et les yeux")] private float eyeOffsetFromTop = 0.15f;
        [SerializeField, Tooltip("Ce qui empêche de se relever (plafonds bas)")] private LayerMask headroomMask = ~0;

        [Header("Endurance")]
        [SerializeField] private Stamina stamina = new Stamina();
        [SerializeField] private float sprintCostPerSecond = 15f;
        [SerializeField] private float jumpCost = 10f;

        private CharacterController controller;
        private PlayerInputHandler input;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float currentHeight;

        public Stamina Stamina => stamina;
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
        /// <summary>Figé (choc de stress…) : ne peut ni marcher ni sauter, mais peut regarder autour.</summary>
        public bool IsFrozen => frozenTimer > 0f;

        private float frozenTimer;

        /// <summary>Fige le joueur sur place pendant cette durée.</summary>
        public void Freeze(float duration)
        {
            frozenTimer = Mathf.Max(frozenTimer, duration);
            planarVelocity = Vector3.zero;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputHandler>();
            if (cameraRoot == null)
                cameraRoot = transform.Find("CameraRoot");

            currentHeight = standingHeight;
            ApplyHeight(currentHeight);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            frozenTimer = Mathf.Max(0f, frozenTimer - dt);

            UpdateCrouch(dt);
            UpdatePlanarVelocity(dt);
            UpdateVerticalVelocity(dt);

            CollisionFlags flags = controller.Move(Velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f; // tête contre le plafond

            stamina.Tick(dt);
        }

        private void UpdatePlanarVelocity(float dt)
        {
            Vector2 move = IsFrozen ? Vector2.zero : input.Move;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            Vector3 wishDirection = transform.right * move.x + transform.forward * move.y;

            bool wantsSprint = input.SprintHeld && move.y > 0.1f && !IsCrouching && controller.isGrounded;
            IsSprinting = wantsSprint && stamina.Drain(sprintCostPerSecond * dt);

            float speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 target = wishDirection * speed;

            float response = controller.isGrounded ? acceleration : acceleration * airControl;
            planarVelocity = IsFrozen ? Vector3.zero : Vector3.Lerp(planarVelocity, target, 1f - Mathf.Exp(-response * dt));
        }

        private void UpdateVerticalVelocity(float dt)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // reste collé au sol

            if (!IsFrozen && input.JumpPressed && controller.isGrounded && !IsCrouching && stamina.TryConsume(jumpCost))
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);

            verticalVelocity += gravity * dt;
        }

        private void UpdateCrouch(float dt)
        {
            if (input.CrouchHeld)
                IsCrouching = true;
            else if (IsCrouching && HasHeadroom())
                IsCrouching = false;

            float targetHeight = IsCrouching ? crouchHeight : standingHeight;
            if (Mathf.Approximately(currentHeight, targetHeight))
                return;

            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, crouchTransitionSpeed * dt);
            ApplyHeight(currentHeight);
        }

        private bool HasHeadroom()
        {
            float radius = controller.radius * 0.95f;
            Vector3 topSphereCenter = transform.position + Vector3.up * (currentHeight - controller.radius);
            float distance = standingHeight - currentHeight + 0.05f;
            return !Physics.SphereCast(topSphereCenter, radius, Vector3.up, out _, distance,
                headroomMask, QueryTriggerInteraction.Ignore);
        }

        private void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            if (cameraRoot != null)
                cameraRoot.localPosition = new Vector3(0f, height - eyeOffsetFromTop, 0f);
        }
    }
}
