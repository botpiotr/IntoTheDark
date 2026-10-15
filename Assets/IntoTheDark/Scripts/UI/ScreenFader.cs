using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IntoTheDark.UI
{
    /// <summary>Voile noir plein écran pour les transitions (mort, réapparition). Temps réel, insensible à la pause.</summary>
    public class ScreenFader : MonoBehaviour
    {
        private Image veil;

        public static ScreenFader Create()
        {
            var canvas = UiFactory.CreateCanvas("ScreenFader", 90);
            var fader = canvas.gameObject.AddComponent<ScreenFader>();
            fader.veil = UiFactory.CreatePanel(canvas.transform, new Color(0f, 0f, 0f, 0f), "Veil");
            fader.veil.raycastTarget = false;
            return fader;
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            float start = veil.color.a;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, alpha, t / duration));
                yield return null;
            }
            SetAlpha(alpha);
        }

        private void SetAlpha(float alpha) => veil.color = new Color(0f, 0f, 0f, alpha);
    }
}
