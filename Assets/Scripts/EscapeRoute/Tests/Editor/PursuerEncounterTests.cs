using System;
using System.Collections.Generic;
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
            var arm = new GameObject("Synthetic windup arm").transform;
            arm.SetParent(root.transform, false); arm.localPosition = Vector3.up * 3;
            var tip = new GameObject("Synthetic grip").transform;
            tip.SetParent(arm, false); tip.localPosition = Vector3.up * .45f;
            Set(chase, "grabArm", arm); Set(chase, "grabTip", tip);
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

        [TestCase(0, true)][TestCase(4, true)][TestCase(8, false)][TestCase(128, false)][TestCase(255, false)]
        public void SeveredAttackHandCannotGrabOrThrowFromAnInvisibleSocket(int mask, bool expected)
        {
            var boss = root.AddComponent(TypeOf("EscapePursuerBoss")); Set(chase, "boss", boss);
            var state = Get(boss, "state");
            Set(state, "Phase", Enum.Parse(state.GetType().GetField("Phase").FieldType, "Battle"));
            Set(state, "Mask", (byte)mask); Set(boss, "state", state);
            Assert.That(chase.GetType().GetProperty("CanUseAttackHand", Flags).GetValue(chase), Is.EqualTo(expected));
        }

        [Test] public void StationaryRiderInsideTheSameProgressIntervalCannotHoldAnEncounterForever()
        {
            Set(chase, "exposedAt", 100d); Set(chase, "pressureProgressAt", 100d);
            var should = chase.GetType().GetMethod("ShouldReposition", Flags);
            Assert.That(should.Invoke(chase, new object[] { 101d, false }), Is.False, "Keep the minimum readable encounter.");
            Assert.That(should.Invoke(chase, new object[] { 105d, false }), Is.True,
                "A rider staying at first-pole progress3.0565 never crosses the old untilPoint6.5 threshold.");
        }

        [Test] public void RepositionWaitsForCaptureAndThreatCuesButDoesNotCampForeverWhileMoving()
        {
            Set(chase, "exposedAt", 100d); Set(chase, "pressureProgressAt", 120d);
            var should = chase.GetType().GetMethod("ShouldReposition", Flags);
            Assert.That(should.Invoke(chase, new object[] { 120d, false }), Is.True, "Maximum exposure remains bounded even after movement.");
            var attack = Get(chase, "attack");
            Set(attack, "Phase", Enum.Parse(attack.GetType().GetField("Phase").FieldType, "Grab")); Set(chase, "attack", attack);
            Assert.That(should.Invoke(chase, new object[] { 120d, true }), Is.False, "Finish the committed capture before physically returning.");
            Set(attack, "Phase", Enum.Parse(attack.GetType().GetField("Phase").FieldType, "None")); Set(chase, "attack", attack);
            var threat = Get(chase, "threat");
            Set(threat, "Phase", Enum.Parse(threat.GetType().GetField("Phase").FieldType, "DebrisFlight")); Set(chase, "threat", threat);
            Assert.That(should.Invoke(chase, new object[] { 120d, true }), Is.False, "Do not interrupt an in-flight attack cue.");
        }

        Vector3 TwoPressureBays()
        {
            Vector3 target = new(50000, 1000, 50000);
            var encounterType = chase.GetType().GetNestedType("PhysicalEncounter");
            var encounters = Array.CreateInstance(encounterType, 2);
            for (int i = 0; i < 2; i++)
            {
                var bay = new GameObject("Pressure bay " + i);
                bay.transform.SetParent(root.transform);
                bay.transform.position = target + new Vector3(i * 24, 2.5f, 0);
                var platform = bay.AddComponent<BoxCollider>(); platform.size = new Vector3(10, 5, 10);
                platform.isTrigger = true;
                var encounter = Activator.CreateInstance(encounterType);
                Set(encounter, "ingress", bay.transform); Set(encounter, "landing", bay.transform);
                Set(encounter, "platform", platform); Set(encounter, "routePoint", 1);
                encounters.SetValue(encounter, i);
            }
            Set(chase, "encounters", encounters);
            return target;
        }

        [Test] public void CooldownExpiryCannotCancelAnInterceptWhileThePoleRiderStaysPut()
        {
            Vector3 target = TwoPressureBays();
            var visited = (Dictionary<int, double>)Get(chase, "visitedEncounters");
            visited[0] = 103.5d;
            var choose = chase.GetType().GetMethod("ChoosePressureEncounter", Flags);
            var score = chase.GetType().GetMethod("SelectPressureEncounter", Flags);
            choose.Invoke(chase, new object[] { 7UL, target, 3.0565f, 100d });
            Assert.That(Get(chase, "pressureEncounter"), Is.EqualTo(1), "Leave the recently unusable bay for another authored intercept.");
            Assert.That(score.Invoke(chase, new object[] { target, 3.0565f, 104d }), Is.EqualTo(0),
                "The old bay becomes superficially cheaper as soon as its cooldown expires.");
            choose.Invoke(chase, new object[] { 7UL, target, 3.0565f, 104d });
            choose.Invoke(chase, new object[] { 7UL, target, 3.2f, 120d });
            Assert.That(Get(chase, "pressureEncounter"), Is.EqualTo(1),
                "Complete the chosen physical route instead of repeatedly turning around before the next bay.");
        }

        [TestCase(8UL, 3.0565f)]
        [TestCase(7UL, 4.2f)]
        public void AChangedRunnerOrRouteIntervalCanReplaceTheCommittedIntercept(ulong actor, float progress)
        {
            Vector3 target = TwoPressureBays();
            Set(chase, "pressureEncounter", 1); Set(chase, "pressureActor", 7UL); Set(chase, "pressureTargetProgress", 3.0565f);
            chase.GetType().GetMethod("ChoosePressureEncounter", Flags).Invoke(chase, new object[] { actor, target, progress, 120d });
            Assert.That(Get(chase, "pressureEncounter"), Is.EqualTo(0));
            Assert.That(Get(chase, "pressureActor"), Is.EqualTo(actor));
        }

        [Test] public void ReachableWireAfterTargetIsNotOutsideTheAheadOfRouteBay()
        {
            TwoPressureBays();
            var bay = ((Array)Get(chase, "encounters")).GetValue(0);
            var platform = (BoxCollider)Get(bay, "platform");
            platform.transform.position = new Vector3(95.55f, -2.5f, 26); platform.size = new Vector3(4.7f, 5, 12);
            Set(bay, "routePoint", 23);
            Vector3 feet = new(87, .05f, 22); // Logged player progress20.519 was below the old index threshold21.5.
            var reachable = chase.GetType().GetMethod("WithinEncounterReach", Flags);
            Assert.That(reachable.Invoke(chase, new object[] { bay, feet, 22f }), Is.True);
            Set(chase, "exposedAt", 100d); Set(chase, "pressureProgressAt", 103d);
            Assert.That(chase.GetType().GetMethod("ShouldReposition", Flags).Invoke(chase, new object[] { 104d, false }), Is.False);
            Assert.That(reachable.Invoke(chase, new object[] { bay, feet + Vector3.left * 50, 22f }), Is.False,
                "Actually leaving the platform's attack reach still permits early relocation.");
        }

        [Test] public void CooldownCannotPreferAnUnreachableBayOverAVisibleSidePositionAroundAPillar()
        {
            Vector3 origin = TwoPressureBays();
            var bays = (Array)Get(chase, "encounters");
            var first = (BoxCollider)Get(bays.GetValue(0), "platform");
            var second = (BoxCollider)Get(bays.GetValue(1), "platform");
            second.transform.position = origin + new Vector3(36.4f, 2.5f, 0);
            var floors = new GameObject[2]; GameObject pillar = null;
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var area = i == 0 ? first : second;
                    floors[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floors[i].transform.position = area.transform.position - Vector3.up * 2.8f;
                    floors[i].transform.localScale = new Vector3(10, .6f, 10);
                }
                pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.transform.position = origin + new Vector3(6, 2.5f, 0);
                pillar.transform.localScale = new Vector3(.5f, 5, .7f);
                Physics.SyncTransforms();
                Vector3 feet = origin + Vector3.right * 10;
                var navigation = TypeOf("EscapePursuerNavigation");
                var near = (Vector3)navigation.GetMethod("Clamp").Invoke(null, new object[] { first, feet });
                Assert.That(navigation.GetMethod("Sees").Invoke(null, new object[] { chase.transform, near, feet + Vector3.up }), Is.False);
                object[] search = { chase.transform, first, feet + Vector3.up, 22f, Vector3.zero, null };
                Assert.That(navigation.GetMethod("TryAttackPosition").Invoke(null, search), Is.True,
                    "A supported side position sees the target even though the single clamped point cannot.");
                ((Dictionary<int, double>)Get(chase, "visitedEncounters"))[0] = 103.5d;
                Assert.That(chase.GetType().GetMethod("SelectPressureEncounter", Flags).Invoke(chase,
                    new object[] { feet, 20.51935f, 100d }), Is.EqualTo(0),
                    "A short revisit cooldown cannot force a long retreat to the out-of-range platform.");
            }
            finally
            {
                foreach (var floor in floors) if (floor) Object.DestroyImmediate(floor);
                if (pillar) Object.DestroyImmediate(pillar);
            }
        }

        [Test] public void ForwardExitBayCanPressureAPlayerBehindItsRoutePoint()
        {
            Vector3 origin = TwoPressureBays();
            var bay = ((Array)Get(chase, "encounters")).GetValue(0);
            ((BoxCollider)Get(bay, "platform")).transform.position = new Vector3(9000, 902.5f, 9000);
            Vector3 feet = new(9000, 900, 9000);
            Set(bay, "continueOutside", true);
            var routePoints = (Array)Get(route, "points");
            var extended = Array.CreateInstance(routePoints.GetType().GetElementType(), 3);
            Array.Copy(routePoints, extended, routePoints.Length);
            extended.SetValue(Activator.CreateInstance(routePoints.GetType().GetElementType()), 2);
            Set(route, "points", extended);
            Set(bay, "routePoint", 2);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.transform.position = feet - Vector3.up * .3f;
                floor.transform.localScale = new Vector3(10, .6f, 10);
                Physics.SyncTransforms();
                var select = chase.GetType().GetMethod("SelectPressureEncounter", Flags);
                Assert.That(select.Invoke(chase, new object[] { feet, .5f, 100d }), Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(floor); }
        }

        [Test] public void ProjectilePressureDelaysIdleRelocationButNotTheMaximumEncounterDuration()
        {
            Set(chase, "encounterIndex", 0); Set(chase, "encounterPhase", 6);
            Set(chase, "exposedAt", 100d); Set(chase, "pressureProgressAt", 100d);
            var record = chase.GetType().GetMethod("RecordEncounterPressure", Flags);
            var relocate = chase.GetType().GetMethod("ShouldReposition", Flags);
            record.Invoke(chase, new object[] { 104d }); // The warning, flight and impact ended here.
            Assert.That(relocate.Invoke(chase, new object[] { 107d, false }), Is.False);
            Assert.That(relocate.Invoke(chase, new object[] { 108.1d, false }), Is.True);
            record.Invoke(chase, new object[] { 112d });
            Assert.That(relocate.Invoke(chase, new object[] { 112d, false }), Is.True);
            Set(chase, "encounterPhase", 9); record.Invoke(chase, new object[] { 120d });
            Assert.That(Get(chase, "pressureProgressAt"), Is.EqualTo(112d), "Hidden return travel is not a visible attack.");
        }
    }
}
