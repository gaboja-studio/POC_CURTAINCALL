using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.SpaceLink.Tests
{
    // runtime은 Assembly-CSharp에 있으므로 기존 SpaceLinkPlayTests와 같이 reflection으로 접근한다.
    static class PerformanceLinkReflection
    {
        internal const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        internal static object Get(object target, string name) => target.GetType().GetProperty(name, Members) is PropertyInfo p
            ? p.GetValue(target) : target.GetType().GetField(name, Members).GetValue(target);
        internal static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Members);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, Members).SetValue(target, value);
        }
        internal static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Members)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        internal static object StaticGet(string type, string name) => GameType(type).GetProperty(name).GetValue(null);
        internal static object StaticCall(string type, string name, params object[] args) => GameType(type)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
        internal static Component Singleton(string type) => StaticGet(type, "Instance") as Component;
        internal static object EnumValue(string type, string name) => Enum.Parse(GameType(type), name);
        internal static Component InScene(Scene scene, string type)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren(GameType(type), true);
                if (found != null) return found;
            }
            return null;
        }
        internal static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), reason + " timed out");
        }
    }

    public sealed class PerformanceRouteTests
    {
        ScriptableObject settingsAsset;
        object settings;
        const string Route = "CurtainCall.SpaceLink.SpaceRoute";
        const string Space = "CurtainCall.SpaceLink.SpaceId";

        [SetUp]
        public void SetUp()
        {
            settingsAsset = ScriptableObject.CreateInstance(PerformanceLinkReflection.GameType("CurtainCall.Settings.BaseGameSettings"));
            settings = PerformanceLinkReflection.Get(settingsAsset, "SpaceTransition");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settingsAsset);

        string Resolve(string space, string trick = null) => (string)PerformanceLinkReflection.StaticCall(Route, "SceneName",
            PerformanceLinkReflection.EnumValue(Space, space), settings, trick); // optional 인수도 Invoke에는 3개를 넘긴다.

        void Set(string field, string value) => PerformanceLinkReflection.Set(settings, field, value);

        void AssertFound(string scene, string expected)
        {
            object[] args = { scene, settings, null };
            Assert.IsTrue((bool)PerformanceLinkReflection.StaticCall(Route, "TryFind", args), scene);
            Assert.AreEqual(expected, args[2].ToString(), scene);
        }

        [Test]
        public void NewSettings_SelectActualPerformanceScenes()
        {
            Assert.AreEqual("Tightrope", Resolve("Performance", "Tightrope"));
            Assert.AreEqual("HumanCannon", Resolve("Performance", "HumanCannon"));
        }

        [Test]
        public void EmptyTrickMappingsKeepLegacyPerformanceFallback()
        {
            Set("tightropeScene", "");
            Set("humanCannonScene", "");
            Assert.AreEqual("", PerformanceLinkReflection.Get(settings, "TightropeScene"));
            Assert.AreEqual("", PerformanceLinkReflection.Get(settings, "HumanCannonScene"));
            string fallback = (string)PerformanceLinkReflection.Get(settings, "PerformanceScene");
            foreach (string trick in new[] { null, "", "Tightrope", "HumanCannon", "Unknown" })
                Assert.AreEqual(fallback, Resolve("Performance", trick));
            var parameter = PerformanceLinkReflection.GameType(Route).GetMethod("SceneName").GetParameters()[2];
            Assert.IsTrue(parameter.IsOptional, "기존 2인자 C# 호출을 유지하는 optional 인수");
        }

        [Test]
        public void BothTricksResolveIndependently_UnknownAndBlankMappingsFallback()
        {
            Set("performanceScene", "Fallback");
            Set("tightropeScene", "Tightrope");
            Set("humanCannonScene", "HumanCannon");
            Assert.AreEqual("Tightrope", Resolve("Performance", "Tightrope"));
            Assert.AreEqual("HumanCannon", Resolve("Performance", "HumanCannon"));
            foreach (string trick in new[] { null, "", "Unknown", "tightrope" })
                Assert.AreEqual("Fallback", Resolve("Performance", trick));
            foreach (string blank in new[] { null, "", " \t" })
            {
                Set("tightropeScene", blank);
                Assert.AreEqual("Fallback", Resolve("Performance", "Tightrope"));
                Assert.AreEqual("HumanCannon", Resolve("Performance", "HumanCannon"));
            }
            Set("tightropeScene", "Tightrope");
            foreach (string blank in new[] { null, "", " \t" })
            {
                Set("humanCannonScene", blank);
                Assert.AreEqual("Fallback", Resolve("Performance", "HumanCannon"));
                Assert.AreEqual("Tightrope", Resolve("Performance", "Tightrope"));
            }
            foreach (string space in new[] { "Hub", "WaitingRoom", "Exploration", "EscapeRoute" })
                Assert.AreEqual(Resolve(space), Resolve(space, "Tightrope"), "공연 외 장소는 묘기에 영향받지 않는다.");
        }

        [Test]
        public void SavedSettings_SelectEnabledActualScenesWithoutOverrides()
        {
            var asset = Resources.Load<ScriptableObject>("Settings/GameSettings/BaseGameSettings");
            Assert.IsNotNull(asset);
            var saved = PerformanceLinkReflection.Get(asset, "SpaceTransition");
            foreach (var mapping in new[] { (Trick: "Tightrope", Scene: "Tightrope"), (Trick: "HumanCannon", Scene: "HumanCannon") })
            {
                string scene = (string)PerformanceLinkReflection.StaticCall(Route, "SceneName",
                    PerformanceLinkReflection.EnumValue(Space, "Performance"), saved, mapping.Trick);
                Assert.AreEqual(mapping.Scene, scene, "저장된 씬 이름에 따옴표나 공백이 섞이면 안 된다.");
                Assert.IsTrue(UnityEditor.EditorBuildSettings.scenes.Any(s => s.enabled
                    && System.IO.Path.GetFileNameWithoutExtension(s.path) == scene), "빌드 목록에 활성화된 실제 공연 씬: " + scene);
            }
        }

        [Test]
        public void TryFindRecognizesFallbackAndBothPerformanceScenes_ForAdditiveCleanup()
        {
            // 기본 대기실/탐색은 같은 실제 씬을 공유한다. 이 순수 검사는 장소별 독립 이름으로 역매핑을 확인한다.
            Set("waitingRoomScene", "RouteWaitingRoom");
            Set("explorationScene", "RouteExploration");
            Set("performanceScene", "Fallback");
            Set("tightropeScene", "Tightrope");
            Set("humanCannonScene", "HumanCannon");
            foreach (string scene in new[] { "Fallback", "Tightrope", "HumanCannon" }) AssertFound(scene, "Performance");
            foreach (string space in new[] { "Hub", "WaitingRoom", "Exploration", "EscapeRoute" }) AssertFound(Resolve(space), space);
            foreach (string scene in new[] { null, "", "NotASpace" })
            {
                object[] args = { scene, settings, null };
                Assert.IsFalse((bool)PerformanceLinkReflection.StaticCall(Route, "TryFind", args));
            }
        }
    }

    /// <summary>
    /// 사용자 지정 저장씬 + 실제 LAN 호스트 1명의 자동 회귀. 실제 키 입력·2인 동기화 검증이 아니다.
    /// 최소 인원/DevMode/저장 에셋은 바꾸지 않는다. 세션 상태·RunState·순간이동은 테스트 전용 합성 입력이다.
    /// </summary>
    public sealed class PerformanceLinkPlayTests
    {
        const string TightropePath = "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity";
        const string TightropeScene = "Tightrope";
        const string CannonScene = "HumanCannon";
        const string CommonUiType = "CurtainCall.UI.SceneIntegration.CommonPerformanceUI";
        const string BindingType = "CurtainCall.UI.SceneIntegration.PerformanceSceneBinding";
        const string CommonUiPath = "Assets/Scenes/UI/CommonPerformanceUI.unity";
        const string EndingType = "CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation";
        const string SessionType = "CurtainCall.Network.Session.NetworkSessionManager";
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        const string CampaignType = "CurtainCall.Campaign.CampaignRun";
        const string RewardsType = "CurtainCall.Reward.PerformanceRewards";
        const string DirectorType = "CurtainCall.SpaceLink.SpaceLinkDirector";
        const string EntryType = "CurtainCall.SpaceLink.CampaignPerformanceEntry";
        const string PracticeBotType = "CurtainCall.HumanCannon.HumanCannonPracticeBot";
        const string AdapterType = "CurtainCall.SpaceLink.CampaignTightropeAdapter";
        const string RunType = "CurtainCall.Tightrope.TightropeRun";
        const string CourseType = "CurtainCall.Tightrope.TightropeCourse";
        const string GameStateType = "CurtainCall.Network.Session.GameSessionState";
        Component session, network, player, mover, run, course, campaign, rewards, entry, adapter, director;
        object host;
        int restartCount, reviveCount, resultCount;
        bool advanceBeforeSettlement;
        Scene syntheticHub;
        readonly List<Action> restore = new();
        readonly List<(EventInfo Event, object Target, Delegate Handler)> subscriptions = new();

        static object Get(object target, string name) => PerformanceLinkReflection.Get(target, name);
        static void Set(object target, string name, object value) => PerformanceLinkReflection.Set(target, name, value);
        static object Call(object target, string name, params object[] args) => PerformanceLinkReflection.Call(target, name, args);
        static Component Singleton(string type) => PerformanceLinkReflection.Singleton(type);
        static object Ensure(string type) => PerformanceLinkReflection.StaticCall(type, "EnsureExists");
        static object EnumValue(string type, string name) => PerformanceLinkReflection.EnumValue(type, name);
        static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string reason) => PerformanceLinkReflection.WaitUntil(predicate, seconds, reason);
        object SpaceSettings => Get(PerformanceLinkReflection.StaticGet("CurtainCall.Settings.GameSettings", "Base"), "SpaceTransition");
        string Phase => Get(Get(campaign, "State"), "Phase").ToString();
        object Results => Get(run, "Results");
        int Round => (int)Get(run, "Round");
        float SettlementWait => Mathf.Max((float)PerformanceLinkReflection.StaticCall(
            "CurtainCall.Settings.PerformanceEndingSettings", "HoldFor", "Tightrope"),
            (float)Get(Get(PerformanceLinkReflection.StaticGet("CurtainCall.Settings.GameSettings", "Base"), "Run"), "FailedPerformanceHold")) + 5f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            restartCount = reviveCount = resultCount = 0;
            advanceBeforeSettlement = false;
            syntheticHub = default;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                TightropePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            session = (Component)Ensure(SessionType);
            network = (Component)Get(session, "NetworkManager");
            var hosting = (Task<bool>)Call(session, "HostLanAsync", Type.Missing);
            yield return WaitUntil(() => hosting.IsCompleted, 10f, "LAN 호스트 시작");
            Assert.IsTrue(hosting.GetAwaiter().GetResult());
            yield return WaitUntil(() =>
            {
                player = PerformanceLinkReflection.StaticGet(PlayerType, "Local") as Component;
                return player != null && (bool)Get(player, "IsSpawned") && (int)Get(player, "Slot") >= 0;
            }, 10f, "실제 호스트 NetworkPlayer spawn/자리 배정");
            var scene = SceneManager.GetSceneByName(TightropeScene);
            run = PerformanceLinkReflection.InScene(scene, RunType);
            course = PerformanceLinkReflection.InScene(scene, CourseType);
            mover = player.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerMover"));
            Assert.IsNotNull(run); Assert.IsNotNull(course); Assert.IsNotNull(mover);
            foreach (string type in new[] { "CurtainCall.Player.PlayerInputReader", "CurtainCall.Player.PlayerController" })
            {
                var behaviour = player.GetComponent(PerformanceLinkReflection.GameType(type)) as Behaviour;
                if (behaviour != null) behaviour.enabled = false;
            }
            // 위치로 판정 경계를 준비하며 실제 이동 입력/물리 완주를 흉내내지 않는다.
            Set(mover, "Simulated", false);
            var balance = player.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerBalance"));
            if (balance != null) Call(balance, "SetBalanceActive", false);
            Override(SpaceSettings, "tightropeScene", TightropeScene);
            Override(SpaceSettings, "humanCannonScene", CannonScene);
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString());
            Assert.IsFalse((bool)Get(run, "ExternallyManaged"), "저장 씬의 독립 실행 기본 정책");
            Subscribe(PerformanceLinkReflection.GameType(PlayerType), null, "Restarted", nameof(Restarted));
            Subscribe(PerformanceLinkReflection.GameType(PlayerType), null, "Revived", nameof(Revived));
            yield return null; // Run/Results의 실제 세션 연결 Update가 끝난 뒤 테스트한다.
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in subscriptions) item.Event.RemoveEventHandler(item.Target, item.Handler);
            subscriptions.Clear();
            // 종료 중 캠페인 이벤트가 추가 씬 전환을 시작하지 않도록 테스트가 만든 Director부터 정리한다.
            if (director != null) Object.Destroy(director.gameObject);
            yield return null;
            Task leave = null;
            Exception cleanupError = null;
            try { if (session != null) leave = (Task)Call(session, "LeaveAsync"); }
            catch (Exception exception) { cleanupError = exception; }
            float deadline = Time.realtimeSinceStartup + 5f;
            while (leave != null && !leave.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            try { if (network != null && (bool)Get(network, "IsListening")) Call(network, "Shutdown", Type.Missing); }
            catch (Exception exception) { cleanupError ??= exception; }
            yield return null;
            if (entry != null) Object.Destroy(entry.gameObject);
            yield return null;
            foreach (var action in Enumerable.Reverse(restore)) action();
            restore.Clear();
            foreach (var go in new[] { Singleton(RewardsType), Singleton(CampaignType), session, network }
                .Where(c => c != null).Select(c => c.gameObject).Distinct()) Object.Destroy(go);
            yield return null;
            if (syntheticHub.IsValid() && syntheticHub.isLoaded && SceneManager.sceneCount > 1)
                yield return SceneManager.UnloadSceneAsync(syntheticHub);
            session = network = player = mover = run = course = campaign = rewards = entry = adapter = director = null;
            host = null;
            if (cleanupError != null) throw cleanupError;
            if (leave != null && leave.IsFaulted) throw leave.Exception;
        }

        [UnityTest]
        public IEnumerator Standalone_ActualSceneBridgeSettlesOnce_WithoutSessionEndOrTrickRetry()
        {
            // 실제 씬은 SpaceLink 진입부가 없어도 저장된 CampaignBridge가 공용 런을 따른다.
            // 최소 인원 시작/2인 입력은 합성하지 않고, 기존처럼 호스트 1명의 런 상태만 준비한다.
            yield return CreateSyntheticCampaign();
            Set(session, "<GameState>k__BackingField", EnumValue(GameStateType, "Playing"));
            Assert.IsTrue((bool)Get(run, "UsesCampaign"));
            Assert.IsNull(PerformanceLinkReflection.InScene(SceneManager.GetSceneByName(TightropeScene), EntryType));
            rewards = Singleton(RewardsType);
            Subscribe(rewards.GetType(), rewards, "ResultConfirmed", nameof(ResultConfirmed));
            Assert.IsTrue((bool)Call(campaign, "EndIntro"));
            yield return WaitUntil(() => Get(run, "State").ToString() == "Running"
                && (bool)Get(Get(rewards, "State"), "IsOpen"), 5f, "실제 씬 Bridge 공연 시작");
            yield return null; // 공용 시작 배치·출발 확인 후 도착 경계를 검사한다.
            Assert.Greater(Round, 0);
            Assert.AreEqual(0, restartCount, "공용 Bridge는 전원 복구/재시작을 부르지 않는다.");
            Assert.IsTrue((bool)Get(Results, "IsAttemptOpen"));
            MoveToFinish();
            yield return WaitUntil(() => Get(run, "State").ToString() == "Succeeded"
                && Get(rewards, "Latest") != null, 5f, "실제 코스 도착과 공용 정산");
            object result = Get(Results, "Latest");
            Assert.IsNotNull(result);
            Assert.IsTrue((bool)Get(result, "Succeeded"));
            Assert.IsTrue((bool)Get(Get(rewards, "Latest"), "Succeeded"));
            string attempt = (string)Get(result, "AttemptId");
            int round = Round;
            long balance = (long)Get(rewards, "Balance");
            Assert.IsFalse((bool)Call(run, "RestartByHost"));
            Assert.IsFalse((bool)Call(run, "RetryByHost", "old-attempt", round));
            Assert.IsFalse((bool)Call(run, "RetryByHost", attempt, round), "실제 씬에서는 최신 결과도 묘기 단독 재도전할 수 없다.");
            Assert.IsFalse((bool)Call(rewards, "FinishPerformance", true, "duplicate", null));
            yield return WaitUntil(() => Phase == "RestTime", SettlementWait, "결과 연출 뒤 다음 시간대");
            Assert.AreEqual("Playing", Get(session, "GameState").ToString(), "공용 공연 성공은 방을 끝내지 않는다.");
            Assert.AreEqual(1, resultCount);
            Assert.AreEqual(balance, Get(rewards, "Balance"));
            Assert.AreEqual(round, Round);
            Assert.AreEqual(attempt, Get(Results, "AttemptId"));
            Assert.IsFalse((bool)Get(run, "ExternallyManaged"), "SpaceLink 없이 저장된 Bridge만 사용했다.");
        }

        [UnityTest]
        public IEnumerator Managed_KeepsActualSharedHudAndLocalBalanceVisible()
        {
            var scene = SceneManager.GetSceneByName(TightropeScene);
            var binding = (Behaviour)PerformanceLinkReflection.InScene(scene, BindingType);
            Assert.IsNotNull(binding, "실제 공연 씬의 UI 연결부");
            Assert.IsTrue(binding.isActiveAndEnabled);
            yield return WaitForSharedUi(binding);
            var common = (Component)PerformanceLinkReflection.StaticGet(CommonUiType, "Current");
            var hud = (Component)Get(common, "Hud");
            var localBalance = (Component)Get(binding, "LocalBalance");
            Assert.IsNotNull(localBalance, "P1은 공통 UI가 아니라 실제 공간 씬 소유다.");
            Assert.AreEqual(scene, localBalance.gameObject.scene);
            yield return PrepareManagedIntro();
            yield return StartManagedPerformance();
            // 플랫폼에서는 P1을 숨기는 것이 정상이다. 실제 줄 위 상태에서 게이지 표시를 검사한다.
            var rope = (Vector3)Call(course, "GetLanePosition", (int)Get(player, "Slot"), 1f);
            Call(mover, "SetStartPose", rope, Quaternion.identity, true);
            Set(mover, "Simulated", true); // 실제 접지 판정으로 CourseControlSwitcher가 P1을 켜게 한다.
            yield return WaitUntil(() => (bool)Get(localBalance, "HasValidData")
                && ((CanvasGroup)Get(localBalance, "visibility")).alpha > 0f,
                5f, "실제 줄 위 로컬 균형 게이지");
            Assert.IsTrue(binding.isActiveAndEnabled, "연결 진입도 공간 UI 연결부를 숨기지 않는다.");
            Assert.AreSame(hud, Get(binding, "Hud"), "연결 진입 전후 공통 HUD 소유권 유지");
            var timer = (Component)Get(hud, "Timer");
            Assert.IsTrue(timer.gameObject.activeInHierarchy);
            Assert.IsTrue((bool)Get(timer, "HasValidData"), "타이머는 실제 공연 데이터를 표시한다.");
            Assert.IsTrue(timer.GetComponentInParent<Canvas>().isActiveAndEnabled);
            var progress = (Component)Get(hud, "Progress");
            var solo = progress.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.UI.SceneIntegration.SoloPerformanceProgress"));
            Assert.IsNotNull(solo, "실제 1인 명부의 진행 표시 연결부");
            Assert.IsTrue((bool)Get(solo, "HasValidData"));
            Assert.IsFalse((bool)Get(progress, "HasValidData"), "2~4인 원본 대신 1인 표시가 소유한다.");
            Assert.IsTrue(((Component)Get(solo, "playerRoot")).gameObject.activeInHierarchy);
            StringAssert.Contains("m", (string)Get(Get(solo, "remainingText"), "text"));
            Assert.IsTrue(progress.GetComponentInParent<Canvas>().isActiveAndEnabled);
            var cheer = (Component)Get(common, "Cheer");
            Assert.IsTrue(cheer.gameObject.activeInHierarchy, "공용 호응 게이지 표시");
            Assert.IsTrue((bool)Get(cheer, "HasData"));
            Assert.AreEqual(1, Get(cheer, "ParticipantCount"), "실제 호스트 명부의 호응 표시");
            Assert.IsTrue(localBalance.gameObject.activeInHierarchy);
            Assert.IsTrue(localBalance.GetComponentInParent<Canvas>().isActiveAndEnabled);
            Assert.IsFalse((bool)Call(run, "RestartByHost"), "실제 UI 연결이 관리 공연의 전체 재시작 보호를 해제하면 안 된다.");
        }

        [UnityTest]
        public IEnumerator Standalone_CannonKeepsGroundSeatAndFlightCameraPolicy()
        {
            var leaving = (Task)Call(session, "LeaveAsync");
            yield return WaitUntil(() => leaving.IsCompleted, 5f, "줄타기 테스트 세션 종료");
            leaving.GetAwaiter().GetResult();
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var hosting = (Task<bool>)Call(session, "HostLanAsync", Type.Missing);
            yield return WaitUntil(() => hosting.IsCompleted, 10f, "대포 독립 LAN 시작");
            Assert.IsTrue(hosting.GetAwaiter().GetResult());
            yield return AssertCannonCameraPolicy();
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString());
            Assert.IsNull(PerformanceLinkReflection.InScene(SceneManager.GetSceneByName(CannonScene), EntryType));
            var practiceBot = (Behaviour)PerformanceLinkReflection.InScene(SceneManager.GetSceneByName(CannonScene), PracticeBotType);
            Assert.IsTrue(practiceBot != null && practiceBot.enabled, "독립 실행은 에디터 연습 봇을 유지한다.");
        }

        [UnityTest]
        public IEnumerator Managed_LatePerformanceStart_PreservesInjuryAndSettlesExactlyOnce()
        {
            yield return PrepareManagedIntro();
            Assert.AreEqual("Intro", Phase);
            Assert.IsFalse((bool)Get(adapter, "Started"));
            Assert.AreEqual("Waiting", Get(run, "State").ToString(), "Playing을 늦게 발견해도 공연 시간대 전에는 시작하지 않는다.");
            yield return StartManagedPerformance();
            AssertBody();
            Assert.AreEqual(0, restartCount, "RestoreAll 설정이어도 연결 공연은 NetworkPlayer 전원 복구를 부르지 않는다.");
            Assert.AreEqual(0, reviveCount);
            long before = (long)Get(rewards, "Balance");
            MoveToFinish();
            yield return WaitUntil(() => Get(rewards, "Latest") != null && Phase == "RestTime", SettlementWait, "공용 정산·결과 연출 후 다음 시간대");
            object result = Get(rewards, "Latest");
            object tightropeResult = Get(Results, "Latest");
            Assert.AreEqual(1, resultCount);
            Assert.IsTrue((bool)Get(result, "Succeeded"));
            Assert.AreEqual("Tightrope", Get(Get(result, "Performance"), "TrickId"));
            Assert.AreEqual(before + (long)Get(result, "Income"), Get(rewards, "Balance"));
            var people = ((IEnumerable)Get(result, "Participants")).Cast<object>().ToArray();
            Assert.AreEqual(1, people.Length, "실제 LAN 호스트 명부만 정산");
            Assert.AreEqual(3, Get(people[0], "StartingLostCount"));
            Assert.AreEqual(3, Get(people[0], "EndingLostCount"));
            Assert.AreEqual("Playing", Get(session, "GameState").ToString(), "연결 공연 성공은 방을 끝내지 않는다.");
            int round = Round;
            long balance = (long)Get(rewards, "Balance");
            Call(adapter, "HandleResult", tightropeResult); // 같은 확정 결과의 지연/중복 통지를 합성한다.
            Call(adapter, "LateUpdate");
            Assert.IsFalse((bool)Call(rewards, "FinishPerformance", true, "duplicate", null));
            Assert.IsFalse((bool)Call(run, "RestartByHost"));
            Assert.IsFalse((bool)Call(run, "RetryByHost", Get(tightropeResult, "AttemptId"), round));
            yield return null;
            Assert.AreEqual(1, resultCount);
            Assert.AreEqual(balance, Get(rewards, "Balance"));
            Assert.AreEqual(round, Round);
            Assert.AreSame(result, Get(rewards, "Latest"));
            AssertExternalJudgementsBlocked();
            AssertBody();
        }

        [UnityTest]
        public IEnumerator Managed_TimeoutSettlesFailure_WithoutSessionEndOrAutomaticRetry()
        {
            yield return PrepareManagedIntro();
            yield return StartManagedPerformance();
            int round = Round;
            // 긴 실제 대기 대신 메모리의 제한시간만 임시 축소한다(에셋 저장 없음). 판정/정산은 실제 프레임으로 실행한다.
            object timing = Get(PerformanceLinkReflection.StaticGet("CurtainCall.Settings.GameSettings", "Tightrope"), "Run");
            Override(timing, "timeLimit", 0f);
            Set(run, "runStartedAt", Get(run, "Clock"));
            yield return WaitUntil(() => Get(rewards, "Latest") != null && Phase == "RestTime", SettlementWait, "실패 정산·결과 연출");
            Assert.AreEqual("Failed", Get(run, "State").ToString());
            Assert.IsFalse((bool)Get(Get(rewards, "Latest"), "Succeeded"));
            Assert.AreEqual(1, resultCount);
            Assert.AreEqual("Playing", Get(session, "GameState").ToString());
            Set(run, "restartAt", Time.time - 1f);
            yield return null;
            yield return null;
            Assert.AreEqual(round, Round, "재시작 예정 시각이 지나도 관리 모드는 재시작하지 않는다.");
            Assert.AreEqual("Failed", Get(run, "State").ToString());
            Assert.IsFalse((bool)Call(run, "RestartByHost"));
            Assert.AreEqual(0, restartCount);
            Assert.AreEqual(0, reviveCount);
            AssertBody();
        }

        [UnityTest]
        public IEnumerator Managed_FullPerformanceIdentityRejectsEachStaleCoordinate()
        {
            yield return PrepareManagedIntro();
            yield return StartManagedPerformance();
            object original = Get(campaign, "State");
            var changes = new (string Field, object Value)[]
            {
                ("Generation", (int)Get(original, "Generation") + 1),
                ("Day", (int)Get(original, "Day") + 1),
                ("PerformanceIndex", (int)Get(original, "PerformanceIndex") + 1),
                ("TrickId", "HumanCannon"),
                ("Phase", EnumValue("CurtainCall.Campaign.CampaignPhase", "RestTime")),
            };
            try
            {
                foreach (var change in changes)
                {
                    // 이벤트/프레임을 실행하지 않고 매번 독립 복사본의 식별자 하나만 바꾸는 합성 경계 검사다.
                    Set(campaign, "state", original);
                    object changed = Get(campaign, "State");
                    Set(changed, change.Field, change.Value);
                    Set(campaign, "state", changed);
                    Assert.IsFalse((bool)Call(adapter, "SamePerformance"), change.Field);
                }
            }
            finally { Set(campaign, "state", original); }
            Assert.IsTrue((bool)Call(adapter, "SamePerformance"));
            Assert.AreEqual(0, resultCount);
        }

        [UnityTest]
        public IEnumerator Managed_StoppedWhileRunStateIsRunning_BlocksDamageAndArrival()
        {
            yield return PrepareManagedIntro();
            yield return StartManagedPerformance();
            Assert.AreEqual("Running", Get(run, "State").ToString());
            Call(run, "StopExternalPerformance");
            AssertExternalJudgementsBlocked();
            yield return null;
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.AreEqual("Running", Get(run, "State").ToString(), "중단은 실행 상태를 가장하지 않고 판정만 잠근다.");
            Assert.AreEqual(0, resultCount);
            AssertBody();
        }

        [UnityTest]
        public IEnumerator Managed_CampaignChangesInsideResultEvent_DiscardsOldSettlement()
        {
            yield return PrepareManagedIntro();
            yield return StartManagedPerformance();
            advanceBeforeSettlement = true;
            Subscribe(Results.GetType(), Results, "ResultConfirmed", nameof(AdvanceCampaignAtResult));
            MoveToFinish();
            yield return WaitUntil(() => Get(run, "State").ToString() == "Succeeded", 5f, "묘기 확정 결과");
            yield return null;
            Assert.AreEqual("RestTime", Phase, "확정 콜백에서 시간대 변경을 합성했다.");
            Assert.AreEqual(0, resultCount, "ResultConfirmed와 LateUpdate 사이에 지난 공연 결과는 공용 정산하지 않는다.");
            Assert.IsNull(Get(rewards, "Latest"));
            Assert.IsTrue((bool)Get(run, "ExternallyManaged"));
            Assert.IsFalse((bool)Call(run, "RestartByHost"));
            Call(adapter, "HandleResult", Get(Results, "Latest"));
            Call(adapter, "LateUpdate");
            Assert.AreEqual(0, resultCount);
            Assert.AreEqual("Playing", Get(session, "GameState").ToString());
        }

        [UnityTest]
        public IEnumerator SamePerformanceSpace_SingleLoadsDifferentConfiguredScene() => SwapPerformanceScene(false);

        [UnityTest]
        public IEnumerator SamePerformanceSpace_AdditiveRemovesPreviousConfiguredScene() => SwapPerformanceScene(true);

        /// <summary>2026-10-19 사용자 재현: 외팔 1인이 인간대포 함정에 닿아 사망 → 사망 장면 없이 바로 대기실. 실패 유지 시간 동안 묘기 씬을 비춘다.</summary>
        [UnityTest]
        public IEnumerator Managed_CannonLastSurvivorDeath_HoldsPerformanceSceneBeforeLeaving()
        {
            yield return SwapPerformanceScene(false);
            rewards = Singleton(RewardsType);
            Assert.IsNotNull(rewards);
            Subscribe(rewards.GetType(), rewards, "ResultConfirmed", nameof(ResultConfirmed));
            yield return WaitUntil(() => Get(rewards, "State") is object s && (bool)Get(s, "IsOpen"), 5f, "인간대포 공용 정산 시작");
            float hold = Mathf.Max((float)PerformanceLinkReflection.StaticCall(
                "CurtainCall.Settings.PerformanceEndingSettings", "HoldFor", "HumanCannon"),
                (float)Get(Get(PerformanceLinkReflection.StaticGet("CurtainCall.Settings.GameSettings", "Base"), "Run"), "FailedPerformanceHold"));
            Assert.Greater(hold, 0f);
            yield return null;
            yield return null; // 기존 진입 가드가 출발점 배치를 확인하게 한다.
            Call(mover, "SetStartPose", new Vector3(0f, -500f, 0f), Quaternion.identity, true);
            Call(player, "ServerCut", 4); // 낙하 위치에서 공통 절단 사망을 합성한다. 실제 함정 입력은 아니다.
            float diedAt = Time.unscaledTime;
            Assert.AreEqual("Fallen", Get(player, "State").ToString());
            var result = Get(rewards, "Latest");
            Assert.IsNotNull(result, "생존자 0명 실패는 바로 확정");
            Assert.AreEqual("NoSurvivors", Get(result, "EndReason"));
            Assert.AreEqual("Performance", Phase, "실패 직후에는 시간대를 넘기지 않는다.");
            yield return new WaitForSecondsRealtime(hold * 0.5f);
            Assert.AreEqual("Performance", Phase, "유지 시간 동안 묘기 씬 유지");
            Assert.IsTrue(SceneManager.GetSceneByName(CannonScene).isLoaded);
            Assert.AreEqual("Fallen", Get(player, "State").ToString(), "유지 중 다음 시간대 부활이 먼저 오지 않는다.");
            yield return WaitUntil(() => Phase != "Performance", hold + 2f, "유지 시간 뒤 다음 시간대");
            Assert.GreaterOrEqual(Time.unscaledTime - diedAt, hold - 0.05f);
            Assert.AreEqual(1, resultCount, "유지 중 중복 정산 없음");
            yield return WaitUntil(() => !(bool)Get(director, "IsTransitioning")
                && Get(director, "Current").ToString() == "WaitingRoom", 20f, "밤 정비 대기실 복귀");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual("Night", Phase);
            Assert.IsFalse(SceneManager.GetSceneByName(CannonScene).isLoaded);
            Assert.AreEqual("Normal", Get(player, "State").ToString(), "부활 뒤 옛 공연 씬에서 다시 죽으면 안 된다.");
            Assert.AreEqual(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerCondition")
                .GetField("OneArmParts").GetValue(null), Get(player, "LostParts"));
            var balance = player.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerBalance"));
            Assert.IsFalse((bool)Get(balance, "HasFallen"));
            Assert.IsTrue((bool)Get(mover, "ControlEnabled"));
            var spectatorType = PerformanceLinkReflection.GameType("CurtainCall.Cameras.CameraSpectator");
            Assert.IsNotEmpty(Object.FindObjectsByType(spectatorType, FindObjectsSortMode.None), "실제 관전 카메라가 있어야 한다.");
            Assert.IsFalse((bool)PerformanceLinkReflection.StaticGet("CurtainCall.Cameras.CameraSpectator", "IsSpectating"));
        }

        IEnumerator SwapPerformanceScene(bool additive)
        {
            yield return CreateSyntheticCampaign();
            Assert.IsTrue((bool)Call(host, "EndIntro"));
            Call(campaign, "Apply", Get(host, "Snapshot"));
            Set(session, "<GameState>k__BackingField", EnumValue(GameStateType, "Playing"));
            Call(run, "SetExternallyManaged", true);
            Override(SpaceSettings, "mode", EnumValue("CurtainCall.Settings.SpaceTransitionMode", additive ? "Additive" : "Single"));
            if (additive)
            {
                // 테스트 전용 코어를 남겨, 현재 공연 씬을 먼저 닫는 실제 Additive 경로를 검사한다.
                syntheticHub = SceneManager.CreateScene("PerformanceLinkSyntheticHub");
                Override(SpaceSettings, "hubScene", syntheticHub.name);
            }
            director = (Component)Ensure(DirectorType);
            yield return null;
            Assert.AreEqual("Performance", Get(director, "Current").ToString());
            object next = Get(host, "Snapshot");
            Set(next, "PerformanceIndex", 1);
            Set(next, "TrickId", "HumanCannon");
            Set(host, "s", next);
            Call(campaign, "Apply", next); // 같은 SpaceId의 다음 공연을 합성한다.
            yield return WaitUntil(() => !(bool)Get(director, "IsTransitioning")
                && SceneManager.GetSceneByName(CannonScene).isLoaded, 20f, "같은 장소의 다른 공연 씬 전환");
            Assert.IsFalse(SceneManager.GetSceneByName(TightropeScene).isLoaded, "TryFind가 이전 실제 공연 씬도 제거 대상으로 인식");
            Assert.AreEqual(CannonScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual("Performance", Get(director, "Current").ToString());
            Assert.IsTrue((bool)Get(session, "IsHost"));
            Assert.AreSame(campaign, Singleton(CampaignType));
            Assert.IsNotNull(PerformanceLinkReflection.InScene(SceneManager.GetSceneByName(CannonScene), EntryType));
            if (additive) Assert.IsTrue(syntheticHub.isLoaded);
            var binding = (Behaviour)PerformanceLinkReflection.InScene(SceneManager.GetSceneByName(CannonScene), BindingType);
            Assert.IsNotNull(binding);
            yield return WaitForSharedUi(binding);
            // 실제 공연이 추가 로드하는 공통 UI·배경만 허용하고 공간 씬은 정확히 비교한다.
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var supporting = new[] { CommonUiPath, "Assets/Scenes/UI/PerformanceTransitionUI.unity",
                "Assets/Scenes/UI/CommonMenuUI.unity", "Assets/Scenes/UI/CommonInteractionUI.unity",
                "Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity" };
            CollectionAssert.AreEquivalent(additive ? new[] { CannonScene, syntheticHub.name } : new[] { CannonScene },
                loaded.Where(s => !supporting.Contains(s.path)).Select(s => s.name).ToArray(),
                "이전 공간이나 불필요한 씬을 남기면 안 된다.");
            Assert.AreEqual(1, loaded.Count(s => s.path == CommonUiPath), "공통 HUD 씬 중복 없음");
            foreach (string path in supporting) Assert.LessOrEqual(loaded.Count(s => s.path == path), 1, path);
            var practiceBot = (Behaviour)PerformanceLinkReflection.InScene(SceneManager.GetSceneByName(CannonScene), PracticeBotType);
            Assert.IsTrue(practiceBot == null || !practiceBot.enabled, "연결 공연은 인간대포 연습 봇 패널을 숨긴다.");
            yield return AssertCannonCameraPolicy();
        }

        IEnumerator WaitForSharedUi(Behaviour binding)
        {
            yield return WaitUntil(() => SceneManager.GetSceneByPath(CommonUiPath).isLoaded
                && !(bool)PerformanceLinkReflection.StaticGet("CurtainCall.UI.SceneIntegration.CommonUiSceneLoader", "IsLoading")
                && PerformanceLinkReflection.StaticGet(CommonUiType, "Current") is Component common
                && PerformanceLinkReflection.StaticGet(EndingType, "Current") != null
                && Get(common, "Hud") is Component && Get(binding, "Hud") == Get(common, "Hud"),
                15f, "실제 공간의 공통 UI 로드와 소유권 연결");
            Assert.IsTrue(binding.isActiveAndEnabled);
            Assert.AreEqual(1, Object.FindObjectsByType(PerformanceLinkReflection.GameType(CommonUiType),
                FindObjectsSortMode.None).Length, "공통 UI 관리자 중복 없음");
        }

        IEnumerator AssertCannonCameraPolicy()
        {
            Component pilot = null, camera = null;
            yield return WaitUntil(() =>
            {
                player = PerformanceLinkReflection.StaticGet(PlayerType, "Local") as Component;
                camera = PerformanceLinkReflection.StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current") as Component;
                pilot = player != null ? player.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.HumanCannon.HumanCannonPilot")) : null;
                return pilot != null && camera != null && Get(camera, "LocalPlayer") == player
                    && (bool)Get(camera, "IsFirstPerson");
            }, 10f, "대포 출발 플랫폼의 기존 1인칭");
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.That(Vector3.Distance(player.transform.position, new Vector3(-2.25f, 4.05f, 0f)), Is.LessThan(0.3f));
            Assert.AreEqual(1, Object.FindObjectsByType(PerformanceLinkReflection.GameType("CurtainCall.Cameras.CameraViewDirector"),
                FindObjectsSortMode.None).Cast<Behaviour>().Count(c => c.isActiveAndEnabled), "카메라 관리자 중복 없음");
            mover = player.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerMover"));
            foreach (string type in new[] { "CurtainCall.Player.PlayerInputReader", "CurtainCall.Player.PlayerController" })
                ((Behaviour)player.GetComponent(PerformanceLinkReflection.GameType(type))).enabled = false;
            Call(player.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerBalance")), "SetBalanceActive", false);
            Set(mover, "Simulated", true);
            var system = Object.FindAnyObjectByType(PerformanceLinkReflection.GameType("CurtainCall.HumanCannon.HumanCannonSystem"));
            var station = Call(system, "Station", 6);
            var boarding = (Transform)Get(station, "BoardingPoint");
            Call(mover, "SetStartPose", boarding.position, Quaternion.identity, true);
            // IsTransitioning 해제 직후에도 커튼 열림/키 해제 대기는 남을 수 있다.
            // 준비 전에 보낸 합성 F 입력은 실제 입력 정책상 버려지므로, 막힘·접지가 풀린 뒤 한 번 보낸다.
            yield return WaitUntil(() => !(bool)PerformanceLinkReflection.StaticGet("CurtainCall.Player.InputGate", "IsBlocked")
                && (int)Call(system, "FindBoardableStation", player) == 6, 10f, "커튼 입력 잠금 해제와 실제 대포 탑승 준비");
            object frame = Activator.CreateInstance(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerInputFrame"));
            Set(frame, "InteractTapped", true);
            Call(pilot, "ReadInput", frame); // 입력 프레임 합성: 카메라·탑승 연결 검사이며 실제 F 키 검사가 아니다.
            yield return WaitUntil(() => (bool)Get(camera, "IsFreeThirdPerson"), 3f, "탑승 후 기존 자유 3인칭");
            Assert.AreEqual(6, Get(pilot, "stationIndex"));
            object released = Activator.CreateInstance(frame.GetType());
            Call(pilot, "ReadInput", released); yield return null;
            Call(pilot, "ReadInput", released); yield return null;
            Call(pilot, "ReadInput", frame);
            Assert.IsTrue((bool)Get(pilot, "IsFlying"));
            yield return WaitUntil(() => (bool)Get(camera, "IsFirstPerson"), 2f, "발사 후 기존 1인칭");
            Assert.AreEqual("FirstPerson", Get(Get(camera, "CurrentView"), "Kind").ToString());
        }

        IEnumerator CreateSyntheticCampaign()
        {
            campaign = (Component)Ensure(CampaignType);
            yield return null;
            // StartGame 최소 인원을 우회하는 테스트 전용 RunState. 접속 명부/NetworkPlayer는 LAN 호스트 1명 그대로다.
            host = Activator.CreateInstance(PerformanceLinkReflection.GameType("CurtainCall.Campaign.RunState"));
            Call(host, "StartSession");
            Set(campaign, "host", host);
            Set(campaign, "offlineRun", false);
            Call(campaign, "Apply", Get(host, "Snapshot"));
        }

        IEnumerator PrepareManagedIntro()
        {
            yield return CreateSyntheticCampaign();
            object body = Get(PerformanceLinkReflection.StaticGet("CurtainCall.Settings.GameSettings", "Base"), "BodyDamage");
            Override(body, "restartRecovery", EnumValue("CurtainCall.Settings.BaseGameSettings+BodyRestartRecovery", "RestoreAll"));
            Call(player, "ServerSetOneArm");
            Set(session, "<GameState>k__BackingField", EnumValue(GameStateType, "Playing"));
            // 씬/세션이 이미 존재한 뒤 연결부가 늦게 Playing을 발견하는 순서를 재현한다.
            var scene = SceneManager.GetSceneByName(TightropeScene);
            PerformanceLinkReflection.StaticCall(EntryType, "Prepare", scene);
            PerformanceLinkReflection.StaticCall(EntryType, "Prepare", scene);
            entry = PerformanceLinkReflection.InScene(scene, EntryType);
            Assert.IsNotNull(entry);
            adapter = entry.GetComponent(PerformanceLinkReflection.GameType(AdapterType));
            Assert.IsNotNull(adapter);
            Assert.AreEqual(1, scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren(
                PerformanceLinkReflection.GameType(EntryType), true).Length), "Prepare 중복 호출로 관리자를 늘리지 않는다.");
            rewards = Singleton(RewardsType);
            Assert.IsNotNull(rewards);
            Subscribe(rewards.GetType(), rewards, "ResultConfirmed", nameof(ResultConfirmed));
            yield return null;
            yield return null;
            Assert.IsTrue((bool)Get(run, "ExternallyManaged"));
            Assert.IsFalse((bool)Get(adapter, "Started"));
            Assert.IsFalse((bool)Get(entry, "LocalPlaced"));
            AssertExternalJudgementsBlocked();
            AssertBody();
        }

        IEnumerator StartManagedPerformance()
        {
            Assert.IsTrue((bool)Call(campaign, "EndIntro"));
            yield return WaitUntil(() => (bool)Get(adapter, "Started") && Get(run, "State").ToString() == "Running"
                && (bool)Get(entry, "LocalPlaced") && (bool)Get(mover, "ControlEnabled"),
                5f, "공연 상태/실제 참가자 배치 후 관리 공연 시작과 조작 해제");
            Assert.IsTrue((bool)Get(entry, "LocalPlaced"));
            Assert.IsTrue((bool)Get(mover, "ControlEnabled"));
            Assert.IsTrue((bool)Get(Get(rewards, "State"), "IsOpen"));
        }

        void MoveToFinish()
        {
            var finish = (Pose)Call(course, "GetFinishPose", (int)Get(player, "Slot"));
            Call(mover, "SetStartPose", finish.position, finish.rotation, true);
        }

        void AssertExternalJudgementsBlocked()
        {
            var guard = (Delegate)PerformanceLinkReflection.StaticGet(PlayerType, "ServerCanChangeState");
            Assert.IsNotNull(guard);
            foreach (string state in new[] { "Fallen", "Arrived" })
                Assert.IsFalse((bool)guard.DynamicInvoke(player, EnumValue("CurtainCall.Network.PlayerSync.PlayerState", state)),
                    "관리 공연 준비 전/중단 후 상태 요청 차단: " + state);
            Assert.AreEqual(0, Convert.ToInt32(Call(player, "ServerCut", 1)), "판정 잠금 중에는 부상도 추가하지 않는다.");
            Assert.IsFalse((bool)Call(player, "ServerKill"));
        }

        void AssertBody()
        {
            Assert.AreEqual(13, Convert.ToInt32(Get(player, "LostParts")), "외팔 상태 부상 보존");
            var condition = player.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.Player.PlayerCondition"));
            Assert.AreEqual(13, Convert.ToInt32(Get(condition, "LostParts")));
        }

        void Override(object target, string field, object value)
        {
            object before = Get(target, field);
            restore.Add(() => Set(target, field, before));
            Set(target, field, value);
        }

        void Restarted<T>(T value) => restartCount++;
        void Revived<T>(T value) => reviveCount++;
        void ResultConfirmed<T>(T value) => resultCount++;
        void AdvanceCampaignAtResult<T>(T value)
        {
            if (advanceBeforeSettlement) Assert.IsTrue((bool)Call(campaign, "EndPerformance"));
        }

        void Subscribe(Type owner, object target, string name, string callback)
        {
            var info = owner.GetEvent(name);
            var method = GetType().GetMethod(callback, PerformanceLinkReflection.Members)
                .MakeGenericMethod(info.EventHandlerType.GenericTypeArguments[0]);
            var handler = Delegate.CreateDelegate(info.EventHandlerType, this, method);
            info.AddEventHandler(target, handler);
            subscriptions.Add((info, target, handler));
        }
    }
}
