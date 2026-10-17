using IntoTheDark.Items;
using NUnit.Framework;
using UnityEngine;

namespace IntoTheDark.Tests
{
    public class InventoryTests
    {
        private ItemDefinition bolt;
        private ItemDefinition scrap;

        [SetUp]
        public void SetUp()
        {
            bolt = ScriptableObject.CreateInstance<ItemDefinition>();
            bolt.Setup("Carreau", 10);
            scrap = ScriptableObject.CreateInstance<ItemDefinition>();
            scrap.Setup("Ferraille", 5);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(bolt);
            Object.DestroyImmediate(scrap);
        }

        [Test]
        public void Add_StacksUpToMaxStack_ThenUsesNewSlot()
        {
            var container = new ItemContainer(3);
            Assert.AreEqual(0, container.Add(bolt, 14));
            Assert.AreEqual(14, container.Count(bolt));
            Assert.AreEqual(10, container.Slots[0].Count);
            Assert.AreEqual(4, container.Slots[1].Count);
        }

        [Test]
        public void Add_ReturnsWhatDoesNotFit()
        {
            var container = new ItemContainer(1);
            Assert.AreEqual(2, container.Add(bolt, 12));
            Assert.AreEqual(10, container.Count(bolt));
            Assert.AreEqual(0, container.SpaceFor(bolt));
        }

        [Test]
        public void Add_FillsExistingStackBeforeEmptySlot()
        {
            var container = new ItemContainer(3);
            container.Add(bolt, 7);
            container.Add(scrap, 1);
            container.Add(bolt, 3);
            Assert.AreEqual(10, container.Slots[0].Count);
            Assert.IsTrue(container.Slots[2].IsEmpty);
        }

        [Test]
        public void TryRemove_TakesExactAmount_OrNothing()
        {
            var container = new ItemContainer(2);
            container.Add(bolt, 3);
            Assert.IsFalse(container.TryRemove(bolt, 4));
            Assert.AreEqual(3, container.Count(bolt));
            Assert.IsTrue(container.TryRemove(bolt, 1));
            Assert.AreEqual(2, container.Count(bolt));
        }

        [Test]
        public void TryRemove_EmptiedSlot_CanTakeAnotherItem()
        {
            var container = new ItemContainer(1);
            container.Add(bolt, 1);
            Assert.IsTrue(container.TryRemove(bolt, 1));
            Assert.AreEqual(5, container.SpaceFor(scrap));
            Assert.AreEqual(0, container.Add(scrap, 5));
        }

        [Test]
        public void Changed_FiresOnAddAndRemove()
        {
            var container = new ItemContainer(1);
            int changes = 0;
            container.Changed += () => changes++;
            container.Add(bolt, 2);
            container.TryRemove(bolt, 1);
            container.TryRemove(bolt, 5); // refusé : pas de changement
            Assert.AreEqual(2, changes);
        }
    }
}
