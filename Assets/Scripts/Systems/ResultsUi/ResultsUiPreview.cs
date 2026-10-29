using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.ResultsV1
{
    /// <summary>C12 독립 표시 검증용이다. 판정·정산·게임 흐름은 연결하지 않는다.</summary>
    public sealed class ResultsUiPreview : MonoBehaviour
    {
        [SerializeField] ResultsPanelView results;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        [SerializeField] UnityEngine.UI.Button[] controls;
        [SerializeField] RectTransform localDestination;
        float originalScale;
        bool originalBackground, captured, paused, light, originalDestinationActive;
        Color originalColor;
        string kind = "grand", message = "명시적으로 열기를 눌러 시작하세요";
        int revision, intents;
        bool acknowledgementEnabled = true, acknowledgementLabel = true;
        public int ActionCount { get; private set; }
        public string LastAction { get; private set; } = "";
        public const string Instructions = "C12 공연 결과 표시 예시 / 판정·정산·게임 흐름 연결 없음\nPlay 시작 Hidden · 확인은 의도만 전달하며 자동으로 닫지 않습니다";
        public const string Caption = "1920×1080 / 1920×1200 · 전체 화면 딤 없음 · 리비전 순서는 호출자 책임\n동전은 C12 내부 장식이며 자금 값을 변경하지 않습니다";
        public const string RequiredText = Instructions + Caption +
            " 명시적으로 열기를 눌러 시작하세요 같은 세션 갱신 다른 세션 거절 갱신 거절 복구 완료 확인 의도 " +
            "실패 성공 대성공 1일차 외줄타기 공연이 끝났습니다. 공연을 무사히 마쳤습니다. 관객의 뜨거운 환호가 쏟아집니다. " +
            "단원별 도달 거리 전체 160 m 기본 보상 추가 보상 공동 자금 이번 공연 확인 후 다음 안내를 기다려 주세요. " +
            "단원 나 추락 예시 긴 문구 가 확인 Hidden True False timeScale runInBackground action intent session revision " +
            "OpenFailure OpenSuccess OpenGrand TwoMembers ThreeMembers FourMembers NoLocal Overlap MissingPositions OutsidePositions LongText RefreshPresentation OtherSession ToggleAcknowledge ToggleAcknowledgeLabel RequestAcknowledge Hide Reopen CoinMinimum CoinMaximum CoinWrongSession ToggleDestination NullDestination RestoreDestination TogglePause ToggleBackground ToggleRunInBackground ResetPreview 0123456789.,+-/:·…<>b_";
        public void Configure(ResultsPanelView view, TMP_Text label, Camera camera, UnityEngine.UI.Button[] buttons, RectTransform destination)
        {
            Unsubscribe(); RestoreGlobals(); results = view; status = label; previewCamera = camera; controls = buttons; localDestination = destination;
            if (Application.isPlaying && isActiveAndEnabled) { CaptureGlobals(); Subscribe(); }
            UpdateStatus();
        }
        void OnEnable() { if (Application.isPlaying) { CaptureGlobals(); Subscribe(); } }
        void OnDisable() { Unsubscribe(); RestoreGlobals(); }
        void OnDestroy() { Unsubscribe(); RestoreGlobals(); }
        void OnApplicationQuit() { Unsubscribe(); RestoreGlobals(); }
        void Update() { if (Application.isPlaying) UpdateStatus(); }
        void Subscribe() { if (results != null) { results.AcknowledgeRequested -= OnIntent; results.AcknowledgeRequested += OnIntent; } }
        void Unsubscribe() { if (results != null) results.AcknowledgeRequested -= OnIntent; }
        void OnIntent(string session) { intents++; message = "확인 의도 " + session; }
        void CaptureGlobals()
        {
            if (captured) return;
            originalScale = Time.timeScale; originalBackground = Application.runInBackground;
            originalColor = previewCamera == null ? Color.black : previewCamera.backgroundColor;
            originalDestinationActive = localDestination != null && localDestination.gameObject.activeSelf; captured = true;
        }
        void RestoreGlobals()
        {
            if (!captured) return;
            Time.timeScale = originalScale; Application.runInBackground = originalBackground;
            if (previewCamera != null) previewCamera.backgroundColor = originalColor;
            if (localDestination != null) localDestination.gameObject.SetActive(originalDestinationActive);
            if (results != null) results.SetRewardTransferDestination(localDestination);
            captured = paused = light = false;
        }
        void Run(int index, Action action, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
        {
            if (!Application.isPlaying || !isActiveAndEnabled || results == null || !results.isActiveAndEnabled ||
                controls == null || index < 0 || index >= controls.Length || controls[index] == null ||
                !controls[index].IsActive() || !controls[index].IsInteractable()) return;
            CaptureGlobals();
            try { action(); ActionCount++; LastAction = name; UpdateStatus(); }
            catch { RestoreGlobals(); throw; }
        }
        ResultsPresentation Sample(string session = "results-preview") => CreateSample(kind, "preview-" + ++revision, session, acknowledgementEnabled, acknowledgementLabel);
        void OpenKind(string value) { kind = value; results.Open(Sample()); }
        public void OpenFailure() => Run(0, () => OpenKind("failure"));
        public void OpenSuccess() => Run(1, () => OpenKind("success"));
        public void OpenGrand() => Run(2, () => OpenKind("grand"));
        public void TwoMembers() => Run(3, () => OpenKind("two"));
        public void ThreeMembers() => Run(4, () => OpenKind("three"));
        public void FourMembers() => Run(5, () => OpenKind("four"));
        public void NoLocal() => Run(6, () => OpenKind("no-local"));
        public void Overlap() => Run(7, () => OpenKind("overlap"));
        public void MissingPositions() => Run(8, () => OpenKind("missing"));
        public void OutsidePositions() => Run(9, () => OpenKind("outside"));
        public void LongText() => Run(10, () => OpenKind("text"));
        public void RefreshPresentation() => Run(11, () => message = results.SetPresentation(Sample()) ? "같은 세션 갱신" : "갱신 거절");
        public void OtherSession() => Run(12, () => message = results.SetPresentation(Sample("other-session")) ? "같은 세션 갱신" : "다른 세션 거절");
        public void ToggleAcknowledge() => Run(13, () => { acknowledgementEnabled = !acknowledgementEnabled; results.SetPresentation(Sample()); });
        public void ToggleAcknowledgeLabel() => Run(14, () => { acknowledgementLabel = !acknowledgementLabel; results.SetPresentation(Sample()); });
        public void RequestAcknowledge() => Run(15, () => results.Acknowledge());
        public void Hide() => Run(16, () => results.Hide());
        public void Reopen() => Run(17, () => { results.Hide(); results.Open(Sample()); });
        public void CoinMinimum() => Run(18, () => message = results.PlayRewardTransfer("results-preview", -10).ToString());
        public void CoinMaximum() => Run(19, () => message = results.PlayRewardTransfer("results-preview", 100).ToString());
        public void CoinWrongSession() => Run(20, () => message = results.PlayRewardTransfer("wrong-session", 5).ToString());
        public void ToggleDestination() => Run(21, () => { if (localDestination != null) localDestination.gameObject.SetActive(!localDestination.gameObject.activeSelf); });
        public void NullDestination() => Run(22, () => results.SetRewardTransferDestination(null));
        public void RestoreDestination() => Run(23, () => { if (localDestination != null) localDestination.gameObject.SetActive(true); results.SetRewardTransferDestination(localDestination); });
        public void TogglePause() => Run(24, () => { paused = !paused; Time.timeScale = paused ? 0 : originalScale; });
        public void ToggleBackground() => Run(25, () => { light = !light; if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(224, 226, 218, 255) : originalColor; });
        public void ToggleRunInBackground() => Run(26, () => Application.runInBackground = !Application.runInBackground);
        public void ResetPreview() => Run(27, () => { results.Hide(); RestoreGlobals(); CaptureGlobals(); kind = "grand"; acknowledgementEnabled = acknowledgementLabel = true; message = "복구 완료"; });
        /// <summary>QA가 같은 원자료를 재현하도록 공개한다. 숫자는 표시 예시이며 보상 공식이 아니다.</summary>
        public static ResultsPresentation CreateSample(string kind, string revision = "sample", string session = "results-preview", bool acknowledgeEnabled = true, bool acknowledgeLabel = true)
        {
            ResultsOutcome outcome = kind == "failure" ? ResultsOutcome.Failure : kind == "success" ? ResultsOutcome.Success : ResultsOutcome.GrandSuccess;
            var demo = ResultsDemoData.Create(outcome);
            int count = kind == "two" ? 2 : kind == "three" ? 3 : 4;
            var members = new ResultsMemberPresentation[count];
            for (int i = 0; i < count; i++)
            {
                var member = demo.Members[i];
                float? position = kind == "overlap" ? .5f : kind == "outside" ? (i % 2 == 0 ? -2f : 2f) : member.NormalizedProgress;
                float? fall = kind == "overlap" ? .5f : member.NormalizedFall;
                if (kind == "missing") { position = i == 0 ? (float?)null : i == 1 ? float.NaN : i == 2 ? float.PositiveInfinity : float.NegativeInfinity; fall = position; }
                members[i] = new ResultsMemberPresentation(member.Id, kind == "text" ? "<b>" + new string('가', 100) + "</b>" : member.Label,
                    position, kind == "text" ? new string('가', 100) : member.DistanceLabel, member.Color, kind != "no-local" && member.IsLocalPlayer, fall);
            }
            return new ResultsPresentation(session, revision, outcome, demo.Title, demo.Context,
                kind == "text" ? new string('가', 120) : demo.Subtitle, demo.ProgressHeading, demo.CourseLabel,
                kind == "missing" ? float.NaN : kind == "outside" ? 2f : demo.NormalizedMaximum,
                demo.BaseLabel, demo.BaseAmount, demo.BonusLabel, demo.BonusAmount, demo.FundsLabel, demo.ContributionText, demo.FundsAmount,
                demo.Footer, acknowledgeLabel ? "확인" : "", acknowledgeEnabled, members);
        }
        void UpdateStatus()
        {
            if (status == null) return;
            status.text = Instructions + "\n" + (results != null && results.IsVisible ? "C12" : "Hidden") +
                " / timeScale " + Time.timeScale.ToString("0.##", CultureInfo.InvariantCulture) + " / runInBackground " + Application.runInBackground +
                " / action " + ActionCount + " " + LastAction + " / intent " + intents + " · " + message;
        }
    }
}
