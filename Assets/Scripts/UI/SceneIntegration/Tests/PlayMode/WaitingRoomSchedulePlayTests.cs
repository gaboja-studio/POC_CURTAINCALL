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
            Assert.AreEqual(day == 4 && Application.version == "0.1.0" ? "DemoEnding" : "RestTime", Field(State, "Phase").ToString());
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
        public IEnumerator W7_FirstDayWorldScheduleConfirmsAndWarnsAtEntrance()
        {
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/quest-flow");
            yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
            yield return WaitQuestReady("gather");
            var campaign = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            var gather = Find("CurtainCall.WaitingRoom.StageEntranceGather");
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "Relocate", gather.GetComponent<BoxCollider>().bounds.center - Vector3.up * .5f, Quaternion.identity);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual("RestTime", Field(State, "Phase").ToString());
            StringAssert.Contains("공연 일정 미확정", (string)Static("CurtainCall.WaitingRoom.WaitingRoomGuidePanel", "CurrentTitle"));
            Assert.IsFalse((bool)Call(campaign, "NotifyAllAtStageEntrance"));
            var guide = T("CurtainCall.WaitingRoom.WaitingRoomGuidePanel");
            guide.GetMethod("Hide").Invoke(null, null);
            // 2026-11-04 사용자: 스케줄표를 보는 동안 다른 화면 UI는 가리고, 닫은 뒤 출력된 순서대로 다시 띄운다.
            guide.GetMethod("Show").Invoke(null, new object[] { "순서 확인 A", "스케줄표를 열기 전에 뜬 안내", 3f });
            yield return null;
            var hud = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "BackstageHudCanvas" && c.enabled);
            Assert.IsNotNull(hud, "열기 전에는 탐색 HUD가 보인다");
            yield return OpenSchedule();
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse(hud.enabled, "스케줄표를 보는 동안 탐색 HUD를 가린다");
            Assert.IsFalse(((Canvas)UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "GuidePanelCanvas")).enabled, "보던 중앙 안내도 가린다");
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var schedule = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
            var book = Get(schedule, "Book");
            Assert.IsTrue((bool)Call(campaign, "DiscoverTrick", "HumanCannon"));
            // 획득 안내는 발견 처리 뒤 프레임에 출력된다. 그 뒤에 B를 출력해 순서를 고정한다.
            float discoverUntil = Time.realtimeSinceStartup + 3f;
            while ((int)guide.GetProperty("PendingCount").GetValue(null) < 1 && Time.realtimeSinceStartup < discoverUntil) yield return null;
            Assert.IsFalse((bool)guide.GetProperty("IsVisible").GetValue(null) && ((Canvas)UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "GuidePanelCanvas")).enabled, "보는 동안 획득 안내가 판을 가리지 않는다");
            guide.GetMethod("Show").Invoke(null, new object[] { "순서 확인 B", "스케줄표를 보는 동안 온 안내", 3f });
            yield return null;
            Assert.AreEqual(2, guide.GetProperty("PendingCount").GetValue(null), "보는 동안 온 안내(획득·B)는 기다린다");
            var edits = (UnityEngine.UI.Button[])Priv(ui, "editButtons");
            var pointer = InputSystem.AddDevice<Mouse>();
            var inputModule = UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Assert.IsNotNull(inputModule.point?.action, "실제 마우스 위치 액션이 연결되어야 한다");
            Assert.IsNotNull(inputModule.leftClick?.action, "실제 마우스 클릭 액션이 연결되어야 한다");
            int actualClicks = 0;
            edits[1].onClick.AddListener(() => actualClicks++);
            yield return ClickScheduleButtonWithMouse(pointer, edits[1]);
            Assert.AreEqual(1, actualClicks, "실제 포인터 클릭은 카드에 한 번 전달되어야 한다");
            Assert.AreEqual("HumanCannon", Call(book, "Get", 1, 1));
            yield return ClickScheduleButtonWithMouse(pointer, edits[1]);
            Assert.AreEqual(2, actualClicks);
            Assert.AreEqual("Tightrope", Call(book, "Get", 1, 1));
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/followup");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/followup/world-schedule-before-confirm.png"));
            yield return null;
            yield return ClickScheduleButtonWithMouse(pointer, (UnityEngine.UI.Button)Priv(ui, "confirmButton"));
            InputSystem.RemoveDevice(pointer);
            Assert.IsTrue((bool)Get(book, "FirstDayConfirmed"));
            Assert.AreEqual("RestTime", Field(State, "Phase").ToString());
            yield return null;
            Assert.IsFalse(((UnityEngine.UI.Button)Priv(ui, "confirmButton")).gameObject.activeSelf, "확정 뒤 조작 버튼도 즉시 사라져야 한다");
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/followup/world-schedule.png"));
            yield return null; yield return null;
            var board = Find("CurtainCall.WaitingRoom.ScheduleBoardView");
            Call(board, "SetOpen", false);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(hud.enabled, "닫으면 탐색 HUD가 돌아온다");
            Assert.AreEqual("순서 확인 A", guide.GetProperty("CurrentTitle").GetValue(null), "열기 전 안내가 남은 시간만큼 먼저 다시 뜬다");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/followup/world-schedule-after-close.png"));
            yield return null;
            guide.GetMethod("Hide").Invoke(null, null); yield return null; yield return null;
            StringAssert.Contains("획득", (string)guide.GetProperty("CurrentTitle").GetValue(null), "다음은 보는 동안 먼저 온 획득 안내");
            guide.GetMethod("Hide").Invoke(null, null); yield return null; yield return null;
            Assert.AreEqual("순서 확인 B", guide.GetProperty("CurrentTitle").GetValue(null), "마지막은 나중에 온 안내");
            guide.GetMethod("Hide").Invoke(null, null);
            var canvas = ((Component)Get(ui, "View")).GetComponentInParent<Canvas>();
            Assert.IsFalse(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled, "닫은 뒤 판은 표시하되 입력을 가로채지 않는다");
            Assert.IsFalse((bool)Get(ui, "IsEditing"));
            Assert.IsFalse((bool)T("CurtainCall.UI.SceneIntegration.DemoRunEnding").GetMethod("ScheduleIsOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null), "판 표시가 다음 날/데모 전환을 막으면 안 됨");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(c => c.name == "ScheduleBoardWorldUI"));
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
        public IEnumerator W3_TierButtons_NightChoiceAndRestTimeAdjust()
        {
            // Task-082: 카드는 묘기, 단계 버튼은 난이도. 쉬는 시간에는 바로 다음 공연의 단계만 다시 고른다.
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Assert.AreEqual(true, Call(run, "RecordClear", "Tightrope", 1));
            Assert.AreEqual(true, Call(run, "DiscoverTrick", "HumanCannon"), "인간 대포 발견(배치 기물)");
            yield return OpenSchedule();
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard");
            var book = Get(schedule, "Book");
            var edits = (Array)Private(ui, "editButtons");
            for (int i = 2; i < 6; i++) Call(Get(edits.GetValue(i), "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.3f);
            var tiers = (UnityEngine.UI.Button[,])Private(ui, "tierButtons");
            Assert.IsNotNull(tiers, "단계 버튼을 만들어야 한다");
            Assert.IsFalse(tiers[0, 0].gameObject.activeSelf, "1일차는 단계 고정");
            Assert.IsTrue(tiers[2, 0].gameObject.activeInHierarchy);
            Assert.IsTrue(tiers[2, 1].interactable, "1단계를 깼으면 2단계까지");
            Assert.IsFalse(tiers[2, 2].interactable, "3단계는 아직 잠김");
            tiers[2, 1].onClick.Invoke();
            Assert.AreEqual(2, Call(book, "Difficulty", 2, 0));
            Assert.AreEqual("Tightrope", Call(book, "Get", 2, 0), "단계 버튼은 묘기를 바꾸지 않는다");
            Call(Get(edits.GetValue(2), "onClick"), "Invoke");
            Assert.AreEqual("HumanCannon", Call(book, "Get", 2, 0), "카드는 묘기만 바꾼다");
            Assert.AreEqual(1, Call(book, "Difficulty", 2, 0), "못 고르는 단계는 낮춘다");
            Call(Get(edits.GetValue(2), "onClick"), "Invoke"); Call(Get(edits.GetValue(2), "onClick"), "Invoke");
            Assert.AreEqual("Tightrope", Call(book, "Get", 2, 0));
            tiers[2, 1].onClick.Invoke();
            Call(ui, "Confirm");
            Assert.AreEqual(true, Get(book, "IsConfirmedTonight"));
            yield return ReadyToMorning(2, ui);
            Assert.AreEqual(2, Field(State, "Difficulty"));

            // 같은 날 앞 공연에서 2단계를 깬 것처럼 기록하고, 아침 쉬는 시간에 3단계로 올린다.
            Assert.AreEqual(true, Call(run, "RecordClear", "Tightrope", 2));
            yield return OpenSchedule();
            yield return new WaitForSecondsRealtime(.3f);
            ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            tiers = (UnityEngine.UI.Button[,])Private(ui, "tierButtons");
            StringAssert.Contains("3단계가 개방되었습니다", (string)Get(Call(ui, "Build"), "Footer"));
            Assert.IsTrue(tiers[2, 2].interactable, "다음 공연은 쉬는 시간에 단계를 올릴 수 있다");
            Assert.IsFalse(tiers[3, 0].interactable, "다음 공연이 아니면 바꿀 수 없다");
            Assert.IsFalse(((UnityEngine.UI.Button)edits.GetValue(2)).interactable, "쉬는 시간에는 묘기를 바꾸지 않는다");
            tiers[2, 2].onClick.Invoke();
            Assert.AreEqual(3, Call(book, "Difficulty", 2, 0));
            Assert.AreEqual(3, Field(State, "Difficulty"), "런 진행 단계도 함께 바뀐다");
            Assert.AreEqual("Tightrope", Field(State, "TrickId"));
        }
        [UnityTest]
        public IEnumerator W3_EditConfirmReady_ThreeNightsAndFinalMorning()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            yield return OpenSchedule();
            var ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            var schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard");
            var book = Get(schedule, "Book");
            Assert.AreEqual(3, ((IEnumerable)Get(Call(ui, "Build"), "Days")).Cast<object>().Count());
            Call(ui, "Confirm");
            Assert.AreEqual(false, Get(book, "IsConfirmedTonight"));
            var edits = (Array)Private(ui, "editButtons");
            Assert.IsFalse(ui.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("EditDay")), "Separate editing buttons must be removed.");
            for (int i = 2; i < 6; i++)
            {
                var card = (UnityEngine.UI.Button)edits.GetValue(i);
                var events = UnityEngine.EventSystems.EventSystem.current;
                var rect = (RectTransform)card.transform;
                var pointer = new UnityEngine.EventSystems.PointerEventData(events) {
                    position = RectTransformUtility.WorldToScreenPoint(rect.GetComponentInParent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center)),
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                events.RaycastAll(pointer, hits);
                Assert.IsNotEmpty(hits);
                var clicked = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
                Assert.AreEqual(card.gameObject, clicked, "The visible schedule entry must receive the click.");
                UnityEngine.EventSystems.ExecuteEvents.Execute(clicked, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            }
            Assert.AreEqual("Tightrope", Call(book, "Get", 2, 0));
            // 발견하지 않은 인간 대포는 카드를 넘겨도 건너뛰고, 발견한 뒤부터 고를 수 있다.
            Call(Get(edits.GetValue(3), "onClick"), "Invoke");
            Assert.AreEqual("", Call(book, "Get", 2, 1), "미발견 공연은 건너뛴다");
            Assert.AreEqual(false, Call(Find("CurtainCall.WaitingRoom.ScheduleBoard"), "Set", 2, 1, "HumanCannon", 1).ToString() == "None", "호스트도 미발견 공연을 편성할 수 없다");
            Assert.AreEqual(true, Call(Static("CurtainCall.Campaign.CampaignRun", "Instance"), "DiscoverTrick", "HumanCannon"));
            Call(Get(edits.GetValue(3), "onClick"), "Invoke"); Call(Get(edits.GetValue(3), "onClick"), "Invoke");
            Assert.AreEqual("HumanCannon", Call(book, "Get", 2, 1));
            Call(Get(Private(ui, "confirmButton"), "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(true, Get(book, "IsConfirmedTonight"));
            Assert.AreEqual(true, Call(book, "IsLocked", 2));
            Assert.AreEqual(false, Call(book, "IsLocked", 3));
            Assert.IsTrue(((Component)edits.GetValue(2)).gameObject.activeInHierarchy, "Locked entries stay visible.");
            Assert.AreEqual(false, Get(edits.GetValue(2), "interactable"));
            Call(ui, "Cycle", 2, 0);
            Assert.AreEqual("Tightrope", Call(book, "Get", 2, 0));
            yield return ReadyToMorning(2, ui);
            Call(Get(ui, "View"), "Close");
            yield return null;
            Assert.AreEqual(false, Get(Find("CurtainCall.WaitingRoom.ScheduleBoardView"), "IsOpen"));
            Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));

            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            yield return OpenSchedule();
            ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard"); book = Get(schedule, "Book");
            Assert.AreEqual(false, Call(book, "IsEditable", 2));
            Call(ui, "Cycle", 3, 0); Call(ui, "Cycle", 3, 1); Call(ui, "Confirm");
            Assert.AreEqual(true, Call(book, "IsLocked", 3));
            yield return ReadyToMorning(3, ui);

            yield return Launch(3, "Night", 0, "", "WaitingRoomBase");
            yield return OpenSchedule();
            ui = Find("CurtainCall.UI.SceneIntegration.WaitingRoomScheduleUI");
            schedule = Find("CurtainCall.WaitingRoom.ScheduleBoard"); book = Get(schedule, "Book");
            Assert.AreEqual(true, Get(book, "IsConfirmedTonight"));
            Assert.AreEqual("", Call(book, "Get", 4, 0));
            Assert.IsFalse(((Component)Private(ui, "confirmButton")).gameObject.activeSelf);
            // Older wire data must not reintroduce day-four performances or a confirmation gate.
            var oldSlots = (string[])Call(book, "ExportSlots"); oldSlots[6] = "Tightrope"; oldSlots[7] = "HumanCannon";
            Assert.AreEqual(true, Call(book, "Load", 3, false, oldSlots, new bool[4]));
            Assert.AreEqual("", Call(book, "Get", 4, 0)); Assert.AreEqual(true, Get(book, "IsConfirmedTonight"));
            yield return ReadyToMorning(4, ui);
            Assert.AreEqual(Application.version != "0.1.0", Field(State, "IsFinalMorning"));
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
