using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class PoleSwingVisualTests
    {
        GameObject root;
        HumanCannonPole pole;
        Transform grip, bar, leftCable, rightCable, leftAnchor, rightAnchor;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PoleSwingVisualTest");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(new Vector3(3, 2, 5), Quaternion.Euler(0, 90, 0));
            pole = root.AddComponent<HumanCannonPole>();
            grip = Child("Grip", Vector3.zero);
            bar = Child("VisualBar", Vector3.zero);
            leftAnchor = Child("LeftAnchor", new Vector3(-2.2f, 8, 0));
            rightAnchor = Child("RightAnchor", new Vector3(2.2f, 8, 0));
            leftCable = Child("LeftCable", new Vector3(-2.2f, 4, 0));
            rightCable = Child("RightCable", new Vector3(2.2f, 4, 0));
            leftCable.localScale = rightCable.localScale = new Vector3(0.1f, 8, 0.1f);
            Set("grip", grip); Set("visualBar", bar);
            Set("leftCable", leftCable); Set("rightCable", rightCable);
            Set("leftAnchor", leftAnchor); Set("rightAnchor", rightAnchor);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void ForwardAndBackwardSwing_MovesBarWithPlayerWhileGripAndCeilingStayFixed()
        {
            Vector3 fixedGrip = grip.position, fixedLeft = leftAnchor.position, fixedRight = rightAnchor.position;
            foreach (float angle in new[] { -55f, 55f })
            {
                Vector3 feet = pole.HangingPosition(angle);
                Vector3 release = pole.ReleaseVelocity(angle, 120);
                pole.PresentSwing(angle);
                Near(bar.position, feet + Vector3.up * 1.5f);
                Vector3 pivot = (fixedLeft + fixedRight) * 0.5f;
                Near(bar.position, pivot + PoleSwingMotion.Offset(angle, 8, pole.Forward));
                Assert.That(Vector3.Distance(pivot, bar.position), Is.EqualTo(8).Within(0.001f));
                Assert.That(Mathf.Abs(Vector3.Dot(bar.position - fixedGrip, pole.Forward)), Is.GreaterThan(6f),
                    "몸만 1.5m 흔드는 대신 봉도 8m 줄 반경으로 크게 이동한다.");
                Near(grip.position, fixedGrip);
                Near(leftAnchor.position, fixedLeft); Near(rightAnchor.position, fixedRight);
                Near(pole.HangingPosition(angle), feet);
                Near(pole.ReleaseVelocity(angle, 120), release);
                AssertCable(leftCable, leftAnchor);
                AssertCable(rightCable, rightAnchor);
                Assert.That(leftCable.localScale.y, Is.EqualTo(8).Within(0.001f));
                Assert.That(rightCable.localScale.y, Is.EqualTo(8).Within(0.001f));
            }
        }

        [Test]
        public void RepeatedPresentation_DoesNotFeedBackIntoPhysics_AndZeroReturnsBarAndCables()
        {
            Vector3 expected = pole.HangingPosition(70) + Vector3.up * 1.5f;
            for (int frame = 0; frame < 100; frame++) pole.PresentSwing(70);
            Near(bar.position, expected);
            pole.PresentSwing(0);
            Near(bar.position, grip.position);
            AssertCable(leftCable, leftAnchor); AssertCable(rightCable, rightAnchor);
            Assert.That(leftCable.localScale.y, Is.EqualTo(8).Within(0.001f));
            Assert.That(rightCable.localScale.y, Is.EqualTo(8).Within(0.001f));
            Assert.That(leftCable.localScale.x, Is.EqualTo(0.1f));
            Assert.That(rightCable.localScale.z, Is.EqualTo(0.1f));
        }

        [TestCase(-35f, -70f)]
        [TestCase(35f, 70f)]
        public void ReleaseVelocity_IsDerivativeOfActualBodyArcPlusUpwardBoost(float angle, float speed)
        {
            const float deltaTime = 0.0001f;
            Vector3 finiteDifference = (pole.HangingPosition(angle + speed * deltaTime)
                - pole.HangingPosition(angle - speed * deltaTime)) / (2 * deltaTime);
            Vector3 tangent = pole.ReleaseVelocity(angle, speed) - Vector3.up * 6;
            Assert.That(Vector3.Distance(tangent, finiteDifference), Is.LessThan(0.02f));
            Vector3 pivot = (leftAnchor.position + rightAnchor.position) * 0.5f;
            Assert.That(Vector3.Dot(tangent, pole.BarPosition(angle) - pivot), Is.EqualTo(0).Within(0.001f));
            Assert.That(tangent.magnitude, Is.EqualTo(8 * Mathf.Abs(speed) * Mathf.Deg2Rad).Within(0.001f));
        }

        [Test]
        public void MissingCeilingAnchor_PreservesLegacyShortPendulum()
        {
            Set("rightAnchor", null);
            Assert.That(pole.SwingLength, Is.EqualTo(1.5f));
            Near(pole.HangingPosition(35), grip.position + PoleSwingMotion.Offset(35, 1.5f, pole.Forward));
            Near(pole.ReleaseVelocity(35, 70), PoleSwingMotion.Velocity(35, 70, 1.5f, pole.Forward) + Vector3.up * 6);
            pole.PresentSwing(35);
            Near(bar.position, pole.HangingPosition(35) + Vector3.up * 1.5f);
        }

        [Test]
        public void CeilingGeometry_ControlsGravityAndVelocityWithoutChangingGrip()
        {
            Assert.That(pole.SwingLength, Is.EqualTo(8).Within(0.001f));
            float expectedAngle = 30, expectedSpeed = 0;
            PoleSwingMotion.Step(ref expectedAngle, ref expectedSpeed, 0, 0.1f, 9.81f, 8, 300, 80, 180, 0.4f);
            object[] args = { 30f, 0f, 0f, 0.1f, 9.81f };
            typeof(HumanCannonPole).GetMethod("Step", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pole, args);
            Assert.That((float)args[0], Is.EqualTo(expectedAngle).Within(0.0001f));
            Assert.That((float)args[1], Is.EqualTo(expectedSpeed).Within(0.0001f));
            Near(pole.HangingPosition(0), grip.position - Vector3.up * 1.5f);
            Near(pole.ReleaseVelocity(0, 100), pole.Forward * (8 * 100 * Mathf.Deg2Rad) + Vector3.up * 6);
        }

        void AssertCable(Transform cable, Transform anchor)
        {
            Near(cable.TransformPoint(Vector3.up * 0.5f), anchor.position);
            Near(cable.TransformPoint(Vector3.down * 0.5f),
                bar.position + Vector3.ProjectOnPlane(anchor.position - grip.position, Vector3.up));
        }
        Transform Child(string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
            child.localPosition = position;
            return child;
        }
        void Set(string field, Transform value) => typeof(HumanCannonPole)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pole, value);
        static void Near(Vector3 actual, Vector3 expected) => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f));
    }
}
