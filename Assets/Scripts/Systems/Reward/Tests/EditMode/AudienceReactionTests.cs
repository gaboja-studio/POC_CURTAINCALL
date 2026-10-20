using CurtainCall.Results;
using NUnit.Framework;

namespace CurtainCall.Reward.Tests
{
    public class AudienceReactionTests
    {
        const string Session = "11111111111111111111111111111111";
        readonly RewardRules rules = new(10, 5, 100, 0, 30);
        PerformanceRewardModel Begin()
        {
            var model = new PerformanceRewardModel(Session, 1);
            Assert.IsTrue(model.Begin(new PerformanceIdentity(Session, 1, 1, 0, "Tightrope"),
                new[] { new RewardParticipantSeed("p", 1, 0), new RewardParticipantSeed("q", 2, 1) }, rules));
            return model;
        }
        [Test]
        public void ReactionsClampAndFallCannotBeReversed()
        {
            var model = Begin();
            model.React("p", 50, rules); model.React("p", 30, rules); model.React("p", 30, rules);
            Assert.AreEqual(100, model.Snapshot().Segments[0].Amount);
            model.React("p", -30, rules); Assert.AreEqual(70, model.Snapshot().Segments[0].Amount);
            model.Observe("p", 0, true, true, rules);
            Assert.IsFalse(model.React("p", 50, rules));
            model.Observe("p", 0, false, true, rules);
            Assert.AreEqual(0, model.Snapshot().Segments[0].Amount);
            Assert.IsTrue(model.Snapshot().Segments[0].Dead);
        }
        [Test]
        public void CutIsAwardedOnceAndArrivalIncludedInSettlement()
        {
            var model = Begin();
            model.Observe("p", 1, false, true, rules); model.Observe("p", 1, false, true, rules);
            Assert.AreEqual(30, model.Snapshot().Segments[0].Amount);
            model.React("p", 15, rules);
            Assert.IsTrue(model.Confirm(true, "Completed", null, 1, rules, out var result));
            Assert.AreEqual(65, result.Income);
            Assert.IsFalse(model.React("p", 15, rules));
        }
        [Test]
        public void JeerAtZeroAndUnknownOrDisconnectedMemberCannotGain()
        {
            var model = Begin();
            Assert.IsFalse(model.React("p", -15, rules));
            Assert.IsFalse(model.React("missing", 50, rules));
            model.Observe("p", 0, false, false, rules);
            Assert.IsFalse(model.React("p", 50, rules));
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
        public void HangingPenaltyOncePerContinuousHold()
        {
            var timer = new AudienceDuration();
            Assert.IsFalse(timer.Tick(true, 0, 3, false));
            Assert.IsFalse(timer.Tick(true, 2.99, 3, false));
            Assert.IsTrue(timer.Tick(true, 3, 3, false));
            Assert.IsFalse(timer.Tick(true, 10, 3, false));
            timer.Tick(false, 11, 3, false);
            Assert.IsFalse(timer.Tick(true, 12, 3, false));
            Assert.IsTrue(timer.Tick(true, 15, 3, false));
        }
        [Test]
        public void LowerFloorRequiresContinuousTenSecondsAndRepeats()
        {
            var timer = new AudienceDuration();
            timer.Tick(true, 0, 10, true);
            timer.Tick(false, 9, 10, true);
            Assert.IsFalse(timer.Tick(true, 10, 10, true));
            Assert.IsFalse(timer.Tick(true, 19.99, 10, true));
            Assert.IsTrue(timer.Tick(true, 20, 10, true));
            Assert.IsFalse(timer.Tick(true, 20, 10, true));
            Assert.IsTrue(timer.Tick(true, 30, 10, true));
        }
    }
}
