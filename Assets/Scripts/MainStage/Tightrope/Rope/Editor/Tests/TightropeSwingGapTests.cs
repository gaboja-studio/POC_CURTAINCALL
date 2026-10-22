using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeSwingGapTests
    {
        [Test]
        public void GapOnlyAppliesToCoursesLongEnoughForBothApproaches()
        {
            Assert.IsTrue(SwingPoleMath.HasGap(330f, 128f, 8f));
            Assert.IsFalse(SwingPoleMath.HasGap(100f, 128f, 8f));
            Assert.IsFalse(SwingPoleMath.HasGap(330f, float.NaN, 8f));
            Assert.IsTrue(SwingPoleMath.Contains(128f, 128f, 8f));
            Assert.IsFalse(SwingPoleMath.Contains(136f, 128f, 8f));
        }

        [TestCase(30)]
        [TestCase(120)]
        public void PoleLaunchSetsContinuousParabolaInsteadOfStoppingAtRangeBoundary(int fps)
        {
            Vector3 origin = new(10f, 3f, 99f), position = origin;
            const float limit = 5.56f, gravity = 20f, upwardSpeed = 6f;
            float time = (upwardSpeed + Mathf.Sqrt(upwardSpeed * upwardSpeed + 2f * gravity * origin.y)) / gravity;
            float speedLimit = SwingPoleMath.FlightSpeedLimit(origin.y, upwardSpeed, gravity, limit);
            Vector3 velocity = SwingPoleMath.LimitFlightVelocity(new Vector3(0f, upwardSpeed, 500f), speedLimit);
            Assert.That(velocity.y, Is.EqualTo(upwardSpeed));
            for (float elapsed = 0f; elapsed < time;)
            {
                float dt = Mathf.Min(1f / fps, time - elapsed);
                Vector3 correction = SwingPoleMath.LimitFlightVelocity(velocity + Vector3.forward * 3f, speedLimit) - velocity;
                Vector3 step = BallisticMotion.Step(ref velocity, correction, gravity, dt);
                Assert.That(step.z / dt, Is.EqualTo(speedLimit).Within(.001f), "거리 끝에서도 수평 속도가 갑자기 사라지지 않는다.");
                position += step;
                elapsed += dt;
                Assert.That(position.y, Is.EqualTo(origin.y + upwardSpeed * elapsed - .5f * gravity * elapsed * elapsed).Within(.001f));
                Assert.That(Vector3.ProjectOnPlane(position - origin, Vector3.up).magnitude, Is.LessThanOrEqualTo(limit + .001f));
            }
            Assert.That(position.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(Vector3.ProjectOnPlane(position - origin, Vector3.up).magnitude, Is.EqualTo(limit).Within(.001f));
            position += BallisticMotion.Step(ref velocity, Vector3.zero, gravity, .1f);
            Assert.That(position.z - origin.z, Is.GreaterThan(limit), "놓친 뒤에는 경계에 멈추지 않고 탄도를 따라 계속 낙하한다.");
        }

        [Test]
        public void PoleLaunchPreservesWeakMomentumAndBackwardDirection()
        {
            float speedLimit = SwingPoleMath.FlightSpeedLimit(3f, 6f, 20f, 5.56f);
            Assert.That(SwingPoleMath.LimitFlightVelocity(new Vector3(0f, 6f, 1f), speedLimit), Is.EqualTo(new Vector3(0f, 6f, 1f)));
            Vector3 backward = SwingPoleMath.LimitFlightVelocity(new Vector3(0f, 6f, -500f), speedLimit);
            Assert.That(backward.z, Is.EqualTo(-speedLimit).Within(.001f));
            Assert.That(backward.y, Is.EqualTo(6f));
        }

        [TestCase(2.5f, 3.1f)]
        [TestCase(1.05f, 3.1f)]
        [TestCase(3.1f, 1.1f)]
        [TestCase(2.5f, 1.65f)]
        public void PoleArcReachesNextGripHeightWithABoundedApex(float startHeight, float endHeight)
        {
            Vector3 origin = Vector3.up * startHeight, target = new(0f, endHeight, 5.56f);
            Vector3 velocity = SwingPoleMath.PoleFlightVelocity(origin, target, new Vector3(0f, 40f, 500f),
                Vector3.forward, 9.81f, 1.5f, out float limit);
            float time = 5.56f / limit;
            Vector3 end = origin + velocity * time + Vector3.down * (.5f * 9.81f * time * time);
            Assert.That(Vector3.Distance(end, target), Is.LessThan(.001f), "최대 비거리 끝은 실제 다음 봉 높이다.");
            float apex = startHeight + velocity.y * velocity.y / (2f * 9.81f);
            Assert.That(apex, Is.LessThanOrEqualTo(Mathf.Max(startHeight, endHeight) + 1.501f));
            Assert.That(velocity.y - 9.81f * time, Is.LessThan(0f));
            Assert.IsFalse(SwingPoleMath.InPoleReach(target + Vector3.up * 3f, target, 1f));
        }

        [Test]
        public void PoleArcDoesNotTurnWeakOrBackwardMomentumIntoAForwardLaunch()
        {
            Vector3 target = new(0f, 3.1f, 5.56f);
            foreach (float speed in new[] { 0f, 1f, -1f })
            {
                Vector3 velocity = SwingPoleMath.PoleFlightVelocity(Vector3.up * 2.5f, target, new Vector3(0f, 6f, speed),
                    Vector3.forward, 9.81f, 1.5f, out _);
                Assert.That(velocity.z, Is.EqualTo(speed));
            }
            Assert.IsTrue(SwingPoleMath.InPoleReach(target + Vector3.up * .3f, target, 1f));
            Assert.IsFalse(SwingPoleMath.InPoleReach(target + Vector3.forward * 2.6f, target, 1f));
            Assert.IsFalse(SwingPoleMath.InPoleReach(target + new Vector3(2f, 2f, 0f), target, 1f));
            Assert.IsTrue(SwingPoleMath.InPoleReach(target + Vector3.up * 2.5f, target, 1f));
        }

        [TestCase(.49f, false)]
        [TestCase(.5f, true)]
        [TestCase(.75f, true)]
        [TestCase(1f, true)]
        [TestCase(1.01f, false)]
        public void PoleCatchWindowCoversOnlyTheLastHalfOfForwardFlight(float fraction, bool expected)
        {
            Vector3 origin = new(10f, 3f, 99f);
            Assert.That(SwingPoleMath.InCatchWindow(origin, origin + Vector3.right * (6f * fraction) + Vector3.up * 4f,
                Vector3.right, 6f, 1.1f), Is.EqualTo(expected));
            Assert.IsFalse(SwingPoleMath.InCatchWindow(origin, origin + Vector3.right * 4f + Vector3.forward * 1.2f,
                Vector3.right, 6f, 1.1f), "다른 통로에서 다음 봉을 원격으로 잡지 않는다.");
            Assert.IsFalse(SwingPoleMath.InCatchWindow(origin, origin - Vector3.right * 4f, Vector3.right, 6f, 1.1f));
            Assert.IsFalse(SwingPoleMath.InCatchWindow(origin, origin, Vector3.right, 0f, 1.1f));
        }

        [Test]
        public void ClearingFireDoesNotRestoreTheStructuralGapAndHostShapeKeepsIt()
        {
            string before = GameSettings.Tightrope.Course.ToJson();
            var go = new GameObject("Swing gap query test");
            try
            {
                JsonUtility.FromJsonOverwrite("{\"finishDistance\":330,\"laneCount\":6,\"swingGapEnabled\":true,\"relayGapEnabled\":false,\"trapezeEnabled\":false,\"swingGapLanes\":[2,5],\"swingGapStart\":128,\"swingGapLength\":8}", GameSettings.Tightrope.Course);
                var course = go.AddComponent<TightropeCourse>();
                Assert.IsTrue(course.HasSwingGap);
                Assert.IsFalse(course.IsLaneUsable(1, 132f));
                Assert.IsFalse(course.IsLaneUsable(4, 132f));
                Assert.IsTrue(course.IsLaneUsable(0, 132f), "봉을 배치하지 않은 줄은 끊어지지 않는다.");
                Assert.IsTrue(course.IsLaneUsable(1, 136f));
                course.BlockSegment(1, 10f, 20f);
                course.ClearBlockedSegments();
                Assert.That(course.GetUsableSpans(1), Is.EqualTo(new[] { new Vector2(0f, 128f), new Vector2(136f, 330f) }));
                var shared = TightropeSettings.CourseSettings.FromJson(course.ExportShape());
                Assert.IsTrue(shared.SwingGapEnabled); Assert.That(shared.SwingGapStart, Is.EqualTo(128f));
                Assert.That(shared.SwingGapLength, Is.EqualTo(8f));
                Assert.That(shared.SwingGapLanes, Is.EqualTo(new[] { 2, 5 }));
            }
            finally { Object.DestroyImmediate(go); JsonUtility.FromJsonOverwrite(before, GameSettings.Tightrope.Course); }
        }

        [TestCase(30)]
        [TestCase(120)]
        public void SharedCannonBallisticStepKeepsGravityAndFrameOnlyAirCorrection(int fps)
        {
            Vector3 velocity = new(0f, 6f, 4f), position = Vector3.zero;
            float gravity = GameSettings.Base.Character.Gravity;
            for (int i = 0; i < fps; i++) position += BallisticMotion.Step(ref velocity, Vector3.right * 3f, gravity, 1f / fps);
            Assert.That(position.x, Is.EqualTo(3f).Within(.001f));
            Assert.That(position.y, Is.EqualTo(6f - .5f * gravity).Within(.001f));
            Assert.That(position.z, Is.EqualTo(4f).Within(.001f));
            Assert.That(velocity.x, Is.Zero, "공중 보정을 발사 관성에 누적하지 않는다.");
            Assert.That(GameSettings.Tightrope.RopeMovement.JumpGravityMultiplier, Is.EqualTo(1f));
            Assert.That(GameSettings.Tightrope.RopeMovement.JumpHeight, Is.EqualTo(GameSettings.Base.Character.JumpHeight * 1.25f));
        }
    }
}
