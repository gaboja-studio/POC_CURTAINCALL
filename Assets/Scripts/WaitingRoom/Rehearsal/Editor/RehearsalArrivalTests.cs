using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
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
            Call(cannon, "PlaceLocalAtCheckpoint", player, player.GetComponent<PlayerMover>(), false);
            Assert.That(RehearsalCannonCourse.Contains(arena.StartFloor, player.transform.position), Is.True);
            var shared = Get(cannon, "shared");
            ((List<ulong>)shared.GetType().GetField("placed").GetValue(shared)).Add(701);
            var progressing = course.transform.position + Vector3.forward * 10f;
            player.transform.position = progressing;
            Call(cannon, "PlaceLocalAtCheckpoint", player, player.GetComponent<PlayerMover>(), false);
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

        [TestCase(true)] [TestCase(false)]
        public void OverlappingRingBoundaryIsHiddenAndItsOriginalVisibilityRestored(bool originallyActive)
        {
            var room = Make("Fixture practice room");
            var arena = Make("RoundArena"); arena.transform.SetParent(room.transform);
            var boundary = Make("Frame8Boundary"); boundary.transform.SetParent(arena.transform);
            boundary.SetActive(originallyActive);
            Set(rehearsal, "ringRoom", room.transform);
            Call(rehearsal, "HideRingProps", true);
            Assert.That(boundary.activeSelf, Is.False);
            Call(rehearsal, "HideRingProps", true); // Repeated entry must not change the restore set.
            Call(rehearsal, "HideRingProps", false);
            Assert.That(boundary.activeSelf, Is.EqualTo(originallyActive));
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
            Assert.That(walls.activeSelf, Is.False, "도착자 화면에서만 켠다.");
            walls.SetActive(true);
            Physics.SyncTransforms();
            var right = walls.transform.Find("Right").GetComponent<BoxCollider>().bounds;
            var back = walls.transform.Find("Back").GetComponent<BoxCollider>().bounds;
            Assert.That(right.min.x - course.transform.position.x, Is.EqualTo(enlarged.max.x).Within(.005f));
            Assert.That(back.min.z - course.transform.position.z, Is.EqualTo(enlarged.max.z).Within(.005f));
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
