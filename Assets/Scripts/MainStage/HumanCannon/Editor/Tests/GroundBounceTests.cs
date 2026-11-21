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
        public void UnansweredGroundBounceRetriesTheSameAttemptAndExpires()
        {
            var request = new GroundBounceRequest();
            request.Begin(7, 4, 10d);
            Assert.IsTrue(request.TrySend(10d));
            Assert.IsFalse(request.TrySend(10.1d));
            Assert.IsTrue(request.TrySend(10.25d));
            Assert.IsTrue(request.Matches(7, 4));
            Assert.IsTrue(request.Expired(13d));
            Assert.IsFalse(request.TrySend(13d));
            request.Cancel();
            Assert.AreEqual(8u, request.NextSequence(), "A replacement trampoline does not restart the player's sequence.");
        }

        [Test]
        public void ConfirmedGroundBounceReplaysTheOriginalContactExactlyOncePerIdentity()
        {
            var history = new GroundBounceHistory();
            var contact = new Vector3(2f, 1f, 3f);
            Assert.IsTrue(history.TryAccept(17, 8, 5, 5));
            Assert.IsFalse(history.TryReplay(17, 8, 5, out _));
            history.RecordAccepted(17, contact);
            Assert.IsTrue(history.TryReplay(17, 8, 5, out Vector3 received));
            Assert.AreEqual(contact, received);
            Assert.IsFalse(history.TryReplay(17, 8, 6, out _));
            Assert.IsFalse(history.TryReplay(18, 8, 5, out _));
            Assert.IsTrue(history.TryAccept(17, 9, 6, 6));
            Assert.IsFalse(history.TryReplay(17, 8, 5, out _));
        }

        [TestCase(2)]
        [TestCase(CannonFlightState.PoleEntrySource)]
        public void LiftAirColumnCountsActualTrampolineReboundRegardlessOfTheOriginalDevice(int station)
        {
            var flights = new CannonFlightState();
            flights.Begin(7, station, 17);
            Assert.IsFalse(flights.IsTrampolineFlight(7));
            Assert.IsTrue(flights.TryRebound(7, station, 17, 0, fromTrampoline: true));
            Assert.IsTrue(flights.IsTrampolineFlight(7));
            Assert.IsTrue(flights.TryRebound(7, station, 17, 1)); // grabs and releases from a pole use the same transition
            Assert.IsFalse(flights.IsTrampolineFlight(7));
            flights.Begin(7, -1, 18, fromTrampoline: true);
            Assert.IsTrue(flights.IsTrampolineFlight(7));
            Assert.IsFalse(flights.TryComplete(7, -1, 17));
            Assert.IsTrue(flights.TryComplete(7, -1, 18));
            Assert.IsFalse(flights.IsTrampolineFlight(7));
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
