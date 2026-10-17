using UnityEngine;

namespace IntoTheDark.Items
{
    /// <summary>Type d'objet (carreau d'arbalète, ressource, module…). Les données vivent dans un asset.</summary>
    [CreateAssetMenu(menuName = "Into the Dark/Objet", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Objet";
        [SerializeField, Min(1), Tooltip("Nombre maximum d'exemplaires par emplacement")] private int maxStack = 1;

        public string DisplayName => displayName;
        public int MaxStack => Mathf.Max(1, maxStack);

        /// <summary>Pour les tests et la génération de la scène.</summary>
        public void Setup(string name, int stack)
        {
            displayName = name;
            maxStack = Mathf.Max(1, stack);
        }
    }
}
