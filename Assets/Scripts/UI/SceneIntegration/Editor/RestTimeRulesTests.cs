using CurtainCall.Campaign;
using CurtainCall.Survival;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    /// <summary>M2 쉬는 시간: 시작하자마자 흐르고, 만료 처리는 교체 가능한 잡힘 처리로 넘긴다.</summary>
    public sealed class RestTimeRulesTests
    {
        [Test]
        public void EveryRestStartsRunningWithoutLeavingWaitingRoom()
        {
            var run = new RunState();
            run.StartSession();
            Assert.IsTrue(run.EndIntro());
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            float before = run.Snapshot.TimeRemaining;
            Assert.IsTrue(run.Tick(1f));
            Assert.AreEqual(before - 1f, run.Snapshot.TimeRemaining, .0001f, "아무도 대기실을 나가지 않아도 흐른다");
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
