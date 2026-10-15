using UnityEngine;
using UnityEngine.InputSystem;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Regard à la première personne : le corps tourne à l'horizontale, CameraRoot à la verticale.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField, Tooltip("Rempli automatiquement si vide")] private Transform cameraRoot;
        [SerializeField, Tooltip("Degrés par pixel de souris")] private float mouseSensitivity = 0.1f;
        [SerializeField, Tooltip("Degrés par seconde au stick")] private float stickSensitivity = 180f;
        [SerializeField] private bool invertY;
        [SerializeField, Range(45f, 89f)] private float pitchLimit = 85f;

        private PlayerInputHandler input;
        private float pitch;

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Max(0.001f, value);
        }

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            if (cameraRoot == null)
                cameraRoot = transform.Find("CameraRoot");
        }

        private void OnEnable() => SetCursorLocked(true);
        private void OnDisable() => SetCursorLocked(false);

        private void Update()
        {
            // Temporaire, en attendant le menu pause : Échap libère la souris, un clic la reprend.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    SetCursorLocked(true);
                return;
            }
            if (input.PausePressed)
            {
                SetCursorLocked(false);
                return;
            }

            Vector2 delta = input.LookMouse * mouseSensitivity
                          + input.LookStick * (stickSensitivity * Time.deltaTime);

            pitch += invertY ? delta.y : -delta.y;
            pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

            transform.Rotate(0f, delta.x, 0f, Space.Self);
            if (cameraRoot != null)
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
