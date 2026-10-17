using System;
using UnityEngine;

namespace IntoTheDark.Items
{
    /// <summary>Un emplacement d'inventaire : un type d'objet et une quantité.</summary>
    [Serializable]
    public class ItemStack
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(0)] private int count;

        public ItemStack(ItemDefinition item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public ItemDefinition Item => item;
        public int Count => count;
        public bool IsEmpty => item == null || count <= 0;

        internal void Set(ItemDefinition newItem, int newCount)
        {
            item = newCount > 0 ? newItem : null;
            count = Mathf.Max(0, newCount);
        }
    }
}
