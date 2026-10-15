using IntoTheDark.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IntoTheDark.UI
{
    /// <summary>
    /// Menu principal : Nouvelle partie, Options (à venir), Quitter.
    /// L'écran est construit par le code au lancement de la scène.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private GameObject mainPanel;
        private GameObject optionsPanel;
        private Button firstButton;
        private Button optionsBackButton;
        private Text title;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("MainMenuCanvas", 0);
            UiFactory.CreatePanel(canvas.transform, Color.black, "Background");

            title = UiFactory.CreateText(canvas.transform, "INTO THE DARK", 110, UiFactory.AccentColor,
                new Vector2(0f, 260f), new Vector2(1600f, 160f), FontStyle.Bold, "Title");

            mainPanel = UiFactory.CreatePanel(canvas.transform, new Color(0f, 0f, 0f, 0f), "Main").gameObject;
            firstButton = UiFactory.CreateButton(mainPanel.transform, "Nouvelle partie", new Vector2(0f, 20f), NewGame);
            UiFactory.CreateButton(mainPanel.transform, "Options", new Vector2(0f, -70f), ShowOptions);
            UiFactory.CreateButton(mainPanel.transform, "Quitter", new Vector2(0f, -160f), Quit);

            optionsPanel = UiFactory.CreatePanel(canvas.transform, new Color(0f, 0f, 0f, 0f), "Options").gameObject;
            UiFactory.CreateText(optionsPanel.transform, "Options", 48, UiFactory.TextColor,
                new Vector2(0f, 60f), new Vector2(900f, 80f), FontStyle.Bold);
            UiFactory.CreateText(optionsPanel.transform, "Bientôt : sensibilité de la souris, volume, affichage.", 28,
                UiFactory.DimTextColor, new Vector2(0f, -10f), new Vector2(1200f, 60f));
            optionsBackButton = UiFactory.CreateButton(optionsPanel.transform, "Retour", new Vector2(0f, -120f), ShowMain);

            ShowMain();
        }

        private void Update()
        {
            // Lueur du titre qui vacille doucement, comme le foyer d'un phare.
            if (title != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.PerlinNoise(0.37f, Time.unscaledTime * 0.8f);
                var c = UiFactory.AccentColor;
                title.color = new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f);
            }
        }

        private void NewGame() => SceneManager.LoadScene(SceneNames.Game);

        private void ShowOptions()
        {
            mainPanel.SetActive(false);
            optionsPanel.SetActive(true);
            Select(optionsBackButton);
        }

        private void ShowMain()
        {
            optionsPanel.SetActive(false);
            mainPanel.SetActive(true);
            Select(firstButton);
        }

        private static void Select(Button button)
        {
            if (EventSystem.current != null && button != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
