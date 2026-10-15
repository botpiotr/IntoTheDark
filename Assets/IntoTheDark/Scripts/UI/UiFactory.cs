using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IntoTheDark.UI
{
    /// <summary>
    /// Construit les écrans hors jeu (menu, game over) par le code : rien à câbler dans l'éditeur.
    /// Style volontairement sobre : noir, texte pâle, accent cyan du phare.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color TextColor = new Color(0.78f, 0.82f, 0.85f);
        public static readonly Color DimTextColor = new Color(0.45f, 0.5f, 0.53f);
        public static readonly Color AccentColor = new Color(0.35f, 0.85f, 1f);

        private static Font font;

        public static Font DefaultFont
        {
            get
            {
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Système d'événements compatible avec le nouveau Input System (souris, clavier, manette).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static Image CreatePanel(Transform parent, Color color, string name = "Panel")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string content, int size, Color color, Vector2 position,
            Vector2 boxSize, FontStyle style = FontStyle.Normal, string name = "Text")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;

            var text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Vector2 position, UnityAction onClick)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(420f, 64f);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(0.35f, 0.85f, 1f, 0.12f);
            colors.selectedColor = new Color(0.35f, 0.85f, 1f, 0.12f);
            colors.pressedColor = new Color(0.35f, 0.85f, 1f, 0.25f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateText(go.transform, label, 34, TextColor, Vector2.zero, rect.sizeDelta, name: "Label");
            return button;
        }
    }
}
