using IntoTheDark.Creatures;
using IntoTheDark.Player;
using NUnit.Framework;

namespace IntoTheDark.Tests
{
    public class CreatureTargetingTests
    {
        [Test]
        public void PrefersPlayer_WhenDistancesAreClose()
        {
            Assert.AreEqual(TargetKind.Player, CreatureTargeting.Choose(10f, 8f, 1.5f));
        }

        [Test]
        public void GoesForBeacon_WhenItIsMuchCloser()
        {
            Assert.AreEqual(TargetKind.Beacon, CreatureTargeting.Choose(20f, 5f, 1.5f));
        }

        [Test]
        public void FallsBackOnWhatever_IsAvailable()
        {
            Assert.AreEqual(TargetKind.Player, CreatureTargeting.Choose(30f, -1f, 1.5f));
            Assert.AreEqual(TargetKind.Beacon, CreatureTargeting.Choose(-1f, 30f, 1.5f));
            Assert.AreEqual(TargetKind.None, CreatureTargeting.Choose(-1f, -1f, 1.5f));
        }
    }

    public class StressMeterTests
    {
        private const float Epsilon = 1e-4f;

        [Test]
        public void RisesInDarkness_FallsSlowlyInLight()
        {
            var stress = new StressMeter(100f, 6f, 3f);
            stress.Tick(10f, isLit: false);
            Assert.AreEqual(60f, stress.Value, Epsilon);
            stress.Tick(10f, isLit: true);
            Assert.AreEqual(30f, stress.Value, Epsilon);
        }

        [Test]
        public void ClampsAndReportsMax()
        {
            var stress = new StressMeter(100f, 6f, 3f);
            stress.Tick(60f, isLit: false);
            Assert.IsTrue(stress.IsMaxed);
            Assert.AreEqual(100f, stress.Value, Epsilon);

            stress.Tick(100f, isLit: true);
            Assert.AreEqual(0f, stress.Value, Epsilon);
        }

        [Test]
        public void Shock_AddsStress()
        {
            var stress = new StressMeter(100f, 6f, 3f);
            stress.Add(25f);
            Assert.AreEqual(0.25f, stress.Normalized, Epsilon);
        }
    }
}
