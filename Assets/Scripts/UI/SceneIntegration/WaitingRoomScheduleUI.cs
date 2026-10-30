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
    public sealed class WaitingRoomScheduleUI : MonoBehaviour
    {
        [SerializeField] SchedulePanelView view;
        [SerializeField] UnityEngine.UI.Button[] editButtons;
        [SerializeField] UnityEngine.UI.Button confirmButton, readyButton;
        // 카드마다 단계 버튼 1~3(Task-082). 씬을 고치지 않도록 실행 중에 공용 버튼 프리팹으로 만든다.
        UnityEngine.UI.Button[,] tierButtons;
        static readonly Color SelectedTier = new(1f, .84f, .45f, 1f);
        ScheduleBoardView boardView;
        EventSystem ownedEvents;
        string session, lastKey, message = "";
        float nextRefresh;
        int revision;
        public SchedulePanelView View => view;
        public string HoverDescription(UnityEngine.UI.Button button)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null) return "일정 정보를 기다리는 중입니다.";
            if (button == confirmButton)
                return schedule.Book.IsConfirmedTonight ? "이번 밤 일정이 확정되었습니다." : "호스트가 편성을 확정합니다. 확정한 일정은 다시 편집할 수 없습니다.";
            int tierIndex = TierIndex(button, out int tier);
            if (tierIndex >= 0) return TierDescription(schedule, tierIndex / 2 + 1, tierIndex % 2, tier);
            int index = Array.IndexOf(editButtons, button);
            if (index < 0) return null;
            int day = index / 2 + 1;
            return day == 1 ? "1일차 공연은 고정입니다." : schedule.Book.IsLocked(day) ? "확정된 일정입니다." :
                schedule.HasAuthority && schedule.Book.IsEditable(day) ? "클릭할 때마다 공연이 바뀝니다. 난이도는 카드 아래 단계 버튼으로 고릅니다." : "밤 정비 시간에 호스트가 편집합니다.";
        }
        string TierDescription(ScheduleBoard schedule, int day, int slot, int tier)
        {
            string trick = schedule.Book.Get(day, slot);
            string reward = $"{tier}단계 · {ScheduleBoard.TierRewardText(tier)}";
            if (!schedule.HasAuthority) return reward + "\n단계는 호스트가 정합니다.";
            if (tier > schedule.MaxTier(trick)) return reward + $"\n{schedule.TrickName(trick)} {tier - 1}단계를 깨면 열립니다.";
            return CanEditTier(schedule, day, slot) ? reward + (schedule.IsNextPerformance(day, slot) ? "\n다음 공연 입장 전까지 바꿀 수 있습니다." : "")
                : reward + "\n밤 정비 또는 그 공연 직전 쉬는 시간에 바꿀 수 있습니다.";
        }
        int TierIndex(UnityEngine.UI.Button button, out int tier)
        {
            tier = 0;
            if (tierButtons == null || button == null) return -1;
            for (int i = 0; i < tierButtons.GetLength(0); i++)
                for (int t = 0; t < tierButtons.GetLength(1); t++)
                    if (tierButtons[i, t] == button) { tier = t + 1; return i; }
            return -1;
        }
        // 밤 정비 편집 중이거나, 쉬는 시간의 바로 다음 공연이면 호스트가 단계를 고칠 수 있다.
        static bool CanEditTier(ScheduleBoard schedule, int day, int slot)
        {
            var run = CampaignRun.Instance;
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            return schedule.HasAuthority && day > 1 && !string.IsNullOrEmpty(schedule.Book.Get(day, slot))
                && ((night && schedule.Book.IsEditable(day)) || schedule.IsNextPerformance(day, slot));
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
            confirmButton.onClick.AddListener(Confirm);
            EnsureTierButtons();
            if (tierButtons == null) return;
            for (int i = 0; i < tierButtons.GetLength(0); i++)
                for (int t = 0; t < tierButtons.GetLength(1); t++)
                {
                    int index = i, tier = t + 1;
                    tierButtons[i, t].onClick.AddListener(() => ChooseTier(index / 2 + 1, index % 2, tier));
                }
        }
        void EnsureTierButtons()
        {
            if (tierButtons != null || editButtons == null) return;
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_General");
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
                    if (label != null) { label.fontSize = 16; label.text = $"{t + 1}단계"; }
                    buttons[i, t] = go.GetComponent<UnityEngine.UI.Button>();
                    go.SetActive(false);
                }
            tierButtons = buttons;
        }
        void Update()
        {
            if (view == null) return;
            var room = CommonUiSceneLoader.CurrentWaitingRoom;
            if (boardView != null && (room == null || boardView.gameObject.scene != room.gameObject.scene))
            { boardView.SetOpen(false); view.Hide(); boardView = null; }
            if (boardView == null && room != null)
                boardView = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ScheduleBoardView>()).FirstOrDefault(b => b.isActiveAndEnabled);
            if (ownedEvents != null && FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Any(e => e != ownedEvents && e.isActiveAndEnabled))
            { Destroy(ownedEvents.gameObject); ownedEvents = null; }
            bool open = boardView != null && boardView.isActiveAndEnabled && boardView.IsOpen && ScheduleBoard.Instance != null;
            if (!open) { if (view.IsVisible) view.Hide(); return; }
            if (!view.IsVisible)
            {
                if (EventSystem.current == null)
                {
                    var events = new GameObject("ScheduleEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    events.transform.SetParent(transform, false); ownedEvents = events.GetComponent<EventSystem>();
                }
                var phone = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>()).FirstOrDefault(p => p.isActiveAndEnabled);
                if (phone != null) phone.SetOpen(false);
                session = Guid.NewGuid().ToString("N"); message = ""; lastKey = null;
                view.Open(Build());
                RefreshControls();
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            var schedule = ScheduleBoard.Instance;
            var run = CampaignRun.Instance;
            string key = $"{schedule.Book.Revision}|{schedule.HasAuthority}|{run?.State.Day}|{run?.State.Phase}|{run?.State.PerformanceIndex}|{run?.State.TrickId}|{run?.State.Difficulty}|{run?.State.ReadyCount}|{run?.State.RequiredCount}|{run?.IsLocalReady}|{message}";
            if (lastKey == key) return;
            lastKey = key; view.SetPresentation(Build()); RefreshControls();
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
                    string title = string.IsNullOrEmpty(trick) ? "미편성" : schedule.TrickName(trick);
                    entries[slot] = new ScheduleEntryPresentation($"{day}:{slot}", $"공연 {slot + 1} · {title}",
                        string.IsNullOrEmpty(trick) ? "공연을 선택해 주세요" : $"{book.Difficulty(day, slot)}단계 · {ScheduleBoard.TierRewardText(book.Difficulty(day, slot))}",
                        // 단계 버튼이 진행 문구 자리를 쓴다(확정 여부는 도장으로 보인다).
                        tierButtons != null && day > 1 && !string.IsNullOrEmpty(trick) ? "" :
                        day == 1 ? "고정" : book.IsLocked(day) ? "확정" : book.IsEditable(day) && schedule.HasAuthority ? "클릭하여 변경" : "미확정",
                        false, book.IsLocked(day));
                }
                days[day - 1] = new ScheduleDayPresentation(day.ToString(), $"{day}일차", run != null && run.State.Day == day, "", entries);
            }
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            string context = night ? $"{run.State.Day}일차 밤 · 준비 {run.State.ReadyCount}/{run.State.RequiredCount}"
                : run != null && run.State.Phase == CampaignPhase.Dawn ? $"{run.State.Day}일차 밤 · 스케줄표를 닫으면 다음 날 아침으로 넘어갑니다"
                : run != null && run.State.Phase == CampaignPhase.DemoEnding ? "4일차 아침 · 스케줄표를 닫으면 데모 종료 연출이 시작됩니다"
                : "공연 일정 1~3일 · 밤 정비 때 편집";
            string footer = !string.IsNullOrEmpty(message) ? message : run != null && (run.State.Phase == CampaignPhase.Dawn || run.State.Phase == CampaignPhase.DemoEnding)
                ? "스케줄표를 닫아 연출을 진행하세요" : !night ? RestFooter(schedule, run) :
                run.State.Day >= 3 ? "준비 완료는 숙소 침대에서" :!schedule.HasAuthority ? "호스트가 일정 편집·확정" : book.IsConfirmedTonight ? "이번 밤 일정 확정 완료" : "카드를 클릭해 묘기 변경 · 단계 버튼으로 난이도 선택";
            return new SchedulePresentation(session ?? "unopened", (++revision).ToString(), "공연 스케줄", context, footer, days);
        }
        // 쉬는 시간: 바로 다음 공연의 단계를 입장 전까지 바꿀 수 있고, 더 높은 단계가 열렸으면 알린다.
        static string RestFooter(ScheduleBoard schedule, CampaignRun run)
        {
            if (run == null || run.State.Phase != CampaignPhase.RestTime || run.State.IsFinalMorning) return "공연 일정 보기";
            int day = run.State.Day, slot = run.State.PerformanceIndex;
            if (!schedule.IsNextPerformance(day, slot)) return "공연 일정 보기";
            if (!schedule.HasAuthority) return $"다음 공연 {schedule.Book.Difficulty(day, slot)}단계 · 단계는 호스트가 입장 전까지 바꿀 수 있습니다";
            int current = schedule.Book.Difficulty(day, slot), max = schedule.MaxTier(schedule.Book.Get(day, slot));
            return max > current ? $"{max}단계가 열렸습니다 · 다음 공연 입장 전까지 단계를 올릴 수 있습니다"
                : "다음 공연 단계는 입장 전까지 바꿀 수 있습니다";
        }
        void RefreshControls()
        {
            var schedule = ScheduleBoard.Instance;
            var run = CampaignRun.Instance;
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            for (int i = 0; i < editButtons.Length; i++)
            {
                int day = i / 2 + 1;
                bool edit = night && schedule.HasAuthority && schedule.Book.IsEditable(day);
                editButtons[i].interactable = edit;
            }
            RefreshTierButtons(schedule);
            confirmButton.gameObject.SetActive(night && schedule.HasAuthority && ScheduleBook.EditableDays(run.State.Day).Length > 0);
            confirmButton.interactable = !schedule.Book.IsConfirmedTonight;
            Label(confirmButton, schedule.Book.IsConfirmedTonight ? "확정 완료" : "일정 확정");
            // M2: 밤 정비 준비 완료는 숙소 침대에서 한다(Task-079). 스케줄표 화면의 준비 버튼은 쓰지 않는다.
            readyButton.gameObject.SetActive(false);
            GetComponentInParent<UiRuntimeFontFallback>()?.ApplyTo(transform);
        }
        static void Label(UnityEngine.UI.Button button, string text) => button.GetComponentInChildren<TMP_Text>(true).text = text;
        void RefreshTierButtons(ScheduleBoard schedule)
        {
            if (tierButtons == null) return;
            for (int i = 0; i < tierButtons.GetLength(0); i++)
            {
                int day = i / 2 + 1, slot = i % 2;
                string trick = schedule.Book.Get(day, slot);
                bool show = day > 1 && !string.IsNullOrEmpty(trick);
                bool edit = show && CanEditTier(schedule, day, slot);
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
                    Label(button, tier == selected ? $"<b>{tier}단계</b>" : $"{tier}단계");
                }
            }
        }
        public void Cycle(int day, int slot)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null || !view.IsVisible || !schedule.HasAuthority || !schedule.Book.IsEditable(day)) return;
            // 카드는 묘기만 차례로 돈다: 묘기 A → 묘기 B → … → 빈 칸. 단계는 단계 버튼으로 고르고, 새 묘기에서 못 고르는 단계면 낮춘다.
            var choices = schedule.SelectableTricks;
            string trick = schedule.Book.Get(day, slot);
            int next = Array.IndexOf(choices, trick) + 1;
            string nextTrick = next < choices.Length ? choices[next] : "";
            int tier = Math.Clamp(schedule.Book.Difficulty(day, slot), 1, schedule.MaxTier(nextTrick));
            var result = schedule.Set(day, slot, nextTrick, tier);
            message = ScheduleBoard.Explain(result); lastKey = null; nextRefresh = 0;
        }
        public void ChooseTier(int day, int slot, int tier)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null || !view.IsVisible || !CanEditTier(schedule, day, slot)) return;
            var run = CampaignRun.Instance;
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            var result = night && schedule.Book.IsEditable(day)
                ? schedule.Set(day, slot, schedule.Book.Get(day, slot), tier)
                : schedule.AdjustTier(day, slot, tier);
            message = result == ScheduleRefusal.None && !night ? $"다음 공연을 {tier}단계로 바꿨습니다" : ScheduleBoard.Explain(result);
            lastKey = null; nextRefresh = 0;
        }
        public void Confirm()
        {
            if (!view.IsVisible || ScheduleBoard.Instance == null) return;
            var result = ScheduleBoard.Instance.Confirm();
            message = result == ScheduleRefusal.None ? "일정이 확정되었습니다" : ScheduleBoard.Explain(result);
            lastKey = null; nextRefresh = 0;
        }
        void Closed() { if (boardView != null) boardView.SetOpen(false); }
        void OnDisable()
        {
            if (view == null) return;
            view.Hide(); view.Closed -= Closed; Closed();
            foreach (var button in editButtons) button.onClick.RemoveAllListeners();
            if (tierButtons != null) foreach (var button in tierButtons) button.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveListener(Confirm);
            if (ownedEvents != null) Destroy(ownedEvents.gameObject);
        }
    }
}
