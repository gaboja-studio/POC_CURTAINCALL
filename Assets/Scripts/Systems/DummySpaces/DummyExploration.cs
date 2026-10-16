using CurtainCall.Settings;
using CurtainCall.Survival;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>구역 입력과 공통 타이머·잡힘 규칙의 연결을 보여준다. 추격 대기 로직은 소유하지 않는다.</summary>
    [RequireComponent(typeof(DummySpaceDiagnostics))]
    public sealed class DummyExploration : MonoBehaviour
    {
        DummySpaceDiagnostics diagnostics;
        public DummySpaceDiagnostics Diagnostics => diagnostics;
        public string Zone { get; private set; } = "대기실";
        public CatchOutcome PreviewCatch => SurvivalRules.ResolveCatch(SurvivalPlace.Exploration, GameSettings.Exploration.DummyLostCount);
        public bool IsSafe => Zone == "무대 입구" || (Zone == "대기실" && GameSettings.Base.Run.RestTimerMode == RestTimerMode.FromRestStart);

        void Awake() => diagnostics = GetComponent<DummySpaceDiagnostics>();

        public bool Leave()
        {
            if (diagnostics.Campaign.State.Phase != CurtainCall.Campaign.CampaignPhase.RestTime)
            {
                diagnostics.Note("더미 입력", "출구", "쉬는 시간이 아니므로 구역 입력 거절");
                return false;
            }
            Zone = "백스테이지";
            diagnostics.Note("더미 입력", "출구 구역", "백스테이지 (이동/위치 동기화 없음)");
            // A안은 이미 진행 중이므로 false일 수 있어도 구역 입력 자체는 유효하다.
            diagnostics.Invoke("출구 → NotifyLeftWaitingRoom", diagnostics.Campaign.NotifyLeftWaitingRoom);
            return true;
        }

        public void EnterWaitingRoom()
        {
            Zone = "대기실";
            diagnostics.Note("더미 입력", "대기실 구역", $"안전 여부 {IsSafe}; 공통 타이머는 정지하지 않음");
        }

        public bool EnterStage()
        {
            Zone = "무대 입구";
            diagnostics.Note("더미 입력", "무대 입구 구역", "한 줄의 위치 입력. 전원 집결 자동 감지 아님");
            return diagnostics.Invoke("수동 전원 집결 신호 → NotifyAllAtStageEntrance", diagnostics.Campaign.NotifyAllAtStageEntrance);
        }

        public CatchOutcome Preview()
        {
            var result = PreviewCatch;
            diagnostics.Note("규칙 미리보기", $"ExplorationSettings 손실 {GameSettings.Exploration.DummyLostCount} → ResolveCatch(Exploration)",
                $"{result}; 실제 플레이어 변경/무대 이동 없음");
            return result;
        }

        public bool Expire() => diagnostics.Invoke("디버그 만료 요청 → DebugSetRemaining(0)", () => diagnostics.Campaign.DebugSetRemaining(0));
    }
}
