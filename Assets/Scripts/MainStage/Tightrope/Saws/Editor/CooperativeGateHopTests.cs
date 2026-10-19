using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws.Tests
{
    public sealed class CooperativeGateHopTests
    {
        [Test]
        public void HopClearsWaitingPlayersThenKeepsMovingAlongTheRope()
        {
            Assert.That(CooperativeGateHop.Sample(0f, 1.2f, 6f, 3f, 3f), Is.EqualTo(Vector2.zero));
            var overhead = CooperativeGateHop.Sample(.6f, 1.2f, 6f, 3f, 3f);
            Assert.That(overhead.x, Is.InRange(-3f, -2f));
            Assert.That(overhead.y, Is.GreaterThanOrEqualTo(3f));
            Assert.That(CooperativeGateHop.Sample(1.2f, 1.2f, 6f, 3f, 3f), Is.EqualTo(new Vector2(-6f, 0f)));
            Assert.That(CooperativeGateHop.Sample(2.2f, 1.2f, 6f, 3f, 3f).x, Is.EqualTo(-9f).Within(.001f));
        }

        [Test]
        public void DefaultHopClearsAllThreeRemainingStackLevelsAtTheLaunchMarker()
        {
            var settings = new SawSettings();
            for (int level = 0; level < 3; level++)
            {
                var body = new BodyCapsule(new Vector3(0f, level * 1.5f, -2.2f), 1.5f, .3f);
                for (int sample = 0; sample <= 120; sample++)
                {
                    var motion = CooperativeGateHop.Sample(settings.cooperativeGateHopDuration * sample / 120f,
                        settings.cooperativeGateHopDuration, settings.cooperativeGateHopDistance,
                        settings.cooperativeGateHopHeight, settings.cooperativeGateApproachSpeed);
                    Assert.IsFalse(CooperativeGateGeometry.TryGetContact(body, new Vector3(0f, motion.y, motion.x),
                        Vector3.right, settings.cooperativeGateHeight, settings.cooperativeGateThickness, out _),
                        $"남은 목마 {level + 1}층과 비행 중 {sample}/120에서 접촉하지 않는다.");
                }
            }
        }

        [Test]
        public void PressingAnAlreadyRaisedFinalGateDoesNotSnapItDown()
        {
            var start = CooperativeGateHop.Sample(0f, 1.2f, 6f, 3f, 3f, 2.5f);
            Assert.That(start.y, Is.EqualTo(2.5f));
            Assert.That(CooperativeGateHop.Sample(.1f, 1.2f, 6f, 3f, 3f, 2.5f).y, Is.GreaterThan(2.5f));
            Assert.That(CooperativeGateHop.Sample(float.NaN, 1f, 6f, 3f, 3f), Is.EqualTo(Vector2.zero));
        }
    }
}
