using CurtainCall.UI.NotificationsV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.Notifications
{
    public sealed class NotificationsPreview : MonoBehaviour
    {
        [SerializeField] SystemNotificationsView combined;
        [SerializeField] SystemNotificationsView bannerOnly;
        [SerializeField] SystemNotificationsView toastOnly;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        [SerializeField] GameObject performanceContext;
        [SerializeField] GameObject backstageContext;
        float originalTimeScale;
        bool ownsTimeScale;
        bool bright;
        bool backstage;
        int sample;
        public int ActionCount { get; private set; }
        public SystemNotificationsView Current { get; private set; }

        public void Configure(SystemNotificationsView both, SystemNotificationsView banner, SystemNotificationsView toast,
            TMP_Text label, Camera camera, GameObject performance, GameObject backstageHud)
        {
            combined = both; bannerOnly = banner; toastOnly = toast; status = label; previewCamera = camera;
            performanceContext = performance; backstageContext = backstageHud;
            Current = combined;
        }
        void Start() { ResetPreview(); }
        void OnDisable() { RestoreTime(); if (Current != null) Current.HideAllImmediate(); }
        void OnDestroy() { RestoreTime(); }

        public void ShowSample(int index, float holdSeconds = 3f)
        {
            sample = index;
            Select(index == 9 ? bannerOnly : index == 10 ? toastOnly : combined);
            Current.HideAllImmediate();
            string title = "막이 오릅니다", subtitle = "공연을 시작합니다", message = "동료가 참가했습니다";
            var bannerState = BannerState.Normal; var toastState = ToastState.Normal;
            var bannerIcon = BannerIcon.Default; var toastIcon = ToastIcon.Default;
            if (index == 1) { title = "단원 2 추락"; subtitle = "무대에서 동료의 상황을 확인하세요"; bannerState = BannerState.Danger; bannerIcon = BannerIcon.Skull; }
            if (index == 2 || index == 5) { title = "소집 경보"; subtitle = index == 5 ? "" : "무대 입구로 집결하세요"; bannerState = BannerState.Danger; }
            if (index == 3) { message = "구매 완료"; toastState = ToastState.Success; }
            if (index == 4) { message = "공동 자금이 부족합니다"; toastState = ToastState.Failure; }
            if (index == 6) { bannerIcon = BannerIcon.None; toastIcon = ToastIcon.None; }
            if (index == 7) { title = "아주 긴 알림 제목입니다 아주 긴 알림 제목입니다 아주 긴 알림 제목입니다"; subtitle = "가나다라마바사 가나다라마바사 가나다라마바사 가나다라마바사 가나다라마바사 가나다라마바사"; message = "아주 긴 알림 내용입니다 아주 긴 알림 내용입니다 아주 긴 알림 내용입니다"; }
            if (index == 8) { title = "<b>알림</b>\n다음\t줄"; subtitle = "제어\r\n문자" + (char)0x2028 + "확인"; message = @"문자 그대로 \n <color=red>알림</color>"; }
            if (Current.Banner != null) Current.Banner.Show(title, subtitle, bannerState, holdSeconds, bannerIcon);
            if (Current.Toast != null) Current.Toast.Show(message, toastState, holdSeconds, toastIcon);
            UpdateStatus();
        }
        void Select(SystemNotificationsView selected)
        {
            foreach (var view in new[] { combined, bannerOnly, toastOnly }) if (view != null) view.gameObject.SetActive(view == selected);
            Current = selected;
        }
        void UpdateStatus()
        {
            if (status != null) status.text = "합성 UI · 게임 연결 없음 / 예시 유지 3초 / 샘플 " + sample +
                " / 입력 " + ActionCount + " / " + (ownsTimeScale ? "시간 정지" : "시간 정상");
        }
        public void Normal() { ActionCount++; ShowSample(0); }
        public void Danger() { ActionCount++; ShowSample(1); }
        public void Alarm() { ActionCount++; ShowSample(2); }
        public void Success() { ActionCount++; ShowSample(3); }
        public void Failure() { ActionCount++; ShowSample(4); }
        public void TitleOnly() { ActionCount++; ShowSample(5); }
        public void NoIcon() { ActionCount++; ShowSample(6); }
        public void LongText() { ActionCount++; ShowSample(7); }
        public void ControlText() { ActionCount++; ShowSample(8); }
        public void BannerOnly() { ActionCount++; ShowSample(9); }
        public void ToastOnly() { ActionCount++; ShowSample(10); }
        public void Replace() { ActionCount++; ShowSample(sample == 1 ? 0 : 1); }
        public void Restart() { ActionCount++; ShowSample(sample); }
        public void Hide() { ActionCount++; Current.HideAll(); UpdateStatus(); }
        public void HideImmediate() { ActionCount++; Current.HideAllImmediate(); UpdateStatus(); }
        public void TogglePause()
        {
            ActionCount++;
            if (ownsTimeScale) RestoreTime();
            else { originalTimeScale = Time.timeScale; ownsTimeScale = true; Time.timeScale = 0f; }
            ShowSample(sample);
        }
        void RestoreTime() { if (ownsTimeScale) { Time.timeScale = originalTimeScale; ownsTimeScale = false; } }
        public void ToggleBackground()
        {
            ActionCount++; bright = !bright;
            if (previewCamera != null) previewCamera.backgroundColor = bright ? new Color32(238, 227, 191, 255) : new Color32(13, 23, 27, 255);
            UpdateStatus();
        }
        public void ToggleContext()
        {
            ActionCount++; backstage = !backstage;
            if (performanceContext != null) performanceContext.SetActive(!backstage);
            if (backstageContext != null) backstageContext.SetActive(backstage);
            ApplyContextSamples(); UpdateStatus();
        }
        void ApplyContextSamples()
        {
            var hud = performanceContext.GetComponentInChildren<CurtainCall.UI.PerformanceHudV1.PerformanceHUDView>(true);
            hud.SetContext(true, true, false); hud.Timer.SetRemainingSeconds(161);
            hud.Progress.SetProgress(160f, new[] { 98f, 76f, 116f, 130f }, 0, 36f, 140f, new[] { 54f }, false);
            hud.Balance.SetBalance(.18f, -.3f, .3f, -.5f, .5f, 0f, "A", "D", false, false);
            performanceContext.GetComponentInChildren<CurtainCall.CheerGauge.C1CheerGaugeView>(true).SetScores(new long[] { 60, 45, 30, 20 }, 400, false);
            var back = backstageContext.GetComponentInChildren<CurtainCall.UI.BackstageHudV1.BackstageHUDView>(true);
            back.SetContext(true, true, true, true);
            back.Clock.SetClock(134, .72f, CurtainCall.UI.BackstageHudV1.BackstageClockState.Normal);
            back.Funds.SetAmount(1280); back.Gather.SetCounts(3, 4);
        }
        public void ResetPreview()
        {
            RestoreTime(); ActionCount = 0; sample = 0; bright = false; backstage = false;
            if (previewCamera != null) previewCamera.backgroundColor = new Color32(13, 23, 27, 255);
            if (performanceContext != null) performanceContext.SetActive(true);
            if (backstageContext != null) backstageContext.SetActive(false);
            Select(combined); Current.HideAllImmediate(); ApplyContextSamples(); UpdateStatus();
        }
    }
}
