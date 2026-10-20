using CurtainCall.CheerGauge;
using CurtainCall.UI.PerformanceHudV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.PerformanceHud
{
    /// <summary>독립 확인 씬 전용 합성 데이터. 게임 상태나 시간 진행을 변경하지 않는다.</summary>
    public sealed class PerformanceHudPreview : MonoBehaviour
    {
        [SerializeField] PerformanceHUDView hud;
        [SerializeField] C1CheerGaugeView cheer;
        [SerializeField] TMP_Text status;
        public PerformanceHUDView Hud => hud;
        public int SelectedSample { get; private set; }
        public bool DangerSample { get; private set; }
        public bool Observing { get; private set; }
        public bool OnTightrope { get; private set; } = true;
        public bool PerformanceActive { get; private set; } = true;

        public void Configure(PerformanceHUDView view, C1CheerGaugeView gauge, TMP_Text label)
        {
            hud = view;
            cheer = gauge;
            status = label;
        }

        void Start() => Normal();
        public void Normal() => ShowSample(0);
        public void Warning()
        {
            bool danger = SelectedSample == 1 && !DangerSample;
            ShowSample(1);
            DangerSample = danger;
            ApplyBalanceSample();
            UpdateStatus();
        }
        public void Overlap() => ShowSample(2);
        public void Unavailable() => ShowSample(3);

        public void ShowSample(int sample)
        {
            if (sample < 0 || sample > 3) throw new System.ArgumentOutOfRangeException(nameof(sample));
            SelectedSample = sample;
            DangerSample = false;
            Observing = false;
            OnTightrope = true;
            PerformanceActive = true;
            hud.ResetForRuntime();
            hud.SetContext(true, true, false);
            cheer.SetScores(new long[] { 60, 45, 30, 20 }, 400, false);
            if (sample != 3)
            {
                bool warning = sample == 1;
                hud.Timer.SetRemainingSeconds(warning ? 9d : 161d);
                hud.Progress.SetProgress(160f, sample == 2 ? new[] { 98f, 98f, 98f, 98f } :
                    new[] { 98f, 76f, 116f, 130f }, 0, warning ? 91f : 36f, 140f, new[] { 54f }, warning);
                ApplyBalanceSample();
            }
            UpdateStatus();
        }

        void ApplyBalanceSample()
        {
            // 최신 외줄의 기본 폭을 재현하는 합성값이며 게임 설정이나 추락 판정에 연결하지 않는다.
            bool warning = SelectedSample == 1;
            hud.Balance.SetBalance(DangerSample ? 0.8f : warning ? 0.4f : 0.18f,
                -0.3f, 0.3f, -0.5f, 0.5f, warning ? 0.67f : 0f, "A", "D", false, warning);
        }

        public void ToggleObserving()
        {
            Observing = !Observing;
            ApplyContext();
        }
        public void ToggleTightrope()
        {
            OnTightrope = !OnTightrope;
            ApplyContext();
        }
        public void TogglePerformance()
        {
            PerformanceActive = !PerformanceActive;
            ApplyContext();
        }
        public void ResetPreview() => Normal();
        void ApplyContext()
        {
            hud.SetContext(PerformanceActive, OnTightrope, Observing);
            UpdateStatus();
        }
        void UpdateStatus()
        {
            string sample = new[] { "평소", "경고", "겹침", "미수신" }[SelectedSample];
            string balance = SelectedSample == 3 ? "—" : DangerSample ? "Danger" : SelectedSample == 1 ? "Warning" : "Safe";
            status.text = $"공연 HUD 확인 · 합성 데이터(게임 미연결) · {sample} / P1 {balance} · 공연 {(PerformanceActive ? "ON" : "OFF")} / 외줄 {(OnTightrope ? "ON" : "OFF")} / 관전 {(Observing ? "ON" : "OFF")}";
        }
    }
}
