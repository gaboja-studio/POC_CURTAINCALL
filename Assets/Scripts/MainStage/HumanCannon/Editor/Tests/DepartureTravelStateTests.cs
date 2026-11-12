using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class DepartureTravelStateTests
    {
        [Test]
        public void Departure_ReachesDestinationAtDeadlineAndCannotStartAgain()
        {
            var travel = new DepartureTravelState();
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Waiting));
            Assert.That(travel.Rider, Is.EqualTo(CannonSeatState.Nobody));
            Assert.That(travel.Progress(10), Is.Zero);
            Assert.IsFalse(travel.Tick(10));
            Assert.IsTrue(travel.TryStart(7, 0, 10, 8));
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Moving));
            Assert.That(travel.Rider, Is.EqualTo(7ul));
            Assert.That(travel.StartedAt, Is.EqualTo(10));
            Assert.That(travel.Duration, Is.EqualTo(8));
            Assert.That(travel.Revision, Is.EqualTo(1u));
            Assert.IsFalse(travel.TryStart(8, 1, 11, 8));
            Assert.That(travel.Progress(5), Is.Zero);
            Assert.That(travel.Progress(10), Is.Zero);
            Assert.That(travel.Progress(14), Is.EqualTo(0.5f));
            Assert.IsFalse(travel.Tick(17.999));
            Assert.That(travel.Progress(18), Is.EqualTo(1f));
            Assert.IsTrue(travel.Tick(18));
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Arrived));
            Assert.That(travel.Rider, Is.EqualTo(7ul));
            Assert.That(travel.Revision, Is.EqualTo(2u));
            Assert.That(travel.Progress(30), Is.EqualTo(1f));
            Assert.IsFalse(travel.Tick(30));
            Assert.IsFalse(travel.TryStart(8, 2, 30, 8));
        }

        [Test]
        public void ArrivedPlatform_ReturnsToStartAndKeepsDelivery()
        {
            var travel = new DepartureTravelState();
            Assert.IsFalse(travel.TryReturn(0));
            travel.TryStart(7, 0, 10, 4);
            Assert.IsFalse(travel.TryReturn(12));
            Assert.IsFalse(travel.Delivered);
            travel.Tick(14);
            Assert.IsTrue(travel.Delivered);
            Assert.IsTrue(travel.TryReturn(16));
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Returning));
            Assert.That(travel.Revision, Is.EqualTo(3u));
            Assert.That(travel.Progress(16), Is.EqualTo(1f));
            Assert.That(travel.Progress(18), Is.EqualTo(0.5f));
            Assert.IsFalse(travel.Tick(19.999));
            Assert.IsTrue(travel.Tick(20));
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Waiting));
            Assert.That(travel.Rider, Is.EqualTo(CannonSeatState.Nobody));
            Assert.That(travel.Progress(30), Is.Zero);
            Assert.IsTrue(travel.Delivered);
            Assert.IsTrue(travel.TryStart(7, 4, 30, 4));
            travel.Reset();
            Assert.IsFalse(travel.Delivered);
        }

        [Test]
        public void Receive_CarriesReturningAndDeliveredWaiting()
        {
            var travel = new DepartureTravelState();
            Assert.IsTrue(travel.Receive(3, DeparturePhase.Returning, 7, 16, 4));
            Assert.IsTrue(travel.Delivered);
            Assert.That(travel.Progress(18), Is.EqualTo(0.5f));
            Assert.IsFalse(travel.Receive(4, DeparturePhase.Returning, CannonSeatState.Nobody, 16, 0));
            Assert.IsTrue(travel.Receive(4, DeparturePhase.Waiting, CannonSeatState.Nobody, 0, 0, true));
            Assert.IsTrue(travel.Delivered);
            Assert.IsTrue(travel.Receive(5, DeparturePhase.Waiting, CannonSeatState.Nobody, 0, 0));
            Assert.IsFalse(travel.Delivered);
        }

        [Test, Category("CommonProgressionRegression")]
        public void ReleasedRiderLeavesThePlatformOnItsOriginalSchedule()
        {
            var host = new DepartureTravelState();
            var observer = new DepartureTravelState();
            Assert.IsTrue(host.TryStart(7, 0, 10, 8));
            Assert.IsFalse(host.TryReleaseRider(8));
            Assert.IsTrue(host.TryReleaseRider(7));
            Assert.IsTrue(observer.Receive(host.Revision, host.Phase, host.Rider, host.StartedAt, host.Duration));
            Assert.AreEqual(CannonSeatState.Nobody, observer.Rider);
            Assert.AreEqual(0.5f, observer.Progress(14));
            Assert.IsTrue(host.Tick(18));
            Assert.IsTrue(observer.Receive(host.Revision, host.Phase, host.Rider, host.StartedAt, host.Duration));
            Assert.AreEqual(CannonSeatState.Nobody, observer.Rider);
            Assert.AreEqual(1f, observer.Progress(18));
            Assert.IsTrue(host.TryReturn(20));
            Assert.IsTrue(observer.Receive(host.Revision, host.Phase, host.Rider, host.StartedAt, host.Duration));
            Assert.AreEqual(0.5f, observer.Progress(24));
            Assert.IsTrue(host.Tick(28));
            Assert.IsTrue(host.TryStart(7, host.Revision, 30, 8));
        }

        [Test]
        public void DelayedTick_ArrivesWithoutExtendingTravel()
        {
            var travel = new DepartureTravelState();
            travel.TryStart(7, 0, 0, 8);
            Assert.That(travel.Progress(30), Is.EqualTo(1f));
            Assert.IsTrue(travel.Tick(30));
            Assert.That(travel.StartedAt, Is.Zero);
            Assert.That(travel.Duration, Is.EqualTo(8));
            Assert.That(travel.Revision, Is.EqualTo(2u));
        }

        [Test]
        public void Reset_InvalidatesDelayedStartAndPreviousRoundSnapshot()
        {
            var travel = new DepartureTravelState();
            travel.Reset();
            Assert.IsFalse(travel.TryStart(7, 0, 0, 8));
            Assert.IsFalse(travel.Receive(0, DeparturePhase.Moving, 7, 0, 8));
            Assert.IsTrue(travel.TryStart(7, 1, 10, 8));
            travel.Reset();
            Assert.That(travel.Revision, Is.EqualTo(3u));
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Waiting));
            Assert.That(travel.Rider, Is.EqualTo(CannonSeatState.Nobody));
            Assert.That(travel.StartedAt, Is.Zero);
            Assert.That(travel.Duration, Is.Zero);
            Assert.IsFalse(travel.TryStart(7, 2, 20, 8));
            Assert.IsFalse(travel.Receive(2, DeparturePhase.Arrived, 7, 10, 8));
            Assert.IsFalse(travel.Tick(30));
            Assert.IsTrue(travel.TryStart(8, 3, 30, 8));
            Assert.That(travel.Rider, Is.EqualTo(8ul));
        }

        [Test]
        public void Receive_AcceptsSnapshotsAndDuplicateButRejectsOlderRevision()
        {
            var travel = new DepartureTravelState();
            Assert.IsTrue(travel.Receive(0, DeparturePhase.Waiting, CannonSeatState.Nobody, 0, 0));
            Assert.IsTrue(travel.Receive(1, DeparturePhase.Moving, 7, 10, 8));
            Assert.IsTrue(travel.Receive(1, DeparturePhase.Moving, 7, 10, 8));
            Assert.That(travel.Progress(14), Is.EqualTo(0.5f));
            Assert.IsTrue(travel.Receive(2, DeparturePhase.Arrived, 7, 10, 8));
            Assert.IsFalse(travel.Receive(1, DeparturePhase.Moving, 7, 10, 8));
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Arrived));
            Assert.That(travel.Revision, Is.EqualTo(2u));
            Assert.IsTrue(travel.Receive(3, DeparturePhase.Waiting, CannonSeatState.Nobody, 0, 0));
            Assert.That(travel.Progress(30), Is.Zero);
        }

        [TestCase(-1d, 8f)]
        [TestCase(double.NaN, 8f)]
        [TestCase(double.PositiveInfinity, 8f)]
        [TestCase(double.NegativeInfinity, 8f)]
        [TestCase(double.MaxValue, 8f)]
        [TestCase(10d, 0f)]
        [TestCase(10d, -1f)]
        [TestCase(10d, float.NaN)]
        [TestCase(10d, float.PositiveInfinity)]
        [TestCase(10d, float.NegativeInfinity)]
        public void InvalidTimeOrDuration_RejectsStartAndActiveSnapshots(double now, float duration)
        {
            var travel = new DepartureTravelState();
            Assert.IsFalse(travel.TryStart(7, 0, now, duration));
            Assert.IsFalse(travel.Receive(9, DeparturePhase.Moving, 7, now, duration));
            Assert.IsFalse(travel.Receive(9, DeparturePhase.Arrived, 7, now, duration));
            Assert.That(travel.Revision, Is.Zero);
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Waiting));
            Assert.That(travel.Rider, Is.EqualTo(CannonSeatState.Nobody));
            Assert.IsTrue(travel.TryStart(7, 0, 0, 8));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidTickAndProgress_DoNotFinishTravel(double now)
        {
            var travel = new DepartureTravelState();
            travel.TryStart(7, 0, 0, 8);
            Assert.IsFalse(travel.Tick(now));
            Assert.That(travel.Progress(now), Is.Zero);
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Moving));
            Assert.That(travel.Revision, Is.EqualTo(1u));
        }

        [Test]
        public void InvalidRiderOrStaleStart_DoesNotConsumeRevision()
        {
            var travel = new DepartureTravelState();
            Assert.IsFalse(travel.TryStart(CannonSeatState.Nobody, 0, 0, 8));
            Assert.IsFalse(travel.TryStart(7, 1, 0, 8));
            Assert.That(travel.Revision, Is.Zero);
            Assert.IsTrue(travel.TryStart(7, 0, 0, 8));
        }

        [TestCase(DeparturePhase.Waiting, 7ul, 0d, 0f)]
        [TestCase(DeparturePhase.Waiting, ulong.MaxValue, 10d, 0f)]
        [TestCase(DeparturePhase.Waiting, ulong.MaxValue, 0d, 8f)]
        [TestCase(DeparturePhase.Waiting, ulong.MaxValue, double.NaN, 0f)]
        [TestCase(DeparturePhase.Waiting, ulong.MaxValue, 0d, float.NaN)]
        [TestCase(DeparturePhase.Moving, ulong.MaxValue, 10d, 0f)]
        [TestCase(DeparturePhase.Arrived, ulong.MaxValue, -1d, 8f)]
        [TestCase((DeparturePhase)99, ulong.MaxValue, 0d, 0f)]
        public void Receive_InvalidCombinationPreservesCurrentTravel(DeparturePhase phase, ulong rider,
            double startedAt, float duration)
        {
            var travel = new DepartureTravelState();
            travel.TryStart(8, 0, 10, 8);
            Assert.IsFalse(travel.Receive(9, phase, rider, startedAt, duration));
            Assert.That(travel.Revision, Is.EqualTo(1u));
            Assert.That(travel.Phase, Is.EqualTo(DeparturePhase.Moving));
            Assert.That(travel.Rider, Is.EqualTo(8ul));
            Assert.That(travel.StartedAt, Is.EqualTo(10));
            Assert.That(travel.Duration, Is.EqualTo(8));
        }
    }
}
