using System.Reflection;
using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonInjuryTests
    {
        [TestCase(0, 1f)]
        [TestCase(1, .8f)]
        [TestCase(2, .5f)]
        public void CannonUsesCommonMovementAndAirPenaltiesWithoutInteractionDelay(int legs, float expected)
        {
            var settings = new HumanCannonInjurySettings();
            float crawl = legs >= 2 ? .5f : 1f;
            Assert.That(settings.MovePolicyMultiplier(legs, .5f) * crawl, Is.EqualTo(expected).Within(.001f));
            Assert.That(settings.AirMultiplier(legs), Is.EqualTo(expected).Within(.001f));
            Assert.That(settings.InteractionSeconds(false), Is.Zero);
            Assert.That(settings.InteractionSeconds(true), Is.Zero);
        }

        [TestCase(.1f)]
        [TestCase(.5f)]
        [TestCase(.9f)]
        [TestCase(0f)]
        public void CannonDoesNotCancelOrRaiseTheCommonCrawlMultiplier(float crawl)
        {
            var settings = new HumanCannonInjurySettings();
            Assert.That(settings.MovePolicyMultiplier(2, crawl), Is.EqualTo(1f));
        }
        [Test]
        public void DelayedRequest_DuplicateCannotAdvanceOrPostponeDeadline()
        {
            var pending = new CannonInteractionDelay();
            int applied = 0;
            pending.Schedule(4, 0, 3, 10, 0.6, () => true, () => applied++);
            pending.Schedule(4, 0, 3, 10.4, 0.6, () => true, () => applied++);
            pending.Tick(10.59);
            Assert.AreEqual(0, applied);
            pending.Tick(10.6);
            pending.Tick(12);
            Assert.AreEqual(1, applied);
        }

        [Test]
        public void DelayedRequest_LeavingRangeCancelsEvenIfBackBeforeDeadline()
        {
            var pending = new CannonInteractionDelay();
            bool valid = true;
            int applied = 0;
            pending.Schedule(4, -1, 3, 10, 0.6, () => valid, () => applied++);
            valid = false; pending.Tick(10.1);
            valid = true; pending.Tick(10.7);
            Assert.AreEqual(0, applied);
            Assert.AreEqual(0, pending.Count);
        }

        [Test]
        public void ChangedDeviceOrReset_InvalidatesOldCompletion()
        {
            var pending = new CannonInteractionDelay();
            int applied = 0;
            pending.Schedule(4, 0, 3, 10, 0.6, () => true, () => applied += 1);
            pending.Schedule(4, -1, 3, 10.1, 0.6, () => true, () => applied += 10);
            pending.Tick(10.6);
            Assert.AreEqual(0, applied);
            pending.Tick(10.8);
            Assert.AreEqual(10, applied);
            pending.Schedule(4, 0, 4, 11, 0.6, () => true, () => applied++);
            pending.Clear(); pending.Tick(12);
            Assert.AreEqual(10, applied);
        }

        [Test]
        public void CompetingActors_OnlyFirstValidCompletionWins()
        {
            var pending = new CannonInteractionDelay();
            bool free = true;
            int applied = 0;
            foreach (ulong actor in new ulong[] { 1, 2 })
                pending.Schedule(actor, 0, 3, 10, 0.6, () => free, () => { free = false; applied++; });
            pending.Tick(11);
            Assert.AreEqual(1, applied);
            Assert.AreEqual(0, pending.Count);
        }
    }
}
