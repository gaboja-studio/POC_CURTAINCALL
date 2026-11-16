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
    public sealed class PursuerRouteTargetTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Vector3 Origin = new(53000, 1000, 53000);
        readonly List<GameObject> roots = new();
        readonly List<Component> actors = new();
        readonly List<(PropertyInfo property, object value)> saved = new();
        IList players;
        object[] previousPlayers;
        Component route, timer;
        BoxCollider hall;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target)
            ?? target.GetType().GetField(name, Flags)?.GetValue(target);
        static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Flags);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, Flags).SetValue(target, value);
        }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        void Isolate(string typeName)
        {
            var property = T(typeName).GetProperty("Current", Static);
            saved.Add((property, property.GetValue(null))); property.SetValue(null, null);
        }
        GameObject Root(string name, bool active = false)
        {
            var root = new GameObject(name); root.SetActive(active); root.transform.position = Origin;
            roots.Add(root); return root;
        }
        Transform Anchor(Vector3 local)
        {
            var anchor = new GameObject("Authored route anchor").transform;
            anchor.SetParent(route.transform, false); anchor.localPosition = local; return anchor;
        }
        object Guide(Vector3 from, Vector3 to, float first, float last)
        {
            var row = Activator.CreateInstance(route.GetType().GetNestedType("PlayerGuide"));
            Set(row, "from", Anchor(from)); Set(row, "to", Anchor(to));
            Set(row, "fromProgress", first); Set(row, "toProgress", last); return row;
        }
        Component Player(ulong id, Vector3 local)
        {
            var actor = Root("Participant " + id); actor.transform.position += local;
            var player = actor.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            Set(player, "IsSpawned", true); Set(player, "IsOwner", false); Set(player, "OwnerClientId", id);
            actors.Add(player); players.Add(player); return player;
        }
        void Airborne(Component player, bool value)
        {
            var jump = Get(player, "jump"); var type = Get(jump, "Value").GetType();
            string name = value ? Enum.GetNames(type).First(n => n != "None") : "None";
            Set(jump, "m_InternalValue", Enum.Parse(type, name));
        }
        float Progress(Component player, out bool inHall)
        {
            object[] args = { player, false }; float progress = (float)Call(route, "PlayerProgress", args);
            inHall = (bool)args[1]; return progress;
        }
        double Now => (double)Get(timer, "Clock");
        double NeglectDeadline
        {
            get
            {
                var tuning = route.GetType().GetProperty("Tuning", Static).GetValue(null);
                return (float)Get(tuning, "pressureIdleGrace") + (float)Get(tuning, "pressureRiseSeconds");
            }
        }
        IDictionary Watches => (IDictionary)Get(route, "progressWatch");
        ulong Id(Component player) => (ulong)Get(player, "OwnerClientId");
        void Sample()
        {
            Set(route, "nextProgressSample", double.NegativeInfinity);
            Call(route, "SampleProgress", Now);
        }
        void Age(Component player, double seconds)
        {
            Sample(); var watch = Watches[Id(player)];
            Set(watch, "AdvancedAt", Now - seconds); Watches[Id(player)] = watch;
        }
        double AdvancedAt(Component player) => (double)Get(Watches[Id(player)], "AdvancedAt");
        Component RefreshTarget()
        {
            Set(route, "nextFocus", double.NegativeInfinity);
            return (Component)Call(route, "SelectTarget");
        }
        void State(Component player, string value)
        {
            var state = Get(player, "state");
            Set(state, "m_InternalValue", Enum.Parse(Get(state, "Value").GetType(), value));
        }
        void ExtendToFinaleProgress()
        {
            var pointType = route.GetType().GetNestedType("Point"); var points = Array.CreateInstance(pointType, 74);
            for (int i = 0; i < points.Length; i++)
            {
                var point = Activator.CreateInstance(pointType); var anchor = Anchor(Vector3.right * (i * 10));
                Set(point, "feet", anchor); Set(point, "playerPath", anchor); Set(point, "throwEnd", anchor); points.SetValue(point, i);
            }
            Set(route, "points", points);
            var guides = (Array)Get(route, "playerGuides");
            guides.SetValue(Guide(new Vector3(80, 0, 0), new Vector3(90, 0, 0), 57, 58), 0);
        }

        [SetUp]
        public void Setup()
        {
            Isolate("CurtainCall.EscapeRoute.EscapeRouteTimer");
            Isolate("CurtainCall.EscapeRoute.EscapePursuerBoss");
            Isolate("CurtainCall.HumanCannon.HumanCannonPoleSystem");
            players = (IList)T("CurtainCall.Network.PlayerSync.NetworkPlayer").GetField("all", Static).GetValue(null);
            previousPlayers = players.Cast<object>().ToArray(); players.Clear();
            timer = Root("Offline encounter clock").AddComponent(T("CurtainCall.EscapeRoute.EscapeRouteTimer"));
            Assert.That(Call(timer, "ApplyState", 1u, 0, 0, 0d), Is.True);
            timer.GetType().GetProperty("Current", Static).SetValue(null, timer);
            hall = Root("Actual entrance volume", true).AddComponent<BoxCollider>(); hall.isTrigger = true;
            hall.transform.position = Origin + new Vector3(500, 43.33f, 0); hall.size = Vector3.one * 4;
            Set(timer, "startArea", hall);
            route = Root("Route with vertically separated player guides").AddComponent(T("CurtainCall.EscapeRoute.EscapePursuerRoute"));
            var pointType = route.GetType().GetNestedType("Point"); var points = Array.CreateInstance(pointType, 13);
            for (int i = 0; i < points.Length; i++)
            {
                var point = Activator.CreateInstance(pointType); var anchor = Anchor(Vector3.right * (i * 10));
                Set(point, "feet", anchor); Set(point, "playerPath", anchor); Set(point, "throwEnd", anchor);
                points.SetValue(point, i);
            }
            Set(route, "points", points);
            var guides = Array.CreateInstance(route.GetType().GetNestedType("PlayerGuide"), 2);
            guides.SetValue(Guide(new Vector3(80, 0, 0), new Vector3(90, 0, 0), 8, 9), 0);
            guides.SetValue(Guide(new Vector3(490, 0, 0), new Vector3(500, 0, 0), 10, 11), 1);
            Set(route, "playerGuides", guides); Call(route, "SynchronizeRound");
            Assert.That(Call(route, "TryEnter", Origin), Is.True);
        }

        [TearDown]
        public void Cleanup()
        {
            try
            {
                foreach (var actor in actors) if (actor) Set(actor, "IsSpawned", false);
                for (int i = roots.Count - 1; i >= 0; i--) if (roots[i]) Object.DestroyImmediate(roots[i]);
            }
            finally
            {
                if (players != null && previousPlayers != null)
                { players.Clear(); foreach (var player in previousPlayers) players.Add(player); }
                foreach (var item in saved) item.property.SetValue(null, item.value);
                roots.Clear(); actors.Clear(); saved.Clear();
            }
        }

        [Test]
        public void DistantEntrantCannotOutrankTheActualProgressNineRunner()
        {
            var lead = Player(0, new Vector3(90, 0, 1.73f));
            var entrant = Player(2, new Vector3(500, 43.33f, 0));
            object[] raw = { entrant.transform.position, 0f };
            Assert.That(Call(route, "Project", raw), Is.EqualTo(11f));
            Assert.That((float)raw[1], Is.EqualTo(43.33f).Within(.01f));
            raw[0] = lead.transform.position;
            Assert.That(Call(route, "Project", raw), Is.EqualTo(9f));
            Assert.That((float)raw[1], Is.EqualTo(1.73f).Within(.01f));
            Set(route, "focus", entrant); Set(route, "focusInHall", false); Set(route, "nextFocus", double.MaxValue);
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(lead));
            Assert.That(Get(route, "desired"), Is.EqualTo(9f));
            Assert.That(Progress(entrant, out bool inHall), Is.Zero); Assert.That(inHall, Is.True);
        }

        [Test]
        public void CachedRouteFocusIsRecheckedWhenThatPlayerReturnsToTheEntrance()
        {
            var first = Player(0, new Vector3(90, 0, 0)); var second = Player(2, new Vector3(80, 0, 0));
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(first));
            Set(route, "nextFocus", double.MaxValue);
            first.transform.position = hall.transform.position;
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(second));
            Assert.That(Get(route, "desired"), Is.EqualTo(8f));
        }

        [Test]
        public void AirborneRouteRunnerKeepsLastTrustedProgressAcrossDistantGuidesAndEntranceVolume()
        {
            var runner = Player(0, new Vector3(90, 0, 0));
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(runner));
            Airborne(runner, true); runner.transform.position = hall.transform.position;
            Assert.That(Progress(runner, out bool inHall), Is.EqualTo(9f)); Assert.That(inHall, Is.False);
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(runner));
            Assert.That(Get(route, "desired"), Is.EqualTo(9f));
            Airborne(runner, false);
            Assert.That(Progress(runner, out inHall), Is.Zero); Assert.That(inHall, Is.True);
        }

        [Test]
        public void TrustedProgressCanDecreaseWhenTheRunnerBacktracks()
        {
            var runner = Player(0, new Vector3(90, 0, 0));
            Assert.That(Progress(runner, out _), Is.EqualTo(9f));
            runner.transform.position = Origin + new Vector3(82, 0, 0);
            Assert.That(Progress(runner, out bool inHall), Is.EqualTo(8.2f).Within(.0001f));
            Assert.That(inHall, Is.False);
        }

        [Test]
        public void RemainingEntrantsRequestPhysicalReturnInsteadOfLeavingNoRouteTarget()
        {
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Set(route, "cursor", 5f);
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(entrant));
            Assert.That(Get(route, "TargetInHall"), Is.True); Assert.That(Get(route, "TeamNeedsPursuit"), Is.False);
            Assert.That(Get(route, "ReturningToHall"), Is.True); Assert.That(Get(route, "desired"), Is.Zero);
            object[] move = { Origin + Vector3.right * 50, 2f, 5f, 1f, 10d, 0f };
            Assert.That((Vector3)Call(route, "Advance", move), Is.EqualTo(Origin + Vector3.right * 45));
            Assert.That(Call(route, "TryLeaveAtEntry", Origin, true), Is.False, "The cursor has not returned along the route.");
        }

        [Test]
        public void EntryHandoffRequiresPhysicalArrivalAndConnectedMeshAndPreservesRoundForReentry()
        {
            var runner = Player(0, new Vector3(90, 0, 0)); Progress(runner, out _);
            var departed = (HashSet<ulong>)Get(route, "departed");
            var pressured = (Dictionary<ulong, double>)Get(route, "pressured"); pressured[0] = 12d;
            runner.transform.position = hall.transform.position; Call(route, "SelectTarget");
            Set(route, "cursor", 0f);
            Assert.That(Call(route, "TryLeaveAtEntry", Origin + Vector3.right * 2, true), Is.False);
            Assert.That(Call(route, "TryLeaveAtEntry", Origin, false), Is.False);
            Assert.That(Call(route, "TryLeaveAtEntry", Origin, true), Is.True);
            Assert.That(Get(route, "Following"), Is.False);
            Assert.That(departed.Contains(0), Is.True); Assert.That(pressured[0], Is.EqualTo(12d));
            Assert.That(Get(route, "epoch"), Is.EqualTo(0d));
            runner.transform.position = hall.transform.position + Vector3.forward * 10;
            Assert.That(Progress(runner, out bool inHall), Is.Zero); Assert.That(inHall, Is.True,
                "Historical departure must not force service re-entry while still crossing the hall.");
            runner.transform.position = Origin + Vector3.right * 90;
            Assert.That(Progress(runner, out inHall), Is.EqualTo(9f)); Assert.That(inHall, Is.False);
            Assert.That(Call(route, "TryEnter", Origin), Is.True);
        }

        [Test]
        public void NoSurvivingRouteCandidateStillSetsAReverseGoalInsteadOfKeepingTheOldBay()
        {
            var runner = Player(0, new Vector3(90, 0, 0)); Call(route, "SelectTarget");
            Set(route, "cursor", 5f);
            var state = Get(runner, "state"); Set(state, "m_InternalValue", Enum.Parse(Get(state, "Value").GetType(), "Retired"));
            Assert.That(Call(route, "SelectTarget"), Is.Null);
            Assert.That(Get(route, "ReturningToHall"), Is.True); Assert.That(Get(route, "desired"), Is.Zero);
        }

        [Test]
        public void FinaleRearPriorityCanStillSelectTheLivingEntrantForHallReturn()
        {
            Player(0, new Vector3(90, 0, 0)); var entrant = Player(2, new Vector3(500, 43.33f, 0));
            var boss = Root("Dormant finale selection").AddComponent(T("CurtainCall.EscapeRoute.EscapePursuerBoss"));
            var arena = Root("Finale far beyond both runners", true).AddComponent<BoxCollider>();
            arena.transform.position = Origin + Vector3.right * 1000; arena.size = Vector3.one * 10;
            Set(boss, "route", route); Set(boss, "arena", arena);
            var state = Get(boss, "state"); Set(state, "Round", 0d);
            Set(state, "Phase", Enum.Parse(Get(state, "Phase").GetType(), "Chasing")); Set(boss, "state", state);
            boss.GetType().GetProperty("Current", Static).SetValue(null, boss);
            Assert.That(Get(boss, "HasRunnerBehindFinale"), Is.True);
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(entrant));
            Assert.That(Get(route, "ReturningToHall"), Is.True); Assert.That(Get(route, "TeamNeedsPursuit"), Is.False);
        }

        [TestCase("Grab")]
        [TestCase("Throw")]
        public void HallReturnCannotInterruptAnExistingCapture(string phase)
        {
            Player(2, new Vector3(500, 43.33f, 0)); Call(route, "SelectTarget");
            var chase = Root("Dormant capture director").AddComponent(T("CurtainCall.EscapeRoute.EscapeUnderstageChase"));
            Set(chase, "fullRoute", route);
            Assert.That(Get(chase, "CanReturnToHall"), Is.True);
            var attack = Get(chase, "attack"); Set(attack, "Phase", Enum.Parse(Get(attack, "Phase").GetType(), phase));
            Set(chase, "attack", attack);
            Assert.That(Get(chase, "CanReturnToHall"), Is.False);
        }

        [Test]
        public void EntrantGetsOneZeroProgressWatchDespiteDistantFalseProjection()
        {
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Sample();
            double first = AdvancedAt(entrant);
            Assert.That(Get(Watches[Id(entrant)], "Furthest"), Is.EqualTo(0f));
            Age(entrant, 60); double aged = AdvancedAt(entrant);
            for (int i = 0; i < 4; i++) { Sample(); RefreshTarget(); }
            Assert.That(AdvancedAt(entrant), Is.EqualTo(aged));
            Assert.That(aged, Is.LessThan(first));
            Assert.That(Progress(entrant, out bool inHall), Is.Zero); Assert.That(inHall, Is.True);
            Assert.That((float)Call(route, "Escalation", entrant, aged + NeglectDeadline), Is.EqualTo(1f));
        }

        [Test]
        public void ActiveLeadKeepsPriorityBeforeTheExistingNeglectDeadline()
        {
            ExtendToFinaleProgress(); var lead = Player(0, new Vector3(90, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Age(entrant, NeglectDeadline - 2);
            Assert.That(RefreshTarget(), Is.SameAs(lead));
            Assert.That(Progress(lead, out _), Is.EqualTo(58f));
        }

        [Test]
        public void NeglectedEntrantOutranksProgressFiftyEightBeforeBossChasing()
        {
            ExtendToFinaleProgress(); var lead = Player(0, new Vector3(90, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Age(entrant, NeglectDeadline + 2);
            Assert.That(Progress(lead, out _), Is.EqualTo(58f));
            Assert.That(RefreshTarget(), Is.SameAs(entrant));
            Assert.That(Get(route, "desired"), Is.EqualTo(0f));
            Assert.That(Get(route, "ReturningToHall"), Is.True);
        }

        [Test]
        public void NeglectedHallTargetStaysSelectedWhilePhysicallyReversingTheServiceRoute()
        {
            ExtendToFinaleProgress(); Player(0, new Vector3(90, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Age(entrant, NeglectDeadline + 20);
            double observed = AdvancedAt(entrant); Set(route, "cursor", 58f);
            for (int i = 0; i < 5; i++) Assert.That(RefreshTarget(), Is.SameAs(entrant));
            object[] move = { Origin + Vector3.right * 580, 2f, 5f, 1f, Now, 0f };
            Assert.That((Vector3)Call(route, "Advance", move), Is.EqualTo(Origin + Vector3.right * 575));
            Assert.That(AdvancedAt(entrant), Is.EqualTo(observed));
            Assert.That(((IDictionary)Get(route, "pressured")).Contains(Id(entrant)), Is.False);
            Assert.That(Call(route, "TryLeaveAtEntry", Origin, true), Is.False);
            Assert.That(RefreshTarget(), Is.SameAs(entrant));
        }

        [Test]
        public void OldestNeglectedRunnerWinsAndOriginalProgressBreaksEqualAge()
        {
            var lead = Player(0, new Vector3(90, 0, 0)); var rear = Player(1, new Vector3(80, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0));
            Age(lead, NeglectDeadline + 5); Age(rear, NeglectDeadline + 15); Age(entrant, NeglectDeadline + 25);
            Assert.That(RefreshTarget(), Is.SameAs(entrant));
            var watch = Watches[Id(rear)]; Set(watch, "AdvancedAt", AdvancedAt(entrant)); Watches[Id(rear)] = watch;
            Assert.That(RefreshTarget(), Is.SameAs(rear));
        }

        [Test]
        public void ActualPressureRefreshesNeglectButDoesNotInventProgress()
        {
            var lead = Player(0, new Vector3(90, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Age(entrant, NeglectDeadline + 20);
            double advanced = AdvancedAt(entrant); Assert.That(RefreshTarget(), Is.SameAs(entrant));
            Call(route, "MarkPressured", entrant);
            Assert.That(RefreshTarget(), Is.SameAs(lead));
            Assert.That(AdvancedAt(entrant), Is.EqualTo(advanced));
            Assert.That(Get(Watches[Id(entrant)], "Furthest"), Is.EqualTo(0f));
            Assert.That((double)((IDictionary)Get(route, "pressured"))[Id(entrant)], Is.EqualTo(Now).Within(.1));
        }

        [Test]
        public void OnlyTheExistingFourMetresOfActualForwardProgressRefreshesNeglect()
        {
            Player(0, new Vector3(90, 0, 0)); var rear = Player(1, new Vector3(80, 0, 0));
            Age(rear, NeglectDeadline + 20); double before = AdvancedAt(rear);
            rear.transform.position = Origin + new Vector3(82, 0, 0); Sample();
            Assert.That(AdvancedAt(rear), Is.EqualTo(before)); Assert.That(RefreshTarget(), Is.SameAs(rear));
            rear.transform.position = Origin + new Vector3(84.1f, 0, 0); Sample();
            Assert.That(AdvancedAt(rear), Is.GreaterThan(before + NeglectDeadline));
            Assert.That((float)Get(Watches[Id(rear)], "Furthest"), Is.GreaterThan(84f));
            Assert.That(RefreshTarget(), Is.Not.SameAs(rear));
        }

        [Test]
        public void DistantAirborneProjectionCannotRefreshARealRunnersNeglectWatch()
        {
            var runner = Player(0, new Vector3(90, 0, 0)); Age(runner, NeglectDeadline + 20);
            double before = AdvancedAt(runner); Airborne(runner, true); runner.transform.position = hall.transform.position;
            Sample();
            Assert.That(AdvancedAt(runner), Is.EqualTo(before));
            Assert.That(Progress(runner, out bool inHall), Is.EqualTo(9f)); Assert.That(inHall, Is.False);
            Assert.That(RefreshTarget(), Is.SameAs(runner));
        }

        [TestCase("EscapeCaptured")]
        [TestCase("Retired")]
        public void NeglectedCachedTargetIsImmediatelyInvalidWhenNoLongerNormal(string state)
        {
            var lead = Player(0, new Vector3(90, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Age(entrant, NeglectDeadline + 20);
            Assert.That(RefreshTarget(), Is.SameAs(entrant)); Set(route, "nextFocus", double.MaxValue);
            State(entrant, state);
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(lead));
        }

        [Test]
        public void RestartClearsNeglectedPriorityAndCachedHallTarget()
        {
            var lead = Player(0, new Vector3(90, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Age(entrant, NeglectDeadline + 20);
            Assert.That(RefreshTarget(), Is.SameAs(entrant));
            Call(route, "ResetRoute", (object)null);
            Assert.That(Watches.Count, Is.Zero); Assert.That(Get(route, "focus"), Is.Null);
            Assert.That(RefreshTarget(), Is.SameAs(lead));
        }

        [Test]
        public void BackwardClockRebasesTemporalPriorityWithoutMovingTheRouteOrLosingTrustedFlightProgress()
        {
            var lead = Player(0, new Vector3(90, 0, 0));
            var entrant = Player(2, new Vector3(500, 43.33f, 0)); Sample();
            double now = Now; Call(route, "SampleProgress", now + NeglectDeadline + 50);
            ((IDictionary)Get(route, "pressured"))[Id(lead)] = now + 100;
            Set(route, "cursor", 5f); Set(route, "nextFocus", double.MaxValue);
            Call(route, "SampleProgress", now);
            Assert.That(AdvancedAt(entrant), Is.EqualTo(now));
            Assert.That(((IDictionary)Get(route, "pressured")).Count, Is.Zero);
            Assert.That(Get(route, "cursor"), Is.EqualTo(5f));
            Airborne(lead, true); lead.transform.position = hall.transform.position;
            Assert.That(Progress(lead, out bool inHall), Is.EqualTo(9f)); Assert.That(inHall, Is.False);
            Assert.That(Call(route, "SelectTarget"), Is.SameAs(lead));
        }
    }
}
