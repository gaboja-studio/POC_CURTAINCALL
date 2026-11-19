using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Settings.EditorTools
{
    // Real collider support queries with synthetic RPC/NetworkTransform order, not a live network test.
    [Category("NetworkFallSegments")]
    [Category("November11CommonFix")]
    [Category("CommonProgressionRegression")]
    public sealed class NetworkFallSegmentTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        NetworkPlayer player;
        PlayerMover mover;
        System.Func<NetworkPlayer, PlayerState, bool> previousDamageGate;
        CurtainCall.Survival.SurvivalPlace previousPlace;
        UnityEngine.SceneManagement.Scene testScene;
        string previousSceneName;
        readonly Vector3 feet = new(6800, 100, 6800);
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }

        [SetUp]
        public void SetUp()
        {
            testScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            previousSceneName = testScene.name;
            testScene.name = "FallSegmentRegression";
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
            testScene.name = previousSceneName;
        }

        [TestCase("WaitingRoom", 5f)] [TestCase("WaitingRoom", 15f)]
        public void WaitingRoomLandingSafetyApplies(string sceneName, float height)
        {
            var settings = GameSettings.WaitingRoom;
            string saved = JsonUtility.ToJson(settings);
            try
            {
                testScene.name = sceneName;
                JsonUtility.FromJsonOverwrite("{\"safeFallLandings\":true}", settings);
                player.CompleteFall(height, feet);
                Assert.AreEqual(PlayerState.Normal, player.State);
                Assert.AreEqual(BodyPart.None, player.LostParts);
                JsonUtility.FromJsonOverwrite("{\"safeFallLandings\":false}", settings);
                player.CompleteFall(height, feet);
                if (height >= GameSettings.Base.BodyDamage.FallDeathHeight) Assert.AreEqual(PlayerState.Fallen, player.State);
                else Assert.AreEqual(GameSettings.Base.BodyDamage.FallInjuryCutCount, PlayerCondition.CountLimbs(player.LostParts));
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(saved, settings);
                testScene.name = "FallSegmentRegression";
            }
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

        [TestCase(0f)] [TestCase(12f)] [TestCase(60f)]
        [Category("EscapeNovember16Recovery")]
        public void WorldDeathAcceptsVerticalNetworkLagBelowTheSharedPlane(float verticalLag)
        {
            CurtainCall.Survival.SurvivalRules.ServerPlace = CurtainCall.Survival.SurvivalPlace.EscapeRoute;
            player.transform.position = new Vector3(feet.x, GameSettings.Base.BodyDamage.WorldDeathY - 1f, feet.z);
            var reported = player.transform.position - Vector3.up * verticalLag;
            Assert.IsTrue((bool)Call(player, "AcceptWorldDeath", reported, 0, 0));
            Assert.That(player.State, Is.EqualTo(PlayerState.Retired));
        }

        [Test, Category("EscapeNovember16Recovery")]
        public void WorldDeathRejectsOldEpochHorizontalTeleportAndRespawnAboveThePlane()
        {
            player.transform.position = new Vector3(feet.x, GameSettings.Base.BodyDamage.WorldDeathY - 1f, feet.z);
            var reported = player.transform.position - Vector3.up * 20f;
            Assert.IsFalse((bool)Call(player, "AcceptWorldDeath", reported, -1, 0));
            Assert.IsFalse((bool)Call(player, "AcceptWorldDeath", reported, 0, -1));
            Assert.IsFalse((bool)Call(player, "AcceptWorldDeath", reported + Vector3.right * 6, 0, 0));
            player.transform.position = feet;
            Assert.IsFalse((bool)Call(player, "AcceptWorldDeath", reported, 0, 0));
            Assert.That(player.State, Is.EqualTo(PlayerState.Normal));
        }

        [Test]
        public void DevLaunchHoldsGravityAndWorldDeathOnlyUntilPlacementCompletes()
        {
            var launcher = Make("Dev launch in progress").AddComponent<CurtainCall.Dev.DevStartLauncher>();
            Call(launcher, "Awake");
            var busy = launcher.GetType().GetField("<IsBusy>k__BackingField", Private);
            try
            {
                busy.SetValue(launcher, true);
                player.transform.position = new Vector3(feet.x, GameSettings.Base.BodyDamage.WorldDeathY - 1, feet.z);
                var position = player.transform.position;
                Set(mover, "verticalSpeed", -30f);
                Call(mover, "Update");
                Assert.AreEqual(position, player.transform.position, "An unplaced player must not fall through the loading scene.");
                Assert.AreEqual(0f, Get(mover, "verticalSpeed"));
                Assert.IsFalse((bool)Call(player, "AcceptWorldDeath", position, 0, 0));
                Assert.AreEqual(PlayerState.Normal, player.State);
                busy.SetValue(launcher, false);
                Assert.IsTrue((bool)Call(player, "AcceptWorldDeath", position, 0, 0), "Normal world-death rules resume immediately after launch.");
            }
            finally { busy.SetValue(launcher, false); Object.DestroyImmediate(launcher.gameObject); }
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
            player.BeginFallAt(feet + Vector3.up * 5.5f);
            Vector3 point = invalid == "air" ? feet + Vector3.up : feet;
            Assert.IsFalse((bool)Call(player, "AcceptFallSupport", invalid == "sequence" ? 1u : 0u,
                invalid == "epoch" ? 1 : 0, 0, point));
            Assert.AreEqual(5.5f, player.FallHeightAt(feet));
        }

        [TestCase(false), TestCase(true)]
        public void RemoteOwnerCanFallAgainAfterLandingWithoutRespawning(bool hostFlagArrivesFirst)
        {
            // Only the host resets tracking when it accepts the landing. The owner
            // receives the replicated drop flag and the landing ACK independently.
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, false);
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, true);
            var hostDrop = (NetworkVariable<bool>)Get(player, "droppingFromSupport");
            Set(player, "localSupportDrop", true);
            hostDrop.Value = true;
            Call(player, "QueueFallLanding", feet.y + 1f, feet);
            uint contact = (uint)Get(player, "fallSequence");

            Call(player, "AcknowledgeFallLanding", contact, 1, 0);
            Assert.IsTrue((bool)Get(player, "landingPending"), "A previous life cannot acknowledge this landing.");
            if (hostFlagArrivesFirst)
            {
                hostDrop.Value = false;
                Assert.IsTrue(player.IsDroppingFromSupport, "The owner still awaits its landing ACK.");
            }
            Call(player, "AcknowledgeFallLanding", contact, 0, 0);
            if (!hostFlagArrivesFirst)
            {
                Assert.IsTrue(player.IsDroppingFromSupport, "The host flag can still be in flight.");
                hostDrop.Value = false;
            }
            Assert.IsFalse(player.IsDroppingFromSupport, "The completed fall must release the owner's local guard.");
            player.RequestNaturalFall();
            Assert.IsTrue((bool)Get(player, "naturalFallQueued"), "A second fall must be accepted without a respawn.");

            // Once a new fall has started, a duplicate ACK of the previous contact must be inert.
            Call(player, "PrepareNaturalFall");
            Set(player, "localSupportDrop", true);
            Call(player, "AcknowledgeFallLanding", contact, 0, 0);
            Assert.IsTrue(player.IsDroppingFromSupport);
        }

        [Test]
        public void FutureJumpPeakCannotDamagePreviousLandingAndSurvivesItsAckRetry()
        {
            // 서버 자세는 착지 검증 거리(5m) 안, 보고 최고점은 자세+2m 상한 안에 둔다.
            player.transform.position = feet + Vector3.up * 4.5f;
            Assert.IsTrue((bool)Call(player, "AcceptFallPeak", feet.y + 5.5f, 0, 0, 2u));
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", 1u, 0, 0, feet.y + .8f, feet));
            Assert.AreEqual(BodyPart.None, player.LostParts, "A later 5.5m peak is not part of this 0.8m landing.");
            Assert.AreEqual(PlayerState.Normal, player.State);
            Assert.AreEqual(2u, Get(player, "ownerPeakSequence"));
            Assert.AreEqual(5.5f, player.FallHeightAt(feet), "The next real fall still retains its validated peak.");
            Assert.AreEqual(CurtainCall.Damage.FallOutcome.Injured, CurtainCall.Damage.CommonDamageRules.Fall(player.FallHeightAt(feet)));
            Assert.IsTrue((bool)Call(player, "AcceptFallLanding", 1u, 0, 0, feet.y + .8f, feet),
                "An accepted duplicate asks the RPC wrapper to resend ACK without applying damage or resetting the next fall.");
            Assert.AreEqual(5.5f, player.FallHeightAt(feet));
            Assert.IsFalse((bool)Call(player, "AcceptFallSupport", 1u, 0, 0, feet), "An old grounded segment cannot erase the newer jump.");
        }

        [TestCase(4.5f, 0, PlayerState.Normal)]
        [TestCase(7f, 2, PlayerState.Normal)]
        [TestCase(17f, 0, PlayerState.Fallen)]
        [Category("FinalEscape164")]
        public void CannonLandingUsesItsOwnPeakAfterAnOldHigherPose(float drop, int lostLimbs, PlayerState state)
        {
            var system = Make("Cannon runtime").AddComponent<CurtainCall.HumanCannon.HumanCannonSystem>();
            const ulong actor = 2;
            typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(player, actor);
            var all = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            all.Add(player);
            try
            {
                Set(system, "stations", new CurtainCall.HumanCannon.HumanCannonStation[1]);
                Call(system, "ResetSeats");
                player.transform.position = feet + Vector3.up * 20f;
                player.BeginFallAt(player.transform.position);
                var muzzle = feet + Vector3.up * 1f;
                Call(system, "Apply", 0, CurtainCall.HumanCannon.CannonAction.Launch, actor, muzzle, Quaternion.identity, Vector3.up);
                Call(player, "LateUpdate");
                Assert.AreEqual(1f, player.FallHeightAt(feet), "The host must neither retain nor sample the old 20m pose.");
                player.transform.position = muzzle;
                Call(player, "LateUpdate");
                player.transform.position = feet + Vector3.up * drop;
                Call(player, "LateUpdate");
                player.transform.position = feet;
                var missingSupport = feet + Vector3.right * 3f + Vector3.up;
                Call(system, "HandleLanding", player, 0, 0u, 0u, missingSupport);
                Assert.That(system.IsFlying(actor));
                Assert.AreEqual(BodyPart.None, player.LostParts, "No damage from a surface the host cannot verify.");
                Assert.AreEqual(drop, player.FallHeightAt(feet));
                Call(system, "HandleLanding", player, 0, 0u, 0u, feet);
                Assert.IsFalse(system.IsFlying(actor), "Actual support must finish the flight.");
                Assert.AreEqual(lostLimbs, PlayerCondition.CountLimbs(player.LostParts));
                Assert.AreEqual(state, player.State);
            }
            finally { all.Remove(player); }
        }

        [Test]
        [Category("FinalEscape164")]
        public void CannonOwnerRequiresSupportForBelowCollisionButReportsWorldDeath()
        {
            var pilot = typeof(CurtainCall.HumanCannon.HumanCannonPilot);
            var report = pilot.GetMethod("ShouldReportLanding", BindingFlags.Static | BindingFlags.NonPublic);
            bool Report(CollisionFlags flags, Vector3 point) => (bool)report.Invoke(null, new object[] { mover, flags, -2f, point });
            Assert.IsFalse(Report(CollisionFlags.Sides, feet));
            Assert.IsFalse(Report(CollisionFlags.Below | CollisionFlags.Sides, feet + Vector3.up));
            Assert.IsTrue(Report(CollisionFlags.Below, feet));
            var death = new Vector3(feet.x, GameSettings.Base.BodyDamage.WorldDeathY, feet.z);
            Assert.IsTrue(Report(CollisionFlags.None, death));
        }

        [Test]
        [Category("FinalEscape164")]
        public void RejectedLandingMessageResumesOnlyTheMatchingFlightSegment()
        {
            const ulong actor = 2;
            typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(player, actor);
            var system = Make("Cannon receiver").AddComponent<CurtainCall.HumanCannon.HumanCannonSystem>();
            var pilot = player.gameObject.AddComponent<CurtainCall.HumanCannon.HumanCannonPilot>();
            Set(system, "boundPlayer", player);
            Set(system, "pilot", pilot);
            var flights = (CurtainCall.HumanCannon.CannonFlightState)Get(system, "flying");
            flights.Begin(actor, 0, 9u);
            Assert.IsTrue(flights.TryRebound(actor, 0, 9u, 0u));
            typeof(CurtainCall.HumanCannon.HumanCannonPilot).GetProperty("IsFlying").SetValue(pilot, true);
            Set(pilot, "flightStationIndex", 0);
            Set(pilot, "launchRevision", 9u);
            Set(pilot, "flightSegment", 1u);
            Set(pilot, "awaitingLanding", true);

            void Retry(ulong sender, uint segment)
            {
                using var writer = new FastBufferWriter(21, Allocator.Temp);
                writer.WriteValueSafe(actor);
                writer.WriteValueSafe(0);
                writer.WriteValueSafe(9u);
                writer.WriteValueSafe(segment);
                writer.WriteValueSafe((byte)2);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                Call(system, "HandleLandingMessage", sender, reader);
            }

            Retry(3, 1);
            Retry(NetworkManager.ServerClientId, 0);
            Assert.IsTrue((bool)Get(pilot, "awaitingLanding"), "A non-host or stale segment must not resume the flight.");
            Retry(NetworkManager.ServerClientId, 1);
            Assert.IsFalse((bool)Get(pilot, "awaitingLanding"));
            Assert.IsTrue(flights.Matches(actor, 0, 9u, 1u));
        }

        [TestCase(4.99f, 0, PlayerState.Normal), TestCase(5f, 2, PlayerState.Normal)]
        [TestCase(14.99f, 2, PlayerState.Normal), TestCase(15f, 0, PlayerState.Fallen)]
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
