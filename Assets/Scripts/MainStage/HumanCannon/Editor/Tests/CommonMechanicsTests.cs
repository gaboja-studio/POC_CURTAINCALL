using System.Reflection;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("November11CommonFix")]
    [Category("CommonProgressionRegression")]
    public sealed class CommonMechanicsTests
    {
        static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target,args);

        [Test, Category("CommonRulesRegression")]
        public void FourPlayersTwoPiggybackPairsCanSelectPoleWhileLongPressStillDismounts()
        {
            var roots = new System.Collections.Generic.List<GameObject>();
            var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
            var savedPlayers = all.ToArray(); all.Clear();
            var types = new[] { typeof(HumanCannonSystem), typeof(HumanCannonPoleSystem), typeof(PiggybackSystem), typeof(EscapeCooperativeRopes) };
            var current = new object[types.Length];
            for (int i = 0; i < types.Length; i++) current[i] = types[i].GetProperty("Current").GetValue(null);
            bool oldSuppressed = PiggybackSystem.Suppressed;
            var spawned = new NetworkPlayer[4];
            GameObject Root(string name, bool active = false) { var go = new GameObject(name); go.SetActive(active); roots.Add(go); return go; }
            try {
                typeof(InputGate).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                var cannon = Root("Cannon for common poles").AddComponent<HumanCannonSystem>(); Call(cannon, "ResetSeats");
                var poles = Root("Common pole system").AddComponent<HumanCannonPoleSystem>();
                var stack = Root("Four player piggyback", true).AddComponent<PiggybackSystem>();
                PiggybackSystem.Suppressed = false;
                types[0].GetProperty("Current").SetValue(null, cannon); types[1].GetProperty("Current").SetValue(null, poles);
                types[2].GetProperty("Current").SetValue(null, stack);
                var below = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("belowOf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stack);
                var above = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("aboveOf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stack);
                for (int i = 0; i < 4; i++) {
                    var go = Root("Actor " + i, true); go.transform.position = new Vector3(15000, 50 + (i % 2) * 1.5f, 15000 + i * .1f);
                    var player = go.AddComponent<NetworkPlayer>(); spawned[i] = player;
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                    typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, (ulong)i);
                    Call(player.GetComponent<PlayerMover>(), "Awake");
                    if (!all.Contains(player)) all.Add(player);
                    if (i % 2 == 1) { below[(ulong)i] = (ulong)(i-1); above[(ulong)(i-1)] = (ulong)i; }
                }
                var pole = Root("Tutorial reachable pole", true).AddComponent<HumanCannonPole>();
                pole.transform.position = new Vector3(15000, 52.5f, 15001);
                poles.Register(pole); Physics.SyncTransforms();
                foreach (int i in new[] { 1, 3 }) {
                    var reader = spawned[i].gameObject.AddComponent<PlayerInputReader>();
                    typeof(PlayerInputReader).GetProperty("Current").SetValue(reader, new PlayerInputFrame { InteractHeld = true });
                    Assert.AreSame(pole, CommonInteractionSelection.Selected(reader), "A mounted rider must be able to target the nearby pole.");
                    typeof(PlayerInputReader).GetProperty("Current").SetValue(reader, new PlayerInputFrame { InteractHeld = true, InteractLongPressed = true });
                    var gestures = (System.Collections.IDictionary)typeof(CommonInteractionSelection).GetField("gestures", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    var gesture = gestures[reader.GetInstanceID()]; gesture.GetType().GetField("Frame").SetValue(gesture, -1);
                    Assert.AreSame(stack, CommonInteractionSelection.Selected(reader), "Holding F still releases the piggyback link.");
                    Assert.IsTrue(CommonInteractionSelection.Consume(reader, stack));
                    Assert.IsFalse(CommonInteractionSelection.Consume(reader, pole));
                    Assert.IsFalse(CommonInteractionSelection.Consume(reader, stack));
                }

                var ropes = Root("Escape return button controls", true).AddComponent<EscapeCooperativeRopes>();
                var button = Root("Escape return button", true).transform;
                button.position = PlayerInteraction.Center(spawned[1]) + Vector3.forward;
                var session = Root("Playing session").AddComponent<NetworkSessionManager>();
                typeof(NetworkSessionManager).GetProperty("GameState").SetValue(session, GameSessionState.Playing);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(EscapeCooperativeRopes).GetField("session", fields).SetValue(ropes, session);
                typeof(EscapeCooperativeRopes).GetField("button", fields).SetValue(ropes, button);
                typeof(EscapeCooperativeRopes).GetField("synchronized", fields).SetValue(ropes, true);
                typeof(EscapeCooperativeRopes).GetProperty("Current").SetValue(null, ropes);
                typeof(CommonInteractionSelection).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                var riderReader = spawned[1].GetComponent<PlayerInputReader>();
                typeof(PlayerInputReader).GetProperty("Current").SetValue(riderReader, new PlayerInputFrame { InteractTapped = true });
                Assert.IsTrue(ropes.CanUseButton(spawned[1]));
                Assert.AreSame(ropes, CommonInteractionSelection.Selected(riderReader), "A mounted F tap can select the button even with a nearby pole.");
                Assert.IsFalse(CommonInteractionSelection.Consume(riderReader, stack));
            } finally {
                for (int i = 0; i < types.Length; i++) types[i].GetProperty("Current").SetValue(null, current[i]);
                PiggybackSystem.Suppressed = oldSuppressed;
                foreach (var player in spawned) if (player != null) typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                foreach (var go in roots) Object.DestroyImmediate(go);
                all.Clear(); all.AddRange(savedPlayers);
                typeof(CommonInteractionSelection).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }
        }

        [Test]
        public void RuntimePoleNetworkIdsDoNotDependOnRegistrationOrderOrRebuildHistory()
        {
            var first = new GameObject("Same generated course"); first.SetActive(false);
            var second = new GameObject("Same generated course"); second.SetActive(false);
            try
            {
                var host = first.AddComponent<HumanCannonPoleSystem>();
                var client = second.AddComponent<HumanCannonPoleSystem>();
                HumanCannonPole Pole(GameObject parent, string name)
                { var child=new GameObject(name); child.transform.SetParent(parent.transform,false); return child.AddComponent<HumanCannonPole>(); }
                int hostA=host.Register(Pole(first,"A")), hostB=host.Register(Pole(first,"B"));
                int clientB=client.Register(Pole(second,"B")), clientA=client.Register(Pole(second,"A"));
                Assert.That(Call(client,"LocalIndex",Call(host,"WireIndex",hostA)),Is.EqualTo(clientA));
                Assert.That(Call(client,"LocalIndex",Call(host,"WireIndex",hostB)),Is.EqualTo(clientB));
                uint previous=host.State(hostA).Revision;
                Assert.That(host.Register(Pole(first,"A")),Is.EqualTo(hostA));
                Assert.That(host.PoleCount,Is.EqualTo(2),"단계 재생성이 봉 번호를 무한히 추가하지 않는다.");
                Assert.That(host.State(hostA).Revision,Is.GreaterThan(previous),"재생성 이전 메시지를 거부한다.");
                Assert.That(Call(client,"LocalIndex",int.MaxValue),Is.EqualTo(-1));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
        static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [TestCase(0)] [TestCase(2)] [TestCase(3)]
        public void MountedRiderCanSelectAndBoardACannonWithoutCarryingTheStack(int lostParts)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Mounted cannon boarding test");
            root.transform.position = new Vector3(16000, 50, 16000);
            var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
            var previous = all.ToArray();
            var cannonCurrent = typeof(HumanCannonSystem).GetProperty("Current");
            var stackCurrent = typeof(PiggybackSystem).GetProperty("Current");
            var previousCannon = cannonCurrent.GetValue(null);
            var previousStack = stackCurrent.GetValue(null);
            var local = typeof(NetworkPlayer).GetProperty("Local");
            var previousLocal = local.GetValue(null);
            bool previousSuppressed = PiggybackSystem.Suppressed;
            var spawned = typeof(NetworkPlayer).GetProperty("IsSpawned");
            GameObject Child(string name, bool active = true)
            {
                var child = new GameObject(name); child.SetActive(active);
                child.transform.SetParent(root.transform, false);
                return child;
            }
            NetworkPlayer Actor(ulong id, float height)
            {
                var actor = Child("Rider").AddComponent<NetworkPlayer>();
                actor.transform.localPosition = Vector3.up * height;
                Call(actor.GetComponent<PlayerMover>(), "Awake");
                spawned.SetValue(actor, true);
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(actor, id);
                all.Add(actor);
                return actor;
            }
            try
            {
                all.Clear();
                typeof(InputGate).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                var lower = Actor(1810, 0f);
                var rider = Actor(1811, 1.5f);
                var upper = Actor(1812, 3f);
                local.SetValue(null, rider);
                var condition = rider.gameObject.AddComponent<PlayerCondition>();
                condition.SetLostParts(PlayerCondition.PartsForLostCount(lostParts));
                rider.gameObject.AddComponent<PlayerInteraction>();
                var damage = rider.gameObject.AddComponent<CurtainCall.Damage.BodyDamageEffects>();
                Call(damage, "Awake"); Call(damage, "Update");
                var sessionRoot = Child("Session", false);
                var session = sessionRoot.AddComponent<CurtainCall.Network.Session.NetworkSessionManager>();
                Set(session, "_networkManager", sessionRoot.GetComponent<Unity.Netcode.NetworkManager>());
                session.GetType().GetProperty("ConnectionState").SetValue(session, CurtainCall.Network.Session.SessionConnectionState.Host);
                var stack = Child("Stack").AddComponent<PiggybackSystem>();
                Set(stack, "session", session);
                stackCurrent.SetValue(null, stack);
                PiggybackSystem.Suppressed = false;
                var below = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("belowOf", fields).GetValue(stack);
                var above = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("aboveOf", fields).GetValue(stack);
                below[rider.OwnerClientId] = lower.OwnerClientId; below[upper.OwnerClientId] = rider.OwnerClientId;
                above[lower.OwnerClientId] = rider.OwnerClientId; above[rider.OwnerClientId] = upper.OwnerClientId;
                var station = Child("Cannon station").AddComponent<HumanCannonStation>();
                station.transform.position = PlayerInteraction.Center(rider) + Vector3.forward * .5f;
                var cannon = Child("Cannon", false).AddComponent<HumanCannonSystem>();
                cannon.ConfigurePractice(new[] { station }, -10f);
                Call(cannon, "ResetSeats");
                Set(cannon, "session", session); Set(cannon, "boundPlayer", rider);
                cannonCurrent.SetValue(null, cannon);
                var pilot = rider.gameObject.AddComponent<HumanCannonPilot>();
                Set(pilot, "system", cannon); Set(pilot, "player", rider);
                Physics.SyncTransforms();

                Assert.AreSame(station, CommonInteractionSelection.Selected(rider));
                Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyCannon"));
                Assert.IsTrue(cannon.IsSeated(rider.OwnerClientId));
                Assert.IsFalse(cannon.IsSeated(upper.OwnerClientId));
                Assert.IsFalse(stack.IsInStack(lower));
                Assert.IsFalse(stack.IsInStack(rider));
                Assert.IsFalse(stack.IsInStack(upper));
            }
            finally
            {
                cannonCurrent.SetValue(null, previousCannon); stackCurrent.SetValue(null, previousStack);
                local.SetValue(null, previousLocal); PiggybackSystem.Suppressed = previousSuppressed;
                foreach (var actor in all) spawned.SetValue(actor, false);
                all.Clear(); all.AddRange(previous);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false, 0f)]
        [TestCase(false, 4f)]
        [TestCase(true, -4f)]
        public void HostCatchStartsTheCommonSwingAndSecondRiderKeepsItsSpeed(bool fromFlight, float approach)
        {
            var root = new GameObject("Host pole catch");
            root.transform.position = new Vector3(15000f, 50f, 15000f);
            var currentCannon = typeof(HumanCannonSystem).GetProperty("Current");
            var savedCannon = currentCannon.GetValue(null);
            var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
            var crew = new System.Collections.Generic.List<NetworkPlayer>();
            var request = typeof(HumanCannonPoleSystem).GetNestedType("PoleAction", BindingFlags.NonPublic);
            GameObject Child(string name, bool active = true)
            {
                var child = new GameObject(name);
                child.SetActive(active);
                child.transform.SetParent(root.transform, false);
                return child;
            }
            NetworkPlayer Actor(ulong id, Vector3 grab)
            {
                var player = Child("Rider").AddComponent<NetworkPlayer>();
                player.transform.position = grab - Vector3.up;
                Call(player.GetComponent<PlayerMover>(), "Awake");
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, id);
                crew.Add(player);
                all.Add(player);
                return player;
            }
            try
            {
                var session = Child("Session", false).AddComponent<CurtainCall.Network.Session.NetworkSessionManager>();
                session.GetType().GetProperty("ConnectionState").SetValue(session, CurtainCall.Network.Session.SessionConnectionState.Host);
                var cannon = Child("Cannon", false).AddComponent<HumanCannonSystem>();
                Call(cannon, "ResetSeats");
                currentCannon.SetValue(null, cannon);
                var poles = Child("Pole system", false).AddComponent<HumanCannonPoleSystem>();
                Set(poles, "session", session);
                var pole = Child("Pole").AddComponent<HumanCannonPole>();
                pole.IsSwitch = fromFlight;
                int index = poles.Register(pole);
                var first = Actor(1701, pole.GrabPoint(0f, 0));
                uint launch = 20u;
                if (fromFlight) Call(cannon, "BeginPoleFlight", first.OwnerClientId, launch);
                Physics.SyncTransforms();
                Call(poles, "HandleRequest", first.OwnerClientId, System.Enum.Parse(request, fromFlight ? "Grab" : "PlatformGrab"),
                    index, poles.State(index).Revision, CannonFlightState.PoleEntrySource, launch, 0u, approach, 0u, -1d);

                Assert.That(poles.State(index).SeatOf(first.OwnerClientId), Is.Zero);
                float speed = poles.SpeedAt(index);
                var velocity = PoleSwingMotion.Velocity(0f, speed, pole.SwingLength, pole.Forward) * GameSettings.Poles.SwingMotionSpeed;
                Assert.That(Vector3.Dot(velocity, pole.Forward), Is.EqualTo(approach).Within(.001f));

                var second = Actor(1702, pole.GrabPoint(0f, 1));
                Physics.SyncTransforms();
                Call(poles, "HandleRequest", second.OwnerClientId, System.Enum.Parse(request, "PlatformGrab"),
                    index, poles.State(index).Revision, 0, 0u, 0u, 100f, 0u, -1d);
                Assert.That(poles.State(index).SeatOf(second.OwnerClientId), Is.EqualTo(1));
                Assert.AreEqual(speed, poles.SpeedAt(index));
            }
            finally
            {
                currentCannon.SetValue(null, savedCannon);
                foreach (var player in crew)
                {
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                    all.Remove(player);
                }
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("ordinary jump", true)]
        [TestCase("ordinary fall", true)]
        [TestCase("cannon flight", false)]
        [TestCase("rope hang", false)]
        [TestCase("mounted rider", false)]
        public void DelayedTrampolineGrabRechecksThePlayersCurrentActivity(string activity, bool allowed)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            const ulong actor = 713UL;
            var root = new GameObject("Carry activity test");
            root.transform.position = new Vector3(3500, 45, 3500);
            var types = new[] { typeof(HumanCannonSystem), typeof(TightropeHangSystem), typeof(PiggybackSystem) };
            var saved = System.Array.ConvertAll(types, t => t.GetProperty("Current").GetValue(null));
            NetworkPlayer player = null;
            GameObject Child(string name) { var child = new GameObject(name); child.transform.SetParent(root.transform, false); return child; }
            try
            {
                var cannon = Child("Cannon").AddComponent<HumanCannonSystem>();
                Call(cannon, "ResetSeats");
                types[0].GetProperty("Current").SetValue(null, cannon);
                var hangs = Child("Rope hang").AddComponent<TightropeHangSystem>();
                types[1].GetProperty("Current").SetValue(null, hangs);
                var stack = Child("Piggyback").AddComponent<PiggybackSystem>();
                types[2].GetProperty("Current").SetValue(null, stack);
                player = Child("Player").AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                typeof(NetworkPlayer).GetProperty("IsOwner").SetValue(player, true);
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, actor);
                var mover = player.GetComponent<PlayerMover>();
                Call(mover, "Awake"); Call(player, "Awake");
                var trampoline = Child("Trampoline").AddComponent<HumanCannonTrampoline>();
                trampoline.transform.localPosition = Vector3.forward * 1.5f;
                var handle = Child("Handle").transform;
                handle.SetParent(trampoline.transform, false);
                handle.localPosition = Vector3.back;
                Set(trampoline, "handle", handle);
                Set(trampoline, "carryBody", trampoline.gameObject.AddComponent<BoxCollider>());
                Physics.SyncTransforms();
                Assert.IsTrue(trampoline.CanGrab(player), "The starting pickup is in range and unobstructed.");
                switch (activity)
                {
                    case "ordinary jump": mover.LaunchJump(0f); break;
                    case "ordinary fall": mover.ResetExternalMotion(true); break;
                    case "cannon flight":
                        ((CannonFlightState)typeof(HumanCannonSystem).GetField("flying", fields).GetValue(cannon)).Begin(actor, 0, 1);
                        break;
                    case "rope hang":
                        ((TightropeHangBook)typeof(TightropeHangSystem).GetField("book", fields).GetValue(hangs)).Begin(actor, 0, 4f, 7d);
                        break;
                    case "mounted rider":
                        var carrier = Child("Carrier").AddComponent<NetworkPlayer>();
                        typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(carrier, actor + 1);
                        var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
                        all.Add(carrier);
                        try
                        {
                            ((System.Collections.IDictionary)typeof(PiggybackSystem).GetField("belowOf", fields).GetValue(stack)).Add(actor, actor + 1);
                            Assert.AreEqual(allowed, trampoline.CanGrab(player));
                        }
                        finally { all.Remove(carrier); }
                        return;
                }
                Assert.AreEqual(allowed, trampoline.CanGrab(player));
            }
            finally
            {
                if (player != null) typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                for (int i = 0; i < types.Length; i++) types[i].GetProperty("Current").SetValue(null, saved[i]);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReviveClearsOnlyThePreviousCannonOccupant(bool host)
        {
            var root = new GameObject("Cannon seat recovery");
            root.SetActive(false);
            try
            {
                const ulong actor = 713UL, other = 714UL;
                var session = root.AddComponent<CurtainCall.Network.Session.NetworkSessionManager>();
                Set(session, "_networkManager", root.GetComponent<Unity.Netcode.NetworkManager>());
                var connection = host ? CurtainCall.Network.Session.SessionConnectionState.Host
                    : CurtainCall.Network.Session.SessionConnectionState.Client;
                session.GetType().GetProperty("ConnectionState").SetValue(session, connection);
                var cannon = root.AddComponent<HumanCannonSystem>();
                cannon.ConfigurePractice(new HumanCannonStation[2], -10f);
                Call(cannon, "ResetSeats");
                Set(cannon, "session", session);
                var player = root.AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, actor);

                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var seats = (System.Array)typeof(HumanCannonSystem).GetField("seats", fields).GetValue(cannon);
                var first = (CannonSeatState)seats.GetValue(0).GetType().GetField("State").GetValue(seats.GetValue(0));
                var second = (CannonSeatState)seats.GetValue(1).GetType().GetField("State").GetValue(seats.GetValue(1));
                Assert.IsTrue(first.TryApply(CannonAction.Board, actor, first.Revision));
                Assert.IsTrue(second.TryApply(CannonAction.Board, other, second.Revision));

                Call(cannon, "HandleRevivedMotion", player);
                Assert.IsFalse(cannon.IsSeated(actor));
                Assert.IsTrue(cannon.IsSeated(other));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CheckpointCleanupReleasesTheTrampolineEvenWhenItsCarrierHasFallen()
        {
            var root = new GameObject("Carry recovery host"); root.SetActive(false);
            try
            {
                const ulong actor = 714UL;
                var session = root.AddComponent<CurtainCall.Network.Session.NetworkSessionManager>();
                Set(session, "_networkManager", root.GetComponent<Unity.Netcode.NetworkManager>());
                session.GetType().GetProperty("ConnectionState").SetValue(session, CurtainCall.Network.Session.SessionConnectionState.Host);
                var player = root.AddComponent<NetworkPlayer>();
                var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, actor);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                ((Unity.Netcode.NetworkVariable<PlayerState>)typeof(NetworkPlayer).GetField("state", fields).GetValue(player)).Value = PlayerState.Fallen;
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                all.Add(player);
                try
                {
                    var trampoline = root.AddComponent<HumanCannonTrampoline>();
                    Set(trampoline, "session", session);
                    var ownership = (CannonSeatState)typeof(HumanCannonTrampoline).GetField("ownership", fields).GetValue(trampoline);
                    Assert.IsTrue(ownership.TryApply(CannonAction.Board, actor, ownership.Revision));
                    trampoline.ServerReleaseForInspection(actor);
                    Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                }
                finally
                {
                    all.Remove(player);
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RevivedPassengerIsNotCarriedByThePreviousTrip(bool host)
        {
            var root = new GameObject("Transport recovery");
            root.SetActive(false);
            var current = typeof(HumanCannonRouteSystem).GetProperty("Current");
            var previous = current.GetValue(null);
            var route = root.AddComponent<HumanCannonRouteSystem>();
            try
            {
                current.SetValue(null, null);
                route.ConfigurePractice(null, -10f);
                Call(route, "OnEnable");
                var session = root.AddComponent<CurtainCall.Network.Session.NetworkSessionManager>();
                var connection = host ? CurtainCall.Network.Session.SessionConnectionState.Host
                    : CurtainCall.Network.Session.SessionConnectionState.Client;
                session.GetType().GetProperty("ConnectionState").SetValue(session, connection);
                Set(route, "session", session);
                var player = root.AddComponent<NetworkPlayer>();
                const ulong actor = 717UL;
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, actor);
                var travel = route.TransportState(0);
                Assert.IsTrue(travel.TryStart(actor, travel.Revision, 10d, 8f));
                Assert.IsTrue(route.IsRiding(actor));

                Call(route, "ReleaseRevivedPassenger", player);
                Assert.IsFalse(route.IsRiding(actor));
                Assert.IsFalse(route.IsRiding(CannonSeatState.Nobody));
                Assert.AreEqual(0.5f, travel.Progress(14d));
                Assert.IsTrue(travel.Tick(18d));
                Assert.IsTrue(travel.TryReturn(20d));
                Assert.IsTrue(travel.Tick(28d));
                Assert.IsTrue(travel.TryStart(actor, travel.Revision, 30d, 8f));
                Assert.IsTrue(route.IsRiding(actor));
            }
            finally
            {
                Call(route, "OnDisable");
                current.SetValue(null, previous);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CarryReleaseSurvivesTheChangeFromPlatformToRopeControls(bool onRope)
        {
            var root = new GameObject("Carry input handoff");
            var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
            var local = typeof(NetworkPlayer).GetProperty("Local");
            var oldLocal = local.GetValue(null);
            const BindingFlags gateFields = BindingFlags.Static | BindingFlags.NonPublic;
            var rearming = typeof(InputGate).GetField("rearming", gateFields);
            var blockedFrame = typeof(InputGate).GetField("blockedFrame", gateFields);
            var previousRearming = rearming.GetValue(null);
            var previousBlockedFrame = blockedFrame.GetValue(null);
            var blockers = (System.Collections.Generic.HashSet<object>)typeof(InputGate).GetField("blockers", gateFields).GetValue(null);
            var previousBlockers = new System.Collections.Generic.HashSet<object>(blockers);
            var scheme = ScriptableObject.CreateInstance<TightropeControlScheme>();
            NetworkPlayer player = null;
            HumanCannonTrampoline trampoline = null;
            PlayerControlContext context = null;
            try
            {
                rearming.SetValue(null, false);
                blockedFrame.SetValue(null, -1);
                blockers.Clear();
                const ulong actor = 715UL;
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var sessionRoot = new GameObject("Host"); sessionRoot.SetActive(false); sessionRoot.transform.SetParent(root.transform);
                var session = sessionRoot.AddComponent<CurtainCall.Network.Session.NetworkSessionManager>();
                Set(session, "_networkManager", sessionRoot.GetComponent<Unity.Netcode.NetworkManager>());
                session.GetType().GetProperty("ConnectionState").SetValue(session, CurtainCall.Network.Session.SessionConnectionState.Host);
                player = root.AddComponent<NetworkPlayer>();
                Call(player.GetComponent<PlayerMover>(), "Awake"); Call(player, "Awake");
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                typeof(NetworkPlayer).GetProperty("IsOwner").SetValue(player, true);
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, actor);
                all.Add(player); local.SetValue(null, player);
                var pilot = root.AddComponent<HumanCannonPilot>();
                Set(pilot, "player", player);
                Set(pilot, "system", sessionRoot.AddComponent<HumanCannonSystem>());
                var deviceRoot = new GameObject("Held trampoline"); deviceRoot.transform.SetParent(root.transform);
                trampoline = deviceRoot.AddComponent<HumanCannonTrampoline>();
                Set(trampoline, "session", session);
                var ownership = (CannonSeatState)typeof(HumanCannonTrampoline).GetField("ownership", fields).GetValue(trampoline);
                Assert.IsTrue(ownership.TryApply(CannonAction.Board, actor, ownership.Revision));
                HumanCannonCarry.Register(trampoline);
                Call(pilot, "ReadInput", default(PlayerInputFrame));
                Call(pilot, "ReadInput", default(PlayerInputFrame));
                var input = new PlayerInputFrame { InteractLongPressed = true, InteractHeld = true, InteractHoldProgress = 1f };
                if (onRope)
                {
                    context = new PlayerControlContext(root);
                    scheme.Enter(context);
                    scheme.Tick(context, input);
                }
                else Call(pilot, "ReadInput", input);
                Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
            }
            finally
            {
                if (context != null) scheme.Exit(context);
                if (trampoline != null) HumanCannonCarry.Unregister(trampoline);
                if (player != null)
                {
                    all.Remove(player);
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                }
                local.SetValue(null, oldLocal);
                blockers.UnionWith(previousBlockers);
                rearming.SetValue(null, previousRearming);
                blockedFrame.SetValue(null, previousBlockedFrame);
                Object.DestroyImmediate(scheme);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UvLampPausesForPhysicalCarryWithoutLosingItsInventorySlot()
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            const ulong actor = 716UL;
            var root = new GameObject("Carry with UV inventory");
            var inventoryInstance = typeof(PropCarry).GetProperty("Instance");
            var oldInventory = inventoryInstance.GetValue(null);
            NetworkPlayer player = null;
            HumanCannonTrampoline trampoline = null;
            try
            {
                player = root.AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, actor);
                var reader = root.AddComponent<PlayerInputReader>();
                var inventory = root.AddComponent<PropCarry>();
                inventoryInstance.SetValue(null, inventory);
                var held = (System.Collections.Generic.Dictionary<ulong, System.Collections.Generic.List<PropItem>>)
                    typeof(PropCarry).GetField("receivedHeld", fields).GetValue(inventory);
                held.Add(actor, new() { new PropItem(13, ItemUses.UvLamp, "UV") });
                var equipped = typeof(UvFlashlight).GetMethod("Equipped", BindingFlags.Static | BindingFlags.NonPublic);
                bool CanLight() => (bool)equipped.Invoke(null, new object[] { player, reader });
                Assert.IsTrue(CanLight());
                trampoline = root.AddComponent<HumanCannonTrampoline>();
                HumanCannonCarry.Register(trampoline);
                var ownership = (CannonSeatState)typeof(HumanCannonTrampoline).GetField("ownership", fields).GetValue(trampoline);
                Assert.IsTrue(ownership.TryApply(CannonAction.Board, actor, ownership.Revision));
                Assert.IsFalse(CanLight(), "A hidden UV prop must not keep shining during physical carry.");
                Assert.IsTrue(ownership.TryApply(CannonAction.Release, actor, ownership.Revision));
                Assert.IsTrue(CanLight());
                Assert.That(inventory.Held(actor)[0].Uid, Is.EqualTo(13));
            }
            finally
            {
                if (trampoline != null) HumanCannonCarry.Unregister(trampoline);
                if (player != null) typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                inventoryInstance.SetValue(null, oldInventory);
                Object.DestroyImmediate(root);
            }
        }

        [Test, Category("CommonRulesRegression")]
        public void RemoteCannonOccupancyExcludesPiggybackWithoutAnOwnerPilot()
        {
            var current = typeof(HumanCannonSystem).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = current.GetValue(null);
            var root = new GameObject("cannon occupancy"); root.SetActive(false);
            var actor = new GameObject("remote actor without pilot"); actor.SetActive(false);
            try
            {
                var station = root.AddComponent<HumanCannonStation>();
                var cannon = root.AddComponent<HumanCannonSystem>();
                cannon.ConfigurePractice(new[] { station }, -10f);
                Call(cannon, "ResetSeats");
                current.SetValue(null, cannon);
                var player = actor.AddComponent<NetworkPlayer>();
                Assert.IsNull(actor.GetComponent<HumanCannonPilot>());
                var occupied = typeof(PiggybackSystem).GetMethod("UsingDevice", BindingFlags.Static | BindingFlags.NonPublic);
                var following = typeof(PiggybackFollow).GetMethod("IsCrossing", BindingFlags.Static | BindingFlags.NonPublic);
                var seats = (System.Array)typeof(HumanCannonSystem).GetField("seats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cannon);
                var runtime = seats.GetValue(0);
                var seat = (CannonSeatState)runtime.GetType().GetField("State").GetValue(runtime);
                Assert.IsFalse((bool)occupied.Invoke(null, new object[] { player }));
                Assert.IsTrue(seat.TryApply(CannonAction.Board, player.OwnerClientId, seat.Revision));
                Assert.IsTrue((bool)occupied.Invoke(null, new object[] { player }), "Remote seat occupancy must block a new shoulder mount.");
                Assert.IsTrue((bool)following.Invoke(null, new object[] { player }), "Seat movement must take precedence over an older piggyback snapshot.");
                Assert.IsTrue(seat.TryApply(CannonAction.Launch, player.OwnerClientId, seat.Revision));
                Call(cannon, "Apply", 0, CannonAction.Launch, player.OwnerClientId, Vector3.zero, Quaternion.identity, Vector3.up);
                Assert.IsTrue((bool)occupied.Invoke(null, new object[] { player }), "Remote cannon flight must remain mutually exclusive with piggyback.");
                Assert.IsTrue((bool)following.Invoke(null, new object[] { player }));
                Call(cannon, "Apply", 0, CannonAction.Release, player.OwnerClientId, Vector3.zero, Quaternion.identity, Vector3.zero);
                Assert.IsFalse((bool)occupied.Invoke(null, new object[] { player }));
            }
            finally
            {
                current.SetValue(null, previous);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        [Test, Category("CommonRulesRegression")]
        public void PiggybackDeviceEligibilityDoesNotGrantClientTransferAuthority()
        {
            var root = new GameObject("client piggyback query"); root.SetActive(false);
            var actor = new GameObject("client rider"); actor.SetActive(false);
            NetworkPlayer player = null;
            try
            {
                var stack = root.AddComponent<PiggybackSystem>();
                player = actor.AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                var below = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem)
                    .GetField("belowOf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stack);
                below[player.OwnerClientId] = 1UL;
                Assert.IsTrue((bool)Call(stack, "CanReleaseForDevice", player), "The owner can see an available pole while mounted, without host authority.");
                Assert.IsFalse((bool)Call(stack, "TryReleaseForDevice", player, null), "Only the host may mutate the shared links.");
                Assert.IsTrue(stack.IsInStack(player), "A client-side query or transfer attempt must leave the link intact.");
            }
            finally
            {
                if (player != null) typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LegacyTrampolineValuesCannotOverrideCommonBounceCarryAndCarrierRequirement()
        {
            var root = new GameObject("common trampoline"); root.SetActive(false);
            try
            {
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                Assert.IsNull(trampoline.GetType().GetField("upwardSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)); Assert.IsNull(trampoline.GetType().GetField("groundBounceAirControlSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
                Assert.IsNull(trampoline.GetType().GetField("requireCarrierForBounce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
                Assert.That(trampoline.UpwardSpeed, Is.EqualTo(12f));
                Assert.That(trampoline.CarryMoveSpeed, Is.EqualTo(4.5f));
                Assert.IsFalse(trampoline.RequireCarrierForBounce, "마지막 운반자도 내려놓고 반동할 수 있다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LegacyStationValuesCannotOverrideCommonReachAndAirControl()
        {
            var root = new GameObject("common station"); root.SetActive(false);
            try
            {
                var station = root.AddComponent<HumanCannonStation>();
                Assert.IsNull(station.GetType().GetField("boardingDistance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)); Assert.IsNull(station.GetType().GetField("airControlSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
                Assert.That(station.BoardingDistance, Is.EqualTo(2.5f));
                Assert.That(station.AirControlSpeed, Is.EqualTo(GameSettings.Base.AirControl.Speed));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(.35f, 45f, true)]
        [TestCase(70f, 45f, false)]
        public void WalkableLandingAcceptsFlatObjectsAndRejectsSteepFaces(float angle, float slopeLimit, bool expected)
        {
            var root = new GameObject("support fixture");
            root.transform.position = new Vector3(3500,3500,3500);
            var actor = new GameObject("actor"); actor.SetActive(false);
            try
            {
                var surface = root.AddComponent<BoxCollider>(); surface.size = new Vector3(8,.2f,8);
                root.transform.rotation = Quaternion.Euler(angle,0,0);
                var mover = actor.AddComponent<PlayerMover>();
                actor.GetComponent<CharacterController>().slopeLimit = slopeLimit;
                Physics.SyncTransforms();
                Assert.That(WalkableSupport.Contains(mover, root.transform.position + Vector3.up*.1f), Is.EqualTo(expected));
                surface.isTrigger = true; Physics.SyncTransforms();
                Assert.IsFalse(WalkableSupport.Contains(mover,root.transform.position + Vector3.up*.1f));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(root); }
        }

        [Test]
        public void FloorAndRopeReadOneJumpBufferAndAirSettingsChangeAllDevices()
        {
            var input = GameSettings.Base.Input;
            var air = GameSettings.Base.AirControl;
            string savedInput = JsonUtility.ToJson(input), savedAir = JsonUtility.ToJson(air);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"jumpInputBuffer\":0.08}",input);
                JsonUtility.FromJsonOverwrite("{\"speed\":2.25}",air);
                Assert.That(GameSettings.Tightrope.RopeMovement.JumpInputBuffer, Is.EqualTo(.08f));
                Assert.That(GameSettings.Poles.AirControlSpeed, Is.EqualTo(2.25f));
                Assert.That(GameSettings.Trampoline.AirControlSpeed, Is.EqualTo(2.25f));
            }
            finally { JsonUtility.FromJsonOverwrite(savedInput,input); JsonUtility.FromJsonOverwrite(savedAir,air); }
        }
    }
}
