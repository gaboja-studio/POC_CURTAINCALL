using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class PerformanceFeedbackNumericTests
    {
        [Test]
        public void CombinesOnlyWithinOriginalWindowAndRespectsActualZero()
        {
            var state = new NumericReactionState();
            state.Add(12, 1, .2f, .85f);
            state.Add(3, 1.1f, .2f, .85f); Assert.AreEqual(15, state.Value);
            state.Add(0, 1.15f, .2f, .85f); Assert.AreEqual(15, state.Value);
            state.Add(2, 1.3f, .2f, .85f); Assert.AreEqual(2, state.Value);
            Assert.IsFalse(state.Visible(2.16f));
        }
        [Test]
        public void OppositeLanesRemainIndependentAndClearOnContextReset()
        {
            var plus = new NumericReactionState(); var minus = new NumericReactionState();
            plus.Add(12, 0, .2f, .85f); minus.Add(-5, .1f, .2f, .85f);
            Assert.AreEqual(12, plus.Value); Assert.AreEqual(-5, minus.Value);
            Assert.IsTrue(plus.Visible(.5f)); Assert.IsTrue(minus.Visible(.5f));
            plus.Clear(); minus.Clear(); Assert.IsFalse(plus.Visible(.5f)); Assert.IsFalse(minus.Visible(.5f));
        }
    }
}
