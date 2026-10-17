using System;
using System.Collections.Generic;

namespace IntoTheDark.Items
{
    /// <summary>
    /// Inventaire (logique pure, testable) : un nombre fixe d'emplacements, chacun empile un seul type
    /// d'objet jusqu'à sa taille de pile maximale.
    /// </summary>
    public class ItemContainer
    {
        private readonly ItemStack[] slots;

        public ItemContainer(int slotCount)
        {
            slots = new ItemStack[Math.Max(1, slotCount)];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new ItemStack(null, 0);
        }

        public IReadOnlyList<ItemStack> Slots => slots;
        public event Action Changed;

        public int Count(ItemDefinition item)
        {
            int total = 0;
            foreach (var slot in slots)
                if (!slot.IsEmpty && slot.Item == item)
                    total += slot.Count;
            return total;
        }

        /// <summary>Place combien d'exemplaires de cet objet peuvent encore entrer.</summary>
        public int SpaceFor(ItemDefinition item)
        {
            if (item == null) return 0;
            int space = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) space += item.MaxStack;
                else if (slot.Item == item) space += item.MaxStack - slot.Count;
            }
            return space;
        }

        /// <summary>Ajoute ce qui rentre (piles existantes d'abord). Renvoie ce qui n'a pas pu entrer.</summary>
        public int Add(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return Math.Max(0, amount);
            int left = amount;
            foreach (var slot in slots)
            {
                if (left == 0) break;
                if (slot.IsEmpty || slot.Item != item) continue;
                int moved = Math.Min(left, item.MaxStack - slot.Count);
                slot.Set(item, slot.Count + moved);
                left -= moved;
            }
            foreach (var slot in slots)
            {
                if (left == 0) break;
                if (!slot.IsEmpty) continue;
                int moved = Math.Min(left, item.MaxStack);
                slot.Set(item, moved);
                left -= moved;
            }
            if (left != amount) Changed?.Invoke();
            return left;
        }

        /// <summary>Retire exactement cette quantité, ou rien du tout s'il n'y en a pas assez.</summary>
        public bool TryRemove(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return false;
            if (Count(item) < amount) return false;
            int left = amount;
            for (int i = slots.Length - 1; i >= 0 && left > 0; i--)
            {
                var slot = slots[i];
                if (slot.IsEmpty || slot.Item != item) continue;
                int taken = Math.Min(left, slot.Count);
                slot.Set(item, slot.Count - taken);
                left -= taken;
            }
            Changed?.Invoke();
            return true;
        }
    }
}
