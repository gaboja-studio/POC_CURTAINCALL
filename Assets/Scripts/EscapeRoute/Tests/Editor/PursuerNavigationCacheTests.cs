using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerNavigationCacheTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject root;
        Transform actor;
        BoxCollider area;
        Collider floor, wall;
        object navigation;
        Type navigationType;
        Vector3 Point(float x, float y, float z) => root.transform.TransformPoint(new Vector3(x, y, z));
        void Set(string name, object value) => navigationType.GetField(name, Flags).SetValue(navigation, value);
        Vector3 Step(Vector3 goal, float distance) => (Vector3)navigationType.GetMethod("Step")
            .Invoke(navigation, new object[] { actor, area, goal, distance });

        Collider Box(string name, Vector3 localPosition, Vector3 size, Transform parent = null)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(parent ? parent : root.transform, false);
            box.transform.position = root.transform.TransformPoint(localPosition);
            box.size = size;
            return box;
        }

        void Make(bool blocked)
        {
            navigationType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CurtainCall.EscapeRoute.EscapePursuerNavigation")).First(t => t != null);
            navigation = Activator.CreateInstance(navigationType);
            root = new GameObject("Pursuer cached navigation regression");
            root.transform.position = new Vector3(52000, 0, 52000);
            floor = Box("Floor", new Vector3(0, -.3f, 0), new Vector3(24, .6f, 20));
            wall = Box("Moving scenery", new Vector3(0, 3, 0), new Vector3(2, 6, 8));
            wall.enabled = blocked;
            area = (BoxCollider)Box("Pursuit area", Vector3.zero, new Vector3(24, 5, 20));
            area.center = Vector3.up * 2.5f;
            area.isTrigger = true;
            actor = new GameObject("Pursuer").transform;
            actor.SetParent(root.transform, false);
            actor.localPosition = Vector3.left * 8;
            Physics.SyncTransforms();
        }

        void FreezeRefresh()
        {
            Set("nextSearch", float.MaxValue);
            Set("nextShortcut", float.MaxValue);
        }

        [TearDown] public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        [TestCase(false)]
        [TestCase(true)]
        public void CachedStepReplansWhenSceneryAppearsOrMovesIntoItsWay(bool moved)
        {
            Make(false);
            Vector3 goal = Point(8, 0, 0);
            if (moved) { wall.enabled = true; wall.transform.position = Point(0, 3, 30); Physics.SyncTransforms(); }
            actor.position = Step(goal, .2f);
            FreezeRefresh();
            wall.enabled = true;
            wall.transform.position = actor.position + new Vector3(2.1f, 3, 0);
            ((BoxCollider)wall).size = new Vector3(.4f, 6, 4);
            Physics.SyncTransforms();
            Vector3 before = actor.position;
            actor.position = Step(goal, .8f);
            Assert.That(Vector3.Distance(actor.position, before), Is.LessThanOrEqualTo(.81f));
            for (int i = 0; i < 300 && Vector3.Distance(actor.position, goal) > .1f; i++)
            {
                actor.position = Step(goal, .2f);
                Assert.That(Physics.CheckCapsule(actor.position + Vector3.up * 1.5f,
                    actor.position + Vector3.up * 3.5f, 1.39f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore), Is.False, "The replacement path must not cross the new scenery.");
            }
            Assert.That(Vector3.Distance(actor.position, goal), Is.LessThan(.1f));
        }

        [Test] public void CachedStepChecksSupportBeforeItsRefreshDeadline()
        {
            Make(false);
            Vector3 goal = Point(8, 0, 0);
            actor.position = Step(goal, .2f);
            FreezeRefresh();
            floor.enabled = false;
            Physics.SyncTransforms();
            Assert.That(Step(goal, 6), Is.EqualTo(actor.position));
        }

        [Test] public void MovingGoalReplacesTheOldDetourWhenSearchIsDue()
        {
            Make(true);
            actor.position = Step(Point(8, 0, 0), .2f);
            FreezeRefresh();
            Set("nextSearch", -1f);
            Vector3 goal = Point(-9, 0, -7);
            Assert.That(Vector3.Distance(Step(goal, .3f), Vector3.MoveTowards(actor.position, goal, .3f)), Is.LessThan(.01f));
        }

        [Test] public void ChangingPursuitAreaDiscardsTheOldCachedGoal()
        {
            Make(false);
            actor.position = Step(Point(8, 0, 0), .2f);
            FreezeRefresh();
            area = (BoxCollider)Box("Replacement area", new Vector3(-7, 0, -5), new Vector3(6, 5, 6));
            area.center = Vector3.up * 2.5f;
            area.isTrigger = true;
            Physics.SyncTransforms();
            Vector3 wanted = Point(8, 0, 0);
            Vector3 clamped = (Vector3)navigationType.GetMethod("Clamp").Invoke(null, new object[] { area, wanted });
            Assert.That(Vector3.Distance(Step(wanted, .3f), Vector3.MoveTowards(actor.position, clamped, .3f)), Is.LessThan(.01f));
        }

        [Test] public void LargeCachedStepCannotSkipAnUnsupportedMiddle()
        {
            Make(false);
            Vector3 goal = Point(8, 0, 0);
            actor.position = Step(goal, .2f);
            FreezeRefresh();
            floor.enabled = false;
            Box("Left ledge", new Vector3(-7.5f, -.3f, 0), new Vector3(9, .6f, 20));
            Box("Right ledge", new Vector3(7.5f, -.3f, 0), new Vector3(9, .6f, 20));
            Physics.SyncTransforms();
            Vector3 next = Step(goal, 20);
            Assert.That(next.x, Is.LessThan(Point(-3, 0, 0).x), "Supported endpoints cannot authorize crossing the new gap.");
        }

        [Test] public void RemovingSceneryKeepsTheDetourUntilShortcutRefreshThenUsesTheOpening()
        {
            Make(true);
            Vector3 goal = Point(8, 0, 0);
            actor.position = Step(goal, .2f);
            FreezeRefresh();
            var path = (IList)navigationType.GetField("path", Flags).GetValue(navigation);
            Vector3 waypoint = path.Cast<Vector3>().First(p => (p - actor.position).sqrMagnitude >= .04f);
            wall.enabled = false;
            Physics.SyncTransforms();
            Vector3 next = Step(goal, .2f);
            Assert.That(Vector3.Distance(next, Vector3.MoveTowards(actor.position, waypoint, .2f)), Is.LessThan(.01f));
            actor.position = next;
            Set("nextShortcut", -1f);
            Assert.That(Vector3.Distance(Step(goal, .2f), Vector3.MoveTowards(actor.position, goal, .2f)), Is.LessThan(.01f));
        }

        [TestCase("Overlap")]
        [TestCase("Sweep")]
        [TestCase("Sight")]
        public void CrowdedIgnoredHitsDoNotHideARealBlocker(string query)
        {
            Make(false);
            Vector3 from = actor.position, to = Point(8, 0, 0);
            Vector3 center = query == "Overlap" ? new Vector3(8, 3, 0) : new Vector3(0, 3, 0);
            for (int i = 0; i < 96; i++) Box("Ignored actor shape " + i, center, Vector3.one * .1f, actor);
            Physics.SyncTransforms();
            bool Check() => (bool)navigationType.GetMethod(query == "Sight" ? "Sees" : "Clear").Invoke(null,
                new object[] { actor, query == "Overlap" ? to : from, query == "Sight" ? to + Vector3.up * 3 : to });
            Assert.That(Check(), Is.True, "A full query containing only the pursuer's shapes must remain traversable.");
            Box("Actual scenery", center, Vector3.one * .2f);
            Physics.SyncTransforms();
            Assert.That(Check(), Is.False, "The overflow fallback must inspect every hit, including fixed scenery.");
        }
    }
}
