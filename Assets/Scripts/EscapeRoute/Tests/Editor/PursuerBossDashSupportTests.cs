using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerBossDashSupportTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject root;
        Component boss;
        MethodInfo clearMovement, supported;

        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CurtainCall.EscapeRoute." + name)).First(type => type != null);
        Vector3 Point(float x, float y = 0, float z = 0) => root.transform.TransformPoint(new Vector3(x, y, z));
        bool CanDash(Vector3 from, Vector3 to) => (bool)clearMovement.Invoke(boss, new object[] { from, to });
        bool HasSupport(Vector3 point) => (bool)supported.Invoke(null, new object[] { point });

        BoxCollider Box(string name, Vector3 center, Vector3 size)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = center;
            box.size = size;
            return box;
        }

        [SetUp]
        public void CreateFixture()
        {
            root = new GameObject("Boss grounded dash regression");
            root.transform.position = new Vector3(42000, 0, 47000);
            var actor = new GameObject("Inactive boss");
            actor.SetActive(false);
            actor.transform.SetParent(root.transform, false);
            boss = actor.AddComponent(TypeOf("EscapePursuerBoss"));
            clearMovement = boss.GetType().GetMethod("ClearMovement", Instance);
            supported = TypeOf("EscapePursuerNavigation").GetMethod("Supported", BindingFlags.Public | BindingFlags.Static);
        }

        [TearDown]
        public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        [Test]
        public void SupportedEndsCannotAuthorizeADashAcrossAnOpenGap()
        {
            Box("West ledge", new Vector3(-2.25f, -.5f, 0), new Vector3(3.5f, 1, 8));
            Box("East ledge", new Vector3(2.25f, -.5f, 0), new Vector3(3.5f, 1, 8));
            Vector3 from = Point(-2), to = Point(2);
            Physics.SyncTransforms();
            Assert.That(HasSupport(from) && HasSupport(to), Is.True, "Both endpoints have the full shared support footprint.");
            Assert.That(CanDash(from, to), Is.False, "A clear airborne body sweep cannot bridge a walking-floor gap.");

            Box("Flush connecting floor", new Vector3(0, -.5f, 0), new Vector3(1, 1, 8));
            Physics.SyncTransforms();
            Assert.That(CanDash(from, to), Is.True, "The same dodge remains available once the real walking surface connects.");
        }

        [Test]
        public void WorkshopCornerCanClearTheColumnAndStillEndWithoutFloor()
        {
            // Saved prototype geometry relative to frame (369.15, 0, 63.5): the aisle
            // begins at X366, while the retained column ends at X364.5.
            Box("Workshop aisle", new Vector3(11.85f, -.4f, 2.5f), new Vector3(30, .8f, 10));
            Box("Retained south column", new Vector3(-7.15f, -9, -2.5f), new Vector3(5, 78, 9));
            Box("Approach edge rail", new Vector3(-17.25f, .8f, 1.5f), new Vector3(27.8f, .15f, .14f));
            Vector3 from = Point(0), to = Point(-3.2f);
            Physics.SyncTransforms();
            Assert.That(HasSupport(from), Is.True);
            Assert.That(HasSupport(to), Is.False, "The proposed feet finish in the strip before the workshop floor.");
            var hits = Physics.CapsuleCastAll(from + Vector3.up * 1.6f, from + Vector3.up * 3.6f, 1.4f,
                Vector3.left, 3.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.That(hits, Is.Empty, "The retained column and guard do not make a collision-only check sufficient.");
            Assert.That(CanDash(from, to), Is.False);
        }

        [Test]
        public void ConnectedFloorStillRejectsAnOrdinaryWall()
        {
            Box("Continuous floor", new Vector3(0, -.5f, 0), new Vector3(12, 1, 8));
            Vector3 from = Point(-3), to = Point(3);
            Physics.SyncTransforms();
            Assert.That(CanDash(from, to), Is.True);
            Box("Wall across dodge", new Vector3(0, 2.5f, 0), new Vector3(.2f, 5, 8));
            Physics.SyncTransforms();
            Assert.That(CanDash(from, to), Is.False, "Floor support never overrides whole-body obstruction.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SupportedStartOrDestinationCannotOverlapScenery(bool atDestination)
        {
            Box("Continuous floor", new Vector3(0, -.5f, 0), new Vector3(12, 1, 8));
            Vector3 from = Point(-3), to = Point(3);
            Box("Body obstruction", new Vector3(atDestination ? 3 : -3, 2.5f, 0), new Vector3(.2f, 1, .2f));
            Physics.SyncTransforms();
            Assert.That(HasSupport(from) && HasSupport(to), Is.True);
            Assert.That(CanDash(from, to), Is.False, "A capsule sweep alone need not report an initial overlap.");
        }
    }
}
