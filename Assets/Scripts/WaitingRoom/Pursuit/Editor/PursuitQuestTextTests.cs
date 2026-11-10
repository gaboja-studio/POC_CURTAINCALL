using CurtainCall.Campaign;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    [Category("BreakFollowup")]
    public sealed class PursuitQuestTextTests
    {
        string originalRest, originalNight;
        [SetUp] public void Save()
        {
            originalRest = JsonUtility.ToJson(GameSettings.WaitingRoom.RestPresentation);
            originalNight = JsonUtility.ToJson(GameSettings.WaitingRoom.MaintenancePresentation);
        }
        [TearDown] public void Restore()
        {
            JsonUtility.FromJsonOverwrite(originalRest, GameSettings.WaitingRoom.RestPresentation);
            JsonUtility.FromJsonOverwrite(originalNight, GameSettings.WaitingRoom.MaintenancePresentation);
            WaitingRoomText.Clear();
        }
        [Test] public void QuestBodyAndWarningsAreIndependentForEachTimePeriod()
        {
            JsonUtility.FromJsonOverwrite("{\"questTitle\":\"휴식 임무\",\"stageObjective\":\"- 입구\",\"escapeObjective\":\"- 탈출\",\"minute\":{\"title\":\"휴식 60\",\"body\":\"휴식 남음\"}}", GameSettings.WaitingRoom.RestPresentation);
            JsonUtility.FromJsonOverwrite("{\"questTitle\":\"정비 임무\",\"stageObjective\":\"- 다음 공연\",\"escapeObjective\":\"- 정비 탈출\",\"sleepObjective\":\"- 전원 잠자기\",\"minute\":{\"title\":\"정비 60\",\"body\":\"정비 남음\"},\"thirtySeconds\":{\"title\":\"정비 30\",\"body\":\"서두르세요\"}}", GameSettings.WaitingRoom.MaintenancePresentation);
            Assert.AreEqual("- 입구", PursuitTexts.QuestBody(true, false));
            Assert.AreEqual("- 입구\n- 탈출", PursuitTexts.QuestBody(true, true));
            Assert.AreEqual("- 탈출", PursuitTexts.QuestBody(false, true));
            Assert.AreEqual("", PursuitTexts.QuestBody(false, false));
            Assert.AreEqual("- 다음 공연\n- 전원 잠자기", PursuitTexts.QuestBody(true, false, true));
            Assert.AreEqual("휴식 임무", PursuitTexts.QuestTitleFor(CampaignPhase.RestTime));
            Assert.AreEqual("정비 임무", PursuitTexts.QuestTitleFor(CampaignPhase.Night));
            Assert.AreEqual("휴식 60", GameSettings.WaitingRoom.Presentation(CampaignPhase.RestTime).Notice(60).Title);
            Assert.AreEqual("정비 남음", GameSettings.WaitingRoom.Presentation(CampaignPhase.Night).Notice(60).Body);
            Assert.AreEqual("서두르세요", GameSettings.WaitingRoom.Presentation(CampaignPhase.Night).Notice(30).Body);
        }
        [Test] public void StageAnnouncementKeepsLegacyFallbackAndAllowsExplicitPeriodOverride()
        {
            WaitingRoomText.Install("pursuit.stage.1 | 표 문구");
            Assert.AreEqual("표 문구", PursuitTexts.StageAnnouncement(0));
            JsonUtility.FromJsonOverwrite("{\"stageAnnouncements\":[\"휴식 추격\"]}", GameSettings.WaitingRoom.RestPresentation);
            Assert.AreEqual("휴식 추격", PursuitTexts.StageAnnouncement(0));
            Assert.AreEqual("", PursuitTexts.StageAnnouncement(-1));
        }
    }
}
