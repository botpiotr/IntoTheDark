using IntoTheDark.Lighting;
using NUnit.Framework;

namespace IntoTheDark.Tests
{
    public class LanternChargeTests
    {
        private const float Epsilon = 1e-4f;

        // Se vide en 100 s, se remplit en 10 s, seuil bas à 20 %.
        private static LanternCharge NewCharge(float start = 1f) => new LanternCharge(100f, 10f, 0.2f, start);

        [Test]
        public void Burns_WhenLitInTheDark()
        {
            var charge = NewCharge();
            charge.Tick(25f, burning: true, nearLitBeacon: false);
            Assert.AreEqual(0.75f, charge.Value, Epsilon);
        }

        [Test]
        public void DoesNotBurn_WhenOff()
        {
            var charge = NewCharge();
            charge.Tick(25f, burning: false, nearLitBeacon: false);
            Assert.AreEqual(1f, charge.Value, Epsilon);
        }

        [Test]
        public void Recharges_AndDoesNotBurn_NearLitBeacon()
        {
            var charge = NewCharge(start: 0.5f);
            charge.Tick(2f, burning: true, nearLitBeacon: true);
            Assert.AreEqual(0.7f, charge.Value, Epsilon);
        }

        [Test]
        public void StaysBetweenZeroAndOne()
        {
            var charge = NewCharge(start: 0.9f);
            charge.Tick(100f, burning: false, nearLitBeacon: true);
            Assert.AreEqual(1f, charge.Value, Epsilon);

            charge.Tick(500f, burning: true, nearLitBeacon: false);
            Assert.AreEqual(0f, charge.Value, Epsilon);
            Assert.IsTrue(charge.IsEmpty);
        }

        [Test]
        public void DefaultCapacity_Is15Seconds()
        {
            var charge = new LanternCharge();
            Assert.AreEqual(15f, charge.Capacity, Epsilon);
            charge.Tick(15f, burning: true, nearLitBeacon: false);
            Assert.IsTrue(charge.IsEmpty);
        }

        [Test]
        public void IncreasingCapacity_KeepsSecondsLeft()
        {
            var charge = NewCharge(start: 0.5f);   // 50 s sur 100
            charge.Capacity = 200f;
            Assert.AreEqual(50f, charge.SecondsLeft, 1e-3f);
            Assert.AreEqual(0.25f, charge.Value, Epsilon);
        }

        [Test]
        public void Burn_EmptiesOverCapacity()
        {
            var charge = NewCharge();
            charge.Burn(50f);
            Assert.AreEqual(0.5f, charge.Value, Epsilon);
            Assert.AreEqual(50f, charge.SecondsLeft, 1e-3f);
        }

        [Test]
        public void Refill_StopsWhenFull()
        {
            var charge = NewCharge(start: 0.5f);
            Assert.AreEqual(0.5f, charge.Refill(0.8f), Epsilon);
            Assert.IsTrue(charge.IsFull);
        }

        [Test]
        public void HalfEmptyBaseLantern_CostsHalfAnAether()
        {
            // Règle : 1 éther pour 15 s de lumière. Lampe de base (15 s) à moitié vide → 0,5 éther.
            var charge = new LanternCharge(15f, 8f, 0.2f, 0.5f);
            const float aetherPerLightSecond = 1f / 15f;
            float cost = (1f - charge.Value) * charge.Capacity * aetherPerLightSecond;
            Assert.AreEqual(0.5f, cost, Epsilon);
        }

        [Test]
        public void Strength_DropsOnlyBelowLowThreshold()
        {
            Assert.AreEqual(1f, NewCharge(start: 0.5f).Strength, Epsilon);
            Assert.AreEqual(0.5f, NewCharge(start: 0.1f).Strength, Epsilon);
            Assert.IsTrue(NewCharge(start: 0.1f).IsLow);
        }
    }
}
