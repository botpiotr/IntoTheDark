using IntoTheDark.Aether;
using NUnit.Framework;

namespace IntoTheDark.Tests
{
    public class AetherReserveTests
    {
        private const float Epsilon = 1e-3f;

        [Test]
        public void DefaultDeposit_Has500()
        {
            var reserve = new AetherReserve();
            Assert.AreEqual(500f, reserve.Amount, Epsilon);
            Assert.AreEqual(1f, reserve.Normalized, Epsilon);
        }

        [Test]
        public void TryConsume_IsAllOrNothing()
        {
            var reserve = new AetherReserve(500f, 15f, 1f);
            Assert.IsTrue(reserve.TryConsume(10f));
            Assert.AreEqual(5f, reserve.Amount, Epsilon);
            Assert.IsFalse(reserve.TryConsume(10f));
            Assert.AreEqual(5f, reserve.Amount, Epsilon);
        }

        [Test]
        public void Take_GivesWhatIsLeft()
        {
            var reserve = new AetherReserve(500f, 0.3f, 1f);
            Assert.AreEqual(0.3f, reserve.Take(0.5f), Epsilon);
            Assert.IsTrue(reserve.IsEmpty);
        }

        [Test]
        public void Regenerate_AddsOnePerCycle_UpToCapacity()
        {
            var reserve = new AetherReserve(500f, 499.5f, 1f);
            reserve.Regenerate();
            Assert.AreEqual(500f, reserve.Amount, Epsilon);
        }

        [Test]
        public void ContinuousCycle_ConsumesTenAndProducesOne()
        {
            // Un cycle découpé en 60 000 images : le phare prend 10 et la tuyère rend 1, en continu.
            var reserve = new AetherReserve(500f, 400f, 1f);
            const int frames = 60000;
            const float fraction = 1f / frames;
            for (int i = 0; i < frames; i++)
            {
                reserve.Take(10f * fraction);
                reserve.Regenerate(fraction);
            }
            Assert.AreEqual(391f, reserve.Amount, 0.01f);
        }

        [Test]
        public void ContinuousRegeneration_RefillsAnEmptyDeposit()
        {
            // Quantités minuscules à chaque image : elles ne doivent pas être perdues par arrondi.
            var reserve = new AetherReserve(500f, 0f, 1f);
            const int frames = 60000;
            for (int i = 0; i < frames; i++)
                reserve.Regenerate(1f / frames);
            Assert.AreEqual(1f, reserve.Amount, 0.001f);
        }

        [Test]
        public void ContinuousRegeneration_IsNotLostWhenFull()
        {
            var reserve = new AetherReserve(500f, 499f, 1f);
            const int frames = 60000;
            for (int i = 0; i < frames; i++)
                reserve.Regenerate(1f / frames);
            Assert.AreEqual(500f, reserve.Amount, 0.001f);
        }
    }
}
