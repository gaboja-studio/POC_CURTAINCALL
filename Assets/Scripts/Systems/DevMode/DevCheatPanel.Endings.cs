using CurtainCall.Campaign;
using UnityEngine;
using System.Linq;
using CurtainCall.Network.PlayerSync;

namespace CurtainCall.Dev
{
    public sealed partial class DevCheatPanel
    {
        void DrawEndingCheats()
        {
            GUILayout.Space(6);
            GUILayout.Label("엔딩 바로 확인", heading);
            GUILayout.Label("현재 런의 날짜·기물·통계를 유지하고 팀 전체의 런을 종료합니다.", label);
            var run = CampaignRun.Instance;
            GUI.enabled = CanShowFailure(run);
            if (GUILayout.Button("실패 엔딩 A · 시간 초과", button)) TryFailureEnding(RunEndReason.Deadline);
            if (GUILayout.Button("실패 엔딩 A · 탈출로 전원 사망", button)) TryFailureEnding(RunEndReason.EscapeAllRetired);
            GUI.enabled = true;
        }

        public bool TryFailureEnding(RunEndReason reason)
        {
            var run = CampaignRun.Instance;
            bool done = CanShowFailure(run) && run.DebugFailureEnding(reason);
            Status = done ? "실패 엔딩 A로 전환합니다." : "진행 중인 런의 호스트만 실행할 수 있습니다.";
            if (done && IsOpen) Toggle();
            return done;
        }
        static bool CanShowFailure(CampaignRun run) => DevMode.Enabled && run != null && run.HasAuthority
            && run.State.Phase is CampaignPhase.Escape or CampaignPhase.RestTime or CampaignPhase.Night or CampaignPhase.Performance or CampaignPhase.Rehearsal
            && NetworkPlayer.All.Any(p => p != null && p.IsSpawned)
            && (CurtainCall.SpaceLink.SpaceLinkDirector.Instance == null || !CurtainCall.SpaceLink.SpaceLinkDirector.Instance.IsTransitioning);
    }
}
