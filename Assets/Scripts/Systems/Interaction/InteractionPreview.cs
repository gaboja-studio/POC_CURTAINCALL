using System;
using CurtainCall.UI.InteractionV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.Interaction
{
    public sealed class InteractionPreview : MonoBehaviour
    {
        [SerializeField] InteractionPromptView prompt;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        [SerializeField] GameObject performance;
        [SerializeField] GameObject backstage;
        bool ownsPause;
        float originalTimeScale;
        bool light;
        int calls;
        public int InvocationCount => calls;
        public InteractionPromptView Prompt => prompt;

        public void Configure(InteractionPromptView view, TMP_Text label, Camera camera, GameObject performanceContext, GameObject backstageContext)
        { prompt = view; status = label; previewCamera = camera; performance = performanceContext; backstage = backstageContext; }
        void Awake() { if (prompt != null) ResetState(); }
        void Start() { ApplyContextSamples(); }
        void OnDisable() { RestoreTime(); }
        void OnDestroy() { RestoreTime(); }
        void Run(string name, Action action)
        {
            try { action(); calls++; if (status != null) status.text = "C6 합성 표시 · 게임 연결 없음 · " + name + " · 호출 " + calls; }
            catch { RestoreTime(); throw; }
        }
        public void Tap() => Run("일반 가능", () => prompt.Show("F", "수화기 들기", InteractionAvailability.Available, false, 0));
        public void Hold() => Run("길게 가능", () => prompt.Show("F", "문 열기", InteractionAvailability.Available, true, .65f));
        public void Unavailable() => Run("일반 불가", () => prompt.Show("F", "문 열기", InteractionAvailability.Unavailable, false, 0, "열쇠가 필요합니다"));
        public void UnavailableHold() => Run("길게 불가", () => prompt.Show("F", "장치 작동", InteractionAvailability.Unavailable, true, .65f, "전원이 필요합니다"));
        public void Progress0() => Progress(0);
        public void Progress25() => Progress(.25f);
        public void Progress50() => Progress(.5f);
        public void Progress65() => Progress(.65f);
        public void Progress75() => Progress(.75f);
        public void Progress100() => Progress(1);
        void Progress(float value) => Run("진행 " + Mathf.RoundToInt(value * 100) + "%", () => prompt.Show("F", "문 열기", InteractionAvailability.Available, true, value));
        public void NewTarget() => Run("새 대상 0%", () => prompt.Show("F", "장치 작동", InteractionAvailability.Available, true, 0));
        public void RepeatShow() => Run("반복 Show", () => prompt.Show("F", "문 열기", InteractionAvailability.Available, true, .65f));
        public void LongText() => Run("긴 문구", () => prompt.Show("LeftShift", "수화기를 들어 무대 뒤쪽의 장치를 작동하고 문을 여세요", InteractionAvailability.Available, true, .5f, "표시 영역을 넘는 긴 보조 문구는 한 줄에서 말줄임으로 표시합니다"));
        public void ControlText() => Run("제어 문자", () => prompt.Show("F" + (char)9, "문" + (char)13 + (char)10 + "열기" + (char)0x2028 + "<b>확인</b>", InteractionAvailability.Available, true, .5f, "길게" + (char)9 + "누르기" + (char)0x2029 + "유지"));
        public void DefaultHelper() => Run("기본 보조", () => prompt.Show("F", "문 열기", InteractionAvailability.Unavailable, true, 0));
        public void CustomHelper() => Run("보조 있음", () => prompt.Show("F", "수화기 들기", InteractionAvailability.Available, false, 0, "장치 작동"));
        public void Hide() => Run("퇴장", () => prompt.Hide());
        public void HideImmediate() => Run("즉시 숨김", () => prompt.HideImmediate());
        public void TogglePause() => Run("시간 정지 전환", () =>
        {
            if (ownsPause) RestoreTime();
            else { originalTimeScale = Time.timeScale; ownsPause = true; Time.timeScale = 0; }
        });
        public void ToggleBackground() => Run("배경 전환", () =>
        { light = !light; if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(220, 226, 220, 255) : new Color32(13, 23, 27, 255); });
        public void ToggleContext() => Run("HUD 전환", () =>
        { bool next = performance != null && !performance.activeSelf; if (performance != null) performance.SetActive(next); if (backstage != null) backstage.SetActive(!next); ApplyContextSamples(); });
        public void ResetPreview() => Run("초기화", ResetState);
        void ResetState()
        {
            RestoreTime(); prompt.ResetForRuntime(); light = false;
            if (previewCamera != null) previewCamera.backgroundColor = new Color32(13, 23, 27, 255);
            if (performance != null) performance.SetActive(true); if (backstage != null) backstage.SetActive(false);
            if (Application.isPlaying) ApplyContextSamples();
        }
        void ApplyContextSamples()
        {
            if (performance != null && performance.activeInHierarchy)
            {
                var hud = performance.GetComponentInChildren<CurtainCall.UI.PerformanceHudV1.PerformanceHUDView>(true);
                hud.SetContext(true, true, false); hud.Timer.SetRemainingSeconds(161);
                hud.Progress.SetProgress(160, new[] { 98f, 76f, 116f, 130f }, 0, 36, 140, new[] { 54f }, false);
                hud.Balance.SetBalance(.18f, -.3f, .3f, -.5f, .5f, 0, "A", "D", false, false);
                performance.GetComponentInChildren<CurtainCall.CheerGauge.C1CheerGaugeView>(true).SetScores(new long[] { 60, 45, 30, 20 }, 400, false);
            }
            if (backstage != null && backstage.activeInHierarchy)
            {
                var hud = backstage.GetComponentInChildren<CurtainCall.UI.BackstageHudV1.BackstageHUDView>(true);
                hud.SetContext(true, true, true, true);
                hud.Clock.SetClock(134, .72f, CurtainCall.UI.BackstageHudV1.BackstageClockState.Normal);
                hud.Funds.SetAmount(1280); hud.Gather.SetCounts(3, 4);
                var slots = new CurtainCall.UI.BackstageHudV1.InventorySlotData[4];
                for (int i = 0; i < slots.Length; i++) slots[i] = new CurtainCall.UI.BackstageHudV1.InventorySlotData { IsEmpty = true, KeyLabel = (i + 1).ToString() };
                hud.Inventory.SetSlots(slots); hud.Inventory.ShowInventory();
            }
        }
        void RestoreTime() { if (!ownsPause) return; Time.timeScale = originalTimeScale; ownsPause = false; }
    }
}
