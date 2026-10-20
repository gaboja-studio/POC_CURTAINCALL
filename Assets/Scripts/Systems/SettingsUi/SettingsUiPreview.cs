using System;
using CurtainCall.UI;
using TMPro;
using UnityEngine;

namespace CurtainCall.SettingsUi
{
    /// <summary>명시적으로 여는 C14 합성 미리보기. 실제 오디오·화면·설정 서비스에는 쓰지 않는다.</summary>
    public sealed class SettingsUiPreview : MonoBehaviour
    {
        [SerializeField] SettingsPanelView settings;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text samplesCaption;
        [SerializeField] Camera previewCamera;
        [SerializeField] CurtainCallTabVisual sampleTab;
        [SerializeField] CurtainCallSliderVisual sampleSlider;
        [SerializeField] CurtainCallToggleVisual sampleToggle;
        SettingsPanelView subscribedView;
        SettingsSnapshot pendingRequest;
        long previousVersion;
        bool hasPreviousVersion, paused, light, samplesEnabled = true;
        float previousScale = 1;
        string result = "명시적으로 데모 열기를 눌러 시작하세요";
        public int ActionCount { get; private set; }
        public int InvocationCount => ActionCount;
        public int ApplyRequestCount { get; private set; }
        public string LastAction { get; private set; } = string.Empty;
        public long PendingVersion => pendingRequest == null ? -1 : pendingRequest.SessionVersion;
        public bool IsPaused => paused;

        /// <summary>확인 씬 전용 참조를 주입하며 설정 창을 자동으로 열지 않는다.</summary>
        public void Configure(SettingsPanelView view, TMP_Text label, TMP_Text caption, Camera camera,
            CurtainCallTabVisual tab, CurtainCallSliderVisual slider, CurtainCallToggleVisual toggle)
        {
            Unbind(); settings = view; status = label; samplesCaption = caption; previewCamera = camera;
            sampleTab = tab; sampleSlider = slider; sampleToggle = toggle;
            if (Application.isPlaying && isActiveAndEnabled) Bind();
        }
        void Awake() { RestoreTime(); UpdateStatus(); }
        void OnEnable() { if (Application.isPlaying) Bind(); }
        void OnDisable() { Unbind(); RestoreTime(); }
        void OnDestroy() { Unbind(); RestoreTime(); }
        void OnApplicationQuit() { RestoreTime(); }
        void Update() { if (Application.isPlaying) UpdateStatus(); }
        void Bind()
        {
            if (subscribedView == settings) return;
            Unbind(); subscribedView = settings;
            if (subscribedView != null) subscribedView.ApplyRequested += OnApplyRequested;
        }
        void Unbind()
        {
            if (subscribedView != null) subscribedView.ApplyRequested -= OnApplyRequested;
            subscribedView = null;
        }
        void OnApplyRequested(SettingsSnapshot request)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || request == null) return;
            if (pendingRequest != null) { previousVersion = pendingRequest.SessionVersion; hasPreviousVersion = true; }
            pendingRequest = request; ApplyRequestCount++;
            result = "합성 적용 요청 보류 · 성공/거절 응답 버튼으로 완료";
            UpdateStatus();
        }
        void Run(Action action, [System.Runtime.CompilerServices.CallerMemberName] string actionName = "")
        {
            if (!Application.isPlaying || !isActiveAndEnabled || settings == null) return;
            ActionCount++;
            try { action(); LastAction = actionName; UpdateStatus(); }
            catch { RestoreTime(); throw; }
        }
        public void OpenDemo() => Run(() => { settings.OpenDemo(); result = "새 데모 세션 · 예시 값은 저장 기본값이 아닙니다"; });
        public void Close() => Run(() => { settings.Cancel(); result = "편집 모델 폐기 · 실제 서비스 변경 없음"; });
        public void RequestApply() => Run(() =>
        {
            settings.RequestApply();
            if (!settings.IsBusy) result = "변경된 값이 있는 열린 데모에서만 요청합니다";
        });
        /// <summary>캡처한 요청 토큰에 대한 외부 성공 응답만 모의한다.</summary>
        public void AcceptResponse() => Run(() =>
        {
            bool accepted = pendingRequest != null && settings.AcceptApplied(pendingRequest.SessionVersion);
            result = accepted ? "합성 성공 응답 수락 · 닫힘 · 실제 저장 없음" : "성공 응답 거절 · 요청 없음 또는 이전 토큰";
        });
        public void RejectResponse() => Run(() =>
        {
            bool accepted = pendingRequest != null && settings.RejectApply(pendingRequest.SessionVersion, "합성 거절 · 값을 유지하고 다시 조작하세요");
            result = accepted ? "합성 거절 응답 수락 · 재조작 가능" : "거절 응답 무시 · 요청 없음 또는 이전 토큰";
        });
        public void DeferResponse() => Run(() => { result = settings.IsBusy ? "응답 보류 중 · 취소/닫기는 계속 가능" : "보류할 요청 없음 · 값을 바꾸고 적용 요청"; });
        public void ReplayPreviousResponse() => Run(() =>
        {
            bool accepted = hasPreviousVersion && settings.AcceptApplied(previousVersion);
            result = accepted ? "이전 응답이 수락됨 · 토큰 가드를 점검하세요" : "이전 응답 거절 확인 · 현재 세션 보존";
        });
        public void TogglePause() => Run(() =>
        {
            if (paused) RestoreTime();
            else { previousScale = Time.timeScale; paused = true; Time.timeScale = 0; }
        });
        public void ToggleBackground() => Run(() => { light = !light; ApplyBackground(); });
        public void ToggleSamplesInteractable() => Run(() =>
        {
            samplesEnabled = !samplesEnabled;
            ApplySamplesInteractable();
        });
        public void ResetPreview() => Run(() =>
        {
            RestoreTime(); light = false; ApplyBackground(); settings.Hide();
            samplesEnabled = true; ApplySamplesInteractable();
            if (sampleTab != null) sampleTab.SetSelectedWithoutNotify(false);
            if (sampleSlider != null) sampleSlider.SetValueWithoutNotify(.5f);
            if (sampleToggle != null) sampleToggle.SetValueWithoutNotify(false);
            result = "미리보기 복구 · Hidden · 데모는 명시적으로 다시 여세요";
        });
        void ApplySamplesInteractable()
        {
            if (sampleTab != null) { sampleTab.NativeToggle.interactable = samplesEnabled; sampleTab.RefreshVisual(true); }
            if (sampleSlider != null) { sampleSlider.NativeSlider.interactable = samplesEnabled; sampleSlider.RefreshVisual(true); }
            if (sampleToggle != null) { sampleToggle.NativeToggle.interactable = samplesEnabled; sampleToggle.RefreshVisual(true); }
        }
        void RestoreTime()
        {
            if (!paused) return;
            Time.timeScale = previousScale; paused = false;
        }
        void ApplyBackground()
        {
            if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(224, 226, 218, 255) : new Color32(14, 24, 28, 255);
            Color color = light ? new Color32(14, 24, 28, 255) : new Color32(240, 238, 220, 255);
            if (status != null) status.color = color;
            if (samplesCaption != null) samplesCaption.color = color;
        }
        void UpdateStatus()
        {
            if (status == null) return;
            status.text = "C14 합성 확인 · 실제 설정 서비스 연결 없음 · " +
                (settings == null || !settings.IsVisible ? "Hidden" : "표시 / 탭 " + settings.CurrentPage + " / dirty " + settings.IsDirty + " / busy " + settings.IsBusy) +
                " / timeScale " + Time.timeScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "\n" + result;
        }
    }
}
