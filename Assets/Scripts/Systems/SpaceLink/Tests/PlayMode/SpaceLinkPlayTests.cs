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
        const int TransitionFrameLimit = 600;

        object campaign;
        Component director;
        int maxSceneCount; // 전환 중 한꺼번에 열린 씬 수의 최대
        readonly List<Action> restore = new();

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
            int frames = 0;
            while ((bool)Get(director, "IsTransitioning") && frames++ < TransitionFrameLimit)
            {
                maxSceneCount = Math.Max(maxSceneCount, SceneManager.sceneCount);
                yield return null;
            }
            Assert.Less(frames, TransitionFrameLimit, "장소 전환이 끝나지 않음");
            yield return null; // 새 공간 씬의 Start
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var action in Enumerable.Reverse(restore)) action();
            restore.Clear();
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
