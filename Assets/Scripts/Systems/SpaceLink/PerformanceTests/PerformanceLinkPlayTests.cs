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
        public void NewSettings_EmptyTrickMappingsKeepLegacyPerformanceFallback()
        {
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
            Set("tightropeScene", "proto_0.0.2");
            Set("humanCannonScene", "HumanCannon");
            Assert.AreEqual("proto_0.0.2", Resolve("Performance", "Tightrope"));
            Assert.AreEqual("HumanCannon", Resolve("Performance", "HumanCannon"));
            foreach (string trick in new[] { null, "", "Unknown", "tightrope" })
                Assert.AreEqual("Fallback", Resolve("Performance", trick));
            foreach (string blank in new[] { null, "", " \t" })
            {
                Set("tightropeScene", blank);
                Assert.AreEqual("Fallback", Resolve("Performance", "Tightrope"));
                Assert.AreEqual("HumanCannon", Resolve("Performance", "HumanCannon"));
            }
            Set("tightropeScene", "proto_0.0.2");
            foreach (string blank in new[] { null, "", " \t" })
            {
                Set("humanCannonScene", blank);
                Assert.AreEqual("Fallback", Resolve("Performance", "HumanCannon"));
                Assert.AreEqual("proto_0.0.2", Resolve("Performance", "Tightrope"));
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
            foreach (var mapping in new[] { (Trick: "Tightrope", Scene: "proto_0.0.2"), (Trick: "HumanCannon", Scene: "HumanCannon") })
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
            Set("performanceScene", "Fallback");
            Set("tightropeScene", "proto_0.0.2");
            Set("humanCannonScene", "HumanCannon");
            foreach (string scene in new[] { "Fallback", "proto_0.0.2", "HumanCannon" }) AssertFound(scene, "Performance");
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
        const string TightropePath = "Assets/Scenes/Prototypes/proto_0.0.2.unity";
        const string TightropeScene = "proto_0.0.2";
        const string CannonScene = "HumanCannon";
        const string SessionType = "CurtainCall.Network.Session.NetworkSessionManager";
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        const string CampaignType = "CurtainCall.Campaign.CampaignRun";
        const string RewardsType = "CurtainCall.Reward.PerformanceRewards";
        const string DirectorType = "CurtainCall.SpaceLink.SpaceLinkDirector";
        const string EntryType = "CurtainCall.SpaceLink.CampaignPerformanceEntry";
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
        public IEnumerator Standalone_SyntheticStartAndFinish_KeepSessionEndAndGuardedRetry()
        {
            // 실제 접속자는 1명이다. 최초 시작 최소인원 검증은 별도 공동 테스트이며 Playing 알림만 합성한다.
            Call(session, "SetGameState", EnumValue(GameStateType, "Playing"));
            yield return WaitUntil(() => Get(run, "State").ToString() == "Running", 5f, "독립 줄타기 시작");
            Assert.Greater(Round, 0);
            Assert.Greater(restartCount, 0, "독립 실행의 기존 전원 재시작 경로");
            Assert.IsTrue((bool)Get(Results, "IsAttemptOpen"));
            MoveToFinish();
            yield return WaitUntil(() => Get(run, "State").ToString() == "Succeeded", 5f, "저장 코스의 호스트 도착 판정");
            Assert.AreEqual("Ended", Get(session, "GameState").ToString(), "독립 성공은 기존처럼 방을 종료한다.");
            object result = Get(Results, "Latest");
            Assert.IsNotNull(result);
            Assert.IsTrue((bool)Get(result, "Succeeded"));
            string attempt = (string)Get(result, "AttemptId");
            int round = Round;
            Assert.IsFalse((bool)Call(run, "RetryByHost", "old-attempt", round));
            // 단독 LAN의 재도전 상태 경계만 검사하기 위한 합성 세션 카운트다. 가짜 NetworkPlayer는 만들지 않는다.
            int count = (int)Get(session, "PlayerCount");
            bool retried;
            try
            {
                Set(session, "<PlayerCount>k__BackingField", (int)Get(session, "MinPlayers"));
                retried = (bool)Call(run, "RetryByHost", attempt, round);
            }
            finally { Set(session, "<PlayerCount>k__BackingField", count); }
            Assert.IsTrue(retried);
            Assert.AreEqual("Running", Get(run, "State").ToString());
            Assert.AreEqual("Playing", Get(session, "GameState").ToString());
            Assert.AreEqual(round + 1, Round);
            Assert.AreNotEqual(attempt, Get(Results, "AttemptId"));
            Assert.IsFalse((bool)Call(run, "RetryByHost", attempt, round), "이전 결과 중복 재도전 거절");
            Assert.IsFalse((bool)Get(run, "ExternallyManaged"));
        }

        [UnityTest]
        public IEnumerator Managed_KeepsOriginalTightropeUiAndHudVisible()
        {
            var scene = SceneManager.GetSceneByName(TightropeScene);
            var ui = (Behaviour)PerformanceLinkReflection.InScene(scene, "CurtainCall.Tightrope.UI.TightropeDemoUI");
            var debug = (Behaviour)PerformanceLinkReflection.InScene(scene, "CurtainCall.Tightrope.TightropeRunDebug");
            Assert.IsNotNull(ui);
            Assert.IsTrue(ui.isActiveAndEnabled, "독립 실행 UI 기본 상태");
            bool debugEnabled = debug != null && debug.enabled;
            yield return PrepareManagedIntro();
            yield return StartManagedPerformance();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsTrue(ui.isActiveAndEnabled, "연결 진입도 기존 UI를 숨기지 않는다.");
            if (debug != null) Assert.AreEqual(debugEnabled, debug.enabled, "기존 디버그 표시 설정 보존");
            var view = ui.GetComponent(PerformanceLinkReflection.GameType("CurtainCall.Tightrope.UI.TightropeDemoView"));
            Assert.IsTrue((bool)Get(view, "Built"));
            Assert.IsFalse((bool)Get(view, "LobbyVisible"), "진행 중에는 접속창 대신 HUD를 표시한다.");
            var hud = (Component)Get(view, "HudText");
            Assert.IsTrue(hud.gameObject.activeInHierarchy);
            StringAssert.Contains("남은 시간", (string)Get(hud, "text"));
            Assert.IsTrue(((Component)Get(view, "Gauge")).gameObject.activeInHierarchy);
            Assert.IsTrue(ui.GetComponent<Canvas>().enabled);
            Assert.IsFalse((bool)Call(run, "RestartByHost"), "UI 복원이 연결 공연의 전체 재시작 보호를 해제하면 안 된다.");
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
            yield return WaitUntil(() => Get(rewards, "Latest") != null && Phase == "RestTime", 5f, "공용 정산 후 다음 시간대");
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
            Set(run, "runStartedAt", Time.time);
            yield return WaitUntil(() => Get(rewards, "Latest") != null && Phase == "RestTime", 5f, "실패 정산");
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
            else Assert.AreEqual(1, SceneManager.sceneCount);
            yield return AssertCannonCameraPolicy();
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
            yield return null;
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
