using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class LiftTrapStateTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void NormalOccupancy_KeepsTrapClosed(int occupants)
        {
            var trap = new LiftTrapState();
            Assert.IsFalse(trap.Tick(occupants, 10, 0.4f));
            Assert.IsFalse(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.Zero);
            Assert.That(trap.FrozenProgress, Is.Zero);
        }

        [TestCase(2, 0f)]
        [TestCase(3, 0.45f)]
        [TestCase(4, 1f)]
        public void OverloadedTrap_OpensAndFreezesCurrentTravel(int occupants, float progress)
        {
            var trap = new LiftTrapState();
            Assert.IsTrue(trap.Tick(occupants, 10, progress));
            Assert.IsTrue(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.EqualTo(10));
            Assert.That(trap.FrozenProgress, Is.EqualTo(progress));
            Assert.IsFalse(trap.Tick(occupants, 20, 1));
            Assert.That(trap.OpenedAt, Is.EqualTo(10));
            Assert.That(trap.FrozenProgress, Is.EqualTo(progress));
        }

        [Test]
        public void EmptyTrap_WaitsForExactResetDeadlineAndReturnsToStart()
        {
            var trap = new LiftTrapState();
            trap.Tick(2, 10, 0.4f);
            const float delay = 1.2f;
            double deadline = 10d + delay;
            Assert.IsFalse(trap.Tick(0, deadline - 0.001, 0.8f, delay));
            Assert.IsTrue(trap.IsOpen);
            Assert.That(trap.FrozenProgress, Is.EqualTo(0.4f));
            Assert.IsTrue(trap.Tick(0, deadline, 1, delay));
            Assert.IsFalse(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.Zero);
            Assert.That(trap.FrozenProgress, Is.Zero);
            Assert.IsFalse(trap.Tick(0, deadline + 1, 0));
        }

        [Test]
        public void RemainingOrReturningRiders_KeepTrapOpenUntilEverybodyLeaves()
        {
            var trap = new LiftTrapState();
            trap.Tick(2, 10, 0.6f);
            Assert.IsFalse(trap.Tick(1, 20, 1));
            Assert.IsFalse(trap.Tick(2, 21, 1));
            Assert.IsFalse(trap.Tick(1, 22, 1));
            Assert.IsTrue(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.EqualTo(10));
            Assert.That(trap.FrozenProgress, Is.EqualTo(0.6f));
            Assert.IsTrue(trap.Tick(0, 23, 1));
            Assert.IsFalse(trap.IsOpen);
            Assert.IsTrue(trap.Tick(2, 24, 0));
            Assert.That(trap.OpenedAt, Is.EqualTo(24));
            Assert.That(trap.FrozenProgress, Is.Zero);
        }

        [Test]
        public void EarlierClock_DoesNotCloseTrapEvenWithZeroResetDelay()
        {
            var trap = new LiftTrapState();
            trap.Tick(2, 10, 0.3f, 0);
            Assert.IsFalse(trap.Tick(0, 9, 1, 0));
            Assert.IsTrue(trap.IsOpen);
            Assert.IsTrue(trap.Tick(0, 10, 1, 0));
            Assert.IsFalse(trap.IsOpen);
        }

        [Test]
        public void Reset_ClearsOpenStateAndAllowsANewOverload()
        {
            var trap = new LiftTrapState();
            trap.Tick(2, 10, 0.7f);
            trap.Reset();
            Assert.IsFalse(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.Zero);
            Assert.That(trap.FrozenProgress, Is.Zero);
            Assert.IsFalse(trap.Tick(1, 0, 0));
            Assert.IsTrue(trap.Tick(2, 0, 0));
            Assert.That(trap.OpenedAt, Is.Zero);
        }

        [TestCase(-1, 10d, 0.4f, 1.2f)]
        [TestCase(2, -1d, 0.4f, 1.2f)]
        [TestCase(2, double.NaN, 0.4f, 1.2f)]
        [TestCase(2, double.PositiveInfinity, 0.4f, 1.2f)]
        [TestCase(2, double.NegativeInfinity, 0.4f, 1.2f)]
        [TestCase(2, 10d, -0.01f, 1.2f)]
        [TestCase(2, 10d, 1.01f, 1.2f)]
        [TestCase(2, 10d, float.NaN, 1.2f)]
        [TestCase(2, 10d, float.PositiveInfinity, 1.2f)]
        [TestCase(2, 10d, float.NegativeInfinity, 1.2f)]
        [TestCase(2, 10d, 0.4f, -0.01f)]
        [TestCase(2, 10d, 0.4f, float.NaN)]
        [TestCase(2, 10d, 0.4f, float.PositiveInfinity)]
        [TestCase(2, 10d, 0.4f, float.NegativeInfinity)]
        public void InvalidTick_DoesNotOpenOrChangeTrap(int occupants, double now, float progress, float delay)
        {
            var trap = new LiftTrapState();
            Assert.IsFalse(trap.Tick(occupants, now, progress, delay));
            Assert.IsFalse(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.Zero);
            Assert.That(trap.FrozenProgress, Is.Zero);
            trap.Tick(2, 3, 0.6f);
            Assert.IsFalse(trap.Tick(occupants, now, progress, delay));
            Assert.IsTrue(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.EqualTo(3));
            Assert.That(trap.FrozenProgress, Is.EqualTo(0.6f));
        }

        [TestCase(double.NaN, 0.5f, 1.2f)]
        [TestCase(double.PositiveInfinity, 0.5f, 1.2f)]
        [TestCase(10d, float.NaN, 1.2f)]
        [TestCase(10d, 1.01f, 1.2f)]
        [TestCase(10d, 0.5f, -1f)]
        [TestCase(10d, 0.5f, float.NaN)]
        [TestCase(10d, 0.5f, float.PositiveInfinity)]
        public void InvalidEmptyTick_DoesNotResetOpenTrap(double now, float progress, float delay)
        {
            var trap = new LiftTrapState();
            trap.Tick(2, 0, 0.3f);
            Assert.IsFalse(trap.Tick(0, now, progress, delay));
            Assert.IsTrue(trap.IsOpen);
            Assert.That(trap.FrozenProgress, Is.EqualTo(0.3f));
        }

        [Test]
        public void Receive_AcceptsOpenAtZeroAndClosedSnapshots()
        {
            var trap = new LiftTrapState();
            Assert.IsTrue(trap.Receive(true, 0, 0));
            Assert.IsTrue(trap.IsOpen);
            Assert.IsTrue(trap.Receive(true, 10, 1));
            Assert.That(trap.OpenedAt, Is.EqualTo(10));
            Assert.That(trap.FrozenProgress, Is.EqualTo(1));
            Assert.IsTrue(trap.Receive(false, 0, 0));
            Assert.IsFalse(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.Zero);
            Assert.That(trap.FrozenProgress, Is.Zero);
        }

        [TestCase(false, 10d, 0f)]
        [TestCase(false, 0d, 0.3f)]
        [TestCase(true, -1d, 0.3f)]
        [TestCase(true, double.NaN, 0.3f)]
        [TestCase(true, double.PositiveInfinity, 0.3f)]
        [TestCase(true, double.NegativeInfinity, 0.3f)]
        [TestCase(true, 10d, -0.01f)]
        [TestCase(true, 10d, 1.01f)]
        [TestCase(true, 10d, float.NaN)]
        [TestCase(true, 10d, float.PositiveInfinity)]
        [TestCase(true, 10d, float.NegativeInfinity)]
        public void Receive_InvalidSnapshotPreservesOpenTrap(bool open, double openedAt, float progress)
        {
            var trap = new LiftTrapState();
            trap.Tick(2, 5, 0.4f);
            Assert.IsFalse(trap.Receive(open, openedAt, progress));
            Assert.IsTrue(trap.IsOpen);
            Assert.That(trap.OpenedAt, Is.EqualTo(5));
            Assert.That(trap.FrozenProgress, Is.EqualTo(0.4f));
        }
    }
}
