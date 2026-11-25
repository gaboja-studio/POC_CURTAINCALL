using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Settings.EditorTools
{
    /// <summary>
    /// 2026-11-26 Task-204: 목마 해제 낙하의 부상 예외와 침대에 누울 때 목마 해제.
    /// 실제 충돌체 지지 판정·낙하 구간 코드·호스트 목마 연결을 쓴다. RPC 순서는 합성이며 다른 PC 플레이 검증은 아니다.
    /// </summary>
    [Category("CommonProgressionRegression")]
    public sealed class PiggybackDismountAndBedTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Any).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);

        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> crew = new();
        readonly Vector3 floor = new(7400, 100, 7400);
        PiggybackSystem stack;
        object savedStack, savedLocal;
        bool savedSuppressed;
        System.Func<NetworkPlayer, PlayerState, bool> previousGate;
        CurtainCall.Survival.SurvivalPlace previousPlace;
        UnityEngine.SceneManagement.Scene scene;
        string previousSceneName;

        GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }

        [SetUp]
        public void SetUp()
        {
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            previousSceneName = scene.name;
            scene.name = "PiggybackDismountRegression";
            previousGate = NetworkPlayer.ServerCanChangeState;
            previousPlace = CurtainCall.Survival.SurvivalRules.ServerPlace;
            NetworkPlayer.ServerCanChangeState = null;
            CurtainCall.Survival.SurvivalRules.ServerPlace = CurtainCall.Survival.SurvivalPlace.Exploration;
            savedStack = typeof(PiggybackSystem).GetProperty("Current").GetValue(null);
            savedLocal = NetworkPlayer.Local;
            savedSuppressed = PiggybackSystem.Suppressed;
            PiggybackSystem.Suppressed = false;
            var go = Make("Dismount stack");
            stack = go.AddComponent<PiggybackSystem>();
            var follow = go.AddComponent<PiggybackFollow>(); Set(follow, "system", stack);
            var session = Make("Dismount authority").AddComponent<NetworkSessionManager>();
            typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            Set(stack, "session", session);
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);
            Floor(floor);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, savedStack);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, savedLocal);
            PiggybackSystem.Suppressed = savedSuppressed;
            var all = (List<NetworkPlayer>)NetworkPlayer.All;
            foreach (var player in crew) { typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false); all.Remove(player); }
            crew.Clear();
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            NetworkPlayer.ServerCanChangeState = previousGate;
            CurtainCall.Survival.SurvivalRules.ServerPlace = previousPlace;
            scene.name = previousSceneName;
        }

        void Floor(Vector3 top)
        {
            var box = Make("Floor").AddComponent<BoxCollider>();
            box.size = new Vector3(12, 1, 12); box.transform.position = top - Vector3.up * .5f;
            Physics.SyncTransforms();
        }

        NetworkPlayer Player(ulong id, Vector3 feet)
        {
            var go = Make("Crew " + id);
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            go.AddComponent<PlayerBalance>();
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, false);
            typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, id);
            player.transform.position = feet;
            player.BeginFallAt(feet);
            crew.Add(player);
            ((List<NetworkPlayer>)NetworkPlayer.All).Add(player);
            return player;
        }

        NetworkPlayer[] Stack(int count, Vector3 feet)
        {
            var members = new NetworkPlayer[count];
            float height = GameSettings.Base.Character.CapsuleHeight;
            var below = (Dictionary<ulong, ulong>)Get(stack, "belowOf");
            var above = (Dictionary<ulong, ulong>)Get(stack, "aboveOf");
            for (int i = 0; i < count; i++)
            {
                members[i] = Player((ulong)(2100 + i), feet + Vector3.up * height * i);
                if (i == 0) continue;
                below[members[i].OwnerClientId] = members[i - 1].OwnerClientId;
                above[members[i - 1].OwnerClientId] = members[i].OwnerClientId;
            }
            Physics.SyncTransforms();
            return members;
        }

        void Unlink(NetworkPlayer upper)
        {
            var below = (Dictionary<ulong, ulong>)Get(stack, "belowOf");
            var above = (Dictionary<ulong, ulong>)Get(stack, "aboveOf");
            if (below.Remove(upper.OwnerClientId, out ulong lower)) above.Remove(lower);
        }

        /// <summary>목마 해제(분리 점프) 뒤 착지까지 소유자 낙하 구간을 재생하고 호스트 착지 판정을 실행한다.</summary>
        void JumpOffAndLand(NetworkPlayer top, Vector3 landing, bool chainFall = false)
        {
            Call(top, "LateUpdate"); // 목마 위: 매 프레임 구간 초기화 + 목마 높이 기록
            Unlink(top);
            if (chainFall) Call(top, "PrepareNaturalFall"); // 아래 단원 추락으로 떨어지는 연쇄 추락
            top.transform.position += Vector3.up * GameSettings.Base.Character.JumpHeight * GameSettings.Tightrope.Piggyback.JumpHeightMultiplier;
            Physics.SyncTransforms(); Call(top, "LateUpdate");
            top.transform.position = landing;
            Physics.SyncTransforms(); Call(top, "LateUpdate");
            // 한 객체가 소유자·호스트를 겸한다. 소유자 착지 보고가 호스트가 관측한 최고점을 지우지 않게 따로 보존한다.
            float hostObservedPeak = (float)Get(top, "fallPeak");
            Call(top, "QueueFallLanding", hostObservedPeak, landing);
            Set(top, "fallPeak", hostObservedPeak);
            float peak = (float)Get(top, "pendingLandingPeak");
            Assert.IsTrue((bool)Call(top, "AcceptFallLanding", (uint)Get(top, "fallSequence"), 0, 0, peak, landing));
        }

        [Test]
        public void FourStackTopDismountToTheSameFloorIsNotInjured()
        {
            var crew = Stack(4, floor);
            var top = crew[3];
            Call(top, "LateUpdate");
            Assert.That(top.StackFallCredit, Is.EqualTo(GameSettings.Base.Character.CapsuleHeight * 3f).Within(.01f), "맨 아래 발부터 맨 위 발까지");
            Assert.That(GameSettings.Base.Character.CapsuleHeight * 3f + 1.2f, Is.GreaterThanOrEqualTo(GameSettings.Base.BodyDamage.FallInjuryHeight),
                "재현 조건: 목마 높이+점프가 부상 높이 이상");
            JumpOffAndLand(top, floor + Vector3.right * 1.2f);
            Assert.AreEqual(BodyPart.None, top.LostParts, "목마 해제 낙하는 부상 예외");
            Assert.AreEqual(PlayerState.Normal, top.State);
        }

        [Test]
        public void DismountIntoAPitStillCountsTheRealDrop()
        {
            var crew = Stack(4, floor);
            var pit = floor + new Vector3(30f, -10f, 0f);
            Floor(pit);
            JumpOffAndLand(crew[3], pit);
            Assert.AreEqual(GameSettings.Base.BodyDamage.FallInjuryCutCount, PlayerCondition.CountLimbs(crew[3].LostParts),
                "목마 높이만 빼고 구덩이 낙차(10m)는 그대로 부상");
        }

        [Test]
        public void ChainFallFromAFallenCarrierKeepsTheExistingInjuryRule()
        {
            var crew = Stack(4, floor);
            JumpOffAndLand(crew[3], floor + Vector3.right * 1.2f, chainFall: true);
            Assert.AreEqual(GameSettings.Base.BodyDamage.FallInjuryCutCount, PlayerCondition.CountLimbs(crew[3].LostParts),
                "연쇄 추락은 예외가 아니다");
        }

        static IEnumerable<TestCaseData> BedCases()
        {
            // (인원, 누운 층) — 맨 아래·중간·맨 위
            yield return new TestCaseData(2, 1).SetName("Bed_TwoStack_TopLiesDown");
            yield return new TestCaseData(2, 0).SetName("Bed_TwoStack_BottomLiesDown");
            yield return new TestCaseData(3, 1).SetName("Bed_ThreeStack_MiddleLiesDown");
            yield return new TestCaseData(4, 0).SetName("Bed_FourStack_BottomLiesDown");
        }

        [TestCaseSource(nameof(BedCases))]
        public void LyingDownReleasesThatMemberAndEveryoneAboveOntoTheFloor(int count, int resting)
        {
            scene.name = WaitingRoomScenes.Name;
            var crew = Stack(count, floor);
            var rider = crew[count - 1];
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(rider, true);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, rider);
            ((NetworkVariable<int>)Get(crew[resting], "restingBed")).Value = 0;
            Call(stack, "ServerReleaseWaitingRoomStates");
            int firstReleased = Mathf.Max(1, resting);
            for (int i = firstReleased; i < count; i++) Assert.IsNull(stack.GetBelow(crew[i]), $"{i}층은 목마에서 내려온다");
            for (int i = 1; i < firstReleased; i++) Assert.IsNotNull(stack.GetBelow(crew[i]), "누운 단원 아래층은 목마를 유지한다");
            // 맨 위 단원은 머리 위에 떠 있지 않고 아래 단원 옆 바닥에 선다(누운 사람이 맨 위면 그 사람 자신).
            Assert.That(rider.transform.position.y, Is.EqualTo(floor.y + .035f).Within(.05f), "바닥에 내려 선다");
            Assert.That(Vector3.ProjectOnPlane(rider.transform.position - crew[0].transform.position, Vector3.up).magnitude,
                Is.GreaterThan(GameSettings.Base.Character.CapsuleRadius * 2f), "아래 단원과 겹치지 않는다");
        }

        [TestCase(1)] [TestCase(0)]
        public void LyingDownReleasesEvenWhileThePracticeRopeCourseIsOpen(int resting)
        {
            // 실제 원인: 연습실 자유 연습 외줄(TightropeCourse, 공연 연결 없음)은 첫 리허설 뒤 낮·밤 내내 켜져 있어
            // 예전 판정(InWaitingRoom)이 거짓이 되고 침대 해제가 건너뛰어졌다.
            scene.name = WaitingRoomScenes.Name;
            var courseField = typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            var previousCourse = courseField.GetValue(null);
            var courseObject = new GameObject("Practice free-play rope course");
            try
            {
                var course = courseObject.AddComponent<TightropeCourse>();
                courseField.SetValue(null, course);
                Assert.IsTrue(course.IsGameplayActive, "재현 조건: 연습실 외줄이 켜져 있다");
                Assert.IsFalse((bool)typeof(PiggybackSystem).GetProperty("InWaitingRoom", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null),
                    "재현 조건: 예전 대기실 판정은 거짓");
                var crew = Stack(2, floor);
                ((NetworkVariable<int>)Get(crew[resting], "restingBed")).Value = 0;
                Call(stack, "ServerReleaseWaitingRoomStates");
                Assert.IsNull(stack.GetBelow(crew[1]), resting == 1 ? "위 사람이 누우면 내려온다" : "아래 사람이 누우면 위 사람이 내려온다");
            }
            finally
            {
                courseField.SetValue(null, previousCourse);
                Object.DestroyImmediate(courseObject);
            }
        }

        [Test]
        public void RestingMemberCannotStartANewPiggyback()
        {
            scene.name = WaitingRoomScenes.Name;
            var climber = Player(2201, floor);
            var target = Player(2202, floor + Vector3.right * .6f);
            Physics.SyncTransforms();
            Assert.AreSame(target, stack.FindMountTarget(climber), "재현 조건: 평소에는 옆 동료 위로 탈 수 있다");
            ((NetworkVariable<int>)Get(target, "restingBed")).Value = 1;
            Assert.IsNull(stack.FindMountTarget(climber), "누운 단원 위로는 목마를 시작하지 않는다");
        }
    }
}
