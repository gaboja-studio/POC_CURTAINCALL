using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Survival;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>로비·지출·복구·밤 준비 연결을 관찰하는 독립 더미.</summary>
    [RequireComponent(typeof(DummySpaceDiagnostics))]
    public sealed class DummyWaitingRoom : MonoBehaviour
    {
        DummySpaceDiagnostics diagnostics;
        int previewLostCount = 3;
        public DummySpaceDiagnostics Diagnostics => diagnostics;
        public int SpendAmount => GameSettings.WaitingRoom.DummySpendAmount;
        public int PreviewLostCount
        {
            get => previewLostCount;
            set => previewLostCount = Mathf.Clamp(value, 0, 3);
        }
        public BodyPart PreviewRestorePart => PlayerCondition.NextRestore(PlayerCondition.PartsForLostCount(previewLostCount));
        public int PreviewSlots => SurvivalRules.ItemSlots();

        void Awake() => diagnostics = GetComponent<DummySpaceDiagnostics>();

        public bool Spend() => diagnostics.Invoke($"지출 검증 {SpendAmount} → 031 TrySpend (기물 지급 없음)",
            () => diagnostics.Rewards.TrySpend(SpendAmount, "더미 지출 검증 — 기물 지급/치료 아님"));

        public bool Leave() => diagnostics.Invoke("대기실 출구 수동 신호(타이머는 쉬는 시간 시작부터 흐름)",
            () => diagnostics.Campaign.State.Phase == CurtainCall.Campaign.CampaignPhase.RestTime);
        public bool Ready() => diagnostics.Invoke("밤 준비 → 028 SetReady", () => diagnostics.Campaign.SetReady(!diagnostics.Campaign.IsLocalReady));
    }
}
