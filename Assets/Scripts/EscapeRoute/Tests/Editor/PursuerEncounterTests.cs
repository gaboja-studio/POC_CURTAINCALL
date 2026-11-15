using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class PursuerEncounterTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject root;
        Component route, chase;
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        bool CanAttack => (bool)chase.GetType().GetProperty("CanAttackFromRoute", Flags).GetValue(chase);

        [SetUp] public void Setup()
        {
            root = new GameObject("Physical pursuer regression"); root.SetActive(false);
            route = root.AddComponent(TypeOf("EscapePursuerRoute"));
            chase = root.AddComponent(TypeOf("EscapeUnderstageChase"));
            var pointType = route.GetType().GetNestedType("Point");
            var points = Array.CreateInstance(pointType, 2);
            for (int i = 0; i < 2; i++) points.SetValue(Activator.CreateInstance(pointType), i);
            Set(route, "points", points); Set(route, "entered", true); Set(chase, "fullRoute", route);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        [TestCase(-1, 0, false)]
        [TestCase(0, 3, false)]
        [TestCase(0, 5, false)]
        [TestCase(0, 6, true)]
        [TestCase(0, 9, false)]
        public void CaptureStartsOnlyAfterLandingOutside(int encounter, int phase, bool expected)
        {
            Set(chase, "encounterIndex", encounter); Set(chase, "encounterPhase", phase);
            Assert.That(CanAttack, Is.EqualTo(expected));
        }
        [Test] public void FinalOutdoorRouteRemainsAttackableAfterEncounterEnds()
        {
            Set(((Array)Get(route, "points")).GetValue(1), "exposed", true);
            Set(route, "cursor", 1f);
            Assert.That(CanAttack, Is.True);
        }
        [Test] public void RestartDoesNotLeaveAnOutdoorCaptureOrJumpActive()
        {
            Set(chase, "encounterIndex", 0); Set(chase, "encounterPhase", 6); Set(chase, "encounterJump", true);
            chase.GetType().GetMethod("ResetEncounters", Flags).Invoke(chase, null);
            Assert.That(CanAttack, Is.False);
            Assert.That(Get(chase, "encounterJump"), Is.False);
        }
    }
}
