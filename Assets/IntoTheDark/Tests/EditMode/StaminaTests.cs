using IntoTheDark.Player;
using NUnit.Framework;

namespace IntoTheDark.Tests
{
    public class StaminaTests
    {
        private const float Epsilon = 1e-4f;

        private static Stamina NewStamina() => new Stamina(max: 100f, regenPerSecond: 20f, regenDelay: 1f, recoverThreshold: 0.25f);

        [Test]
        public void StartsFull()
        {
            var stamina = NewStamina();
            Assert.AreEqual(100f, stamina.Current, Epsilon);
            Assert.IsFalse(stamina.IsExhausted);
        }

        [Test]
        public void TryConsume_PaysTheCost()
        {
            var stamina = NewStamina();
            Assert.IsTrue(stamina.TryConsume(30f));
            Assert.AreEqual(70f, stamina.Current, Epsilon);
        }

        [Test]
        public void TryConsume_FailsWithoutChangingWhenNotEnough()
        {
            var stamina = NewStamina();
            stamina.TryConsume(80f);
            Assert.IsFalse(stamina.TryConsume(30f));
            Assert.AreEqual(20f, stamina.Current, Epsilon);
        }

        [Test]
        public void Drain_ToZero_Exhausts()
        {
            var stamina = NewStamina();
            Assert.IsFalse(stamina.Drain(150f));
            Assert.AreEqual(0f, stamina.Current, Epsilon);
            Assert.IsTrue(stamina.IsExhausted);
            Assert.IsFalse(stamina.TryConsume(1f));
        }

        [Test]
        public void Regenerates_OnlyAfterDelay()
        {
            var stamina = NewStamina();
            stamina.TryConsume(50f);

            stamina.Tick(0.5f);
            stamina.Tick(0.5f);
            Assert.AreEqual(50f, stamina.Current, Epsilon, "pas de récupération pendant le délai");

            stamina.Tick(1f);
            Assert.AreEqual(70f, stamina.Current, Epsilon);
        }

        [Test]
        public void Exhaustion_EndsAtThreshold()
        {
            var stamina = NewStamina();
            stamina.Drain(100f);

            stamina.Tick(1f);   // délai
            stamina.Tick(1f);   // 20 / 100 : encore épuisé
            Assert.IsTrue(stamina.IsExhausted);

            stamina.Tick(0.5f); // 30 / 100 : au-dessus de 25 %
            Assert.IsFalse(stamina.IsExhausted);
        }

        [Test]
        public void NeverExceedsMax()
        {
            var stamina = NewStamina();
            stamina.Tick(10f);
            Assert.AreEqual(100f, stamina.Current, Epsilon);
        }
    }
}
