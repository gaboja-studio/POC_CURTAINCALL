using System;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.WaitingRoom;
using CurtainCall.UI.ScheduleV1;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.UI.SceneIntegration
{
    public sealed partial class WaitingRoomScheduleUI : MonoBehaviour
    {
        [SerializeField] SchedulePanelView view;
        [SerializeField] UnityEngine.UI.Button[] editButtons;
        [SerializeField] UnityEngine.UI.Button confirmButton, readyButton;
        // 카드마다 단계 버튼 1~3(Task-082). 씬을 고치지 않도록 실행 중에 공용 버튼 프리팹으로 만든다.
        UnityEngine.UI.Button[,] tierButtons;
        ScheduleTicketCard[] ticketCards;
        static readonly Color SelectedTier = new(1f, .84f, .45f, 1f);
        ScheduleBoardView boardView;
        EventSystem ownedEvents;
        string session, lastKey, message = "";
        float nextRefresh;
        int revision;
        public SchedulePanelView View => view;
        public bool IsEditing => boardView != null && boardView.IsOpen;
        public string HoverDescription(UnityEngine.UI.Button button)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null) return WaitingRoomText.Get("schedule.ui-waiting", "일정 정보를 기다리는 중입니다.");

            int tierIndex = TierIndex(button, out int tier);
            if (tierIndex >= 0) return TierDescription(schedule, tierIndex / 2 + 1, tierIndex % 2, tier);
            int index = Array.IndexOf(editButtons, button);
            if (index < 0) return null;
            int day = index / 2 + 1;
            return schedule.HasAuthority && schedule.CanEdit(day, index % 2)
                ? "슬롯 클릭으로 공연을 변경합니다. 좌우 화살표는 난이도만 변경합니다."
                : "공연이 시작되기 전 쉬는 시간에 호스트가 변경할 수 있습니다.";
        }
        public string HoverTitle(UnityEngine.UI.Button button)
        {
            int index = TierIndex(button, out int tier);
            return index >= 0 ? $"{tier}단계" : "공연 변경";
        }
        string TierDescription(ScheduleBoard schedule, int day, int slot, int tier)
        {
            string trick = schedule.Book.Get(day, slot);
            string reward = ScheduleBoard.TierRewardText(tier);
            if (!schedule.HasAuthority) return WaitingRoomText.Format("schedule.tier-host-only", "{tier}단계 · {reward}\n단계는 호스트가 정합니다.", ("tier", tier), ("reward", reward));
            if (tier > schedule.MaxTier(trick)) return WaitingRoomText.Format("schedule.tier-locked", "{tier}단계 · {reward}\n{trick} {prev}단계를 깨면 열립니다.", ("tier", tier), ("reward", reward), ("trick", schedule.TrickName(trick)), ("prev", tier - 1));
            return CanEditTier(schedule, day, slot)
                ? schedule.IsNextPerformance(day, slot)
                    ? WaitingRoomText.Format("schedule.tier-next-performance", "{tier}단계 · {reward}\n다음 공연 입장 전까지 바꿀 수 있습니다.", ("tier", tier), ("reward", reward))
                    : WaitingRoomText.Format("schedule.tier-reward-line", "{tier}단계 · {reward}", ("tier", tier), ("reward", reward))
                : WaitingRoomText.Format("schedule.tier-rest-only", "{tier}단계 · {reward}\n밤 정비 또는 그 공연 직전 쉬는 시간에 바꿀 수 있습니다.", ("tier", tier), ("reward", reward));
        }
        int TierIndex(UnityEngine.UI.Button button, out int tier)
        {
            tier = 0;
            if (tierButtons == null || button == null) return -1;
            for (int i = 0; i < tierButtons.GetLength(0); i++)
                for (int t = 0; t < tierButtons.GetLength(1); t++)
                    if (tierButtons[i, t] == button)
                    {
                        tier = ticketCards != null ? (ScheduleBoard.Instance?.Book.Difficulty(i / 2 + 1, i % 2) ?? 1) + (t == 0 ? -1 : 1) : t + 1;
                        tier = Mathf.Clamp(tier, 1, ScheduleBook.MaxDifficulty);
                        return i;
                    }
            return -1;
        }
        // 밤 정비 편집 중이거나, 쉬는 시간의 바로 다음 공연이면 호스트가 단계를 고칠 수 있다.
        static bool CanEditTier(ScheduleBoard schedule, int day, int slot)
        {
            return schedule.HasAuthority && day > 1 && schedule.CanEdit(day, slot);
        }
        public void Configure(SchedulePanelView panel, UnityEngine.UI.Button[] edits, UnityEngine.UI.Button confirm, UnityEngine.UI.Button ready)
        { view = panel; editButtons = edits; confirmButton = confirm; readyButton = ready; }
        void OnEnable()
        {
            if (view == null) return;
            view.Closed += Closed;
            for (int i = 0; i < editButtons.Length; i++)
            {
                int index = i;
                editButtons[i].onClick.AddListener(() => Cycle(index / 2 + 1, index % 2));
            }
            confirmButton.gameObject.SetActive(false);
            EnsureTierButtons();
            if (tierButtons == null) return;
            for (int i = 0; i < tierButtons.GetLength(0); i++)
                for (int t = 0; t < tierButtons.GetLength(1); t++)
                {
                    int index = i, tier = t + 1;
                    int direction = t == 0 ? -1 : 1;
                    tierButtons[i, t].onClick.AddListener(() =>
                    {
                        if (ticketCards != null) StepTier(index, direction);
                        else ChooseTier(index / 2 + 1, index % 2, tier);
                    });
                }
        }
        void EnsureTierButtons()
        {
            ticketCards = view != null ? view.GetComponentsInChildren<ScheduleTicketCard>(true) : null;
            if (ticketCards != null && ticketCards.Length == 6)
            {
                tierButtons = new UnityEngine.UI.Button[6, 2];
                for (int i = 0; i < 6; i++) { tierButtons[i, 0] = ticketCards[i].previousButton; tierButtons[i, 1] = ticketCards[i].nextButton; }
                return;
            }
            ticketCards = null;
            if (tierButtons != null || editButtons == null) return;
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Roles/Retained/CommonButtons/Button_General");
            if (prefab == null) { Debug.LogWarning("[Schedule] 단계 버튼 프리팹이 없어 단계 선택을 표시하지 않습니다.", this); return; }
            var buttons = new UnityEngine.UI.Button[editButtons.Length, ScheduleBook.MaxDifficulty];
            for (int i = 0; i < editButtons.Length; i++)
                for (int t = 0; t < ScheduleBook.MaxDifficulty; t++)
                {
                    // 카드 아래 줄(진행 문구 자리, 확정 도장 왼쪽)에 놓는다. 카드 가운데 클릭은 그대로 묘기 변경.
                    var go = Instantiate(prefab, editButtons[i].transform, false);
                    go.name = $"Tier{t + 1}";
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(44 + t * 60, -78); rect.sizeDelta = new Vector2(56, 28);
                    var label = go.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                    {
                        label.fontSize = 16; label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 16;
                        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                        label.rectTransform.offsetMin = new Vector2(2, 0); label.rectTransform.offsetMax = new Vector2(-2, 0);
                        label.text = WaitingRoomText.Format("schedule.tier-button", "{tier}단계", ("tier", t + 1));
                    }
                    buttons[i, t] = go.GetComponent<UnityEngine.UI.Button>();
                    go.SetActive(false);
                }
            tierButtons = buttons;
        }
        bool wasOpen;
        void Update()
        {
            if (view == null && templateView != null) ReleaseBoardDisplay();
            if (view == null) return;
            var room = CommonUiSceneLoader.CurrentWaitingRoom;
            if (boardView != null && (room == null || boardView.gameObject.scene != room.gameObject.scene))
            { boardView.SetOpen(false); view.Hide(); ReleaseBoardDisplay(); boardView = null; }
            if (boardView == null && room != null)
                boardView = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ScheduleBoardView>()).FirstOrDefault(b => b.isActiveAndEnabled);
            if (ownedEvents != null && FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Any(e => e != ownedEvents && e.isActiveAndEnabled))
            { Destroy(ownedEvents.gameObject); ownedEvents = null; }
            bool open = boardView != null && boardView.isActiveAndEnabled && boardView.IsOpen && ScheduleBoard.Instance != null;
            if (open && !wasOpen)
            {
                if (EventSystem.current == null)
                {
                    var events = new GameObject("ScheduleEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    events.transform.SetParent(transform, false); ownedEvents = events.GetComponent<EventSystem>();
                }
                // 런타임 AddComponent로 만든 InputSystemUIInputModule에는 기본 포인터 액션이 없을 수 있다.
                // Raycaster만 존재하고 실제 마우스 클릭은 들어오지 않던 원인이다.
                var module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
                if (module != null && (module.point == null || module.leftClick == null)) module.AssignDefaultActions();
                var phone = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>()).FirstOrDefault(p => p.isActiveAndEnabled);
                if (phone != null) phone.SetOpen(false);
                session = Guid.NewGuid().ToString("N"); message = ""; lastKey = null;
            }
            wasOpen = open;
            TickBoardDisplay();
        }
        public SchedulePresentation Build()
        {
            var schedule = ScheduleBoard.Instance;
            var book = schedule.Book;
            var run = CampaignRun.Instance;
            var days = new ScheduleDayPresentation[3];
            for (int day = 1; day <= 3; day++)
            {
                var entries = new ScheduleEntryPresentation[2];
                for (int slot = 0; slot < 2; slot++)
                {
                    string trick = book.Get(day, slot);
                    string title = schedule.TrickName(trick);
                    entries[slot] = new ScheduleEntryPresentation($"{day}:{slot}", WaitingRoomText.Format("schedule.card-title", "공연 {n} · {title}", ("n", slot + 1), ("title", title)),
                        WaitingRoomText.Format("schedule.tier-reward-line", "{tier}단계 · {reward}", ("tier", book.Difficulty(day, slot)), ("reward", ScheduleBoard.TierRewardText(book.Difficulty(day, slot)))),
                        day == 1 && schedule.CanEdit(day, slot) ? (schedule.HasAuthority ? WaitingRoomText.Get("schedule.card-click", "클릭하여 변경") : "입장 전 변경 가능") : "",
                        false, false);
                }
                days[day - 1] = new ScheduleDayPresentation(day.ToString(), WaitingRoomText.Format("schedule.day-label", "{day}일차", ("day", day)), run != null && run.State.Day == day, "", entries);
            }
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            string context = night ? WaitingRoomText.Format("schedule.context-night", "{day}일차 밤 · 준비 {ready}/{total}", ("day", run.State.Day), ("ready", run.State.ReadyCount), ("total", run.State.RequiredCount))
                : run != null && run.State.Phase == CampaignPhase.Dawn ? WaitingRoomText.Format("schedule.context-dawn", "{day}일차 밤 · 스케줄표를 닫으면 다음 날 아침으로 넘어갑니다", ("day", run.State.Day))
                : run != null && run.State.Phase == CampaignPhase.DemoEnding ? WaitingRoomText.Get("schedule.context-demo-ending", "4일차 아침 · 스케줄표를 닫으면 데모 종료 연출이 시작됩니다")
                : schedule.IsFirstDayChoiceOpen ? WaitingRoomText.Get("schedule.context-first-day", "1일차 · 두 번째 공연 선택")
                : WaitingRoomText.Get("schedule.context-default", "공연 일정 1~3일 · 입장 전 변경 가능");
            // 기물 해금으로 새로 열린 공연은 편성할 때까지 스케줄표 위에 알린다(사용자 2026-11-04).
            if (schedule.TryGetNewTrick(out string newTrick))
                context += " · " + WaitingRoomText.Format("schedule.context-new-trick", "새 공연 {trick} 편성 가능", ("trick", schedule.TrickName(newTrick)));
            string footer = !string.IsNullOrEmpty(message) ? message : run != null && (run.State.Phase == CampaignPhase.Dawn || run.State.Phase == CampaignPhase.DemoEnding)
                ? WaitingRoomText.Get("schedule.footer-close-to-continue", "스케줄표를 닫아 연출을 진행하세요")
                : schedule.HasAuthority ? "선택 즉시 적용 · 공연 입장 전까지 변경 가능 · 수면은 숙소 침대에서" : "호스트가 공연 입장 전까지 일정을 변경할 수 있습니다";
            if (ticketCards != null && string.IsNullOrEmpty(message))
                footer = schedule.HasAuthority ? "슬롯 클릭: 공연 변경   |   좌우 화살표: 난이도 변경   ·   선택 즉시 적용" : "호스트가 공연과 난이도를 변경합니다 · 선택 내용은 모두에게 적용됩니다";
            return new SchedulePresentation(session ?? "unopened", (++revision).ToString(), WaitingRoomText.Get("schedule.title", "공연 스케줄"), context, footer, days);
        }
        void RefreshControls()
        {
            var schedule = ScheduleBoard.Instance;
            var run = CampaignRun.Instance;
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            for (int i = 0; i < editButtons.Length; i++)
            {
                int day = i / 2 + 1;
                editButtons[i].interactable = boardView != null && boardView.IsOpen && schedule.HasAuthority && schedule.CanEdit(day, i % 2);
            }
            RefreshTierButtons(schedule);
            confirmButton.gameObject.SetActive(false);
            // M2: 밤 정비 준비 완료는 숙소 침대에서 한다(Task-079). 스케줄표 화면의 준비 버튼은 쓰지 않는다.
            readyButton.gameObject.SetActive(false);
            GetComponentInParent<UiRuntimeFontFallback>()?.ApplyTo(boardDisplay != null ? boardDisplay : transform);
        }
        static void Label(UnityEngine.UI.Button button, string text) => button.GetComponentInChildren<TMP_Text>(true).text = text;
        void RefreshTierButtons(ScheduleBoard schedule)
        {
            if (ticketCards != null)
            {
                for (int i = 0; i < ticketCards.Length; i++)
                {
                    int day = i / 2 + 1, slot = i % 2;
                    var trick = schedule.Book.Get(day, slot);
                    ticketCards[i].Present(slot, schedule.TrickName(trick), schedule.Book.Difficulty(day, slot), schedule.MaxTier(trick),
                        schedule.HasAuthority && schedule.CanEdit(day, slot), CanEditTier(schedule, day, slot), schedule.HasAuthority, boardView != null && boardView.IsOpen);
                }
                return;
            }
            if (tierButtons == null) return;
            for (int i = 0; i < tierButtons.GetLength(0); i++)
            {
                int day = i / 2 + 1, slot = i % 2;
                string trick = schedule.Book.Get(day, slot);
                bool show = day > 1 && !string.IsNullOrEmpty(trick);
                bool edit = show && boardView != null && boardView.IsOpen && CanEditTier(schedule, day, slot);
                int selected = schedule.Book.Difficulty(day, slot), max = schedule.MaxTier(trick);
                for (int t = 0; t < tierButtons.GetLength(1); t++)
                {
                    var button = tierButtons[i, t];
                    button.gameObject.SetActive(show);
                    if (!show) continue;
                    int tier = t + 1;
                    // 고른 단계는 색으로, 아직 안 열린 단계는 비활성으로 보인다. 참가자는 보기만 한다.
                    button.interactable = edit && tier <= max;
                    if (button.targetGraphic != null) button.targetGraphic.color = tier == selected ? SelectedTier : Color.white;
                    // 작은 공용 버튼은 숫자만 표시한다. 선택은 색상, 단계 설명은 호버/카드 본문이 맡는다.
                    Label(button, tier.ToString());
                }
            }
        }
        public void Cycle(int day, int slot)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null || !view.IsVisible || boardView == null || !boardView.IsOpen || !schedule.HasAuthority || !schedule.CanEdit(day, slot)) return;
            var choices = Array.FindAll(schedule.SelectableTricks, ScheduleBoard.IsUnlocked);
            if (choices.Length == 0) return;
            string trick = schedule.Book.Get(day, slot);
            string nextTrick = choices[(Array.IndexOf(choices, trick) + 1) % choices.Length];
            int tier = Math.Clamp(schedule.Book.Difficulty(day, slot), 1, schedule.MaxTier(nextTrick));
            var result = schedule.Set(day, slot, nextTrick, tier);
            if (result == ScheduleRefusal.None && nextTrick != trick && ticketCards != null) ticketCards[(day - 1) * 2 + slot].Applied("공연 변경됨");
            message = ScheduleBoard.Explain(result); lastKey = null; nextRefresh = 0;
        }
        public void ChooseTier(int day, int slot, int tier)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null || !view.IsVisible || boardView == null || !boardView.IsOpen || !CanEditTier(schedule, day, slot)) return;
            var run = CampaignRun.Instance;
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            int previousTier = schedule.Book.Difficulty(day, slot);
            var result = schedule.Set(day, slot, schedule.Book.Get(day, slot), tier);
            if (result == ScheduleRefusal.None && previousTier != tier && ticketCards != null) ticketCards[(day - 1) * 2 + slot].Applied($"{tier}단계 적용됨");
            message = result == ScheduleRefusal.None && !night ? WaitingRoomText.Format("schedule.tier-changed", "다음 공연을 {tier}단계로 바꿨습니다", ("tier", tier)) : ScheduleBoard.Explain(result);
            lastKey = null; nextRefresh = 0;
        }
        public void Confirm()
        {
            // 이전 프리팹/리뷰 도구 호환성. 선택 즉시 적용하므로 확인 동작은 없다.
        }
        void StepTier(int index, int direction)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null) return;
            int day = index / 2 + 1, slot = index % 2;
            int next = schedule.Book.Difficulty(day, slot) + direction;
            if (next < 1 || next > schedule.MaxTier(schedule.Book.Get(day, slot))) return;
            ChooseTier(day, slot, next);
        }
        void Closed() { if (boardView != null) boardView.SetOpen(false); }
        void OnDisable()
        {
            if (view == null)
            {
                ReleaseBoardDisplay();
                if (ownedEvents != null) Destroy(ownedEvents.gameObject);
                return;
            }
            view.Hide(); view.Closed -= Closed; Closed();
            foreach (var button in editButtons) button.onClick.RemoveAllListeners();
            if (tierButtons != null) foreach (var button in tierButtons) button.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveListener(Confirm);
            ReleaseBoardDisplay();
            if (ownedEvents != null) Destroy(ownedEvents.gameObject);
        }
    }
}
