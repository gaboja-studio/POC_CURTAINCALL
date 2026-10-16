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
                Near(grip.position, fixedGrip);
                Near(leftAnchor.position, fixedLeft); Near(rightAnchor.position, fixedRight);
                Near(pole.HangingPosition(angle), feet);
                Near(pole.ReleaseVelocity(angle, 120), release);
                AssertCable(leftCable, leftAnchor);
                AssertCable(rightCable, rightAnchor);
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
