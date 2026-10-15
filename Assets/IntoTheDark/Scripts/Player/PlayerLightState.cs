using System;
using IntoTheDark.Lighting;
using UnityEngine;

namespace IntoTheDark.Player
{
    /// <summary>
    /// Sait si le joueur est éclairé (dans une zone de lumière) ou dans les ténèbres.
    /// Servira au stress, à la régénération de santé et au comportement des créatures.
    /// Pas d'affichage à l'écran : l'état est visible dans l'Inspector pendant le Play.
    /// </summary>
    public class PlayerLightState : MonoBehaviour
    {
        [SerializeField, Tooltip("Hauteur du point testé au-dessus des pieds (milieu du corps)")]
        private float bodyHeight = 1f;

        [Header("État (lecture seule)")]
        [SerializeField] private bool isLit;
        [SerializeField] private bool inBeaconLight;
        [SerializeField] private float secondsInDarkness;
        [SerializeField] private float secondsInLight;

        /// <summary>Dans au moins une zone de lumière (lampe, phare…).</summary>
        public bool IsLit => isLit;
        /// <summary>Dans la lumière d'un phare allumé (protection, soin rapide, recharge).</summary>
        public bool InBeaconLight => inBeaconLight;
        public float SecondsInDarkness => secondsInDarkness;
        public float SecondsInLight => secondsInLight;
        public Vector3 BodyPoint => transform.position + Vector3.up * bodyHeight;

        /// <summary>Appelé quand le joueur passe de la lumière au noir (false) ou l'inverse (true).</summary>
        public event Action<bool> LitChanged;
        public event Action<bool> BeaconLightChanged;

        private void Update()
        {
            Vector3 point = BodyPoint;
            bool litNow = LightZone.IsLit(point);
            bool beaconNow = Beacon.AnyLitBeaconCovers(point);

            if (litNow != isLit)
            {
                isLit = litNow;
                LitChanged?.Invoke(isLit);
            }
            if (beaconNow != inBeaconLight)
            {
                inBeaconLight = beaconNow;
                BeaconLightChanged?.Invoke(inBeaconLight);
            }

            if (isLit)
            {
                secondsInLight += Time.deltaTime;
                secondsInDarkness = 0f;
            }
            else
            {
                secondsInDarkness += Time.deltaTime;
                secondsInLight = 0f;
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = isLit ? new Color(1f, 0.8f, 0.3f) : new Color(0.3f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(BodyPoint, 0.3f);
        }
    }
}
