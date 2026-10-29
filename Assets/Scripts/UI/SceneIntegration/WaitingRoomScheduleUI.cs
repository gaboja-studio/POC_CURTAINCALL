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
            if (button == confirmButton)
                return schedule.Book.IsConfirmedTonight ? "이번 밤 일정이 확정되었습니다." : "호스트가 편성을 확정합니다. 확정한 일정은 다시 편집할 수 없습니다.";
            int index = Array.IndexOf(editButtons, button);
            if (index < 0) return null;
            int day = index / 2 + 1;
            return day == 1 ? "1일차 공연은 고정입니다." : schedule.Book.IsLocked(day) ? "확정된 일정입니다." :
                schedule.HasAuthority && schedule.Book.IsEditable(day) ? "클릭할 때마다 공연과 난이도 단계를 바꿉니다. 깬 단계의 다음 단계까지 고를 수 있습니다." : "밤 정비 시간에 호스트가 편집합니다.";
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
                        string.IsNullOrEmpty(trick) ? "공연을 선택해 주세요" : $"{book.Difficulty(day, slot)}단계 · {ScheduleBoard.TierRewardText(book.Difficulty(day, slot))}",
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
                ? "스케줄표를 닫아 연출을 진행하세요" : !night ? "공연 일정 보기" :
                run.State.Day >= 3 ? "준비 완료는 숙소 침대에서" :!schedule.HasAuthority ? "호스트가 일정 편집·확정" : book.IsConfirmedTonight ? "이번 밤 일정 확정 완료" : "공연 항목을 클릭해 묘기·단계 변경";
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
            // M2: 밤 정비 준비 완료는 숙소 침대에서 한다(Task-078). 스케줄표 화면의 준비 버튼은 쓰지 않는다.
            readyButton.gameObject.SetActive(false);
            GetComponentInParent<UiRuntimeFontFallback>()?.ApplyTo(transform);
        }
        static void Label(UnityEngine.UI.Button button, string text) => button.GetComponentInChildren<TMP_Text>(true).text = text;
        public void Cycle(int day, int slot)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null || !view.IsVisible || !schedule.HasAuthority || !schedule.Book.IsEditable(day)) return;
            // 한 칸 버튼으로 (묘기, 단계)를 차례로 돈다: 묘기 A 1단계 → … → 고를 수 있는 최고 단계 → 묘기 B 1단계 → … → 빈 칸.
            var choices = schedule.SelectableTricks;
            string trick = schedule.Book.Get(day, slot);
            int tier = schedule.Book.Difficulty(day, slot);
            ScheduleRefusal result;
            if (!string.IsNullOrEmpty(trick) && tier < schedule.MaxTier(trick)) result = schedule.Set(day, slot, trick, tier + 1);
            else
            {
                int next = Array.IndexOf(choices, trick) + 1;
                result = schedule.Set(day, slot, next < choices.Length ? choices[next] : "", 1);
            }
            message = ScheduleBoard.Explain(result); lastKey = null; nextRefresh = 0;
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
            confirmButton.onClick.RemoveListener(Confirm);
            if (ownedEvents != null) Destroy(ownedEvents.gameObject);
        }
    }
}
