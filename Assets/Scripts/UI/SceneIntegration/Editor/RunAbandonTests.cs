using CurtainCall.Campaign;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class RunAbandonTests
    {
        [TestCase(RunStartPhase.Morning)]
        [TestCase(RunStartPhase.Between)]
        [TestCase(RunStartPhase.Night)]
        public void Abandon_RestOrNight_StartsNextGenerationOnce(RunStartPhase phase)
        {
            var run = new RunState();
            run.DebugStartAt(new RunStartPoint(2, phase, phase == RunStartPhase.Between ? 1 : 0, "Tightrope"));
            run.RecordClear("Tightrope", 2);
            var before = run.Snapshot;
            Assert.IsTrue(run.AbandonRun(before));
            Assert.AreEqual(before.Generation + 1, run.Snapshot.Generation);
            Assert.AreEqual(RunEndReason.Abandoned, run.Snapshot.LastEnd);
            Assert.AreEqual(1, run.Snapshot.Day);
            Assert.AreEqual(0, run.Snapshot.PerformanceIndex);
            Assert.AreEqual(1, run.Snapshot.Difficulty);
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            Assert.AreEqual(0, run.Snapshot.ReadyCount);
            Assert.AreEqual(0, run.ClearedTier("Tightrope"));
            Assert.IsFalse(run.AbandonRun(before));
            Assert.AreEqual(before.Generation + 1, run.Snapshot.Generation);
        }

        [Test]
        public void Abandon_PerformanceIntroEscapeAndLobby_AreRejected()
        {
            var run = new RunState();
            Assert.IsFalse(run.AbandonRun(run.Snapshot));
            run.StartSession();
            Assert.IsFalse(run.AbandonRun(run.Snapshot));
            run.EndIntro();
            Assert.IsFalse(run.AbandonRun(run.Snapshot));
            run.EndPerformance();
            run.StartEscape();
            var before = run.Snapshot;
            Assert.IsFalse(run.AbandonRun(before));
            Assert.AreEqual(before, run.Snapshot);
        }

        [Test]
        public void Abandon_StaleConfirmation_DoesNotChangeCurrentRun()
        {
            var run = new RunState();
            run.DebugStartAt(new RunStartPoint(2, RunStartPhase.Morning, 0, "Tightrope"));
            var old = run.Snapshot;
            run.DebugStartAt(new RunStartPoint(2, RunStartPhase.Morning, 0, "Tightrope"));
            var current = run.Snapshot;
            Assert.IsFalse(run.AbandonRun(old));
            Assert.AreEqual(current, run.Snapshot);
            old = current;
            old.Day++;
            Assert.IsFalse(run.AbandonRun(old));
            old = current;
            old.Phase = CampaignPhase.Night;
            Assert.IsFalse(run.AbandonRun(old));
            Assert.IsTrue(run.AbandonRun(current));
        }

        [Test]
        public void Abandon_TimerTickDoesNotInvalidateConfirmation()
        {
            var run = new RunState();
            run.DebugStartAt(new RunStartPoint(2, RunStartPhase.Morning, 0, "Tightrope"));
            var before = run.Snapshot;
            run.Tick(.1f);
            Assert.IsTrue(run.AbandonRun(before));
        }
    }
}
