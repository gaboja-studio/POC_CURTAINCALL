using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Network.Session;
using CurtainCall.HumanCannon;
using CurtainCall.SpaceLink;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    // Synthetic authority/state-order tests; no live clients, transport or PlayMode simulation.
    [Category("MedicalReservationRecovery")]
    [Category("CommonProgressionRegression")]
    public sealed class ExpertRoomRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> players = new();
        readonly List<GameObject> existingRooms = new();
        ExpertService previousService, service;
        CampaignRun previousRun;
        NetworkPlayer previousLocal, local;
        ExpertRoomView room;
        ExpertServiceBook book;
        Transform inside, outside;
        static List<NetworkPlayer> All => (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void SetLocal(NetworkPlayer player) => typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);

        GameObject Make(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(go);
            return go;
        }

        NetworkPlayer Player(ulong id)
        {
            var go = Make("Medical recovery player " + id);
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(player, id);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            All.Add(player); players.Add(player);
            return player;
        }

        [SetUp]
        public void SetUp()
        {
            // Enter resolves the active scene entrance. Isolate this synthetic room when the
            // developer has the real WaitingRoom open, then restore that scene after the test.
            foreach (var existing in Object.FindObjectsByType<ExpertRoomView>(FindObjectsSortMode.None))
                if (existing.gameObject.activeInHierarchy) { existingRooms.Add(existing.gameObject); existing.gameObject.SetActive(false); }
            previousService = ExpertService.Instance; previousRun = CampaignRun.Instance; previousLocal = NetworkPlayer.Local;
            typeof(ExpertService).GetProperty("Instance").SetValue(null, null);
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, null);
            service = Make("Medical recovery service").AddComponent<ExpertService>(); Call(service, "Awake");
            book = (ExpertServiceBook)Get(service, "book");
            local = Player(0); SetLocal(local);
            room = Make("Medical recovery room").AddComponent<ExpertRoomView>();
            room.gameObject.hideFlags = HideFlags.HideInHierarchy; // The real Enter path excludes DontSave objects from its scene lookup.
            inside = Make("Medical inside").transform; inside.position = new Vector3(0, 0, 1.5f);
            outside = Make("Medical outside").transform; outside.position = new Vector3(0, 0, -1.4f);
            Set(room, "inside", inside); Set(room, "outside", outside);
            // Same mesh height as the active WaitingRoom lift (not a special interaction distance).
            var left = Make("Medical left door").transform; left.position = new Vector3(-.8f, 1.8f, 0);
            var right = Make("Medical right door").transform; right.position = new Vector3(.8f, 1.8f, 0);
            left.SetParent(room.transform, true); right.SetParent(room.transform, true);
            Set(room, "curtainLeft", left); Set(room, "curtainRight", right);
            Set(room, "leftClosed", left.localPosition); Set(room, "rightClosed", right.localPosition);
            local.GetComponent<PlayerMover>().SetStartPose(outside.position, Quaternion.identity);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var player in players)
            {
                All.Remove(player);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            }
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear(); players.Clear();
            typeof(ExpertService).GetProperty("Instance").SetValue(null, previousService);
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            SetLocal(previousLocal);
            foreach (var existing in existingRooms) if (existing != null) existing.SetActive(true);
            existingRooms.Clear();
        }

        void BeginLocalWork()
        {
            Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, 1, 1, 3, () => true, 100));
            Assert.AreEqual(ExpertRefusal.None, book.Enter(0));
            Call(room, "Bind", service);
            Assert.IsTrue((bool)Get(room, "localInside"));
            Assert.IsTrue(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
        }

        void WithRehearsal(System.Action<RehearsalLifecycleTests, object, object> test)
        {
            var lifecycle = new RehearsalLifecycleTests(); lifecycle.SetUp();
            var previousDirector = SpaceLinkDirector.Instance;
            var previousScene = SceneManager.GetActiveScene();
            var previousName = previousScene.name;
            try
            {
                previousScene.name = GameSettings.Base.SpaceTransition.WaitingRoomScene;
                var director = Make("Medical rehearsal director").AddComponent<SpaceLinkDirector>();
                typeof(SpaceLinkDirector).GetProperty("Instance").SetValue(null, director);
                var run = (CampaignRun)Get(lifecycle, "run"); var state = run.State; state.CannonUnlocked = true; Set(run, "state", state);
                test(lifecycle, Get(lifecycle, "cannon"), Get(lifecycle, "rehearsal"));
            }
            finally
            {
                typeof(SpaceLinkDirector).GetProperty("Instance").SetValue(null, previousDirector);
                previousScene.name = previousName;
                lifecycle.TearDown();
            }
        }

        [TestCase(2), TestCase(3), TestCase(4)]
        public void TreatmentAndExitBlockBothRehearsalsButBookingAndFinishedTreatmentDoNot(int participants)
        {
            for (int i = 1; i < participants; i++) Player((ulong)i);
            WithRehearsal((_, _, _) =>
            {
                book.BookMedical(0, true, true, 1, 1, 3, () => true, 100);
                Assert.IsTrue(WaitingRoomCannonRehearsal.CanPractice);
                Assert.IsTrue(WaitingRoomRehearsal.CanProposePractice);
                book.Enter(0); Call(room, "Bind", service);
                Assert.IsFalse(WaitingRoomCannonRehearsal.CanPractice);
                Assert.IsFalse(WaitingRoomRehearsal.CanProposePractice);
                Assert.IsFalse(WaitingRoomCannonRehearsal.StartPractice());
                book.Tick(1000f); Call(room, "ReconcileLocalOccupancy");
                Assert.IsFalse(WaitingRoomCannonRehearsal.CanPractice, "Exit snapshot must finish before relocation is offered.");
                Assert.IsFalse(WaitingRoomRehearsal.CanProposePractice);
                book.Tick(1000f);
                Assert.IsTrue(WaitingRoomCannonRehearsal.CanPractice);
                Assert.IsTrue(WaitingRoomRehearsal.CanProposePractice);
                Assert.AreEqual(outside.position, local.transform.position);
                Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            });
        }

        [TestCase("cannon"), TestCase("rope"), TestCase("pending")]
        public void LateMedicalEntryCannotMoveARehearsalParticipant(string operation)
        {
            WithRehearsal((life, cannon, rope) =>
            {
                if (operation == "cannon") Call(life, "Shared", "active", true);
                else Set(rope, operation == "rope" ? "active" : "practiceVote", true);
                book.BookMedical(0, true, true, 1, 1, 3, () => true, 100);
                Call(room, "Bind", service); Physics.SyncTransforms();
                var result = ((bool ok, string text))Call(service, "Enter", 0UL);
                Assert.IsFalse(result.ok);
                Assert.AreEqual(ExpertRoomPhase.Booked, book.Phase, "Keep the paid reservation for after rehearsal.");
                Assert.AreEqual(outside.position, local.transform.position);
                Assert.IsFalse((bool)Get(room, "localInside"));
                Set(rope, "active", false); Set(rope, "practiceVote", false);
            });
        }

        [Test]
        public void EarlyRehearsalSnapshotWaitsForMedicalExitBeforeCapturingReturnPose()
        {
            BeginLocalWork();
            WithRehearsal((life, cannon, _) =>
            {
                var course = Make("Medical delayed rehearsal course").AddComponent<RehearsalCannonCourse>();
                var floor = Make("Medical delayed rehearsal floor").AddComponent<BoxCollider>();
                floor.transform.position = new Vector3(9100, 9000, 9000); floor.size = new Vector3(12, 1, 8);
                course.StartFloor = course.MiddleFloor = floor; Set(cannon, "course", course);
                Call(life, "Shared", "active", true); Call(life, "Shared", "round", 3); Physics.SyncTransforms();
                var mover = local.GetComponent<PlayerMover>();
                Call(cannon, "CaptureReturnPose", local); Call(cannon, "PlaceLocalAtCheckpoint", local, mover);
                Assert.AreEqual(-1, Get(cannon, "returnPoseRound"));
                Assert.AreEqual(inside.position, local.transform.position);
                Assert.IsFalse((bool)Call(cannon, "CaptureEndingMover"), "An early ending packet must wait too.");
                book.Tick(1000f); Call(room, "ReconcileLocalOccupancy");
                Call(cannon, "PlaceLocalAtCheckpoint", local, mover);
                Assert.AreEqual(outside.position, local.transform.position);
                book.Tick(1000f);
                Call(cannon, "PlaceLocalAtCheckpoint", local, mover);
                Assert.IsTrue(RehearsalCannonCourse.Contains(floor, local.transform.position));
                Assert.AreEqual(outside.position, ((Pose)Get(cannon, "returnPose")).position);
                Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
                Call(room, "ReconcileLocalOccupancy");
                Assert.IsTrue(RehearsalCannonCourse.Contains(floor, local.transform.position));
            });
        }

        void WithAutomaticReturn(System.Action<HumanCannonTrampoline, CannonSeatState, WaitingRoomStageDevices> test)
        {
            var previousSystem = HumanCannonSystem.Current;
            var stageField = typeof(WaitingRoomStageDevices).GetField("instance", Static);
            var previousStage = stageField.GetValue(null);
            var shift = new Vector3(9000, 9000, 9000);
            room.transform.position += shift; inside.position += shift; outside.position += shift;
            local.gameObject.hideFlags = HideFlags.None;
            local.GetComponent<PlayerMover>().SetStartPose(outside.position, Quaternion.identity);
            var systemObject = Make("Return authority cannon"); systemObject.SetActive(false);
            var system = systemObject.AddComponent<HumanCannonSystem>();
            Set(system, "seats", System.Array.CreateInstance(typeof(HumanCannonSystem).GetField("seats", Private).FieldType.GetElementType(), 0));
            typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, system);
            try
            {
                WithCarriedTrampoline((trampoline, ownership) =>
                {
                    trampoline.gameObject.hideFlags = HideFlags.None;
                    Call(trampoline, "RefreshCarryBody", local);
                    var devices = Make("Reserved return devices").AddComponent<WaitingRoomStageDevices>(); stageField.SetValue(null, devices);
                    Set(devices, "room", Make("Reserved room").transform); Set(devices, "trampoline", trampoline.gameObject);
                    typeof(WaitingRoomStageDevices).GetMethod("ReserveCarrierForRehearsal", Static).Invoke(null, null);
                    ownership.TryApply(CannonAction.Release, 0, ownership.Revision);
                    Physics.SyncTransforms();
                    test(trampoline, ownership, devices);
                });
            }
            finally { stageField.SetValue(null, previousStage); typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, previousSystem); }
        }

        [TestCase(2, false), TestCase(2, true), TestCase(3, false), TestCase(3, true), TestCase(4, false), TestCase(4, true)]
        [Category("RehearsalReturnCaptureRegression")]
        public void AutomaticReturnAndMedicalEntryAreSafeInEitherOrder(int participants, bool returnFirst)
        {
            using var input = new CurtainCall.Tightrope.EditorTools.Tests.InputGateTestScope();
            for (int i = 1; i < participants; i++) Player((ulong)i);
            WithAutomaticReturn((trampoline, ownership, devices) =>
            {
                if (returnFirst) { Call(devices, "RestoreReservedCarrier"); Assert.AreEqual(0UL, trampoline.Carrier); }
                book.BookMedical(0, true, true, 1, 1, 3, () => true, 100);
                SetLocal(null); // The host still has the remote player's entrance position.
                var entered = ((bool ok, string text))Call(service, "Enter", 0UL);
                Assert.IsTrue(entered.ok, entered.text);
                for (int i = 0; i < 3; i++) Call(devices, "RestoreReservedCarrier");
                Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                Assert.IsFalse(trampoline.CanGrab(local));
                SetLocal(local); Call(room, "Bind", service); Call(room, "ReconcileLocalOccupancy");
                Assert.IsTrue((bool)Get(room, "localInside"));
                Assert.AreEqual(inside.position, local.transform.position);
                book.Tick(1000f); Call(room, "ReconcileLocalOccupancy");
                Call(devices, "RestoreReservedCarrier");
                Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier, "Exiting still owns the player.");
                book.Tick(1000f); Call(room, "ReconcileLocalOccupancy");
                local.GetComponent<PlayerMover>().SetStartPose(outside.position, Quaternion.identity);
                Call(devices, "RestoreReservedCarrier");
                Assert.AreEqual(returnFirst ? CannonSeatState.Nobody : 0UL, trampoline.Carrier);
                Assert.IsNull(Get(devices, "reservedCarrier"));
                Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            });
        }

        [Test, Category("RehearsalReturnCaptureRegression")]
        public void BlockedAutomaticReturnExpiresWithoutReattachingAfterTreatment()
        {
            WithAutomaticReturn((trampoline, ownership, devices) =>
            {
                book.BookMedical(0, true, true, 1, 1, 3, () => true, 100); book.Enter(0);
                Call(devices, "RestoreReservedCarrier"); Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                Set(devices, "resumeUntil", Time.unscaledTime - 1f); Call(devices, "RestoreReservedCarrier");
                Assert.IsNull(Get(devices, "reservedCarrier"));
                book.Tick(1000f); book.Tick(1000f); Call(devices, "RestoreReservedCarrier");
                Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
            });
        }

        [Test, Category("RehearsalReturnCaptureRegression")]
        public void AutomaticReturnUsesTheSamePracticePermissionAsManualGrab()
        {
            WithAutomaticReturn((trampoline, ownership, devices) =>
            {
                var permission = typeof(HumanCannonTrampoline).GetProperty("PracticeGrabAllowed", Private);
                permission.SetValue(trampoline, (System.Func<bool>)(() => false));
                Assert.IsFalse(trampoline.CanGrab(local)); Call(devices, "RestoreReservedCarrier");
                Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                permission.SetValue(trampoline, (System.Func<bool>)(() => true));
                Call(devices, "RestoreReservedCarrier"); Assert.AreEqual(0UL, trampoline.Carrier);
            });
        }

        [Test, Category("RehearsalReturnCaptureRegression")]
        public void AutomaticReturnWaitsUntilTheRiderDismounts()
        {
            var previousStack = PiggybackSystem.Current;
            try
            {
                WithAutomaticReturn((trampoline, ownership, devices) =>
                {
                    var carrier = Player(1); carrier.transform.position = outside.position + Vector3.back * 4;
                    var stack = Make("Return rider stack").AddComponent<PiggybackSystem>();
                    typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);
                    var below = (Dictionary<ulong, ulong>)Get(stack, "belowOf"); var above = (Dictionary<ulong, ulong>)Get(stack, "aboveOf");
                    below[0] = 1; above[1] = 0;
                    Call(devices, "RestoreReservedCarrier"); Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                    below.Clear(); above.Clear(); Call(devices, "RestoreReservedCarrier");
                    Assert.AreEqual(0UL, trampoline.Carrier);
                });
            }
            finally { typeof(PiggybackSystem).GetProperty("Current").SetValue(null, previousStack); }
        }

        [TestCase(2, "retired"), TestCase(3, "retired"), TestCase(4, "retired")]
        [TestCase(2, "disconnected"), TestCase(3, "disconnected"), TestCase(4, "disconnected")]
        [TestCase(2, "released"), TestCase(3, "released"), TestCase(4, "released")]
        [Category("RehearsalReturnCaptureRegression")]
        public void CaptureBlocksBothRehearsalsAndLatePlacementButDoesNotPermanentlyBlockTheTeam(int participants, string recovery)
        {
            for (int i = 1; i < participants; i++) Player((ulong)i);
            var current = typeof(Pursuit.PursuitDirector).GetProperty("Current"); var previous = current.GetValue(null);
            try
            {
                WithRehearsal((life, cannon, rope) =>
                {
                    var run = (CampaignRun)Get(life, "run"); var snapshot = run.State; snapshot.Phase = CampaignPhase.Night; Set(run, "state", snapshot);
                    Assert.IsTrue(WaitingRoomCannonRehearsal.CanPractice); Assert.IsTrue(WaitingRoomRehearsal.CanProposePractice);
                    var go = Make("Rehearsal captured player"); go.SetActive(false);
                    var pursuit = go.AddComponent<Pursuit.PursuitDirector>(); current.SetValue(null, pursuit);
                    var victimType = typeof(Pursuit.PursuitDirector).GetNestedType("Victim", BindingFlags.NonPublic);
                    var victim = System.Activator.CreateInstance(victimType, true); victimType.GetField("Object").SetValue(victim, local.gameObject);
                    var victims = (System.Collections.IDictionary)Get(pursuit, "victims"); victims.Add(0UL, victim);
                    Assert.IsFalse(WaitingRoomCannonRehearsal.CanPractice); Assert.IsFalse(WaitingRoomCannonRehearsal.StartPractice());
                    Assert.IsFalse(WaitingRoomRehearsal.CanProposePractice);
                    Set(rope, "practiceVote", true); var yes = (HashSet<ulong>)Get(rope, "practiceYes");
                    for (int i = 0; i < participants; i++) yes.Add((ulong)i);
                    Call(rope, "HostTickVotes"); Assert.IsFalse((bool)Get(rope, "practice"));
                    Set(rope, "practice", true); // A previously approved start must recheck before activation.
                    Call(rope, "HostTickVotes"); Assert.IsFalse((bool)Get(rope, "practice"));
                    var mover = local.GetComponent<PlayerMover>(); mover.SetControlEnabled(false);
                    var before = local.transform.position; Set(rope, "local", local);
                    Call(rope, "Relocate", mover, before + Vector3.right * 20, Quaternion.identity);
                    Assert.AreEqual(before, local.transform.position); Assert.IsFalse(mover.ControlEnabled);
                    Call(cannon, "CaptureReturnPose", local); Call(cannon, "PlaceLocalAtCheckpoint", local, mover);
                    Assert.AreEqual(before, local.transform.position); Assert.AreEqual(-1, Get(cannon, "returnPoseRound"));
                    Assert.AreEqual(-1, Get(cannon, "placedRound"));
                    if (recovery == "retired") { ((NetworkVariable<PlayerState>)Get(local, "state")).Value = PlayerState.Retired; victimType.GetField("Retired").SetValue(victim, true); }
                    else if (recovery == "disconnected") typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(local, false);
                    else victims.Clear();
                    Set(rope, "practiceVote", false);
                    Assert.IsTrue(WaitingRoomCannonRehearsal.CanPractice); Assert.IsTrue(WaitingRoomRehearsal.CanProposePractice);
                    Set(rope, "practiceVote", true); Call(rope, "HostTickVotes");
                    Assert.IsTrue((bool)Get(rope, "practice"), "The remaining agreed players can start after capture finishes or the player leaves.");
                });
            }
            finally { current.SetValue(null, previous); }
        }

        [TestCase(2), TestCase(3), TestCase(4), Category("RehearsalReturnCaptureRegression")]
        public void EmptyRehearsalVoteDoesNotKeepNightLockedAfterEveryoneRetires(int participants)
        {
            for (int i = 1; i < participants; i++) Player((ulong)i);
            WithRehearsal((life, cannon, rope) =>
            {
                var run = (CampaignRun)Get(life, "run"); var night = new RunState(false, true, true);
                night.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
                Assert.IsTrue(night.DebugBeginPursuit()); Set(run, "host", night); Set(run, "state", night.Snapshot);
                Set(rope, "practiceVote", true); var yes = (HashSet<ulong>)Get(rope, "practiceYes");
                foreach (var player in players) { yes.Add(player.OwnerClientId); ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Retired; }
                Call(rope, "HostTickVotes"); Assert.IsFalse(WaitingRoomRehearsal.IsPracticePending);
                Assert.IsEmpty(yes); Assert.IsFalse((bool)Get(rope, "practice"));
                Call(run, "RefreshNight", night); Assert.AreEqual(CampaignPhase.Dawn, night.Snapshot.Phase);
            });
        }

        [Test, Category("RehearsalReturnCaptureRegression")]
        public void AutomaticReturnRejectsCapturedCarrierAndResumesAfterRelease()
        {
            var current = typeof(Pursuit.PursuitDirector).GetProperty("Current"); var previous = current.GetValue(null);
            try
            {
                WithAutomaticReturn((trampoline, ownership, devices) =>
                {
                    var go = Make("Captured return owner"); go.SetActive(false);
                    var pursuit = go.AddComponent<Pursuit.PursuitDirector>(); current.SetValue(null, pursuit);
                    var victimType = typeof(Pursuit.PursuitDirector).GetNestedType("Victim", BindingFlags.NonPublic);
                    var victim = System.Activator.CreateInstance(victimType, true); victimType.GetField("Object").SetValue(victim, local.gameObject);
                    var victims = (System.Collections.IDictionary)Get(pursuit, "victims"); victims.Add(0UL, victim);
                    Assert.IsFalse(trampoline.CanGrab(local)); Call(devices, "RestoreReservedCarrier");
                    Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                    victims.Clear(); Call(devices, "RestoreReservedCarrier"); Assert.AreEqual(0UL, trampoline.Carrier);
                });
            }
            finally { current.SetValue(null, previous); }
        }

        void WithCarriedTrampoline(System.Action<HumanCannonTrampoline, CannonSeatState> test)
        {
            var previous = HumanCannonTrampoline.Current;
            var host = Make("Medical carry host"); host.SetActive(false);
            var session = host.AddComponent<NetworkSessionManager>(); Set(session, "_networkManager", host.GetComponent<NetworkManager>());
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            var root = Make("Medical carried trampoline"); root.transform.position = outside.position + Vector3.forward;
            var handle = Make("Medical carry handle").transform; handle.SetParent(root.transform, false); handle.localPosition = Vector3.back;
            var trampoline = root.AddComponent<HumanCannonTrampoline>(); Set(trampoline, "handle", handle); Set(trampoline, "session", session);
            var ownership = (CannonSeatState)Get(trampoline, "ownership"); ownership.TryApply(CannonAction.Board, 0, ownership.Revision);
            typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, trampoline); HumanCannonCarry.Register(trampoline);
            try { test(trampoline, ownership); }
            finally { HumanCannonCarry.Unregister(trampoline); typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, previous); }
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void EnterReleasesCarriedTrampolineBeforeMedicalTeleport(int lostParts)
        {
            local.gameObject.AddComponent<PlayerCondition>().SetLostParts(PlayerCondition.PartsForLostCount(lostParts));
            WithCarriedTrampoline((trampoline, _) =>
            {
                book.BookMedical(0, true, true, 1, 1, 3, () => true, 100); Call(room, "Bind", service); Physics.SyncTransforms();
                var propPosition = trampoline.transform.position;
                var result = ((bool ok, string text))Call(service, "Enter", 0UL);
                Assert.IsTrue(result.ok, result.text);
                Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                Assert.AreEqual(propPosition, trampoline.transform.position, "The load stays outside rather than sweeping into the cabin.");
                Assert.AreEqual(inside.position, local.transform.position);
                Assert.IsTrue(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            });
        }

        [TestCase(true), TestCase(false)]
        public void MedicalSnapshotWaitsForLoadReleaseInEitherOrder(bool medicalFirst)
        {
            WithCarriedTrampoline((trampoline, ownership) =>
            {
                book.BookMedical(0, true, true, 1, 1, 3, () => true, 100);
                if (!medicalFirst) ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
                book.Enter(0); Call(room, "Bind", service);
                if (medicalFirst)
                {
                    Assert.AreEqual(outside.position, local.transform.position);
                    Assert.IsFalse((bool)Get(room, "localInside"));
                    ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
                    Call(room, "ReconcileLocalOccupancy");
                }
                Assert.AreEqual(inside.position, local.transform.position);
                Assert.IsTrue(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
                book.Tick(1000f); Call(room, "ReconcileLocalOccupancy");
                Assert.AreEqual(outside.position, local.transform.position);
                Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            });
        }

        [Test]
        public void MedicalOccupancyRejectsNewCarryUntilExitAndDoesNotBlockTeammates()
        {
            var other = Player(5);
            var occupied = typeof(HumanCannonCarry).GetMethod("HasOccupiedHands", BindingFlags.Static | BindingFlags.NonPublic);
            BeginLocalWork();
            Assert.IsTrue((bool)occupied.Invoke(null, new object[] { local }));
            Assert.IsFalse((bool)occupied.Invoke(null, new object[] { other }));
            book.Tick(1000f); Call(room, "ReconcileLocalOccupancy");
            Assert.IsTrue((bool)occupied.Invoke(null, new object[] { local }));
            book.Tick(1000f);
            Assert.IsFalse((bool)occupied.Invoke(null, new object[] { local }));
        }

        [Test]
        public void DelayedGrabRequestCannotReacquireTrampolineAfterTreatmentEntry()
        {
            WithCarriedTrampoline((trampoline, ownership) =>
            {
                var previous = HumanCannonSystem.Current;
                var cannon = Make("Medical late grab cannon").AddComponent<HumanCannonSystem>();
                typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, cannon);
                try
                {
                    ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
                    BeginLocalWork();
                    Assert.IsFalse(trampoline.CanGrab(local));
                    var action = System.Enum.Parse(typeof(HumanCannonTrampoline).GetNestedType("TrampolineAction", BindingFlags.NonPublic), "Grab");
                    Call(trampoline, "HandleRequest", 0UL, action, ownership.Revision, -1, 0U, 0U,
                        trampoline.transform.position, trampoline.transform.rotation, true);
                    Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                    Assert.AreEqual(inside.position, local.transform.position);
                }
                finally { typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, previous); }
            });
        }

        [TestCase(false), TestCase(true)]
        public void TreatmentEndingBeforeDelayedLoadReleaseDoesNotTeleportOrLockBuyer(bool dies)
        {
            WithCarriedTrampoline((_, ownership) =>
            {
                book.BookMedical(0, true, true, 1, 1, 3, () => true, 100); book.Enter(0); Call(room, "Bind", service);
                Assert.IsFalse((bool)Get(room, "localInside"));
                if (dies) ((NetworkVariable<PlayerState>)Get(local, "state")).Value = PlayerState.Fallen;
                else book.Tick(1000f);
                ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
                Call(room, "ReconcileLocalOccupancy");
                Assert.AreEqual(outside.position, local.transform.position);
                Assert.IsFalse((bool)Get(room, "localInside"));
                var block = (UIInputBlocker)Get(room, "workInput");
                Assert.IsTrue(block == null || !block.IsBlocking);
            });
        }

        [Test]
        public void DeathReservationCanBeReleasedBeforeUpdateAndNextBuyerCanEnter()
        {
            var previous = Player(941);
            book.BookMedical(previous.OwnerClientId, true, true, 2, 2, 3, () => true, 150);
            ((NetworkVariable<PlayerState>)Get(previous, "state")).Value = PlayerState.Fallen;
            // The new request itself must clear the dead buyer; no service Update has run.
            var purchase = ((bool ok, string text))Call(service, "BuyMedical", 0UL, 1);
            Assert.IsFalse(purchase.ok, "This uninjured test avatar cannot purchase; the old dead reservation must still clear.");
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
            Assert.AreEqual(150, book.PendingRefund);
            Call(service, "RefreshReservation");
            Assert.AreEqual(150, book.PendingRefund, "Repeated cleanup cannot add another refund.");
            Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, 1, 1, 3, () => true, 125));
            Call(room, "Bind", service);
            Physics.SyncTransforms();
            var result = ((bool ok, string text))Call(service, "Enter", 0UL);
            Assert.IsTrue(result.ok, result.text);
            Assert.AreEqual(ExpertRoomPhase.Working, book.Phase);
            Assert.IsTrue((bool)Get(room, "localInside"));
            Assert.Less(Vector3.Distance(local.transform.position, inside.position), .001f);
        }

        [Test]
        public void LateWorkingSnapshotEntersAndChangedBuyerSnapshotReleasesLocalInput()
        {
            BeginLocalWork();
            // A later synchronization can retain Working while replacing the reservation owner.
            Call(service, "HandleState", "{\"revision\":50,\"phase\":2,\"buyer\":942,\"limbs\":1,\"uses\":2}");
            Call(room, "ReconcileLocalOccupancy");
            Assert.IsFalse((bool)Get(room, "localInside"));
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            Assert.Less(Vector3.Distance(local.transform.position, outside.position), .001f);
        }

        [TestCase(2, 0)] [TestCase(2, 1)]
        [TestCase(3, 0)] [TestCase(3, 1)] [TestCase(3, 2)]
        [TestCase(4, 0)] [TestCase(4, 1)] [TestCase(4, 3)]
        public void MountedBuyerWaitsForDetachAndDoesNotSnapBackAfterEntering(int count, int buyerFloor)
        {
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(local, true);
            var previousStack = PiggybackSystem.Current;
            var stack = Make("Medical mounted stack").AddComponent<PiggybackSystem>();
            var follow = stack.gameObject.AddComponent<PiggybackFollow>(); Set(follow, "system", stack);
            var session = Make("Medical authority").AddComponent<NetworkSessionManager>();
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            Set(stack, "session", session);
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);
            try
            {
                var below = (Dictionary<ulong, ulong>)Get(stack, "belowOf");
                var above = (Dictionary<ulong, ulong>)Get(stack, "aboveOf");
                var riders = new NetworkPlayer[count];
                for (int i = 0; i < count; i++)
                {
                    riders[i] = i == buyerFloor ? local : Player((ulong)(950 + i));
                    riders[i].gameObject.AddComponent<PlayerBalance>();
                    riders[i].transform.position = outside.position + Vector3.up * i * 1.5f;
                    if (i == 0) continue;
                    below[riders[i].OwnerClientId] = riders[i - 1].OwnerClientId;
                    above[riders[i - 1].OwnerClientId] = riders[i].OwnerClientId;
                }
                Call(follow, "UpdateLocal", local);
                Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, 1, 1, 3, () => true, 100));
                Assert.AreEqual(ExpertRefusal.None, book.Enter(0));
                Call(room, "Bind", service); // Service snapshot deliberately arrives before detach.
                Assert.IsFalse((bool)Get(room, "localInside"));
                stack.ServerReleaseForRelocation(0);
                Call(room, "ReconcileLocalOccupancy");
                Assert.IsTrue((bool)Get(room, "localInside"));
                Call(follow, "UpdateLocal", local); // Late follow must not put the buyer beside the old carrier.
                Assert.Less(Vector3.Distance(local.transform.position, inside.position), .001f);
                Assert.IsFalse(stack.IsInStack(local));
                Assert.IsTrue(local.GetComponent<PlayerMover>().Simulated);
                for (int i = buyerFloor + 1; i < count; i++) Assert.IsFalse(stack.IsInStack(riders[i]));
            }
            finally { typeof(PiggybackSystem).GetProperty("Current").SetValue(null, previousStack); }
        }

        [TestCase(true), TestCase(false)]
        public void CancelAlwaysReleasesInputWhenPlayerOrReturnPointIsMissing(bool missingPlayer)
        {
            BeginLocalWork();
            if (missingPlayer) SetLocal(null); else Set(room, "outside", null);
            book.CancelUnfulfilled();
            Call(room, "ReconcileLocalOccupancy");
            Assert.IsFalse((bool)Get(room, "localInside"));
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ReservedInjuredRiderCanBoardAndTheHostDetachesTheirLink(int lostParts)
        {
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(local, true);
            local.gameObject.AddComponent<PlayerCondition>().SetLostParts(PlayerCondition.PartsForLostCount(lostParts));
            var support = Player(970);
            support.transform.position = outside.position - Vector3.up * 1.5f;
            var previousStack = PiggybackSystem.Current;
            var stack = Make("Medical entry stack").AddComponent<PiggybackSystem>();
            var follow = stack.gameObject.AddComponent<PiggybackFollow>(); Set(follow, "system", stack);
            var session = Make("Medical authority").AddComponent<NetworkSessionManager>();
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            Set(stack, "session", session);
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);
            try
            {
                ((Dictionary<ulong, ulong>)Get(stack, "belowOf"))[0] = support.OwnerClientId;
                ((Dictionary<ulong, ulong>)Get(stack, "aboveOf"))[support.OwnerClientId] = 0;
                Call(follow, "UpdateLocal", local);
                Assert.AreEqual(ExpertRefusal.None, book.BookMedical(0, true, true, lostParts, 1, 3, () => true, 100));
                Physics.SyncTransforms();
                Assert.IsTrue((bool)Call(room, "CanBoard", local));
                Assert.IsFalse((bool)Call(room, "CanBoard", support));
                var result = ((bool entered, string reason))Call(service, "Enter", 0UL);
                Assert.IsTrue(result.entered, result.reason);
                Call(room, "Bind", service);
                Assert.AreEqual(ExpertRoomPhase.Working, book.Phase);
                Assert.IsFalse(stack.IsInStack(local));
                Assert.Less(Vector3.Distance(local.transform.position, inside.position), .001f);
            }
            finally { typeof(PiggybackSystem).GetProperty("Current").SetValue(null, previousStack); }
        }

        [Test]
        public void DeathInsideReleasesInputWithoutReenablingCorpseController()
        {
            BeginLocalWork();
            var mover = local.GetComponent<PlayerMover>();
            ((NetworkVariable<PlayerState>)Get(local, "state")).Value = PlayerState.Fallen;
            mover.Body.enabled = false; mover.SetControlEnabled(false);
            Call(room, "ReconcileLocalOccupancy");
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            Assert.AreEqual(PlayerState.Fallen, local.State);
            Assert.IsFalse(mover.Body.enabled);
            Assert.IsFalse(mover.ControlEnabled);
        }

        [TestCase(false), TestCase(true)]
        public void LostCabinOwnerReleasesLockWithoutMovingReplacementOrReenteringAsOffline(bool replaceOwner)
        {
            BeginLocalWork();
            All.Remove(local);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(local, false);
            SetLocal(null);
            NetworkPlayer replacement = replaceOwner ? Player(0) : null;
            var untouched = new Vector3(100, 0, 100);
            if (replacement != null) { replacement.transform.position = untouched; SetLocal(replacement); }
            Call(room, "ReconcileLocalOccupancy");
            Call(room, "ReconcileLocalOccupancy");
            Assert.AreEqual(ExpertRoomPhase.Working, book.Phase, "The delayed old reservation has not cleared yet.");
            Assert.IsFalse((bool)Get(room, "localInside"));
            Assert.IsFalse(((UIInputBlocker)Get(room, "workInput")).IsBlocking);
            if (replacement != null) Assert.AreEqual(untouched, replacement.transform.position);
        }

        [Test]
        public void InjuredBuyerCanReachBoardingPointButStillCannotReachThroughWallOrBeyondCommonRange()
        {
            var condition = local.gameObject.AddComponent<PlayerCondition>(); condition.SetLostParts(PlayerCondition.OneArmParts);
            local.transform.position = outside.position + Vector3.left * .5f;
            Physics.SyncTransforms();
            Assert.GreaterOrEqual(room.Reach(local.transform.position, local), 0f,
                "The old door-centre-plus-one-metre target was above the injured reach sphere.");
            float reach = GameSettings.Base.Interaction.Distance * GameSettings.Base.BodyDamage.ReachMultiplier(1);
            local.transform.position = outside.position + Vector3.left * (reach + .1f);
            Physics.SyncTransforms();
            Assert.Less(room.Reach(local.transform.position, local), 0f);
            local.transform.position = outside.position + Vector3.left * .5f;
            var wall = Make("Independent wall").AddComponent<BoxCollider>();
            wall.size = new Vector3(.1f, 2f, 1f);
            wall.transform.position = outside.position + new Vector3(-.25f, .75f, 0);
            Physics.SyncTransforms();
            Assert.Less(room.Reach(local.transform.position, local), 0f, "The shared wall/height/reach policy remains active.");
        }
    }
}
