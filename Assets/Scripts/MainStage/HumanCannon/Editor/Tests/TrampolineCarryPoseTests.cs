using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Damage;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom.Pursuit;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("November11CommonFix")]
    public class TrampolineCarryPoseTests
    {
        [TestCase(false)]
        [TestCase(true)]
        [Category("CannonTransportRecovery")]
        public void LostCarrierChoosesTheClosestSurvivorWithAUsablePlacement(bool closestHasFloor)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var origin = new Vector3(15000f, 100f, 15000f);
            var root = new GameObject("Recovery trampoline");
            var floor = new GameObject("Survivor ground");
            var close = new GameObject("Closest survivor");
            var further = new GameObject("Further survivor");
            var all = (List<NetworkPlayer>)NetworkPlayer.All;
            var previous = all.ToArray();
            var closest = close.AddComponent<NetworkPlayer>();
            var teammate = further.AddComponent<NetworkPlayer>();
            var spawned = typeof(NetworkPlayer).GetProperty("IsSpawned");
            try
            {
                root.transform.position = origin;
                var body = root.AddComponent<BoxCollider>();
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                body.center = Vector3.up * .3f;
                body.size = new Vector3(.5f, .4f, .5f);
                typeof(HumanCannonTrampoline).GetField("carryBody", members).SetValue(trampoline, body);
                typeof(HumanCannonTrampoline).GetField("initialPosition", members).SetValue(trampoline, origin);
                typeof(HumanCannonTrampoline).GetField("initialRotation", members).SetValue(trampoline, Quaternion.identity);
                closest.transform.position = origin + Vector3.right * 4f;
                teammate.transform.position = origin + Vector3.right * 10f;
                Vector3 expected = (closestHasFloor ? closest : teammate).transform.position;
                floor.transform.position = expected - Vector3.up * .1f;
                floor.AddComponent<BoxCollider>().size = new Vector3(3f, .2f, 3f);
                spawned.SetValue(closest, true);
                spawned.SetValue(teammate, true);
                all.Clear(); all.Add(teammate); all.Add(closest);
                Physics.SyncTransforms();

                var args = new object[] { Vector3.zero };
                var recover = typeof(HumanCannonTrampoline).GetMethod("TryFindSurvivorGround", members);
                Assert.IsTrue((bool)recover.Invoke(trampoline, args));
                Assert.That(Vector3.Distance((Vector3)args[0], expected), Is.LessThan(.01f));
            }
            finally
            {
                spawned.SetValue(closest, false);
                spawned.SetValue(teammate, false);
                all.Clear(); all.AddRange(previous);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(close);
                Object.DestroyImmediate(further);
                Physics.SyncTransforms();
            }
        }

        [TestCase(DeparturePhase.Waiting)]
        [TestCase(DeparturePhase.Moving)]
        [TestCase(DeparturePhase.Arrived)]
        [TestCase(DeparturePhase.Returning)]
        [Category("CannonTransportRecovery")]
        public void DisconnectedTransportRiderLeavesTrampolineOnReachableGround(DeparturePhase phase)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var origin = new Vector3(15100f, 100f, 15100f);
            var root = new GameObject("Trampoline above deck");
            var routeRoot = new GameObject("Recovery test transport");
            var platform = new GameObject("Moving deck");
            var start = new GameObject("Deck start");
            var end = new GameObject("Deck end");
            var floor = new GameObject("Reachable survivor ground");
            var buddyRoot = new GameObject("Remaining player");
            var sessionRoot = new GameObject("Unstarted recovery network");
            sessionRoot.SetActive(false);
            var buddy = buddyRoot.AddComponent<NetworkPlayer>();
            var spawned = typeof(NetworkPlayer).GetProperty("IsSpawned");
            var current = typeof(HumanCannonRouteSystem).GetProperty(nameof(HumanCannonRouteSystem.Current));
            var previousRoute = current.GetValue(null);
            var all = (List<NetworkPlayer>)NetworkPlayer.All;
            var previous = all.ToArray();
            var route = routeRoot.AddComponent<HumanCannonRouteSystem>();
            var type = typeof(HumanCannonTrampoline);
            try
            {
                var manager = sessionRoot.AddComponent<NetworkManager>();
                var session = sessionRoot.AddComponent<NetworkSessionManager>();
                typeof(NetworkSessionManager).GetField("_networkManager", members).SetValue(session, manager);
                var transport = routeRoot.AddComponent<HumanCannonTransport>();
                typeof(HumanCannonTransport).GetField("platform", members).SetValue(transport, platform.transform);
                typeof(HumanCannonTransport).GetField("start", members).SetValue(transport, start.transform);
                typeof(HumanCannonTransport).GetField("destination", members).SetValue(transport, end.transform);
                start.transform.position = origin - Vector3.forward * 8f;
                end.transform.position = origin + Vector3.forward * 8f;
                platform.transform.position = origin - Vector3.up * .1f;
                platform.AddComponent<BoxCollider>().size = new Vector3(4f, .2f, 4f);
                route.enabled = false;
                route.ConfigurePractice(transport, origin.y - 10f);
                current.SetValue(null, null);
                typeof(HumanCannonRouteSystem).GetMethod("OnEnable", members).Invoke(route, null);
                var state = route.TransportState(0);
                if (phase != DeparturePhase.Waiting)
                    Assert.IsTrue(state.Receive(1, phase, CannonSeatState.Nobody, 0d, 5f));

                root.transform.position = origin + Vector3.up * .05f;
                var body = root.AddComponent<BoxCollider>();
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                body.size = new Vector3(.5f, .4f, .5f);
                body.center = Vector3.up * .3f;
                type.GetField("carryBody", members).SetValue(trampoline, body);
                type.GetField("initialPosition", members).SetValue(trampoline, start.transform.position);
                type.GetField("session", members).SetValue(trampoline, session);
                type.GetField("initialRotation", members).SetValue(trampoline, Quaternion.identity);
                var ownership = (CannonSeatState)type.GetField("ownership", members).GetValue(trampoline);
                Assert.IsTrue(ownership.TryApply(CannonAction.Board, 99, ownership.Revision));

                var safe = origin + Vector3.right * 9f;
                buddy.transform.position = safe;
                floor.transform.position = safe - Vector3.up * .1f;
                floor.AddComponent<BoxCollider>().size = new Vector3(4f, .2f, 4f);
                spawned.SetValue(buddy, true);
                all.Clear(); all.Add(buddy);
                Physics.SyncTransforms();

                type.GetMethod("ReleaseCarrierToGround", members).Invoke(trampoline, new object[] { null });
                Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
                Vector3 settled = (Vector3)type.GetProperty("SharedPosition", members).GetValue(trampoline);
                Assert.That(Vector3.Distance(settled, phase == DeparturePhase.Waiting ? origin : safe), Is.LessThan(.01f));
            }
            finally
            {
                spawned.SetValue(buddy, false);
                all.Clear(); all.AddRange(previous);
                typeof(HumanCannonRouteSystem).GetMethod("OnDisable", members).Invoke(route, null);
                current.SetValue(null, previousRoute);
                foreach (var item in new[] { root, routeRoot, platform, start, end, floor, buddyRoot, sessionRoot })
                    Object.DestroyImmediate(item);
                Physics.SyncTransforms();
            }
        }

        [Test, Category("CommonProgressionRegression")]
        public void DuplicateOwnershipReplyDoesNotRestartDropOrChangeCarrier()
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Remote trampoline"); root.SetActive(false);
            var origin = new Vector3(12800, 100, 12800);
            root.transform.position = origin + Vector3.up * 2;
            var trampoline = root.AddComponent<HumanCannonTrampoline>();
            var type = typeof(HumanCannonTrampoline);
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(1, .5f, 1);
            type.GetField("carryBody", members).SetValue(trampoline, collider);
            var receive = type.GetMethod("HandleStateMessage", members);
            void Deliver(uint revision, ulong carrier, Vector3 point)
            {
                using var writer = new FastBufferWriter(40, Allocator.Temp);
                writer.WriteValueSafe(revision); writer.WriteValueSafe(carrier);
                writer.WriteValueSafe(point); writer.WriteValueSafe(Quaternion.identity);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                receive.Invoke(trampoline, new object[] { NetworkManager.ServerClientId, reader });
            }
            try
            {
                Deliver(3, CannonSeatState.Nobody, origin);
                Assert.That(type.GetField("hasOwnershipSnapshot", members).GetValue(trampoline), Is.True);
                Assert.That(type.GetField("dropping", members).GetValue(trampoline), Is.True);
                type.GetField("dropSpeed", members).SetValue(trampoline, 4f);
                Deliver(3, CannonSeatState.Nobody, origin);
                Assert.That(type.GetField("dropSpeed", members).GetValue(trampoline), Is.EqualTo(4f));
                Deliver(3, 2, origin);
                Deliver(4, 2, new Vector3(float.NaN, 0, 0));
                Assert.That(trampoline.Carrier, Is.EqualTo(CannonSeatState.Nobody));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test, Category("CommonProgressionRegression")]
        public void GroundBelowIsNotADropPlacementWhenTheFrameOverlapsAPillar()
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Trampoline drop"); root.SetActive(false);
            var floor = new GameObject("Floor");
            var pillar = new GameObject("Pillar next to drop");
            var origin = new Vector3(12800, 100, 12800);
            try
            {
                root.transform.position = origin + Vector3.up * 2;
                var body = root.AddComponent<BoxCollider>(); body.center = Vector3.up * .3f; body.size = new Vector3(3, .6f, 3);
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                var type = typeof(HumanCannonTrampoline);
                root.SetActive(true);
                body.center = Vector3.up * .3f; body.size = new Vector3(3, .6f, 3);
                type.GetField("carryBody", members).SetValue(trampoline, body);
                floor.transform.position = origin + Vector3.down * .25f;
                floor.AddComponent<BoxCollider>().size = new Vector3(10, .5f, 10);
                pillar.transform.position = origin + new Vector3(1f, 1, 0);
                pillar.AddComponent<BoxCollider>().size = new Vector3(.5f, 2, .5f);
                Physics.SyncTransforms();
                Assert.That(HumanCannonCarry.CanMove(root.transform, body, origin, Quaternion.identity, null, false), Is.False);
                var check = type.GetMethod("TryDropHere", members);
                Assert.That(check.Invoke(trampoline, new object[] { null, Vector3.zero }), Is.False);
                var search = type.GetMethod("TryFindNearbySidePlacement", members);
                var args = new object[] { root.transform.position, root.transform.rotation, null, Vector3.zero, false, true };
                Assert.That(search.Invoke(trampoline, args), Is.True, "The clear floor beside the blocked footprint is reachable.");
                Assert.That(HumanCannonCarry.CanMove(root.transform, body, (Vector3)args[3], root.transform.rotation, null, false), Is.True);
                root.transform.position += Vector3.left * 2;
                Assert.That(check.Invoke(trampoline, new object[] { null, Vector3.zero }), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(floor); Object.DestroyImmediate(pillar);
                Physics.SyncTransforms();
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WaitingRoomDropFindsADiagonalLandingButCannotSlideThroughAWall(bool wall)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Trampoline diagonal drop");
            var floor = new GameObject("Diagonal landing support");
            var barrier = new GameObject("Blocked carry path");
            try
            {
                var origin = new Vector3(13200, 100, 13200);
                root.SetActive(false);
                root.transform.position = origin + Vector3.up;
                var body = root.AddComponent<BoxCollider>();
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                var type = typeof(HumanCannonTrampoline);
                root.SetActive(true);
                body.center = Vector3.up * .2f;
                body.size = new Vector3(.4f, .4f, .4f);
                type.GetField("carryBody", members).SetValue(trampoline, body);

                Vector3 offset = (Vector3.right + Vector3.forward).normalized * (GameSettings.Trampoline.GrabDistance / 2f);
                floor.transform.position = origin + offset - Vector3.up * .25f;
                floor.AddComponent<BoxCollider>().size = new Vector3(.5f, .5f, .5f);
                if (wall)
                {
                    barrier.transform.position = origin + Vector3.right * (offset.x * .5f) + Vector3.up;
                    barrier.AddComponent<BoxCollider>().size = new Vector3(.1f, 4f, 6f);
                }
                Physics.SyncTransforms();

                var oldSearch = type.GetMethod("TryFindNearbySidePlacement", members);
                var oldArgs = new object[] { root.transform.position, root.transform.rotation, null, Vector3.zero, false, true };
                Assert.That(oldSearch.Invoke(trampoline, oldArgs), Is.False);
                var search = type.GetMethod("TryFindWaitingRoomDrop", members);
                var args = new object[] { root.transform.position, root.transform.rotation, null, Vector3.zero };
                Assert.That(search.Invoke(trampoline, args), Is.EqualTo(!wall));
                if (!wall) Assert.That(Vector3.Distance((Vector3)args[3], origin + offset), Is.LessThan(.02f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(barrier);
                Physics.SyncTransforms();
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        [Category("CommonProgressionRegression")]
        public void CargoColumnRejectsLocalSweepButDoesNotFreezeAValidRemoteArrival(bool local)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var origin = new Vector3(12800, 100, 12800);
            var root = new GameObject("Carried trampoline");
            var actor = new GameObject("Carrier");
            var column = new GameObject("Cargo column");
            try
            {
                actor.transform.position = origin;
                var player = actor.AddComponent<NetworkPlayer>();
                var mover = actor.GetComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", members).Invoke(mover, null);
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                typeof(NetworkPlayer).GetProperty("IsOwner").SetValue(player, local);
                mover.Simulated = local;
                root.transform.position = origin + Vector3.forward * 1.2f;
                var collider = root.AddComponent<BoxCollider>();
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                var handle = new GameObject("Handle").transform;
                handle.SetParent(root.transform, false);
                handle.localPosition = Vector3.back * 1.2f;
                typeof(HumanCannonTrampoline).GetField("handle", members).SetValue(trampoline, handle);
                typeof(HumanCannonTrampoline).GetField("halfSize", members).SetValue(trampoline, Vector2.one * .5f);
                var ownership = new CannonSeatState();
                Assert.IsTrue(ownership.TryApply(CannonAction.Board, 0, 0));
                typeof(HumanCannonTrampoline).GetField("ownership", members).SetValue(trampoline, ownership);
                var follow = typeof(HumanCannonTrampoline).GetMethod("UpdateCarryPose", members);
                follow.Invoke(trampoline, new object[] { player, true, false });
                var startingPose = root.transform.position;
                column.transform.position = startingPose + new Vector3(1, 0, 0);
                column.AddComponent<BoxCollider>().size = new Vector3(.2f, 4, 1.5f);

                // An observer can receive only the samples before and after the owner's turn around a column.
                actor.transform.position = origin + new Vector3(2, -.4f, 0);
                Physics.SyncTransforms();
                var arrival = startingPose + new Vector3(2, -.4f, 0);
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, collider, arrival, Quaternion.identity, player, true));
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, collider, arrival, Quaternion.identity, player, false));
                follow.Invoke(trampoline, new object[] { player, false, false });
                Assert.That(root.transform.position.x, Is.EqualTo(local ? startingPose.x : arrival.x).Within(.002f));
                Assert.That(actor.transform.position.y, Is.EqualTo(origin.y - .4f).Within(.002f),
                    "The load must not lift a falling player back to the last carry height.");

                if (!local)
                {
                    var previousSurface = (Vector3)typeof(HumanCannonTrampoline).GetField("previousSurface", members).GetValue(trampoline);
                    Assert.AreEqual(trampoline.Surface, previousSurface,
                        "A remote correction must not generate rebound contacts across the skipped segment.");
                    actor.transform.position = origin + Vector3.right;
                    Physics.SyncTransforms();
                    follow.Invoke(trampoline, new object[] { player, false, false });
                    Assert.That(root.transform.position.x, Is.EqualTo(arrival.x).Within(.002f),
                        "A sampled pose inside the solid column must still be rejected.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(column);
            }
        }

        [TestCase("WaitingRoom")]
        [TestCase("escape_route")]
        [Category("CommonProgressionRegression")]
        public void TraversalCoreClearsTheRimAndPursuerButPlacementChecksTheFullShape(string sceneName)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var scene = SceneManager.GetActiveScene();
            string previousSceneName = scene.name;
            scene.name = sceneName + "_TrampolineTest";
            var root = new GameObject("Trampoline");
            var floor = new GameObject("Floor");
            var pillar = new GameObject("Pillar");
            var pursuer = new GameObject("Pursuer");
            try
            {
                var origin = new Vector3(12000, 100, 12000);
                root.transform.position = origin + Vector3.up;
                root.SetActive(false);
                var fullBody = root.AddComponent<BoxCollider>();
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                trampoline.enabled = false;
                root.SetActive(true);
                var ownership = (CannonSeatState)typeof(HumanCannonTrampoline)
                    .GetField("ownership", members).GetValue(trampoline);
                var refresh = typeof(HumanCannonTrampoline).GetMethod("RefreshCarryBody", members);
                Assert.IsTrue(ownership.TryApply(CannonAction.Board, 42, ownership.Revision));
                refresh.Invoke(trampoline, new object[] { null });
                var core = (BoxCollider)typeof(HumanCannonTrampoline).GetProperty("MoveCheckBody", members).GetValue(trampoline);
                Assert.That(core, Is.Not.SameAs(fullBody));
                Assert.That(core.bounds.size.x, Is.EqualTo(1f).Within(.01f));
                Assert.IsTrue(fullBody.isTrigger);

                floor.AddComponent<BoxCollider>().size = new Vector3(10, .2f, 10);
                floor.transform.position = origin - Vector3.up * .1f;
                pillar.AddComponent<BoxCollider>().size = new Vector3(.3f, 3f, .3f);
                var destination = root.transform.position + Vector3.forward;
                pillar.transform.position = destination + Vector3.right * 1.25f;
                Physics.SyncTransforms();

                var findPlacement = typeof(HumanCannonTrampoline).GetMethod("TryFindPlacement", members);
                bool CanPlace() => (bool)findPlacement.Invoke(trampoline,
                    new object[] { destination, Quaternion.identity, null, Vector3.zero, false });
                bool CanCarry() => HumanCannonCarry.CanMove(root.transform, core, destination, Quaternion.identity, null);
                Assert.IsTrue(CanCarry(), "The rim may overlap the pillar during carry.");
                Assert.IsFalse(CanPlace(), "The same pillar obstructs the full landing footprint.");
                pillar.transform.position = destination;
                Physics.SyncTransforms();
                Assert.IsFalse(CanCarry(), "The core must still stop at the pillar.");

                pillar.SetActive(false);
                pursuer.SetActive(false);
                var capsule = pursuer.AddComponent<CapsuleCollider>();
                capsule.center = Vector3.up * .9f;
                capsule.height = 1.8f;
                capsule.radius = .28f;
                if (sceneName == "WaitingRoom") pursuer.AddComponent<PursuitActor>();
                else
                {
                    pursuer.AddComponent<EscapeUnderstageChase>().enabled = false;
                    pursuer.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                }
                pursuer.transform.position = destination - Vector3.up;
                pursuer.SetActive(true);
                Physics.SyncTransforms();
                Assert.IsTrue(CanCarry(), "The carried core must not obstruct the pursuer.");
                Assert.IsFalse(CanPlace(), "The full footprint must not be placed on the pursuer.");
                Assert.IsFalse(PursuitSeparation.TryResolve(capsule, pursuer.transform.position, pursuer.transform, 1f,
                    false, out _), "The carried trampoline must not displace the pursuer.");

                pursuer.SetActive(false);
                Physics.SyncTransforms();
                Assert.IsTrue(CanPlace());
                Assert.IsTrue(ownership.TryApply(CannonAction.Release, 42, ownership.Revision));
                refresh.Invoke(trampoline, new object[] { null });
                Assert.IsFalse(fullBody.isTrigger, "A settled trampoline restores its original collider.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(pillar);
                Object.DestroyImmediate(pursuer);
                scene.name = previousSceneName;
            }
        }

        [Test, Category("TrampolineLayerRegression")]
        public void MarkedLimbCannotObstructCarryOrBecomeDropSupportWithStaleLayer()
        {
            var root = new GameObject("Trampoline");
            var limb = new GameObject("Detached limb");
            var floor = new GameObject("Floor");
            try
            {
                root.SetActive(false);
                root.layer = LayerMask.NameToLayer(CurtainCall.Settings.GamePhysicsLayers.Trampoline);
                root.transform.position = new Vector3(12000, 100, 12000);
                var body = root.AddComponent<BoxCollider>();
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                trampoline.enabled = false;
                root.SetActive(true);
                limb.AddComponent<DismemberedLimb>();
                limb.layer = 0;
                limb.transform.position = root.transform.position + Vector3.forward;
                limb.AddComponent<BoxCollider>();
                floor.transform.position = limb.transform.position + Vector3.down * 2f;
                var support = floor.AddComponent<BoxCollider>();
                Physics.SyncTransforms();
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, limb.transform.position, Quaternion.identity, null, sweep: true));
                object[] args = { limb.transform.position + Vector3.up, Vector3.zero, false };
                Assert.IsTrue((bool)typeof(HumanCannonTrampoline).GetMethod("TryFindGround",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trampoline, args));
                Assert.That(((Vector3)args[1]).y, Is.EqualTo(support.bounds.max.y).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(limb);
                Object.DestroyImmediate(floor);
            }
        }

        [Test, Category("CommonProgressionRegression"), Category("TrampolineLayerRegression")]
        public void ProjectMatrixIgnoresOnlyTheTwoRequestedLayerPairs()
        {
            int trampoline = LayerMask.NameToLayer(CurtainCall.Settings.GamePhysicsLayers.Trampoline);
            int limb = LayerMask.NameToLayer(CurtainCall.Settings.GamePhysicsLayers.DetachedLimb);
            int hazard = LayerMask.NameToLayer(CurtainCall.Settings.GamePhysicsLayers.TrickHazard);
            Assert.GreaterOrEqual(trampoline, 8); Assert.GreaterOrEqual(limb, 8); Assert.GreaterOrEqual(hazard, 8);
            for (int first = 0; first < 32; first++) for (int second = 0; second < 32; second++)
            {
                bool ignore = first == trampoline && (second == limb || second == hazard)
                    || second == trampoline && (first == limb || first == hazard);
                Assert.AreEqual(ignore, Physics.GetIgnoreLayerCollision(first, second), $"Layer pair {first}/{second}");
            }
        }

        [TestCase("DismemberedLimb"), TestCase("TrickHazard")]
        [Category("CommonProgressionRegression"), Category("TrampolineLayerRegression")]
        public void CarryAndDropQueriesFollowTheProjectMatrixAndStillStopAtWalls(string ignoredLayer)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Layered trampoline query"); root.SetActive(false);
            var obstacle = new GameObject("Ignored query obstacle"); var floor = new GameObject("Preserved query floor");
            int trampolineLayer = LayerMask.NameToLayer(CurtainCall.Settings.GamePhysicsLayers.Trampoline);
            int otherLayer = LayerMask.NameToLayer(ignoredLayer);
            bool previous = Physics.GetIgnoreLayerCollision(trampolineLayer, otherLayer);
            try
            {
                root.layer = trampolineLayer; obstacle.layer = otherLayer;
                root.transform.position = new Vector3(12000, 100, 12000);
                var body = root.AddComponent<BoxCollider>(); body.size = Vector3.one * .5f;
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                trampoline.enabled = false; root.SetActive(true); // Keep real collision shapes active without claiming the gameplay singleton.
                var wall = obstacle.AddComponent<BoxCollider>(); wall.size = Vector3.one;
                obstacle.transform.position = root.transform.position + Vector3.forward;
                var ground = floor.AddComponent<BoxCollider>(); ground.size = new Vector3(5, .2f, 5);
                floor.transform.position = root.transform.position + Vector3.down * 2;
                Physics.SyncTransforms();
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, obstacle.transform.position, Quaternion.identity, null));
                obstacle.layer = 0; Physics.SyncTransforms();
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, obstacle.transform.position, Quaternion.identity, null));
                obstacle.layer = otherLayer; obstacle.transform.position = root.transform.position; Physics.SyncTransforms();
                object[] args = { root.transform.position + Vector3.up * 2, Vector3.zero, false };
                var findGround = typeof(HumanCannonTrampoline).GetMethod("TryFindGround", members);
                Assert.IsTrue((bool)findGround.Invoke(trampoline, args));
                Assert.That(((Vector3)args[1]).y, Is.EqualTo(ground.bounds.max.y).Within(.001f));
                Physics.IgnoreLayerCollision(trampolineLayer, otherLayer, false);
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, obstacle.transform.position, Quaternion.identity, null));
                args[1] = Vector3.zero; Assert.IsTrue((bool)findGround.Invoke(trampoline, args));
                Assert.That(((Vector3)args[1]).y, Is.EqualTo(wall.bounds.max.y).Within(.001f));
            }
            finally
            {
                Physics.IgnoreLayerCollision(trampolineLayer, otherLayer, previous);
                Object.DestroyImmediate(root); Object.DestroyImmediate(obstacle); Object.DestroyImmediate(floor);
            }
        }

        [TestCase(false, false), TestCase(true, false), TestCase(false, true), TestCase(true, true)]
        [Category("CommonProgressionRegression"), Category("TrampolineLayerRegression")]
        public void IgnoredPropsPassThroughAllTrampolineShapesAndStillLandOnTheFloor(bool createdAfterTrampoline, bool hazard)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var physics = scene.GetPhysicsScene();
            var oldTrampoline = HumanCannonTrampoline.Current;
            typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, null);
            GameObject Make(string name)
            {
                var go = new GameObject(name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }
            try
            {
                Assert.That(physics, Is.Not.EqualTo(Physics.defaultPhysicsScene), "Never simulate the user's open scene.");
                var floor = Make("Solid floor").AddComponent<BoxCollider>();
                floor.transform.position = Vector3.down * 2; floor.size = new Vector3(10, .5f, 10);
                var limb = Make("Detached limb");
                var shape = limb.AddComponent<BoxCollider>(); shape.size = Vector3.one * .2f;
                var rb = limb.AddComponent<Rigidbody>(); rb.useGravity = true;
                limb.transform.position = Vector3.up * 3;
                void RegisterLimb()
                {
                    if (hazard) { CurtainCall.Settings.GamePhysicsLayers.AssignHierarchy(limb.transform, CurtainCall.Settings.GamePhysicsLayers.TrickHazard); return; }
                    var marker = limb.AddComponent<DismemberedLimb>();
                    var active = (System.Collections.Generic.List<DismemberedLimb>)typeof(DismemberedLimb)
                        .GetField("active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    if (!active.Contains(marker)) typeof(DismemberedLimb).GetMethod("OnEnable", members).Invoke(marker, null);
                }
                if (!createdAfterTrampoline) RegisterLimb();
                var root = Make("Trampoline"); root.SetActive(false);
                var main = root.AddComponent<BoxCollider>();
                var child = Make("Child trampoline frame"); child.transform.SetParent(root.transform);
                var frame = child.AddComponent<BoxCollider>(); frame.size = new Vector3(4, .1f, 4); frame.center = Vector3.up * .6f;
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                root.SetActive(true);
                if (HumanCannonTrampoline.Current != trampoline)
                    typeof(HumanCannonTrampoline).GetMethod("OnEnable", members).Invoke(trampoline, null);
                if (createdAfterTrampoline) RegisterLimb();
                var ownership = (CannonSeatState)typeof(HumanCannonTrampoline).GetField("ownership", members).GetValue(trampoline);
                // Idle, carried, released and re-enabled shapes use the same physical exclusion.
                foreach (ulong actor in new[] { CannonSeatState.Nobody, 12UL, CannonSeatState.Nobody })
                {
                    ownership.Receive(ownership.Revision + 1, actor);
                    frame.enabled = false; frame.enabled = true;
                    Physics.SyncTransforms();
                    Assert.IsTrue(Physics.GetIgnoreLayerCollision(main.gameObject.layer, shape.gameObject.layer));
                    Assert.IsTrue(Physics.GetIgnoreLayerCollision(frame.gameObject.layer, shape.gameObject.layer));
                    Assert.IsFalse(Physics.GetIgnoreCollision(main, shape), "No per-collider override should hide the project matrix setting.");
                    Assert.IsFalse(Physics.GetIgnoreLayerCollision(floor.gameObject.layer, shape.gameObject.layer));
                }
                for (int i = 0; i < 100; i++) physics.Simulate(.02f);
                Assert.That(rb.position.y, Is.LessThan(-1f), "The limb must pass through, not land on the trampoline.");
                Assert.That(rb.position.y, Is.GreaterThan(-1.8f), "Ordinary floor collision must remain enabled.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, oldTrampoline);
            }
        }

        [Test]
        [Category("CommonProgressionRegression")]
        public void CarriedTrampolineClearsRingHeightButStopsAtWallAndLowCeiling()
        {
            var root = new GameObject("Carry step probe");
            var terrain = new GameObject("Ring lip");
            var ceiling = new GameObject("Overhead obstacle");
            try
            {
                root.transform.position = new Vector3(12000f, 100f, 12000f);
                var body = root.AddComponent<BoxCollider>();
                body.center = Vector3.up * .25f;
                body.size = new Vector3(1f, .5f, 1f);
                var step = terrain.AddComponent<BoxCollider>();
                terrain.transform.position = root.transform.position + Vector3.forward;
                step.center = Vector3.up * .3f;
                step.size = new Vector3(4f, .6f, 1f);
                var roof = ceiling.AddComponent<BoxCollider>();
                roof.size = new Vector3(2f, .1f, 2f);
                ceiling.SetActive(false);
                var destination = root.transform.position + Vector3.forward * .5f;
                var method = typeof(HumanCannonCarry).GetMethod("TryCarryOverStep", BindingFlags.Static | BindingFlags.NonPublic);
                bool Lift(out Vector3 position)
                {
                    object[] args = { root.transform, body, destination, null, 0f, .65f, Vector3.zero };
                    bool moved = (bool)method.Invoke(null, args);
                    position = (Vector3)args[6];
                    return moved;
                }
                Physics.SyncTransforms();
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, destination, Quaternion.identity, null));
                Assert.IsTrue(Lift(out var lifted));
                Assert.That(lifted.y, Is.EqualTo(destination.y + .65f).Within(.001f));

                ceiling.transform.position = root.transform.position + Vector3.up * .8f;
                ceiling.SetActive(true);
                Physics.SyncTransforms();
                Assert.IsFalse(Lift(out _), "The lifting segment must stop at the ceiling.");
                ceiling.SetActive(false);
                step.center = Vector3.up;
                step.size = new Vector3(4f, 2f, 1f);
                Physics.SyncTransforms();
                Assert.IsFalse(Lift(out _), "The raised horizontal segment must stop at a full wall.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(terrain);
                Object.DestroyImmediate(ceiling);
            }
        }

        [Test]
        public void CarriedBodyCannotSkipAcrossAThinWallBetweenClearEndpoints()
        {
            var root = new GameObject("Carry sweep probe");
            var wall = new GameObject("Thin wall");
            try {
                root.transform.position = new Vector3(12000, 100, 12000);
                var body = root.AddComponent<BoxCollider>(); body.size = new Vector3(1, .5f, 1);
                var obstacle = wall.AddComponent<BoxCollider>(); obstacle.size = new Vector3(4, 4, .05f);
                wall.transform.position = root.transform.position + Vector3.forward * 2;
                Physics.SyncTransforms();
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.forward, Quaternion.identity, null));
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.forward * 4, Quaternion.identity, null));
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.forward * 4, Quaternion.identity, null, false));
            } finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wall); }
        }

        [Test]
        [Category("CommonProgressionRegression")]
        public void CarryIgnoresDetachedLimbsAndCanLeaveAnExistingContact()
        {
            var root = new GameObject("Carry overlap probe");
            var obstacle = new GameObject("Carry obstruction");
            try
            {
                root.transform.position = new Vector3(12000f, 100f, 12000f);
                var body = root.AddComponent<BoxCollider>();
                var shape = obstacle.AddComponent<BoxCollider>();
                shape.size = new Vector3(.6f, 2f, 3f);
                obstacle.transform.position = root.transform.position + Vector3.right * .55f;
                Physics.SyncTransforms();
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.left, Quaternion.identity, null));
                var check = typeof(HumanCannonCarry).GetMethod("CheckMove", BindingFlags.NonPublic | BindingFlags.Static);
                bool Try(Vector3 travel) => (bool)check.Invoke(null, new object[] { root.transform, body,
                    root.transform.position + travel, Quaternion.identity, null, true, 0f, null, true, Vector3.zero });
                Assert.IsTrue(Try(Vector3.left));
                Assert.IsFalse(Try(Vector3.right));
                obstacle.AddComponent<DismemberedLimb>();
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.right, Quaternion.identity, null));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(obstacle); }
        }

        [Test]
        public void RotatingCarriedBodyCannotSweepThroughAnObstacle()
        {
            var root = new GameObject("Turning carry probe");
            var wall = new GameObject("Corner obstacle");
            try {
                root.transform.position = new Vector3(12000, 100, 12000);
                var body = root.AddComponent<BoxCollider>(); body.size = new Vector3(4, .5f, .3f);
                wall.AddComponent<BoxCollider>().size = Vector3.one * .2f;
                wall.transform.position = root.transform.position + new Vector3(1.3f, 0, -1.3f);
                Physics.SyncTransforms();
                var turn = Quaternion.Euler(0, 90, 0);
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position, turn, null, false));
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, root.transform.position, turn, null));
            } finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wall); }
        }

        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        public void CardinalFacing_PutsCenterInFrontAndHandleAtCarrier(float x, float z)
        {
            var carrier = new Vector3(3, 2, 7);
            var forward = new Vector3(x, 0, z);
            var handle = new Vector3(-2.8f, 0, 0);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier, forward, handle, out var position, out var rotation));
            Near(position, carrier + forward * 2.8f);
            Near(position + rotation * handle, carrier);
            Near(rotation * Vector3.up, Vector3.up);
        }

        [Test]
        public void DifferentApproachPositions_KeepTheHandleDefinedDistance()
        {
            var firstCarrier = new Vector3(-13, 4, 11);
            var secondCarrier = new Vector3(7, -2, -8);
            var forward = new Vector3(1, 5, 1);
            var handle = new Vector3(-2.8f, 0, 0);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(firstCarrier, forward, handle, out var first, out _));
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(secondCarrier, forward, handle, out var second, out _));
            Vector3 expected = new Vector3(1, 0, 1).normalized * 2.8f;
            Near(first - firstCarrier, expected);
            Near(second - secondCarrier, expected);
        }

        [Test]
        public void JumpHeight_MovesDeviceBySameHeightWithoutChangingRotation()
        {
            var carrier = new Vector3(1, 0, 2);
            var handle = new Vector3(-2.8f, 0.4f, 0);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier, Vector3.forward, handle, out var grounded, out var groundedRotation));
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier + Vector3.up * 1.5f, Vector3.forward, handle,
                out var airborne, out var airborneRotation));
            Near(airborne - grounded, Vector3.up * 1.5f);
            Assert.That(airborneRotation, Is.EqualTo(groundedRotation));
            Near(airborne + airborneRotation * handle, carrier + Vector3.up * 1.5f);
        }

        [Test]
        public void OffsetHandleAndPitchedForward_KeepAnchorExactAndDeviceLevel()
        {
            var carrier = new Vector3(4, 2, -3);
            var handle = new Vector3(-2, 0.3f, 1.5f);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier, new Vector3(-2, 20, 3), handle,
                out var position, out var rotation));
            Near(position + rotation * handle, carrier);
            Near(rotation * Vector3.up, Vector3.up);
            Near(Vector3.ProjectOnPlane(position - carrier, Vector3.up), new Vector3(-2, 0, 3).normalized * 2.5f);
        }

        [Test]
        public void MissingHorizontalDirectionOrNonFiniteVectors_AreRejected()
        {
            var handle = new Vector3(-2.8f, 0, 0);
            Invalid(Vector3.zero, Vector3.zero, handle);
            Invalid(Vector3.zero, Vector3.up, handle);
            Invalid(Vector3.zero, Vector3.forward, Vector3.zero);
            Invalid(Vector3.zero, Vector3.forward, Vector3.up);
            Invalid(new Vector3(float.NaN, 0, 0), Vector3.forward, handle);
            Invalid(Vector3.zero, new Vector3(0, float.PositiveInfinity, 1), handle);
            Invalid(Vector3.zero, Vector3.forward, new Vector3(-2.8f, 0, float.NegativeInfinity));
        }

        static void Near(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));

        static void Invalid(Vector3 carrier, Vector3 forward, Vector3 handle)
        {
            Assert.IsFalse(TrampolineCarryPose.TryCalculate(carrier, forward, handle, out var position, out var rotation));
            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(rotation, Is.EqualTo(Quaternion.identity));
        }
    }
}
