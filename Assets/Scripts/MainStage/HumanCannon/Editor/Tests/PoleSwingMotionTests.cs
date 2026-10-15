using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class PoleSwingMotionTests
    {
        [Test]
        public void Offset_PositiveAndNegativeAnglesFollowPlanarForward()
        {
            AssertVector(PoleSwingMotion.Offset(0, 2, Vector3.forward), Vector3.down * 2);
            AssertVector(PoleSwingMotion.Offset(90, 2, Vector3.forward), Vector3.forward * 2);
            AssertVector(PoleSwingMotion.Offset(-90, 2, Vector3.forward), Vector3.back * 2);
            var raised = PoleSwingMotion.Offset(45, 2, Vector3.forward);
            Assert.That(raised.magnitude, Is.EqualTo(2).Within(0.0001));
            Assert.That(raised.y, Is.GreaterThan(-2));
        }

        [Test]
        public void Velocity_IsTangentInMetersPerSecond()
        {
            AssertVector(PoleSwingMotion.Velocity(0, 90, 2, Vector3.forward), Vector3.forward * Mathf.PI);
            AssertVector(PoleSwingMotion.Velocity(90, 90, 2, Vector3.forward), Vector3.up * Mathf.PI);
            Vector3 offset = PoleSwingMotion.Offset(35, 3, Vector3.forward);
            Vector3 velocity = PoleSwingMotion.Velocity(35, 60, 3, Vector3.forward);
            Assert.That(Vector3.Dot(offset, velocity), Is.EqualTo(0).Within(0.0001));
            AssertVector(PoleSwingMotion.Velocity(35, -60, 3, Vector3.forward), -velocity);
        }

        [Test]
        public void RotatedAndPitchedForward_RotatesOnlyTheHorizontalPlane()
        {
            var turn = Quaternion.Euler(0, 90, 0);
            Vector3 pitched = new Vector3(5, 12, 0);
            AssertVector(PoleSwingMotion.Offset(40, 3, pitched),
                turn * PoleSwingMotion.Offset(40, 3, Vector3.forward));
            AssertVector(PoleSwingMotion.Velocity(40, 60, 3, pitched),
                turn * PoleSwingMotion.Velocity(40, 60, 3, Vector3.forward));
        }

        [Test]
        public void Input_AppliesOppositeForcesAndClampsToOne()
        {
            float positiveAngle = 0, positiveSpeed = 0, negativeAngle = 0, negativeSpeed = 0;
            float clampedAngle = 0, clampedSpeed = 0;
            PoleSwingMotion.Step(ref positiveAngle, ref positiveSpeed, 1, 0.1f, 0, 3, 60, 80, 100, 0);
            PoleSwingMotion.Step(ref negativeAngle, ref negativeSpeed, -1, 0.1f, 0, 3, 60, 80, 100, 0);
            PoleSwingMotion.Step(ref clampedAngle, ref clampedSpeed, 8, 0.1f, 0, 3, 60, 80, 100, 0);
            Assert.That(positiveAngle, Is.GreaterThan(0));
            Assert.That(positiveSpeed, Is.GreaterThan(0));
            Assert.That(negativeAngle, Is.EqualTo(-positiveAngle).Within(0.0001));
            Assert.That(negativeSpeed, Is.EqualTo(-positiveSpeed).Within(0.0001));
            Assert.That(clampedAngle, Is.EqualTo(positiveAngle));
            Assert.That(clampedSpeed, Is.EqualTo(positiveSpeed));
        }

        [Test]
        public void NoInput_KeepsMomentumAndGravityRestoresDisplacement()
        {
            float angle = 40, speed = 0;
            PoleSwingMotion.Step(ref angle, ref speed, 0, 0.1f, 9.81f, 3, 60, 80, 100, 0);
            Assert.That(angle, Is.LessThan(40));
            Assert.That(speed, Is.LessThan(0));
            angle = 0;
            speed = 30;
            PoleSwingMotion.Step(ref angle, ref speed, 0, 0.1f, 9.81f, 3, 60, 80, 100, 0);
            Assert.That(angle, Is.GreaterThan(0));
            Assert.That(speed, Is.GreaterThan(0));
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void Limits_StopOutwardVelocityAndAllowReturn(float direction)
        {
            float angle = 0, speed = 0;
            for (int i = 0; i < 20; i++)
            {
                PoleSwingMotion.Step(ref angle, ref speed, direction, 0.1f, 0, 3, 100, 10, 15, 0);
                Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(10));
                Assert.That(Mathf.Abs(speed), Is.LessThanOrEqualTo(15));
            }
            Assert.That(angle, Is.EqualTo(direction * 10));
            Assert.That(speed, Is.Zero);
            PoleSwingMotion.Step(ref angle, ref speed, -direction, 0.1f, 0, 3, 100, 10, 15, 0);
            Assert.That(Mathf.Abs(angle), Is.LessThan(10));
            Assert.That(speed * direction, Is.LessThan(0));
        }

        [Test]
        public void LargeDeltaTime_IsBoundedAndFinite()
        {
            float angle = 30, speed = 20, boundedAngle = angle, boundedSpeed = speed;
            PoleSwingMotion.Step(ref angle, ref speed, 1, float.MaxValue, 9.81f, 3, 60, 80, 100, 0.1f);
            PoleSwingMotion.Step(ref boundedAngle, ref boundedSpeed, 1, 0.25f, 9.81f, 3, 60, 80, 100, 0.1f);
            Assert.That(angle, Is.EqualTo(boundedAngle));
            Assert.That(speed, Is.EqualTo(boundedSpeed));
            Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(80));
            Assert.That(Mathf.Abs(speed), Is.LessThanOrEqualTo(100));
            PoleSwingMotion.Step(ref angle, ref speed, 1, 0.25f, float.MaxValue, float.Epsilon,
                float.MaxValue, 80, 100, float.MaxValue);
            Assert.IsFalse(float.IsNaN(angle) || float.IsInfinity(angle));
            Assert.IsFalse(float.IsNaN(speed) || float.IsInfinity(speed));
        }

        [Test]
        public void ZeroDeltaAndInvalidParameters_DoNotMoveValidState()
        {
            float angle = 20, speed = 10;
            PoleSwingMotion.Step(ref angle, ref speed, 1, 0, 9.81f, 3, 60, 80, 100, 0);
            PoleSwingMotion.Step(ref angle, ref speed, float.NaN, 0.1f, 9.81f, 3, 60, 80, 100, 0);
            PoleSwingMotion.Step(ref angle, ref speed, 1, float.PositiveInfinity, 9.81f, 3, 60, 80, 100, 0);
            PoleSwingMotion.Step(ref angle, ref speed, 1, 0.1f, 9.81f, 0, 60, 80, 100, 0);
            Assert.That(angle, Is.EqualTo(20));
            Assert.That(speed, Is.EqualTo(10));
            angle = float.NaN;
            speed = float.PositiveInfinity;
            PoleSwingMotion.Step(ref angle, ref speed, 0, 0, 9.81f, 3, 60, 80, 100, 0);
            Assert.That(angle, Is.Zero);
            Assert.That(speed, Is.Zero);
        }

        [Test]
        public void InvalidGeometry_ReturnsFiniteZero()
        {
            AssertVector(PoleSwingMotion.Offset(float.NaN, 3, Vector3.forward), Vector3.zero);
            AssertVector(PoleSwingMotion.Offset(30, -1, Vector3.forward), Vector3.zero);
            AssertVector(PoleSwingMotion.Offset(30, 3, Vector3.up), Vector3.zero);
            AssertVector(PoleSwingMotion.Velocity(30, float.PositiveInfinity, 3, Vector3.forward), Vector3.zero);
            AssertVector(PoleSwingMotion.Velocity(30, 60, 3, new Vector3(float.NaN, 0, 1)), Vector3.zero);
            AssertVector(PoleSwingMotion.Velocity(0, float.MaxValue, float.MaxValue, Vector3.forward), Vector3.zero);
        }

        static void AssertVector(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
