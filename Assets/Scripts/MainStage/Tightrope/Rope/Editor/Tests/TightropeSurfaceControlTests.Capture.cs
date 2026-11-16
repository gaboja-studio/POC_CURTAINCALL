using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed partial class TightropeSurfaceControlTests
    {
        const BindingFlags CaptureMembers = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(PlayerState.EscapeCaptured)]
        [TestCase(PlayerState.Retired)]
        [TestCase(PlayerState.Fallen)]
        [Category("PiggybackCaptureRegression")]
        public void InterruptedCarrierCannotRelocateLivingPassengerAfterUnlink(PlayerState interrupted)
        {
            WithMountedPair((stack, follow, passenger) =>
            {
                Vector3 before = passenger.transform.position;
                SetCaptureFixtureState(player, interrupted);
                // Capture/fall presentation can move the former carrier before the owner's
                // late follow update consumes an unlink received in this same frame.
                player.transform.position = origin + new Vector3(5f, 7f, 3f);
                ForgetCaptureFixtureLinks(stack);
                Physics.SyncTransforms();

                Invoke(follow, "LateUpdate");

                Assert.That(passenger.transform.position, Is.EqualTo(before),
                    "A living passenger must detach where they are, not teleport beside a captured or dead carrier.");
                Assert.That(passenger.Simulated, Is.True);
                Assert.That(passenger.ControlEnabled, Is.True);
                Assert.That(stack.IsInStack(passenger.GetComponent<NetworkPlayer>()), Is.False);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("PiggybackCaptureRegression")]
        public void CapturedPassengerKeepsCaptureMovementOwnershipWhenStackReleases(bool unlinkReceived)
        {
            WithMountedPair((stack, follow, passenger) =>
            {
                var rider = passenger.GetComponent<NetworkPlayer>();
                SetCaptureFixtureState(rider, PlayerState.EscapeCaptured);
                Invoke(rider, "ApplyState", PlayerState.EscapeCaptured);
                Vector3 heldPosition = origin + new Vector3(4f, 6f, 2f);
                passenger.transform.position = heldPosition;
                if (unlinkReceived) ForgetCaptureFixtureLinks(stack);
                Physics.SyncTransforms();

                // Follow runs after the pursuer's LateUpdate and must not restore simulation.
                Invoke(follow, "LateUpdate");

                Assert.That(passenger.transform.position, Is.EqualTo(heldPosition));
                Assert.That(passenger.Simulated, Is.False);
                Assert.That(passenger.ControlEnabled, Is.False);
                Assert.That(typeof(PiggybackFollow).GetField("localMover", CaptureMembers).GetValue(follow), Is.Null,
                    "The old stack releases its reference while capture retains movement ownership.");
            });
        }

        [Test, Category("PiggybackCaptureRegression")]
        public void NormalPassengerUnlinkStillPutsDownBesideLivingCarrierAndRestoresControl()
        {
            WithMountedPair((stack, follow, passenger) =>
            {
                Vector3 before = passenger.transform.position;
                ForgetCaptureFixtureLinks(stack);
                Invoke(follow, "LateUpdate");

                Assert.That(passenger.transform.position, Is.Not.EqualTo(before));
                Assert.That(passenger.transform.position.y, Is.EqualTo(player.transform.position.y).Within(.001f));
                Assert.That(passenger.Simulated, Is.True);
                Invoke(follow, "LateUpdate");
                Assert.That(passenger.ControlEnabled, Is.True);
            });
        }

        static void SetCaptureFixtureState(NetworkPlayer actor, PlayerState state)
        {
            bool spawned = actor.IsSpawned;
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, false);
            ((NetworkVariable<PlayerState>)typeof(NetworkPlayer).GetField("state", CaptureMembers).GetValue(actor)).Value = state;
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, spawned);
        }

        static void ForgetCaptureFixtureLinks(PiggybackSystem stack)
        {
            // Reproduce the empty link snapshot without invoking host state or player input.
            ((Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("belowOf", CaptureMembers).GetValue(stack)).Clear();
            ((Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("aboveOf", CaptureMembers).GetValue(stack)).Clear();
        }
    }
}
