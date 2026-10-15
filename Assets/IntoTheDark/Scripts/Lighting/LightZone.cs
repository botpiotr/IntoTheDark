using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDark.Lighting
{
    /// <summary>
    /// Sphère de lumière qui repousse les ténèbres (lampe, phare…).
    /// À l'intérieur, on voit et on est protégé ; au-delà, le brouillard noir cache tout.
    /// Toutes les zones actives sont transmises au shader par DarknessSettings.
    /// </summary>
    [ExecuteAlways]
    public class LightZone : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("Rayon de la zone éclairée, en mètres")]
        private float radius = 5f;

        [SerializeField, Tooltip("Couleur du liseré lumineux à la limite de la zone")]
        private Color edgeColor = new Color(1f, 0.75f, 0.45f);

        private static readonly List<LightZone> active = new List<LightZone>();

        public static IReadOnlyList<LightZone> Active => active;

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.1f, value);
        }

        public Vector3 Center => transform.position;

        public Color EdgeColor
        {
            get => edgeColor;
            set => edgeColor = value;
        }

        public bool Contains(Vector3 point) => (point - Center).sqrMagnitude <= radius * radius;

        /// <summary>Ce point est-il dans au moins une zone éclairée ?</summary>
        public static bool IsLit(Vector3 point)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].Contains(point))
                    return true;
            return false;
        }

        private void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(Center, radius);
        }
    }
}
