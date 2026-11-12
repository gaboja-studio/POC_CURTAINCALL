using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    [Category("CommonProgressionRegression")]
    public sealed class RehearsalArrivalTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> players = new();
        WaitingRoomRehearsal rehearsal;
        TightropeCourse course, previousCourse;
        int savedStage;

        static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
        static List<NetworkPlayer> All => (List<NetworkPlayer>)typeof(NetworkPlayer)
            .GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        static FieldInfo SavedStage => typeof(WaitingRoomRehearsal).GetField("savedStage", BindingFlags.Static | BindingFlags.NonPublic);

        GameObject Make(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(go);
            return go;
        }

        [SetUp]
        public void SetUp()
        {
            previousCourse = TightropeCourse.Current;
            savedStage = (int)SavedStage.GetValue(null);
            rehearsal = Make("Rehearsal arrival fixture").AddComponent<WaitingRoomRehearsal>();
            course = Make("Rehearsal course fixture").AddComponent<TightropeCourse>();
            course.transform.position = new Vector3(9000f, 9000f, 9000f);
            var shape = GameSettings.Tightrope.Course.Clone();
            JsonUtility.FromJsonOverwrite(RehearsalStage.All[0].CourseJson, shape);
            // Use an isolated shape in EditMode; do not mutate the project's settings asset.
            Set(course, "useSceneGeometry", true);
            Set(course, "sceneShape", shape);
            Set(rehearsal, "course", course);
            Set(rehearsal, "courseRoot", course.gameObject);
            Set(rehearsal, "active", true);
            Set(rehearsal, "stage", 0);
            Set(rehearsal, "round", 5);
            Set(rehearsal, "builtRound", 5);
        }

        [TearDown]
        public void TearDown()
        {
            if (course != null)
            {
                var materials = (Dictionary<Color, Material>)Get(course, "courseMaterials");
                foreach (var material in materials.Values) if (material != null) Object.DestroyImmediate(material);
                materials.Clear(); // EditMode fixture owns immediate cleanup of generated runtime art.
            }
            foreach (var player in players)
            {
                All.Remove(player);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            }
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            players.Clear(); objects.Clear();
            SavedStage.SetValue(null, savedStage);
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousCourse);
        }

        NetworkPlayer Participant(ulong id, bool placed = true)
        {
            var go = Make("Participant " + id);
            var mover = go.AddComponent<PlayerMover>();
            Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>();
            Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(player, id);
            ((NetworkVariable<int>)Get(player, "slot")).Value = players.Count;
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            players.Add(player); All.Add(player);
            if (placed) ((HashSet<ulong>)Get(rehearsal, "placed")).Add(id);
            player.transform.position = course.GetFinishPose(0).position;
            return player;
        }

        [Test]
        public void FirstStage_AllFourArrivalsAdvanceWithoutTransientJumpHistory()
        {
            for (ulong id = 701; id <= 704; id++) Participant(id);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(1), "전원 도착은 순간 점프 기록과 무관하게 다음 소구간으로 진행해야 한다.");
            Assert.That(Get(rehearsal, "round"), Is.EqualTo(6));
            Assert.That((HashSet<ulong>)Get(rehearsal, "placed"), Is.Empty, "다음 구간은 새 배치 승인을 기다린다.");
        }

        [Test]
        public void CannonRehearsalDoesNotOverrideExplorationZoneTimer()
        {
            var previousRun = CampaignRun.Instance;
            var cannonField = typeof(WaitingRoomCannonRehearsal).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousCannon = cannonField.GetValue(null);
            var gate = RunState.SecondPerformanceGate;
            try
            {
                var run = Make("Timer run").AddComponent<CampaignRun>();
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
                var state = new RunState();
                state.StartSession(true); state.EndIntro(); state.EndRehearsal(); state.EndPerformance();
                state.SetTutorialDone(0); state.RefreshGuideRest(new ulong[] { 0 });
                RunState.SecondPerformanceGate = () => false;
                Set(run, "host", state); Set(run, "state", state.Snapshot); Set(run, "offlineRun", true);
                var cannon = Make("Active cannon rehearsal").AddComponent<WaitingRoomCannonRehearsal>();
                cannonField.SetValue(null, cannon);
                var shared = Get(cannon, "shared");
                shared.GetType().GetField("active").SetValue(shared, true);
                var zones = Make("Timer zones").AddComponent<ExplorationTimerZones>();
                var running = Make("Running floor").AddComponent<BoxCollider>();
                running.transform.position = course.transform.position;
                running.size = Vector3.one * 6f;
                ((List<BoxCollider>)Get(zones, "running")).Add(running);
                var mover = Make("Offline explorer").AddComponent<PlayerMover>();
                mover.transform.position = course.transform.position;
                ((List<PlayerMover>)Get(zones, "offlinePlayers")).Add(mover);
                Physics.SyncTransforms();
                state.SetRestTimerPaused(true);
                Set(run, "state", state.Snapshot);
                Call(zones, "Update");
                Assert.That(zones.ExploringCount, Is.EqualTo(1));
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.Running));
                Assert.That(state.Tick(1f), Is.True);
                mover.transform.position += Vector3.right * 20f;
                Call(zones, "Update");
                Assert.That(run.State.Timer, Is.EqualTo(RestTimerState.Stopped));
            }
            finally
            {
                RunState.SecondPerformanceGate = gate;
                cannonField.SetValue(null, previousCannon);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void CannonEndingRestoresEachOwnersOriginalPose(int slot)
        {
            var previousLocal = NetworkPlayer.Local;
            try
            {
                var player = Participant((ulong)(701 + slot));
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
                var mover = player.GetComponent<PlayerMover>();
                mover.SetControlEnabled(true); mover.Simulated = true;
                var original = new Pose(course.transform.position + Vector3.right * (10f + slot * 3f), Quaternion.Euler(0, 30f + slot * 45f, 0));
                player.transform.SetPositionAndRotation(original.position, original.rotation);
                var cannon = Make("Return fixture").AddComponent<WaitingRoomCannonRehearsal>();
                var shared = Get(cannon, "shared");
                shared.GetType().GetField("round").SetValue(shared, 5);
                Call(cannon, "CaptureReturnPose", player);
                player.transform.position = course.GetFinishPose(0).position;
                Call(cannon, "CaptureReturnPose", player);
                shared.GetType().GetField("active").SetValue(shared, true);
                shared.GetType().GetField("ending").SetValue(shared, true);
                Set(cannon, "endingRound", 5); Set(cannon, "returnAt", -1d);
                Set(cannon, "room", Make("Return room").transform);
                Call(cannon, "TickEnding");
                Assert.That(Vector3.Distance(player.transform.position, original.position), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(player.transform.rotation, original.rotation), Is.LessThan(.01f));
                Assert.That(mover.Simulated, Is.False);
                Call(cannon, "ReleaseEndingLock");
                Assert.That(mover.Simulated && mover.ControlEnabled, Is.True);
                shared.GetType().GetField("round").SetValue(shared, 6);
                player.transform.position += Vector3.forward * 2f;
                Call(cannon, "CaptureReturnPose", player);
                Assert.That(((Pose)Get(cannon, "returnPose")).position, Is.EqualTo(player.transform.position));
            }
            finally { typeof(NetworkPlayer).GetProperty("Local").SetValue(null, previousLocal); }
        }

        [Test]
        public void FirstStage_OneArrivalUnlocksMajorityAndCountsOnlyCurrentParticipants()
        {
            Participant(701);
            for (ulong id = 702; id <= 704; id++) Participant(id).transform.position = course.GetLanePosition(0, 8f);
            Call(rehearsal, "HostJudgeStage");
            Assert.That((HashSet<ulong>)Get(rehearsal, "arrived"), Is.EquivalentTo(new[] { 701UL }));
            Call(rehearsal, "SetVote", 701UL, true);
            Call(rehearsal, "SetVote", 702UL, true);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.Zero, "4명 중 2명은 과반이 아니다.");
            Call(rehearsal, "SetVote", 703UL, true);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(1));
        }

        [Test]
        public void DisconnectedClientDoesNotKeepThePreviousHostsVersion()
        {
            Set(rehearsal, "active", false);
            Set(rehearsal, "registered", true);
            Set(rehearsal, "registeredFor", SessionConnectionState.Client);
            Set(rehearsal, "version", 900);
            Set(rehearsal, "stage", 4);
            ((HashSet<ulong>)Get(rehearsal, "arrived")).Add(77);
            Call(rehearsal, "Connect");
            Assert.That(Get(rehearsal, "version"), Is.Zero, "새 호스트의 낮은 버전 상태를 이전 방의 버전으로 거절하면 안 된다.");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(-1));
            Assert.That((HashSet<ulong>)Get(rehearsal, "arrived"), Is.Empty);
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(1); writer.WriteValueSafe(0); writer.WriteValueSafe(1);
            writer.WriteValueSafe(false); writer.WriteValueSafe(false); writer.WriteValueSafe(false); writer.WriteValueSafe(-1d);
            for (int i = 0; i < 6; i++) writer.WriteValueSafe(0); // participants, placed, arrived, votes, lesson, voters
            writer.WriteValueSafe(false); writer.WriteValueSafe(false); writer.WriteValueSafe(0); writer.WriteValueSafe(0);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Call(rehearsal, "HandleState", NetworkManager.ServerClientId, reader);
            Assert.That(Get(rehearsal, "stage"), Is.Zero, "새 호스트의 첫 구간 상태 수신");
            Assert.That(Get(rehearsal, "round"), Is.EqualTo(1));
        }

        [Test]
        public void UnacknowledgedSpawnDisplacedOffStartIsPlacedAgain()
        {
            var player = Participant(701, false);
            Set(rehearsal, "local", player);
            Set(rehearsal, "placedRound", 5);
            var mover = player.GetComponent<PlayerMover>();
            mover.SetControlEnabled(true);
            player.transform.position = course.transform.position + Vector3.down * 3f;
            Call(rehearsal, "UpdatePlacement", mover);
            Assert.That(Call(rehearsal, "IsStartPosition", player.transform.position), Is.True,
                "출발대 밖으로 밀린 배치 대기자는 입력 잠금 상태로 영구 대기하지 않는다.");
            Assert.That(mover.ControlEnabled, Is.False, "위치 복제/배치 승인을 기다리는 동안 조작 잠금 유지");
        }

        [Test]
        public void ClientActivationKeepsTheSnapshotReceivedBeforeRoomReadiness()
        {
            Set(rehearsal, "active", false);
            Set(rehearsal, "stage", -1);
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(9); writer.WriteValueSafe(0); writer.WriteValueSafe(5);
            writer.WriteValueSafe(false); writer.WriteValueSafe(false); writer.WriteValueSafe(false); writer.WriteValueSafe(-1d);
            writer.WriteValueSafe(4); writer.WriteValueSafe(1); writer.WriteValueSafe(701UL);
            for (int i = 0; i < 4; i++) writer.WriteValueSafe(0); // arrived, votes, lessons, voters
            writer.WriteValueSafe(false); writer.WriteValueSafe(false); writer.WriteValueSafe(0); writer.WriteValueSafe(0);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Call(rehearsal, "HandleState", NetworkManager.ServerClientId, reader);

            var panel = typeof(WaitingRoomTutorialPanel).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousPanel = panel.GetValue(null);
            bool previousHold = ScheduleScreenHold.IsHeld;
            try
            {
                // Hold the HUD in EditMode while exercising the real activation path.
                panel.SetValue(null, Make("Tutorial panel fixture").AddComponent<WaitingRoomTutorialPanel>());
                ScheduleScreenHold.Set(true);
                Call(rehearsal, "Activate");
            }
            finally
            {
                ScheduleScreenHold.Set(previousHold);
                panel.SetValue(null, previousPanel);
            }
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(0));
            Assert.That(Get(rehearsal, "round"), Is.EqualTo(5));
            Assert.That(Get(rehearsal, "participantCount"), Is.EqualTo(4));
            Assert.That((HashSet<ulong>)Get(rehearsal, "placed"), Is.EquivalentTo(new[] { 701UL }));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InitialPlacementWaitsForTheNetworkSlotAndUsesTheRaisedStart(int assignedSlot)
        {
            var player = Participant(701, false);
            var slot = (NetworkVariable<int>)Get(player, "slot");
            slot.Value = NetworkPlayer.NoSlot;
            var mover = player.GetComponent<PlayerMover>();
            var before = course.transform.position + Vector3.down * 3f;
            player.transform.position = before;
            Call(rehearsal, "Bind", player);

            Call(rehearsal, "PlaceLocal", mover);
            Assert.That(player.transform.position, Is.EqualTo(before));
            Assert.That(Get(rehearsal, "placedRound"), Is.EqualTo(-1));

            slot.Value = assignedSlot;
            Call(rehearsal, "PlaceLocal", mover);
            Assert.That(player.transform.position, Is.EqualTo(course.GetLanePosition(0, -.6f - assignedSlot * .85f)));
            Assert.That(Get(rehearsal, "placedRound"), Is.EqualTo(5));
        }

        [Test]
        public void ReplacingTheLocalPlayerReleasesTheOldLockAndPlacesTheNewOwnerInTheSameRound()
        {
            var first = Participant(701, false);
            var replacement = Participant(702, false);
            var firstMover = first.GetComponent<PlayerMover>();
            Call(rehearsal, "Bind", first);
            Call(rehearsal, "PlaceLocal", firstMover);
            Call(rehearsal, "UpdatePlacement", firstMover);
            Assert.That(firstMover.ControlEnabled, Is.False);

            Call(rehearsal, "Bind", replacement);
            Assert.That(firstMover.ControlEnabled, Is.True);
            Call(rehearsal, "PlaceLocal", replacement.GetComponent<PlayerMover>());
            Assert.That(replacement.transform.position, Is.EqualTo(course.GetLanePosition(0, -1.45f)));
        }

        [Test]
        public void Cannon_AllFourOnFinishDoNotNeedTransientCheckpointOrBounceHistory()
        {
            var cannon = Make("Cannon rehearsal fixture").AddComponent<WaitingRoomCannonRehearsal>();
            var arena = Make("Cannon course fixture").AddComponent<RehearsalCannonCourse>();
            arena.Cannon = arena.gameObject.AddComponent<HumanCannonSystem>();
            arena.Trampoline = Make("Trampoline fixture").AddComponent<HumanCannonTrampoline>();
            arena.MiddleFloor = Make("Middle floor").AddComponent<BoxCollider>();
            arena.FinishFloor = Make("Finish floor").AddComponent<BoxCollider>();
            arena.MiddleFloor.transform.position = course.transform.position;
            arena.FinishFloor.transform.position = course.transform.position + Vector3.forward * 50f;
            arena.FinishFloor.transform.localScale = new Vector3(8, 1, 10);
            Physics.SyncTransforms();
            Set(cannon, "course", arena);
            var shared = Get(cannon, "shared");
            shared.GetType().GetField("active").SetValue(shared, true);
            var placed = (List<ulong>)shared.GetType().GetField("placed").GetValue(shared);
            for (ulong id = 701; id <= 704; id++)
            {
                var player = Participant(id);
                player.transform.position = arena.FinishFloor.bounds.center + Vector3.up * arena.FinishFloor.bounds.extents.y;
                placed.Add(id);
            }
            Call(cannon, "HostTick");
            Assert.That(shared.GetType().GetField("ending").GetValue(shared), Is.True,
                "기획상 전원 최종 도착은 완료다. 중간 도착/반동 관찰 순서로 막지 않는다.");
            Set(cannon, "course", null); // Fixture cleanup uses DestroyImmediate, never runtime Destroy.
        }

        [Test]
        public void Cannon_LandingAtTheVisibleFloorEdgeCounts()
        {
            var floor = Make("Floor edge fixture").AddComponent<BoxCollider>();
            floor.size = new Vector3(8, 1, 10);
            floor.transform.position = course.transform.position;
            Physics.SyncTransforms();
            var feet = new Vector3(floor.bounds.max.x - .05f, floor.bounds.max.y, floor.bounds.center.z);
            Assert.That(RehearsalCannonCourse.Contains(floor, feet), Is.True);
            feet.x = floor.bounds.max.x + .05f;
            Assert.That(RehearsalCannonCourse.Contains(floor, feet), Is.False);
        }

        [Test]
        public void Cannon_UnacknowledgedPlayerOutsideStartIsReplacedButApprovedPlayerIsNot()
        {
            var cannon = Make("Cannon placement fixture").AddComponent<WaitingRoomCannonRehearsal>();
            var arena = Make("Cannon placement course").AddComponent<RehearsalCannonCourse>();
            arena.StartFloor = Make("Start floor").AddComponent<BoxCollider>();
            ((BoxCollider)arena.StartFloor).size = new Vector3(12, 1, 8);
            arena.StartFloor.transform.position = course.transform.position;
            Physics.SyncTransforms();
            Set(cannon, "course", arena);
            var player = Participant(701, false);
            player.transform.position = course.transform.position + Vector3.down * 3f;
            Call(cannon, "PlaceLocalAtCheckpoint", player, player.GetComponent<PlayerMover>());
            Assert.That(RehearsalCannonCourse.Contains(arena.StartFloor, player.transform.position), Is.True);
            var shared = Get(cannon, "shared");
            ((List<ulong>)shared.GetType().GetField("placed").GetValue(shared)).Add(701);
            var progressing = course.transform.position + Vector3.forward * 10f;
            player.transform.position = progressing;
            Call(cannon, "PlaceLocalAtCheckpoint", player, player.GetComponent<PlayerMover>());
            Assert.That(player.transform.position, Is.EqualTo(progressing), "배치 승인 뒤 정상 진행자는 끌어오지 않는다.");
            Set(cannon, "course", null);
        }

        [Test]
        public void IntermediatePlatformExplainsTheNativePlatformJump()
        {
            var player = Participant(701);
            Set(rehearsal, "local", player);
            var mover = player.GetComponent<PlayerMover>();
            mover.FreeMovement = true;
            player.transform.position = course.GetLanePosition(0, 23f);
            string guide = (string)Call(rehearsal, "QuestFor", RehearsalStage.All[0], mover);
            StringAssert.Contains("발판에서는", guide);
            StringAssert.Contains("이동하며 Space", guide);
        }

        [TestCase(true)] [TestCase(false), Category("WaitingRoomCommonRecovery")]
        public void RingBoundaryRemainsDuringCourseAndGathering(bool originallyActive)
        {
            var room = Make("Fixture practice room");
            var arena = Make("RoundArena"); arena.transform.SetParent(room.transform);
            var boundary = Make("Frame8Boundary"); boundary.transform.SetParent(arena.transform);
            var wall = boundary.AddComponent<BoxCollider>();
            boundary.SetActive(originallyActive);
            var legacy = Make("LegacyRing"); legacy.transform.SetParent(room.transform);
            var practiceLanes = Make("PracticeLanes"); practiceLanes.transform.SetParent(legacy.transform);
            Set(rehearsal, "ringRoom", room.transform);
            Call(rehearsal, "HideRingProps", true);
            Call(rehearsal, "SetRingBoundaryHidden", true);
            Call(rehearsal, "SetRingBoundaryHidden", true);
            Assert.That(boundary.activeSelf, Is.False);

            // 이미 생성된 코스의 반복 Ensure도 기존 외곽 경계를 복원·유지한다.
            Set(rehearsal, "builtStage", 0);
            Call(rehearsal, "EnsureCourse");
            Assert.That(wall.gameObject.activeInHierarchy, Is.EqualTo(originallyActive), "교육 코스에는 대체 외곽벽이 없으므로 원래 경계를 유지한다.");
            Assert.That(Get(rehearsal, "course"), Is.SameAs(course));

            // 코스가 정리되고 참가자는 아직 리허설 집결 상태에 있다.
            Set(rehearsal, "completed", true);
            Set(rehearsal, "course", null);
            Set(rehearsal, "courseRoot", null);
            Set(rehearsal, "builtStage", -1); // Geometry is already cleared; this check does not instantiate Play-only completion UI.
            Call(rehearsal, "EnsureCourse");
            Assert.That(wall.gameObject.activeInHierarchy, Is.EqualTo(originallyActive));
            Assert.That(practiceLanes.activeSelf, Is.False, "집결 경로에 옛 연습 기물을 되돌리지 않는다.");
        }

        [TestCase(WaitingRoomSceneVariant.Base)]
        [TestCase(WaitingRoomSceneVariant.Pursuit)]
        [TestCase(WaitingRoomSceneVariant.Integrated)]
        public void ActualPracticeRoomSuppliesTheDeckAndFitsEveryLesson(WaitingRoomSceneVariant variant)
        {
            var scene = EditorSceneManager.OpenPreviewScene(WaitingRoomScenes.Path(variant));
            try
            {
                var room = WaitingRoomScenes.FindPracticeRoom(scene);
                Assert.IsNotNull(room, "Use the physical practice room in the saved scene.");
                var deck = room.Find("LegacyRing/PracticeRing/DeckCollider")?.GetComponent<Collider>();
                var boundary = room.Find("RoundArena/Frame8Boundary/WallColliders");
                Assert.IsNotNull(deck);
                Assert.IsNotNull(room.Find("Corridor/FloorCollider")?.GetComponent<Collider>());
                Assert.IsNotNull(boundary);
                var bounds = deck.bounds;
                var center = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                var pose = Make("Saved rehearsal bounds").transform;
                foreach (var lesson in RehearsalStage.All)
                {
                    var shape = TightropeSettings.CourseSettings.FromJson(lesson.CourseJson);
                    pose.position = center + new Vector3(0f, 2.5f, -(lesson.Finish - 1f) * .5f);
                    Assert.IsTrue(RehearsalPlatformLayout.TryMeasureInnerRadius(boundary, center, pose.position.y, out float radius));
                    float scale = RehearsalPlatformLayout.FitFinishScale(shape, pose, center, boundary, 2f);
                    Assert.IsTrue(RehearsalPlatformLayout.FitsInsideRoom(TightropeCourse.PreviewStartPlatformBounds(shape), pose, center, radius, 0f), lesson.Id);
                    Assert.IsTrue(TightropeCourse.PreviewFinishPlatformBounds(shape, scale, out var finish), lesson.Id);
                    Assert.IsTrue(RehearsalPlatformLayout.FitsInsideRoom(finish, pose, center, radius, RehearsalArrivalWalls.Thickness), lesson.Id);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [Category("RehearsalPlatformLayoutRegression")]
        public void EveryLessonFitsGeneratedStartFinishAndArrivalWallsInsideTheExistingRoom(int stage)
        {
            var lesson = RehearsalStage.All[stage];
            var shape = TightropeSettings.CourseSettings.FromJson(lesson.CourseJson);
            Vector3 center = new(9000f, 9000f, 9000f);
            course.transform.position = center + new Vector3(0f, 2.5f, -(lesson.Finish - 1f) * .5f);
            var boundary = Make("Measured room walls").transform;
            const int segments = 160;
            const float inner = 26f, thickness = .5f;
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + .5f) * step;
                var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var wall = new GameObject("WallCollider").AddComponent<BoxCollider>();
                wall.transform.SetParent(boundary, false);
                wall.transform.SetPositionAndRotation(center + outward * (inner * Mathf.Cos(step * .5f) + thickness * .5f) + Vector3.up * 6f,
                    Quaternion.LookRotation(new Vector3(-outward.z, 0, outward.x)));
                wall.size = new Vector3(thickness, 12f, 2f * inner * Mathf.Sin(step * .5f) + .05f);
            }
            Assert.IsTrue(RehearsalPlatformLayout.TryMeasureInnerRadius(boundary, center, course.transform.position.y, out float measured));
            Assert.That(measured, Is.InRange(25.98f, 26.01f));
            float scale = RehearsalPlatformLayout.FitFinishScale(shape, course.transform, center, boundary, 2f);
            Assert.That(scale, Is.InRange(1f, 2f));
            if (stage is 1 or 3) Assert.AreEqual(2f, scale, "Already fitting decks retain their requested expansion.");
            else Assert.Less(scale, 2f, "Only the overflowing expansion is limited.");
            Assert.IsTrue(TightropeCourse.PreviewFinishPlatformBounds(shape, scale, out var preview));
            JsonUtility.FromJsonOverwrite(lesson.CourseJson, Get(course, "sceneShape"));
            course.FinishPlatformScale = scale;
            Assert.IsTrue(course.TryGetFinishPlatformBounds(out var actual));
            Assert.AreEqual(preview, actual, "Generation must use the same fitted bounds as the placement calculation.");
            Assert.AreEqual(lesson.Finish, actual.min.z, "The finish entrance and traversal distance do not move.");
            var generated = new GameObject("Generated fitted platforms").transform;
            generated.SetParent(course.transform, false); Set(course, "generated", generated);
            var start = TightropeCourse.PreviewStartPlatformBounds(shape);
            var startObject = (GameObject)Call(course, "CreateBox", "StartPlatform", start.center, start.size, Color.white, true);
            var finishObject = (GameObject)Call(course, "CreateBox", "FinishPlatform", actual.center, actual.size, Color.white, true);
            var walls = (GameObject)Call(rehearsal, "BuildFinishWalls"); walls.SetActive(true);
            Physics.SyncTransforms();
            void Fits(Collider collider, string role)
            {
                var b = collider.bounds;
                foreach (float x in new[] { b.min.x, b.max.x })
                    foreach (float z in new[] { b.min.z, b.max.z })
                        Assert.That(Vector2.Distance(new Vector2(x, z), new Vector2(center.x, center.z)) + GameSettings.Base.Character.CapsuleRadius,
                            Is.LessThanOrEqualTo(measured + .01f), $"Lesson {lesson.Id}: {role} must leave room for the common capsule.");
            }
            Fits(startObject.GetComponent<Collider>(), "start platform");
            Fits(finishObject.GetComponent<Collider>(), "finish platform");
            foreach (var wall in walls.GetComponentsInChildren<Collider>()) Fits(wall, wall.name);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void DoubledFinishUsesItsWholeFloorAndMatchingWalls(int stage)
        {
            JsonUtility.FromJsonOverwrite(RehearsalStage.All[stage].CourseJson, Get(course, "sceneShape"));
            Assert.That(course.TryGetFinishPlatformBounds(out var original), Is.True);
            course.FinishPlatformScale = 2;
            Assert.That(course.TryGetFinishPlatformBounds(out var enlarged), Is.True);
            Assert.That(enlarged.size.x, Is.EqualTo(original.size.x * 2).Within(.001f));
            Assert.That(enlarged.size.z, Is.EqualTo(original.size.z * 2).Within(.001f));
            Assert.That(enlarged.min.z, Is.EqualTo(original.min.z).Within(.001f), "출발 쪽으로 늘려 코스를 건너뛰지 않는다.");
            Assert.That(enlarged.size.y, Is.EqualTo(original.size.y));
            var player = Participant(701);
            Vector3 Feet(float side, float distance) => course.transform.position + course.Right * side + course.Forward * distance;
            foreach (float side in new[] { enlarged.min.x + .05f, enlarged.max.x - .05f })
                foreach (float distance in new[] { enlarged.min.z + .05f, enlarged.max.z - .05f })
                {
                    player.transform.position = Feet(side, distance);
                    Assert.That(Call(rehearsal, "IsFinished", player), Is.True, $"구간{stage} 확장 발판 모서리");
                }
            player.transform.position = Feet(enlarged.max.x + .05f, enlarged.center.z);
            Assert.That(Call(rehearsal, "IsFinished", player), Is.False);
            player.transform.position = Feet(enlarged.center.x, enlarged.min.z - .05f);
            Assert.That(Call(rehearsal, "IsFinished", player), Is.False);
            player.transform.position = Feet(enlarged.center.x, enlarged.center.z) + Vector3.down * 1f;
            Assert.That(Call(rehearsal, "IsFinished", player), Is.False);
            var walls = (GameObject)Call(rehearsal, "BuildFinishWalls");
            Assert.That(walls.activeSelf, Is.False, "튜토리얼 도착자 화면에서만 켠다.");
            walls.SetActive(true);
            Physics.SyncTransforms();
            var right = walls.transform.Find("Right").GetComponent<BoxCollider>().bounds;
            var back = walls.transform.Find("Back").GetComponent<BoxCollider>().bounds;
            Assert.That(right.min.x - course.transform.position.x, Is.EqualTo(enlarged.max.x).Within(.005f));
            Assert.That(back.min.z - course.transform.position.z, Is.EqualTo(enlarged.max.z).Within(.005f));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void PracticeArrivedPlayerCanReturnFromFinishAndReenterWithoutLosingCredit(int stage)
        {
            Set(rehearsal, "practice", true);
            Set(rehearsal, "stage", stage);
            JsonUtility.FromJsonOverwrite(RehearsalStage.All[stage].CourseJson, Get(course, "sceneShape"));
            course.FinishPlatformScale = 2;
            Assert.That(course.TryGetFinishPlatformBounds(out var floor), Is.True);
            var player = Participant(701);
            var mover = player.GetComponent<PlayerMover>();
            Set(rehearsal, "local", player);
            var arrived = (HashSet<ulong>)Get(rehearsal, "arrived");
            arrived.Add(701);
            mover.SetStartPose(course.transform.position + course.Forward * 5f, Quaternion.identity, true);
            var before = player.transform.position;
            Call(rehearsal, "TrackPersonalProgress", mover);
            Assert.That(player.transform.position, Is.EqualTo(before), "재연습은 학습 완료 시점에도 강제 이동시키지 않는다.");
            Vector3 edge = course.transform.position + course.Right * floor.center.x
                + course.Forward * (floor.min.z + .4f);
            mover.SetStartPose(edge, Quaternion.LookRotation(-course.Forward), true);
            Call(rehearsal, "TrackPersonalProgress", mover);
            Physics.SyncTransforms();
            mover.Body.Move(-course.Forward * 2f);
            Assert.That(Vector3.Dot(edge - player.transform.position, course.Forward), Is.GreaterThan(1.9f),
                "도착 후에도 줄 쪽 출구를 투명 벽으로 막지 않는다.");
            Call(rehearsal, "TrackPersonalProgress", mover);
            Assert.That(Vector3.Dot(edge - player.transform.position, course.Forward), Is.GreaterThan(1.9f),
                "되돌아간 위치를 도착대로 강제 복귀시키지 않는다.");
            mover.Body.Move(course.Forward * 2f);
            Assert.That(Vector3.Distance(player.transform.position, edge), Is.LessThan(.05f));
            Assert.That(arrived.Contains(701), Is.True, "왕복 이동과 이수 기록은 분리한다.");
        }

        [TestCase(false), TestCase(true)]
        public void TutorialFinishBarrierUsesCommonSettingAndNeverBlocksPractice(bool block)
        {
            var instance = typeof(WaitingRoomRehearsal).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            var previousRun = CampaignRun.Instance;
            var settings = GameSettings.WaitingRoom.Tutorial;
            var barrier = settings.GetType().GetField("keepFinishBarrier", Fields);
            bool previousBarrier = settings.KeepFinishBarrier;
            try
            {
            barrier.SetValue(settings, block);
            var campaign = Make("Initial tutorial run").AddComponent<CampaignRun>();
            var state = new RunState(); state.StartSession(true); state.EndIntro();
            Set(campaign, "host", state); Set(campaign, "state", state.Snapshot);
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, campaign);
            Set(rehearsal, "run", campaign); instance.SetValue(null, rehearsal);
            var player = Participant(701);
            var mover = player.GetComponent<PlayerMover>();
            Set(rehearsal, "local", player);
            ((HashSet<ulong>)Get(rehearsal, "arrived")).Add(701);
            Call(rehearsal, "TrackPersonalProgress", mover);
            var walls = (GameObject)Get(rehearsal, "finishWalls");
            if (!block)
            {
                Assert.That(walls, Is.Null, "양방향 설정이면 최초 튜토리얼에서도 도착벽을 만들지 않는다.");
                Vector3 before = player.transform.position;
                mover.Body.Move(-course.Forward * 2f);
                Call(rehearsal, "TrackPersonalProgress", mover);
                Assert.That(Vector3.Dot(before - player.transform.position, course.Forward), Is.GreaterThan(1.9f));
                Assert.That(((HashSet<ulong>)Get(rehearsal, "arrived")).Contains(701), Is.True);
                return;
            }
            Assert.That(walls, Is.Not.Null);
            Assert.That(walls.activeSelf, Is.True);
            Set(rehearsal, "practice", true);
            Call(rehearsal, "TrackPersonalProgress", mover);
            Assert.That(walls.activeSelf, Is.False, "튜토리얼 벽이 남아 있어도 재연습을 막으면 안 된다.");
            }
            finally
            {
                barrier.SetValue(settings, previousBarrier);
                instance.SetValue(null, previous);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            }
        }

        [Test]
        public void PracticeKeepsCourseAfterAllArriveUntilMajorityRequestsNext()
        {
            Set(rehearsal, "practice", true);
            for (ulong id = 701; id <= 704; id++) Participant(id);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(0));
            Assert.That(Get(rehearsal, "round"), Is.EqualTo(5));
            Assert.That((HashSet<ulong>)Get(rehearsal, "arrived"), Has.Count.EqualTo(4));
            var votes = (HashSet<ulong>)Get(rehearsal, "votes");
            votes.Add(701); votes.Add(702);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(0));
            votes.Add(703);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(1));
        }

        [Test]
        public void ArrivalWaitsForPlacementAndCurrentCourseRound()
        {
            var player = Participant(701, false);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.Zero);
            ((HashSet<ulong>)Get(rehearsal, "placed")).Add(701);
            Set(rehearsal, "builtRound", 4);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.Zero);
            Set(rehearsal, "builtRound", 5);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(1));
        }

        [Test]
        public void DepartedParticipantDoesNotKeepArrivedPlayersWaiting()
        {
            for (ulong id = 701; id <= 703; id++) Participant(id);
            var leaving = Participant(704, false);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.Zero);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(leaving, false);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(1));
        }

        [TestCase(1, 64)] [TestCase(2, 128)] [TestCase(3, 16)] [TestCase(4, 128)]
        public void NativeDeviceLessonsStillRequireTheirSkillAndAdvanceAfterIt(int stage, int learned)
        {
            Set(rehearsal, "stage", stage); Set(rehearsal, "lessonRound", 5);
            Participant(701);
            Call(rehearsal, "HostJudgeStage");
            Assert.That(Get(rehearsal, "stage"), Is.EqualTo(stage));
            Assert.That((HashSet<ulong>)Get(rehearsal, "arrived"), Is.Empty);
            ((Dictionary<ulong, byte>)Get(rehearsal, "lessonProgress"))[701] = (byte)learned;
            Call(rehearsal, "HostJudgeStage");
            if (stage == 4) Assert.That(Get(rehearsal, "completed"), Is.True);
            else Assert.That(Get(rehearsal, "stage"), Is.EqualTo(stage + 1));
        }

        [TestCase(RehearsalClearRule.EveryoneOrMajority, 0, 0, 0, false)]
        [TestCase(RehearsalClearRule.EveryoneOrMajority, 4, 0, 4, false)]
        [TestCase(RehearsalClearRule.EveryoneOrMajority, 4, 1, 2, false)]
        [TestCase(RehearsalClearRule.EveryoneOrMajority, 4, 1, 3, true)]
        [TestCase(RehearsalClearRule.EveryoneOrMajority, 3, 1, 2, true)]
        [TestCase(RehearsalClearRule.EveryoneOrMajority, 4, 4, 0, true)]
        [TestCase(RehearsalClearRule.Everyone, 4, 3, 4, false)]
        [TestCase(RehearsalClearRule.Anyone, 4, 1, 0, true)]
        public void StageCompletionUsesItsDeclaredRule(RehearsalClearRule rule, int participants, int arrivals, int votes, bool expected)
            => Assert.That(new RehearsalStage { Rule = rule }.CanAdvance(participants, arrivals, votes), Is.EqualTo(expected));

        [TestCase(4, false)] [TestCase(5, true)]
        public void VotePacketsMustMatchTheCurrentRound(int packetRound, bool accepted)
        {
            Participant(701);
            ((HashSet<ulong>)Get(rehearsal, "arrived")).Add(701);
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe((byte)1); writer.WriteValueSafe(packetRound); writer.WriteValueSafe(true);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Call(rehearsal, "HandleRequest", 701UL, reader);
            Assert.That(((HashSet<ulong>)Get(rehearsal, "votes")).Contains(701), Is.EqualTo(accepted));
        }
    }
}
