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
    }
}
