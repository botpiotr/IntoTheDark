using IntoTheDark.Core;
using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Possède les actions d'entrée du joueur et expose des valeurs simples aux autres composants.
    /// Les actions sont recréées si besoin (par exemple après une recompilation pendant le Play,
    /// qui efface les objets non sérialisés).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputHandler : MonoBehaviour
    {
        private GameInput input;

        public GameInput Input
        {
            get
            {
                if (input == null)
                {
                    input = new GameInput();
                    if (isActiveAndEnabled)
                        input.Enable();
                }
                return input;
            }
        }

        public Vector2 Move => Input.Move.ReadValue<Vector2>();
        public Vector2 LookMouse => Input.LookMouse.ReadValue<Vector2>();
        public Vector2 LookStick => Input.LookStick.ReadValue<Vector2>();
        public bool JumpPressed => Input.Jump.WasPressedThisFrame();
        public bool SprintHeld => Input.Sprint.IsPressed();
        public bool CrouchHeld => Input.Crouch.IsPressed();
        public bool InteractPressed => Input.Interact.WasPressedThisFrame();
        public bool PausePressed => Input.Pause.WasPressedThisFrame();
        public bool ToggleLampPressed => Input.ToggleLamp.WasPressedThisFrame();

        private void OnEnable() => Input.Enable();
        private void OnDisable() => input?.Disable();

        private void OnDestroy()
        {
            input?.Dispose();
            input = null;
        }
    }
}
