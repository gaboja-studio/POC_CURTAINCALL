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

        [Test]
        public void ConveyorWaitCoversFirstBoardingButCannotBeRefreshedByDismountingOrLaunching()
        {
            var window = Activator.CreateInstance(TypeOf("EscapeUnderstageChase").GetNestedType("ConveyorWait", BindingFlags.NonPublic));
            bool Wait(double now, bool deck, bool seated = false, bool flying = false)
                => (bool)Call(window, "Sample", now, deck, seated, flying, 16f, 13f);

            Assert.That(Wait(90, false), Is.False, "Nearby lower or upper routes must not start the window.");
            Assert.That(Wait(100, true), Is.True);
            Assert.That(Wait(112, true, true), Is.True);
            Assert.That(Wait(124, true, true), Is.True, "Let the player watch more than two pole arrivals after boarding.");
            Assert.That(Wait(124.1, false), Is.False);
            Assert.That(Wait(124.2, true, true, true), Is.False);
            Assert.That(Wait(124.5, true, true), Is.False, "A rebounding or returning player cannot restart the wait.");

            window = Activator.CreateInstance(window.GetType());
            Assert.That(Wait(100, true), Is.True);
            Assert.That(Wait(115, true, true), Is.True);
            Assert.That(Wait(117, true), Is.False);
            Assert.That(Wait(127, true, true), Is.True);
            Assert.That(Wait(128, true, true), Is.False, "Dismount and reboard must keep the first boarding deadline.");

            window = Activator.CreateInstance(window.GetType());
            Assert.That(Wait(100, true), Is.True);
            Assert.That(Wait(116, true, true), Is.False, "Parking before boarding must not mint a new window.");
        }

        [Test]
        public void EntranceClipCanBeSampledWithoutAnimatorInAPlayerBuild()
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/UnderstageEntrance.anim");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.legacy, Is.True);
        }

        [TestCase(419d, 44d, 0f)]
        [TestCase(60d, 67.5d, .5f)]
        [TestCase(442.5d, 0d, .5f)]
        [TestCase(442.5d, 67.5d, .5f)]
        [TestCase(5d, 150d, 1f)]
        public void RoutePressureUsesTheStrongerClockAndCapsTheResult(double elapsed, double idle, float expected)
        {
            var method = TypeOf("EscapePursuerRoute").GetMethod("PressureAt", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((float)method.Invoke(null, new object[] { elapsed, idle, 420f, 45f, 45f }), Is.EqualTo(expected));
        }

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
            Assert.That(next.x, Is.EqualTo(.5f).Within(.001f));
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

        [Test] public void CloseTargetDoesNotResetRunningSpeed()
        {
            MakeRoute();
            Call(route, "TryEnter", Vector3.zero);
            Set(route, "cursor", .95f);
            Set(route, "desired", 1f);
            object[] args = { Vector3.right * 9.5f, 2f, 5f, .02f, 10d, 0f };
            var next = (Vector3)Call(route, "Advance", args);
            Assert.That(next.x, Is.EqualTo(9.6f).Within(.001f));
            Assert.That((float)args[5], Is.EqualTo(1f));
        }

        [Test] public void CrossingARevealKeepsRunningAndUsesTheRemainingBudget()
        {
            MakeRoute();
            var points = (Array)Get(route, "points");
            Set(points.GetValue(1), "reveal", true);
            Call(route, "TryEnter", Vector3.zero);
            Set(route, "cursor", .98f);
            Set(route, "desired", 2f);
            object[] args = { Vector3.right * 9.8f, 2f, 5f, .1f, 10d, 0f };
            Assert.That(((Vector3)Call(route, "Advance", args)).x, Is.EqualTo(10.3f).Within(.001f));
        }

        [Test] public void RestartClearsTravelAndFinaleDelay()
        {
            MakeRoute(); Call(route,"TryEnter",Vector3.zero); Set(route,"cursor",1.2f); Set(route,"desired",2f); Set(route,"finaleReady",80d);
            Call(route,"ResetRoute",new object[]{null});
            Assert.That((float)Get(route,"cursor"), Is.Zero);
            Assert.That(Get(route,"entered"), Is.False);
            Assert.That((double)Get(route,"finaleReady"), Is.Zero);
        }

        [Test] public void ReturningSurvivorCanBeFollowedBackThroughTheRoute()
        {
            MakeRoute();
            Call(route, "TryEnter", Vector3.zero);
            Set(route, "cursor", 1.5f);
            Set(route, "desired", .5f);
            object[] args = { Vector3.right * 15, 2f, 5f, 1f, 10d, 0f };
            Assert.That(((Vector3)Call(route, "Advance", args)).x, Is.EqualTo(10).Within(.001f));
            args[0] = Vector3.right * 10;
            Assert.That(((Vector3)Call(route, "Advance", args)).x, Is.EqualTo(5).Within(.001f));
        }

        [Test] public void ReturningAcrossAGapRequiresAReverseJump()
        {
            MakeRoute(true);
            Call(route, "TryEnter", Vector3.zero);
            Set(route, "cursor", 1f);
            Set(route, "desired", 0f);
            object[] args = { Vector3.right * 10, 2f, 5f, 1f, 10d, 0f };
            Assert.That((Vector3)Call(route, "Advance", args), Is.EqualTo(Vector3.right * 10));
            object[] jump = { Vector3.zero, Vector3.zero };
            Assert.That(Call(route, "TryJump", jump), Is.True);
            Assert.That((Vector3)jump[0], Is.EqualTo(Vector3.right * 10));
            Assert.That((Vector3)jump[1], Is.EqualTo(Vector3.zero));
            Call(route, "CompleteJump");
            Assert.That((float)Get(route, "cursor"), Is.Zero);
        }

        [Test] public void ReverseArrivalPinsTheCursorToTheStartAndKeepsAllRouteReadersValid()
        {
            MakeRoute();
            var points = (Array)Get(route, "points");
            ((Transform)Get(points.GetValue(1), "feet")).position = Vector3.right * 27.631f;
            Call(route, "TryEnter", Vector3.zero);
            Set(route, "desired", 0f);
            for (int i = 11; i < 300; i++)
            {
                Set(route, "cursor", i / 100003f);
                object[] args = { Vector3.zero, 2f, 5f, 1f, 10d, 0f };
                Assert.That((Vector3)Call(route, "Advance", args), Is.EqualTo(Vector3.zero));
                Assert.That((float)Get(route, "cursor"), Is.Zero);
                foreach (string property in new[] { "Exposed", "NextPosition", "FinalSprint", "CurrentSection", "ThrowEnd" })
                    Assert.DoesNotThrow(() => route.GetType().GetProperty(property).GetValue(route), property);
            }
        }

        [Test] public void BoundaryRoundoffCannotPoisonTheNextMovementUpdate()
        {
            MakeRoute();
            Call(route, "TryEnter", Vector3.zero);
            Set(route, "cursor", -0.00000001f);
            Set(route, "desired", 1f);
            foreach (string property in new[] { "Exposed", "FinalSprint", "CurrentSection", "ThrowEnd" })
                Assert.DoesNotThrow(() => route.GetType().GetProperty(property).GetValue(route), property);
            object[] args = { Vector3.zero, 2f, 5f, .1f, 10d, 0f };
            Assert.That(((Vector3)Call(route, "Advance", args)).x, Is.EqualTo(.5f).Within(.001f));
        }

        [Test] public void StationaryPoleTargetStillLeadsToAnAuthoredEmergenceAnchor()
        {
            MakeRoute(); Call(route, "TryEnter", Vector3.zero);
            Set(route, "cursor", .7f); Set(route, "desired", .7f);
            Call(route, "ApproachEncounter", 1);
            object[] args = { Vector3.right * 7, 2f, 5f, 1f, 10d, 0f };
            Assert.That((Vector3)Call(route, "Advance", args), Is.EqualTo(Vector3.right * 10));
            Assert.That((float)Get(route, "cursor"), Is.EqualTo(1));
        }

        object MakeNavigation(out Transform actor, out BoxCollider area, out Collider floor, out Collider wall)
        {
            root = new GameObject("Pursuer navigation regression");
            root.transform.position = new Vector3(50000, 0, 50000);
            Collider Box(string name, Vector3 position, Vector3 size)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = name;
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = position;
                box.transform.localScale = size;
                return box.GetComponent<Collider>();
            }
            floor = Box("Floor", new Vector3(0, -.3f, 0), new Vector3(24, .6f, 20));
            wall = Box("Blocking scenery", new Vector3(0, 3, 0), new Vector3(2, 6, 8));
            area = new GameObject("Pursuit area").AddComponent<BoxCollider>();
            area.transform.SetParent(root.transform, false);
            area.center = new Vector3(0, 2.5f, 0);
            area.size = new Vector3(24, 5, 20);
            area.isTrigger = true;
            actor = new GameObject("Pursuer").transform;
            actor.SetParent(root.transform, false);
            actor.localPosition = new Vector3(-8, 0, 0);
            var body = actor.gameObject.AddComponent<CapsuleCollider>();
            body.center = Vector3.up * 2.5f;
            body.height = 5;
            body.radius = 1.25f;
            Physics.SyncTransforms();
            return Activator.CreateInstance(TypeOf("EscapePursuerNavigation"));
        }

        [Test] public void PursuerReachesTargetAroundSceneryWithoutCrossingIt()
        {
            var navigation = MakeNavigation(out var actor, out var area, out _, out var wall);
            var body = actor.GetComponent<Collider>();
            Vector3 goal = root.transform.TransformPoint(new Vector3(8, 0, 0));
            for (int i = 0; i < 300 && Vector3.Distance(actor.position, goal) > .1f; i++)
            {
                Vector3 before = actor.position;
                actor.position = (Vector3)Call(navigation, "Step", actor, area, goal, .2f);
                Assert.That(Vector3.Distance(before, actor.position), Is.LessThanOrEqualTo(.21f));
                Assert.That(Physics.ComputePenetration(body, actor.position, actor.rotation,
                    wall, wall.transform.position, wall.transform.rotation, out _, out _), Is.False);
            }
            Assert.That(Vector3.Distance(actor.position, goal), Is.LessThan(.1f));
        }

        [Test] public void CachedDetourStopsWhenItsFloorDisappears()
        {
            var navigation = MakeNavigation(out var actor, out var area, out var floor, out _);
            Vector3 goal = root.transform.TransformPoint(new Vector3(8, 0, 0));
            actor.position = (Vector3)Call(navigation, "Step", actor, area, goal, .2f);
            floor.enabled = false;
            Physics.SyncTransforms();
            Assert.That((Vector3)Call(navigation, "Step", actor, area, goal, .2f), Is.EqualTo(actor.position));
        }

        [Test] public void AlternateReturnLeapRequiresClearFlightAndSupportedEnds()
        {
            MakeNavigation(out var actor, out _, out var floor, out var wall);
            Vector3 end = root.transform.TransformPoint(new Vector3(8, 0, 0));
            var jump = TypeOf("EscapePursuerNavigation").GetMethod("ClearJump", BindingFlags.Static | BindingFlags.Public);
            bool Clear() => (bool)jump.Invoke(null, new object[] { actor, actor.position, end });
            wall.transform.localPosition = new Vector3(0, .5f, 0); wall.transform.localScale = new Vector3(2, 1, 8);
            Physics.SyncTransforms(); Assert.That(Clear(), Is.True, "A low parked obstacle may be cleared by the authored leap.");
            wall.transform.localPosition = new Vector3(0, 8, 0); wall.transform.localScale = new Vector3(2, 1, 8);
            Physics.SyncTransforms(); Assert.That(Clear(), Is.False, "A low ceiling must not be crossed.");
            wall.enabled = false; floor.enabled = false; Physics.SyncTransforms();
            Assert.That(Clear(), Is.False, "Never recover by jumping from or into the abyss.");
        }

        [Test] public void FirstCannonDeckFindsAnAttackPositionForTheHangingExitPole()
        {
            var navigation = MakeNavigation(out var actor, out var area, out var floor, out var wall);
            wall.enabled = false;
            floor.transform.localPosition = new Vector3(27.25f, -.3f, 76);
            floor.transform.localScale = new Vector3(9.5f, .6f, 12);
            area.center = new Vector3(27.25f, 2.5f, 76); area.size = new Vector3(9.5f, 5, 12);
            actor.localPosition = new Vector3(27, 0, 73);
            Physics.SyncTransforms();
            // Real A04A_01_CavernExit neutral feet=(61,-6,75); target the body above them.
            var target = root.transform.TransformPoint(new Vector3(61, -5, 75));
            var find = TypeOf("EscapePursuerNavigation").GetMethod("TryAttackPosition", BindingFlags.Static | BindingFlags.Public);
            object[] args = { actor, area, target, 34f, Vector3.zero, null };
            Assert.That(find.Invoke(null, args), Is.True);
            Vector3 goal = (Vector3)args[4];
            Assert.That(Vector3.Distance(goal + Vector3.up * 3, target), Is.LessThanOrEqualTo(34));
            Assert.That(goal.x, Is.GreaterThan(actor.position.x), "The first landing is too far; approach the east side of the existing deck.");
            for (int i = 0; i < 80 && Vector3.Distance(actor.position, goal) > .1f; i++)
                actor.position = (Vector3)Call(navigation, "Step", actor, area, goal, .2f);
            Assert.That(Vector3.Distance(actor.position, goal), Is.LessThan(.1f));
            floor.enabled = false; Physics.SyncTransforms();
            Assert.That(find.Invoke(null, args), Is.False, "A firing position may never be invented over the abyss.");
        }

        [Test] public void OccludedRiderGetsAVisibleSupportedAttackPositionAroundTheWall()
        {
            var navigation = MakeNavigation(out var actor, out var area, out _, out _);
            var type = TypeOf("EscapePursuerNavigation");
            var sees = type.GetMethod("Sees", BindingFlags.Static | BindingFlags.Public);
            Vector3 target = root.transform.TransformPoint(new Vector3(8, 1, 0));
            Assert.That(sees.Invoke(null, new object[] { actor, actor.position, target }), Is.False);
            object[] args = { actor, area, target, 20f, Vector3.zero, null };
            Assert.That(type.GetMethod("TryAttackPosition").Invoke(null, args), Is.True);
            Vector3 goal = (Vector3)args[4];
            Assert.That(sees.Invoke(null, new object[] { actor, goal, target }), Is.True);
            for (int i = 0; i < 300 && Vector3.Distance(actor.position, goal) > .1f; i++)
                actor.position = (Vector3)Call(navigation, "Step", actor, area, goal, .2f);
            Assert.That(Vector3.Distance(actor.position, goal), Is.LessThan(.1f));
        }
    }
}
