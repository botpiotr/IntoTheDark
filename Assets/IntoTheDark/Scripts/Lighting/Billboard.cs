using UnityEngine;

namespace IntoTheDark.Lighting
{
    /// <summary>Tourne l'objet (un halo plat) face à la caméra.</summary>
    public class Billboard : MonoBehaviour
    {
        private Camera target;

        private void LateUpdate()
        {
            if (target == null)
                target = Camera.main;
            if (target == null)
                return;

            transform.rotation = Quaternion.LookRotation(transform.position - target.transform.position, Vector3.up);
        }
    }
}
