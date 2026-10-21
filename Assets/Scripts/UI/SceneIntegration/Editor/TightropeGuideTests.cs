using CurtainCall.Campaign;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class TightropeGuideTests
    {
        [Test]
        public void OnlyFirstDayTightropePerformanceShowsTutorial()
        {
            var state = new CampaignSnapshot { Day = 1, Phase = CampaignPhase.Performance, TrickId = "Tightrope" };
            Assert.IsTrue(TightropeGuideProgress.IsFirstDay(state));
            state.Day = 2;
            Assert.IsFalse(TightropeGuideProgress.IsFirstDay(state));
            state.Day = 3;
            Assert.IsFalse(TightropeGuideProgress.IsFirstDay(state));
            state.Day = 1;
            state.Phase = CampaignPhase.Intro;
            Assert.IsFalse(TightropeGuideProgress.IsFirstDay(state));
            state.Phase = CampaignPhase.RestTime;
            Assert.IsFalse(TightropeGuideProgress.IsFirstDay(state));
            state.Phase = CampaignPhase.Performance;
            state.TrickId = "HumanCannon";
            Assert.IsFalse(TightropeGuideProgress.IsFirstDay(state));
            Assert.IsFalse(TightropeGuideProgress.IsFirstDay(default));
        }

        [Test]
        public void LaneChangeFollowsObstacleOnceAndResetsForNewAttempt()
        {
            var progress = new TightropeGuideProgress();
            Assert.IsTrue(progress.Begin(1, 0, 1, 10));
            Assert.IsFalse(progress.LaneChangePending);
            progress.MarkShown("obstacle");
            Assert.IsTrue(progress.LaneChangePending);
            Assert.IsFalse(progress.Begin(1, 0, 1, 10));
            Assert.IsTrue(progress.LaneChangePending);
            progress.MarkShown("lane-change");
            progress.MarkShown("obstacle");
            Assert.IsFalse(progress.LaneChangePending);
            Assert.IsTrue(progress.Begin(1, 0, 2, 10));
            Assert.IsFalse(progress.LaneChangePending);
            Assert.IsFalse(progress.HasShown("lane-change"));
            progress.MarkShown("relay-held");
            Assert.IsTrue(progress.Begin(2, 0, 2, 10));
            Assert.IsFalse(progress.HasShown("relay-held"));
        }

        [Test]
        public void RelayHoldTextUsesActualCourseLimit()
        {
            Assert.AreEqual("최대 30초 간 버틸 수 있습니다.", TightropeGuideProgress.HoldText(30));
            Assert.AreEqual("최대 45초 간 버틸 수 있습니다.", TightropeGuideProgress.HoldText(45));
        }
    }
}
