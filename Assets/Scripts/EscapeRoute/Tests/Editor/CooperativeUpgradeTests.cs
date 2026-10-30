using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    /// <summary>Offline state fixtures only: no session start, player input or live-scene traversal.</summary>
    public sealed class CooperativeUpgradeTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        const string LocksType = "CurtainCall.EscapeRoute.EscapeCooperativeLocks";
        const string CannonType = "CurtainCall.HumanCannon.HumanCannonSystem";
        const string PoleType = "CurtainCall.HumanCannon.HumanCannonPoleSystem";
        static readonly Vector3 Origin = new(9300, 40, 9300);
        readonly List<GameObject> roots = new();
        readonly List<(Type type, string name, object value)> savedStatics = new();
        IList players;
        object[] previousPlayers;
        Component poleSystem;

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
        static void SetStatic(Type type, string name, object value)
        {
            var property = type.GetProperty(name, StaticFlags);
            if (property != null) property.SetValue(null, value);
            else type.GetField(name, StaticFlags).SetValue(null, value);
        }
        void Isolate(string typeName, string name = "Current")
        {
            var type = T(typeName);
            object value = type.GetProperty(name, StaticFlags)?.GetValue(null) ?? type.GetField(name, StaticFlags)?.GetValue(null);
            savedStatics.Add((type, name, value)); SetStatic(type, name, null);
        }
        GameObject Root(string name, bool active = false)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.DontSave };
            root.SetActive(active); root.transform.position = Origin; roots.Add(root); return root;
        }
        Component Player(ulong id)
        {
            var root = Root("Upgrade actor " + id);
            var player = root.AddComponent(T(PlayerType));
            Call(root.GetComponent(T("CurtainCall.Player.PlayerMover")), "Awake");
            Set(player, "IsSpawned", true); Set(player, "OwnerClientId", id);
            root.SetActive(true); players.Add(player); return player;
        }
        Component HostSession()
        {
            var session = Root("Offline host session state").AddComponent(T("CurtainCall.Network.Session.NetworkSessionManager"));
            Set(session, "ConnectionState", Enum.Parse(T("CurtainCall.Network.Session.SessionConnectionState"), "Host"));
            Set(session, "GameState", Enum.Parse(T("CurtainCall.Network.Session.GameSessionState"), "Playing"));
            return session;
        }

        [TestCase(false)] [TestCase(true)]
        public void ObservationFrameBlocksGrabForDisplayAndHostButTriggersAndOpenApproachRemainValid(bool trigger)
        {
            var actor = Player(82);
            var poleObject = Root("Observation pole", true);
            poleObject.transform.position = Origin + new Vector3(3, 1, 0);
            var pole = poleObject.AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole"));
            Set(pole, "grabDistance", 3f);
            var frame = Root("Solid observation frame", true).AddComponent<BoxCollider>();
            frame.transform.position = Origin + new Vector3(1.5f, 1, 0);
            frame.size = new Vector3(.12f, 4, 4); frame.isTrigger = trigger;
            Physics.SyncTransforms();
            Assert.That(Call(pole, "IsInReach", actor), Is.True, "Existing scenes retain the old default.");
            Set(pole, "requireClearGrabPath", true);
            Assert.That(Call(pole, "IsInReach", actor), Is.EqualTo(trigger));
            Assert.That(Call(pole, "IsSeatInReach", actor, 0f, 0, -1d), Is.EqualTo(trigger));
            actor.transform.position = Origin + new Vector3(1.22f, 0, 0);
            Physics.SyncTransforms();
            Assert.That(Call(pole, "IsInReach", actor), Is.EqualTo(trigger), "Pressing into a frame must not skip an initial sphere overlap.");
            actor.transform.position = Origin + new Vector3(2.3f, 0, 0);
            Physics.SyncTransforms();
            Assert.That(Call(pole, "IsInReach", actor), Is.True);
            Assert.That(Call(pole, "IsSeatInReach", actor, 0f, 0, -1d), Is.True);
        }
        Component Locks(out object checkpoint, bool ready = false)
        {
            var locks = Root("Upgrade locks").AddComponent(T(LocksType));
            var checkpointType = locks.GetType().GetNestedType("Checkpoint");
            checkpoint = Activator.CreateInstance(checkpointType);
            var entries = Array.CreateInstance(checkpointType, 1); entries.SetValue(checkpoint, 0);
            Set(locks, "checkpoints", entries);
            if (ready)
            {
                Set(locks, "session", HostSession()); Set(locks, "synchronized", true);
                locks.gameObject.SetActive(true);
            }
            return locks;
        }

        [SetUp]
        public void SetUp()
        {
            Isolate(LocksType); Isolate(CannonType); Isolate(PoleType);
            Isolate("CurtainCall.Tightrope.PiggybackSystem");
            Isolate("CurtainCall.Tightrope.TightropeCourse", "current");
            Isolate("CurtainCall.HumanCannon.HumanCannonTrampoline");
            players = (IList)T(PlayerType).GetField("all", StaticFlags).GetValue(null);
            previousPlayers = players.Cast<object>().ToArray(); players.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (poleSystem != null) Call(poleSystem, "OnDisable");
            foreach (var player in players.Cast<object>().ToArray()) Set(player, "IsSpawned", false);
            players.Clear();
            for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
            roots.Clear();
            foreach (var value in savedStatics) SetStatic(value.type, value.name, value.value);
            savedStatics.Clear();
            foreach (var player in previousPlayers) players.Add(player);
            poleSystem = null; Physics.SyncTransforms();
        }

        [Test]
        public void DrawbridgeSupportsOnlyFullyDeployedDeckAndRevokesSupportWhenRetracting()
        {
            var span = Root("Two-leaf span").AddComponent(T("CurtainCall.EscapeRoute.EscapeCooperativeSpan"));
            var left = Root("Left leaf", true); var right = Root("Right leaf", true);
            var colliders = new Collider[] { left.AddComponent<BoxCollider>(), right.AddComponent<BoxCollider>() };
            Set(span, "leftLeaf", left.transform); Set(span, "rightLeaf", right.transform); Set(span, "deckColliders", colliders);
            Call(span, "SetDeployed", false);
            Assert.That(colliders.All(c => !c.enabled), Is.True);
            Assert.That(Quaternion.Angle(left.transform.localRotation, Quaternion.identity), Is.GreaterThan(60));
            Call(span, "SetDeployed", true); Set(span, "amount", .95f); Call(span, "Present");
            Assert.That(colliders.All(c => !c.enabled), Is.True, "An almost-flat visual is not a safe landing yet.");
            Set(span, "amount", 1f); Call(span, "Present");
            Assert.That(colliders.All(c => c.enabled), Is.True);
            Assert.That(Quaternion.Angle(left.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(right.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Call(span, "SetDeployed", false);
            Assert.That(colliders.All(c => !c.enabled), Is.True, "Support closes before the leaves rotate through actors.");
        }

        [Test]
        public void ReenabledDrawbridgeRestartsItsDeploymentTransitionBeforeRestoringSupport()
        {
            var span = Root("Reset span").AddComponent(T("CurtainCall.EscapeRoute.EscapeCooperativeSpan"));
            var collider = Root("Deck", true).AddComponent<BoxCollider>();
            Set(span, "deckColliders", new Collider[] { collider });
            Call(span, "SetDeployed", true); Set(span, "amount", 1f); Call(span, "Present");
            Assert.That(collider.enabled, Is.True);
            Call(span, "OnEnable");
            Assert.That(collider.enabled, Is.False);
            Assert.That(Get(span, "Traversable"), Is.False);
            Assert.That(Get(span, "deployed"), Is.True, "OnEnable restarts presentation; the lock still owns the target state.");
            Set(span, "amount", 1f); Call(span, "Present");
            Assert.That(collider.enabled, Is.True);
        }

        [Test]
        public void CannonInterlockRejectsBoardingAndLaunchWithoutInvalidatingAnExistingFlight()
        {
            var system = Root("Offline cannon").AddComponent(T(CannonType));
            Set(system, "session", HostSession());
            var station = Root("Interlocked cannon", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonStation"));
            var stations = Array.CreateInstance(station.GetType(), 1); stations.SetValue(station, 0);
            Set(system, "stations", stations); Call(system, "ResetSeats");
            var player = Player(31); Physics.SyncTransforms();
            Assert.That(Call(system, "FindBoardableStation", player), Is.EqualTo(0));
            Set(station, "InteractionAvailable", false);
            Assert.That(Call(system, "FindBoardableStation", player), Is.EqualTo(-1));
            Assert.That(Call(system, "ValidIndex", 0), Is.True, "A locked station remains valid for cancel and landing.");
            var seat = Get(((Array)Get(system, "seats")).GetValue(0), "State");
            var actionType = T("CurtainCall.HumanCannon.CannonAction");
            Assert.That(Call(seat, "TryApply", Enum.Parse(actionType, "Board"), 31UL, 0U), Is.True);
            uint revision = (uint)Get(seat, "Revision");
            Assert.That(Get(Get(system, "session"), "IsHost"), Is.True);
            Assert.That(Get(Get(system, "session"), "GameState").ToString(), Is.EqualTo("Playing"));
            Assert.That(Get(player, "IsSpawned"), Is.True);
            Assert.That(Get(player, "State").ToString(), Is.EqualTo("Normal"));
            Assert.That(Get(seat, "Occupant"), Is.EqualTo(31UL));
            Assert.That(revision, Is.EqualTo(1U));
            // A valid occupied host request must stop at the interlock, before network publication.
            Call(system, "HandleRequest", 31UL, 0, Enum.Parse(actionType, "Launch"), revision, 0U, Vector3.zero, true);
            Assert.That(Get(seat, "Revision"), Is.EqualTo(revision));
            Assert.That(Get(seat, "Occupant"), Is.EqualTo(31UL));
            var flying = Get(system, "flying"); Call(flying, "Begin", 44UL, 0, 7U);
            Assert.That(Call(system, "IsFlying", 44UL), Is.True);
            Assert.That(Call(system, "ValidIndex", 0), Is.True);
            Set(station, "InteractionAvailable", true);
            Assert.That(Call(system, "IsFlying", 44UL), Is.True, "Changing the interlock does not clear a flight's identity.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ButtonWorksSoloOnGroundOrInAirAndImmediatelyPowersItsDevice(bool airborne)
        {
            var locks = Locks(out object checkpoint, true);
            var player = Player(41);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(player, "mover", mover); Set(player, "IsOwner", true);
            Set(mover, "CurrentJump", Enum.Parse(T("CurtainCall.Player.JumpKind"), airborne ? "Fall" : "None"));
            var control = Root("Independent power button", true).transform;
            control.position = player.transform.position + Vector3.up * .75f;
            Set(checkpoint, "shoulderControl", control);
            var station = Root("Controlled cannon", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonStation"));
            Set(checkpoint, "lockedStation", station); Call(locks, "Present");
            Assert.That(Get(station, "InteractionAvailable"), Is.False);
            Assert.That(Get(player, "IsAirborne"), Is.EqualTo(airborne));
            Assert.That(Call(locks, "CanUseControl", player, 0), Is.True, "No stack, rope or grounded prerequisite.");
            Vector3 center = control.position;
            control.position = center + new Vector3(1.7f, 0, 1.7f);
            Assert.That(Call(locks, "CanUseControl", player, 0), Is.True);
            control.position = center + new Vector3(1.8f, 0, 1.8f);
            Assert.That(Call(locks, "CanUseControl", player, 0), Is.False);
            control.position = center;
            Call(locks, "Arm", 41UL, 0, 0U, 1U);
            Assert.That(Get(locks, "armed"), Is.EqualTo(1U));
            Assert.That(Call(locks, "IsLatched", 0), Is.True);
            Assert.That(Get(station, "InteractionAvailable"), Is.True, "No hidden arrival or helper requirement after pressing F.");
            Assert.That(Call(locks, "CanUseControl", player, 0), Is.False, "An already-on button cannot queue a second action.");
            uint revision = (uint)Get(locks, "revision");
            Call(locks, "Arm", 41UL, 0, revision, 1U);
            Assert.That(Get(locks, "revision"), Is.EqualTo(revision));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void HostAllowsAnyPiggybackRoleToTakeNearbyPoleAndReleasesItsLinks(int role)
        {
            var session = HostSession();
            var stack = Root("Stack roles").AddComponent(T("CurtainCall.Tightrope.PiggybackSystem"));
            Set(stack, "session", session); SetStatic(stack.GetType(), "Current", stack);
            var crew = new[] { Player(71), Player(72), Player(73) };
            var below = (IDictionary)Get(stack, "belowOf"); var above = (IDictionary)Get(stack, "aboveOf");
            below.Add(72UL, 71UL); above.Add(71UL, 72UL); below.Add(73UL, 72UL); above.Add(72UL, 73UL);
            var cannon = Root("Independent cannon registry").AddComponent(T(CannonType));
            Set(cannon, "session", session); Call(cannon, "ResetSeats"); SetStatic(cannon.GetType(), "Current", cannon);
            var pole = Root("Nearby independent pole", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole"));
            pole.transform.position += Vector3.up * 1.5f;
            poleSystem = Root("Independent pole registry").AddComponent(T(PoleType));
            var poles = Array.CreateInstance(pole.GetType(), 1); poles.SetValue(pole, 0); Set(poleSystem, "poles", poles);
            Set(poleSystem, "allowPlatformJumpGrab", true); Set(poleSystem, "independentInteraction", true);
            Call(poleSystem, "OnEnable"); Set(poleSystem, "session", session); Physics.SyncTransforms();
            Assert.That(Call(poleSystem, "FindGrabbable", crew[role]), Is.EqualTo(0));
            var action = Enum.Parse(poleSystem.GetType().GetNestedType("PoleAction", BindingFlags.NonPublic), "PlatformGrab");
            ulong actor = 71UL + (ulong)role;
            Call(poleSystem, "HandleRequest", actor, action, 0, 0U, 0, 0U, 0U, 0f, 0U, -1d);
            Assert.That(Call(poleSystem, "HeldBy", actor), Is.EqualTo(0));
            Assert.That(below.Contains(actor) || above.Contains(actor), Is.False);
            Assert.That(below.Count, Is.EqualTo(role == 2 ? 1 : 0));
            Assert.That(above.Count, Is.EqualTo(below.Count));
            Assert.That(Call(cannon, "IsFlying", actor), Is.True, "The catch publishes a real pole flight before releasing stack follow.");
            Assert.That(Call(poleSystem, "FindGrabbable", crew[(role + 1) % 3]), Is.EqualTo(-1), "Seat occupancy still applies.");
        }

        [Test]
        public void ReturnButtonWorksDuringFlightAndFeedbackCoalescesSnapshotsAndClearsOnReset()
        {
            var session = HostSession(); var player = Player(81);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(player, "mover", mover); Set(player, "IsOwner", true);
            Set(mover, "CurrentJump", Enum.Parse(T("CurtainCall.Player.JumpKind"), "Fall"));
            var ropes = Root("Independent return button").AddComponent(T("CurtainCall.EscapeRoute.EscapeCooperativeRopes"));
            var button = Root("Return switch", true).transform; button.position += Vector3.up * .75f;
            Set(ropes, "button", button); Set(ropes, "session", session); Set(ropes, "synchronized", true);
            // Keep this fixture detached from the scene singleton; the readiness check only needs an active component.
            var previous = ropes.GetType().GetProperty("Current").GetValue(null);
            SetStatic(ropes.GetType(), "Current", null); ropes.gameObject.SetActive(true);
            Set(ropes, "session", session); Set(ropes, "synchronized", true);
            try
            {
                Assert.That(Call(ropes, "CanUseButton", player), Is.True);
                var cannon = Root("Flying return actor").AddComponent(T(CannonType));
                Call(cannon, "ResetSeats"); SetStatic(cannon.GetType(), "Current", cannon);
                Call(Get(cannon, "flying"), "Begin", 81UL, 0, 7U);
                Assert.That(Call(ropes, "CanUseButton", player), Is.True);
                button.position += Vector3.right * 3;
                Assert.That(Call(ropes, "CanUseButton", player), Is.False);
            }
            finally { Call(ropes, "OnDisable"); SetStatic(ropes.GetType(), "Current", previous); }

            var feedback = Root("Snapshot feedback").AddComponent(T("CurtainCall.EscapeRoute.EscapeMechanismFeedback"));
            var pending = Get(feedback, "pending");
            Call(feedback, "ObserveState", true, 1U, 0U, false);
            Assert.That(Get(pending, "Count"), Is.EqualTo(0), "Late join does not replay previous activations.");
            Call(feedback, "ObserveState", true, 3U, 0U, false);
            Call(feedback, "ObserveState", true, 3U, 0U, false);
            Assert.That(Get(pending, "Count"), Is.EqualTo(1), "Repeated network snapshots do not repeat the toast.");
            Call(feedback, "ObserveState", true, 1U, 0U, false);
            Assert.That(Get(pending, "Count"), Is.EqualTo(1), "A pending device notice displays its latest state.");
            Call(feedback, "ObserveState", true, 1U, 1U, true);
            Assert.That(Get(pending, "Count"), Is.EqualTo(3));
            Call(feedback, "ObserveState", false, 0U, 0U, false);
            Assert.That(Get(pending, "Count"), Is.EqualTo(0));
            Call(feedback, "ObserveState", true, 7U, 7U, true);
            Assert.That(Get(pending, "Count"), Is.EqualTo(0));
        }

        [Test]
        public void LinkedCannonAndIndicatorFollowHostStateAndExplainActivationAndRelease()
        {
            var locks = Locks(out object checkpoint, true);
            Set(checkpoint, "sourceName", "상승 유지봉"); Set(checkpoint, "effectName", "상승 대포");
            var station = Root("Linked cannon feedback", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonStation"));
            Set(checkpoint, "lockedStation", station);
            var indicator = Root("Destination indicator", true).AddComponent<MeshRenderer>();
            Set(checkpoint, "linkedIndicators", new Renderer[] { indicator });
            var tint = new MaterialPropertyBlock();
            Call(locks, "ApplyState", 1U, 0U, 0U, 1U, 0);
            indicator.GetPropertyBlock(tint);
            Assert.That(Get(station, "InteractionAvailable"), Is.True);
            Assert.That(tint.GetColor("_BaseColor").g, Is.GreaterThan(.5f));
            Assert.That(Call(locks, "Feedback", 0), Is.EqualTo("상승 유지봉 → 상승 대포 · 가동 중"));
            Call(locks, "ApplyState", 2U, 0U, 0U, 0U, 0);
            indicator.GetPropertyBlock(tint);
            Assert.That(Get(station, "InteractionAvailable"), Is.False);
            Assert.That(tint.GetColor("_BaseColor"), Is.EqualTo(Color.red));
            Assert.That(Call(locks, "Feedback", 0), Is.EqualTo("상승 유지봉 해제 → 상승 대포 · 정지"));
            Call(locks, "ApplyState", 3U, 0U, 1U, 0U, 0);
            indicator.GetPropertyBlock(tint);
            Assert.That(tint.GetColor("_BaseColor"), Is.EqualTo(Color.green));
            Assert.That(Call(locks, "Feedback", 0), Is.EqualTo("맞은편 도착 → 상승 대포 · 가동 고정"));
        }

        [Test]
        public void ArrivalAcceptsSupportedRemoteOwnersButRejectsHoveringAndOriginalPoleHolders()
        {
            var locks = Locks(out object checkpoint);
            var player = Player(61);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(player, "mover", mover);
            var zone = Root("Remote arrival volume", true).AddComponent<BoxCollider>();
            zone.isTrigger = true; zone.center = Vector3.up * .5f; zone.size = new Vector3(3, 2, 3);
            Set(checkpoint, "arrival", zone);
            var slab = Root("Remote arrival support", true).AddComponent<BoxCollider>();
            slab.center = Vector3.down * .15f; slab.size = new Vector3(3, .3f, 3);
            Set(player, "IsOwner", true); Set(mover, "Simulated", true); Physics.SyncTransforms();
            Assert.That(Call(locks, "AtArrival", player, 0), Is.True);
            Set(player, "IsOwner", false); Set(mover, "Simulated", false);
            Assert.That(Call(locks, "AtArrival", player, 0), Is.True, "Host inspection must accept grounded remote players.");
            slab.enabled = false; Physics.SyncTransforms();
            Assert.That(Call(locks, "AtArrival", player, 0), Is.False);
            slab.enabled = true; Physics.SyncTransforms();

            var pole = Root("Other station's original pole", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole"));
            poleSystem = Root("Arrival pole registry").AddComponent(T(PoleType));
            var poles = Array.CreateInstance(pole.GetType(), 1); poles.SetValue(pole, 0); Set(poleSystem, "poles", poles);
            Call(poleSystem, "OnEnable");
            var state = Call(poleSystem, "State", 0);
            Assert.That(Call(state, "TryGrab", 61UL, 0U, 0d, 0f, false, 0), Is.True);
            Assert.That(Call(locks, "AtArrival", player, 0), Is.False, "Actual pole occupancy excludes arrival even over a real floor.");
            Assert.That(Call(state, "TryRelease", 61UL, 1U, 0d, 2f), Is.True);
            Assert.That(Call(locks, "AtArrival", player, 0), Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void CrewLatchRequiresConfiguredAliveHoldersAndSurvivesTheirDeparture(int required)
        {
            var locks = Locks(out object checkpoint);
            Set(checkpoint, "minimumHelpers", required); Set(checkpoint, "latchWhenCrewReady", true);
            var pole = Root("Shared original bar", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole"));
            Set(pole, "riderCapacity", 4); Set(checkpoint, "helperPole", pole);
            poleSystem = Root("Original pole registry").AddComponent(T(PoleType));
            var poles = Array.CreateInstance(pole.GetType(), 1); poles.SetValue(pole, 0); Set(poleSystem, "poles", poles);
            Call(poleSystem, "OnEnable");
            var state = Call(poleSystem, "State", 0);
            var first = Player(51); var second = Player(52); Player(53);
            Assert.That(Call(state, "TryGrab", 51UL, 0U, 0d, 0f, false, 0), Is.True);
            Call(locks, "HostStep"); Assert.That(Call(locks, "IsLatched", 0), Is.EqualTo(required == 1));
            Assert.That(Call(state, "TryGrab", 52UL, 1U, 0d, 0f, false, 1), Is.True);
            Set(second, "IsSpawned", false); Call(locks, "HostStep");
            Assert.That(Call(locks, "IsLatched", 0), Is.EqualTo(required == 1), "A stale occupied seat is not a living helper.");
            Set(second, "IsSpawned", true);
            Set(Get(second, "state"), "m_InternalValue", Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen"));
            Call(locks, "HostStep"); Assert.That(Call(locks, "IsLatched", 0), Is.EqualTo(required == 1));
            Set(Get(second, "state"), "m_InternalValue", Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), "Normal"));
            Call(locks, "HostStep"); Assert.That(Call(locks, "IsLatched", 0), Is.True);
            Assert.That(Get(locks, "held"), Is.EqualTo(1U));
            Set(first, "IsSpawned", false); Set(second, "IsSpawned", false); Call(locks, "HostStep");
            Assert.That(Get(locks, "held"), Is.EqualTo(0U)); Assert.That(Call(locks, "IsLatched", 0), Is.True);
            Call(locks, "Detach"); Assert.That(Call(locks, "IsLatched", 0), Is.False);
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void HigherSwitchRejectsSoloJumpButAcceptsInjuredTwoPersonStack(bool lowerInjured, bool upperInjured)
        {
            var stack = Root("Switch injured stack").AddComponent(T("CurtainCall.Tightrope.PiggybackSystem"));
            SetStatic(stack.GetType(), "Current", stack);
            var lower = Player(91); var upper = Player(92);
            ((IDictionary)Get(stack, "belowOf")).Add(92UL, 91UL);
            ((IDictionary)Get(stack, "aboveOf")).Add(91UL, 92UL);
            foreach (var entry in new[] { (player: lower, injured: lowerInjured), (player: upper, injured: upperInjured) })
            {
                var body = entry.player.GetComponent<CharacterController>();
                body.height = entry.injured ? .9f : 1.5f; body.center = Vector3.up * body.height * .5f;
                if (!entry.injured) continue;
                var condition = entry.player.GetComponent(T("CurtainCall.Player.PlayerCondition"))
                    ?? entry.player.gameObject.AddComponent(T("CurtainCall.Player.PlayerCondition"));
                Call(condition, "SetLostParts", Enum.Parse(T("CurtainCall.Player.BodyPart"), "LeftLeg, RightLeg, LeftArm"));
            }
            upper.transform.position = Origin + Vector3.up * (lowerInjured ? .9f : 1.5f);
            var button = Root("Higher piggyback switch", true).transform;
            button.position = Origin + new Vector3(1, 4.4f, 0);
            var rule = T("CurtainCall.EscapeRoute.EscapeSwitchInteraction");
            Assert.That(rule.GetMethod("InRange").Invoke(null, new object[] { upper, button, 2.5f }), Is.True);
            lower.transform.position = Origin + Vector3.up * .8f;
            Assert.That(rule.GetMethod("InRange").Invoke(null, new object[] { lower, button, 2.5f }), Is.False);
        }

        [Test]
        public void UpperCannonButtonKeepsPowerAfterTheHelperLeaves()
        {
            var locks = Locks(out var checkpoint, true);
            var player = Player(94);
            var button = Root("Upper rope power latch", true).transform;
            button.position = player.transform.position + Vector3.up * .75f;
            Set(checkpoint, "shoulderControl", button);
            Set(checkpoint, "helperPole", Root("Lower cannon clutch", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole")));
            var station = Root("Latched ascent cannon", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonStation"));
            Set(checkpoint, "lockedStation", station);
            Call(locks, "Arm", 94UL, 0, 0U, 1U);
            Call(locks, "HostStep"); Call(locks, "Present");
            Assert.That(Get(locks, "held"), Is.EqualTo(0U));
            Assert.That(Call(locks, "IsLatched", 0), Is.True);
            Assert.That(Get(station, "InteractionAvailable"), Is.True);
        }

        [TestCase(0)] [TestCase(1)]
        public void EitherSideHelperControlsTheSameBridge(int side)
        {
            var locks = Locks(out var checkpoint);
            var first = Root("Near side bridge clutch", true).AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole"));
            var second = Root("Far side bridge clutch", true).AddComponent(first.GetType());
            Set(checkpoint, "helperPole", first); Set(checkpoint, "returnHelperPole", second);
            poleSystem = Root("Bidirectional pole registry").AddComponent(T(PoleType));
            var poles = Array.CreateInstance(first.GetType(), 2); poles.SetValue(first, 0); poles.SetValue(second, 1);
            Set(poleSystem, "poles", poles); Call(poleSystem, "OnEnable");
            var player = Player(95);
            Call(Call(poleSystem, "State", side), "TryGrab", 95UL, 0U, 0d, 0f, false, 0);
            Call(locks, "HostStep");
            Assert.That(Get(locks, "held"), Is.EqualTo(1U));
            Assert.That(Call(locks, "IsHelperPole", first), Is.True);
            Assert.That(Call(locks, "IsHelperPole", second), Is.True);
            Set(player, "IsSpawned", false); Call(locks, "HostStep");
            Assert.That(Get(locks, "held"), Is.EqualTo(0U));
        }
    }
}
