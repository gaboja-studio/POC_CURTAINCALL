using CurtainCall.Campaign;
using CurtainCall.Settings;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class DevStartPointTests
    {
        [Test]
        public void SelectedPerformance_ContinuesThroughNormalNightAndMorning()
        {
            var run = new RunState();
            Assert.IsTrue(run.DebugStartAt(new RunStartPoint(2, RunStartPhase.Performance, 1, "HumanCannon")));
            Assert.AreEqual(2, run.Snapshot.Day);
            Assert.AreEqual(1, run.Snapshot.PerformanceIndex);
            Assert.AreEqual("HumanCannon", run.Snapshot.TrickId);
            Assert.AreEqual(RestTimerState.None, run.Snapshot.Timer);
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase);
            run.SetReady(0, true);
            run.RefreshNight(new ulong[] { 0 });
            Assert.AreEqual(3, run.Snapshot.Day);
            Assert.AreEqual(RestKind.Morning, run.Snapshot.Rest);
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.AreEqual("Tightrope", run.Snapshot.TrickId, "Skipped nights must not leave the next test performance undefined.");
        }

        [Test]
        public void UnscheduledDays_DefaultToTightrope_AndNightEndsWithoutConfirmation()
        {
            // M2(Task-078): 스케줄표 편성 보류 동안 2일차부터 편성하지 않은 공연은 외줄타기, 밤은 침대 준비만으로 끝난다.
            var run = new RunState();
            run.StartSession();
            Assert.IsTrue(run.EndIntro());
            Assert.IsTrue(run.EndPerformance());
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase);
            Assert.IsTrue(run.SetReady(0, true));
            run.RefreshNight(new ulong[] { 0 });
            Assert.AreEqual(2, run.Snapshot.Day);
            Assert.AreEqual(RestKind.Morning, run.Snapshot.Rest);
            Assert.AreEqual("Tightrope", run.Snapshot.TrickId);
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual("Tightrope", run.Snapshot.TrickId, "Second performance of an unscheduled day.");
            // A confirmed setlist still wins over the default.
            Assert.IsTrue(run.SetSetlist(2, new[] { "Tightrope", "HumanCannon" }));
            Assert.AreEqual("HumanCannon", run.Snapshot.TrickId);
        }

        [TestCase(1, RestKind.Guide)]
        [TestCase(2, RestKind.Between)]
        public void Between_StartsWithNormalTimerAndChosenNextPerformance(int day, RestKind kind)
        {
            var run = new RunState();
            run.DebugStartAt(new RunStartPoint(day, RunStartPhase.Between, 1, "Tightrope"));
            Assert.AreEqual(kind, run.Snapshot.Rest);
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer, "M2: 쉬는 시간 시작부터 흐른다");
            Assert.Greater(run.Snapshot.TimeRemaining, 0);
            run.NotifyAllAtStageEntrance();
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            Assert.AreEqual("Tightrope", run.Snapshot.TrickId);
            Assert.AreEqual(1, run.Snapshot.PerformanceIndex);
        }

        [Test]
        public void FinalMorning_HasNoPerformanceAndUsesNormalDeadlineFailure()
        {
            var run = new RunState();
            run.DebugStartAt(new RunStartPoint(GameSettings.Base.Run.DayCount + 1, RunStartPhase.Morning, 0, ""));
            Assert.IsTrue(run.Snapshot.IsFinalMorning);
            Assert.IsEmpty(run.Snapshot.TrickId);
            Assert.IsFalse(run.NotifyAllAtStageEntrance());
            run.DebugSetRemaining(0);
            run.Tick(.1f);
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            Assert.AreEqual(RunEndReason.Deadline, run.Snapshot.LastEnd);
        }

        [TestCase(0, RunStartPhase.Performance, 0, "Tightrope")]
        [TestCase(1, RunStartPhase.Morning, 0, "Tightrope")]
        [TestCase(1, RunStartPhase.Performance, -1, "Tightrope")]
        [TestCase(1, RunStartPhase.Performance, 2, "Tightrope")]
        [TestCase(2, RunStartPhase.Between, 0, "Tightrope")]
        [TestCase(2, RunStartPhase.Performance, 0, "Missing")]
        [TestCase(4, RunStartPhase.Night, 0, "")]
        [TestCase(5, RunStartPhase.Morning, 0, "")]
        [TestCase(1, (RunStartPhase)99, 0, "Tightrope")]
        public void InvalidPoint_DoesNotChangeRun(int day, RunStartPhase phase, int slot, string trick)
        {
            var run = new RunState(); run.StartSession(); run.EndIntro();
            var before = run.Snapshot;
            Assert.IsFalse(run.DebugStartAt(new RunStartPoint(day, phase, slot, trick)));
            Assert.AreEqual(before, run.Snapshot);
        }

        [Test]
        public void Reentry_ResetsReadinessAndOrdinaryRunRestoresDayOneOrder()
        {
            var run = new RunState();
            var night = new RunStartPoint(2, RunStartPhase.Night, 0, "");
            run.DebugStartAt(night);
            run.SetReady(0, true);
            run.DebugStartAt(night);
            run.RefreshNight(new ulong[] { 0 });
            Assert.AreEqual(2, run.Snapshot.Generation);
            Assert.AreEqual(0, run.Snapshot.ReadyCount);
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase);
            run.DebugStartAt(new RunStartPoint(1, RunStartPhase.Performance, 0, "HumanCannon"));
            Assert.AreEqual("HumanCannon", run.Snapshot.TrickId);
            Assert.IsFalse(run.SetSetlist(1, new[] { "HumanCannon", "HumanCannon" }));
            run.StartSession(); run.EndIntro();
            Assert.AreEqual(GameSettings.Base.Run.FirstDayTrick(0), run.Snapshot.TrickId);
        }
    }
}
