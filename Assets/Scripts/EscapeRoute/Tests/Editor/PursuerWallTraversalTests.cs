using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerWallTraversalTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        GameObject root, runtime;
        Component walls, route;
        Array gates;
        float BreakSeconds => (float)walls.GetType().GetProperty("BreakSeconds", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        [SetUp] public void Setup()
        {
            root = new GameObject("Offline wall traversal geometry"); root.transform.position = new Vector3(3000, 400, -3000);
            runtime = new GameObject("Inactive wall authority"); runtime.transform.SetParent(root.transform, false); runtime.SetActive(false);
            walls = runtime.AddComponent(TypeOf("EscapePursuerWalls"));
            route = runtime.AddComponent(TypeOf("EscapePursuerRoute"));
        }

        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        object Gate(Vector3 center, Vector3 size, float yaw = 0)
        {
            var go = new GameObject("Closed concrete gate"); go.transform.SetParent(root.transform, false);
            go.transform.localPosition = center; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var box = go.AddComponent<BoxCollider>(); box.size = size;
            var gate = Activator.CreateInstance(walls.GetType().GetNestedType("Gate"));
            Set(gate, "panel", go.transform); Set(gate, "barrier", box);
            return gate;
        }

        void SetGates(params object[] entries)
        {
            gates = Array.CreateInstance(walls.GetType().GetNestedType("Gate"), entries.Length);
            for (int i = 0; i < entries.Length; i++) gates.SetValue(entries[i], i);
            Set(walls, "gates", gates); Physics.SyncTransforms();
        }

        Vector3 MakeRoute(params Vector3[] localPoints)
        {
            var pointType = route.GetType().GetNestedType("Point"); var points = Array.CreateInstance(pointType, localPoints.Length);
            for (int i = 0; i < points.Length; i++)
            {
                var anchor = new GameObject("Travel anchor " + i).transform; anchor.SetParent(root.transform, false); anchor.localPosition = localPoints[i];
                var point = Activator.CreateInstance(pointType);
                Set(point, "feet", anchor); Set(point, "playerPath", anchor); Set(point, "throwEnd", anchor);
                points.SetValue(point, i);
            }
            Set(route, "points", points);
            var start = root.transform.TransformPoint(localPoints[0]);
            Assert.That(Call(route, "TryEnter", start), Is.True); Set(route, "desired", (float)(points.Length - 1));
            return start;
        }

        float Limit(Vector3 from, Vector3 to, float distance, double now, bool authority = true)
            => (float)Call(walls, "TravelLimitAt", from, to, distance, now, authority, false);

        Vector3 Tick(Vector3 from, float speed, float dt, double now)
        {
            Vector3 next = (Vector3)route.GetType().GetProperty("NextPosition").GetValue(route);
            float allowed = Limit(from, next, speed * dt, now);
            if (allowed <= .00001f) return from;
            object[] advance = { from, 2.7f, speed, allowed / speed, now, 0f };
            return (Vector3)Call(route, "Advance", advance);
        }

        void AssertClosedWallsWereNotCrossed(Vector3 feet, double now)
        {
            foreach (var gate in gates)
            {
                double started = (double)Get(gate, "started");
                if (started >= 0 && now - started >= BreakSeconds) continue;
                var box = (BoxCollider)Get(gate, "barrier"); var chest = feet + Vector3.up * 2.5f;
                Assert.That(Vector3.Distance(chest, box.ClosestPoint(chest)), Is.GreaterThan(1.39f),
                    "The 1.4 m body cannot enter a still-closed gate during a long frame.");
            }
        }

        [TestCase(0, false, 24f, 1f / 60)][TestCase(0, true, 24f, 1f / 60)]
        [TestCase(37, false, 24f, 1f / 60)][TestCase(37, true, 24f, 1f / 60)]
        [TestCase(90, false, 24f, .33f)][TestCase(90, true, 24f, .33f)]
        [TestCase(-53, false, 6.6f, .33f)][TestCase(-53, true, 6.6f, .33f)]
        public void FirstCannonDiagonalMakesProgressThroughItsActualGateAtDifferentFrameRates(float yaw, bool reverse, float speed, float dt)
        {
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var gate = Gate(new Vector3(23.5f, 3.5f, 57.6f), new Vector3(2.4f, 7, 7)); SetGates(gate);
            Vector3 a = new(18.5f, 0, 66.8f), b = new(26, 0, 53);
            Vector3 position = reverse ? MakeRoute(b, a) : MakeRoute(a, b);
            double now = 10; int stopped = 0, longestStop = 0;
            for (int tick = 0; tick < Mathf.CeilToInt(20 / dt) && (float)Get(route, "cursor") < .99999f; tick++, now += dt)
            {
                var before = position; position = Tick(position, speed, dt, now);
                stopped = Vector3.Distance(before, position) < .00001f ? stopped + 1 : 0;
                longestStop = Mathf.Max(longestStop, stopped);
                AssertClosedWallsWereNotCrossed(position, now);
            }
            Assert.That((double)Get(gate, "started"), Is.GreaterThanOrEqualTo(10d), "The strike actually starts rather than camping outside its reach.");
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(1f).Within(.00001f));
            Assert.That(longestStop, Is.LessThanOrEqualTo(Mathf.CeilToInt(BreakSeconds / dt) + 2));
        }

        [Test] public void RecordedLiveStallAtTwoPointEightTwoMetresTakesTheMissingApproachStep()
        {
            var gate = Gate(new Vector3(23.5f, 3.5f, 57.6f), new Vector3(2.4f, 7, 7)); SetGates(gate);
            MakeRoute(new Vector3(18.5f, 0, 66.8f), new Vector3(26, 0, 53));
            const float cursor = .25877887f; Set(route, "cursor", cursor);
            Vector3 position = root.transform.TransformPoint(Vector3.Lerp(new Vector3(18.5f, 0, 66.8f), new Vector3(26, 0, 53), cursor));
            Vector3 advanced = Tick(position, 24, 1f / 60, 10);
            Assert.That(Vector3.Distance(position, advanced), Is.GreaterThan(.01f));
            Assert.That((double)Get(gate, "started"), Is.EqualTo(-1d), "Approach first; never strike from the future position.");
            Tick(advanced, 24, 1f / 60, 10.02);
            Assert.That((double)Get(gate, "started"), Is.EqualTo(10.02d));
        }

        [Test] public void LongFramesBreakTwoWallsInOrderWithoutSkippingEither()
        {
            var first = Gate(new Vector3(7, 7, 0), new Vector3(.6f, 14, 7));
            var second = Gate(new Vector3(14, 7, 0), new Vector3(.6f, 14, 7)); SetGates(first, second);
            Vector3 position = MakeRoute(Vector3.zero, Vector3.right * 20);
            double now = 10;
            for (int tick = 0; tick < 100 && (float)Get(route, "cursor") < .99999f; tick++, now += .33)
            {
                position = Tick(position, 24, .33f, now);
                AssertClosedWallsWereNotCrossed(position, now);
                if ((double)Get(second, "started") >= 0)
                    Assert.That((double)Get(first, "started"), Is.GreaterThanOrEqualTo(10));
            }
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(1f).Within(.00001f));
            Assert.That((double)Get(second, "started") - (double)Get(first, "started"), Is.GreaterThanOrEqualTo(BreakSeconds));
        }

        [Test] public void FullChaseBudgetChecksTheNextEdgeAfterTurningACorner()
        {
            var gate = Gate(new Vector3(2, 7, 5), new Vector3(7, 14, .6f)); SetGates(gate);
            Vector3 from = MakeRoute(Vector3.zero, Vector3.right * 2, new Vector3(2, 0, 10));
            var chase = runtime.AddComponent(TypeOf("EscapeUnderstageChase")); Set(chase, "fullRoute", route); Set(chase, "walls", walls);
            object[] args = { from, 24f, .33f, 10d, 0f };
            Vector3 next = (Vector3)Call(chase, "AdvanceGroundRoute", args);
            var local = root.transform.InverseTransformPoint(next);
            Assert.That(local.x, Is.EqualTo(2).Within(.001f));
            Assert.That(local.z, Is.InRange(1.8f, 2f), "Spend the first edge's budget, then stop within reach of the second edge's wall.");
            AssertClosedWallsWereNotCrossed(next, 10);
        }

        [TestCase(.99999964f, 2f, 1)]
        [TestCase(1.0000004f, 0f, 1)]
        [TestCase(1.9999996f, 2f, 2)]
        public void PhysicallyReachedAnchorPinsItsFractionalCursorForwardBackwardAndAtTheEnd(float cursor, float desired, int reached)
        {
            MakeRoute(Vector3.zero, Vector3.right * 10, Vector3.right * 20);
            Set(route, "cursor", cursor); Set(route, "desired", desired);
            var chase = runtime.AddComponent(TypeOf("EscapeUnderstageChase")); Set(chase, "fullRoute", route);
            Vector3 from = root.transform.TransformPoint(Vector3.right * (reached * 10));
            object[] args = { from, 24f, 1f / 60, 10d, 0f };
            Assert.That((Vector3)Call(chase, "AdvanceGroundRoute", args), Is.EqualTo(from));
            Assert.That((float)Get(route, "cursor"), Is.EqualTo((float)reached),
                "The encounter's strict source-index threshold must be reachable after world-space rounding.");
        }

        [TestCase(.99999f, 1f)][TestCase(1.00001f, 1f)][TestCase(1.99999f, 2f)]
        public void SubMillimetreRemainingTravelOutsideTheZeroDistanceBranchStillReachesItsAnchor(float cursor, float desired)
        {
            root.transform.position = new Vector3(-500, 400, -500);
            MakeRoute(Vector3.zero, Vector3.right * 100, Vector3.right * 200);
            Set(route, "cursor", cursor); Set(route, "desired", desired);
            Vector3 from = root.transform.TransformPoint(Vector3.right * (cursor * 100));
            Vector3 end = root.transform.TransformPoint(Vector3.right * (desired * 100));
            Assert.That(Vector3.Distance(from, end), Is.GreaterThan(.00001f));
            var chase = runtime.AddComponent(TypeOf("EscapeUnderstageChase")); Set(chase, "fullRoute", route);
            object[] move = { from, 24f, 1f / 60, 10d, 0f };
            Vector3 next = (Vector3)Call(chase, "AdvanceGroundRoute", move);
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(desired));
            Assert.That(Vector3.Distance(next, end), Is.LessThan(.0001f));
        }

        [Test] public void TinyRemainingDistanceStillHonorsTheActualMovementBudget()
        {
            root.transform.position = new Vector3(-500, 400, -500);
            MakeRoute(Vector3.zero, Vector3.right * 100);
            const float cursor = .99999f;
            Set(route, "cursor", cursor); Set(route, "desired", 1f);
            Vector3 from = root.transform.TransformPoint(Vector3.right * (cursor * 100));
            object[] advance = { from, 1f, 1f, .0001f, 10d, 0f };
            Vector3 next = (Vector3)Call(route, "Advance", advance);
            Assert.That((float)Get(route, "cursor"), Is.GreaterThan(cursor).And.LessThan(1f),
                "Consume the small budget instead of snapping over the remaining millimetre.");
            Assert.That(Vector3.Distance(next, from), Is.LessThanOrEqualTo(.0001f + .0000611f),
                "Allow only the world coordinate's float rounding, not a wider arrival teleport.");
        }

        [Test] public void ReachedTakeoffAnchorAlignsBeforeTheNextRealJumpWithoutCompletingIt()
        {
            MakeRoute(Vector3.zero, Vector3.right * 10, Vector3.right * 20);
            Set(((Array)Get(route, "points")).GetValue(2), "jump", true);
            Set(route, "cursor", .99999964f); Set(route, "desired", 2f);
            var chase = runtime.AddComponent(TypeOf("EscapeUnderstageChase")); Set(chase, "fullRoute", route);
            Vector3 from = root.transform.TransformPoint(Vector3.right * 10);
            object[] move = { from, 24f, 1f / 60, 10d, 0f };
            Call(chase, "AdvanceGroundRoute", move);
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(1f));
            object[] jump = { Vector3.zero, Vector3.zero };
            Assert.That(Call(route, "TryJump", jump), Is.True);
            Assert.That((Vector3)jump[0], Is.EqualTo(from));
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(1f));
            Call(route, "CompleteJump");
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(2f));
        }

        [Test] public void CursorAlignmentCannotSkipAnAirborneEdgeOrMoveToADistantAnchor()
        {
            MakeRoute(Vector3.zero, Vector3.right * 10, Vector3.right * 20);
            Set(route, "cursor", .99999964f); Set(route, "desired", 2f);
            var point = ((Array)Get(route, "points")).GetValue(1); Set(point, "jump", true);
            Vector3 at = root.transform.TransformPoint(Vector3.right * 10);
            Assert.That(Call(route, "AlignReachedAnchor", at), Is.False);
            Set(point, "jump", false);
            Assert.That(Call(route, "AlignReachedAnchor", at + Vector3.forward), Is.False);
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(.99999964f));
        }

        [Test] public void ClientQueriesCannotStartABreachButDoNotCrossItsWall()
        {
            var gate = Gate(new Vector3(3, 7, 0), new Vector3(.6f, 14, 7)); SetGates(gate);
            Vector3 from = root.transform.position, to = root.transform.TransformPoint(Vector3.right * 10);
            Assert.That(Limit(from, to, 8, 10, false), Is.Zero);
            Assert.That((double)Get(gate, "started"), Is.EqualTo(-1d));
        }

        [TestCase(0f, -1f)]
        [TestCase(45f, -1f)]
        [TestCase(45f, 1f)]
        public void BreachUsesTheStruckFaceAndKeepsTheFollowThroughAfterCollisionOpens(float yaw, float side)
        {
            var gate = Gate(new Vector3(0, 5, 0), new Vector3(7, 10, .6f), yaw);
            SetGates(gate);
            var panel = (Transform)Get(gate, "panel");
            var from = panel.TransformPoint(new Vector3(.6f, -5, 2f * side));
            Assert.That(Call(walls, "PrepareBreachAt", gate, from, 10d, true), Is.False);
            var contact = panel.InverseTransformPoint((Vector3)Get(gate, "contact"));
            Assert.That(contact.x, Is.EqualTo(.6f).Within(.001f));
            Assert.That(contact.z, Is.EqualTo(.3f * side).Within(.001f));
            double afterImpact = 10d + BreakSeconds + .02;
            Assert.That(Call(walls, "PrepareBreachAt", gate, from, afterImpact, true), Is.True);
            Assert.That(Call(walls, "FinishingStrike", 0, afterImpact), Is.True);
            Assert.That(Call(walls, "FinishingStrike", 0, afterImpact + .4), Is.False);
        }

        [Test] public void AJumpWaitsForItsTakeoffWallToBreak()
        {
            var gate = Gate(new Vector3(3, 7, 0), new Vector3(.6f, 14, 7)); SetGates(gate);
            Vector3 from = root.transform.position, to = root.transform.TransformPoint(Vector3.right * 10);
            Assert.That(Call(walls, "PrepareJumpAt", from, to, 10d, true), Is.False);
            Assert.That((double)Get(gate, "started"), Is.EqualTo(10d));
            Assert.That(Call(walls, "PrepareJumpAt", from, to, 10d + BreakSeconds * .5, true), Is.False);
            Assert.That(Call(walls, "PrepareJumpAt", from, to, 10d + BreakSeconds + .01, true), Is.True);
        }

        [Test] public void ARealJumpArcClearsALowWallThatItsGroundChordWouldHit()
        {
            SetGates(Gate(new Vector3(5, .5f, 0), new Vector3(.6f, 1, 7)));
            Vector3 from = root.transform.position, to = root.transform.TransformPoint(Vector3.right * 10);
            Assert.That(Call(walls, "PrepareJumpAt", from, to, 10d, true), Is.True);
            Assert.That((double)Get(gates.GetValue(0), "started"), Is.EqualTo(-1d));
        }
    }
}
