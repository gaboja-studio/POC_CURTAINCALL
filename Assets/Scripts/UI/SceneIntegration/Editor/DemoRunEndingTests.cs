using CurtainCall.Campaign;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class DemoRunEndingTests
    {
        [Test]
        public void ThirdNightReady_DemoEndsWithoutStartingOrExpiringRestTimer()
        {
            var run = new RunState(true);
            run.DebugStartAt(new RunStartPoint(3, RunStartPhase.Night, 0, ""));
            int generation = run.Snapshot.Generation;
            run.SetReady(0, true);
            Assert.IsTrue(run.RefreshNight(new ulong[] { 0 }));
            Assert.AreEqual(4, run.Snapshot.Day);
            Assert.AreEqual(CampaignPhase.DemoEnding, run.Snapshot.Phase);
            Assert.AreEqual(SpaceId.WaitingRoom, SpaceRoute.ForPhase(run.Snapshot.Phase));
            Assert.AreEqual(RestTimerState.None, run.Snapshot.Timer);
            Assert.AreEqual(0, run.Snapshot.TimeRemaining);
            Assert.IsFalse(run.Snapshot.AlarmRaised);
            Assert.IsFalse(run.NotifyLeftWaitingRoom());
            Assert.IsFalse(run.DebugSetRemaining(0));
            Assert.IsFalse(run.NotifyAllAtStageEntrance());
            Assert.IsFalse(run.StartEscape());
            bool expired = false;
            Assert.IsFalse(run.Tick(1000, _ => expired = true));
            Assert.IsFalse(expired);
            Assert.AreEqual(generation, run.Snapshot.Generation);
            Assert.AreEqual(0, run.Snapshot.ReadyCount, "Night readiness must not confirm the ending.");
            Assert.IsTrue(run.SetReady(0, true));
            Assert.IsTrue(run.RefreshNight(new ulong[] { 0 }));
            Assert.AreEqual(generation + 1, run.Snapshot.Generation);
            Assert.AreEqual(RunEndReason.Deadline, run.Snapshot.LastEnd);
            AssertFirstPerformance(run);
            Assert.IsFalse(run.SetReady(0, true), "Completion must not reset the next run twice.");
        }

        [Test]
        public void OtherVersion_FourthMorningStillOffersNormalRest()
        {
            var run = new RunState(false);
            run.DebugStartAt(new RunStartPoint(3, RunStartPhase.Night, 0, ""));
            run.SetReady(0, true);
            run.RefreshNight(new ulong[] { 0 });
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
            Assert.Greater(run.Snapshot.TimeRemaining, 0);
            Assert.IsFalse(run.SetReady(0, true));
            Assert.IsTrue(run.StartEscape());
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Dawn_WaitsForEveryCoverBeforeAdvancingDay(int night)
        {
            var run = new RunState(true, true);
            var players = new ulong[] { 0, 1 };
            run.DebugStartAt(new RunStartPoint(night, RunStartPhase.Night, 0, ""));
            run.SetReady(0, true); run.SetReady(1, true); run.RefreshNight(players);
            Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase);
            Assert.AreEqual(night, run.Snapshot.Day);
            Assert.AreEqual(RestTimerState.None, run.Snapshot.Timer);
            Assert.AreEqual(0, run.Snapshot.ReadyCount);
            run.SetReady(0, true); run.RefreshNight(players);
            Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase);
            run.SetReady(1, true); run.RefreshNight(players);
            Assert.AreEqual(night + 1, run.Snapshot.Day);
            Assert.AreEqual(night == 3 ? CampaignPhase.DemoEnding : CampaignPhase.RestTime, run.Snapshot.Phase);
            if (night != 3) return;
            int generation = run.Snapshot.Generation;
            run.RefreshNight(players);
            Assert.AreEqual(CampaignPhase.DemoEnding, run.Snapshot.Phase, "Cover acknowledgments must not confirm the demo.");
            run.SetReady(0, true); run.RefreshNight(players);
            Assert.AreEqual(CampaignPhase.DemoEnding, run.Snapshot.Phase);
            run.SetReady(1, true); run.RefreshNight(players);
            Assert.AreEqual(generation + 1, run.Snapshot.Generation);
            AssertFirstPerformance(run);
        }

        [Test]
        public void DisconnectedPlayer_DoesNotLeaveEndingPermanentlyBlocked()
        {
            var run = new RunState(true, true);
            run.DebugStartAt(new RunStartPoint(4, RunStartPhase.Morning, 0, ""));
            run.SetReady(0, true); run.RefreshNight(new ulong[] { 0, 1 });
            Assert.AreEqual(CampaignPhase.DemoEnding, run.Snapshot.Phase);
            run.RefreshNight(new ulong[] { 0 });
            AssertFirstPerformance(run);
        }

        [Test]
        public void EscapeAllRetired_ReturnsDirectlyToFirstPerformance()
        {
            var run = new RunState();
            run.DebugStartAt(new RunStartPoint(2, RunStartPhase.Morning, 0, "Tightrope"));
            int generation = run.Snapshot.Generation;
            run.StartEscape();
            Assert.IsTrue(run.EndEscape(false));
            Assert.AreEqual(generation + 1, run.Snapshot.Generation);
            Assert.AreEqual(RunEndReason.EscapeAllRetired, run.Snapshot.LastEnd);
            AssertFirstPerformance(run);
        }

        static void AssertFirstPerformance(RunState run)
        {
            Assert.AreEqual(1, run.Snapshot.Day);
            Assert.AreEqual(0, run.Snapshot.PerformanceIndex);
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            Assert.AreEqual(GameSettings.Base.Run.FirstDayTrick(0), run.Snapshot.TrickId);
            Assert.AreEqual(SpaceId.Performance, SpaceRoute.ForPhase(run.Snapshot.Phase));
            Assert.AreEqual(RestTimerState.None, run.Snapshot.Timer);
            Assert.AreEqual(0, run.Snapshot.ReadyCount);
        }
    }
}
