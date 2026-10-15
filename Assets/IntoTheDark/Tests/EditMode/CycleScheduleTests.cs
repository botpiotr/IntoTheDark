using IntoTheDark.Cycle;
using NUnit.Framework;

namespace IntoTheDark.Tests
{
    public class CycleScheduleTests
    {
        private const float Epsilon = 1e-3f;

        [Test]
        public void NormalCycle_RespectsDurationsAndOrder()
        {
            var timings = CycleTimings.Normal();
            for (int seed = 1; seed <= 50; seed++)
            {
                var schedule = CycleSchedule.Build(timings, new System.Random(seed));
                var spans = schedule.Spans;

                Assert.That(schedule.StormCount, Is.InRange(0, 2));
                Assert.AreEqual(CyclePhase.Calm, spans[0].Phase, "un cycle commence par le calme");
                Assert.AreEqual(CyclePhase.Apocalypse, spans[spans.Count - 1].Phase, "un cycle finit par l'apocalypse");
                Assert.AreEqual(120f, spans[spans.Count - 1].Duration, Epsilon);

                float calm = 0f;
                int storms = 0;
                for (int i = 0; i < spans.Count; i++)
                {
                    if (i > 0) Assert.AreEqual(spans[i - 1].End, spans[i].Start, Epsilon, "phases contiguës");
                    if (spans[i].Phase == CyclePhase.Calm) calm += spans[i].Duration;
                    if (spans[i].Phase == CyclePhase.Storm)
                    {
                        storms++;
                        Assert.That(spans[i].Duration, Is.InRange(60f - Epsilon, 120f + Epsilon));
                    }
                }
                Assert.AreEqual(900f, calm, Epsilon, "15 min de calme au total");
                Assert.AreEqual(schedule.StormCount, storms);
            }
        }

        [Test]
        public void FirstStorm_NeverComesInTheQuietStart()
        {
            var timings = CycleTimings.Normal();
            for (int seed = 1; seed <= 50; seed++)
            {
                var schedule = CycleSchedule.Build(timings, new System.Random(seed));
                Assert.That(schedule.Spans[0].Duration, Is.GreaterThanOrEqualTo(900f * timings.quietStartFraction - Epsilon));
            }
        }

        [Test]
        public void TestCycle_IsShortAndHasAStorm()
        {
            var schedule = CycleSchedule.Build(CycleTimings.Test(), new System.Random(3));
            Assert.That(schedule.StormCount, Is.InRange(1, 2));
            Assert.That(schedule.Duration, Is.InRange(110f, 141f));
        }

        [Test]
        public void IndexAt_FindsTheRightPhase()
        {
            var schedule = CycleSchedule.Build(CycleTimings.Normal(), new System.Random(5));
            Assert.AreEqual(0, schedule.IndexAt(0f));
            Assert.AreEqual(schedule.Spans.Count - 1, schedule.IndexAt(schedule.Duration - 0.01f));
            Assert.AreEqual(schedule.Spans.Count - 1, schedule.IndexAt(schedule.Duration + 100f));
        }
    }
}
