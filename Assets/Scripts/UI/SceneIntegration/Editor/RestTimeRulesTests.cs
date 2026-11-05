using CurtainCall.Campaign;
using CurtainCall.Survival;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    /// <summary>M2 쉬는 시간: 시작하자마자 흐르고, 만료 처리는 교체 가능한 잡힘 처리로 넘긴다.</summary>
    public sealed class RestTimeRulesTests
    {
        [Test]
        public void FirstDayUnconfirmedScheduleCannotEnterByGatherOrTimeout()
        {
            var original = RunState.SecondPerformanceGate;
            try
            {
                bool confirmed = false;
                RunState.SecondPerformanceGate = () => confirmed;
                var run = new RunState(); run.StartSession(); run.EndIntro(); run.EndPerformance();
                run.SetTutorialDone(0); run.RefreshGuideRest(new ulong[] { 0 });
                float remaining = run.Snapshot.TimeRemaining;
                Assert.IsFalse(run.NotifyAllAtStageEntrance());
                Assert.IsFalse(run.Tick(10000f));
                Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
                Assert.AreEqual(remaining, run.Snapshot.TimeRemaining);
                confirmed = true;
                Assert.IsTrue(run.NotifyAllAtStageEntrance());
                Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            }
            finally { RunState.SecondPerformanceGate = original; }
        }

        [Test]
        public void UnconfirmedScheduleSharesStoppedTimerUntilConfirmed()
        {
            var original = RunState.SecondPerformanceGate;
            try
            {
                bool confirmed = false;
                RunState.SecondPerformanceGate = () => confirmed;
                var run = new RunState(); run.StartSession(); run.EndIntro(); run.EndPerformance();
                run.SetTutorialDone(0); run.RefreshGuideRest(new ulong[] { 0 });
                Assert.IsTrue(run.SetRestTimerPaused(false), "호스트가 멈춘 시간을 받는 쪽도 멈춤으로 받아야 한다");
                Assert.AreEqual(RestTimerState.Stopped, run.Snapshot.Timer);
                confirmed = true;
                Assert.IsTrue(run.SetRestTimerPaused(false));
                Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            }
            finally { RunState.SecondPerformanceGate = original; }
        }

        [Test]
        public void EveryRestStartsRunningWithoutLeavingWaitingRoom()
        {
            var run = new RunState();
            run.StartSession();
            Assert.IsTrue(run.EndIntro());
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
            // Task-097: 1일차 가이드 쉬는 시간만 전원 tutorial 완료를 기다린다.
            Assert.AreEqual(RestTimerState.WaitingForExit, run.Snapshot.Timer);
            Assert.IsFalse(run.Tick(1f), "tutorial 전에는 흐르지 않는다");
            Assert.IsTrue(run.SetTutorialDone(0));
            Assert.IsTrue(run.RefreshGuideRest(new ulong[] { 0 }));
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            float before = run.Snapshot.TimeRemaining;
            Assert.IsTrue(run.Tick(1f));
            Assert.AreEqual(before - 1f, run.Snapshot.TimeRemaining, .0001f, "아무도 대기실을 나가지 않아도 흐른다");
        }

        [Test]
        public void FirstDay_WaitsForEveryoneThenSecondPerformanceAndNight()
        {
            var run = new RunState();
            run.StartSession(withRehearsal: true);
            Assert.IsTrue(run.EndIntro());
            Assert.AreEqual(CampaignPhase.Rehearsal, run.Snapshot.Phase);
            Assert.IsTrue(run.EndRehearsal());
            Assert.AreEqual(1, run.Snapshot.Day);
            Assert.AreEqual(0, run.Snapshot.PerformanceIndex);
            run.EndPerformance();
            Assert.IsFalse(run.NotifyAllAtStageEntrance(), "안내 전 집결로 진행하지 않는다");
            run.SetTutorialDone(0);
            run.RefreshGuideRest(new ulong[] { 0, 1 });
            Assert.IsFalse(run.Tick(10000f));
            Assert.IsFalse(run.NotifyAllAtStageEntrance(), "한 명만 완료하면 진행하지 않는다");
            run.SetTutorialDone(1);
            run.RefreshGuideRest(new ulong[] { 0, 1 });
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.AreEqual(1, run.Snapshot.Day);
            Assert.AreEqual(1, run.Snapshot.PerformanceIndex);
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            run.EndPerformance();
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase);
            Assert.AreEqual(1, run.Snapshot.Day);
        }

        [Test]
        public void ExpiryCallsReplaceableHandlerBeforePerformanceOnly()
        {
            var original = RestTimeoutCatch.Handler;
            int calls = 0;
            CampaignSnapshot seen = default;
            try
            {
                RestTimeoutCatch.Handler = step => { calls++; seen = step; return 2; };
                var run = new RunState();
                run.StartSession();
                run.EndIntro();
                run.EndPerformance();
                run.SetTutorialDone(0); run.RefreshGuideRest(new ulong[] { 0 }); // 가이드 쉬는 시간은 tutorial 뒤에 흐른다(Task-097)
                Assert.IsTrue(run.Tick(10000f, step => RestTimeoutCatch.ServerHandleExpired(step)));
                Assert.AreEqual(1, calls);
                Assert.AreEqual(CampaignPhase.RestTime, seen.Phase, "공연으로 넘어가기 전 쉬는 시간 상태로 처리한다");
                Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);

                Assert.AreEqual(0, RestTimeoutCatch.ServerHandleExpired(run.Snapshot), "쉬는 시간이 아니면 잡지 않는다");
                var finalMorning = new CampaignSnapshot { Phase = CampaignPhase.RestTime, IsFinalMorning = true };
                Assert.AreEqual(0, RestTimeoutCatch.ServerHandleExpired(finalMorning), "마지막 아침 만료는 런 실패라 잡지 않는다");
                Assert.AreEqual(1, calls);
            }
            finally { RestTimeoutCatch.Handler = original; }
        }
    }
}
