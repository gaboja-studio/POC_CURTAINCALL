using System.Reflection;
using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class RouteResetBarrierTests
    {
        static bool Retry(RouteResetBarrier barrier, double now, out int token)
        {
            var args = new object[] { now, 0 };
            var method = typeof(RouteResetBarrier).GetMethod("TryGetRetryToken", BindingFlags.Instance | BindingFlags.NonPublic);
            bool due = (bool)method.Invoke(barrier, args);
            token = (int)args[1];
            return due;
        }

        [Test]
        public void RestartSyncRetry_RepeatsLostRequestAndStopsAfterMatchingReply()
        {
            var barrier = new RouteResetBarrier();
            Assert.IsFalse(Retry(barrier, 100d, out _));
            int expected = barrier.Begin();
            Assert.IsTrue(Retry(barrier, 100d, out int first));
            Assert.AreEqual(expected, first);
            Assert.IsFalse(Retry(barrier, 100.49d, out _));
            Assert.IsFalse(barrier.Accept(0));
            Assert.IsTrue(Retry(barrier, 100.5d, out int repeated));
            Assert.AreEqual(first, repeated);
            Assert.IsTrue(barrier.Accept(repeated));
            Assert.IsFalse(Retry(barrier, 102d, out _));
        }

        [Test]
        public void RestartSyncRetry_NewRestartReplacesThePendingTokenImmediately()
        {
            var barrier = new RouteResetBarrier();
            int first = barrier.Begin();
            Assert.IsTrue(Retry(barrier, 80d, out _));
            int current = barrier.Begin();
            Assert.IsFalse(barrier.Accept(first));
            Assert.IsTrue(Retry(barrier, 80.1d, out int retry));
            Assert.AreEqual(current, retry);
            Assert.IsTrue(barrier.Accept(retry));
        }

        [Test]
        public void RestartSyncRetry_DisconnectedClientDiscardsThePendingRequest()
        {
            var barrier = new RouteResetBarrier();
            barrier.Begin();
            Assert.IsTrue(Retry(barrier, 4d, out _));
            barrier.Clear();
            Assert.IsFalse(Retry(barrier, 12d, out _));
        }

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
