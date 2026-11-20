using CurtainCall.Campaign;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class RunAbandonTests
    {
        [TestCase(RunStartPhase.Morning)]
        [TestCase(RunStartPhase.Between)]
        [TestCase(RunStartPhase.Night)]
        public void Abandon_RestOrNight_UsesFailureEndingThenStartsNextGenerationOnce(RunStartPhase phase)
        {
            var run = new RunState();
            run.DebugStartAt(new RunStartPoint(2, phase, phase == RunStartPhase.Between ? 1 : 0, "Tightrope"));
            run.RecordClear("Tightrope", 2);
            var before = run.Snapshot;
            Assert.IsTrue(run.AbandonRun(before));
            // 기한 초과와 같은 실패 엔딩: 세대·날짜를 유지하고 실패 연출을 기다린다.
            Assert.AreEqual(CampaignPhase.Ending, run.Snapshot.Phase);
            Assert.AreEqual(RunEndReason.Abandoned, run.Snapshot.LastEnd);
            Assert.AreEqual(before.Generation, run.Snapshot.Generation);
            Assert.AreEqual(RestTimerState.None, run.Snapshot.Timer);
            Assert.IsTrue(CurtainCall.EscapeRoute.FailureEndingPresentation.IsFailureEnding(run.Snapshot));
            Assert.IsFalse(run.AbandonRun(before));
            Assert.AreEqual(before.Generation, run.Snapshot.Generation);

            Assert.IsTrue(run.RestartAfterFailure());
            Assert.AreEqual(before.Generation + 1, run.Snapshot.Generation);
            Assert.AreEqual(RunEndReason.Abandoned, run.Snapshot.LastEnd);
            Assert.AreEqual(1, run.Snapshot.Day);
            Assert.AreEqual(0, run.Snapshot.PerformanceIndex);
            Assert.AreEqual(1, run.Snapshot.Difficulty);
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
            Assert.AreEqual(RestKind.Morning, run.Snapshot.Rest);
            Assert.IsTrue(run.Snapshot.IsFirstMorningAfterEnding);
            Assert.AreEqual(0, run.Snapshot.ReadyCount);
            Assert.AreEqual(0, run.ClearedTier("Tightrope"));
            Assert.IsFalse(run.RestartAfterFailure(), "A repeated confirmation must not start another generation.");
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
            // 첫 공연 뒤 안내를 마친 다음에야 탈출 상태에 진입할 수 있다.
            Assert.IsTrue(run.SetTutorialDone(0));
            run.RefreshGuideRest(new ulong[] { 0 });
            Assert.IsTrue(run.StartEscape());
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
