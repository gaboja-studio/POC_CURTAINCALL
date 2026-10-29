using CurtainCall.Results;
using NUnit.Framework;

namespace CurtainCall.Reward.Tests
{
    public class AudienceReactionTests
    {
        const string Session = "11111111111111111111111111111111";
        readonly RewardRules rules = new(10, 5, 100, 100, 10);
        PerformanceRewardModel Begin()
        {
            var model = new PerformanceRewardModel(Session, 1);
            Assert.IsTrue(model.Begin(new PerformanceIdentity(Session, 1, 1, 0, "Tightrope"),
                new[] { new RewardParticipantSeed("p", 1, 0), new RewardParticipantSeed("q", 2, 1) }, rules));
            return model;
        }
        [Test]
        public void ReactionsClampAndDeathFreezesCheer()
        {
            var model = Begin();
            model.React("p", 50, rules); model.React("p", 30, rules); model.React("p", 30, rules);
            Assert.AreEqual(100, model.Snapshot().Segments[0].Amount);
            model.React("p", -30, rules); Assert.AreEqual(70, model.Snapshot().Segments[0].Amount);
            model.Observe("p", 0, true, true, rules);
            Assert.IsFalse(model.React("p", 50, rules));
            Assert.IsFalse(model.React("p", -50, rules), "사망 후 호응도는 고정");
            model.Observe("p", 0, false, true, rules);
            Assert.AreEqual(70, model.Snapshot().Segments[0].Amount, "사망 전 호응도를 지우지 않는다");
            Assert.IsTrue(model.Snapshot().Segments[0].Dead);
        }
        [Test]
        public void CutIsAwardedOnceAndSurvivorDoublesCheer()
        {
            var model = Begin();
            model.Observe("p", 1, false, true, rules); model.Observe("p", 1, false, true, rules);
            Assert.AreEqual(10, model.Snapshot().Segments[0].Amount, "절단 1개당 +10, 같은 부위 중복 없음");
            model.React("p", 15, rules);
            Assert.IsTrue(model.Confirm(true, "Completed", null, 1, rules, out var result));
            Assert.AreEqual(50, result.Participants[0].Settlement, "생존 호응 25 × 2");
            Assert.AreEqual(50, result.Income);
            Assert.IsFalse(model.React("p", 15, rules));
        }
        [Test]
        public void CutThatKillsStillCountsBeforeFreeze()
        {
            var model = Begin();
            model.React("p", 20, rules);
            model.Observe("p", 15, false, true, rules);
            var segment = model.Snapshot().Segments[0];
            Assert.IsTrue(segment.Dead);
            Assert.AreEqual(60, segment.Amount, "20 + 절단 4개 × 10 뒤 사망 고정");
        }
        [Test]
        public void JeerAtZeroAndUnknownOrDisconnectedMemberCannotGain()
        {
            var model = Begin();
            Assert.IsFalse(model.React("p", -15, rules));
            Assert.IsFalse(model.React("missing", 50, rules));
            model.Observe("p", 0, false, false, rules);
            Assert.IsFalse(model.React("p", 50, rules));
            Assert.AreEqual(0, model.Snapshot().Segments[0].Amount, "이탈자는 0");
        }
        [Test]
        public void InterestDecaysAfterGraceAndCheerRestartsGrace()
        {
            var model = Begin();
            model.React("p", 30, rules);
            Assert.IsFalse(model.Tick(0, 20, 1), "첫 호출은 유예 시작만");
            Assert.IsFalse(model.Tick(20.5, 20, 1));
            Assert.IsTrue(model.Tick(21, 20, 1));
            Assert.AreEqual(29, model.Snapshot().Segments[0].Amount);
            Assert.IsTrue(model.Tick(24.2, 20, 1));
            Assert.AreEqual(26, model.Snapshot().Segments[0].Amount, "초당 −1, 긴 프레임은 지난 초만큼");
            model.React("p", 5, rules);
            Assert.IsFalse(model.Tick(44, 20, 1), "환호 뒤 20초 유예를 다시 센다");
            Assert.IsTrue(model.Tick(45.2, 20, 1));
            Assert.AreEqual(30, model.Snapshot().Segments[0].Amount);
        }
        [Test]
        public void StallJeerRestartsGraceAndTeammateCheerDoesNot()
        {
            var model = Begin();
            model.React("p", 30, rules); model.React("q", 30, rules);
            model.Tick(0, 20, 1);
            model.Tick(10, 20, 1);
            model.React("q", 10, rules);
            Assert.IsTrue(model.Tick(21, 20, 1));
            Assert.AreEqual(29, model.Snapshot().Segments[0].Amount, "동료 환호로 본인 감소는 멈추지 않는다");
            Assert.AreEqual(40, model.Snapshot().Segments[1].Amount);
            Assert.IsTrue(model.React("p", -10, rules, true));
            Assert.AreEqual(19, model.Snapshot().Segments[0].Amount);
            model.Tick(40, 20, 1);
            Assert.AreEqual(19, model.Snapshot().Segments[0].Amount, "시간 끌기 야유가 유예 20초를 다시 센다");
            Assert.AreEqual(30, model.Snapshot().Segments[1].Amount, "q는 31초부터 초당 −1");
        }
        [Test]
        public void ArrivedAndDeadDoNotDecay()
        {
            var model = Begin();
            model.React("p", 30, rules); model.React("q", 30, rules);
            model.Tick(0, 20, 1);
            Assert.IsTrue(model.MarkArrived("p"));
            model.Observe("q", 0, true, true, rules);
            Assert.IsFalse(model.Tick(60, 20, 1));
            Assert.AreEqual(30, model.Snapshot().Segments[0].Amount);
            Assert.AreEqual(30, model.Snapshot().Segments[1].Amount);
            Assert.IsFalse(model.React("p", 10, rules), "완주 뒤 고정");
        }
        [Test]
        public void DodgeRequiresApproachAndIsAwardedOnlyOnce()
        {
            var encounter = new AudienceEncounter();
            Assert.IsFalse(encounter.Sample(3, 8, 2, 0, false, false, false, false));
            Assert.IsFalse(encounter.Sample(-3, 8, 2, 0, false, true, false, false));
            Assert.IsTrue(encounter.Sample(3, 8, 2, 0, false, false, false, false));
            Assert.IsTrue(encounter.WasClose);
            Assert.IsFalse(encounter.Sample(-3, 8, 2, 0, false, false, false, false));
            Assert.IsFalse(encounter.Sample(3, 8, 2, 0, false, false, false, false));
        }
        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void TouchOrCutDisqualifiesDodge(bool hit, int parts)
        {
            var encounter = new AudienceEncounter();
            encounter.Sample(-3, 8, 2, 0, false, false, false, false);
            encounter.Sample(0, 8, 2, parts, hit, true, false, false);
            Assert.IsFalse(encounter.Sample(3, 8, 2, parts, false, false, false, false));
        }
        [Test]
        public void LaneChangeAndStackAreTrackedAcrossCrossing()
        {
            var encounter = new AudienceEncounter();
            encounter.Sample(-3, 8, 2, 0, false, false, false, false);
            encounter.Sample(0, 8, 2, 0, false, false, true, true);
            Assert.IsTrue(encounter.Sample(3, 8, 2, 0, false, false, false, true));
            Assert.IsTrue(encounter.WasStacked); Assert.IsTrue(encounter.ChangedLane);
        }
        [Test]
        public void StallRequiresContinuousNoProgressAndRepeats()
        {
            var stall = new AudienceStall();
            Assert.IsFalse(stall.Sample(true, 0, 0, 15, 1));
            Assert.IsFalse(stall.Sample(true, .5f, 14.9, 15, 1));
            Assert.IsTrue(stall.Sample(true, .5f, 15, 15, 1));
            Assert.IsFalse(stall.Sample(true, .5f, 29.9, 15, 1));
            Assert.IsTrue(stall.Sample(true, .5f, 30, 15, 1), "15초마다 반복");
            Assert.IsFalse(stall.Sample(true, 1.6f, 44, 15, 1), "진행하면 다시 센다");
            Assert.IsFalse(stall.Sample(true, 1.6f, 58, 15, 1));
            Assert.IsTrue(stall.Sample(true, 1.6f, 59, 15, 1));
            Assert.IsFalse(stall.Sample(false, 1.6f, 80, 15, 1), "받기 대기 등 제외 상태는 지운다");
            Assert.IsFalse(stall.Sample(true, 1.6f, 90, 15, 1));
            Assert.IsTrue(stall.Sample(true, 1.6f, 105, 15, 1));
        }
        [Test]
        public void ProximityDodgeResolvesWhenMovingThreatLeavesWithoutForwardMovement()
        {
            var encounter = new AudienceEncounter();
            Assert.IsFalse(encounter.SampleProximity(3, 8, 0, false, false));
            Assert.IsFalse(encounter.SampleProximity(0, 8, 0, false, true));
            Assert.IsTrue(encounter.SampleProximity(1, 8, 0, false, false));
            Assert.IsFalse(encounter.SampleProximity(0, 8, 0, false, true));
            Assert.IsFalse(encounter.SampleProximity(1, 8, 0, false, false));
        }
        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void ProximityHitOrCutNeverRewards(bool hit, int parts)
        {
            var encounter = new AudienceEncounter();
            encounter.SampleProximity(3, 8, 0, false, false);
            encounter.SampleProximity(0, 8, parts, hit, true);
            Assert.IsFalse(encounter.SampleProximity(1, 8, parts, false, false));
        }
        [Test]
        public void SuccessResetsFullStallWait()
        {
            var stall = new AudienceStall();
            stall.Sample(true, 0, 0, 15, 1);
            stall.Reset();
            Assert.IsFalse(stall.Sample(true, 0, 14, 15, 1));
            Assert.IsFalse(stall.Sample(true, 0, 28.9, 15, 1));
            Assert.IsTrue(stall.Sample(true, 0, 29, 15, 1));
        }
        [Test]
        public void ExplicitReactionHistoryIgnoresDecayAndZeroBooAndIncludesCapSuccess()
        {
            var model = Begin();
            long initial = model.Revision;
            model.React("p", -15, rules, true);
            Assert.AreEqual(initial, model.Revision);
            Assert.AreEqual(0, model.Snapshot().Segments[0].ReactionSequence);
            model.React("p", 100, rules);
            model.Tick(0, 20, 1); model.Tick(10, 20, 1);
            Assert.IsFalse(model.React("p", 5, rules));
            var success = model.Snapshot().Segments[0];
            Assert.AreEqual(2, success.ReactionSequence);
            Assert.AreEqual(5, success.ReactionDelta);
            Assert.AreEqual(0, success.ReactionAmount);
            Assert.IsFalse(model.Tick(30, 20, 1));
            Assert.IsTrue(model.Tick(31, 20, 1));
            Assert.AreEqual(2, model.Snapshot().Segments[0].ReactionSequence);
        }
        [Test]
        public void CutSuccessRestartsPersonalGrace()
        {
            var model = Begin();
            model.React("p", 20, rules);
            model.Tick(0, 20, 1); model.Tick(19, 20, 1);
            model.Observe("p", 1, false, true, rules);
            Assert.AreEqual(2, model.Snapshot().Segments[0].ReactionSequence);
            Assert.IsFalse(model.Tick(39, 20, 1));
            Assert.IsTrue(model.Tick(40, 20, 1));
        }
    }
}
