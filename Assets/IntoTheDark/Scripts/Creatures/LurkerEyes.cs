using IntoTheDark.Lighting;
using UnityEngine;

namespace IntoTheDark.Creatures
{
    /// <summary>
    /// Yeux du rôdeur : ils ne brillent vraiment que lorsqu'il regarde le joueur en face ;
    /// de profil ou de dos, ils s'éteignent presque. Discret tant qu'il ne vous fixe pas.
    /// </summary>
    public class LurkerEyes : MonoBehaviour
    {
        [SerializeField] private Renderer[] eyes = new Renderer[0];
        [SerializeField, Tooltip("Repère du regard (la tête) ; la racine si vide")] private Transform head;
        [SerializeField, Range(0f, 1f), Tooltip("Lueur résiduelle quand il ne regarde pas le joueur")] private float minGlow = 0.06f;
        [SerializeField, Range(0f, 90f), Tooltip("En deçà de cet angle avec la direction du joueur : lueur maximale")]
        private float fullGlowAngle = 15f;
        [SerializeField, Range(0f, 180f), Tooltip("Au-delà de cet angle : lueur résiduelle")] private float noGlowAngle = 55f;
        [SerializeField, Min(0.1f), Tooltip("Vitesse d'allumage / extinction")] private float fadeSpeed = 6f;

        private EmissionControl[] glows;
        private Transform viewer;
        private float glow;

        private void Awake()
        {
            if (head == null) head = transform;
            glows = new EmissionControl[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) glows[i] = new EmissionControl(eyes[i]);
        }

        // Après le flash de coup du rôdeur : les yeux gardent leur propre lueur.
        private void LateUpdate()
        {
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            float target = minGlow;
            if (viewer != null)
            {
                Vector3 toViewer = viewer.position - head.position;
                toViewer.y = 0f;
                Vector3 forward = head.forward;
                forward.y = 0f;
                if (toViewer.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.Angle(forward, toViewer);
                    float facing = 1f - Mathf.InverseLerp(fullGlowAngle, noGlowAngle, angle);
                    target = Mathf.Lerp(minGlow, 1f, facing * facing);
                }
            }
            glow = Mathf.MoveTowards(glow, target, fadeSpeed * Time.deltaTime);
            foreach (var control in glows) control.SetIntensity(glow);
        }
    }
}
