using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws.EditorTools.Tests
{
    public sealed class CooperativeGateGeometryTests
    {
        [TestCase(0f)] [TestCase(.5f)] [TestCase(1.2f)]
        public void EnlargedOriginalSawBlocksSoloJumpOnItsLane(float footHeight)
        {
            var body = new BodyCapsule(new Vector3(0f, footHeight, 0f), 1.5f, .3f);
            Assert.IsTrue(CooperativeGateGeometry.TryGetContact(body, Vector3.zero, Vector3.right,
                2.1f, .2f, out _));
        }

        [Test]
        public void EnlargedOriginalSawLeavesAdjacentLaneAndEmptyCornersClear()
        {
            Assert.IsFalse(CooperativeGateGeometry.TryGetContact(new BodyCapsule(new Vector3(3f, 0f, 0f), 1.5f, .3f),
                Vector3.zero, Vector3.right, 2.1f, .2f, out _), "옆줄에 보이지 않는 벽을 남기지 않는다.");
            Assert.IsFalse(CooperativeGateGeometry.TryGetContact(new BodyCapsule(new Vector3(0f, 1.9f, .95f), 1.5f, .1f),
                Vector3.zero, Vector3.right, 2.1f, .2f, out _), "원판 바깥 사각 모서리는 접촉하지 않는다.");
            Assert.IsFalse(CooperativeGateGeometry.TryGetContact(new BodyCapsule(new Vector3(0f, 2.11f, 0f), 1.5f, .3f),
                Vector3.zero, Vector3.right, 2.1f, .2f, out _));
        }

        [Test]
        public void EnlargedOriginalSawUsesCourseRightAxisAndLiftsAboveStandingPlayer()
        {
            var body = new BodyCapsule(new Vector3(.8f, 0f, 0f), 1.5f, .3f);
            Assert.IsFalse(CooperativeGateGeometry.TryGetContact(body, Vector3.zero, Vector3.right, 2.1f, .2f, out _));
            Assert.IsTrue(CooperativeGateGeometry.TryGetContact(body, Vector3.zero, Vector3.forward, 2.1f, .2f, out _));
            Assert.IsFalse(CooperativeGateGeometry.TryGetContact(new BodyCapsule(Vector3.zero, 1.5f, .3f),
                Vector3.up * 2.5f, Vector3.right, 2.1f, .2f, out _));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void CooperativeGateCanBeClearedByActualTwoHighForwardJumpEnvelope(int framesPerSecond)
        {
            var settings = new SawSettings { cooperativeGatePlateOffset = 2.2f };
            float gravity = 9.81f * 1.35f, launchGap = 2.2f;
            Assert.IsTrue(CooperativeGateGeometry.TryCalculateLaunch(1.5f, 1.2f, gravity, launchGap, .3f,
                settings, out float height, out float speed));
            float y = 1.5f, along = -launchGap, up = Mathf.Sqrt(2f * gravity * height), dt = 1f / framesPerSecond;
            bool crossed = false;
            for (int frame = 0; frame < framesPerSecond * 2 && y > 0f; frame++)
            {
                up -= gravity * dt;
                y += up * dt;
                along += speed * dt;
                Assert.IsFalse(CooperativeGateGeometry.TryGetContact(new BodyCapsule(new Vector3(0f, y, along), 1.5f, .3f),
                    Vector3.zero, Vector3.right, 2.1f, .2f, out _));
                crossed |= along > 1.4f;
            }
            Assert.IsTrue(crossed);
            Assert.That(along, Is.InRange(1.4f, 3f), "뒤 발판 길이1.6m 안에 착지한다.");
        }

        [Test]
        public void CooperativeGateIsOptInAndStopsInReachOfFinalPlate()
        {
            var settings = new SawSettings();
            Assert.IsFalse(settings.cooperativeGates);
            Assert.That(settings.cooperativeGateDistances, Is.EqualTo(new[] { 170f, 320f }));
            Assert.That(settings.cooperativeGateApproachDistance / settings.cooperativeGateApproachSpeed, Is.EqualTo(4f));
            Assert.That(settings.cooperativeGateDistances[1] + settings.cooperativeGatePlateOffset, Is.LessThan(330f));
        }

        [TestCase(.55f)] [TestCase(.85f)] [TestCase(1.5f)]
        public void CooperativeGateInjuredStackLaunchClearsAtBothEdgesOfMarkedStrip(float footHeight)
        {
            var settings = new SawSettings { cooperativeGatePlateOffset = 2.2f };
            float gravity = 9.81f * 1.35f;
            float launchGap = 2.2f;
            foreach (float shift in new[] { -.35f, 0f, .35f })
            {
                float gap = launchGap + shift;
                Assert.IsTrue(CooperativeGateGeometry.TryCalculateLaunch(footHeight, .42f, gravity, gap, .3f,
                    settings, out float height, out float speed));
                Assert.That(height, Is.LessThan(3f), "확대된 원판을 넘는 데 필요한 수준으로만 높이를 보정한다.");
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    float y = footHeight, z = -gap, up = Mathf.Sqrt(2f * gravity * height), dt = 1f / fps;
                    for (int frame = 0; frame < fps * 3 && y > 0f; frame++)
                    {
                        up -= gravity * dt;
                        y += up * dt;
                        z += speed * dt;
                        Assert.IsFalse(CooperativeGateGeometry.TryGetContact(new BodyCapsule(new Vector3(0f, y, z), 1.5f, .3f),
                            Vector3.zero, Vector3.right, 2.1f, .2f, out _), $"height={footHeight}, shift={shift}, fps={fps}");
                    }
                    Assert.That(z, Is.InRange(1.4f, 3f));
                }
            }
        }
    }
}
