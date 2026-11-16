using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerBranchGuideTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject root;
        Component route;
        Type routeType;

        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);

        [SetUp]
        public void CreateRoute()
        {
            root = new GameObject("Branch player guide regression");
            root.SetActive(false);
            routeType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CurtainCall.EscapeRoute.EscapePursuerRoute")).First(t => t != null);
            route = root.AddComponent(routeType);
            var pointType = routeType.GetNestedType("Point");
            var points = Array.CreateInstance(pointType, 3);
            for (int i = 0; i < points.Length; i++)
            {
                var point = Activator.CreateInstance(pointType);
                var anchor = Anchor(Vector3.right * (i * 10));
                Set(point, "feet", anchor);
                Set(point, "playerPath", anchor);
                Set(point, "throwEnd", anchor);
                points.SetValue(point, i);
            }
            Set(route, "points", points);
        }

        [TearDown]
        public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        Transform Anchor(Vector3 position)
        {
            var anchor = new GameObject("Guide anchor").transform;
            anchor.SetParent(root.transform);
            anchor.position = position;
            return anchor;
        }

        object Guide(Vector3 from, Vector3 to, float fromProgress, float toProgress)
        {
            var guide = Activator.CreateInstance(routeType.GetNestedType("PlayerGuide"));
            Set(guide, "from", Anchor(from));
            Set(guide, "to", Anchor(to));
            Set(guide, "fromProgress", fromProgress);
            Set(guide, "toProgress", toProgress);
            return guide;
        }

        void Guides(params object[] entries)
        {
            var guides = Array.CreateInstance(routeType.GetNestedType("PlayerGuide"), entries.Length);
            for (int i = 0; i < entries.Length; i++) guides.SetValue(entries[i], i);
            Set(route, "playerGuides", guides);
        }

        float Project(Vector3 position, out float distance)
        {
            object[] args = { position, 0f };
            float progress = (float)routeType.GetMethod("Project").Invoke(route, args);
            distance = (float)args[1];
            return progress;
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void AlternateArmsMapToTheSameServiceProgress(float side)
        {
            Vector3 start = Vector3.zero, end = Vector3.right * 20;
            Vector3 upper = new(10, 0, 10), lower = new(10, 0, -10);
            Guides(Guide(start, upper, 0, 1), Guide(upper, end, 1, 2),
                Guide(start, lower, 0, 1), Guide(lower, end, 1, 2));

            Assert.That(Project(new Vector3(15, 0, side * 5), out float distance), Is.EqualTo(1.5f).Within(.0001f));
            Assert.That(distance, Is.Zero.Within(.0001f));
            Assert.That(Project(end, out distance), Is.EqualTo(2));
            Assert.That(distance, Is.Zero.Within(.0001f));
        }

        [Test]
        public void SeparateGuideRowsDoNotInventAConnectorOrUseTheLegacyCenterline()
        {
            Guides(Guide(new Vector3(0, 0, 10), new Vector3(10, 0, 10), 0, 1),
                Guide(new Vector3(0, 0, -10), new Vector3(10, 0, -10), 0, 1));

            Assert.That(Project(new Vector3(5, 0, 0), out float distance), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(distance, Is.EqualTo(10).Within(.0001f),
                "The empty space between arms is neither a guide edge nor the old player-path line.");
        }

        [Test]
        public void NullAndZeroLengthGuidesFallBackToTheLegacyPath()
        {
            var missing = Guide(Vector3.zero, Vector3.one, 0, 1);
            Set(missing, "to", null);
            Guides(null, missing, Guide(Vector3.zero, Vector3.zero, 0, 2));

            Assert.That(Project(new Vector3(15, 0, 3), out float distance), Is.EqualTo(1.5f).Within(.0001f));
            Assert.That(distance, Is.EqualTo(3).Within(.0001f));
        }

        [Test]
        public void InvalidRowsCannotHideAValidGuideAndMappedProgressStaysWithinTheRoute()
        {
            Guides(null, Guide(Vector3.zero, Vector3.zero, 0, 2),
                Guide(new Vector3(0, 0, 10), new Vector3(10, 0, 10), -2, 4));

            Assert.That(Project(new Vector3(0, 0, 10), out float distance), Is.Zero);
            Assert.That(distance, Is.Zero);
            Assert.That(Project(new Vector3(10, 0, 10), out distance), Is.EqualTo(2));
            Assert.That(distance, Is.Zero);
        }

        [TestCase(-5f, 3f, 0f, 5.8309519f)]
        [TestCase(5f, 3f, .5f, 3f)]
        [TestCase(15f, 3f, 1.5f, 3f)]
        [TestCase(25f, 3f, 2f, 5.8309519f)]
        public void NoGuidesPreserveLegacyProjection(float x, float z, float expectedProgress, float expectedDistance)
        {
            Assert.That(Project(new Vector3(x, 0, z), out float distance), Is.EqualTo(expectedProgress).Within(.0001f));
            Assert.That(distance, Is.EqualTo(expectedDistance).Within(.0001f));
        }

        [Test]
        public void RestartInvalidatesTheCachedPlayerPathDistances()
        {
            Set(route, "pathMetres", new[] { 0f, 10f, 20f });
            routeType.GetMethod("ResetRoute", Flags).Invoke(route, new object[] { null });
            Assert.That(routeType.GetField("pathMetres", Flags).GetValue(route), Is.Null);
        }
    }
}
