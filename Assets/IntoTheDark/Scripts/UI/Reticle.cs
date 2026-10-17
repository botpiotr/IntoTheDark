using UnityEngine;
using UnityEngine.UI;

namespace IntoTheDark.UI
{
    /// <summary>Réticule : un simple point blanc au centre de l'écran.</summary>
    public class Reticle : MonoBehaviour
    {
        [SerializeField, Min(1f), Tooltip("Diamètre en pixels (écran 1080p)")] private float size = 5f;
        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.85f);

        private void Awake()
        {
            var canvas = UiFactory.CreateCanvas("Reticle", 40);
            canvas.transform.SetParent(transform, false);

            var go = new GameObject("Dot", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.sprite = CreateDot(32);
            image.color = color;
            image.raycastTarget = false;
        }

        private static Sprite CreateDot(int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[resolution * resolution];
            float r = resolution * 0.5f;
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }
    }
}
