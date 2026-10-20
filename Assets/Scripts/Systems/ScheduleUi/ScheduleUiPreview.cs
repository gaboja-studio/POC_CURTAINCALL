using System;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.ScheduleV1
{
    /// <summary>W3 독립 표시 예시만 제공한다. 실제 일정 권한·경제·영속화·네트워크는 연결하지 않는다.</summary>
    public sealed class ScheduleUiPreview : MonoBehaviour
    {
        [SerializeField] SchedulePanelView schedule;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        [SerializeField] UnityEngine.UI.Button[] controls;
        float originalScale;
        bool originalBackground, captured, paused, light;
        Color originalColor;
        int revision;
        string result = "데모 열기를 눌러 시작하세요";
        public int ActionCount { get; private set; }
        public string LastAction { get; private set; } = "";
        public const string Instructions = "W3 읽기 전용 일정 표시 예시 / 실제 일정·보상·경제·저장·네트워크 연결 없음\nPlay 시작 Hidden · 현재와 확정은 독립 표시 값입니다";
        public const string Caption = "1920×1080 / 1920×1200 · 전체 화면 딤 없음 · 일차별 스크롤\n표시 갱신은 일차 ID로 스크롤 유지 · 리비전 순서는 호출자 책임";
        public const string RequiredText = Instructions + Caption +
            "데모 열기를 눌러 시작하세요 같은 세션 갱신 다른 세션 거절 갱신 거절 복구 완료 " +
            "공연 일정표 3일 공연 일정 일정을 보는 동안에도 플레이는 계속됩니다. 현재 일차 공연 일정 없음 " +
            "공개된 일정이 없습니다. 외줄타기 인간대포 공연 종료 현재 예정 확정 미확정 닫기 " +
            "예시 긴 문구 가 일차 Hidden True False timeScale runInBackground 0123456789.-/:·…<>b";
        public void Configure(SchedulePanelView view, TMP_Text label, Camera camera, UnityEngine.UI.Button[] buttons)
        {
            RestoreGlobals(); schedule = view; status = label; previewCamera = camera; controls = buttons;
            if (Application.isPlaying && isActiveAndEnabled) CaptureGlobals();
            UpdateStatus();
        }
        void OnEnable() { if (Application.isPlaying) CaptureGlobals(); }
        void OnDisable() { RestoreGlobals(); }
        void OnDestroy() { RestoreGlobals(); }
        void OnApplicationQuit() { RestoreGlobals(); }
        void Update() { if (Application.isPlaying) UpdateStatus(); }
        void CaptureGlobals()
        {
            if (captured) return;
            originalScale = Time.timeScale; originalBackground = Application.runInBackground;
            originalColor = previewCamera == null ? Color.black : previewCamera.backgroundColor; captured = true;
        }
        void RestoreGlobals()
        {
            if (!captured) return;
            Time.timeScale = originalScale; Application.runInBackground = originalBackground;
            if (previewCamera != null) previewCamera.backgroundColor = originalColor;
            captured = paused = light = false;
        }
        void Run(int index, Action action, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
        {
            if (!Application.isPlaying || !isActiveAndEnabled || schedule == null || !schedule.isActiveAndEnabled ||
                controls == null || index < 0 || index >= controls.Length || controls[index] == null ||
                !controls[index].IsActive() || !controls[index].IsInteractable()) return;
            CaptureGlobals();
            try { action(); ActionCount++; LastAction = name; UpdateStatus(); }
            catch { RestoreGlobals(); throw; }
        }
        public void OpenDemo() => Run(0, () => schedule.Open(ScheduleDemoData.Create()));
        public void Empty() => Run(1, () => schedule.Open(CreateSample("empty")));
        public void Many() => Run(2, () => schedule.Open(CreateSample("many")));
        public void LongText() => Run(3, () => schedule.Open(CreateSample("text")));
        public void FourStates() => Run(4, () => schedule.Open(CreateSample("states")));
        public void Unconfirmed() => Run(5, () => schedule.Open(CreateSample("unconfirmed")));
        public void Day1Top() => Run(6, () => schedule.ScrollDay(0, -100000));
        public void Day1Bottom() => Run(7, () => schedule.ScrollDay(0, 100000));
        public void Day2Top() => Run(8, () => schedule.ScrollDay(1, -100000));
        public void Day2Bottom() => Run(9, () => schedule.ScrollDay(1, 100000));
        public void Day3Top() => Run(10, () => schedule.ScrollDay(2, -100000));
        public void Day3Bottom() => Run(11, () => schedule.ScrollDay(2, 100000));
        public void RefreshPresentation() => Run(12, () => result = schedule.SetPresentation(CreateSample("reorder", "preview-" + ++revision)) ? "같은 세션 갱신" : "갱신 거절");
        public void OtherSession() => Run(13, () => result = schedule.SetPresentation(CreateSample("many", "other", "other-session")) ? "같은 세션 갱신" : "다른 세션 거절");
        public void Close() => Run(14, () => schedule.Close());
        public void Hide() => Run(15, () => schedule.Hide());
        public void Reopen() => Run(16, () => { schedule.Hide(); schedule.Open(ScheduleDemoData.Create()); });
        public void TogglePause() => Run(17, () => { paused = !paused; Time.timeScale = paused ? 0 : originalScale; });
        public void ToggleBackground() => Run(18, () => { light = !light; if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(224, 226, 218, 255) : originalColor; });
        public void ToggleRunInBackground() => Run(19, () => Application.runInBackground = !Application.runInBackground);
        public void ResetPreview() => Run(20, () => { schedule.Hide(); RestoreGlobals(); CaptureGlobals(); result = "복구 완료"; });
        public static SchedulePresentation CreateSample(string kind, string revision = "sample", string session = "schedule-preview")
        {
            var days = new ScheduleDayPresentation[3];
            for (int i = 0; i < 3; i++)
            {
                int day = kind == "reorder" ? (i + 1) % 3 : i;
                int count = kind == "empty" ? 0 : kind == "states" ? 4 : kind == "text" ? 1 : 12;
                var entries = new ScheduleEntryPresentation[count];
                for (int j = 0; j < count; j++)
                    entries[j] = new ScheduleEntryPresentation("d" + day + "-" + j,
                        kind == "text" ? "<b>" + new string('가', 100) + "</b>" : "예시 공연 " + j,
                        "공연 " + j, j % 2 == 0 ? "현재" : "예정", j % 2 == 0,
                        kind != "unconfirmed" && (kind == "states" ? j >= 2 : j % 2 == 0));
                days[i] = new ScheduleDayPresentation("day" + (day + 1), (day + 1) + "일차", day == 1, "공연 일정 없음", entries);
            }
            return new SchedulePresentation(session, revision, "공연 일정표", "3일 공연 일정", "일정을 보는 동안에도 플레이는 계속됩니다.", days);
        }
        void UpdateStatus()
        {
            if (status == null) return;
            status.text = Instructions + "\n" + (schedule != null && schedule.IsVisible ? "공연 일정표" : "Hidden") +
                " / timeScale " + Time.timeScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                " / runInBackground " + Application.runInBackground + " · " + result;
        }
    }
}
