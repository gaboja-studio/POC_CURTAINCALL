using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class FinalSawTimelineTests
    {
        [Test]
        public void FirstEntry_OnlyStartsOnce_AndLateEntryOrLaunchCannotRestartWarning()
        {
            var timeline = new FinalSawTimeline();
            AssertDormant(FinalSawTimeline.Sample(timeline.Started, timeline.StartedAt, 12, 2, 4));
            Assert.That(timeline.TryStart(12), Is.True);
            AssertSample(Sample(timeline, 12), FinalSawPhase.Warning, 0, 0, 2);
            Assert.That(timeline.TryStart(15), Is.False);
            Assert.That(timeline.TryStart(20), Is.False);
            Assert.That(timeline.StartedAt, Is.EqualTo(12));
            AssertSample(Sample(timeline, 15), FinalSawPhase.Active, 0, 0.25f, 3);
            AssertSample(Sample(timeline, 20), FinalSawPhase.Active, 1, 0, 4);
        }

        [Test]
        public void ExactBoundaries_RemoveOldSawBeforeNextFullWarning()
        {
            AssertSample(At(12), FinalSawPhase.Warning, 0, 0, 2);
            AssertSample(At(13.999), FinalSawPhase.Warning, 0, 0, 0.001);
            AssertSample(At(14), FinalSawPhase.Active, 0, 0, 4);
            AssertSample(At(17.999), FinalSawPhase.Active, 0, 0.99975f, 0.001);
            AssertSample(At(18), FinalSawPhase.Warning, 1, 0, 2);
            AssertSample(At(19.999), FinalSawPhase.Warning, 1, 0, 0.001);
            AssertSample(At(20), FinalSawPhase.Active, 1, 0, 4);
        }

        [Test]
        public void EachCycle_HasOneForwardConstantSpeedTravelAndAnEmptyWarningGap()
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                double start = 12 + cycle * 6;
                for (int step = 0; step < 8; step++)
                    AssertSample(At(start + step * 0.25), FinalSawPhase.Warning, cycle, 0, 2 - step * 0.25);

                // 동일 시간 간격에 동일 거리만 이동하며, 한 주기에는 하나의 Active 구간만 존재한다.
                for (int step = 0; step < 16; step++)
                    AssertSample(At(start + 2 + step * 0.25), FinalSawPhase.Active, cycle,
                        step / 16f, 4 - step * 0.25);

                AssertSample(At(start + 6), FinalSawPhase.Warning, cycle + 1, 0, 2);
            }
        }

        [Test]
        public void DelayedSnapshotOrSkippedFrames_UsesOriginalEpochWithoutExtendingCycle()
        {
            var timeline = new FinalSawTimeline();
            timeline.TryStart(12);
            // 중간 Tick 없이도 166번째 주기의 정확한 위치를 복원한다.
            var host = Sample(timeline, 1012);
            var lateClient = FinalSawTimeline.Sample(true, 12, 1012, 2, 4);
            AssertSample(host, FinalSawPhase.Active, 166, 0.5f, 2);
            AssertSample(lateClient, host.Phase, host.CycleIndex, host.Travel01, host.SecondsToNext);
            AssertSample(At(1014), FinalSawPhase.Warning, 167, 0, 2);
        }

        [Test]
        public void Reset_OnlyExplicitResetAllowsANewStart()
        {
            var timeline = new FinalSawTimeline();
            timeline.TryStart(12);
            timeline.Reset();
            Assert.That(timeline.Started, Is.False);
            Assert.That(timeline.StartedAt, Is.Zero);
            AssertDormant(Sample(timeline, 30));
            Assert.That(timeline.TryStart(30), Is.True);
            AssertSample(Sample(timeline, 30), FinalSawPhase.Warning, 0, 0, 2);
        }

        [Test]
        public void ZeroEpoch_IsValid_AndAFutureEpochHasNoHazard()
        {
            var timeline = new FinalSawTimeline();
            Assert.That(timeline.TryStart(0), Is.True);
            AssertSample(Sample(timeline, 0), FinalSawPhase.Warning, 0, 0, 2);
            AssertDormant(At(11.999));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        public void InvalidStart_DoesNotConsumeFirstEntry(double now)
        {
            var timeline = new FinalSawTimeline();
            Assert.That(timeline.TryStart(now), Is.False);
            Assert.That(timeline.Started, Is.False);
            Assert.That(timeline.StartedAt, Is.Zero);
            Assert.That(timeline.TryStart(12), Is.True);
            Assert.That(timeline.TryStart(now), Is.False);
            Assert.That(timeline.StartedAt, Is.EqualTo(12));
        }

        [TestCase(double.NaN, 12d)]
        [TestCase(double.PositiveInfinity, 12d)]
        [TestCase(double.NegativeInfinity, 12d)]
        [TestCase(-1d, 12d)]
        [TestCase(12d, double.NaN)]
        [TestCase(12d, double.PositiveInfinity)]
        [TestCase(12d, double.NegativeInfinity)]
        [TestCase(12d, -1d)]
        [TestCase(0d, double.MaxValue)]
        public void InvalidOrUnrepresentableTime_NeverProducesAHazard(double startedAt, double now)
        {
            AssertDormant(FinalSawTimeline.Sample(true, startedAt, now, 2, 4));
        }

        [TestCase(0f, 4f)]
        [TestCase(-1f, 4f)]
        [TestCase(float.NaN, 4f)]
        [TestCase(float.PositiveInfinity, 4f)]
        [TestCase(float.NegativeInfinity, 4f)]
        [TestCase(2f, 0f)]
        [TestCase(2f, -1f)]
        [TestCase(2f, float.NaN)]
        [TestCase(2f, float.PositiveInfinity)]
        [TestCase(2f, float.NegativeInfinity)]
        [TestCase(float.MaxValue, float.Epsilon)]
        [TestCase(float.Epsilon, float.MaxValue)]
        public void InvalidOrCollapsedDurations_NeverRemoveTheWarningGap(float warning, float travel)
        {
            AssertDormant(FinalSawTimeline.Sample(true, 12, 15, warning, travel));
        }

        static FinalSawSample At(double now) => FinalSawTimeline.Sample(true, 12, now, 2, 4);
        static FinalSawSample Sample(FinalSawTimeline timeline, double now) =>
            FinalSawTimeline.Sample(timeline.Started, timeline.StartedAt, now, 2, 4);

        static void AssertDormant(FinalSawSample sample) => AssertSample(sample, FinalSawPhase.Dormant, -1, 0, 0);

        static void AssertSample(FinalSawSample sample, FinalSawPhase phase, long cycle, float travel01, double secondsToNext)
        {
            Assert.That(sample.Phase, Is.EqualTo(phase));
            Assert.That(sample.CycleIndex, Is.EqualTo(cycle));
            Assert.That(sample.Travel01, Is.EqualTo(travel01).Within(0.000001));
            Assert.That(sample.SecondsToNext, Is.EqualTo(secondsToNext).Within(0.000001));
        }
    }
}
