using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class RouteResetBarrierTests
    {
        [Test]
        public void RestartBeforeSnapshot_RejectsQueuedStateUntilItsRequestedReply()
        {
            var barrier = new RouteResetBarrier();
            int token = barrier.Begin();
            Assert.That(barrier.Accept(0), Is.False);
            Assert.That(barrier.Waiting, Is.True);
            Assert.That(barrier.Accept(token), Is.True);
            Assert.That(barrier.Waiting, Is.False);
            Assert.That(barrier.Accept(0), Is.True);
        }

        [Test]
        public void ResetAndNewDepartureBeforeLocalRestart_StillAcceptsFreshReply()
        {
            var barrier = new RouteResetBarrier();
            Assert.That(barrier.Accept(0), Is.True); // reset snapshot
            Assert.That(barrier.Accept(0), Is.True); // next departure snapshot
            int token = barrier.Begin(); // delayed player Restarted event
            Assert.That(barrier.Accept(token), Is.True); // reply can already be Moving/Arrived
            Assert.That(barrier.Waiting, Is.False);
        }

        [Test]
        public void SecondRestart_DoesNotAcceptFirstRestartsReply()
        {
            var barrier = new RouteResetBarrier();
            int oldToken = barrier.Begin();
            int newToken = barrier.Begin();
            Assert.That(barrier.Accept(oldToken), Is.False);
            Assert.That(barrier.Accept(newToken), Is.True);
        }

        [Test]
        public void Disconnect_ClearsWaitAndAllowsNewHostSnapshot()
        {
            var barrier = new RouteResetBarrier();
            barrier.Begin();
            barrier.Clear();
            Assert.That(barrier.Waiting, Is.False);
            Assert.That(barrier.Token, Is.Zero);
            Assert.That(barrier.Accept(0), Is.True);
        }
    }
}
