using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class LiftBoardingStateTests
    {
        [Test]
        public void OneEligibleRider_BecomesReadyAfterUninterruptedDefaultDelay()
        {
            var boarding = new LiftBoardingState();
            Assert.IsFalse(boarding.IsCounting);
            Assert.That(boarding.Rider, Is.EqualTo(CannonSeatState.Nobody));
            Assert.IsFalse(boarding.Ready(100));
            Assert.IsTrue(boarding.Tick(7, true, 10));
            Assert.IsTrue(boarding.IsCounting);
            Assert.That(boarding.Rider, Is.EqualTo(7ul));
            Assert.That(boarding.BoardedAt, Is.EqualTo(10));
            Assert.IsFalse(boarding.Tick(7, true, 11));
            Assert.IsFalse(boarding.Ready(11.499));
            Assert.IsTrue(boarding.Ready(11.5));
            Assert.IsFalse(boarding.Tick(7, true, 11.5));
            Assert.IsTrue(boarding.Ready(20));
            Assert.That(boarding.BoardedAt, Is.EqualTo(10));
        }

        [TestCase(ulong.MaxValue, true)]
        [TestCase(ulong.MaxValue, false)]
        [TestCase(7ul, false)]
        public void NoSoleRiderOrLostEligibility_CancelsAndRequiresFullNewDelay(ulong rider, bool eligible)
        {
            var boarding = new LiftBoardingState();
            Assert.IsFalse(boarding.Tick(rider, eligible, 0));
            boarding.Tick(7, true, 10);
            Assert.IsTrue(boarding.Tick(rider, eligible, 11.4));
            Assert.IsFalse(boarding.IsCounting);
            Assert.That(boarding.Rider, Is.EqualTo(CannonSeatState.Nobody));
            Assert.That(boarding.BoardedAt, Is.Zero);
            Assert.IsFalse(boarding.Ready(12));
            Assert.IsFalse(boarding.Tick(rider, eligible, 12));
            Assert.IsTrue(boarding.Tick(7, true, 13));
            Assert.IsFalse(boarding.Ready(14.499));
            Assert.IsTrue(boarding.Ready(14.5));
        }

        [Test]
        public void RiderReplacement_StartsNewCountdownWithoutInheritingElapsedTime()
        {
            var boarding = new LiftBoardingState();
            boarding.Tick(7, true, 10);
            Assert.IsTrue(boarding.Ready(12));
            Assert.IsTrue(boarding.Tick(8, true, 12));
            Assert.That(boarding.Rider, Is.EqualTo(8ul));
            Assert.That(boarding.BoardedAt, Is.EqualTo(12));
            Assert.IsFalse(boarding.Ready(12));
            Assert.IsFalse(boarding.Ready(13.499));
            Assert.IsTrue(boarding.Ready(13.5));
        }

        [Test]
        public void Reset_ClearsReadyStateAndSupportsNewRoundAtTimeZero()
        {
            var boarding = new LiftBoardingState();
            boarding.Tick(7, true, 10);
            boarding.Reset();
            Assert.IsFalse(boarding.IsCounting);
            Assert.That(boarding.Rider, Is.EqualTo(CannonSeatState.Nobody));
            Assert.That(boarding.BoardedAt, Is.Zero);
            Assert.IsFalse(boarding.Ready(100));
            Assert.IsTrue(boarding.Tick(7, true, 0));
            Assert.IsFalse(boarding.Ready(1.499));
            Assert.IsTrue(boarding.Ready(1.5));
        }

        [Test]
        public void ConfiguredDelayAndEarlierClock_UseCurrentRidersBoardingTime()
        {
            var boarding = new LiftBoardingState();
            boarding.Tick(7, true, 10, 2f);
            Assert.IsFalse(boarding.Ready(9, 0));
            Assert.IsFalse(boarding.Tick(7, true, 9, 2f));
            Assert.That(boarding.BoardedAt, Is.EqualTo(10));
            Assert.IsFalse(boarding.Ready(11.999, 2f));
            Assert.IsTrue(boarding.Ready(12, 2f));
            Assert.IsTrue(boarding.Ready(10, 0));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidClock_CannotStartReplaceCancelOrMakeReady(double now)
        {
            var boarding = new LiftBoardingState();
            Assert.IsFalse(boarding.Tick(7, true, now));
            Assert.IsFalse(boarding.IsCounting);
            boarding.Tick(7, true, 10);
            Assert.IsFalse(boarding.Tick(8, true, now));
            Assert.IsFalse(boarding.Tick(CannonSeatState.Nobody, false, now));
            Assert.IsFalse(boarding.Ready(now));
            Assert.That(boarding.Rider, Is.EqualTo(7ul));
            Assert.That(boarding.BoardedAt, Is.EqualTo(10));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDelay_CannotStartReplaceCancelOrMakeReady(float delay)
        {
            var boarding = new LiftBoardingState();
            Assert.IsFalse(boarding.Tick(7, true, 10, delay));
            Assert.IsFalse(boarding.IsCounting);
            boarding.Tick(7, true, 10);
            Assert.IsFalse(boarding.Tick(8, true, 20, delay));
            Assert.IsFalse(boarding.Tick(CannonSeatState.Nobody, false, 20, delay));
            Assert.IsFalse(boarding.Ready(20, delay));
            Assert.That(boarding.Rider, Is.EqualTo(7ul));
            Assert.That(boarding.BoardedAt, Is.EqualTo(10));
        }

        [Test]
        public void Receive_ReconstructsCountdownAndEmptyState()
        {
            var boarding = new LiftBoardingState();
            Assert.IsTrue(boarding.Receive(7, 10));
            Assert.IsTrue(boarding.IsCounting);
            Assert.IsFalse(boarding.Ready(11.499));
            Assert.IsTrue(boarding.Ready(11.5));
            Assert.IsTrue(boarding.Receive(8, 0));
            Assert.That(boarding.Rider, Is.EqualTo(8ul));
            Assert.IsTrue(boarding.Ready(1.5));
            Assert.IsTrue(boarding.Receive(CannonSeatState.Nobody, 0));
            Assert.IsFalse(boarding.IsCounting);
            Assert.That(boarding.BoardedAt, Is.Zero);
            Assert.IsFalse(boarding.Ready(100));
        }

        [TestCase(ulong.MaxValue, 10d)]
        [TestCase(ulong.MaxValue, -1d)]
        [TestCase(ulong.MaxValue, double.NaN)]
        [TestCase(8ul, -1d)]
        [TestCase(8ul, double.NaN)]
        [TestCase(8ul, double.PositiveInfinity)]
        [TestCase(8ul, double.NegativeInfinity)]
        public void Receive_InvalidSnapshotPreservesCountdown(ulong rider, double boardedAt)
        {
            var boarding = new LiftBoardingState();
            boarding.Tick(7, true, 10);
            Assert.IsFalse(boarding.Receive(rider, boardedAt));
            Assert.IsTrue(boarding.IsCounting);
            Assert.That(boarding.Rider, Is.EqualTo(7ul));
            Assert.That(boarding.BoardedAt, Is.EqualTo(10));
        }
    }
}
