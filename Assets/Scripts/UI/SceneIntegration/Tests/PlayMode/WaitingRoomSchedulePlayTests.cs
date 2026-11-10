using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest, Category("BreakFollowup"), Category("BreakFollowupRetry")]
        public IEnumerator ScheduleBetweenRest_AllUpcomingCardsCanChange()
        {
            yield return Launch(2, "Between", 1, "Tightrope", "WaitingRoomBase");
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Call(run, "RecordClear", "Tightrope", 1);
            yield return OpenSchedule();
            yield return CannonWait(() => !(bool)Get(Find("Unity.Cinemachine.CinemachineBrain"), "IsBlending"), 5, "Board camera");
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var edits = (UnityEngine.UI.Button[])Private(ui, "editButtons");
            Assert.IsTrue(edits[3].interactable, "Current next show is editable during between-show rest");
            Assert.IsTrue(edits[4].interactable, "Future days are also editable during rest");
            Assert.IsFalse(edits[2].interactable, "Completed show remains historical");
            Call(ui, "ChooseTier", 2, 1, 2);
            Assert.AreEqual(2, Field(State, "Difficulty"), "A rest-time UI change immediately reaches the campaign");
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/break-followup-schedule.png"));
            yield return null; yield return null;
        }

        IEnumerator OpenSchedule()
        {
            var board = Find("CurtainCall.WaitingRoom.ScheduleBoardView");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var point = (Vector3)Get(board, "PresentationPoint");
            Call(player.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", new Vector3(point.x, .1f, point.z), Quaternion.identity);
            Call(board, "Interact");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(false, Private(board, "showLegacyUi"));
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            Assert.AreEqual(true, Get(Get(adapter, "View"), "IsVisible"));
            var world = ((Component)Get(adapter, "View")).GetComponentInParent<Canvas>();
            Assert.AreEqual(RenderMode.WorldSpace, world.renderMode, "별도 화면 팝업이 아니라 판 위 캔버스여야 한다");
            Assert.IsTrue(world.transform.IsChildOf(board.transform));
            Assert.IsTrue(world.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled);

        }
        IEnumerator ReadyToMorning(int day, object ui)
        {
            // M2: 준비 완료는 숙소 침대 F(Task-079). 스케줄표 화면에는 준비 버튼이 없다.
            Assert.IsFalse(((Component)Private(ui, "readyButton")).gameObject.activeSelf);
            Call(Find("CurtainCall.WaitingRoom.NightBed"), "Interact");
            Call(Get(ui, "View"), "Close");
            float until = Time.realtimeSinceStartup + 15;
            while ((Field(State, "Phase").ToString() == "Night" || Field(State, "Phase").ToString() == "Dawn")
                && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(day, Field(State, "Day"));
            bool pursuit = (bool)T("CurtainCall.Settings.WaitingRoomScenes").GetProperty("PursuitEnabled").GetValue(null);
            Assert.AreEqual(day == 4 && Application.version == "0.1.0" && !pursuit ? "DemoEnding" : "RestTime", Field(State, "Phase").ToString());
            yield return new WaitForSecondsRealtime(2.2f);
        }
        static IEnumerator ClickScheduleButtonWithMouse(Mouse mouse, UnityEngine.UI.Button button)
        {
            var rect = (RectTransform)button.transform;
            Assert.IsTrue(button.IsInteractable(), button.name + " 실제 포인터 입력 가능 상태");
            var events = UnityEngine.EventSystems.EventSystem.current;
            var camera = rect.GetComponentInParent<Canvas>().worldCamera;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = position }, hits);
            if (hits.Count == 0)
            {
                System.IO.Directory.CreateDirectory(".harness/local/rehearsal/followup");
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/followup/world-schedule-pointer-failure.png"));
                Debug.Log($"Schedule pointer miss: {button.name}, screen={position}, size={Screen.width}x{Screen.height}, world={rect.TransformPoint(rect.rect.center)}, canvas={rect.GetComponentInParent<Canvas>().transform.localScale}");
            }
            Assert.IsNotEmpty(hits, button.name + " 실제 포인터 클릭 대상");
            Assert.AreEqual(button.gameObject,
                UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 }); yield return null; yield return null;
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return new WaitForSecondsRealtime(.2f);
        }

        [UnityTest]
        [Category("BreakFlow"), Category("BreakFollowup")] public IEnumerator W7_FirstDayWorldScheduleChangesImmediatelyAndEntersWithoutConfirm()
        {
            yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
            yield return WaitQuestReady("gather");
            var campaign = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            yield return OpenSchedule();
            yield return CannonWait(() => !(bool)Get(Find("Unity.Cinemachine.CinemachineBrain"), "IsBlending"), 5,
                "Wait for the board close-up before testing the real pointer");
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var schedule = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            var book = Get(schedule, "Book");
            Assert.IsFalse(((Component)Private(ui, "confirmButton")).gameObject.activeSelf);
            var labels = ((Component)Get(ui, "View")).GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.IsFalse(labels.Any(t => t.isActiveAndEnabled && t.gameObject.activeInHierarchy && t.text.Contains("확정")), "No confirmation text is visible in the rendered card template");
            var pointer = InputSystem.AddDevice<Mouse>();
            try
            {
                var edits = (UnityEngine.UI.Button[])Private(ui, "editButtons");
                yield return ClickScheduleButtonWithMouse(pointer, edits[1]);
                Assert.AreEqual("Tightrope", Call(book, "Get", 1, 1), "Unopened tricks and empty entries are skipped");
                Assert.AreEqual("Tightrope", Field(State, "TrickId"));
                Assert.IsFalse((bool)Get(book, "FirstDayConfirmed"));
            }
            finally { InputSystem.RemoveDevice(pointer); }
            Call(Get(ui, "View"), "Close"); yield return null;
            Assert.IsTrue((bool)Call(campaign, "NotifyAllAtStageEntrance"));
            Assert.AreEqual("Performance", Field(State, "Phase").ToString());
        }

        [UnityTest]
        public IEnumerator W7_PanelsKeepSidesAndOnlyNewestStackItemHasMarker()
        {
            GameObject probe = null;
            Behaviour tutorialToggle = null;
            bool wasEnabled = false;
            object panel = null;
            try
            {
                yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
                yield return new WaitForSecondsRealtime(.5f);
                tutorialToggle = Find(TutorialType) as Behaviour;
                wasEnabled = tutorialToggle != null && tutorialToggle.enabled;
                if (tutorialToggle != null) tutorialToggle.enabled = false;
                var flow = Find("CurtainCall.WaitingRoom.WaitingRoomFirstDayFlow");
                var quest = Priv(flow, "firstDayQuest");
                var firstView = (Component)Priv(quest, "view");
                Assert.IsNotNull(firstView);
                probe = new GameObject("PanelStackProbe");
                panel = Activator.CreateInstance(T("CurtainCall.WaitingRoom.WaitingRoomRightPanel"), probe.transform, "NewQuestProbe", 300f, 400f, null);
                Assert.IsTrue((bool)Call(panel, "Show", "추가 퀘스트", "새로 쌓인 패널만 느낌표를 표시합니다."));
                Assert.IsTrue((bool)T(GuideType).GetMethod("Show").Invoke(null, new object[] { "설명", "설명은 왼쪽, 퀘스트는 오른쪽에 표시합니다.", 5f }));
                yield return new WaitForSecondsRealtime(.35f); Canvas.ForceUpdateCanvases();
                var nextView = (Component)Priv(panel, "view");
                var title0 = firstView.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.name == "60_TitleTMP");
                var title1 = nextView.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.name == "60_TitleTMP");
                Assert.IsFalse(title0.text.StartsWith("!"));
                Assert.IsTrue(title1.text.StartsWith("! "));
                var firstRect = (RectTransform)firstView.transform;
                var nextRect = (RectTransform)nextView.transform;
                float gap = -nextRect.anchoredPosition.y + firstRect.anchoredPosition.y - ((Vector2)Get(firstView, "PresentationSizeUi")).y;
                Assert.That(gap, Is.InRange(4f, 12f));
                var guide = GameObject.Find("TutorialGuideCanvas").GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.name == "60_TitleTMP");
                Assert.IsFalse(guide.text.StartsWith("!"));
                Assert.IsTrue((bool)Static(GuideType, "IsVisible"), "왼쪽 설명이 실제 표시 중이어야 한다");
                Assert.IsTrue(guide.gameObject.activeInHierarchy);
                Assert.IsTrue((bool)Get(guide.GetComponentInParent(T("CurtainCall.UI.TooltipsV1.TooltipView")), "IsVisible"));
                Assert.IsFalse(title0.isTextTruncated, "최신 퀘스트 제목이 잘리면 안 됨");
                Assert.Less(RectTransformUtility.WorldToScreenPoint(null, guide.rectTransform.position).x, Screen.width * .5f);
                Assert.Greater(RectTransformUtility.WorldToScreenPoint(null, title0.rectTransform.position).x, Screen.width * .5f);
                System.IO.Directory.CreateDirectory(".harness/local/rehearsal/followup");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/followup/panels.png"));
                yield return null; yield return null;
                Assert.IsTrue((bool)Call(quest, "Show", "다음 구간 퀘스트", "기존 패널을 재사용하는 새 퀘스트입니다."));
                Assert.IsTrue(title0.text.StartsWith("! "), "새 퀘스트를 재사용 패널에 표시해도 최신 순서로 이동");
                Assert.IsFalse(title1.text.StartsWith("!"));
                Call(panel, "Show", "추가 퀘스트", "진행 인원 1/4");
                Assert.IsFalse(title1.text.StartsWith("!"), "기존 퀘스트의 인원 갱신은 최신 표시를 빼앗지 않음");
                Call(panel, "Hide");
                Call(panel, "Show", "추가 퀘스트", "다시 추가된 패널");
                Assert.IsTrue(title1.text.StartsWith("! "));
                Assert.IsFalse(title0.text.StartsWith("!"));
                Call(quest, "Hide");
                Assert.IsFalse(title1.text.StartsWith("!"), "한 개만 남으면 신규 표시를 제거한다");
                Call(panel, "Hide");
            }
            finally
            {
                if (panel != null) Call(panel, "Hide");
                T(GuideType).GetMethod("Hide").Invoke(null, null);
                if (probe != null) UnityEngine.Object.Destroy(probe);
                if (tutorialToggle != null) tutorialToggle.enabled = wasEnabled;
            }
        }

        [UnityTest]
        [Category("BreakFlow")] public IEnumerator W3_TierButtons_NightChoiceAndRestTimeAdjust()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Assert.AreEqual(true, Call(run, "RecordClear", "Tightrope", 1));
            yield return OpenSchedule();
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var book = Get(Find("CurtainCall.WaitingRoom.ScheduleBoard"), "Book");
            var tiers = (UnityEngine.UI.Button[,])Private(ui, "tierButtons");
            Assert.IsFalse(tiers[0, 0].gameObject.activeSelf);
            Assert.IsTrue(tiers[2, 1].interactable);
            Assert.IsFalse(tiers[2, 2].interactable);
            tiers[2, 1].onClick.Invoke();
            Assert.AreEqual(2, Call(book, "Difficulty", 2, 0));
            yield return ReadyToMorning(2, ui);
            Assert.AreEqual(2, Field(State, "Difficulty"));
            Assert.AreEqual(true, Call(run, "RecordClear", "Tightrope", 2));
            yield return OpenSchedule(); yield return new WaitForSecondsRealtime(.3f);
            ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            tiers = (UnityEngine.UI.Button[,])Private(ui, "tierButtons");
            Assert.IsTrue(tiers[2, 2].interactable);
            tiers[2, 2].onClick.Invoke();
            Assert.AreEqual(3, Field(State, "Difficulty"));
            Assert.AreEqual("Tightrope", Field(State, "TrickId"));
        }
        [UnityTest]
        [Category("BreakFlow")] public IEnumerator W3_InstantSchedulesAndSleep_ThreeNightsAndFinalMorning()
        {
            for (int day = 1; day <= 3; day++)
            {
                yield return Launch(day, "Night", 0, "", "WaitingRoomBase");
                yield return OpenSchedule();
                var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
                var schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard");
                var book = Get(schedule, "Book");
                Assert.IsFalse(((Component)Private(ui, "confirmButton")).gameObject.activeSelf);
                Assert.AreEqual(false, Get(book, "IsConfirmedTonight"));
                Assert.AreEqual("", Call(book, "Get", 4, 0));
                if (day < 3)
                {
                    Assert.AreEqual(true, Call(schedule, "CanEdit", day + 1, 0));
                    Call(ui, "Cycle", day + 1, 0); Call(ui, "Cycle", day + 1, 0);
                    Assert.AreEqual("Tightrope", Call(book, "Get", day + 1, 0), "One unlocked choice never cycles to empty");
                    Assert.AreEqual(false, Call(book, "IsLocked", day + 1));
                }
                Assert.AreEqual(false, Call(schedule, "CanEdit", day, 0), "Completed shows cannot change");
                yield return ReadyToMorning(day + 1, ui);
            }
        }
        [UnityTest]
        public IEnumerator W3_HumanCannonSet_FAcquireUnlocksAndMovesToPractice()
        {
            // 2026-10-30: 무대 장치 보관실의 인간 대포·트램펄린 세트를 F로 획득하면 공연이 열리고 기물이 연습실로 옮겨진다.
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            yield return null;
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            var set = Find("CurtainCall.WaitingRoom.PerformancePropDiscovery");
            var map = (GameObject[])Private(set, "mapProps"); var practice = (GameObject[])Private(set, "practiceProps");
            Assert.AreEqual(3, map.Length); Assert.AreEqual(2, practice.Length);
            Assert.AreEqual(false, Call(run, "IsTrickUnlocked", "HumanCannon"));
            Assert.IsTrue(map.All(m => m.activeInHierarchy), "획득 전 보관실에 있다");
            Assert.IsTrue(practice.All(p => !p.activeSelf), "획득 전 연습실에는 없다");
            var pickup = map[0].GetComponent(T("CurtainCall.WaitingRoom.PerformancePropPickup"));
            Assert.AreEqual(true, Get(pickup, "IsAvailable"));
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var point = (Vector3)Get(pickup, "PresentationPoint");
            Call(player.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", new Vector3(point.x + 2f, map[0].transform.position.y + .1f, point.z), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.GreaterOrEqual((float)Call(pickup, "Reach", player.transform.position, player), 0f, "F가 닿는다");
            Call(pickup, "Interact");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(true, Call(run, "IsTrickUnlocked", "HumanCannon"));
            Assert.IsTrue(map.All(m => !m.activeSelf), "획득하면 보관실에서 사라진다");
            Assert.IsTrue(practice.All(p => p.activeInHierarchy), "연습실에 보인다");
        }
    }
}
