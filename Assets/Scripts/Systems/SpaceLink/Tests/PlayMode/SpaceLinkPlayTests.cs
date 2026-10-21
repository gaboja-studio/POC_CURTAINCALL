using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.SpaceLink.Tests
{
    /// <summary>
    /// 오프라인 런으로 장소 전환(통째 교체·추가 로드)과 전환 뒤 런 상태 유지를 확인한다.
    /// 접속 중 NGO 전환·2명 확인·실제 클릭은 공동 테스트로 따로 한다.
    /// </summary>
    public sealed class SpaceLinkPlayTests
    {
        const string HubPath = "Assets/Scenes/Tests/Systems/SpaceLink/SpaceLink.unity";
        const float TransitionTimeoutSeconds = 60f;

        object campaign;
        Component director;
        int maxSceneCount; // 전환 중 한꺼번에 열린 씬 수의 최대
        readonly List<Action> restore = new();
        readonly List<GameObject> actors = new();
        object keyboard;

        void Key(string name, float value)
        {
            var input = GameType("UnityEngine.InputSystem.InputSystem");
            keyboard ??= input.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Keyboard", null, null });
            var keyType = GameType("UnityEngine.InputSystem.Key");
            var keys = Array.CreateInstance(keyType, value > 0 ? 1 : 0);
            if (value > 0) keys.SetValue(Enum.Parse(keyType, name.Substring(0, name.Length - 3), true), 0);
            var stateType = GameType("UnityEngine.InputSystem.LowLevel.KeyboardState");
            var state = Activator.CreateInstance(stateType, new object[] { keys });
            input.GetMethods().Single(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(stateType).Invoke(null, new[] { keyboard, state, -1d });
        }

        Component Actor(string name, Vector3 position)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            actors.Add(go);
            var mover = go.AddComponent(GameType("CurtainCall.Player.PlayerMover"));
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            Place(mover, position);
            return mover;
        }

        static void Place(Component mover, Vector3 position) =>
            mover.GetType().GetMethod("SetStartPose", new[] { typeof(Vector3), typeof(Quaternion) })
                .Invoke(mover, new object[] { position, Quaternion.identity });

        IEnumerator OpenPhysicalRoom()
        {
            yield return OpenHub();
            SetMode("Single");
            SetTimer("FromLeavingWaitingRoom");
            Override(Get(Settings("Base"), "SpaceTransition"), "waitingRoomScene", "WaitingRoomBase");
            Override(Get(Settings("Base"), "SpaceTransition"), "keepWaitingRoomOnExit", true);
            Assert.IsTrue((bool)Call(campaign, "StartOfflineRun"));
            yield return WaitIdle();
            yield return Signal("EndIntro");
        }

        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name) is PropertyInfo p ? p.GetValue(target) : target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        static object Static(string type, string name) => GameType(type).GetProperty(name).GetValue(null);
        static object Settings(string name) => Static("CurtainCall.Settings.GameSettings", name);
        static string Active => SceneManager.GetActiveScene().name;
        static bool Loaded(string name) => SceneManager.GetSceneByName(name).isLoaded;
        string Phase => Get(Get(campaign, "State"), "Phase").ToString();
        object State => Get(campaign, "State");

        void Override(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            object before = info.GetValue(target);
            restore.Add(() => info.SetValue(target, before));
            info.SetValue(target, value);
        }

        void SetMode(string mode) => Override(Get(Settings("Base"), "SpaceTransition"), "mode",
            Enum.Parse(GameType("CurtainCall.Settings.SpaceTransitionMode"), mode));

        void SetTimer(string mode) => Override(Get(Settings("Base"), "Run"), "restTimerMode",
            Enum.Parse(GameType("CurtainCall.Settings.RestTimerMode"), mode));

        IEnumerator OpenHub()
        {
            // 게임의 시작 씬은 로비(Task-052)지만, 이 테스트는 SpaceLink 허브로 장소 전환만 확인한다.
            Override(Get(Settings("Base"), "SpaceTransition"), "hubScene", "SpaceLink");
            Override(Get(Settings("Base"), "SpaceTransition"), "waitingRoomScene", "DummyWaitingRoom");
            Override(Get(Settings("Base"), "SpaceTransition"), "keepWaitingRoomOnExit", false);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            director = GameType("CurtainCall.SpaceLink.SpaceLinkDirector").GetMethod("EnsureExists").Invoke(null, null) as Component;
            campaign = GameType("CurtainCall.Campaign.CampaignRun").GetMethod("EnsureExists").Invoke(null, null);
            yield return null; // 각자의 Start(이벤트 연결)
            Assert.AreEqual("SpaceLink", Active);
            maxSceneCount = 0;
        }

        IEnumerator Signal(string method, params object[] args)
        {
            Assert.IsTrue((bool)Call(campaign, method, args), $"{method} ({Phase})");
            yield return WaitIdle();
        }

        IEnumerator WaitIdle()
        {
            yield return null;
            // 배치 모드 프레임은 실제 시간보다 빠르므로 대형 아트 씬의 로딩을 초 단위로 기다린다.
            float deadline = Time.realtimeSinceStartup + TransitionTimeoutSeconds;
            while ((bool)Get(director, "IsTransitioning") && Time.realtimeSinceStartup < deadline)
            {
                maxSceneCount = Math.Max(maxSceneCount, SceneManager.sceneCount);
                yield return null;
            }
            Assert.IsFalse((bool)Get(director, "IsTransitioning"), "장소 전환이 60초 안에 끝나지 않음");
            yield return null; // 새 공간 씬의 Start
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var action in Enumerable.Reverse(restore)) action();
            restore.Clear();
            foreach (var actor in actors) if (actor != null) Object.Destroy(actor);
            actors.Clear();
            if (keyboard != null)
            {
                GameType("UnityEngine.InputSystem.InputSystem").GetMethod("RemoveDevice")
                    .Invoke(null, new[] { keyboard });
                keyboard = null;
            }
            foreach (string type in new[] { "CurtainCall.WaitingRoom.ScheduleBoard", "CurtainCall.WaitingRoom.WaitingRoomShop", "CurtainCall.WaitingRoom.PropCarry" })
            {
                var manager = Static(type, "Instance") as Component;
                if (manager != null) Object.Destroy(manager.gameObject);
            }
            if (campaign is Component c && c != null) Call(campaign, "StopOfflineRun");
            var session = Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance") as Component;
            var network = session != null ? Get(session, "NetworkManager") as Component : null;
            foreach (var target in new[]
            {
                director,
                campaign as Component,
                Static("CurtainCall.Reward.PerformanceRewards", "Instance") as Component,
                session,
                network,
            })
                if (target != null) Object.Destroy(target.gameObject);
            yield return null;
        }

        [Test]
        public void Route_PhaseMapsToSpace()
        {
            var phase = GameType("CurtainCall.Campaign.CampaignPhase");
            var route = GameType("CurtainCall.SpaceLink.SpaceRoute").GetMethod("ForPhase");
            string For(string p) => route.Invoke(null, new[] { Enum.Parse(phase, p) }).ToString();
            Assert.AreEqual("Hub", For("None"));
            Assert.AreEqual("Hub", For("Intro"));
            Assert.AreEqual("Performance", For("Performance"));
            Assert.AreEqual("WaitingRoom", For("RestTime"));
            Assert.AreEqual("WaitingRoom", For("Night"));
            Assert.AreEqual("EscapeRoute", For("Escape"));
            Assert.AreEqual("Hub", For("Ending"));
        }

        [UnityTest]
        public IEnumerator Single_FollowsPhasesAndKeepsRunState()
        {
            yield return OpenHub();
            SetMode("Single");
            SetTimer("FromLeavingWaitingRoom");
            Assert.IsTrue((bool)Call(campaign, "StartOfflineRun"));
            yield return WaitIdle();
            Assert.AreEqual("SpaceLink", Active, "도입은 시작 씬");

            yield return Signal("EndIntro");
            Assert.AreEqual("DummyPerformance", Active);
            Assert.AreEqual(1, SceneManager.sceneCount, "통째 교체는 공간 씬만 남는다");
            var rewards = Static("CurtainCall.Reward.PerformanceRewards", "Instance");
            Assert.IsNotNull(rewards, "더미 공연장이 보상 관리자를 쓴다");
            long balance = (long)Get(rewards, "Balance");
            int generation = (int)Get(State, "Generation");
            int day = (int)Get(State, "Day");

            yield return Signal("EndPerformance");
            Assert.AreEqual("RestTime", Phase);
            Assert.AreEqual("DummyWaitingRoom", Active);
            Assert.AreSame(campaign, Static("CurtainCall.Campaign.CampaignRun", "Instance"), "런 관리자 유지");
            Assert.AreSame(rewards, Static("CurtainCall.Reward.PerformanceRewards", "Instance"), "보상·자금 관리자 유지");
            Assert.AreEqual(balance, (long)Get(rewards, "Balance"), "전환만으로 자금이 바뀌지 않는다");
            Assert.AreEqual(generation, (int)Get(State, "Generation"));
            Assert.AreEqual(day, (int)Get(State, "Day"));

            Assert.IsTrue((bool)Call(director, "LeaveWaitingRoom"));
            yield return WaitIdle();
            Assert.AreEqual("DummyExploration", Active);
            Assert.AreEqual("Running", Get(State, "Timer").ToString(), "B안: 대기실에서 나오면 타이머 시작");

            Assert.IsTrue((bool)Call(campaign, "StartEscape"));
            yield return WaitIdle();
            Assert.AreEqual("DummyEscapeRoute", Active);

            yield return Signal("EndEscape", true);
            Assert.AreEqual("Ending", Phase);
            Assert.AreEqual("SpaceLink", Active, "엔딩은 시작 씬");
        }

        [UnityTest]
        public IEnumerator PhysicalExit_KeepsRoomAndAllowsReturningPlayerWithoutRestartingTimer()
        {
            yield return OpenPhysicalRoom();
            // 실제 입력 대신 위치를 주입하는 2인 판정 회귀다. 네트워크 공동 플레이를 대체하지 않는다.
            var first = Actor("퇴장자", new Vector3(-2, .1f, 67));
            var second = Actor("잔류자", new Vector3(2, .1f, 67));
            yield return Signal("EndPerformance");
            Assert.AreEqual("WaitingRoomBase", Active);
            var scene = SceneManager.GetActiveScene();
            Assert.IsFalse(scene.GetRootGameObjects().Any(g => g.name == "WaitingRoom" || g.name == "대기실"));
            Assert.IsTrue(scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").activeSelf);
            Assert.IsFalse(scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomTestPanels").activeSelf);
            Assert.AreEqual("WaitingForExit", Get(State, "Timer").ToString());
            var spawnType = GameType("CurtainCall.Network.PlayerSync.PlayerSpawnPoints");
            Assert.AreEqual(1, Object.FindObjectsByType(spawnType, FindObjectsSortMode.None).Length);
            for (int slot = 0; slot < 4; slot++)
            {
                object[] pose = { slot, 3f, Vector3.zero, Quaternion.identity };
                spawnType.GetMethod("GetPose").Invoke(null, pose);
                var position = (Vector3)pose[2];
                var greybox = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
                var localPosition = greybox.InverseTransformPoint(position);
                Assert.That(localPosition.x, Is.InRange(-10f, 10f));
                Assert.That(localPosition.z, Is.InRange(-10f, 10f));
                Assert.Greater(localPosition.y, 0, "실제 그레이박스 로비 스폰을 쓴다");
            }
            var watch = Object.FindAnyObjectByType(GameType("CurtainCall.WaitingRoom.WaitingRoomExitWatch")) as Component;
            Place(first, new Vector3(-2, .1f, 67));
            Place(second, new Vector3(2, .1f, 67));
            yield return null;
            Place(first, new Vector3(12, .1f, 70f));
            yield return null;
            yield return null;
            yield return WaitIdle();
            Assert.AreEqual("Running", Get(State, "Timer").ToString());
            Assert.AreEqual(scene.handle, SceneManager.GetActiveScene().handle, "첫 퇴장으로 씬을 바꾸지 않는다");
            Assert.IsFalse(Loaded("DummyExploration"));
            Assert.AreEqual(new Vector3(2, .1f, 67), second.transform.position, "잔류자는 이동시키지 않는다");
            Assert.AreEqual(1, Get(watch, "LeftCount"));
            float remaining = (float)Get(State, "TimeRemaining");
            Place(first, new Vector3(0, .1f, 67));
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector3(0, .1f, 67), first.transform.position, "퇴장한 플레이어도 대기실에 재입장할 수 있다");
            Assert.AreEqual(1, Get(watch, "LeftCount"), "재입장해도 이번 쉬는 시간의 첫 퇴장 기록을 유지한다");
            Place(first, new Vector3(12, .1f, 70f));
            yield return null;
            yield return null;
            Assert.AreEqual(1, Get(watch, "LeftCount"), "같은 플레이어가 다시 나가도 중복 집계하지 않는다");
            Assert.LessOrEqual((float)Get(State, "TimeRemaining"), remaining, "재입장·재퇴장으로 탐색 시간을 초기화하지 않는다");
            Place(second, new Vector3(12, .1f, 70f));
            yield return null;
            yield return null;
            Assert.AreEqual(2, Get(watch, "LeftCount"));
            Assert.LessOrEqual((float)Get(State, "TimeRemaining"), remaining, "후속 퇴장으로 제한 시간을 초기화하지 않는다");
            var gather = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.WaitingRoom.StageEntranceGather"));
            var gatheredPosition = gather.transform.position - Vector3.up;
            Place(first, gatheredPosition);
            yield return null;
            yield return null;
            Assert.AreEqual("RestTime", Phase, "일부 집결만으로 공연을 시작하지 않는다");
            Place(second, gatheredPosition);
            yield return null;
            yield return WaitIdle();
            Assert.AreEqual("Performance", Phase);
            Assert.AreEqual("DummyPerformance", Active);
        }

        [UnityTest]
        public IEnumerator PhysicalRoom_PlayersCanPassEachOtherOnlyInsideRoom()
        {
            yield return OpenPhysicalRoom();
            var first = Actor("상점 앞 플레이어", new Vector3(5.8f, .08f, 63.2f));
            var second = Actor("퇴로 플레이어", new Vector3(6.2f, .08f, 63.2f));
            yield return Signal("EndPerformance");
            yield return null;
            Assert.IsTrue((bool)Get(first, "IgnorePlayerBlocking"));
            Assert.IsTrue((bool)Get(second, "IgnorePlayerBlocking"));
            var limit = first.GetType().GetMethod("LimitAgainstPlayers", BindingFlags.Instance | BindingFlags.NonPublic);
            var step = new Vector3(.8f, 0, 0);
            Assert.AreEqual(step, limit.Invoke(first, new object[] { step }), "상대 몸통 방향 이동을 자르지 않는다");
            var controller = first.GetComponent<CharacterController>();
            Assert.IsTrue(controller.detectCollisions, "환경 충돌은 계속 켠다");
            Assert.IsTrue(Physics.GetIgnoreCollision(controller, second.GetComponent<CharacterController>()));
            controller.Move(step);
            Assert.Greater(first.transform.position.x, second.transform.position.x, "실제 몸통도 서로 통과한다");

            Place(first, new Vector3(12, .08f, 70));
            Place(second, new Vector3(12.4f, .08f, 70));
            yield return null;
            yield return null;
            Assert.IsFalse((bool)Get(first, "IgnorePlayerBlocking"), "밖에서는 원래 제한 복구");
            Assert.IsFalse((bool)Get(second, "IgnorePlayerBlocking"));
            Assert.That(((Vector3)limit.Invoke(first, new object[] { new Vector3(.1f, 0, 0) })).sqrMagnitude, Is.LessThan(.00001f));

            Place(first, new Vector3(5.8f, .08f, 63.2f));
            Place(second, new Vector3(6.2f, .08f, 63.2f));
            yield return null;
            yield return null;
            Assert.IsTrue((bool)Get(first, "IgnorePlayerBlocking"));
            var watch = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.WaitingRoom.WaitingRoomExitWatch"));
            watch.gameObject.SetActive(false);
            Assert.IsFalse((bool)Get(first, "IgnorePlayerBlocking"), "공간 비활성·씬 해제 시 제한 복구");
            Assert.IsFalse((bool)Get(second, "IgnorePlayerBlocking"));
        }

        [UnityTest]
        public IEnumerator PhysicalRoom_GatherCoversCorridorWidthAndCurtainContact()
        {
            yield return OpenPhysicalRoom();
            var first = Actor("커튼 앞 왼쪽", new Vector3(-2, .08f, 67));
            var second = Actor("커튼 앞 오른쪽", new Vector3(2, .08f, 67));
            yield return Signal("EndPerformance");
            var gather = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.WaitingRoom.StageEntranceGather"));
            var area = gather.GetComponent<BoxCollider>().bounds;
            Assert.That(area.min.x, Is.EqualTo(47.2f).Within(.01f));
            Assert.That(area.max.x, Is.EqualTo(51.85f).Within(.01f));
            Assert.That(area.min.z, Is.EqualTo(66.15f).Within(.01f));
            Assert.That(area.max.z, Is.EqualTo(73.85f).Within(.01f));
            foreach (float z in new[] { 66.45f, 69.52f, 71.42f, 73.55f })
                Assert.IsTrue(area.Contains(new Vector3(51.29f, .58f, z)), "커튼 접촉 위치와 복도 양쪽을 포함한다");
            Place(first, new Vector3(51.29f, .08f, 69.52f));
            yield return null;
            yield return null;
            Assert.AreEqual(1, Get(gather, "InsideCount"));
            Assert.AreEqual("RestTime", Phase, "일부만 커튼에 붙으면 기다린다");
            Place(second, new Vector3(51.29f, .08f, 71.42f));
            yield return null;
            yield return WaitIdle();
            Assert.AreEqual("Performance", Phase, "전원이 커튼에 붙어 있어도 공연으로 전환한다");
            Assert.IsFalse((bool)Get(first, "IgnorePlayerBlocking"), "공연에서는 원래 이동 제한 복구");
        }

        [UnityTest]
        public IEnumerator PhysicalRoom_ChairCollisionAllowsRetreatAtStandingAndCrawlingHeights()
        {
            yield return OpenPhysicalRoom();
            var actor = Actor("의자 통행 검사", new Vector3(5.8f, .08f, 63.2f));
            yield return Signal("EndPerformance");
            var chair = GameObject.Find("WaitingRoomGreybox/Mood/Tunnel/WallDressing/SM_WW2_WoodenChair_c1_LOD0");
            Assert.IsNotNull(chair);
            Assert.IsFalse(chair.GetComponent<MeshCollider>().enabled);
            var proxy = chair.GetComponent<BoxCollider>();
            Assert.IsTrue(proxy.enabled);
            var controller = actor.GetComponent<CharacterController>();
            foreach (float height in new[] { .9f, .5f })
            {
                var p = proxy.bounds.center;
                p.x = proxy.bounds.min.x - .4f;
                p.y = .08f;
                Place(actor, p);
                controller.height = height;
                controller.center = Vector3.up * height * .5f;
                controller.Move(Vector3.right * .6f);
                float contactX = actor.transform.position.x;
                Assert.Less(contactX, proxy.bounds.min.x, "다리와 좌판 사이로 몸통이 들어가지 않는다");
                controller.Move(Vector3.left * .6f);
                Assert.Less(actor.transform.position.x, contactX - .4f, "접촉 후 반대쪽으로 빠져나올 수 있다");
            }
        }

        [UnityTest]
        public IEnumerator PhysicalRoom_NightLocksAndReadyStartsMorningWithoutReload()
        {
            yield return OpenPhysicalRoom();
            var reader = Actor("스케줄 상호작용", new Vector3(-7, .1f, 77.5f));
            yield return Signal("EndPerformance");
            Place(reader, new Vector3(12, .1f, 70f));
            yield return null;
            yield return null;
            Assert.IsTrue((bool)Call(director, "LeaveWaitingRoom"));
            yield return WaitIdle();
            Assert.AreEqual("WaitingRoomBase", Active, "API 퇴장 경로도 같은 씬 유지");
            yield return Signal("NotifyAllAtStageEntrance");
            yield return Signal("EndPerformance");
            Assert.AreEqual("Night", Phase);
            Assert.AreEqual("WaitingRoomBase", Active);
            Assert.AreNotEqual("Running", Get(State, "Timer").ToString());
            var door = Object.FindAnyObjectByType(GameType("CurtainCall.WaitingRoom.NightDoorLock"));
            Assert.IsTrue((bool)Get(door, "IsLocked"));
            yield return new WaitForSeconds(3);
            Assert.IsTrue((bool)Get(door, "IsFullyClosed"));
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.OverlapCapsule(new Vector3(10, .5f, 70f), new Vector3(10, 1.5f, 70f), .3f,
                ~0, QueryTriggerInteraction.Ignore).Any(c => c.transform.IsChildOf(((Component)door).transform)),
                "그레이박스 로비 출구에 닫힌 야간 셔터 충돌체가 있어야 한다");
            Place(reader, new Vector3(-7, .1f, 77.5f));
            reader.gameObject.AddComponent(GameType("CurtainCall.Player.PlayerInputReader"));
            var view = Object.FindAnyObjectByType(GameType("CurtainCall.WaitingRoom.ScheduleBoardView"));
            // 입력 리더 등록 직후에는 모든 키를 놓은 다음 프레임까지 InputGate가 막는다.
            for (int frame = 0; frame < 30 && (bool)Static("CurtainCall.Player.InputGate", "IsBlocked"); frame++)
                yield return null;
            yield return null;
            var inReach = view.GetType().GetField("inReach", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsTrue((bool)inReach.GetValue(view), $"스케줄표 조작 거리: player={reader.transform.position}");
            Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), "스케줄표 앞에서 입력이 차단되지 않음");
            Key("fKey", 1f);
            yield return new WaitForSecondsRealtime(.05f);
            Key("fKey", 0f);
            yield return new WaitForSecondsRealtime(.05f);
            Assert.IsTrue((bool)Get(view, "IsOpen"), "Input System F 누름·뗌 → 실제 스케줄표 열기");
            // 이후 스케줄 작성은 API 합성 검증이다. 키 입력 검증과 구분한다.
            var board = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            int night = (int)Get(State, "Day");
            var days = (int[])GameType("CurtainCall.WaitingRoom.ScheduleBook").GetMethod("EditableDays").Invoke(null, new object[] { night });
            foreach (int day in days)
            {
                Assert.AreEqual("None", Call(board, "Set", day, 0, "Tightrope").ToString());
                Assert.AreEqual("None", Call(board, "Set", day, 1, "HumanCannon").ToString());
            }
            Assert.AreEqual("None", Call(board, "Confirm").ToString());
            var scene = SceneManager.GetActiveScene();
            Assert.IsTrue((bool)Call(board, "RequestReady", true));
            yield return WaitIdle();
            Assert.AreEqual("RestTime", Phase);
            Assert.AreEqual("WaitingForExit", Get(State, "Timer").ToString());
            Assert.AreEqual(scene.handle, SceneManager.GetActiveScene().handle);
            Assert.IsFalse((bool)Get(door, "IsLocked"));
            var watch = Object.FindAnyObjectByType(GameType("CurtainCall.WaitingRoom.WaitingRoomExitWatch"));
            Assert.AreEqual(0, Get(watch, "LeftCount"), "새 쉬는 시간에는 이전 퇴장 이력을 비운다");
            yield return new WaitForSeconds(3);
            Physics.SyncTransforms();
            Assert.IsFalse(Physics.OverlapCapsule(new Vector3(10, .5f, 70f), new Vector3(10, 1.5f, 70f), .3f,
                ~0, QueryTriggerInteraction.Ignore).Any(c => c.transform.IsChildOf(((Component)door).transform)),
                "아침에는 실제 통로 중심의 셔터 충돌이 해제되어야 한다");
            Place(reader, new Vector3(12, .1f, 70f));
            yield return null;
            yield return null;
            Assert.AreEqual("Running", Get(State, "Timer").ToString(), "다음 아침에도 첫 물리 퇴장으로 시작");
            Assert.AreEqual(1, Get(watch, "LeftCount"));
            Assert.AreEqual(scene.handle, SceneManager.GetActiveScene().handle);
        }

        [UnityTest]
        public IEnumerator Hub_CanvasButtonsStartRunWithoutImguiPanel()
        {
            yield return OpenHub();
            SetMode("Single");
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Network.Session.NetworkSessionTestUI")), "시작 씬 IMGUI 접속 패널 제거");
            var hub = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.SpaceLink.SpaceLinkHubCanvas")) as Component;
            Assert.IsNotNull(hub, "시작 씬 Canvas 화면");
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType(GameType("UnityEngine.EventSystems.EventSystem")), "시작 씬 EventSystem");
            Assert.IsTrue((bool)Call(hub, "CanExecute", "offlineStart"));
            Call(hub, "Execute", "offlineStart");
            yield return WaitIdle();
            Assert.AreEqual("Intro", Phase, "Canvas '오프라인 런 시작'");
            Call(hub, "Execute", "intro");
            yield return WaitIdle();
            Assert.AreEqual("Performance", Phase, "Canvas '도입 끝 → 공연'");
            Assert.AreEqual("DummyPerformance", Active);
        }

        [UnityTest]
        public IEnumerator Additive_KeepsHubAndSwapsSpaceScenes()
        {
            yield return OpenHub();
            SetMode("Additive");
            SetTimer("FromLeavingWaitingRoom");
            Assert.IsTrue((bool)Call(campaign, "StartOfflineRun"));
            yield return WaitIdle();

            yield return Signal("EndIntro");
            Assert.IsTrue(Loaded("SpaceLink"), "추가 로드는 시작 씬이 남는다");
            Assert.IsTrue(Loaded("DummyPerformance"));
            Assert.AreEqual("DummyPerformance", Active, "공간 씬을 활성 씬으로");
            Assert.AreEqual(2, SceneManager.sceneCount);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType(GameType("UnityEngine.EventSystems.EventSystem"), FindObjectsSortMode.None).Length,
                "공간 씬이 붙어 있는 동안 시작 씬 EventSystem은 꺼서 하나만 켜짐");
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.SpaceLink.SpaceLinkHubCanvas")), "시작 씬 Canvas는 꺼짐");

            yield return Signal("EndPerformance");
            Assert.IsTrue(Loaded("SpaceLink"));
            Assert.IsTrue(Loaded("DummyWaitingRoom"));
            Assert.IsFalse(Loaded("DummyPerformance"), "이전 공간 씬은 닫는다");
            Assert.AreEqual(2, SceneManager.sceneCount);

            // 더미 대기실의 출구 신호처럼 런 진행에 직접 알려도(B안 타이머 시작) 백스테이지로 따라간다.
            Assert.IsTrue((bool)Call(campaign, "NotifyLeftWaitingRoom"));
            yield return WaitIdle();
            Assert.IsTrue(Loaded("DummyExploration"));
            Assert.IsFalse(Loaded("DummyWaitingRoom"));
            Assert.AreEqual("Exploration", Get(director, "Current").ToString());

            // 쉬는 시간이 끝나면 다음 공연으로.
            Assert.IsTrue((bool)Call(campaign, "DebugSetRemaining", 0f));
            yield return null;
            yield return WaitIdle();
            Assert.AreEqual("Performance", Phase);
            Assert.IsTrue(Loaded("DummyPerformance"));
            Assert.IsFalse(Loaded("DummyExploration"));
            Assert.IsTrue(Loaded("SpaceLink"));
            Assert.LessOrEqual(maxSceneCount, 2, "전환 중에도 공간 씬은 하나만(이전 공간을 닫고 다음을 붙인다, EventSystem 겹침 방지)");
        }
    }
}
