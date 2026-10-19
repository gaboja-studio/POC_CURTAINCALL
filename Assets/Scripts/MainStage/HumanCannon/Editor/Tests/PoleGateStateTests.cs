using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class PoleGateStateTests
    {
        [Test]
        public void HoldAndWarning_TransitionAtExactDeadlines()
        {
            var gate = new PoleGateState();
            Assert.IsTrue(gate.TryGrab(7, 0, 10, 8));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Retracted));
            Assert.That(gate.HoldUntil, Is.EqualTo(18));
            Assert.IsFalse(gate.Tick(17.999, 2));
            Assert.IsTrue(gate.Tick(18, 2));
            Assert.That(gate.Occupant, Is.EqualTo(PoleGateState.Nobody));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Warning));
            Assert.That(gate.HoldUntil, Is.Zero);
            Assert.That(gate.RearmAt, Is.EqualTo(20));
            Assert.That(gate.Revision, Is.EqualTo(2u));
            Assert.IsFalse(gate.Tick(19.999, 2));
            Assert.IsTrue(gate.Tick(20, 2));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Active));
            Assert.That(gate.RearmAt, Is.Zero);
            Assert.That(gate.Revision, Is.EqualTo(3u));
        }

        [Test]
        public void DelayedTick_UsesOriginalDeadlineAndCanFinishBothTransitions()
        {
            var gate = new PoleGateState();
            gate.TryGrab(7, 0, 10, 8);
            Assert.IsTrue(gate.Tick(19, 2));
            Assert.That(gate.RearmAt, Is.EqualTo(20));
            Assert.IsTrue(gate.Tick(30, 2));
            Assert.IsFalse(gate.Tick(30, 2));
            Assert.That(gate.Revision, Is.EqualTo(3u));

            gate.TryGrab(8, gate.Revision, 30, 8);
            Assert.IsTrue(gate.Tick(50, 2));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Active));
            Assert.IsFalse(gate.IsOccupied);
            Assert.That(gate.HoldUntil, Is.Zero);
            Assert.That(gate.RearmAt, Is.Zero);
            Assert.That(gate.Revision, Is.EqualTo(6u));
            Assert.IsFalse(gate.Tick(50, 2));
        }

        [Test]
        public void ManualOrDeathRelease_StartsWarningOnlyForCurrentOccupant()
        {
            var gate = new PoleGateState();
            gate.TryGrab(7, 0, 10, 8);
            Assert.IsFalse(gate.TryRelease(8, 1, 12, 2));
            Assert.IsFalse(gate.TryRelease(7, 0, 12, 2));
            Assert.IsTrue(gate.TryRelease(7, 1, 12, 2));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Warning));
            Assert.That(gate.RearmAt, Is.EqualTo(14));
            Assert.That(gate.HoldUntil, Is.Zero);
            Assert.IsFalse(gate.IsOccupied);
            Assert.IsFalse(gate.TryRelease(7, 2, 13, 2));
            Assert.IsFalse(gate.TryRelease(PoleGateState.Nobody, 2, 13, 2));
            Assert.That(gate.RearmAt, Is.EqualTo(14));
            Assert.That(gate.Revision, Is.EqualTo(2u));
        }

        [Test]
        public void Warning_AllowsRegrabAndRejectsOldRelease()
        {
            var gate = new PoleGateState();
            gate.TryGrab(7, 0, 10, 8);
            Assert.IsFalse(gate.TryGrab(8, 1, 11, 8));
            gate.TryRelease(7, 1, 12, 2);
            Assert.IsTrue(gate.TryGrab(8, 2, 13, 8));
            Assert.That(gate.Occupant, Is.EqualTo(8ul));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Retracted));
            Assert.That(gate.HoldUntil, Is.EqualTo(21));
            Assert.That(gate.RearmAt, Is.Zero);
            Assert.IsFalse(gate.TryRelease(7, 1, 13, 2));
            Assert.IsFalse(gate.Tick(14, 2));
        }

        [Test]
        public void EmptyReset_InvalidatesDelayedGrabAndSnapshot()
        {
            var gate = new PoleGateState();
            gate.Reset();
            Assert.That(gate.Revision, Is.EqualTo(1u));
            Assert.IsFalse(gate.TryGrab(7, 0, 10, 8));
            Assert.IsFalse(gate.Receive(0, 7, PoleSawPhase.Retracted, 18, 0));
            Assert.IsTrue(gate.TryGrab(7, 1, 10, 8));
            gate.Reset();
            Assert.That(gate.Revision, Is.EqualTo(3u));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Active));
            Assert.IsFalse(gate.IsOccupied);
            Assert.That(gate.HoldUntil, Is.Zero);
            Assert.That(gate.RearmAt, Is.Zero);
            Assert.IsFalse(gate.TryRelease(7, 2, 11, 2));
            Assert.IsFalse(gate.Receive(2, 7, PoleSawPhase.Retracted, 18, 0));
            Assert.IsFalse(gate.Tick(30, 2));
        }

        [Test]
        public void Receive_AcceptsValidSnapshotsAndRejectsOlderRevision()
        {
            var gate = new PoleGateState();
            Assert.IsTrue(gate.Receive(1, 7, PoleSawPhase.Retracted, 18, 0));
            Assert.IsTrue(gate.Receive(1, 7, PoleSawPhase.Retracted, 18, 0));
            Assert.IsTrue(gate.Receive(2, PoleGateState.Nobody, PoleSawPhase.Warning, 0, 20));
            Assert.IsFalse(gate.Receive(1, 7, PoleSawPhase.Retracted, 18, 0));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Warning));
            Assert.That(gate.RearmAt, Is.EqualTo(20));
            Assert.IsTrue(gate.Receive(3, PoleGateState.Nobody, PoleSawPhase.Active, 0, 0));
            Assert.That(gate.Revision, Is.EqualTo(3u));
            Assert.IsFalse(gate.IsOccupied);
        }

        [Test]
        public void UnlimitedHold_KeepsGateStoppedUntilReleaseThenUsesNormalWarning()
        {
            var gate = new PoleGateState();
            Assert.IsTrue(gate.TryGrab(7, 0, 10, 0));
            Assert.IsTrue(gate.IsUnlimitedHold);
            Assert.That(gate.HoldUntil, Is.Zero);
            foreach (double now in new[] { 18d, 1000d, double.MaxValue })
            {
                Assert.IsFalse(gate.Tick(now, 2), "시간만 지나서는 손을 놓거나 관문을 재가동하지 않는다.");
                Assert.That(gate.Occupant, Is.EqualTo(7ul));
                Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Retracted));
                Assert.That(gate.Revision, Is.EqualTo(1u));
            }
            Assert.IsFalse(gate.TryGrab(8, gate.Revision, 1001, 0));
            Assert.IsFalse(gate.TryRelease(8, gate.Revision, 1001, 2));
            Assert.IsTrue(gate.TryRelease(7, gate.Revision, 1001, 2));
            Assert.IsFalse(gate.IsUnlimitedHold);
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Warning));
            Assert.IsFalse(gate.Tick(1002.999, 2));
            Assert.IsTrue(gate.Tick(1003, 2));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Active));
        }

        [Test]
        public void UnlimitedSnapshot_KeepsOccupantAndResetRejectsDelayedState()
        {
            var host = new PoleGateState();
            var client = new PoleGateState();
            Assert.IsTrue(host.TryGrab(7, 0, 10, 0));
            Assert.IsTrue(client.Receive(host.Revision, host.Occupant, host.Phase, host.HoldUntil, host.RearmAt));
            Assert.IsTrue(client.IsUnlimitedHold);
            Assert.IsFalse(client.Tick(1000, 2));
            client.Reset();
            Assert.IsFalse(client.IsOccupied);
            Assert.IsFalse(client.IsUnlimitedHold);
            Assert.IsFalse(client.Receive(host.Revision, host.Occupant, host.Phase, host.HoldUntil, host.RearmAt));
            Assert.IsTrue(client.TryGrab(8, client.Revision, 1001, 0));
            Assert.That(client.Occupant, Is.EqualTo(8ul));
        }

        [Test]
        public void ZeroWarningDuration_DoesNotReleaseUnlimitedOccupant()
        {
            var gate = new PoleGateState();
            Assert.IsTrue(gate.TryGrab(7, 0, 10, 0));
            Assert.IsFalse(gate.TryRelease(7, gate.Revision, 20, 0));
            Assert.IsTrue(gate.IsUnlimitedHold);
            Assert.That(gate.Revision, Is.EqualTo(1u));
        }

        [TestCase(-1d, 8f)]
        [TestCase(double.NaN, 8f)]
        [TestCase(double.PositiveInfinity, 8f)]
        [TestCase(double.MaxValue, 8f)]
        [TestCase(10d, -1f)]
        [TestCase(10d, float.NaN)]
        [TestCase(10d, float.PositiveInfinity)]
        public void InvalidTimeOrDuration_DoesNotChangeGrabOrRelease(double now, float seconds)
        {
            var gate = new PoleGateState();
            Assert.IsFalse(gate.TryGrab(7, 0, now, seconds));
            Assert.That(gate.Revision, Is.Zero);
            Assert.IsFalse(gate.IsOccupied);
            gate.TryGrab(7, 0, 0, 8);
            Assert.IsFalse(gate.TryRelease(7, 1, now, seconds));
            Assert.That(gate.Occupant, Is.EqualTo(7ul));
            Assert.That(gate.HoldUntil, Is.EqualTo(8));
            Assert.That(gate.Revision, Is.EqualTo(1u));
        }

        [Test]
        public void InvalidActorOrTick_DoesNotChangeState()
        {
            var gate = new PoleGateState();
            Assert.IsFalse(gate.TryGrab(PoleGateState.Nobody, 0, 10, 8));
            gate.TryGrab(7, 0, 10, 8);
            Assert.IsFalse(gate.Tick(double.NaN, 2));
            Assert.IsFalse(gate.Tick(double.PositiveInfinity, 2));
            Assert.IsFalse(gate.Tick(-1, 2));
            Assert.IsFalse(gate.Tick(20, 0));
            Assert.IsFalse(gate.Tick(20, float.PositiveInfinity));
            Assert.IsFalse(gate.Tick(20, float.NaN));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Retracted));
            Assert.That(gate.Revision, Is.EqualTo(1u));
        }

        [TestCase(PoleSawPhase.Active, 7ul, 0d, 0d)]
        [TestCase(PoleSawPhase.Active, ulong.MaxValue, 18d, 0d)]
        [TestCase(PoleSawPhase.Active, ulong.MaxValue, 0d, 20d)]
        [TestCase(PoleSawPhase.Retracted, ulong.MaxValue, 18d, 0d)]
        [TestCase(PoleSawPhase.Retracted, 7ul, 18d, 20d)]
        [TestCase(PoleSawPhase.Warning, 7ul, 0d, 20d)]
        [TestCase(PoleSawPhase.Warning, ulong.MaxValue, 18d, 20d)]
        [TestCase(PoleSawPhase.Warning, ulong.MaxValue, 0d, 0d)]
        [TestCase((PoleSawPhase)99, ulong.MaxValue, 0d, 0d)]
        [TestCase(PoleSawPhase.Retracted, 7ul, double.NaN, 0d)]
        [TestCase(PoleSawPhase.Retracted, 7ul, double.PositiveInfinity, 0d)]
        [TestCase(PoleSawPhase.Warning, ulong.MaxValue, 0d, double.NaN)]
        [TestCase(PoleSawPhase.Warning, ulong.MaxValue, 0d, double.PositiveInfinity)]
        [TestCase(PoleSawPhase.Retracted, 7ul, -1d, 0d)]
        [TestCase(PoleSawPhase.Warning, ulong.MaxValue, 0d, -1d)]
        public void Receive_InvalidSnapshotDoesNotConsumeRevision(PoleSawPhase phase, ulong occupant,
            double holdUntil, double rearmAt)
        {
            var gate = new PoleGateState();
            gate.TryGrab(8, 0, 10, 8);
            Assert.IsFalse(gate.Receive(9, occupant, phase, holdUntil, rearmAt));
            Assert.That(gate.Revision, Is.EqualTo(1u));
            Assert.That(gate.Occupant, Is.EqualTo(8ul));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Retracted));
            Assert.That(gate.HoldUntil, Is.EqualTo(18));
            Assert.That(gate.RearmAt, Is.Zero);
        }
    }
}
