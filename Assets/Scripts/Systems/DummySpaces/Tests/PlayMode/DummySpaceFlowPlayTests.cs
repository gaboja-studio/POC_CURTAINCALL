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

namespace CurtainCall.DummySpaces.Tests
{
    /// <summary>독립 씬·공개 API·세팅 반영 회귀. 실제 마우스 입력/멀티플레이/공동 테스트를 대체하지 않는다.</summary>
    public sealed class DummySpaceFlowPlayTests
    {
        Component diagnostics;
        Component space;
        object campaign;
        readonly List<Action> restore = new();
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name) is PropertyInfo p ? p.GetValue(target) : target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        static object Settings(string name) => GameType("CurtainCall.Settings.GameSettings").GetProperty(name).GetValue(null);
        string Phase => Get(Get(campaign, "State"), "Phase").ToString();
        bool Advance() => (bool)Call(diagnostics, "AdvanceOffline");

        void Override(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            object before = info.GetValue(target);
            restore.Add(() => info.SetValue(target, before));
            info.SetValue(target, value);
        }

        IEnumerator Open(string name)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                $"Assets/Scenes/Tests/Systems/DummySpaces/{name}.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            space = Object.FindAnyObjectByType(GameType($"CurtainCall.DummySpaces.{name}")) as Component;
            diagnostics = Object.FindAnyObjectByType(GameType("CurtainCall.DummySpaces.DummySpaceDiagnostics")) as Component;
            Assert.IsNotNull(space, "독립 씬의 컴포넌트 저장");
            Assert.IsNotNull(diagnostics);
            campaign = Get(diagnostics, "Campaign");
        }

        void PrepareRest()
        {
            Assert.IsTrue(Advance());
            Assert.AreEqual("Intro", Phase);
            Assert.IsTrue(Advance());
            Assert.AreEqual("Performance", Phase);
            Assert.IsTrue(Advance());
            Assert.AreEqual("RestTime", Phase);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var action in Enumerable.Reverse(restore)) action();
            restore.Clear();
            if (campaign is Component c && c != null) Call(campaign, "StopOfflineRun");
            var session = GameType("CurtainCall.Network.Session.NetworkSessionManager").GetProperty("Instance").GetValue(null) as Component;
            var network = session != null ? Get(session, "NetworkManager") as Component : null;
            var rewards = diagnostics != null ? Get(diagnostics, "Rewards") as Component : null;
            if (space != null) Object.Destroy(space.gameObject);
            if (rewards != null) Object.Destroy(rewards.gameObject);
            if (campaign is Component component && component != null) Object.Destroy(component.gameObject);
            if (session != null) Object.Destroy(session.gameObject);
            if (network != null) Object.Destroy(network.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WaitingRoom_SpendSettingChangesRequestWithoutGrantingItems()
        {
            yield return Open("DummyWaitingRoom");
            PrepareRest();
            var rewards = Get(diagnostics, "Rewards");
            long before = (long)Get(rewards, "Balance");
            Assert.Greater(before, 0L, "031 환호 보상으로 준비, 자금 직접 주입 없음");
            Override(Settings("WaitingRoom"), "dummySpendAmount", 1);
            Assert.AreEqual(1, Get(space, "SpendAmount"));
            Assert.IsTrue((bool)Call(space, "Spend"));
            Assert.AreEqual(before - 1, Get(rewards, "Balance"));
            Override(Settings("WaitingRoom"), "dummySpendAmount", int.MaxValue);
            Assert.IsFalse((bool)Call(space, "Spend"), "잔액 부족은 지급/구매 성공이 아님");
            Assert.AreEqual(before - 1, Get(rewards, "Balance"));
            StringAssert.Contains("TrySpend", (string)Get(diagnostics, "TraceText"));
        }

        [UnityTest]
        public IEnumerator WaitingRoom_NightReadyUsesCampaignCountAndNextMorning()
        {
            yield return Open("DummyWaitingRoom");
            PrepareRest();
            int limit = (int)Get(Get(Settings("Base"), "Run"), "PerformancesPerDay") * 2 + 2;
            while (Phase != "Night" && limit-- > 0) Assert.IsTrue(Advance());
            Assert.AreEqual("Night", Phase);
            // Campaign.Update의 접속 인원 갱신 뒤 관찰한다. 밤 진입 시점에는 아직 0일 수 있다.
            yield return null;
            yield return null;
            Assert.AreEqual(1, Get(Get(campaign, "State"), "RequiredCount"), "오프라인 Campaign 인원은 보상 명부 2명과 다름");
            Assert.IsTrue((bool)Call(space, "Ready"));
            Assert.AreEqual("RestTime", Phase);
            Assert.AreEqual(2, Get(Get(campaign, "State"), "Day"));
            Assert.IsFalse((bool)Call(space, "Ready"), "밤 외 준비 입력 거절");
        }

        IEnumerator CheckTimer(bool fromStart)
        {
            yield return Open("DummyExploration");
            var run = Get(Settings("Base"), "Run");
            var mode = GameType("CurtainCall.Settings.RestTimerMode");
            Override(run, "restTimerMode", Enum.Parse(mode, fromStart ? "FromRestStart" : "FromLeavingWaitingRoom"));
            PrepareRest();
            Assert.AreEqual(fromStart ? "Running" : "WaitingForExit", Get(Get(campaign, "State"), "Timer").ToString());
            Assert.AreEqual(fromStart, Get(space, "IsSafe"));
            Assert.IsTrue((bool)Call(space, "Leave"));
            Assert.AreEqual("Running", Get(Get(campaign, "State"), "Timer").ToString());
            Assert.IsFalse((bool)Get(space, "IsSafe"));
            Call(space, "EnterWaitingRoom");
            Assert.AreEqual(fromStart, Get(space, "IsSafe"));
            Assert.IsTrue((bool)Call(space, "Expire"));
            yield return null;
            yield return null;
            Assert.AreEqual("Performance", Phase, "현행 만료 전환만 검증, 추격 정리 연결 PASS 아님");
        }

        [UnityTest] public IEnumerator Exploration_TimerAUsesBaseAndRestoresSetting() { yield return CheckTimer(true); }
        [UnityTest] public IEnumerator Exploration_TimerBStartsOnExitAndRestoresSetting() { yield return CheckTimer(false); }

        [UnityTest]
        public IEnumerator Exploration_CatchPreviewUsesSettingWithoutChangingRewardRoster()
        {
            yield return Open("DummyExploration");
            PrepareRest();
            var place = Enum.Parse(GameType("CurtainCall.Survival.SurvivalPlace"), "Exploration");
            Assert.IsFalse((bool)Call(diagnostics, "CatchPlayer", null, place), "실제 서버 대상 없이 신체 변경 성공으로 표시하지 않음");
            Override(Settings("Exploration"), "dummyLostCount", 0);
            Assert.AreEqual("CutToOneArm", Call(space, "Preview").ToString());
            Override(Settings("Exploration"), "dummyLostCount", 3);
            Assert.AreEqual("Died", Call(space, "Preview").ToString());
            Assert.AreEqual("RestTime", Phase, "조회는 공연 이동/신체 변경 아님");
            Assert.IsTrue((bool)Call(space, "EnterStage"));
            Assert.AreEqual("Performance", Phase);
            Assert.IsFalse((bool)Call(space, "EnterStage"), "같은 집결 신호 중복 전환 거절");
        }

        [UnityTest]
        public IEnumerator Escape_ManualArrivalConclusionEndsRunWithoutAutomaticAggregation()
        {
            yield return Open("DummyEscapeRoute");
            Assert.IsFalse((bool)Call(space, "Finish", true));
            PrepareRest();
            Override(Settings("EscapeRoute"), "dummyInputRows", 3);
            Assert.AreEqual(3, Get(space, "InputRows"));
            Assert.IsTrue((bool)Call(space, "StartEscape"));
            Assert.AreEqual("Stopped", Get(Get(campaign, "State"), "Timer").ToString());
            Assert.IsTrue((bool)Call(space, "SetRow", 2, true));
            Assert.IsFalse((bool)Call(space, "SetRow", 2, true));
            Assert.IsFalse((bool)Call(space, "SetRow", 3, true));
            Assert.AreEqual("Escape", Phase, "행 입력으로 자동 종료하지 않음");
            Assert.IsTrue((bool)Call(space, "Finish", true));
            Assert.AreEqual("Ending", Phase);
            Assert.IsFalse((bool)Call(space, "Finish", true));
        }

        [UnityTest]
        public IEnumerator Escape_ManualAllRetiredConclusionStartsNextGeneration()
        {
            yield return Open("DummyEscapeRoute");
            PrepareRest();
            Assert.IsTrue((bool)Call(space, "StartEscape"));
            Assert.IsTrue((bool)Call(space, "Finish", false));
            Assert.AreEqual("Intro", Phase);
            Assert.AreEqual(2, Get(Get(campaign, "State"), "Generation"));
            StringAssert.Contains("EndEscape", (string)Get(diagnostics, "TraceText"));
        }
    }
}
