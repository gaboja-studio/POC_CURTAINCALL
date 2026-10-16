using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class GroundBounceTests
    {
        [Test]
        public void RestartOrDeath_CancelsPendingResponse()
        {
            var request = new GroundBounceRequest();
            request.Begin(1, 4);
            Assert.IsTrue(request.Matches(1, 4));
            request.Cancel();
            Assert.IsFalse(request.Matches(1, 4));
        }

        [Test]
        public void NewAttempt_RejectsOldResponseAndWrongEpoch()
        {
            var request = new GroundBounceRequest();
            request.Begin(1, 4);
            request.Cancel();
            request.Begin(2, 5);
            Assert.IsFalse(request.Matches(1, 4));
            Assert.IsFalse(request.Matches(2, 4));
            Assert.IsTrue(request.Matches(2, 5));
        }

        [Test]
        public void Host_RejectsDuplicateAndOutOfOrderGroundContacts()
        {
            var history = new GroundBounceHistory();
            Assert.IsTrue(history.TryAccept(7, 2, 3, 3));
            Assert.IsFalse(history.TryAccept(7, 2, 3, 3));
            Assert.IsFalse(history.TryAccept(7, 1, 3, 3));
            Assert.IsTrue(history.TryAccept(7, 3, 3, 3));
        }

        [Test]
        public void Restart_RejectsOldEpochWithoutConsumingNewSequence()
        {
            var history = new GroundBounceHistory();
            Assert.IsFalse(history.TryAccept(7, 1, 3, 4));
            Assert.IsTrue(history.TryAccept(7, 1, 4, 4));
            Assert.IsFalse(history.TryAccept(8, 0, 4, 4));
        }

        [Test]
        public void DifferentPlayersAndNewSession_HaveIndependentSequences()
        {
            var history = new GroundBounceHistory();
            Assert.IsTrue(history.TryAccept(7, 1, 0, 0));
            Assert.IsTrue(history.TryAccept(8, 1, 0, 0));
            history.Clear();
            Assert.IsTrue(history.TryAccept(7, 1, 0, 0));
        }

        [Test]
        public void GroundFlight_ReboundsAgainAndRejectsStaleLanding()
        {
            var flights = new CannonFlightState();
            flights.Begin(7, -1, 8);
            Assert.IsFalse(flights.TryRebound(7, 0, 8, 0));
            Assert.IsTrue(flights.TryRebound(7, -1, 8, 0));
            Assert.IsFalse(flights.TryComplete(7, -1, 8, 0));
            Assert.IsTrue(flights.TryComplete(7, -1, 8, 1));
            flights.Begin(7, -1, 9);
            Assert.IsFalse(flights.TryComplete(7, -1, 8, 1));
            Assert.IsTrue(flights.Matches(7, -1, 9, 0));
        }

        [Test]
        public void GroundJump_CrossesLoweredSurfaceOnlyOnDescent()
        {
            Vector3 surface = Vector3.up * 0.5f;
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.zero, Vector3.up * 0.8f,
                surface, surface, Vector2.one * 2f, 0.3f, out _));
            Assert.IsTrue(TrampolineContact.TryCross(Vector3.up * 0.8f, Vector3.zero,
                surface, surface, Vector2.one * 2f, 0.3f, out var contact));
            Assert.That(contact.y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void WalkingUnderSurfaceOrJumpingOutside_DoesNotBounce()
        {
            Vector3 surface = Vector3.up * 0.5f;
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.back, Vector3.forward,
                surface, surface, Vector2.one * 2f, 0.3f, out _));
            Assert.IsFalse(TrampolineContact.TryCross(new Vector3(3, 0.8f, 0), new Vector3(3, 0, 0),
                surface, surface, Vector2.one * 2f, 0.3f, out _));
        }
    }
}
