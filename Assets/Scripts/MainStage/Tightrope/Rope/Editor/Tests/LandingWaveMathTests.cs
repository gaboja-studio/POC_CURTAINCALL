using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class LandingWaveMathTests
    {
        [Test]
        public void NearbyShockFallsOffInBothDirectionsAndNeverExceedsTheCap()
        {
            Assert.That(LandingWaveMath.Strength(0f, 6f, 6f), Is.EqualTo(6f));
            Assert.That(LandingWaveMath.Strength(-3f, 6f, 6f), Is.EqualTo(3f));
            Assert.That(LandingWaveMath.Strength(3f, 6f, 6f), Is.EqualTo(3f));
            Assert.That(LandingWaveMath.Strength(6f, 6f, 6f), Is.Zero);
            Assert.That(LandingWaveMath.Strength(0f, 6f, 1000f), Is.EqualTo(15f));
            Assert.That(LandingWaveMath.Strength(float.NaN, 6f, 6f), Is.Zero);
            Assert.That(LandingWaveMath.Strength(1f, 0f, 6f), Is.Zero);
        }

        [Test]
        public void ADisappearedRopeSectionStopsPropagation()
        {
            var spans = new[] { new Vector2(0f, 5f), new Vector2(7f, 12f) };
            Assert.IsTrue(LandingWaveMath.SharesSupport(spans, 1f, 4f));
            Assert.IsFalse(LandingWaveMath.SharesSupport(spans, 4f, 8f));
            Assert.IsFalse(LandingWaveMath.SharesSupport(spans, 4f, 5f));
        }
    }
}
