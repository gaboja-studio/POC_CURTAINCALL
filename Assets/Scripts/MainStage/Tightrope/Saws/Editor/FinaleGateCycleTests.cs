using NUnit.Framework;

namespace CurtainCall.Tightrope.Saws.EditorTools.Tests
{
    public sealed class FinaleGateCycleTests
    {
        [Test]
        public void OnlyTheLastGateInTheFinalPartOfTheCourseCycles()
        {
            float[] gates = { 170f, 320f };
            Assert.IsFalse(FinaleGateCycle.Applies(0, gates, 330f, .9f));
            Assert.IsTrue(FinaleGateCycle.Applies(1, gates, 330f, .9f));
            Assert.IsFalse(FinaleGateCycle.Applies(0, new[] { 170f }, 330f, .9f));
            Assert.IsFalse(FinaleGateCycle.Applies(-1, gates, 330f, .9f));
        }

        [Test]
        public void CycleTelegraphsOpensClosesAndRepeatsWithoutDependingOnPlayerCount()
        {
            Assert.That(FinaleGateCycle.Sample(-2f, 4f, 4f, .3f).Lift, Is.Zero);
            Assert.That(FinaleGateCycle.Sample(3f, 4f, 4f, .3f).UntilOpen, Is.EqualTo(1.3f).Within(.001f));
            Assert.That(FinaleGateCycle.Sample(4.15f, 4f, 4f, .3f).Lift, Is.EqualTo(.5f).Within(.001f));
            var open = FinaleGateCycle.Sample(5f, 4f, 4f, .3f);
            Assert.That(open.Lift, Is.EqualTo(1f));
            Assert.That(open.OpenRemaining, Is.EqualTo(3.3f).Within(.001f));
            Assert.IsTrue(FinaleGateCycle.Sample(8.45f, 4f, 4f, .3f).Closing);
            Assert.That(FinaleGateCycle.Sample(8.7f, 4f, 4f, .3f).Lift, Is.Zero);
            Assert.That(FinaleGateCycle.Sample(float.NaN, 4f, 4f, .3f).Lift, Is.Zero);
        }
    }
}
