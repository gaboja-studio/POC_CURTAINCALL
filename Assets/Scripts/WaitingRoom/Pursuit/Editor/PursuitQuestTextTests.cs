using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitQuestTextTests
    {
        [TearDown] public void Restore() => WaitingRoomText.Clear();

        [Test] public void QuestBodyAddsEscapeLineOnlyAfterVisit()
        {
            WaitingRoomText.Install("pursuit.quest.stage | - 입구\npursuit.quest.escape | - 탈출");
            Assert.AreEqual("- 입구", PursuitTexts.QuestBody(true, false));
            Assert.AreEqual("- 입구\n- 탈출", PursuitTexts.QuestBody(true, true));
            Assert.AreEqual("- 탈출", PursuitTexts.QuestBody(false, true), "마지막 아침에는 공연 줄이 없다");
            Assert.AreEqual("", PursuitTexts.QuestBody(false, false));
        }

        [Test] public void StageAnnouncementPrefersTextTable()
        {
            WaitingRoomText.Install("pursuit.stage.1 | 표 문구");
            Assert.AreEqual("표 문구", PursuitTexts.StageAnnouncement(0));
            Assert.AreEqual("", PursuitTexts.StageAnnouncement(-1));
            Assert.AreEqual("pursuit.stage.3", PursuitTexts.StageKey(2));
        }
    }
}
