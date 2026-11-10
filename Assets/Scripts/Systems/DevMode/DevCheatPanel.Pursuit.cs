using CurtainCall.Campaign;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEngine;

namespace CurtainCall.Dev
{
    public sealed partial class DevCheatPanel
    {
        public bool ShowsPursuitControls => CampaignRun.Instance != null
            && PursuitDirector.IsPursuitPhase(CampaignRun.Instance.State.Phase) && CommonUiSceneLoader.CurrentWaitingRoom != null;
        void DrawPursuitCheats()
        {
            var run = CampaignRun.Instance;
            if (!ShowsPursuitControls) return;
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
            if (!allowed) GUILayout.Label(run.HasAuthority ? (WaitingRoomScenes.PursuitEnabled ? "추격 시스템을 준비 중입니다." : "현재 대기실 설정에서는 추격을 사용하지 않습니다.") : "호스트만 대기실 치트를 변경할 수 있습니다.", label);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && allowed;
            bool invulnerable = director != null && director.DebugInvulnerable;
            bool nextInvulnerable = GUILayout.Toggle(invulnerable, $" 무적 · 추격자 포획 방지 {(invulnerable ? "ON" : "OFF")}", toggle);
            if (nextInvulnerable != invulnerable)
                PursuitResult(director.DebugSetInvulnerable(nextInvulnerable), nextInvulnerable ? "전원 포획 방지 ON · 추격은 유지" : "전원 포획 방지 OFF");
            bool recognition = director == null || director.DebugRecognitionEnabled;
            bool nextRecognition = GUILayout.Toggle(recognition, $" 추격자 인식 {(recognition ? "ON" : "OFF")} · 시야 / 소리", toggle);
            if (nextRecognition != recognition)
                PursuitResult(director.DebugSetRecognition(nextRecognition), nextRecognition ? "전원 인식 ON" : "전원 인식 OFF · 추격/소리 조사 중단, 순찰 유지");
            GUILayout.Label("호스트가 변경하면 전원에게 적용됩니다. 무적은 포획만 막고, 인식 OFF는 4단계 상시 추격도 막습니다.", label);
            GUILayout.Label("이미 시작한 포획은 계속됩니다. 해제는 아래 ‘타이머 초기화 및 추격 철회’를 사용하세요. 두 옵션은 대기실을 떠나면 초기화됩니다.", label);
            GUILayout.Space(6);
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
