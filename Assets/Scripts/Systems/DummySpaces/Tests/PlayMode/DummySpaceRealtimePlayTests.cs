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
    /// <summary>실제 프레임 경과로 더미 공연 시계만 검증한다. UI 포인터·온라인 시계 검증은 아니다.</summary>
    public sealed class DummySpaceRealtimePlayTests
    {
        Component space;
        Component diagnostics;
        Component canvas;
        Component campaign;
        Component rewards;
        readonly List<Action> restore = new();
        EventInfo resultEvent;
        Delegate resultHandler;
        int resultCount;

        // 기존 공간 회귀처럼 게임 어셈블리와 UI 패키지의 직접 참조 없이 공개 API를 호출한다.
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name) is PropertyInfo p ? p.GetValue(target) : target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        float Remaining => (float)Get(space, "PerformanceTimeRemaining");
        float Limit => (float)Get(space, "PerformanceTimeLimit");
        bool Running => (bool)Get(space, "ClockRunning");
        string Phase => Get(Get(campaign, "State"), "Phase").ToString();
        object State => Get(rewards, "State");
        int HistoryCount => ((IEnumerable)Get(rewards, "History")).Cast<object>().Count();

        void Override(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"메모리 설정 필드 {field}");
            object before = info.GetValue(target);
            restore.Add(() => info.SetValue(target, before));
            info.SetValue(target, value);
        }

        void SetTimeLimit(float seconds)
        {
            var settings = GameType("CurtainCall.Settings.GameSettings").GetProperty("Tightrope").GetValue(null);
            Override(Get(settings, "Run"), "timeLimit", seconds);
        }

        void CountResult<T>(T result) => resultCount++;

        void Begin()
        {
            Assert.IsTrue((bool)Call(space, "BeginPerformance"));
            Assert.IsTrue(Running, "Begin 성공 직후 시계 시작");
            Assert.AreEqual(2f, Limit);
            Assert.AreEqual(Limit, Remaining, "같은 프레임 시작은 아직 시간을 소비하지 않음");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            resultCount = 0;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Tests/Systems/DummySpaces/DummyPerformance.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            space = Object.FindAnyObjectByType(GameType("CurtainCall.DummySpaces.DummyPerformance")) as Component;
            diagnostics = Object.FindAnyObjectByType(GameType("CurtainCall.DummySpaces.DummySpaceDiagnostics")) as Component;
            canvas = Object.FindAnyObjectByType(GameType("CurtainCall.DummySpaces.DummySpaceCanvas")) as Component;
            Assert.IsNotNull(space);
            Assert.IsNotNull(diagnostics);
            campaign = Get(diagnostics, "Campaign") as Component;
            rewards = Get(space, "Rewards") as Component;
            Assert.IsTrue((bool)Call(space, "StartOfflineRun"));
            Assert.AreEqual("Performance", Phase);
            SetTimeLimit(2f);
            resultEvent = rewards.GetType().GetEvent("ResultConfirmed");
            var handlerMethod = GetType().GetMethod(nameof(CountResult), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(resultEvent.EventHandlerType.GenericTypeArguments[0]);
            resultHandler = Delegate.CreateDelegate(resultEvent.EventHandlerType, this, handlerMethod);
            resultEvent.AddEventHandler(rewards, resultHandler);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 실패한 assertion 뒤에도 reflection 변경을 역순으로 복원하며 에셋 저장은 하지 않는다.
            foreach (var action in Enumerable.Reverse(restore)) action();
            restore.Clear();
            if (rewards != null && resultHandler != null) resultEvent.RemoveEventHandler(rewards, resultHandler);
            resultHandler = null;
            resultEvent = null;
            if (campaign != null) Call(campaign, "StopOfflineRun");
            var session = GameType("CurtainCall.Network.Session.NetworkSessionManager").GetProperty("Instance").GetValue(null) as Component;
            var network = session != null ? Get(session, "NetworkManager") as Component : null;
            if (canvas != null) Object.Destroy(canvas.gameObject);
            if (space != null) Object.Destroy(space.gameObject);
            if (rewards != null) Object.Destroy(rewards.gameObject);
            if (campaign != null) Object.Destroy(campaign.gameObject);
            if (session != null) Object.Destroy(session.gameObject);
            if (network != null) Object.Destroy(network.gameObject);
            yield return null;
            space = diagnostics = canvas = campaign = rewards = null;
        }

        [UnityTest]
        public IEnumerator NaturalFrames_CutAndSingleArrivalKeepClockRunning()
        {
            Begin();
            yield return new WaitForSeconds(.2f);
            float before = Remaining;
            Assert.Less(before, Limit, "프레임 경과로 실제 감소");
            Assert.Greater(before, 0f);
            Assert.IsTrue((bool)Call(space, "Cut", "Dummy-0"));
            Assert.IsTrue((bool)Call(space, "Arrive", "Dummy-0"));
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(Running, "한 참가자의 부상·도착은 전체 종료가 아님");
            Assert.Less(Remaining, before);
            Assert.AreEqual(1, Get(((IEnumerable)Get(State, "Segments")).Cast<object>().First(), "LostCount"));
            Assert.AreEqual(0, resultCount);
        }

        [UnityTest]
        public IEnumerator DuplicateBegin_PreservesArrivalRemainingAndCapturedSetting()
        {
            Begin();
            Assert.IsTrue((bool)Call(space, "Arrive", "Dummy-0"));
            yield return new WaitForSeconds(.2f);
            float before = Remaining;
            SetTimeLimit(1f);
            Assert.IsTrue((bool)Call(space, "BeginPerformance"));
            Assert.AreEqual(before, Remaining, "동일 명부 Begin은 시계를 초기화하지 않음");
            Assert.AreEqual(2f, Limit, "진행 중 설정 변경은 시작 시 보관한 기준 시간을 바꾸지 않음");
            Assert.IsTrue((bool)Call(space, "HasArrived", "Dummy-0"));
            Assert.IsFalse((bool)Call(space, "Arrive", "Dummy-0"));
            yield return new WaitForSeconds(.2f);
            Assert.Less(Remaining, before);
        }

        [UnityTest]
        public IEnumerator NaturalExpiry_ConfirmsFailureOnceAndTransitionsThrough031()
        {
            SetTimeLimit(1f);
            long balance = (long)Get(rewards, "Balance");
            int history = HistoryCount;
            int ledger = (int)Get(State, "LedgerCount");
            Assert.IsTrue((bool)Call(space, "BeginPerformance"));
            Assert.AreEqual(1f, Limit);
            Call(canvas, "Execute", "cut");
            string selectedResult = (string)Get(Get(canvas, "resultTitle"), "text");
            yield return new WaitForSeconds(1.2f);
            var result = Get(rewards, "Latest");
            StringAssert.Contains("공연 실패 확정", (string)Get(Get(canvas, "history"), "text"));
            StringAssert.Contains("제한 시간 만료", (string)Get(Get(canvas, "live"), "text"));
            Assert.AreEqual(selectedResult, Get(Get(canvas, "resultTitle"), "text"), "자동 결과는 마지막 사용자 선택과 분리");
            Assert.IsNotNull(result, "수동 만료 입력 없이 자연 만료");
            Assert.IsFalse((bool)Get(result, "Succeeded"));
            Assert.AreEqual("TimeExpired", Get(result, "EndReason"));
            Assert.AreEqual(0L, Get(result, "Income"));
            // 실패는 설정한 유지 시간(기본 2초) 동안 공연 화면을 비춘 뒤 넘어간다.
            float waited = 0f;
            while (Phase == "Performance" && waited < 5f) { waited += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual("RestTime", Phase, "031 결과 확정이 028 시간대를 전환");
            Assert.AreEqual(0f, Remaining);
            Assert.IsFalse(Running);
            Assert.AreEqual(1, resultCount);
            Assert.AreEqual(history + 1, HistoryCount);
            Assert.AreEqual(ledger + 1, Get(State, "LedgerCount"));
            Assert.AreEqual(balance, Get(rewards, "Balance"));
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(1, resultCount, "만료 이후 추가 결과 확정 없음");
            Assert.AreEqual(history + 1, HistoryCount);
            Assert.AreEqual(ledger + 1, Get(State, "LedgerCount"));
            Assert.AreSame(result, Get(rewards, "Latest"));
        }

        [UnityTest]
        public IEnumerator EarlySuccess_StopsClockWithoutLaterExpiryOrSettlement()
        {
            SetTimeLimit(1f);
            Assert.IsTrue((bool)Call(space, "BeginPerformance"));
            Assert.IsTrue((bool)Call(space, "Arrive", "Dummy-0"));
            Assert.IsTrue((bool)Call(space, "Arrive", "Dummy-1"));
            Assert.IsFalse(Running, "전체 성공 즉시 시계 중지");
            float stopped = Remaining;
            var result = Get(rewards, "Latest");
            int ledger = (int)Get(State, "LedgerCount");
            long balance = (long)Get(rewards, "Balance");
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(stopped, Remaining);
            Assert.AreSame(result, Get(rewards, "Latest"));
            Assert.AreEqual("Completed", Get(result, "EndReason"));
            Assert.AreEqual("RestTime", Phase);
            Assert.AreEqual(1, resultCount);
            Assert.AreEqual(ledger, Get(State, "LedgerCount"));
            Assert.AreEqual(balance, Get(rewards, "Balance"));
        }

        [UnityTest]
        public IEnumerator CanvasDisabled_ClockContinuesAndReenableDoesNotRestart()
        {
            Begin();
            Assert.IsNotNull(canvas);
            var root = canvas.GetComponentInParent<Canvas>();
            Assert.IsNotNull(root);
            Assert.IsFalse(space.transform.IsChildOf(root.transform), "공연 드라이버는 Canvas 계층과 독립");
            root.gameObject.SetActive(false);
            float before = Remaining;
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(Running);
            Assert.Less(Remaining, before);
            float hidden = Remaining;
            root.gameObject.SetActive(true);
            yield return null;
            Assert.LessOrEqual(Remaining, hidden, "UI 재활성화는 시계 리셋 아님");
            Assert.AreEqual(0, resultCount);
        }

        [UnityTest]
        public IEnumerator DriverDisabled_StopsAndResumesOnlySavedRemaining()
        {
            Begin();
            yield return new WaitForSeconds(.2f);
            var driver = (Behaviour)space;
            driver.enabled = false;
            float stopped = Remaining;
            Assert.IsFalse(Running);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(stopped, Remaining, "드라이버 비활성 중에는 시계 중지");
            driver.enabled = true;
            yield return null;
            yield return null;
            Assert.IsTrue(Running);
            Assert.Less(Remaining, stopped, "재활성화는 보관한 잔여 시간부터 진행");
            Assert.Less(Remaining, Limit);
            Assert.AreEqual(0, resultCount);
        }

        [UnityTest]
        public IEnumerator ExternalRoster_IsObservedButReplacementDriverCannotRestoreClock()
        {
            var seed = GameType("CurtainCall.Reward.RewardParticipantSeed");
            var people = Array.CreateInstance(seed, 2);
            people.SetValue(Activator.CreateInstance(seed, "Dummy-0", 0UL, 0, 0, false), 0);
            people.SetValue(Activator.CreateInstance(seed, "Dummy-1", 1UL, 1, 0, false), 1);
            Assert.IsTrue((bool)Call(rewards, "BeginOfflinePerformance", people, 1, null));
            yield return null;
            yield return null;
            Assert.IsTrue(Running, "다른 입력 경로에서 연 새 명부도 Update가 관찰");
            Assert.AreEqual(2f, Limit);
            var host = space.gameObject;
            Object.Destroy(space);
            yield return null;
            space = host.AddComponent(GameType("CurtainCall.DummySpaces.DummyPerformance"));
            yield return null;
            yield return null;
            Assert.IsFalse(Running, "새 드라이버가 기존 명부에 전체 시간을 재부여하지 않음");
            StringAssert.Contains("복원 미지원", (string)Get(space, "ClockStatus"));
            Assert.IsTrue((bool)Call(space, "BeginPerformance"));
            Assert.IsFalse(Running, "같은 명부 Begin도 복원 제한을 우회하지 못함");
            Assert.AreEqual(0, resultCount);
        }
    }
}
