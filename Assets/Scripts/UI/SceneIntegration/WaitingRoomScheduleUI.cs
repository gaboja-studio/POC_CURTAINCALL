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
            if (button == readyButton)
                return schedule.CanReady(out var reason) ? "준비 완료/취소를 전환합니다. 전원 준비 후 다음 시점으로 진행합니다." : reason;
            if (button == confirmButton)
                return schedule.Book.IsConfirmedTonight ? "이번 밤 일정이 확정되었습니다." : "호스트가 편성을 확정합니다. 확정한 일정은 다시 편집할 수 없습니다.";
            int index = Array.IndexOf(editButtons, button);
            if (index < 0) return null;
            int day = index / 2 + 1;
            return day == 1 ? "1일차 공연은 고정입니다." : schedule.Book.IsLocked(day) ? "확정된 일정입니다." :
                schedule.HasAuthority && schedule.Book.IsEditable(day) ? "클릭할 때마다 공연을 변경합니다. 난도는 출연 순번에 따라 결정됩니다." : "밤 정비 시간에 호스트가 편집합니다.";
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
            readyButton.onClick.AddListener(ToggleReady);
        }
        void Update()
        {
            if (view == null) return;
            var room = CommonUiSceneLoader.CurrentWaitingRoom;
            if (boardView != null && (room == null || boardView.gameObject.scene != room.gameObject.scene))
            { boardView.SetOpen(false); view.Hide(); boardView = null; }
            if (boardView == null && room != null)
                boardView = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ScheduleBoardView>()).FirstOrDefault();
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
                var phone = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>()).FirstOrDefault();
                if (phone != null) phone.SetOpen(false);
                session = Guid.NewGuid().ToString("N"); message = ""; lastKey = null;
                view.Open(Build());
                RefreshControls();
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            var schedule = ScheduleBoard.Instance;
            var run = CampaignRun.Instance;
            string key = $"{schedule.Book.Revision}|{schedule.HasAuthority}|{run?.State.Day}|{run?.State.Phase}|{run?.State.ReadyCount}|{run?.State.RequiredCount}|{run?.IsLocalReady}|{message}";
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
                        string.IsNullOrEmpty(trick) ? "공연을 선택해 주세요" : $"난도 {book.Difficulty(day, slot)} · 출연 순번 기준",
                        day == 1 ? "고정" : book.IsLocked(day) ? "확정" : book.IsEditable(day) && schedule.HasAuthority ? "클릭하여 변경" : "미확정",
                        false, book.IsLocked(day));
                }
                days[day - 1] = new ScheduleDayPresentation(day.ToString(), $"{day}일차 · {(day == 2 ? "시설" : "의료")}", run != null && run.State.Day == day, "", entries);
            }
            bool night = run != null && run.State.Phase == CampaignPhase.Night;
            string context = night ? $"{run.State.Day}일차 밤 · 준비 {run.State.ReadyCount}/{run.State.RequiredCount}" : "공연 일정 1~3일 · 밤 정비 때 편집";
            string footer = !string.IsNullOrEmpty(message) ? message : !night ? "4일차는 아침 쉬는 시간 이후 종료" :
                run.State.Day >= 3 ? "추가 편성 없이 준비할 수 있습니다" : !schedule.HasAuthority ? "호스트가 일정 편집·확정" : book.IsConfirmedTonight ? "이번 밤 일정 확정 완료" : "공연 항목을 클릭해 묘기 변경";
            return new SchedulePresentation(session ?? "unopened", (++revision).ToString(), "공연 스케줄", context, footer, days);
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
            confirmButton.gameObject.SetActive(night && schedule.HasAuthority && ScheduleBook.EditableDays(run.State.Day).Length > 0);
            confirmButton.interactable = !schedule.Book.IsConfirmedTonight;
            Label(confirmButton, schedule.Book.IsConfirmedTonight ? "확정 완료" : "일정 확정");
            readyButton.gameObject.SetActive(night);
            readyButton.interactable = run != null && (run.IsLocalReady || schedule.CanReady(out _));
            Label(readyButton, run != null && run.IsLocalReady ? "준비 취소" : "준비 완료");
            GetComponentInParent<UiRuntimeFontFallback>()?.ApplyTo(transform);
        }
        static void Label(UnityEngine.UI.Button button, string text) => button.GetComponentInChildren<TMP_Text>(true).text = text;
        public void Cycle(int day, int slot)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null || !view.IsVisible || !schedule.HasAuthority || !schedule.Book.IsEditable(day)) return;
            var choices = schedule.SelectableTricks;
            int next = Array.IndexOf(choices, schedule.Book.Get(day, slot)) + 1;
            var result = schedule.Set(day, slot, next < choices.Length ? choices[next] : "");
            message = ScheduleBoard.Explain(result); lastKey = null; nextRefresh = 0;
        }
        public void Confirm()
        {
            if (!view.IsVisible || ScheduleBoard.Instance == null) return;
            var result = ScheduleBoard.Instance.Confirm();
            message = result == ScheduleRefusal.None ? "일정이 확정되었습니다" : ScheduleBoard.Explain(result);
            lastKey = null; nextRefresh = 0;
        }
        public void ToggleReady()
        {
            if (!view.IsVisible || CampaignRun.Instance == null || ScheduleBoard.Instance == null) return;
            var run = CampaignRun.Instance;
            bool accepted = ScheduleBoard.Instance.RequestReady(!run.IsLocalReady);
            message = accepted ? "" : ScheduleBoard.Instance.LastMessage;
            lastKey = null; nextRefresh = 0;
        }
        void Closed() { if (boardView != null) boardView.SetOpen(false); }
        void OnDisable()
        {
            if (view == null) return;
            view.Hide(); view.Closed -= Closed; Closed();
            foreach (var button in editButtons) button.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveListener(Confirm); readyButton.onClick.RemoveListener(ToggleReady);
            if (ownedEvents != null) Destroy(ownedEvents.gameObject);
        }
    }
}
