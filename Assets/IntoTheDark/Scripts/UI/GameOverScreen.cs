using System.Collections;
using IntoTheDark.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IntoTheDark.UI
{
    /// <summary>
    /// Écran de fin : fondu au noir, message, cycles survécus, puis retour au menu principal
    /// (sur une touche, ou automatiquement).
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] private float fadeDuration = 2.5f;
        [SerializeField, Tooltip("Délai avant d'accepter une touche")] private float inputDelay = 3f;
        [SerializeField, Tooltip("Retour automatique au menu")] private float autoReturnDelay = 15f;

        public static GameOverScreen Show(int cyclesSurvived)
        {
            var canvas = UiFactory.CreateCanvas("GameOverCanvas", 100);
            var screen = canvas.gameObject.AddComponent<GameOverScreen>();
            screen.StartCoroutine(screen.Run(canvas.transform, cyclesSurvived));
            return screen;
        }

        private IEnumerator Run(Transform root, int cyclesSurvived)
        {
            var veil = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0f), "Veil");
            var message = UiFactory.CreateText(root, "Les ténèbres t'ont englouti.", 64, Color.clear,
                new Vector2(0f, 60f), new Vector2(1600f, 100f), FontStyle.Bold, "Message");
            string cycles = cyclesSurvived <= 1 ? $"{cyclesSurvived} cycle survécu" : $"{cyclesSurvived} cycles survécus";
            var detail = UiFactory.CreateText(root, cycles, 32, Color.clear,
                new Vector2(0f, -20f), new Vector2(1200f, 60f), name: "Cycles");
            var prompt = UiFactory.CreateText(root, "Appuie sur une touche", 26, Color.clear,
                new Vector2(0f, -160f), new Vector2(1200f, 50f), name: "Prompt");

            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / fadeDuration;
                veil.color = new Color(0f, 0f, 0f, k);
                message.color = WithAlpha(UiFactory.TextColor, Mathf.Clamp01(k * 2f - 0.6f));
                detail.color = WithAlpha(UiFactory.DimTextColor, Mathf.Clamp01(k * 2f - 1f));
                yield return null;
            }
            veil.color = Color.black;
            message.color = UiFactory.TextColor;
            detail.color = UiFactory.DimTextColor;

            float waited = 0f;
            while (waited < autoReturnDelay)
            {
                waited += Time.unscaledDeltaTime;
                if (waited >= inputDelay)
                {
                    float blink = 0.5f + 0.5f * Mathf.Sin(waited * 3f);
                    prompt.color = WithAlpha(UiFactory.DimTextColor, blink);
                    if (AnyInputPressed())
                        break;
                }
                yield return null;
            }

            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        private static bool AnyInputPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
