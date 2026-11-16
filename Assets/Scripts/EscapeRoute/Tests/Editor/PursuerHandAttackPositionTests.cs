using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerHandAttackPositionTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        GameObject root;
        Component chase;
        BoxCollider area, floor;
        Type navigation;
        Vector3 target;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
        static void Set(object value, string name, object data) => value.GetType().GetField(name, Flags).SetValue(value, data);
        Vector3 Point(Vector3 local) => root.transform.TransformPoint(local);

        BoxCollider Box(string name, Vector3 at, Vector3 size, bool trigger = false)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(root.transform, false); box.transform.localPosition = at;
            box.size = size; box.isTrigger = trigger;
            return box;
        }

        void Make(float yaw = 0)
        {
            root = new GameObject("Receiver hand obstruction regression");
            root.transform.SetPositionAndRotation(new Vector3(56000, 1000, 56000), Quaternion.Euler(0, yaw, 0));
            var inactive = new GameObject("Inactive actor holder");
            inactive.transform.SetParent(root.transform, false); inactive.SetActive(false);
            var clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), inactive.transform);
            chase = clone.GetComponentInChildren(T("EscapeUnderstageChase"), true);
            Set(chase, "breach", null); Set(chase, "walls", null); Set(chase, "boss", null);
            target = Point(new Vector3(-4, .08f, .29f));
            chase.transform.SetPositionAndRotation(Point(Vector3.zero),
                Quaternion.LookRotation(Vector3.ProjectOnPlane(target - Point(Vector3.zero), Vector3.up)));
            floor = Box("Receiver floor", new Vector3(-4.25f, -.3f, .4f), new Vector3(11.5f, .6f, 6.8f));
            area = Box("Receiver search area", new Vector3(-4.25f, 0, .4f), new Vector3(11.5f, 5, 6.8f), true);
            area.center = Vector3.up * 2.5f;
            // Recorded receiver fin relative to feet(-234,5.8,183.3). The R7 windup
            // hand is about(-.106,1.948,1.645), inside the fin; the chest ray is clear.
            Box("Conveyor unloading concrete fin", new Vector3(.2f, -30.8f, 6.25f), new Vector3(.8f, 90, 9.5f));
            navigation = T("EscapePursuerNavigation");
            Physics.SyncTransforms();
        }

        Func<Vector3, bool> Filter() => (Func<Vector3, bool>)Call(chase, "StrikePositionFilter", null, target);
        bool Find(Func<Vector3, bool> filter, out Vector3 result, Vector3? aim = null)
        {
            object[] args = { chase.transform, area, aim ?? target + Vector3.up, 22f, Vector3.zero, filter };
            bool found = (bool)navigation.GetMethod("TryAttackPosition").Invoke(null, args);
            result = (Vector3)args[4]; return found;
        }

        [TearDown] public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        [TestCase(0f)][TestCase(90f)][TestCase(180f)][TestCase(270f)]
        public void RealWindupHandBlockedByReceiverFinSelectsAnotherSupportedAttackPosition(float yaw)
        {
            Make(yaw);
            Vector3 before = chase.transform.position;
            Assert.That(Call(chase, "ClearReach", before + Vector3.up * 3, target + Vector3.up, null), Is.True);
            Vector3 hand = (Vector3)Call(chase, "GrabWindupOrigin");
            Assert.That(Call(chase, "ClearReach", hand, target + Vector3.up, null), Is.False,
                "Reproduce the real model's blocked windup, not merely a blocked chest ray.");
            Assert.That(Find(null, out var unfiltered), Is.True);
            Assert.That(Vector3.Distance(unfiltered, before), Is.LessThan(.02f), "The old search preferred the unusable current position.");
            var filter = Filter();
            Assert.That(filter(before), Is.False);
            Assert.That(Find(filter, out var goal), Is.True);
            Assert.That(Vector3.Distance(goal, before), Is.GreaterThan(.1f));
            Assert.That(filter(goal), Is.True);
            var path = Activator.CreateInstance(navigation);
            for (int i = 0; i < 240 && Vector3.Distance(chase.transform.position, goal) > .05f; i++)
                chase.transform.position = (Vector3)Call(path, "Step", chase.transform, area, goal, .15f);
            Assert.That(Vector3.Distance(chase.transform.position, goal), Is.LessThan(.05f), "The alternate position must be physically reachable.");
            chase.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target - chase.transform.position, Vector3.up));
            hand = (Vector3)Call(chase, "GrabWindupOrigin");
            Assert.That(Call(chase, "ClearReach", hand, target + Vector3.up, null), Is.True,
                "Resampling the actual rig after walking and turning must agree with the candidate filter.");
        }

        [Test] public void BayChoiceRejectsChestVisibleButHandBlockedBayEvenWhenTheAlternativeHasACooldown()
        {
            Make();
            floor.size = new Vector3(14, .6f, 12);
            var near = Box("Blocked hand bay", Vector3.zero, new Vector3(3, 5, 3), true);
            near.center = Vector3.up * 2.5f;
            var other = Box("Clear side bay", new Vector3(-4, 0, -3.5f), new Vector3(4, 5, 4), true);
            other.center = Vector3.up * 2.5f;
            var route = chase.gameObject.AddComponent(T("EscapePursuerRoute"));
            var point = route.GetType().GetNestedType("Point");
            var points = Array.CreateInstance(point, 2);
            for (int i = 0; i < 2; i++) points.SetValue(Activator.CreateInstance(point), i);
            Set(route, "points", points); Set(route, "entered", true); Set(chase, "fullRoute", route);
            var encounter = chase.GetType().GetNestedType("PhysicalEncounter");
            var bays = Array.CreateInstance(encounter, 2);
            for (int i = 0; i < 2; i++)
            {
                var value = Activator.CreateInstance(encounter); var deck = i == 0 ? near : other;
                Set(value, "platform", deck); Set(value, "ingress", deck.transform);
                Set(value, "landing", deck.transform); Set(value, "routePoint", 1); bays.SetValue(value, i);
            }
            Set(chase, "encounters", bays);
            var visited = (Dictionary<int, double>)chase.GetType().GetField("visitedEncounters", Flags).GetValue(chase);
            visited[1] = 103.5d;
            Physics.SyncTransforms();
            Assert.That(Call(chase, "SelectPressureEncounter", target, 0f, 100d), Is.EqualTo(1));
        }

        [TestCase("unsupported")][TestCase("range")][TestCase("body")]
        public void AdditionalStrikeFilterCannotBypassExistingPhysicalConstraints(string condition)
        {
            Make();
            Vector3 aim = target + Vector3.up;
            if (condition == "unsupported") floor.enabled = false;
            if (condition == "range") aim += Vector3.up * 100;
            if (condition == "body") Box("Solid enclosure", new Vector3(-4.25f, 3, .4f), new Vector3(30, 6, 30));
            Physics.SyncTransforms();
            Assert.That(Find(_ => true, out _, aim), Is.False);
        }

        [Test] public void NoUsableHandPositionReportsFailureWithoutInventingAnAttackPoint()
        {
            Make();
            Assert.That(Find(_ => false, out _), Is.False);
        }
    }
}
