using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerFinaleHandoffTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject root;
        Component route, chase, timer, runner;
        object encounter;
        IList players;
        object[] previousPlayers;
        readonly List<(PropertyInfo property, object value)> singletons = new();
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name.Contains('.') ? name : "CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static object Get(object o, string name) => o.GetType().GetField(name, F)?.GetValue(o)
            ?? o.GetType().GetProperty(name, F)?.GetValue(o);
        static void Set(object o, string name, object value)
        {
            var property = o.GetType().GetProperty(name, F);
            if (property != null) property.SetValue(o, value);
            else o.GetType().GetField(name, F).SetValue(o, value);
        }
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
        void Current(Type type, object value)
        {
            var property = type.GetProperty("Current", S);
            singletons.Add((property, property.GetValue(null)));
            property.SetValue(null, value);
        }
        Transform Anchor(string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false); t.localPosition = position;
            return t;
        }
        Component Player(ulong id, float x)
        {
            var go = Anchor("Runner " + id, Vector3.right * x).gameObject;
            var p = go.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            Set(p, "IsSpawned", true); Set(p, "OwnerClientId", id);
            var mover = go.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(mover, "controller", go.GetComponent<CharacterController>());
            players.Add(p);
            ((HashSet<ulong>)Get(route, "departed")).Add(id);
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[id] = Mathf.Clamp(x / 10, 0, 73);
            return p;
        }

        [SetUp] public void Setup()
        {
            root = new GameObject("Finale spatial handoff regression"); root.SetActive(false);
            root.transform.position = new Vector3(61000, 1000, 61000);
            Current(T("CurtainCall.HumanCannon.HumanCannonPoleSystem"), null);
            Current(T("EscapePursuerBoss"), null);
            timer = root.AddComponent(T("EscapeRouteTimer"));
            Set(timer, "startedAt", 0d); Set(timer, "synchronized", true); Current(timer.GetType(), timer);
            route = root.AddComponent(T("EscapePursuerRoute"));
            var pointType = route.GetType().GetNestedType("Point");
            var points = Array.CreateInstance(pointType, 74);
            for (int i = 0; i < points.Length; i++)
            {
                var point = Activator.CreateInstance(pointType);
                var feet = Anchor("Route " + i, Vector3.right * (10 * i));
                Set(point, "feet", feet); Set(point, "playerPath", feet); Set(point, "throwEnd", feet);
                Set(point, "exposed", i >= 71); Set(point, "jump", i == 71);
                points.SetValue(point, i);
            }
            Set(route, "points", points); Call(route, "SynchronizeRound"); Call(route, "ResumeAt", 70);
            chase = Anchor("Dormant pursuer", Vector3.right * 710).gameObject.AddComponent(T("EscapeUnderstageChase"));
            Set(chase, "fullRoute", route); Set(chase, "body", chase.GetComponent<CapsuleCollider>());
            Set(chase, "grabArm", Anchor("Windup arm", new Vector3(710, 3, 0)));
            Set(chase, "grabTip", Anchor("Hand", new Vector3(710, 3.45f, 0)));
            Set(chase, "encounterRound", 0d); Set(chase, "encounterIndex", 0); Set(chase, "encounterPhase", 6);
            var eType = chase.GetType().GetNestedType("PhysicalEncounter"); encounter = Activator.CreateInstance(eType);
            Set(encounter, "routePoint", 71); Set(encounter, "untilPoint", 73); Set(encounter, "continueOutside", true);
            var platform = Anchor("Existing final corridor", new Vector3(710, 2.5f, 0)).gameObject.AddComponent<BoxCollider>();
            platform.size = new Vector3(60, 5, 14); platform.isTrigger = true;
            Set(encounter, "platform", platform);
            Set(encounter, "landing", Anchor("Landing", Vector3.right * 710));
            Set(encounter, "exit", Anchor("Broken wall exit", new Vector3(700, 0, 20)));
            Set(encounter, "ingress", Anchor("Hidden ingress", new Vector3(700, 0, 30)));
            var onward = Activator.CreateInstance(eType);
            Set(onward, "routePoint", 73); Set(onward, "continueOutside", true);
            Set(onward, "ingress", Anchor("Onward ingress", new Vector3(730, 0, 30)));
            Set(onward, "landing", Anchor("Onward landing", Vector3.right * 900));
            var onwardPlatform = Anchor("Onward platform", new Vector3(900, 2.5f, 0)).gameObject.AddComponent<BoxCollider>();
            onwardPlatform.size = new Vector3(30, 5, 14); onwardPlatform.isTrigger = true;
            Set(onward, "platform", onwardPlatform);
            var encounters = Array.CreateInstance(eType, 2); encounters.SetValue(encounter, 0); encounters.SetValue(onward, 1);
            Set(chase, "encounters", encounters);
            players = (IList)T("CurtainCall.Network.PlayerSync.NetworkPlayer").GetField("all", S).GetValue(null);
            previousPlayers = players.Cast<object>().ToArray(); players.Clear();
            runner = Player(50, 690); // Inside the deck, but below the old progress >= 70 condition.
        }

        [TearDown] public void Cleanup()
        {
            foreach (Component player in players) Set(player, "IsSpawned", false);
            if (root) Object.DestroyImmediate(root);
            players.Clear(); foreach (var player in previousPlayers) players.Add(player);
            foreach (var saved in singletons.AsEnumerable().Reverse()) saved.property.SetValue(null, saved.value);
            singletons.Clear();
        }

        [TestCase(0f)] [TestCase(47f)] [TestCase(180f)]
        public void RunnerAlreadyOnTheCorridorKeepsThePursuerOutsideDespiteEarlierProgress(float rotation)
        {
            root.transform.rotation = Quaternion.Euler(0, rotation, 0);
            var before = chase.transform.position;
            Assert.That(Call(chase, "AdvanceEncounter", runner, false), Is.True);
            Assert.That(Get(chase, "encounterIndex"), Is.EqualTo(-1));
            Assert.That(Get(chase, "outsideEncounter"), Is.EqualTo(0));
            Assert.That(Get(route, "Progress"), Is.EqualTo(71f));
            Assert.That(chase.transform.position, Is.EqualTo(before), "Handoff changes ownership, never the actor's position.");
            for (int i = 0; i < 5; i++)
            {
                Call(route, "SelectTarget");
                Assert.That(Call(chase, "AdvanceEncounter", runner, false), Is.False);
                Assert.That(Get(route, "desired"), Is.EqualTo(71f), "Do not reverse-jump into the hidden route to match progress 69.");
                Assert.That(Get(chase, "outsideEncounter"), Is.EqualTo(0));
                Assert.That(Get(chase, "jumping"), Is.False);
                Assert.That(Call(route, "TryJump", Vector3.zero, Vector3.zero), Is.False);
                Assert.That(Get(chase, "CanAttackFromRoute"), Is.True);
            }
        }

        [Test] public void RunnerActuallyLeavingTheCorridorCanStillBeFollowedBackThroughTheBreach()
        {
            Call(chase, "AdvanceEncounter", runner, false);
            runner.transform.localPosition = Vector3.right * 650;
            Call(route, "SelectTarget"); Call(chase, "AdvanceEncounter", runner, false);
            Assert.That(Get(chase, "outsideEncounter"), Is.EqualTo(-1));
            Assert.That(Get(chase, "encounterIndex"), Is.EqualTo(0));
            Assert.That(Get(chase, "encounterPhase"), Is.EqualTo(8));
            Assert.That(Get(chase, "jumping"), Is.True, "Physical return jump is retained after leaving the real platform.");
        }

        [TestCase(0, 12)] [TestCase(1, 11)] [TestCase(2, 10)] [TestCase(3, 9)] [TestCase(4, 9)]
        public void EmptyOutdatedBayRetracesOnlyTheApproachAlreadyWalked(int phase, int returnPhase)
        {
            Set(encounter, "continueOutside", false);
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            Set(chase, "encounterPhase", phase);
            var before = chase.transform.position;
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.True);
            Assert.That(Get(chase, "encounterPhase"), Is.EqualTo(returnPhase));
            Assert.That(chase.transform.position, Is.EqualTo(before));
            Assert.That(Get(chase, "jumping"), Is.False);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ApproachIsKeptWhenAnyLivingTeammateCanStillBePressured(int count)
        {
            Set(encounter, "continueOutside", false);
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            for (int i = 0; i < count; i++) Player((ulong)i, 700 + i);
            Set(chase, "encounterPhase", 3);
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.False);
            Assert.That(Get(chase, "encounterPhase"), Is.EqualTo(3));
        }

        [TestCase(5)] [TestCase(6)] [TestCase(8)]
        public void CommittedJumpOrOutdoorEncounterIsNeverCancelledByApproachRetargeting(int phase)
        {
            Set(encounter, "continueOutside", false);
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            Set(chase, "encounterPhase", phase);
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.False);
            Assert.That(Get(chase, "encounterPhase"), Is.EqualTo(phase));
        }

        [Test] public void FinalContinuationCannotBeSkippedWhenTheRunnerAlreadyReachedTheExit()
        {
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            Set(chase, "encounterPhase", 3);
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.False);
            Assert.That(Get(chase, "encounterPhase"), Is.EqualTo(3));
        }

        [Test] public void NoAlternativeBayDoesNotCauseAnImmediateApproachReturnLoop()
        {
            Set(encounter, "continueOutside", false);
            var only = Array.CreateInstance(encounter.GetType(), 1); only.SetValue(encounter, 0); Set(chase, "encounters", only);
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            Set(chase, "encounterPhase", 3);
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.False);
        }

        [TestCase("Fallen", true)] [TestCase("Retired", true)] [TestCase("Arrived", true)]
        [TestCase("EscapeCaptured", true)] [TestCase("Normal", false)]
        public void UnavailableTeammateDoesNotHoldAnObsoleteApproach(string stateName, bool spawned)
        {
            Set(encounter, "continueOutside", false);
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            var teammate = Player(2, 700);
            var state = Get(teammate, "state");
            Set(state, "m_InternalValue", Enum.Parse(Get(state, "Value").GetType(), stateName));
            Set(teammate, "IsSpawned", spawned); Set(chase, "encounterPhase", 3);
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.True);
        }

        [Test] public void WallStrikeFinishesBeforeAnOutdatedApproachCanReturn()
        {
            Set(encounter, "continueOutside", false);
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            Set(chase, "encounterPhase", 3);
            var walls = root.AddComponent(T("EscapePursuerWalls"));
            var gateType = walls.GetType().GetNestedType("Gate");
            var gate = Activator.CreateInstance(gateType); Set(gate, "started", 9.9d);
            var gates = Array.CreateInstance(gateType, 1); gates.SetValue(gate, 0);
            Set(walls, "gates", gates); Set(chase, "walls", walls);
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.False);
            Assert.That(Get(chase, "encounterPhase"), Is.EqualTo(3));
            Assert.That(Get(gate, "started"), Is.EqualTo(9.9d));
        }

        [TestCase("Warning")] [TestCase("Strike")] [TestCase("Grab")] [TestCase("Throw")]
        public void ExistingAttackCannotBeInterruptedByApproachRetargeting(string phase)
        {
            Set(encounter, "continueOutside", false);
            runner.transform.localPosition = Vector3.right * 900;
            ((Dictionary<ulong, float>)Get(route, "trustedProgress"))[50] = 73;
            Set(chase, "encounterPhase", 3);
            var attack = Get(chase, "attack");
            Set(attack, "Phase", Enum.Parse(Get(attack, "Phase").GetType(), phase)); Set(chase, "attack", attack);
            Assert.That(Call(chase, "ReturnFromStaleApproach", encounter, runner, 10d), Is.False);
            Assert.That(Get(attack, "Phase").ToString(), Is.EqualTo(phase));
        }
    }
}
