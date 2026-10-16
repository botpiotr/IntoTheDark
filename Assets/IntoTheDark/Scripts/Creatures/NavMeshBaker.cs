using Unity.AI.Navigation;
using UnityEngine;

namespace IntoTheDark.Creatures
{
    /// <summary>
    /// Calcule le maillage de navigation des créatures au lancement de la scène
    /// (rapide sur une petite carte, et toujours à jour avec la géométrie).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshBaker : MonoBehaviour
    {
        private void Awake() => GetComponent<NavMeshSurface>().BuildNavMesh();
    }
}
