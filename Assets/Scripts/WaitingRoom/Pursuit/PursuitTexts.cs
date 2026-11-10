using CurtainCall.Settings;
using CurtainCall.Campaign;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 추격 화면 문구(Task-103). 대기실 문구 표(WaitingRoomTexts.txt)의 <c>pursuit.*</c> 키에서 고친다.
    /// 단계 알림은 <c>pursuit.stage.&lt;번호&gt;</c>(1부터, 추격 설계 창의 단계 순서). 줄이 없으면 설정 에셋의 단계 알림 문구를 쓴다.
    /// </summary>
    public static class PursuitTexts
    {
        public const string AlertTitleKey = "pursuit.alert-title";
        public static string StageKey(int index) => "pursuit.stage." + (index + 1);

        static CampaignPhase Phase => CampaignRun.Instance != null ? CampaignRun.Instance.State.Phase : CampaignPhase.RestTime;
        public static string AlertTitle => GameSettings.WaitingRoom.Presentation(Phase).PursuitAlertTitle;

        /// <summary>단계 알림 문구. 문구 표 → 설정 에셋 순으로 찾는다. 없으면 "".</summary>
        public static string StageAnnouncement(int index)
        {
            if (index < 0) return "";
            string custom = GameSettings.WaitingRoom.Presentation(Phase).StageAnnouncement(index);
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            var stages = GameSettings.WaitingRoom.Pursuit.Stages;
            string fallback = stages != null && index < stages.Length && stages[index] != null ? stages[index].Announcement : "";
            return WaitingRoomText.Get(StageKey(index), fallback);
        }

        public static string QuestTitle => WaitingRoomText.Get("pursuit.quest.title", "추격자를 피해 달아나세요.");
        public static string QuestTitleFor(CampaignPhase phase) => GameSettings.WaitingRoom.Presentation(phase).QuestTitle;
        public static string QuestStageLine => WaitingRoomText.Get("pursuit.quest.stage", "- 모든 플레이어가 공연장 입구로 가서 공연 진행");
        public static string QuestEscapeLine => WaitingRoomText.Get("pursuit.quest.escape", "- 모든 플레이어가 탈출로로 진입해 탈출 시도");

        /// <summary>퀘스트 본문. 공연이 없는 마지막 아침에는 공연 줄을 빼고, 탈출로 입구를 방문한 런에만 탈출 줄을 붙인다.</summary>
        public static string QuestBody(bool performanceAvailable, bool escapeVisited, bool maintenance = false)
        {
            return GameSettings.WaitingRoom.Presentation(maintenance ? CampaignPhase.Night : CampaignPhase.RestTime)
                .QuestBody(performanceAvailable, escapeVisited, maintenance);
        }
    }
}
