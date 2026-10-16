using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.DummySpaces.Tests
{
    /// <summary>저장된 UI 참조와 Button.onClick 연결 회귀. 실제 마우스 입력 검사는 아니다.</summary>
    public sealed class DummySpaceCanvasPlayTests
    {
        Component diagnostics;
        Component space;
        Component canvas;
        object campaign;
        readonly System.Collections.Generic.List<Action> restore = new();

        void Override(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            object before = info.GetValue(target);
            restore.Add(() => info.SetValue(target, before));
            info.SetValue(target, value);
        }

        static object Settings(string name) => GameType("CurtainCall.Settings.GameSettings").GetProperty(name).GetValue(null);

        // 테스트 어셈블리에서 게임 코드와 UI 패키지를 직접 참조하지 않는다.
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name) is PropertyInfo p ? p.GetValue(target) : target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        string Phase => Get(Get(campaign, "State"), "Phase").ToString();
        object Rewards => Get(diagnostics, "Rewards");
        long Balance => (long)Get(Rewards, "Balance");
        int ExecutionCount => (int)Get(canvas, "ExecutionCount");
        object[] Actions => ((IEnumerable)Get(canvas, "actions")).Cast<object>().ToArray();

        object Button(string id)
        {
            var matches = Actions.Where(a => (string)Get(a, "id") == id).ToArray();
            Assert.AreEqual(1, matches.Length, $"액션 {id}는 한 번만 저장되어야 함");
            var button = Get(matches[0], "button");
            Assert.IsNotNull(button, $"액션 {id}의 버튼 참조");
            return button;
        }

        void Click(string id)
        {
            var button = Button(id);
            Assert.IsTrue((bool)Get(button, "interactable"), $"현재 단계에서 {id} 버튼 사용 가능");
            // 저장된 버튼의 이벤트를 호출한다. 포인터 이동이나 실제 클릭을 모사하지 않는다.
            var onClick = button.GetType().GetProperty("onClick").GetValue(button);
            onClick.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(onClick, null);
        }

        string Text(string field) => (string)Get(Get(canvas, field), "text");

        IEnumerator Open(string name)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                $"Assets/Scenes/Tests/Systems/DummySpaces/{name}.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            space = Object.FindAnyObjectByType(GameType($"CurtainCall.DummySpaces.{name}")) as Component;
            diagnostics = Object.FindAnyObjectByType(GameType("CurtainCall.DummySpaces.DummySpaceDiagnostics")) as Component;
            canvas = Object.FindAnyObjectByType(GameType("CurtainCall.DummySpaces.DummySpaceCanvas")) as Component;
            Assert.IsNotNull(space, "독립 씬의 공간 컴포넌트");
            Assert.IsNotNull(diagnostics);
            Assert.IsNotNull(canvas, "씬에 저장된 UI 연결 컴포넌트");
            campaign = Get(diagnostics, "Campaign");
        }

        void PrepareRest()
        {
            Click("advance");
            Assert.AreEqual("Intro", Phase);
            Click("advance");
            Assert.AreEqual("Performance", Phase);
            Click("advance");
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
            if (canvas != null) Object.Destroy(canvas.gameObject);
            if (space != null) Object.Destroy(space.gameObject);
            if (rewards != null) Object.Destroy(rewards.gameObject);
            if (campaign is Component component && component != null) Object.Destroy(component.gameObject);
            if (session != null) Object.Destroy(session.gameObject);
            if (network != null) Object.Destroy(network.gameObject);
            yield return null;
            canvas = null;
            space = null;
            diagnostics = null;
            campaign = null;
        }

        [UnityTest]
        public IEnumerator FourScenes_HaveCanvasInputAndSavedActionReferences()
        {
            var scenes = new[] { "DummyPerformance", "DummyWaitingRoom", "DummyExploration", "DummyEscapeRoute" };
            var expected = new[]
            {
                new[] { "start", "begin", "person", "arrive", "cut", "kill", "cheer", "timeout" },
                new[] { "advance", "spend", "leave", "ready", "lost", "backpack", "restorePreview" },
                new[] { "advance", "leave", "room", "stage", "expire", "catchPreview" },
                new[] { "advance", "escape", "row", "rowArrive", "rowRetire", "success", "retired" }
            };
            for (int i = 0; i < scenes.Length; i++)
            {
                yield return Open(scenes[i]);
                var scene = SceneManager.GetActiveScene();
                var savedCanvas = canvas.GetComponentInParent<Canvas>();
                Assert.IsNotNull(savedCanvas, $"{scenes[i]} Canvas 계층");
                Assert.AreEqual(scene, savedCanvas.gameObject.scene);
                var eventSystem = Object.FindAnyObjectByType(GameType("UnityEngine.EventSystems.EventSystem")) as Component;
                var input = Object.FindAnyObjectByType(GameType("UnityEngine.InputSystem.UI.InputSystemUIInputModule")) as Component;
                Assert.IsNotNull(eventSystem, $"{scenes[i]} EventSystem");
                Assert.IsNotNull(input, $"{scenes[i]} InputSystemUIInputModule");
                Assert.AreEqual(scene, eventSystem.gameObject.scene);
                Assert.AreEqual(eventSystem.gameObject, input.gameObject);
                foreach (string field in new[] { "status", "live", "clock", "clockFill", "steps", "selection", "condition", "resultTitle", "resultBadge", "resultBody", "changes", "connection", "history", "details", "preview" })
                {
                    var reference = Get(canvas, field) as Component;
                    Assert.IsNotNull(reference, $"{scenes[i]} 저장 참조 {field}");
                    Assert.AreEqual(scene, reference.gameObject.scene);
                }
                var detailsPanel = Get(canvas, "detailsPanel") as GameObject;
                Assert.IsNotNull(detailsPanel, "세부 정보 패널 저장 참조");
                Assert.AreEqual(scene, detailsPanel.scene);
                Assert.IsNotEmpty(Actions, "저장된 액션 목록");
                var ids = Actions.Select(a => (string)Get(a, "id")).ToArray();
                Assert.AreEqual(ids.Length, ids.Distinct().Count(), "중복 액션 ID 없음");
                foreach (string id in ids)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(id), "액션 ID 저장");
                    var button = Button(id) as Component;
                    Assert.IsNotNull(button);
                    Assert.AreEqual("UnityEngine.UI.Button", button.GetType().FullName);
                    Assert.AreEqual(scene, button.gameObject.scene);
                }
                foreach (string id in expected[i]) Button(id);
                Button("details");
                Assert.AreEqual(0, ExecutionCount, "씬 로드만으로 액션 실행 없음");
                // 루프의 다음 씬에도 기존 회귀 테스트와 같은 싱글턴 정리를 적용한다.
                yield return TearDown();
            }
        }

        [UnityTest]
        public IEnumerator Exploration_NaturalTimeUpdatesLiveStoryWithoutOverwritingSelection()
        {
            yield return Open("DummyExploration");
            var run = Get(Settings("Base"), "Run");
            Override(run, "restTimerMode", Enum.Parse(GameType("CurtainCall.Settings.RestTimerMode"), "FromLeavingWaitingRoom"));
            Override(run, "guideRestDuration", 1.2f);
            Override(run, "recallAlarmLeadTime", 0.6f);
            PrepareRest();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("WaitingForExit", Get(Get(campaign, "State"), "Timer").ToString());
            StringAssert.Contains("출구 입력 대기", Text("live"));
            float before = (float)Get(Get(campaign, "State"), "TimeRemaining");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(before, (float)Get(Get(campaign, "State"), "TimeRemaining"));
            Click("leave");
            string selectionResult = Text("resultTitle");
            yield return new WaitForSeconds(0.3f);
            Assert.Less((float)Get(Get(campaign, "State"), "TimeRemaining"), before);
            StringAssert.Contains("진행 중", Text("live"));
            int count = (int)Get(diagnostics, "StoryCount");
            yield return null;
            Assert.AreEqual(count, Get(diagnostics, "StoryCount"), "초 감소를 프레임마다 기록하지 않음");
            yield return new WaitForSeconds(1.1f);
            Assert.AreEqual("Performance", Phase);
            StringAssert.Contains("탐색 제한 시간 만료", Text("history"));
            StringAssert.Contains("소집 경보", Text("history"));
            Assert.AreEqual(selectionResult, Text("resultTitle"), "자동 변화는 최근 선택 결과를 덮지 않음");
            StringAssert.Contains("더미 공연 시계 없음", Text("live"));
            Assert.AreEqual("--:--", Text("clock"));
        }

        [UnityTest]
        public IEnumerator Exploration_TimerAStartsNaturallyAndNightHasNoClock()
        {
            yield return Open("DummyExploration");
            var run = Get(Settings("Base"), "Run");
            Override(run, "restTimerMode", Enum.Parse(GameType("CurtainCall.Settings.RestTimerMode"), "FromRestStart"));
            PrepareRest();
            float before = (float)Get(Get(campaign, "State"), "TimeRemaining");
            yield return new WaitForSeconds(0.25f);
            Assert.Less((float)Get(Get(campaign, "State"), "TimeRemaining"), before);
            StringAssert.Contains("진행 중", Text("live"));
            int safety = 12;
            while (Phase != "Night" && safety-- > 0) Assert.IsTrue((bool)Call(diagnostics, "AdvanceOffline"));
            Assert.AreEqual("Night", Phase);
            yield return new WaitForSeconds(0.2f);
            StringAssert.Contains("제한 시간 없음", Text("live"));
            Assert.AreEqual("--:--", Text("clock"));
        }

        [UnityTest]
        public IEnumerator Performance_ButtonEventsConfirmSuccessIncomeAndRestPhase()
        {
            yield return Open("DummyPerformance");
            Click("start");
            Assert.AreEqual("Performance", Phase);
            Click("begin");
            var segments = ((IEnumerable)Get(Get(Rewards, "State"), "Segments")).Cast<object>().ToArray();
            Assert.AreEqual(2, segments.Length, "오프라인 더미 명부 2명");
            string first = (string)Get(canvas, "SelectedParticipant");
            long before = Balance;
            long cheerBefore = Convert.ToInt64(Get(segments[0], "Amount"));
            Click("cheer");
            var updated = ((IEnumerable)Get(Get(Rewards, "State"), "Segments")).Cast<object>().First();
            Assert.Greater(Convert.ToInt64(Get(updated, "Amount")), cheerBefore);
            Assert.AreEqual(before, Balance, "환호 입력은 정산 전 자금을 바꾸지 않음");
            Click("arrive");
            Assert.IsTrue((bool)Call(space, "HasArrived", first));
            Assert.AreEqual("Performance", Phase, "한 명 도착으로 종료하지 않음");
            Assert.IsFalse((bool)Get(Button("arrive"), "interactable"), "선택 참가자 중복 도착 방지");
            int countBeforeSelection = ExecutionCount;
            Click("person");
            Assert.AreNotEqual(first, Get(canvas, "SelectedParticipant"));
            Assert.AreEqual(countBeforeSelection, ExecutionCount, "대상 선택은 공통 API 실행 횟수에서 제외");
            Click("arrive");
            var result = Get(Rewards, "Latest");
            Assert.IsNotNull(result);
            Assert.IsTrue((bool)Get(result, "Succeeded"));
            Assert.AreEqual("Completed", Get(result, "EndReason").ToString());
            long income = Convert.ToInt64(Get(result, "Income"));
            Assert.Greater(income, 0L);
            Assert.AreEqual(before + income, Balance);
            Assert.AreEqual("RestTime", Phase);
            Assert.AreEqual(5, ExecutionCount, "시작·명부·환호·두 도착을 한 번씩 실행");
            Assert.AreEqual("arrive", Get(canvas, "LastAction"));
            StringAssert.Contains("공연 성공", (string)Get(canvas, "ResultText"));
            StringAssert.Contains(income.ToString(), (string)Get(canvas, "ResultText"));
        }

        [UnityTest]
        public IEnumerator NonPerformanceSpace_ApiRosterDoesNotEnablePerformanceActions()
        {
            yield return Open("DummyWaitingRoom");
            Click("advance");
            Click("advance");
            Assert.AreEqual("Performance", Phase);
            // 더미 버튼이 아니라 공개 API로 명부를 만든 경우(032 연결 런)를 재현한다.
            var seedType = GameType("CurtainCall.Reward.RewardParticipantSeed");
            var seeds = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(seedType));
            seeds.Add(Activator.CreateInstance(seedType, "Api-0", 0UL, 0, 0, false));
            Assert.IsTrue((bool)Call(Rewards, "BeginOfflinePerformance", seeds, 1, null), "API 명부 생성");
            Assert.IsNotNull(Get(canvas, "SelectedParticipant"), "명부가 있어 선택 참가자가 생김");
            foreach (var id in new[] { "arrive", "cut", "kill", "cheer" })
                Assert.IsFalse((bool)Call(canvas, "CanExecute", id), $"공연 컴포넌트가 없는 공간에서 {id} 비활성");
            // 주기 Refresh가 모든 버튼의 CanExecute를 다시 부른다. 예외 로그가 나면 테스트가 실패한다.
            yield return new WaitForSecondsRealtime(0.4f);
        }

        [UnityTest]
        public IEnumerator Reenable_ButtonEventExecutesExactlyOncePerInvocation()
        {
            yield return Open("DummyWaitingRoom");
            int count = ExecutionCount;
            Click("restorePreview");
            Assert.AreEqual(++count, ExecutionCount);
            for (int i = 0; i < 2; i++)
            {
                canvas.gameObject.SetActive(false);
                yield return null;
                canvas.gameObject.SetActive(true);
                yield return null;
                yield return null;
                Assert.AreEqual(count, ExecutionCount, "재활성화 자체는 액션을 실행하지 않음");
                Click("restorePreview");
                Assert.AreEqual(++count, ExecutionCount, "재활성화 후 이벤트 리스너 중복 등록 없음");
            }
        }

        [UnityTest]
        public IEnumerator WaitingRoom_PreviewButtonsShowRulesWithoutChangingBalance()
        {
            yield return Open("DummyWaitingRoom");
            PrepareRest();
            long before = Balance;
            int executions = ExecutionCount;
            for (int i = 0; i < 4; i++)
            {
                int lost = (int)Get(space, "PreviewLostCount");
                bool backpack = (bool)Get(space, "PreviewBackpack");
                Click("lost");
                Assert.AreEqual((lost + 1) % 4, Get(space, "PreviewLostCount"));
                Click("backpack");
                Assert.AreEqual(!backpack, Get(space, "PreviewBackpack"));
                Assert.AreEqual(executions, ExecutionCount, "미리보기 입력 선택은 API 실행이 아님");
                Click("restorePreview");
                Assert.AreEqual(++executions, ExecutionCount);
                string preview = (string)Get(canvas, "ResultText");
                StringAssert.Contains($"손실 {Get(space, "PreviewLostCount")} → 복구 {Get(space, "PreviewRestorePart")}", preview);
                StringAssert.Contains($"소지 {Get(space, "PreviewSlots")}칸", preview);
                StringAssert.Contains("실제 신체·인벤토리·자금은 바뀌지 않습니다.", preview);
                Assert.AreEqual("조회 · 미리보기", Text("resultBadge"));
                Assert.AreEqual(before, Balance);
                Assert.AreEqual("RestTime", Phase);
            }
        }

        [UnityTest]
        public IEnumerator Escape_RowButtonsDoNotEndEscapeUntilManualSuccess()
        {
            yield return Open("DummyEscapeRoute");
            PrepareRest();
            Click("escape");
            Assert.AreEqual("Escape", Phase);
            int rows = (int)Get(space, "InputRows");
            Assert.Greater(rows, 0);
            for (int i = 0; i < rows; i++)
            {
                Assert.AreEqual(i, Get(canvas, "SelectedIndex"));
                Click("rowArrive");
                Assert.AreEqual("하역장 도착 입력", Call(space, "RowState", i));
                Assert.AreEqual("Escape", Phase, "행 도착을 자동 집계하지 않음");
                if (i + 1 < rows) Click("row");
            }
            yield return null;
            Assert.AreEqual("Escape", Phase, "모든 더미 행 도착 후에도 자동 종료하지 않음");
            Click("rowRetire");
            Assert.AreEqual("리타이어 입력", Call(space, "RowState", rows - 1));
            Assert.AreEqual("더미 입력 반영", Text("resultBadge"));
            Assert.AreEqual("Escape", Phase);
            int before = ExecutionCount;
            Click("success");
            Assert.AreEqual(before + 1, ExecutionCount);
            Assert.AreEqual("success", Get(canvas, "LastAction"));
            Assert.AreEqual("Ending", Phase, "행과 독립된 수동 성공 결론으로 전환");
            for (int i = 0; i < rows; i++) Assert.AreEqual("미입력", Call(space, "RowState", i));
            Assert.IsFalse((bool)Get(Button("success"), "interactable"), "엔딩 이후 중복 결론 비활성");
        }
    }
}
