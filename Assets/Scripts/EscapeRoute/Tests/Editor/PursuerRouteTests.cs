using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerRouteTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject root;
        Component route;
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);

        [TearDown] public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        [TestCase(-8f, 8f, 1f, 0f, true)]
        [TestCase(-8f, 8f, 1f, 2.1f, false)]
        [TestCase(-8f, 8f, 5f, 0f, false)]
        [TestCase(0f, 0f, 1f, 0f, true)]
        [TestCase(0f, 0f, 1f, 2f, false)]
        public void SweptHandDetectsCrossingAndAllowsLeavingLockedLine(float from, float to, float height, float side, bool expected)
        {
            var method = TypeOf("EscapeUnderstageChase").GetMethod("SweptHandTouches", BindingFlags.Static | BindingFlags.NonPublic);
            bool hit = (bool)method.Invoke(null, new object[] { new Vector3(from,height,0), new Vector3(to,height,0), new Vector3(0,0,side), 1.73f, .25f });
            Assert.That(hit, Is.EqualTo(expected));
        }

        void MakeRoute(bool jump = false)
        {
            root = new GameObject("Pursuer route regression");
            root.SetActive(false);
            var type = TypeOf("EscapePursuerRoute");
            route = root.AddComponent(type);
            var pointType = type.GetNestedType("Point");
            var points = Array.CreateInstance(pointType, 3);
            for (int i = 0; i < 3; i++)
            {
                var anchor = new GameObject("Anchor " + i).transform;
                anchor.SetParent(root.transform); anchor.position = Vector3.right * (i * 10);
                var point = Activator.CreateInstance(pointType);
                Set(point,"feet",anchor); Set(point,"playerPath",anchor); Set(point,"throwEnd",anchor);
                Set(point,"label","Stage " + i); Set(point,"jump",jump && i == 1);
                points.SetValue(point,i);
            }
            Set(route,"points",points);
        }

        [Test] public void CannotEnterServiceRouteByTeleportingFromAnotherRoom()
        {
            MakeRoute();
            Assert.That(Call(route,"TryEnter",new Vector3(40,0,0)), Is.False);
            Assert.That(Get(route,"entered"), Is.False);
        }

        [Test] public void ServiceTravelUsesAvailableMovementBudget()
        {
            MakeRoute(); Call(route,"TryEnter",Vector3.zero); Set(route,"desired",2f);
            object[] args = {Vector3.zero, 2f, 5f, .1f, 10d, 0f};
            var next = (Vector3)Call(route,"Advance",args);
            Assert.That(next.x, Is.EqualTo(.2f).Within(.001f));
            Assert.That(next.z, Is.EqualTo(0));
        }

        [Test] public void JumpGapCannotBeWalkedAcross()
        {
            MakeRoute(true); Call(route,"TryEnter",Vector3.zero); Set(route,"desired",2f);
            object[] args = {Vector3.zero, 2f, 5f, 1f, 10d, 0f};
            Assert.That((Vector3)Call(route,"Advance",args), Is.EqualTo(Vector3.zero));
            object[] jump = {Vector3.zero,Vector3.zero};
            Assert.That(Call(route,"TryJump",jump), Is.True);
            Assert.That((Vector3)jump[1], Is.EqualTo(Vector3.right * 10));
            Call(route,"CompleteJump");
            Assert.That((float)Get(route,"cursor"), Is.EqualTo(1));
        }

        [Test] public void RestartClearsTravelAndFinaleDelay()
        {
            MakeRoute(); Call(route,"TryEnter",Vector3.zero); Set(route,"cursor",1.2f); Set(route,"desired",2f); Set(route,"finaleReady",80d);
            Call(route,"ResetRoute",new object[]{null});
            Assert.That((float)Get(route,"cursor"), Is.Zero);
            Assert.That(Get(route,"entered"), Is.False);
            Assert.That((double)Get(route,"finaleReady"), Is.Zero);
        }
    }
}
