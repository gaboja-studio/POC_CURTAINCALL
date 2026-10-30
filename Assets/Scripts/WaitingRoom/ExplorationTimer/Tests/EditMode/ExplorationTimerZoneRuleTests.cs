using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class ExplorationTimerZoneRuleTests
    {
        [Test]
        public void 재생_구역에_한_명이라도_있으면_흐른다()
        {
            Assert.IsFalse(ExplorationTimerZoneRule.Paused(new[] { (false, true), (true, false), (false, false) }));
        }

        [Test]
        public void 전원이_멈춤_구역이거나_구역_밖이면_멈춘다()
        {
            Assert.IsTrue(ExplorationTimerZoneRule.Paused(new[] { (false, true), (false, false) }));
            Assert.IsTrue(ExplorationTimerZoneRule.Paused(new (bool, bool)[0]));
        }

        [Test]
        public void 겹치는_자리는_멈춤이_우선한다()
        {
            Assert.IsFalse(ExplorationTimerZoneRule.IsExploring(true, true));
            Assert.IsTrue(ExplorationTimerZoneRule.Paused(new[] { (true, true) }));
        }
    }
}
