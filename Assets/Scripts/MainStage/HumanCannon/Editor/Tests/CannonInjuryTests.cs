using System.Reflection;
using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonInjuryTests
    {
        [TestCase(0, 1f, 1f)]
        [TestCase(1, 0.8f, 0.8f)]
        [TestCase(2, 0.8f, 0.8f)]
        public void Legs_KeepAtLeastEightyPercentAfterCommonCrawl(int legs, float move, float air)
        {
            var settings = new HumanCannonInjurySettings();
            float crawl = legs >= 2 ? 0.5f : 1f;
            Assert.That(settings.MovePolicyMultiplier(legs, 0.5f) * crawl, Is.EqualTo(move).Within(0.001f));
            Assert.AreEqual(air, settings.AirMultiplier(legs));
            Assert.AreEqual(0f, settings.InteractionSeconds(false));
            Assert.AreEqual(0.6f, settings.InteractionSeconds(true));
        }

        [Test]
        public void ChangedCommonCrawl_KeepsFloorWithoutMakingTwoLegLossFasterThanOneLeg()
        {
            var settings = new HumanCannonInjurySettings();
            foreach (float crawl in new[] { 0.1f, 0.5f, 0.9f, 1f })
                Assert.That(settings.MovePolicyMultiplier(2, crawl) * crawl, Is.EqualTo(0.8f).Within(0.00001f));
        }

        [Test]
        public void OldSerializedLowMultipliers_AlsoUseTheEightyPercentFloor()
        {
            var settings = new HumanCannonInjurySettings();
            Set(settings, "oneLegMove", 0.4f);
            Set(settings, "oneLegAir", 0.7f);
            Set(settings, "twoLegAir", 0.5f);
            foreach (int legs in new[] { 1, 2 })
            {
                float common = legs == 2 ? 0.5f : 1f;
                Assert.That(settings.MovePolicyMultiplier(legs, 0.5f) * common, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(settings.AirMultiplier(legs), Is.EqualTo(0.8f));
            }
        }

        [Test]
        public void ExistingFasterSettings_ArePreservedAndCommonZeroDoesNotDivide()
        {
            var settings = new HumanCannonInjurySettings();
            Set(settings, "oneLegMove", 0.9f);
            Set(settings, "oneLegAir", 0.9f);
            Set(settings, "twoLegAir", 0.85f);
            Assert.That(settings.MovePolicyMultiplier(1, 0.95f), Is.EqualTo(0.9f));
            Assert.That(settings.MovePolicyMultiplier(2, 0.85f) * 0.85f, Is.EqualTo(0.85f).Within(0.00001f));
            Assert.That(settings.MovePolicyMultiplier(2, 0.95f) * 0.95f, Is.EqualTo(0.9f).Within(0.00001f));
            Assert.That(settings.AirMultiplier(1), Is.EqualTo(0.9f));
            Assert.That(settings.AirMultiplier(2), Is.EqualTo(0.85f));
            Assert.That(settings.MovePolicyMultiplier(2, 0f), Is.EqualTo(1f));
        }

        static void Set(HumanCannonInjurySettings settings, string field, float value) =>
            typeof(HumanCannonInjurySettings).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, value);

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
