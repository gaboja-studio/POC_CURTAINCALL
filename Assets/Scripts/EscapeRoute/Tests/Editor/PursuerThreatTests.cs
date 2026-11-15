using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class PursuerThreatTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type Chase => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeUnderstageChase")).First(t => t != null);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Static(string name, params object[] args) => Chase.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        [TestCase(0f, 0f, true)]
        [TestCase(4f, 0f, true)]
        [TestCase(12f, 0f, true)]
        [TestCase(4f, 2f, false)]
        public void ProjectileUsesElevatedBodyCapsuleAndAllowsSidestepping(float feetHeight, float side, bool expected)
        {
            var feet = new Vector3(0, feetHeight, side);
            Assert.That(Static("SweptSphereTouches", new Vector3(-10, feetHeight + 1, 0), new Vector3(10, feetHeight + 1, 0), feet, 1.73f, .25f, .55f), Is.EqualTo(expected));
        }

        [TestCase("DebrisPosition")]
        [TestCase("ThrowPosition")]
        public void ThrowReachesLockedTargetAndHasAnElevatedMidpoint(string method)
        {
            var start = new Vector3(-4, 7, 2); var end = new Vector3(11, 2, -7);
            Assert.That(Static(method, start, end, 0f), Is.EqualTo(start));
            Assert.That(Static(method, start, end, 1f), Is.EqualTo(end));
            Assert.That(((Vector3)Static(method, start, end, .5f)).y, Is.GreaterThan((start.y + end.y) / 2));
        }

        [Test]
        public void ThreatSnapshotRejectsInvalidDurationAndDelayedRoundWithoutReplacingAcceptedState()
        {
            var root = new GameObject("Threat snapshot regression"); root.SetActive(false);
            try
            {
                var chase = root.AddComponent(Chase);
                var snapshotType = Chase.GetNestedType("Snapshot", BindingFlags.NonPublic);
                var threatType = Chase.GetNestedType("ThreatCue", BindingFlags.NonPublic);
                object Snapshot(double round, double time, float duration)
                {
                    var value = Activator.CreateInstance(snapshotType);
                    Set(value, "Round", round); Set(value, "Time", time); Set(value, "Rotation", Quaternion.identity);
                    var cue = Activator.CreateInstance(threatType);
                    Set(cue, "Phase", Enum.ToObject(threatType.GetField("Phase").FieldType, 1));
                    Set(cue, "Started", time); Set(cue, "Duration", duration); Set(value, "Threat", cue);
                    return value;
                }
                bool Accept(object value, double round) => (bool)Chase.GetMethod("Accept", Flags).Invoke(chase, new[] { value, (object)round });
                Assert.That(Accept(Snapshot(10, 11, 1.15f), 10), Is.True);
                foreach (float duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 11f })
                    Assert.That(Accept(Snapshot(10, 12, duration), 10), Is.False);
                Assert.That(Accept(Snapshot(10, 11.5, 1.15f), 10), Is.True, "Malformed updates must not advance the receive clock.");
                Assert.That(Accept(Snapshot(10, 11, 1.15f), 10), Is.False);
                Assert.That(Accept(Snapshot(20, 21, 1.15f), 20), Is.True);
                Assert.That(Accept(Snapshot(10, 22, 1.15f), 20), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
