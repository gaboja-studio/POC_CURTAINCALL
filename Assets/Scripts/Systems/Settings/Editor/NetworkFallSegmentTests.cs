using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Settings.EditorTools
{
    // Real collider support queries with synthetic RPC/NetworkTransform order, not a live network test.
    [Category("NetworkFallSegments")]
    public sealed class NetworkFallSegmentTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        NetworkPlayer player;
        PlayerMover mover;
        System.Func<NetworkPlayer, PlayerState, bool> previousDamageGate;
        CurtainCall.Survival.SurvivalPlace previousPlace;
        readonly Vector3 feet = new(6800, 100, 6800);
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }

        [SetUp]
        public void SetUp()
        {
            previousDamageGate = NetworkPlayer.ServerCanChangeState;
            previousPlace = CurtainCall.Survival.SurvivalRules.ServerPlace;
            NetworkPlayer.ServerCanChangeState = null;
            CurtainCall.Survival.SurvivalRules.ServerPlace = CurtainCall.Survival.SurvivalPlace.Exploration;
            var go = Make("Synthetic remote fall actor");
            mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, false);
            var floor = Make("Synthetic stair tread").AddComponent<BoxCollider>();
            floor.size = new Vector3(8, 1, 8); floor.transform.position = feet - Vector3.up * .5f;
            player.transform.position = feet;
            player.BeginFallAt(feet);
            Physics.SyncTransforms();
            Assert.IsTrue(WalkableSupport.Contains(mover, feet));
        }

        [TearDown]
        public void TearDown()
        {
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            NetworkPlayer.ServerCanChangeState = previousDamageGate;
            CurtainCall.Survival.SurvivalRules.ServerPlace = previousPlace;
        }

        [Test]
        public void OwnerWalkingSupportRemovesHostOnlyOldPeakBeforeSmallStairJump()
        {
            // The remote interpolated pose loses support on a higher stair, although the owner walks.
            player.transform.position = feet + Vector3.up * 8f;
            player.BeginFallAt(player.transform.position);
            Call(player, "LateUpdate");
            player.transform.position = feet;
            Physics.SyncTransforms(); Call(player, "LateUpdate");
            Assert.AreEqual(8f, player.FallHeightAt(feet), "This reproduces the stale host-only airborne record.");
            Assert.IsTrue((bool)Call(player, "AcceptFallSupport", 0u, 0, 0, feet));
            Assert.AreEqual(0f, player.FallHeightAt(feet));
            player.transform.position = feet + Vector3.up * .8f;
            Physics.SyncTransforms(); Call(player, "LateUpdate");
            Assert.That(player.FallHeightAt(feet), Is.EqualTo(.8f).Within(.001f));
            player.transform.position = feet; Physics.SyncTransforms();
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", 1u, 0, 0, feet.y + .8f, feet));
            Assert.AreEqual(BodyPart.None, player.LostParts);
            Assert.AreEqual(PlayerState.Normal, player.State);
        }

        [Test]
        public void OrdinaryLandingAlsoWaitsForDelayedServerPoseWithoutResamplingItsHeight()
        {
            player.transform.position = feet + Vector3.up * 4.5f;
            player.BeginFallAt(feet + Vector3.up * .8f);
            Assert.IsFalse((bool)Get(player, "fallTeleportPending"));
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", 1u, 0, 0, feet.y + .8f, feet));
            Assert.IsTrue((bool)Get(player, "fallTeleportPending"));
            Call(player, "LateUpdate");
            Assert.AreEqual(0f, player.FallHeightAt(feet));
            player.transform.position = feet;
            Physics.SyncTransforms(); Call(player, "LateUpdate");
            Assert.IsFalse((bool)Get(player, "fallTeleportPending"));
            Assert.AreEqual(0f, player.FallHeightAt(feet));
        }

        [Test]
        public void DelayedLandingAckDoesNotEraseNextJumpOrConfirmItsNewLanding()
        {
            Call(player, "QueueFallLanding", feet.y + .8f, feet);
            uint previous = (uint)Get(player, "fallSequence");
            player.transform.position = feet + Vector3.up * 1.5f;
            Physics.SyncTransforms(); Call(player, "LateUpdate");
            Assert.AreEqual(feet, Get(player, "pendingLandingFeet"), "Retries keep the original landing contact.");
            Assert.That((float)Get(player, "pendingLandingPeak"), Is.EqualTo(feet.y + .8f).Within(.001f));
            Call(player, "AcknowledgeFallLanding", previous, 0, 0);
            Assert.IsTrue((bool)Get(player, "fallAirborne"));
            Assert.AreEqual(1.5f, player.FallHeightAt(feet), "The next jump survives the previous ACK.");
            Call(player, "QueueFallLanding", feet.y + 1.5f, feet);
            uint next = (uint)Get(player, "fallSequence");
            Assert.AreNotEqual(previous, next);
            Call(player, "AcknowledgeFallLanding", previous, 0, 0);
            Assert.IsTrue((bool)Get(player, "landingPending"), "Old ACK cannot acknowledge a new contact.");
            Call(player, "AcknowledgeFallLanding", next, 0, 0);
            Assert.IsFalse((bool)Get(player, "landingPending"));
        }

        [TestCase("air"), TestCase("epoch"), TestCase("sequence")]
        public void UnsupportedOrStaleWalkingReportCannotEraseAnActualFall(string invalid)
        {
            player.BeginFallAt(feet + Vector3.up * 4.5f);
            Vector3 point = invalid == "air" ? feet + Vector3.up : feet;
            Assert.IsFalse((bool)Call(player, "AcceptFallSupport", invalid == "sequence" ? 1u : 0u,
                invalid == "epoch" ? 1 : 0, 0, point));
            Assert.AreEqual(4.5f, player.FallHeightAt(feet));
        }

        [Test]
        public void FutureJumpPeakCannotDamagePreviousLandingAndSurvivesItsAckRetry()
        {
            player.transform.position = feet + Vector3.up * 4.5f;
            Assert.IsTrue((bool)Call(player, "AcceptFallPeak", feet.y + 4.5f, 0, 0, 2u));
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", 1u, 0, 0, feet.y + .8f, feet));
            Assert.AreEqual(BodyPart.None, player.LostParts, "A later 4.5m peak is not part of this 0.8m landing.");
            Assert.AreEqual(PlayerState.Normal, player.State);
            Assert.AreEqual(2u, Get(player, "ownerPeakSequence"));
            Assert.AreEqual(4.5f, player.FallHeightAt(feet), "The next real fall still retains its validated peak.");
            Assert.AreEqual(CurtainCall.Damage.FallOutcome.Injured, CurtainCall.Damage.CommonDamageRules.Fall(player.FallHeightAt(feet)));
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", 1u, 0, 0, feet.y + .8f, feet),
                "An accepted duplicate asks the RPC wrapper to resend ACK without applying damage or resetting the next fall.");
            Assert.AreEqual(4.5f, player.FallHeightAt(feet));
            Assert.IsFalse((bool)Call(player, "AcceptFallSupport", 1u, 0, 0, feet), "An old grounded segment cannot erase the newer jump.");
        }

        [TestCase(4f, 2, PlayerState.Normal), TestCase(8f, 0, PlayerState.Fallen)]
        public void HostOwnerQueuePreservesMeasuredHighFallForAcceptanceAndDamage(float height, int lostLimbs, PlayerState state)
        {
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, true);
            player.BeginFallAt(feet + Vector3.up * height);
            Call(player, "QueueFallLanding", feet.y + height, feet);
            uint sequence = (uint)Get(player, "fallSequence");
            Assert.AreEqual(0f, player.FallHeightAt(feet), "The owner's next movement segment starts at contact.");
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", sequence, 0, 0, feet.y + height, feet),
                "The measured host segment must survive the queue reset and pass the same upper-bound validation.");
            Assert.AreEqual(lostLimbs, PlayerCondition.CountLimbs(player.LostParts));
            Assert.AreEqual(state, player.State);
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", sequence, 0, 0, feet.y + height, feet));
            Assert.AreEqual(lostLimbs, PlayerCondition.CountLimbs(player.LostParts), "A retry cannot cut another pair of limbs.");
        }
    }
}
