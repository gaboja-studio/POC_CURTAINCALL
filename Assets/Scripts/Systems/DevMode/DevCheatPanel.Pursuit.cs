using CurtainCall.Campaign;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEngine;

namespace CurtainCall.Dev
{
    public sealed partial class DevCheatPanel
    {
        void DrawPursuitCheats()
        {
            var run = CampaignRun.Instance;
            if (run == null || run.State.Phase != CampaignPhase.RestTime || CommonUiSceneLoader.CurrentWaitingRoom == null) return;
            var director = PursuitDirector.Current;
            GUILayout.Space(6);
            GUILayout.Label("대기실 · 타이머 / 추격", heading);
            var state = run.State;
            string time = state.Timer == RestTimerState.Unlimited ? "무제한" : $"{Mathf.CeilToInt(state.TimeRemaining)}초";
            string pursuit = director != null && director.Elapsed >= 0
                ? $"추격 {director.CurrentStageIndex + 1}단계 · {(director.StageIsForced ? "고정" : "자동")}"
                : "추격 대기";
            GUILayout.Label($"{time} ({state.Timer}) · {pursuit}", label);
            bool allowed = director != null && director.CanDebug;
            if (!allowed) GUILayout.Label(run.HasAuthority ? "추격 시스템을 준비 중입니다." : "호스트만 타이머·단계를 변경할 수 있습니다.", label);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && allowed;
            if (GUILayout.Button("타이머 초기화 및 추격 철회", button))
                PursuitResult(director.DebugResetTimer(), "기본 제한시간 복원 · 추격 철회");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("타이머 -1분", button)) PursuitResult(director.DebugAdjustTimer(-60), "타이머 -1분");
            if (GUILayout.Button("타이머 +1분", button)) PursuitResult(director.DebugAdjustTimer(60), "타이머 +1분");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("타이머 1분 남음", button)) PursuitResult(director.DebugSetTimer(60), "남은 시간 1분 · 추격 철회");
            if (GUILayout.Button("타이머 오버 및 추격 시작", button)) PursuitResult(director.DebugExpireTimer(), "타이머 만료 · 추격 시작");
            GUILayout.Label("추격 단계 설정 (선택 시 추격 시작·단계 고정)", label);
            var stages = GameSettings.WaitingRoom.Pursuit.Stages;
            if (stages != null) for (int row = 0; row < stages.Length; row += 4) {
                GUILayout.BeginHorizontal();
                for (int i = row; i < Mathf.Min(row + 4, stages.Length); i++) {
                    GUI.enabled = wasEnabled && allowed && stages[i] != null;
                    string mark = director != null && director.CurrentStageIndex == i ? "● " : "";
                    if (GUILayout.Button($"{mark}{i + 1}단계", button)) PursuitResult(director.DebugSetStage(i), $"추격 {i + 1}단계 고정");
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = wasEnabled && allowed;
            if (GUILayout.Button("단계 자동 진행으로 복귀", button)) PursuitResult(director.DebugSetStage(-1), "실제 추격 경과시간에 따른 단계로 복귀");
            GUI.enabled = wasEnabled;
            GUILayout.Label("양수 시간 설정은 추격을 철회합니다. 부상은 유지되며 ‘모두 되살리기’로 회복할 수 있습니다.", label);
        }
        void PursuitResult(bool success, string message) => Report(success, message, "대기실의 호스트/오프라인 런에서만 사용할 수 있습니다.");
    }
}
