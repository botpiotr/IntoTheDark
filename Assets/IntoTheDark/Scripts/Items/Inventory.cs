using System;
using UnityEngine;

namespace IntoTheDark.Items
{
    /// <summary>Inventaire du joueur. Pas d'écran pour l'instant : les quantités se lisent sur les objets du monde.</summary>
    public class Inventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int slotCount = 12;
        [SerializeField, Tooltip("Contenu au début de la partie")] private ItemStack[] startingItems = Array.Empty<ItemStack>();

        private ItemContainer container;

        public ItemContainer Container
        {
            get
            {
                if (container == null)
                {
                    container = new ItemContainer(slotCount);
                    foreach (var stack in startingItems)
                        if (stack != null && !stack.IsEmpty)
                            container.Add(stack.Item, stack.Count);
                }
                return container;
            }
        }

        public event Action Changed
        {
            add => Container.Changed += value;
            remove => Container.Changed -= value;
        }

        public int Count(ItemDefinition item) => Container.Count(item);
        public int Add(ItemDefinition item, int amount) => Container.Add(item, amount);
        public bool TryRemove(ItemDefinition item, int amount) => Container.TryRemove(item, amount);
        public bool HasRoomFor(ItemDefinition item) => Container.SpaceFor(item) > 0;

        private void Awake() => _ = Container;
    }
}
