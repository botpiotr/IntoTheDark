using System;
using UnityEngine.InputSystem;

namespace IntoTheDark.Core
{
    /// <summary>
    /// Actions de jeu définies en code (clavier/souris + manette).
    /// Le Input System lit les touches clavier par position physique :
    /// "&lt;Keyboard&gt;/w" est la touche Z sur un clavier AZERTY, "a" est Q.
    /// D'où ZQSD en jeu avec des bindings écrits en WASD.
    /// </summary>
    public sealed class GameInput : IDisposable
    {
        public InputActionMap Gameplay { get; }

        public InputAction Move { get; }
        public InputAction LookMouse { get; }
        public InputAction LookStick { get; }
        public InputAction Jump { get; }
        public InputAction Sprint { get; }
        public InputAction Crouch { get; }
        public InputAction Interact { get; }
        public InputAction Pause { get; }
        public InputAction ToggleLamp { get; }

        public GameInput()
        {
            Gameplay = new InputActionMap("Gameplay");

            Move = Gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")      // Z en AZERTY
                .With("Down", "<Keyboard>/s")    // S
                .With("Left", "<Keyboard>/a")    // Q en AZERTY
                .With("Right", "<Keyboard>/d");  // D
            Move.AddBinding("<Gamepad>/leftStick");

            LookMouse = Gameplay.AddAction("LookMouse", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            LookStick = Gameplay.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Crouch = Button("Crouch", "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            ToggleLamp = Button("ToggleLamp", "<Keyboard>/l", "<Gamepad>/dpad/up");
        }

        private InputAction Button(string name, params string[] bindings)
        {
            var action = Gameplay.AddAction(name, InputActionType.Button);
            foreach (var binding in bindings)
                action.AddBinding(binding);
            return action;
        }

        public void Enable() => Gameplay.Enable();
        public void Disable() => Gameplay.Disable();
        public void Dispose() => Gameplay.Dispose();
    }
}
