using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using CurtainCall.Survival;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    [Category("CommonProgressionRegression")]
    public sealed class RehearsalLifecycleTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> players = new();
        CampaignRun previousRun;
        NetworkPlayer previousLocal;
        object previousCannon, previousRehearsal;
        int previousStage;
        CampaignRun run;
        WaitingRoomCannonRehearsal cannon;
        WaitingRoomRehearsal rehearsal;
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static FieldInfo Singleton<T>() => typeof(T).GetField("instance", Static);
        static List<NetworkPlayer> All => (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
        void Shared(string name, object value)
        {
            var shared = Get(cannon, "shared");
            shared.GetType().GetField(name).SetValue(shared, value);
        }
        GameObject Make(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(go);
            return go;
        }
        [SetUp]
        public void SetUp()
        {
            previousRun = CampaignRun.Instance;
            previousLocal = NetworkPlayer.Local;
            previousCannon = Singleton<WaitingRoomCannonRehearsal>().GetValue(null);
            previousRehearsal = Singleton<WaitingRoomRehearsal>().GetValue(null);
            previousStage = (int)typeof(WaitingRoomRehearsal).GetField("savedStage", Static).GetValue(null);
            run = Make("Lifecycle campaign").AddComponent<CampaignRun>();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
            var state = new RunState();
            state.StartSession(true); state.EndIntro(); state.EndRehearsal(); state.EndPerformance();
            Set(run, "host", state); Set(run, "state", state.Snapshot); Set(run, "offlineRun", true);
            cannon = Make("Lifecycle cannon").AddComponent<WaitingRoomCannonRehearsal>();
            Call(cannon, "AwakeNetwork");
            Singleton<WaitingRoomCannonRehearsal>().SetValue(null, cannon);
            rehearsal = Make("Lifecycle tightrope").AddComponent<WaitingRoomRehearsal>();
            Singleton<WaitingRoomRehearsal>().SetValue(null, rehearsal);
            Set(rehearsal, "run", run);
            typeof(WaitingRoomRehearsal).GetField("savedStage", Static).SetValue(null, RehearsalStage.All.Length);
        }
        [TearDown]
        public void TearDown()
        {
            foreach (var p in players)
            {
                All.Remove(p);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(p, false);
            }
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear(); players.Clear();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, previousLocal);
            Singleton<WaitingRoomCannonRehearsal>().SetValue(null, previousCannon);
            Singleton<WaitingRoomRehearsal>().SetValue(null, previousRehearsal);
            typeof(WaitingRoomRehearsal).GetField("savedStage", Static).SetValue(null, previousStage);
        }
        NetworkPlayer Player(ulong id)
        {
            var go = Make("Lifecycle player " + id);
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(player, id);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            All.Add(player); players.Add(player);
            return player;
        }

        [TestCase(false), TestCase(true), Category("WaitingRoomCommonRecovery")]
        public void CannonRevivalWithoutAnObservedFallenFrameKeepsCheckpointAndClearsArrival(bool middle)
        {
            var player = Player(940);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            var mover = player.GetComponent<PlayerMover>();
            Shared("active", true); Shared("round", 7);
            var shared = Get(cannon, "shared");
            List<ulong> List(string name) => (List<ulong>)shared.GetType().GetField(name).GetValue(shared);
            foreach (string name in new[] { "placed", "arrived", "votes", "skipVotes" }) List(name).Add(player.OwnerClientId);
            if (middle) List("middle").Add(player.OwnerClientId);
            Set(cannon, "local", player); Set(cannon, "previousState", PlayerState.Normal); Set(cannon, "placedRound", 7);
            var walls = Make("Old cannon finish walls"); Set(cannon, "walls", walls);
            var course = Make("Checkpoint course").AddComponent<RehearsalCannonCourse>();
            var start = Make("Start floor").AddComponent<BoxCollider>();
            start.transform.position = new Vector3(9100, 9000, 9000); start.size = new Vector3(12, .4f, 8);
            var midway = Make("Middle floor").AddComponent<BoxCollider>();
            midway.transform.position = new Vector3(9200, 9004, 9000); midway.size = start.size;
            course.StartFloor = start; course.MiddleFloor = midway; Set(cannon, "course", course);
            var pilot = player.gameObject.AddComponent<HumanCannonPilot>();
            Set(pilot, "mover", mover); Set(pilot, "player", player);
            Set(pilot, "previousSimulation", true);
            typeof(HumanCannonPilot).GetProperty("IsFlying").SetValue(pilot, true); mover.Simulated = false;
            Call(cannon, "OnPlayerRevived", player);
            Assert.IsTrue((bool)Get(cannon, "localRecoveryPending"), "A zero-delay revival need not expose an intermediate Fallen state.");
            Call(cannon, "ResetLocalRecovery", player);
            Assert.IsFalse(walls.activeSelf);
            foreach (string name in new[] { "placed", "arrived", "votes", "skipVotes" }) Assert.IsEmpty(List(name), name);
            Assert.AreEqual(middle, List("middle").Contains(player.OwnerClientId), "Recovery must keep the last acquired checkpoint.");
            Call(cannon, "PlaceLocalAtCheckpoint", player, mover);
            Assert.That(Vector3.Distance(player.transform.position, course.CheckpointPose(middle ? 1 : 0, 0).position), Is.LessThan(.001f));
            Assert.IsFalse(pilot.IsDriving, "A stale flight must not move the player away from the recovered checkpoint.");
            Assert.IsTrue(mover.Simulated);
            Set(cannon, "course", null); // EditMode fixture owns immediate destruction.
        }

        [Test]
        public void CannonRecoveryUsesLastSafeDeckPoseAndTreatsThePracticeRoomFloorAsFatal()
        {
            var player = Player(947);
            var mover = player.GetComponent<PlayerMover>();
            Shared("active", true); Shared("round", 4);
            Shared("placed", new List<ulong> { player.OwnerClientId });
            var course = Make("Cannon safe pose fixture").AddComponent<RehearsalCannonCourse>();
            var deck = Make("Lower deck").AddComponent<BoxCollider>();
            deck.transform.position = new Vector3(9300, 9004, 9000);
            deck.size = new Vector3(10, .4f, 8);
            var floor = Make("Practice ring floor").AddComponent<BoxCollider>();
            floor.transform.position = deck.transform.position + Vector3.down * 4f;
            floor.size = new Vector3(20, .4f, 18);
            course.LowerFloor = deck;
            Set(cannon, "course", course); Set(cannon, "roomFloor", floor);
            Physics.SyncTransforms();
            var lastSafe = new Pose(deck.bounds.center + Vector3.up * deck.bounds.extents.y + Vector3.right, Quaternion.Euler(0, 35f, 0));
            var roomFeet = floor.bounds.center + Vector3.up * floor.bounds.extents.y;
            var tutorial = GameSettings.WaitingRoom.Tutorial;
            var floorRule = Get(tutorial, "cannonPracticeFloorFallIsFatal");
            try
            {
                Set(tutorial, "cannonPracticeFloorFallIsFatal", true);
                var floorFall = typeof(WaitingRoomCannonRehearsal).GetMethod("ShouldRestartAfterFloorLanding", Static);
                Assert.That(floorFall.Invoke(null, new object[] { player, roomFeet }), Is.True);
                Assert.That(floorFall.Invoke(null, new object[] { player, lastSafe.position }), Is.False);
                Set(cannon, "lastSafePose", (Pose?)lastSafe);
                Call(cannon, "ResetLocalRecovery", player);
                Call(cannon, "PlaceLocalAtCheckpoint", player, mover);
                Assert.That(player.transform.position, Is.EqualTo(lastSafe.position));
                Assert.That(Quaternion.Angle(player.transform.rotation, lastSafe.rotation), Is.LessThan(.01f));
            }
            finally { Set(tutorial, "cannonPracticeFloorFallIsFatal", floorRule); Set(cannon, "course", null); }
        }

        [Test]
        public void PolePassengerReachUsesTheSwingingTeamMateAndStillRejectsWalls()
        {
            var player = Player(948);
            var pole = Make("Passenger reach pole").AddComponent<HumanCannonPole>();
            pole.transform.position = new Vector3(9400, 9000, 9000);
            const float angle = 65f;
            var center = pole.GrabPoint(angle, 1);
            float radius = GameSettings.Poles.PassengerGrabRadius;
            Vector3 bodyOffset = PlayerInteraction.Center(player) - player.transform.position;
            player.transform.position = center - bodyOffset + Vector3.right * (radius - .1f);
            Physics.SyncTransforms();
            Assert.That(Call(pole, "IsSeatInReach", player, angle, 1, -1d), Is.True);
            player.transform.position += Vector3.right * .2f;
            Physics.SyncTransforms();
            Assert.That(Call(pole, "IsSeatInReach", player, angle, 1, -1d), Is.False);
            player.transform.position -= Vector3.right * .2f;
            var wall = Make("Reach wall").AddComponent<BoxCollider>();
            wall.transform.position = center + Vector3.right * radius * .5f;
            wall.size = new Vector3(.2f, 2f, 2f);
            Physics.SyncTransforms();
            Assert.That(Call(pole, "IsSeatInReach", player, angle, 1, -1d), Is.False);
        }

        [Test]
        public void CannonPracticeDoorUsesVisibleBarsWithoutAnInvisibleBarrier()
        {
            var gate = Make(NightDoorLock.RehearsalPassageName).AddComponent<NightDoorLock>();
            var bars = Make("Practice gate bars").transform;
            bars.SetParent(gate.transform, false); bars.localPosition = Vector3.up * 3.4f;
            var collider = bars.gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.down * 1.7f; collider.size = new Vector3(4f, 3.4f, .2f);
            Set(gate, "shutter", bars);
            Shared("active", true);
            var tutorial = GameSettings.WaitingRoom.Tutorial;
            var seal = Get(tutorial, "sealCannonPracticeExit");
            try
            {
                Set(tutorial, "sealCannonPracticeExit", true);
                Call(gate, "OnEnable");
                Assert.That(gate.IsLocked, Is.True);
                var barrier = (BoxCollider)Get(gate, "tutorialBarrier");
                Assert.That(barrier, Is.Null);
                Assert.That(bars.localPosition.y, Is.EqualTo(3.4f));
                Shared("active", false);
                Call(gate, "Update");
                Assert.That(gate.IsLocked, Is.False);
            }
            finally { Set(tutorial, "sealCannonPracticeExit", seal); }
        }

        [Test, Category("WaitingRoomCommonRecovery")]
        public void TutorialCompletionRevivalWaitsForNormalThenReleasesFinishState()
        {
            InitialTutorial(true);
            var player = Player(941);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            Set(rehearsal, "local", player); Set(rehearsal, "placedRound", 3); Set(rehearsal, "lessonReturnedRound", 3);
            foreach (string name in new[] { "arrived", "votes", "placed" }) ((HashSet<ulong>)Get(rehearsal, name)).Add(player.OwnerClientId);
            var walls = Make("Old tutorial finish walls"); Set(rehearsal, "finishWalls", walls);
            var state = (NetworkVariable<PlayerState>)Get(player, "state");
            state.Value = PlayerState.Fallen;
            Call(rehearsal, "OnLocalRevived", player);
            Call(rehearsal, "RecoverLocal");
            Assert.IsTrue((bool)Get(rehearsal, "localRecoveryPending"), "A revival notification arriving before Normal must wait.");
            state.Value = PlayerState.Normal;
            Call(rehearsal, "RecoverLocal");
            Assert.IsFalse((bool)Get(rehearsal, "localRecoveryPending"));
            Assert.IsFalse(walls.activeSelf);
            foreach (string name in new[] { "arrived", "votes", "placed" }) Assert.IsEmpty((HashSet<ulong>)Get(rehearsal, name), name);
            Assert.AreEqual(-1, Get(rehearsal, "placedRound"), "Completed education must still return to the practice-room deck before the first performance.");
            Assert.IsTrue(WaitingRoomRehearsal.UsesInitialTutorialRules);
        }

        [Test, Category("WaitingRoomCommonRecovery")]
        public void TrapezeRecoveryIgnoresDelayedArrivalWhileBackAtStart()
        {
            InitialTutorial(false);
            var player = Player(948);
            var mover = player.GetComponent<PlayerMover>();
            var course = Make("Trapeze recovery course").AddComponent<CurtainCall.Tightrope.TightropeCourse>();
            course.transform.position = new Vector3(9400f, 9000f, 9000f);
            var start = course.transform.position - course.Forward;
            player.transform.position = start;
            Set(rehearsal, "local", player); Set(rehearsal, "course", course);
            Set(rehearsal, "stage", RehearsalStage.All.Length - 1); Set(rehearsal, "round", 5);
            foreach (string name in new[] { "arrived", "placed" }) ((HashSet<ulong>)Get(rehearsal, name)).Add(player.OwnerClientId);
            var walls = Make("Old trapeze finish walls"); Set(rehearsal, "finishWalls", walls);
            Call(rehearsal, "TrackPersonalProgress", mover);
            Assert.That(player.transform.position, Is.EqualTo(start));
            Assert.IsFalse(walls.activeSelf);
            Set(rehearsal, "localRecoveryPending", true);
            Call(rehearsal, "TrackPersonalProgress", mover);
            Assert.IsFalse(walls.activeSelf);
            Set(rehearsal, "course", null);
        }

        [Test, Category("WaitingRoomCommonRecovery")]
        public void NewCannonConnectionDropsQueuedRevivalAndOldCheckpoint()
        {
            Set(run, "host", null);
            Set(cannon, "localRecoveryPending", true);
            var shared = Get(cannon, "shared");
            ((List<ulong>)shared.GetType().GetField("middle").GetValue(shared)).Add(942);
            Call(cannon, "ResetConnection");
            Assert.IsFalse((bool)Get(cannon, "localRecoveryPending"));
            Assert.AreEqual(0, WaitingRoomCannonRehearsal.Checkpoint(942), "A new room cannot retain the previous rehearsal checkpoint.");
        }

        [TestCase("seat")] [TestCase("flight")] [TestCase("transport")]
        public void CannonEndingRestoresMovementOwnedByNativeDevice(string motion)
        {
            var player = Player(901);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            var mover = player.GetComponent<PlayerMover>();
            mover.SetControlEnabled(true); mover.Simulated = true;
            var pilot = player.gameObject.AddComponent<HumanCannonPilot>();
            Set(pilot, "mover", mover); Set(pilot, "player", player);
            Call(pilot, "TakeMovement");
            if (motion == "seat") Set(pilot, "stationIndex", 0);
            else if (motion == "flight") typeof(HumanCannonPilot).GetProperty("IsFlying").SetValue(pilot, true);
            else Set(pilot, "onTransport", true);
            Assert.That(pilot.IsDriving, Is.True);
            Assert.That(mover.Simulated, Is.False);
            Shared("active", true); Shared("ending", true); Shared("round", 3);
            Call(cannon, "CaptureEndingMover");
            // The native course's OnDisable detaches the pilot when Cleanup removes it.
            Call(pilot, "Detach");
            Assert.That(mover.Simulated, Is.False, "기물 정리 후에도 종료 복귀 전 물리가 잠겨 있어야 한다.");
            Call(cannon, "ReleaseEndingLock");
            Assert.That(mover.Simulated && mover.ControlEnabled, Is.True, "탑승/비행 때의 임시 정지가 종료 후 남으면 안 된다.");
        }

        [TestCase(2, false), TestCase(3, false), TestCase(4, false)]
        [TestCase(2, true), TestCase(3, true), TestCase(4, true)]
        public void CannonEndingUnlinksLivingStacksBeforeCapturingAndReturning(int count, bool endingArrivesFirst)
        {
            var previousStack = CurtainCall.Tightrope.PiggybackSystem.Current;
            bool previousSuppressed = CurtainCall.Tightrope.PiggybackSystem.Suppressed;
            var group = Make("Ending stack");
            var stack = group.AddComponent<CurtainCall.Tightrope.PiggybackSystem>();
            var follow = group.AddComponent<CurtainCall.Tightrope.PiggybackFollow>();
            var sessionRoot = Make("Ending host"); sessionRoot.SetActive(false);
            var session = sessionRoot.AddComponent<NetworkSessionManager>();
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            Set(stack, "session", session);
            typeof(CurtainCall.Tightrope.PiggybackSystem).GetProperty("Current").SetValue(null, stack);
            CurtainCall.Tightrope.PiggybackSystem.Suppressed = false;
            try
            {
                Call(follow, "Awake");
                var participants = new List<ulong>();
                NetworkPlayer previous = null, rider = null;
                for (int i = 0; i < count; i++)
                {
                    rider = Player((ulong)(9500 + i)); participants.Add(rider.OwnerClientId);
                    rider.GetComponent<PlayerMover>().FreeMovement = true;
                    if (previous != null)
                    {
                        ((Dictionary<ulong, ulong>)Get(stack, "belowOf"))[rider.OwnerClientId] = previous.OwnerClientId;
                        ((Dictionary<ulong, ulong>)Get(stack, "aboveOf"))[previous.OwnerClientId] = rider.OwnerClientId;
                    }
                    previous = rider;
                }
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, rider);
                typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(rider, true);
                var mover = rider.GetComponent<PlayerMover>(); mover.SetControlEnabled(true); mover.Simulated = true;
                Shared("participants", participants); Shared("active", true); Shared("round", 3);
                Call(cannon, "CaptureReturnPose", rider);
                Call(follow, "UpdateLocal", rider);
                Assert.That(mover.Simulated, Is.False);
                if (endingArrivesFirst)
                {
                    Assert.That(Call(cannon, "CaptureEndingMover"), Is.False, "Wait for the replicated unlink before storing simulation or teleporting.");
                    Assert.That(Get(cannon, "endingMover"), Is.Null);
                }
                Call(cannon, "BeginEnding", false, false);
                foreach (var member in players)
                {
                    Assert.That(stack.GetBelow(member), Is.Null);
                    Assert.That(stack.GetAbove(member), Is.Null);
                }
                Assert.That(Call(cannon, "CaptureEndingMover"), Is.True);
                Call(cannon, "Cleanup");
                Vector3 destination = new Vector3(100, 0, 100);
                mover.SetStartPose(destination, Quaternion.identity);
                Call(cannon, "ReleaseEndingLock");
                Call(follow, "LateUpdate");
                Assert.That(mover.Simulated && mover.ControlEnabled, Is.True);
                Assert.That(Vector3.Distance(rider.transform.position, destination), Is.LessThan(.01f));
            }
            finally
            {
                Call(follow, "ReleaseForSceneChange");
                typeof(CurtainCall.Tightrope.PiggybackSystem).GetProperty("Current").SetValue(null, previousStack);
                CurtainCall.Tightrope.PiggybackSystem.Suppressed = previousSuppressed;
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void CannonEndingUsesTheSharedRecoveryRuleWhenAVoteOverlapsDeath(bool recoveryEnabled)
        {
            var player = Player(904);
            var outsidePractice = Player(905);
            foreach (var fallenPlayer in new[] { player, outsidePractice })
            {
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(fallenPlayer, true);
                ((NetworkVariable<PlayerState>)Get(fallenPlayer, "state")).Value = PlayerState.Fallen;
            }
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            var mover = player.GetComponent<PlayerMover>();
            mover.SetControlEnabled(false);
            Shared("participants", new List<ulong> { player.OwnerClientId });
            Shared("active", true); Shared("ending", true); Shared("round", 3); Shared("endAt", double.PositiveInfinity);
            Set(cannon, "endingRound", 3); Set(cannon, "returnedRound", 3);
            Call(cannon, "CaptureEndingMover");

            var settings = GameSettings.WaitingRoom.Rehearsal;
            bool previous = settings.RecoverAfterDeath;
            try
            {
                Set(settings, "recoverAfterDeath", recoveryEnabled);
                Call(cannon, "TickEnding");
                Call(cannon, "ReleaseEndingLock");
                Assert.That(player.State, Is.EqualTo(recoveryEnabled ? PlayerState.Normal : PlayerState.Fallen));
                Assert.That(mover.ControlEnabled, Is.EqualTo(recoveryEnabled));
                Assert.That(mover.Simulated, Is.True);
                Assert.That(outsidePractice.State, Is.EqualTo(PlayerState.Fallen));
            }
            finally { Set(settings, "recoverAfterDeath", previous); }
        }

        [TestCase(true, true)] [TestCase(false, true)] [TestCase(true, false)]
        public void TightropePracticeEndingOnlyRecoversFallenPlayersWhilePracticeRulesApply(bool recoveryEnabled, bool practicePhase)
        {
            var player = Player(906);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Fallen;
            Set(rehearsal, "active", true); Set(rehearsal, "practice", true);
            var snapshot = run.State;
            snapshot.Phase = practicePhase ? CampaignPhase.RestTime : CampaignPhase.Performance;
            Set(run, "state", snapshot);

            var settings = GameSettings.WaitingRoom.Rehearsal;
            bool previous = settings.RecoverAfterDeath;
            try
            {
                Set(settings, "recoverAfterDeath", recoveryEnabled);
                Assert.That(WaitingRoomRehearsal.EndPractice(), Is.True);
                Assert.That(player.State, Is.EqualTo(recoveryEnabled && practicePhase ? PlayerState.Normal : PlayerState.Fallen));
            }
            finally { Set(settings, "recoverAfterDeath", previous); }
        }

        [TestCase(false)] [TestCase(true)]
        public void PendingTightropeVoteCannotStartOverCannon(bool ending)
        {
            var player = Player(902);
            Shared("active", true); Shared("ending", ending);
            Set(rehearsal, "practiceVote", true);
            ((HashSet<ulong>)Get(rehearsal, "practiceYes")).Add(player.OwnerClientId);
            Call(rehearsal, "HostTickVotes");
            Assert.That(Get(rehearsal, "practice"), Is.False, "한 연습실에서 두 리허설이 동시에 시작되면 안 된다.");
            Assert.That(Get(rehearsal, "practiceVote"), Is.False);
        }

        [TestCase(true, true)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(false, false)]
        public void LeavingPracticePhaseCancelsPendingReturnAndAllLocalLocks(bool active, bool host)
        {
            var player = Player(903);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            var mover = player.GetComponent<PlayerMover>();
            mover.SetControlEnabled(true); mover.Simulated = true;
            Shared("active", active); Shared("ending", true); Shared("round", 2);
            Call(cannon, "CaptureEndingMover");
            var blocks = new List<UIInputBlocker>();
            foreach (string name in new[] { "promptBlock", "placementBlock", "endingBlock" })
            {
                var block = Make(name).AddComponent<UIInputBlocker>();
                block.BlockWhileActive = false; block.SetOpen(true);
                Set(cannon, name, block); blocks.Add(block);
            }
            var fader = Make("Pending return fader").AddComponent<RehearsalScreenFader>();
            Set(fader, "canvasRoot", Make("Pending return cover")); Set(cannon, "fader", fader);
            Set(cannon, "prompt", true);
            var state = run.State; state.Phase = CampaignPhase.Performance; Set(run, "state", state);
            if (!host) Set(run, "host", null);
            Vector3 position = player.transform.position;
            Assert.That(Call(cannon, "UpdatePracticeContext"), Is.False);
            foreach (var block in blocks) Assert.That(block.IsBlocking, Is.False);
            Assert.That(mover.Simulated && mover.ControlEnabled, Is.True);
            Assert.That(fader.IsActive, Is.False);
            Assert.That(Get(cannon, "prompt"), Is.False);
            Assert.That(Get(cannon, "returnedRound"), Is.EqualTo(-1));
            Assert.That(player.transform.position, Is.EqualTo(position), "다음 공연 배치를 옛 복귀 위치로 덮어쓰면 안 된다.");
            if (host)
            {
                var shared = Get(cannon, "shared");
                Assert.That(shared.GetType().GetField("active").GetValue(shared), Is.False);
                Assert.That(shared.GetType().GetField("ending").GetValue(shared), Is.False);
            }
        }

        [Test]
        public void RemoteStartClosesAnOlderSchedulePrompt()
        {
            Call(cannon, "AwakeUi");
            Set(cannon, "prompt", true); Shared("active", true);
            Call(cannon, "TickUi");
            Assert.That(Get(cannon, "prompt"), Is.False);
            Assert.That(((UIInputBlocker)Get(cannon, "promptBlock")).IsBlocking, Is.False);
        }

        [Test]
        public void NewRoomRestartsCannonActionGuidesEvenWhenRoundNumberRepeats()
        {
            Set(run, "host", null);
            Set(cannon, "guideRound", 1);
            ((HashSet<string>)Get(cannon, "actionGuides")).Add("cannon");
            ((Queue<(string title, string body)>)Get(cannon, "pendingGuides")).Enqueue(("Old", "Room"));
            Call(cannon, "ResetConnection");
            Assert.That(Get(cannon, "guideRound"), Is.EqualTo(-1));
            Assert.That((HashSet<string>)Get(cannon, "actionGuides"), Is.Empty);
            Assert.That((Queue<(string title, string body)>)Get(cannon, "pendingGuides"), Is.Empty);
        }

        [TestCase("practiceVote")] [TestCase("practice")]
        public void WaitingRoomStartRequestsAreExclusiveBeforeCourseActivation(string pending)
        {
            var oldDirector = SpaceLinkDirector.Instance;
            var oldScene = SceneManager.GetActiveScene();
            string oldName = oldScene.name;
            try
            {
                // The EditMode runner owns an unsaved scene; restore its name without adding/saving any scene.
                oldScene.name = GameSettings.Base.SpaceTransition.WaitingRoomScene;
                var director = Make("Lifecycle space director").AddComponent<SpaceLinkDirector>();
                typeof(SpaceLinkDirector).GetProperty("Instance").SetValue(null, director);
                var state = run.State; state.CannonUnlocked = true; Set(run, "state", state);
                Assert.That(WaitingRoomCannonRehearsal.CanPractice, Is.True);
                Assert.That(WaitingRoomRehearsal.CanProposePractice, Is.True);
                Shared("active", true);
                Assert.That(WaitingRoomRehearsal.CanProposePractice, Is.False);
                Shared("active", false); Set(rehearsal, pending, true);
                Assert.That(WaitingRoomCannonRehearsal.CanPractice, Is.False);
                Assert.That(WaitingRoomRehearsal.CanProposePractice, Is.False);
            }
            finally
            {
                typeof(SpaceLinkDirector).GetProperty("Instance").SetValue(null, oldDirector);
                oldScene.name = oldName;
            }
        }

        [Test, Category("RehearsalEntryHandoff")]
        public void TrampolineRejectsLateGrabUntilPlacementAndAllowsItAfterwards(
            [Values(2, 3, 4)] int playerCount, [NUnit.Framework.Range(0, 14)] int lostMask)
        {
            using var gate = new CurtainCall.Tightrope.EditorTools.Tests.InputGateTestScope();
            var previousSystem = HumanCannonSystem.Current;
            var previousTrampoline = HumanCannonTrampoline.Current;
            HumanCannonTrampoline trampoline = null;
            try
            {
                var player = Player(0);
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, true);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
                ((NetworkVariable<int>)Get(player, "slot")).Value = 0;
                var parts = (BodyPart)lostMask;
                ((NetworkVariable<BodyPart>)Get(player, "lostParts")).Value = parts;
                var condition = player.gameObject.AddComponent<PlayerCondition>(); condition.SetLostParts(parts);
                var mover = player.GetComponent<PlayerMover>();
                mover.SetStartPose(new Vector3(9000, 9000, 9000), Quaternion.identity);
                var participants = new List<ulong> { 0 };
                for (ulong id = 1; id < (ulong)playerCount; id++)
                {
                    var other = Player(id); other.transform.position = new Vector3(9200 + id * 10, 9000, 9000);
                    participants.Add(id);
                }
                var hostObject = Make("Entry handoff host"); hostObject.SetActive(false);
                var session = hostObject.AddComponent<NetworkSessionManager>();
                Set(session, "_networkManager", hostObject.GetComponent<NetworkManager>());
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
                var system = hostObject.AddComponent<HumanCannonSystem>();
                typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, system);
                Set(system, "session", session);
                Set(system, "seats", System.Array.CreateInstance(typeof(HumanCannonSystem).GetField("seats", Private).FieldType.GetElementType(), 0));
                system.ConfigurePractice(new HumanCannonStation[0], 8990);
                var shared = Get(cannon, "shared");
                var placed = (List<ulong>)shared.GetType().GetField("placed").GetValue(shared);
                Shared("active", true); Shared("round", 4); Shared("participants", participants);
                system.SetPracticePlacementGate(id => (bool)shared.GetType().GetField("active").GetValue(shared)
                    && !(bool)shared.GetType().GetField("ending").GetValue(shared) && placed.Contains(id));
                var root = Make("Entry handoff trampoline"); root.transform.position = player.transform.position + Vector3.forward * 2;
                root.hideFlags = HideFlags.None; player.gameObject.hideFlags = HideFlags.None;
                var handle = Make("Entry handoff handle").transform; handle.SetParent(root.transform, false); handle.localPosition = Vector3.back * 2;
                trampoline = root.AddComponent<HumanCannonTrampoline>(); Set(trampoline, "handle", handle); Set(trampoline, "session", session);
                Call(trampoline, "RefreshCarryBody", player);
                typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, trampoline);
                HumanCannonCarry.Register(trampoline);
                var course = Make("Entry handoff course").AddComponent<RehearsalCannonCourse>();
                var floor = Make("Entry handoff floor").AddComponent<BoxCollider>();
                floor.gameObject.hideFlags = HideFlags.None;
                floor.transform.position = new Vector3(9020, 8999.5f, 9000); floor.size = new Vector3(12, 1, 8);
                course.StartFloor = course.MiddleFloor = floor; course.Cannon = system; course.Trampoline = trampoline;
                Set(cannon, "course", course);
                var ownership = (CannonSeatState)Get(trampoline, "ownership");
                var grab = System.Enum.Parse(typeof(HumanCannonTrampoline).GetNestedType("TrampolineAction", BindingFlags.NonPublic), "Grab");
                void RequestGrab() => Call(trampoline, "HandleRequest", 0UL, grab, ownership.Revision, -1, 0U, 0U,
                    root.transform.position, root.transform.rotation, true);
                Physics.SyncTransforms();
                Assert.IsFalse(trampoline.CanGrab(player));
                RequestGrab(); // Host must reject even if the client sent before receiving the placement block.
                Assert.IsNull(HumanCannonCarry.Held(0));
                Call(cannon, "PlaceLocalAtCheckpoint", player, mover);
                Assert.AreEqual(4, Get(cannon, "placedRound"));
                Assert.IsTrue(RehearsalCannonCourse.Contains(floor, player.transform.position));
                placed.Add(0);
                root.transform.position = player.transform.position + Vector3.forward * 2; root.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                Assert.IsTrue(trampoline.CanGrab(player), "Placement completion must restore normal common grab rules, including limb loss.");
                RequestGrab(); Assert.AreEqual(0UL, trampoline.Carrier);
                trampoline.RequestDrop(); Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                Shared("ending", true);
                RequestGrab(); Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier, "Late grab during return must also be rejected.");
                Assert.AreEqual(parts, player.LostParts);
            }
            finally
            {
                if (trampoline != null) HumanCannonCarry.Unregister(trampoline);
                typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, previousSystem);
                typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, previousTrampoline);
            }
        }

        [TestCase("practiceVote"), TestCase("practice"), TestCase("active"), TestCase("cannon")]
        [Category("RehearsalEntryHandoff")]
        public void RehearsalRejectsLateSleepAndDoesNotAdvanceNight(string pending)
        {
            var previousGate = RunState.NightEndGate;
            try
            {
                RunState.NightEndGate = () => true;
                var night = new RunState(false, true);
                night.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
                Set(run, "host", night); Set(run, "state", night.Snapshot);
                var player = Player(0); typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                if (pending == "cannon") Shared("active", true); else Set(rehearsal, pending, true);
                Assert.IsFalse(CampaignRun.CanSleep(0)); Assert.IsFalse(run.SetReady(true));
                using (var writer = new FastBufferWriter(10, Allocator.Temp))
                {
                    writer.WriteValueSafe(true); writer.WriteValueSafe(run.State.Generation);
                    writer.WriteValueSafe(run.State.Day); writer.WriteValueSafe((byte)CampaignPhase.Night);
                    using var reader = new FastBufferReader(writer, Allocator.Temp);
                    Call(run, "HandleReady", 0UL, reader);
                }
                Assert.IsFalse(night.IsReady(0), "The host must reject a delayed pre-rehearsal sleep packet.");
                night.SetReady(0, true); Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.Night, night.Snapshot.Phase);
                Call(run, "WakeForRehearsal"); Assert.IsFalse(night.IsReady(0));
                if (pending == "cannon") Shared("active", false); else Set(rehearsal, pending, false);
                Assert.IsTrue(CampaignRun.CanSleep(0));
                Assert.IsTrue(run.SetReady(true));
                Assert.AreEqual(CampaignPhase.Dawn, night.Snapshot.Phase, "Ordinary sleeping must work again after ending/cancelling practice.");
            }
            finally { RunState.NightEndGate = previousGate; }
        }

        [Test, Category("RehearsalEntryHandoff")]
        public void LastOrdinarySleeperStillCreatesTheBedViewWhenReadyAdvancesToDawn()
        {
            var previousGate = RunState.NightEndGate;
            try
            {
                RunState.NightEndGate = () => true;
                var night = new RunState(false, true);
                night.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
                Set(run, "host", night); Set(run, "state", night.Snapshot);
                var player = Player(0); typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, true);
                var bed = Make("Last sleeper bed").AddComponent<NightBed>(); bed.gameObject.hideFlags = HideFlags.None;
                Call(bed, "Awake"); bed.Interact();
                Assert.AreEqual(CampaignPhase.Dawn, run.State.Phase);
                Assert.IsTrue(bed.GetComponent<BedRestView>()?.IsResting == true);
                Assert.That(player.RestingBed, Is.GreaterThanOrEqualTo(0));
            }
            finally { RunState.NightEndGate = previousGate; }
        }

        [TestCase(false)] [TestCase(true)]
        public void FirstDayTeamWaitDoesNotLockPlayersInsideRehearsal(bool cannonLesson)
        {
            var oldFlow = Singleton<WaitingRoomFirstDayFlow>().GetValue(null);
            var hudSingleton = Singleton<CurtainCall.UI.SceneIntegration.HudUIManager>();
            var oldHud = hudSingleton.GetValue(null);
            var memoryOnly = typeof(WaitingRoomTutorial).GetField("memoryOnly", Static);
            var memoryEnabled = typeof(WaitingRoomTutorial).GetField("memoryEnabled", Static);
            var oldMemory = memoryOnly.GetValue(null); var oldEnabled = memoryEnabled.GetValue(null);
            try
            {
                // The shared HUD is normally created with DontDestroyOnLoad. Supply the real
                // prefab explicitly for this EditMode fixture, without that PlayMode-only call.
                if (oldHud == null)
                {
                    var hud = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/Roles/HudUIManager"));
                    hud.hideFlags = HideFlags.HideAndDontSave; objects.Add(hud);
                    hudSingleton.SetValue(null, hud.GetComponent<CurtainCall.UI.SceneIntegration.HudUIManager>());
                }
                // This owner has finished/disabled the guide; a teammate still needs it.
                memoryOnly.SetValue(null, true); memoryEnabled.SetValue(null, false);
                var flow = Make("Lifecycle first day flow").AddComponent<WaitingRoomFirstDayFlow>();
                Call(flow, "Awake");
                Set(flow, "tutorialRun", run);
                Set(flow, "tutorialDoneKey", run.State.Generation + "/" + run.State.Day + "/" + run.State.Phase
                    + "/" + run.State.Rest + "/" + run.State.PerformanceIndex);
                var player = Player(904); typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                if (cannonLesson) Shared("active", true);
                else { Set(rehearsal, "active", true); Set(rehearsal, "practice", true); }
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.WaitingForExit));
                Call(flow, "LateUpdate");
                var blockers = (HashSet<object>)typeof(InputGate).GetField("blockers", Static).GetValue(null);
                Assert.That(blockers.Contains(flow), Is.False,
                    "리허설이 일반 안내를 중단한 동안 안내 전원 완료 잠금이 걸리면 진행할 수 없다.");
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.WaitingForExit), "안내 완료나 타이머를 강제로 건너뛰지 않는다.");
                Shared("active", false); Set(rehearsal, "active", false); Set(rehearsal, "practice", false);
                Call(flow, "LateUpdate");
                Assert.That(blockers.Contains(flow), Is.True, "리허설 뒤에는 기존 전원 안내 대기로 돌아간다.");
            }
            finally
            {
                memoryOnly.SetValue(null, oldMemory); memoryEnabled.SetValue(null, oldEnabled);
                Singleton<WaitingRoomFirstDayFlow>().SetValue(null, oldFlow);
                hudSingleton.SetValue(null, oldHud);
            }
        }

        [TestCase(CampaignPhase.RestTime)] [TestCase(CampaignPhase.Night)]
        public void HostFinisherReturnsToNormalAlongWithTeammatesBeforeWaitingRoomGuides(CampaignPhase phase)
        {
            for (ulong id = 0; id < 4; id++)
            {
                var player = Player(id);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
                if (id == 0)
                {
                    typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                    ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Arrived;
                }
            }
            Assert.That(NetworkPlayer.Local.State, Is.EqualTo(PlayerState.Arrived));
            var step = run.State; step.Phase = phase;
            InjuryRevival.ServerApplyStep(step);
            Assert.That(NetworkPlayer.Local.State, Is.EqualTo(PlayerState.Normal), "완주한 호스트도 가이드와 상호작용의 Normal 조건을 만족해야 한다.");
            foreach (var player in players) Assert.That(player.State, Is.EqualTo(PlayerState.Normal));
        }

        [Test]
        public void ReusedCampaignRunReportsPreviouslySeenGuidesInTheNewSession()
        {
            var oldFlow = Singleton<WaitingRoomFirstDayFlow>().GetValue(null);
            var memoryOnly = typeof(WaitingRoomTutorial).GetField("memoryOnly", Static);
            var memoryEnabled = typeof(WaitingRoomTutorial).GetField("memoryEnabled", Static);
            var oldMemory = memoryOnly.GetValue(null); var oldEnabled = memoryEnabled.GetValue(null);
            try
            {
                memoryOnly.SetValue(null, true); memoryEnabled.SetValue(null, false);
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, null);
                var flow = Make("Rehosted first day flow").AddComponent<WaitingRoomFirstDayFlow>(); Call(flow, "Awake");
                Call(flow, "LateUpdate");
                Assert.That(run.State.Timer, Is.Not.EqualTo(RestTimerState.WaitingForExit));
                // Leaving the first guide/run must clear only the sent acknowledgement, not seen guides.
                Call(flow, "LateUpdate");
                var next = new RunState();
                next.StartSession(true); next.EndIntro(); next.EndRehearsal(); next.EndPerformance();
                Set(run, "host", next); Set(run, "state", next.Snapshot);
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.WaitingForExit));
                Call(flow, "LateUpdate");
                Assert.That(run.State.Timer, Is.Not.EqualTo(RestTimerState.WaitingForExit),
                    "같은 CampaignRun으로 새 방을 열어도 호스트 완료 신호를 다시 보내야 한다.");
            }
            finally
            {
                memoryOnly.SetValue(null, oldMemory); memoryEnabled.SetValue(null, oldEnabled);
                Singleton<WaitingRoomFirstDayFlow>().SetValue(null, oldFlow);
            }
        }

        [TestCase(PlayerState.Normal, true, true, true)]
        [TestCase(PlayerState.Normal, false, true, false)]
        [TestCase(PlayerState.Fallen, true, true, false)]
        [TestCase(PlayerState.Retired, true, true, false)]
        [TestCase(PlayerState.Arrived, true, true, false)]
        [TestCase(PlayerState.Normal, true, false, false)]
        [Category("PursuitRecovery"), Category("WaitingRoomCommonRecovery")]
        public void RemovingCapturePoseRestoresControlOnlyForAnEligibleSurvivor(
            PlayerState finalState, bool priorControl, bool spawned, bool expectedControl)
        {
            var player = Player(942);
            var mover = player.GetComponent<PlayerMover>();
            var pose = player.gameObject.AddComponent<Pursuit.PursuitVictimPose>();
            // Exercise teardown without creating the capture HUD or loading visual assets.
            Set(pose, "mover", mover); Set(pose, "priorControl", priorControl);
            mover.SetControlEnabled(priorControl); Call(pose, "LateUpdate");
            Assert.IsFalse(mover.ControlEnabled);
            ((NetworkVariable<PlayerState>)Get(player, "state")).Value = finalState;
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, spawned);

            // These EditMode components never enter Unity's Start lifecycle. Invoke the
            // teardown explicitly, and start enabled so blocked cases prove it ran too.
            mover.SetControlEnabled(true);
            Call(pose, "OnDestroy");

            Assert.AreEqual(expectedControl, mover.ControlEnabled,
                "Dawn capture cleanup must not reactivate a dead, retired or despawned player before revival.");
        }

        static void ReceiveRewardSnapshot(PerformanceRewards rewards, RewardSnapshot snapshot)
        {
            var dataType = typeof(PerformanceRewards).Assembly.GetType("CurtainCall.Reward.RewardStateData", true);
            var data = dataType.GetMethod("From").Invoke(null, new object[] { snapshot });
            // 실제 SendState와 같이 현재 런의 엔딩 통계를 넣는다. 빠진 통계는 JsonUtility에서 generation=0으로 복원된다.
            dataType.GetField("runStatistics").SetValue(data, new EscapeRunStatistics { generation = snapshot.Generation });
            string json = JsonUtility.ToJson(data);
            using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(json), Allocator.Temp);
            writer.WriteValueSafe(json);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Call(rewards, "ReadState", NetworkManager.ServerClientId, reader, false);
        }

        [TestCase(0), TestCase(1), TestCase(2), Category("RewardLobbyBoundaryRegression")]
        public void LobbyRewardResetUsesCampaignBoundaryOnceAcrossSessionMessageOrders(int order)
        {
            // 0: Waiting first; 1: Campaign None first; 2: Waiting is coalesced out.
            var session = Make("Reward client session").AddComponent<NetworkSessionManager>();
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Client);
            typeof(NetworkSessionManager).GetProperty("GameState").SetValue(session,
                order == 2 ? GameSessionState.Playing : GameSessionState.Ended);
            var rewards = Make("Reward lobby lifecycle").AddComponent<PerformanceRewards>();
            Set(rewards, "campaign", run); Set(rewards, "session", session);
            var previous = new PerformanceRewardModel("6dc1b9c8804243e2bcf10d96c3490d0a", 4);
            var next = new PerformanceRewardModel("3c6e0000000000000000000000000000", 2);
            Assert.IsTrue(next.DebugGrant(850, 2));
            var oldSnapshot = previous.Snapshot(); var newSnapshot = next.Snapshot();
            Set(rewards, "state", oldSnapshot); Set(rewards, "ledgerPage", oldSnapshot);
            var handler = (System.Action<CampaignSnapshot>)System.Delegate.CreateDelegate(
                typeof(System.Action<CampaignSnapshot>), rewards,
                typeof(PerformanceRewards).GetMethod("HandleStep", Private));
            run.StepChanged += handler;
            var running = run.State;
            try
            {
                ReceiveRewardSnapshot(rewards, newSnapshot);
                Assert.AreSame(oldSnapshot, rewards.State,
                    "로비 경계를 보기 전 다른 자금 세션은 기존 보호대로 거절한다.");
                if (order == 0) Call(session, "SetGameState", GameSessionState.Waiting);
                Call(run, "Apply", default(CampaignSnapshot));
                Assert.IsNull(rewards.State, "Campaign None 자체가 이전 런의 자금을 초기화한다.");
                Assert.AreEqual(oldSnapshot.SessionId, Get(rewards, "retiredSession"));

                // New reward state may precede both the next campaign and a delayed Waiting.
                ReceiveRewardSnapshot(rewards, newSnapshot);
                Assert.AreEqual(850, rewards.Balance);
                if (order == 1) Call(session, "SetGameState", GameSessionState.Waiting);
                Call(run, "Apply", default(CampaignSnapshot)); // Repeated None is not another step.
                Call(rewards, "SyncCampaign");
                Assert.AreEqual(newSnapshot.SessionId, rewards.State.SessionId);
                Assert.AreEqual(850, rewards.Balance, "늦은 로비 상태나 중복 None이 새 자금 세션을 지우지 않는다.");
                Assert.AreEqual(oldSnapshot.SessionId, Get(rewards, "retiredSession"));
                Call(session, "SetGameState", GameSessionState.Playing);
                Call(run, "Apply", running);
                Assert.AreEqual(850, rewards.Balance);
                ReceiveRewardSnapshot(rewards, oldSnapshot);
                Assert.AreEqual(newSnapshot.SessionId, rewards.State.SessionId, "폐기한 이전 세션은 되살리지 않는다.");
                var unrelated = new PerformanceRewardModel("4c6e0000000000000000000000000000", 9).Snapshot();
                ReceiveRewardSnapshot(rewards, unrelated);
                Assert.AreEqual(newSnapshot.SessionId, rewards.State.SessionId, "로비 전환 외 타세션 보호는 유지한다.");

                // A second genuine run-end must reset again, including same-frame restart.
                Call(run, "Apply", default(CampaignSnapshot));
                Assert.IsNull(rewards.State);
                Assert.AreEqual(newSnapshot.SessionId, Get(rewards, "retiredSession"));
                ReceiveRewardSnapshot(rewards, unrelated);
                Assert.AreEqual(unrelated.SessionId, rewards.State.SessionId);
            }
            finally { run.StepChanged -= handler; }
        }

        ExplorationState ExplorationLifecycleFixture()
        {
            var state = Make("Exploration lifecycle service").AddComponent<ExplorationState>();
            var channelType = typeof(ExplorationState).Assembly.GetType("CurtainCall.WaitingRoom.ShopChannel", true);
            Set(state, "channel", System.Activator.CreateInstance(channelType, new object[] {
                "ExplorationLifecycleTests", (System.Action<ulong, string>)((_, __) => { }),
                (System.Action<string>)(_ => { }), (System.Action<string>)(_ => { }),
                (System.Action)(() => { }), (System.Action)(() => { })
            }));
            return state;
        }

        ExplorationGate ExplorationLifecycleGate(ExplorationGateKind kind)
        {
            var gate = Make("Exploration lifecycle gate").AddComponent<ExplorationGate>();
            Set(gate, "targetId", "exploration-lifecycle-regression");
            Set(gate, "kind", kind);
            gate.transform.position = new Vector3(9000, 100.75f, 9001);
            return gate;
        }

        [TestCase(PlayerState.Fallen), TestCase(PlayerState.Retired), TestCase(PlayerState.Arrived)]
        [Category("ExplorationInteractionLifecycle")]
        public void ExplorationRejectsLateUseFromNonNormalActor(PlayerState blockedState)
        {
            var actor = Player(960);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(actor, true);
            actor.GetComponent<PlayerMover>().SetStartPose(new Vector3(9000, 100, 9000), Quaternion.identity);
            var state = ExplorationLifecycleFixture();
            var gate = ExplorationLifecycleGate(ExplorationGateKind.Open);
            Physics.SyncTransforms();
            Assert.GreaterOrEqual(gate.Reach(actor.transform.position, actor), 0f);
            ((NetworkVariable<PlayerState>)Get(actor, "state")).Value = blockedState;
            var result = ((bool ok, string text, string reason))Call(state, "UseCore", actor.OwnerClientId, gate.TargetId, null, gate);
            Assert.IsFalse(result.ok);
            Assert.AreEqual("NOT_ALIVE", result.reason);
            Assert.AreEqual(0, state.Hits(gate.TargetId));
            Assert.IsFalse(state.IsOpen(gate.TargetId), "도착이 늦은 요청으로 탐색 진행을 바꾸지 않는다.");
        }

        [Test, Category("ExplorationInteractionLifecycle")]
        public void ExplorationCooperationDoesNotCountPartnerWhoDiedAfterPressing()
        {
            var first = Player(961); var second = Player(962);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(first, true);
            first.GetComponent<PlayerMover>().SetStartPose(new Vector3(8999.5f, 100, 9000), Quaternion.identity);
            second.GetComponent<PlayerMover>().SetStartPose(new Vector3(9000.5f, 100, 9000), Quaternion.identity);
            var state = ExplorationLifecycleFixture();
            var gate = ExplorationLifecycleGate(ExplorationGateKind.Cooperative);
            Physics.SyncTransforms();
            var pending = ((bool ok, string text, string reason))Call(state, "UseCore", first.OwnerClientId, gate.TargetId, null, gate);
            Assert.AreEqual("PARTNER_REQUIRED", pending.reason, "첫 단원의 정상 요청이 협동 대기에 들어간다.");
            ((NetworkVariable<PlayerState>)Get(first, "state")).Value = PlayerState.Fallen;
            var result = ((bool ok, string text, string reason))Call(state, "UseCore", second.OwnerClientId, gate.TargetId, null, gate);
            Assert.IsFalse(result.ok);
            Assert.AreEqual("PARTNER_REQUIRED", result.reason);
            Assert.IsFalse(state.IsOpen(gate.TargetId), "동시 입력 시간 안이어도 사망한 단원은 협동 인원에서 제외한다.");
        }

        void InvalidateExplorationPanelOwner(NetworkPlayer actor, bool replaceOwner)
        {
            if (replaceOwner)
            {
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, false);
                var replacement = Player(actor.OwnerClientId);
                replacement.GetComponent<PlayerMover>().SetStartPose(actor.transform.position, Quaternion.identity);
                replacement.gameObject.AddComponent<PlayerInputReader>();
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, replacement);
            }
            else
            {
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(actor, true);
                ((NetworkVariable<PlayerState>)Get(actor, "state")).Value = PlayerState.Fallen;
            }
        }

        [TestCase(false), TestCase(true), Category("ExplorationInteractionLifecycle")]
        public void ClueOverviewReleasesLockWhenOpeningActorDiesOrIsReplaced(bool replaceOwner)
        {
            var actor = Player(0); actor.gameObject.AddComponent<PlayerInputReader>();
            actor.GetComponent<PlayerMover>().SetStartPose(new Vector3(9000, 100, 9000), Quaternion.identity);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, actor);
            var clue = Make("Exploration lifecycle clue").AddComponent<ClueInspectable>(); Call(clue, "Awake");
            clue.transform.position = actor.transform.position;
            var previousClue = ClueInspectable.Current;
            var blocker = (UIInputBlocker)Get(clue, "blocker");
            try
            {
                // Open-session fixture omits only the camera copy and panel artwork.
                typeof(ClueInspectable).GetProperty("Current").SetValue(null, clue);
                Set(clue, "openedBy", actor); Set(clue, "openedByNetworkPlayer", true);
                blocker.SetOpen(true);
                InvalidateExplorationPanelOwner(actor, replaceOwner);
                Call(clue, "Update");
                Assert.IsFalse(clue.IsOpen);
                Assert.IsFalse(blocker.IsBlocking, "사망/교체 뒤 남아 있던 단서 입력 잠금을 해제한다.");
                Assert.IsNull(Get(clue, "openedBy"));
            }
            finally
            {
                clue.SetOpen(false);
                typeof(ClueInspectable).GetProperty("Current").SetValue(null, previousClue);
            }
        }

        [TestCase(false), TestCase(true), Category("ExplorationInteractionLifecycle")]
        public void ExplorationPanelReleasesLockWhenOpeningActorDiesOrIsReplaced(bool replaceOwner)
        {
            var actor = Player(0); actor.gameObject.AddComponent<PlayerInputReader>();
            actor.GetComponent<PlayerMover>().SetStartPose(new Vector3(9000, 100, 9000), Quaternion.identity);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, actor);
            var state = ExplorationLifecycleFixture();
            var previousState = ExplorationState.Instance;
            var gate = ExplorationLifecycleGate(ExplorationGateKind.Keypad); Call(gate, "Awake");
            var blocker = (UIInputBlocker)Get(gate, "inputBlocker");
            try
            {
                typeof(ExplorationState).GetProperty("Instance").SetValue(null, state);
                Set(gate, "panel", true); Set(gate, "panelOpenedBy", actor); Set(gate, "panelOpenedByNetworkPlayer", true);
                blocker.SetOpen(true);
                Physics.SyncTransforms();
                Assert.IsTrue(state.CanReachLocal(gate));
                InvalidateExplorationPanelOwner(actor, replaceOwner);
                Call(gate, "Update");
                Assert.IsFalse((bool)Get(gate, "panel"));
                Assert.IsFalse(blocker.IsBlocking, "사망/교체 뒤 남아 있던 암호 패널 입력 잠금을 해제한다.");
                Assert.IsNull(Get(gate, "panelOpenedBy"));
            }
            finally
            {
                Call(gate, "ClosePanel");
                typeof(ExplorationState).GetProperty("Instance").SetValue(null, previousState);
            }
        }

        [TestCase(PlayerState.Fallen), TestCase(PlayerState.Retired), TestCase(PlayerState.Arrived), Category("WaitingRoomCommonRecovery")]
        public void NonNormalPlayersCannotSelectShopTargetsOrderOrPrepareForSleep(PlayerState blockedState)
        {
            var player = Player(0);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            Assert.IsTrue(PlayerInteraction.CanInteract(player));
            ((NetworkVariable<PlayerState>)Get(player, "state")).Value = blockedState;
            Assert.IsFalse(PlayerInteraction.CanInteract(player));
            Assert.IsFalse(PlayerInteraction.CanInteract(player.OwnerClientId));
            Assert.IsNull(CommonInteractionSelection.Selected(player));
            var shop = Make("Blocked shop request").AddComponent<WaitingRoomShop>();
            var purchase = ((bool ok, string text))Call(shop, "Buy", "spring-shoes", player.OwnerClientId);
            Assert.IsFalse(purchase.ok);
            StringAssert.Contains("살아", purchase.text);
            var night = new RunState(false, true);
            night.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
            Set(run, "host", night); Set(run, "state", night.Snapshot);
            Assert.IsFalse(run.SetReady(true), "로컬/호스트 준비 요청도 사망 상태를 거절한다.");
            var bed = Make("Blocked bed").AddComponent<NightBed>();
            Assert.IsFalse(bed.IsAvailable);
            bed.Interact();
            Assert.IsFalse(run.IsLocalReady);
        }

        [Test, Category("WaitingRoomCommonRecovery")]
        public void NightDoesNotTreatAnUnspawnedConnectedAvatarAsDead()
        {
            var oldGate = RunState.NightEndGate;
            try
            {
                RunState.NightEndGate = () => true;
                var night = new RunState(false, true);
                night.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
                Set(run, "host", night); Set(run, "state", night.Snapshot);
                Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.Night, night.Snapshot.Phase, "접속자의 캐릭터 생성은 기다린다.");
                var player = Player(0);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
                ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Fallen;
                Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.Dawn, night.Snapshot.Phase, "확정된 전원 사망이면 기존 밤 종료로 진행한다.");
            }
            finally { RunState.NightEndGate = oldGate; }
        }

        [TestCase(PlayerState.Normal), TestCase(PlayerState.Fallen), TestCase(PlayerState.Retired)]
        [Category("NightPursuitTransitionRegression")]
        public void NightWaitsForLastCaptureThenRequiresFreshDawnAcknowledgment(PlayerState duringCapture)
        {
            var current = typeof(Pursuit.PursuitDirector).GetProperty("Current");
            var previous = current.GetValue(null);
            var oldGate = RunState.NightEndGate;
            try
            {
                RunState.NightEndGate = () => false;
                var night = new RunState(false, true, true);
                night.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
                Assert.IsTrue(night.DebugBeginPursuit());
                Set(run, "host", night); Set(run, "state", night.Snapshot);
                var player = Player(0);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
                var state = (NetworkVariable<PlayerState>)Get(player, "state");
                state.Value = duringCapture;
                var go = Make("Pending night capture"); go.SetActive(false);
                var pursuit = go.AddComponent<Pursuit.PursuitDirector>(); current.SetValue(null, pursuit);
                var victimType = typeof(Pursuit.PursuitDirector).GetNestedType("Victim", BindingFlags.NonPublic);
                var victim = System.Activator.CreateInstance(victimType, true);
                victimType.GetField("Object").SetValue(victim, player.gameObject);
                ((System.Collections.IDictionary)Get(pursuit, "victims")).Add(0UL, victim);

                Assert.IsTrue(Pursuit.PursuitDirector.IsCapturePending(player));
                Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.Night, night.Snapshot.Phase, "A captured player is not a completed death while the capture sequence is pending.");
                Assert.AreEqual(RestTimerState.Pursuit, night.Snapshot.Timer);

                state.Value = PlayerState.Retired;
                victimType.GetField("Retired").SetValue(victim, true);
                Assert.IsTrue(Pursuit.PursuitDirector.IsCaptured(player), "The completed victim still cannot interact until morning revival.");
                Assert.IsFalse(Pursuit.PursuitDirector.IsCapturePending(player));
                Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.Dawn, night.Snapshot.Phase);
                Assert.AreEqual(1, night.Snapshot.Day);
                Assert.AreEqual(0, night.Snapshot.ReadyCount);
                Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.Dawn, night.Snapshot.Phase, "Retirement is not the owner's fade-complete acknowledgment.");
                night.SetReady(0, true);
                Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.RestTime, night.Snapshot.Phase);
                Assert.AreEqual(RestKind.Morning, night.Snapshot.Rest);
                Assert.AreEqual(2, night.Snapshot.Day);
            }
            finally { current.SetValue(null, previous); RunState.NightEndGate = oldGate; }
        }

        [Test, Category("NightPursuitTransitionRegression")]
        public void DisconnectedPendingCaptureDoesNotBlockTheRemainingDeadPlayers()
        {
            var current = typeof(Pursuit.PursuitDirector).GetProperty("Current");
            var previous = current.GetValue(null);
            try
            {
                var night = new RunState(false, true);
                night.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
                Set(run, "host", night); Set(run, "state", night.Snapshot);
                var local = Player(0);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(local, true);
                ((NetworkVariable<PlayerState>)Get(local, "state")).Value = PlayerState.Fallen;
                var departed = Player(1); // Offline connected roster contains only actor 0.
                var go = Make("Disconnected pending night capture"); go.SetActive(false);
                var pursuit = go.AddComponent<Pursuit.PursuitDirector>(); current.SetValue(null, pursuit);
                var victimType = typeof(Pursuit.PursuitDirector).GetNestedType("Victim", BindingFlags.NonPublic);
                var victim = System.Activator.CreateInstance(victimType, true);
                victimType.GetField("Object").SetValue(victim, departed.gameObject);
                ((System.Collections.IDictionary)Get(pursuit, "victims")).Add(1UL, victim);
                Assert.IsTrue(Pursuit.PursuitDirector.IsCapturePending(departed));
                Call(run, "RefreshNight", night);
                Assert.AreEqual(CampaignPhase.Dawn, night.Snapshot.Phase);
            }
            finally { current.SetValue(null, previous); }
        }

        [Test, Category("WaitingRoomCommonRecovery")]
        public void MedicalReservationCancelsOnDeathAndRejectsDeadEntry()
        {
            var player = Player(930);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Fallen;
            var service = Make("Dead medical reservation").AddComponent<ExpertService>();
            var book = new ExpertServiceBook(3f, 2f);
            book.BookMedical(player.OwnerClientId, true, true, 2, 2, 3, () => true, 150);
            Set(service, "book", book);
            Assert.IsTrue((bool)Call(service, "CancelUnavailableBuyer"));
            Assert.IsFalse((bool)Call(service, "CancelUnavailableBuyer"));
            Assert.AreEqual(150, book.PendingRefund);
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
            var enter = ((bool ok, string text))Call(service, "Enter", player.OwnerClientId);
            Assert.IsFalse(enter.ok);
            StringAssert.Contains("살아", enter.text);
        }

        [Test, Category("WaitingRoomCommonRecovery")]
        public void OnlyTheFirstGenerationUsesInitialEducationRestrictions()
        {
            var state = run.State;
            state.Generation = 1; state.Day = 1; state.PerformanceIndex = 0; state.Phase = CampaignPhase.Performance;
            Set(run, "state", state);
            Assert.IsTrue(WaitingRoomRehearsal.IsPreTutorialRestricted);
            state.Generation = 2; Set(run, "state", state);
            Assert.IsFalse(WaitingRoomRehearsal.IsPreTutorialRestricted);
            state.Phase = CampaignPhase.Rehearsal; Set(run, "state", state);
            Assert.IsFalse(WaitingRoomRehearsal.IsPreTutorialRestricted);
        }

        void InitialTutorial(bool completed)
        {
            var state = new RunState();
            state.StartSession(true); state.EndIntro();
            Set(run, "host", state); Set(run, "state", state.Snapshot);
            Set(rehearsal, "active", true); Set(rehearsal, "practice", false); Set(rehearsal, "completed", completed);
            Set(rehearsal, "practiceAreaFound", true);
            Set(rehearsal, "practiceArea", new Bounds(Vector3.zero, Vector3.one * 10f));
        }

        [TestCase(false), TestCase(true), Category("TutorialCommonRules")]
        public void TutorialPracticeGateTracksOccupantsBeforeAndAfterCompletion(bool completed)
        {
            var rules = GameSettings.WaitingRoom.Tutorial;
            string saved = JsonUtility.ToJson(rules);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"openPracticeDoorWhileOccupied\":true}", rules);
                InitialTutorial(completed);
                var player = Player(910);
                Set(rehearsal, "practiceSealed", true);
                Call(rehearsal, "HostTickPracticeSeal");
                Assert.IsFalse(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName), "실내 단원이 있으면 교육 중과 완료 후 모두 문이 열린다.");
                player.transform.position = Vector3.right * 20f;
                Call(rehearsal, "HostTickPracticeSeal");
                Assert.IsTrue(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName), "모두 나가면 문이 닫힌다.");
                int sealedVersion = (int)Get(rehearsal, "version");
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
                ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Fallen;
                player.transform.position = Vector3.zero;
                Call(rehearsal, "HostTickPracticeSeal");
                Assert.IsFalse(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName), "죽은 단원도 방 안에 있으면 복구 전부터 문을 다시 연다.");
                Assert.That((int)Get(rehearsal, "version"), Is.GreaterThan(sealedVersion), "다시 열린 문도 새 상태 버전으로 공유한다.");
            }
            finally { JsonUtility.FromJsonOverwrite(saved, rules); }
        }

        [Test, Category("TutorialCommonRules")]
        public void TutorialPracticeGateWaitsForThreePlayersToClearTheRealExitPlane()
        {
            InitialTutorial(true);
            Set(rehearsal, "ringCenter", Vector3.forward * 10);
            Set(rehearsal, "practiceArea", new Bounds(Vector3.forward * 6, new Vector3(10, 10, 8)));
            var gate = Make("Practice exit plane");
            Set(rehearsal, "practiceExit", gate.transform);
            var passage = Make("Practice passage").AddComponent<BoxCollider>();
            passage.center = Vector3.forward * 2;
            passage.size = new Vector3(6, .2f, 4);
            Set(rehearsal, "practicePassage", passage);
            var roof = Make("Practice passage roof").AddComponent<BoxCollider>();
            roof.center = new Vector3(0, 3.15f, 2);
            roof.size = new Vector3(6, .3f, 4);
            Set(rehearsal, "practicePassageRoof", roof);
            Physics.SyncTransforms();
            var first = Player(920);
            var second = Player(921);
            var last = Player(922);
            first.transform.position = Vector3.back * 2;
            second.transform.position = Vector3.back * 3;
            last.transform.position = Vector3.forward;
            Call(rehearsal, "HostTickPracticeSeal");
            Assert.IsFalse(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName));
            last.transform.position = Vector3.back * .1f;
            Call(rehearsal, "HostTickPracticeSeal");
            Assert.IsFalse(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName));
            last.transform.position = Vector3.back * 2;
            Call(rehearsal, "HostTickPracticeSeal");
            Assert.IsTrue(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName));

            last.transform.position = new Vector3(0, 2.5f, 2f);
            Call(rehearsal, "HostTickPracticeSeal");
            Assert.IsFalse(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName), "A rider above the floor still occupies the passage.");
            last.transform.position = new Vector3(0, 3.31f, 2f);
            Call(rehearsal, "HostTickPracticeSeal");
            Assert.IsTrue(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName), "The roof remains outside even where the room bounds overlap it.");
            last.transform.position = new Vector3(0, -1f, 2f);
            Call(rehearsal, "HostTickPracticeSeal");
            Assert.IsTrue(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName), "The space below the passage floor is also outside.");
        }

        [Test, Category("TutorialCommonRules")]
        public void TutorialPracticeGateReadsSharedPolicyInsteadOfForcingOccupancyOverride()
        {
            var rules = GameSettings.WaitingRoom.Tutorial;
            string saved = JsonUtility.ToJson(rules);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"openPracticeDoorWhileOccupied\":false}", rules);
                InitialTutorial(false);
                Player(911);
                Call(rehearsal, "HostTickPracticeSeal");
                Assert.IsTrue(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName), "공통 정책을 끄면 교육 미완료의 기존 잠금을 유지한다.");
                JsonUtility.FromJsonOverwrite("{\"openPracticeDoorWhileOccupied\":true}", rules);
                Call(rehearsal, "HostTickPracticeSeal");
                Assert.IsFalse(WaitingRoomRehearsal.IsGateLocked(NightDoorLock.RehearsalPassageName));
            }
            finally { JsonUtility.FromJsonOverwrite(saved, rules); }
        }

        [Test, Category("TutorialCommonRules")]
        public void TutorialEntranceWaitsForRecoveryThenStartsPerformance()
        {
            var rules = GameSettings.WaitingRoom.Tutorial;
            string saved = JsonUtility.ToJson(rules);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"waitForRecoveryBeforePerformance\":true}", rules);
                InitialTutorial(true);
                var alive = Player(912);
                var recovering = Player(913);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(recovering, true);
                ((NetworkVariable<PlayerState>)Get(recovering, "state")).Value = PlayerState.Fallen;
                var gather = Make("Tutorial stage entrance").AddComponent<StageEntranceGather>(); Call(gather, "Awake");
                alive.transform.position = Vector3.zero;
                recovering.transform.position = Vector3.right * 20f;
                Physics.SyncTransforms();
                Call(gather, "Update");
                Assert.AreEqual(CampaignPhase.Rehearsal, run.State.Phase, "살아 있는 동료가 모두 집결해도 튜토리얼 복구를 기다린다.");
                ((NetworkVariable<PlayerState>)Get(recovering, "state")).Value = PlayerState.Normal;
                Call(gather, "Update");
                Assert.AreEqual(CampaignPhase.Rehearsal, run.State.Phase, "복구한 동료도 직접 집결해야 한다.");
                recovering.transform.position = Vector3.zero;
                Physics.SyncTransforms();
                Call(gather, "Update");
                Assert.AreEqual(CampaignPhase.Performance, run.State.Phase);
            }
            finally { JsonUtility.FromJsonOverwrite(saved, rules); }
        }

        [Test, Category("TutorialCommonRules")]
        public void TutorialRecoveryWaitStopsWhenPolicyIsOffOrInitialTutorialEnds()
        {
            var rules = GameSettings.WaitingRoom.Tutorial;
            string saved = JsonUtility.ToJson(rules);
            try
            {
                InitialTutorial(true);
                var recovering = Player(914);
                typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(recovering, true);
                ((NetworkVariable<PlayerState>)Get(recovering, "state")).Value = PlayerState.Fallen;
                var gather = Make("Tutorial recovery boundary").AddComponent<StageEntranceGather>();
                JsonUtility.FromJsonOverwrite("{\"waitForRecoveryBeforePerformance\":false}", rules);
                Assert.IsFalse((bool)Call(gather, "MustWaitForTutorialRecovery"));
                JsonUtility.FromJsonOverwrite("{\"waitForRecoveryBeforePerformance\":true}", rules);
                Assert.IsTrue((bool)Call(gather, "MustWaitForTutorialRecovery"));
                Set(rehearsal, "practice", true);
                Assert.IsFalse((bool)Call(gather, "MustWaitForTutorialRecovery"), "일반 재연습은 최초 교육 복구 대기를 쓰지 않는다.");
                Set(rehearsal, "practice", false);
                var state = (RunState)Get(run, "host"); state.EndRehearsal(); state.EndPerformance();
                Set(run, "state", state.Snapshot);
                Assert.IsFalse((bool)Call(gather, "MustWaitForTutorialRecovery"), "첫 공연 뒤 일반 집결은 기존 생존자 기준을 유지한다.");
            }
            finally { JsonUtility.FromJsonOverwrite(saved, rules); }
        }
    }
}
