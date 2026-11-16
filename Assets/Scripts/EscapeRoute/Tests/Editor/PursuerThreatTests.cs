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
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

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

        [Test] public void DiscardThrowUsesTheLongClearArcBeforeTheNearbyAuthoredBay()
        {
            var root = new GameObject("Long throw search"); root.SetActive(false); root.transform.position = new Vector3(10000, 5000, 10000);
            try
            {
                var chase = root.AddComponent(Chase);
                var release = root.transform.position + Vector3.up * 2;
                var preferred = root.transform.position + Vector3.forward * 7 - Vector3.up * 8;
                var args = new object[] { release, preferred, Vector3.zero };
                Assert.That(Chase.GetMethod("TryFindThrow", Flags).Invoke(chase, args), Is.True);
                var drop = (Vector3)args[2];
                var tuning = Chase.GetProperty("Tuning", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                float range = (float)tuning.GetType().GetField("torsoThrowRange").GetValue(tuning);
                Assert.That(Vector3.Distance(release, drop), Is.EqualTo(range).Within(.01f));
                Assert.That(Vector3.ProjectOnPlane(drop - release, Vector3.up).magnitude, Is.GreaterThan(range * .75f));
                Assert.That(Vector3.Dot(drop - release, Vector3.forward), Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void RaisedThrowArcRejectsAnOverheadBeamThatAStraightSweepMisses()
        {
            var root = new GameObject("Arc collision"); root.SetActive(false); root.transform.position = new Vector3(10000, 5000, 10000);
            var beam = new GameObject("Overhead beam").AddComponent<BoxCollider>();
            try
            {
                var chase = root.AddComponent(Chase);
                Vector3 release = root.transform.position, drop = release + Vector3.right * 12;
                beam.transform.position = release + new Vector3(6, 1.7f, 0); beam.size = new Vector3(1, .2f, 2);
                Physics.SyncTransforms();
                Assert.That(Physics.SphereCast(release + Vector3.up * .8f, .55f, Vector3.right, out _, 12f), Is.False);
                Assert.That(Chase.GetMethod("ClearThrowArc", Flags).Invoke(chase, new object[] { release, drop }), Is.False);
                beam.enabled = false; Physics.SyncTransforms();
                Assert.That(Chase.GetMethod("ClearThrowArc", Flags).Invoke(chase, new object[] { release, drop }), Is.True);
            }
            finally { Object.DestroyImmediate(beam.gameObject); Object.DestroyImmediate(root); }
        }

        [Test] public void DiscardThrowRejectsAPlatformUnderItsEndpoint()
        {
            var root = new GameObject("Throw endpoint support"); root.SetActive(false); root.transform.position = new Vector3(10000, 5000, 10000);
            var floor = new GameObject("Occupied throw bay").AddComponent<BoxCollider>();
            try
            {
                var chase = root.AddComponent(Chase);
                Vector3 release = root.transform.position + Vector3.up * 2, drop = root.transform.position + new Vector3(10, -8, 0);
                floor.transform.position = drop - Vector3.up * .1f; floor.size = new Vector3(2, .2f, 2); Physics.SyncTransforms();
                Assert.That(Chase.GetMethod("ClearThrow", Flags).Invoke(chase, new object[] { release, drop }), Is.False);
                floor.enabled = false; Physics.SyncTransforms();
                Assert.That(Chase.GetMethod("ClearThrow", Flags).Invoke(chase, new object[] { release, drop }), Is.True);
            }
            finally { Object.DestroyImmediate(floor.gameObject); Object.DestroyImmediate(root); }
        }

        [Test] public void HeldFirstPoleKeepsItsWireWarningAndFullReachAfterRouteEntry()
        {
            var root = new GameObject("First pole capture range"); root.SetActive(false);
            var poleType = TypeOf("CurtainCall.HumanCannon.HumanCannonPoleSystem");
            var current = poleType.GetProperty("Current"); var previous = current.GetValue(null);
            Component poles = null;
            try
            {
                var chase = root.AddComponent(Chase);
                var route = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapePursuerRoute"));
                Set(chase, "fullRoute", route); Set(route, "entered", true);
                root.AddComponent(TypeOf("Unity.Netcode.NetworkObject"));
                var player = root.AddComponent(TypeOf("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                ulong actor = (ulong)player.GetType().GetProperty("OwnerClientId").GetValue(player);
                poles = root.AddComponent(poleType); current.SetValue(null, poles);
                var runtimeType = poleType.GetNestedType("Runtime", BindingFlags.NonPublic);
                var runtime = Activator.CreateInstance(runtimeType, new object[] { 1 });
                var states = Array.CreateInstance(runtimeType, 1); states.SetValue(runtime, 0); Set(poles, "states", states);
                var state = runtimeType.GetField("State").GetValue(runtime);
                Assert.That(state.GetType().GetMethod("TryGrab").Invoke(state, new object[] { actor, 0u, 0d, 0f, false, -1 }), Is.True);
                var reach = Chase.GetNestedType("Reach", BindingFlags.NonPublic);
                var hall = Enum.Parse(reach, "Hall");
                Assert.That(Chase.GetMethod("AttackReach", Flags).Invoke(chase, new[] { (object)player, hall }).ToString(), Is.EqualTo("Wire"));
                var tuning = Chase.GetProperty("Tuning", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                float armRange = (float)tuning.GetType().GetField("armRange").GetValue(tuning);
                float exposedRange = (float)tuning.GetType().GetField("exposedGrabRange").GetValue(tuning);
                Assert.That(Chase.GetMethod("AttackRange", Flags).Invoke(chase, new object[] { player }), Is.EqualTo(armRange));
                Assert.That(armRange, Is.GreaterThan(35f), "The first pole is about 35 m across the chasm.");
                Assert.That(state.GetType().GetMethod("TryRelease").Invoke(state, new object[] { actor, 1u, 0d, .1f }), Is.True);
                Assert.That(Chase.GetMethod("AttackReach", Flags).Invoke(chase, new[] { (object)player, hall }).ToString(), Is.EqualTo("Hall"));
                Assert.That(Chase.GetMethod("AttackRange", Flags).Invoke(chase, new object[] { player }), Is.EqualTo(exposedRange));
            }
            finally
            {
                if (poles) Set(poles, "states", null);
                Object.DestroyImmediate(root); current.SetValue(null, previous);
            }
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

        [Test] public void FinishedThreatRemovesItsGuidesAndProjectile()
        {
            var root = new GameObject("Threat guide lifetime"); root.SetActive(false);
            try
            {
                var chase = root.AddComponent(Chase);
                var line = new GameObject("Aim line").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform);
                var ring = new GameObject("Impact marker").AddComponent<LineRenderer>(); ring.transform.SetParent(root.transform);
                var debris = new GameObject("Debris").transform; debris.SetParent(root.transform);
                Set(chase, "captureCable", line); Set(chase, "aimRing", ring); Set(chase, "thrownDebris", debris);
                var cueType = Chase.GetNestedType("ThreatCue", BindingFlags.NonPublic);
                var cue = Activator.CreateInstance(cueType);
                Set(cue, "Phase", Enum.Parse(cueType.GetField("Phase").FieldType, "Impact"));
                Set(cue, "Duration", 1f);
                line.enabled = ring.enabled = true;
                Chase.GetMethod("PresentThreat", Flags).Invoke(chase, new[] { cue, (object)1d });
                Assert.That(line.enabled || ring.enabled || debris.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void MissingPoseReferenceCannotLeaveAnOldCaptureGuideVisible()
        {
            var root = new GameObject("Capture guide cleanup"); root.SetActive(false);
            try
            {
                var chase = root.AddComponent(Chase);
                var line = new GameObject("Aim line").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform);
                var ring = new GameObject("Impact marker").AddComponent<LineRenderer>(); ring.transform.SetParent(root.transform);
                Set(chase, "captureCable", line); Set(chase, "aimRing", ring);
                line.enabled = ring.enabled = true;
                var cue = Activator.CreateInstance(Chase.GetNestedType("AttackCue", BindingFlags.NonPublic));
                Chase.GetMethod("PresentAttack", Flags).Invoke(chase, new[] { cue, (object)0d });
                Assert.That(line.enabled || ring.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void OpeningRoarStartsWithoutAHallTargetAndRetainsItsGuards()
        {
            var root = new GameObject("Targetless opening roar"); root.SetActive(false);
            var timerType = TypeOf("CurtainCall.EscapeRoute.EscapeRouteTimer");
            var current = timerType.GetProperty("Current"); var previous = current.GetValue(null);
            try
            {
                var chase = root.AddComponent(Chase);
                var agent = (Behaviour)root.GetComponent(TypeOf("UnityEngine.AI.NavMeshAgent"));
                agent.enabled = false; Set(chase, "agent", agent);
                current.SetValue(null, root.AddComponent(timerType));
                var begin = Chase.GetMethod("BeginChargeWarning", Flags);
                Assert.That(begin.Invoke(chase, new object[] { null }), Is.True,
                    "A player already on the first pole may be excluded from the entrance hall target during the opening pause.");
                var cue = Chase.GetField("threat", Flags).GetValue(chase);
                Assert.That(cue.GetType().GetField("Phase").GetValue(cue).ToString(), Is.EqualTo("ChargeWarning"));
                Assert.That(cue.GetType().GetField("Actor").GetValue(cue), Is.EqualTo(ulong.MaxValue));
                Assert.That(cue.GetType().GetField("To").GetValue(cue), Is.EqualTo(root.transform.position + root.transform.forward * 3));
                Set(chase, "charging", true);
                Assert.That(begin.Invoke(chase, new object[] { null }), Is.False, "Do not restart a completed opening charge.");
                Set(chase, "charging", false); Chase.GetProperty("InShowcase", Flags).SetValue(chase, true);
                Assert.That(begin.Invoke(chase, new object[] { null }), Is.False, "Showcase presentation remains isolated.");
            }
            finally { Object.DestroyImmediate(root); current.SetValue(null, previous); }
        }

        [Test] public void OpeningRoarDoesNotShowAPlayerTargetMarkerOrLine()
        {
            var root = new GameObject("Opening roar presentation"); root.SetActive(false);
            try
            {
                var chase = root.AddComponent(Chase);
                var line = new GameObject("Aim line").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform);
                var ring = new GameObject("Target ring").AddComponent<LineRenderer>(); ring.transform.SetParent(root.transform);
                Set(chase, "captureCable", line); Set(chase, "aimRing", ring); line.enabled = ring.enabled = true;
                var cueType = Chase.GetNestedType("ThreatCue", BindingFlags.NonPublic); var cue = Activator.CreateInstance(cueType);
                Set(cue, "Phase", Enum.Parse(cueType.GetField("Phase").FieldType, "ChargeWarning")); Set(cue, "Duration", 1.5f);
                Chase.GetMethod("PresentThreat", Flags).Invoke(chase, new[] { cue, (object).5d });
                Assert.That(line.enabled || ring.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
