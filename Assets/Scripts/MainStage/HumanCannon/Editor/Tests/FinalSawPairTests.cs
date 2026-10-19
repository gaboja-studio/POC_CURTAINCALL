using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class FinalSawPairTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        HumanCannonFinalSaws saws;
        Transform first, second, firstWarning, secondWarning;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FinalSawPairTest");
            root.SetActive(false);
            saws = root.AddComponent<HumanCannonFinalSaws>();
            var start = Child("Start"); start.position = new Vector3(0, 12, 20);
            var end = Child("End"); end.position = new Vector3(0, 12, 4);
            first = Child("First"); second = Child("Second");
            firstWarning = Child("FirstWarning"); secondWarning = Child("SecondWarning");
            Set("verticalStart", start); Set("verticalEnd", end);
            Set("verticalView", first); Set("secondaryVerticalView", second);
            Set("warningView", firstWarning); Set("secondaryWarningView", secondWarning);
            Set("startedAt", 10d);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(7)]
        public void SharedPass_UsesTwoSeparatedLanesWithoutChangingTravel(int cycle)
        {
            double now = 10 + cycle * 6.5 + 3.5; // 예고 2.5초 뒤 통과 1초째.
            float[] lanes = { -6, -2, 2, 6 };
            Vector3 primary = saws.VerticalPosition(now);
            Vector3 secondary = saws.SecondaryVerticalPosition(now);
            Assert.That(saws.VerticalCount, Is.EqualTo(2));
            Assert.That(primary.x, Is.EqualTo(lanes[cycle % 4]));
            Assert.That(secondary.x, Is.EqualTo(lanes[(cycle + 2) % 4]));
            Assert.That(Vector3.Distance(primary, secondary), Is.EqualTo(8).Within(0.001f));
            Assert.That(primary.y, Is.EqualTo(12));
            Assert.That(primary.z, Is.EqualTo(16).Within(0.001f));
            Assert.That(secondary.y, Is.EqualTo(primary.y));
            Assert.That(secondary.z, Is.EqualTo(primary.z));
        }

        [TestCase(FinalSawPhase.Dormant, false, false)]
        [TestCase(FinalSawPhase.Warning, false, true)]
        [TestCase(FinalSawPhase.Active, true, false)]
        public void BothViews_UseTheSameWarningAndActiveVisibility(FinalSawPhase phase, bool visible, bool warning)
        {
            var present = typeof(HumanCannonFinalSaws).GetMethod("PresentVertical", Private);
            present.Invoke(saws, new object[] { first, firstWarning, Array.Empty<Renderer>(), Vector3.zero, phase, 45f, Color.yellow });
            present.Invoke(saws, new object[] { second, secondWarning, Array.Empty<Renderer>(), Vector3.right * 8, phase, 45f, Color.yellow });
            Assert.That(first.gameObject.activeSelf, Is.EqualTo(visible));
            Assert.That(second.gameObject.activeSelf, Is.EqualTo(visible));
            Assert.That(firstWarning.gameObject.activeSelf, Is.EqualTo(warning));
            Assert.That(secondWarning.gameObject.activeSelf, Is.EqualTo(warning));
            Assert.That(Vector3.Distance(first.position, second.position), Is.EqualTo(8));
        }

        Transform Child(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
            return child;
        }
        void Set(string name, object value) => typeof(HumanCannonFinalSaws).GetField(name, Private).SetValue(saws, value);

        [TestCase(0)]
        [TestCase(1)]
        public void LateralPasses_KeepHeightAndDepth_ReverseAtBothEdges_WithFlatContact(int cycle)
        {
            Set("lateralPasses", true); Set("horizontalHalfWidth", 24f);
            double start = 10 + cycle * 6.5 + 2.5;
            Vector3 before = saws.VerticalPosition(start);
            Vector3 middle = saws.VerticalPosition(start + 2);
            Vector3 after = saws.VerticalPosition(start + 3.999);
            Assert.That(before.x, Is.EqualTo(cycle % 2 == 0 ? -24 : 24).Within(.001f));
            Assert.That(after.x, Is.EqualTo(-before.x).Within(.02f));
            Assert.That(middle.x, Is.Zero.Within(.001f));
            Assert.That(middle.y, Is.EqualTo(before.y)); Assert.That(middle.z, Is.EqualTo(before.z));
            Assert.That(Vector3.Dot(saws.PassNormal, Vector3.up), Is.GreaterThan(.999f));
            var secondMiddle = saws.SecondaryVerticalPosition(start + 2);
            Assert.That(Mathf.Abs(middle.z - secondMiddle.z), Is.GreaterThan(2.4f), "같은 순간 중앙에서 두 톱이 포개지지 않는다.");
            typeof(HumanCannonFinalSaws).GetMethod("PresentVertical", Private).Invoke(saws,
                new object[] { first, firstWarning, Array.Empty<Renderer>(), middle, FinalSawPhase.Active, 45f, Color.yellow });
            Assert.That(Vector3.Dot(first.up, saws.PassNormal), Is.GreaterThan(.999f));
        }
    }
}
