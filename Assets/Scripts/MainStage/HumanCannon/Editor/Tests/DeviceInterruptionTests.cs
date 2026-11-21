using System;
using System.Collections.Generic;
using System.Reflection;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("CommonProgressionRegression"), Category("DeviceInterruptionAudit")]
    public sealed class DeviceInterruptionTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static object Get(object o, string name) => o.GetType().GetField(name, F).GetValue(o);
        static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);

        [Test, Combinatorial]
        public void DelayedCannonPacketCannotMoveAnUnavailableOwner(
            [Values(PlayerState.Normal, PlayerState.Fallen, PlayerState.Retired, PlayerState.Arrived, PlayerState.EscapeCaptured)] PlayerState state,
            [Values(CannonAction.Board, CannonAction.Launch, CannonAction.Cancel, CannonAction.Sync)] CannonAction action,
            [Values(true, false)] bool spawned)
        {
            var root = new GameObject("Late cannon state"); root.SetActive(false);
            var stationObject = new GameObject("Late packet station");
            stationObject.transform.position = new Vector3(17000, 100, 17000);
            var origin = new Vector3(17020, 80, 17020);
            root.transform.position = origin;
            NetworkPlayer actor = null;
            try
            {
                var station = stationObject.AddComponent<HumanCannonStation>();
                var cannon = root.AddComponent<HumanCannonSystem>();
                cannon.ConfigurePractice(new[] { station }, -10); Call(cannon, "ResetSeats");
                actor = root.AddComponent<NetworkPlayer>();
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, spawned);
                var mover = root.GetComponent<PlayerMover>(); Call(mover, "Awake");
                Set(Get(actor, "state"), "m_InternalValue", state);
                var pilot = root.AddComponent<HumanCannonPilot>();
                Set(pilot, "player", actor); Set(pilot, "mover", mover); Set(pilot, "system", cannon);
                Set(pilot, "previousSimulation", true);
                Set(cannon, "boundPlayer", actor); Set(cannon, "pilot", pilot);
                mover.Simulated = state == PlayerState.Normal;
                ulong occupant = action is CannonAction.Board or CannonAction.Sync ? actor.OwnerClientId : CannonSeatState.Nobody;
                using var writer = new FastBufferWriter(128, Allocator.Temp);
                writer.WriteValueSafe((byte)action); writer.WriteValueSafe(0); writer.WriteValueSafe(5u);
                writer.WriteValueSafe(actor.OwnerClientId); writer.WriteValueSafe(occupant);
                writer.WriteValueSafe(stationObject.transform.position); writer.WriteValueSafe(Quaternion.identity);
                writer.WriteValueSafe(Vector3.up * 10); writer.WriteValueSafe(-1d); writer.WriteValueSafe(-1d);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                Call(cannon, "HandleStateMessage", NetworkManager.ServerClientId, reader);
                Assert.That(cannon.Occupant(0), Is.EqualTo(occupant), "Shared seat state must still converge.");
                if (state == PlayerState.Normal && spawned)
                {
                    Assert.That(actor.transform.position, Is.EqualTo(stationObject.transform.position), "Live owners still receive normal seat, launch and cancel movement.");
                    Assert.That(pilot.IsDriving, Is.EqualTo(action != CannonAction.Cancel));
                }
                else
                {
                    Assert.That(actor.transform.position, Is.EqualTo(origin), "A late device packet cannot teleport a captured/dead/arrived/despawned owner.");
                    Assert.That(pilot.IsDriving || cannon.IsFlying(actor.OwnerClientId), Is.False);
                    if (state == PlayerState.EscapeCaptured) Assert.That(mover.Simulated, Is.False);
                }
            }
            finally
            {
                if (actor != null) typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, false);
                Object.DestroyImmediate(root); Object.DestroyImmediate(stationObject);
            }
        }

        [Test, Combinatorial]
        public void DelayedBounceCannotResumeAnUnavailableOwner(
            [Values(PlayerState.Normal, PlayerState.Fallen, PlayerState.Retired, PlayerState.Arrived, PlayerState.EscapeCaptured)] PlayerState state,
            [Values("spring", "trampoline", "ground")] string kind, [Values(true, false)] bool spawned)
        {
            var root = new GameObject("Late bounce"); root.SetActive(false);
            var previousTrampoline = HumanCannonTrampoline.Current;
            var origin = new Vector3(17520, 80, 17520); root.transform.position = origin;
            NetworkPlayer actor = null;
            try
            {
                var cannon = root.AddComponent<HumanCannonSystem>();
                cannon.ConfigurePractice(Array.Empty<HumanCannonStation>(), -10); Call(cannon, "ResetSeats");
                actor = root.AddComponent<NetworkPlayer>();
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, spawned);
                var mover = root.GetComponent<PlayerMover>(); Call(mover, "Awake");
                Set(Get(actor, "state"), "m_InternalValue", state);
                var pilot = root.AddComponent<HumanCannonPilot>();
                Set(pilot, "player", actor); Set(pilot, "mover", mover); Set(pilot, "system", cannon);
                Set(pilot, "previousSimulation", true);
                Set(cannon, "boundPlayer", actor); Set(cannon, "pilot", pilot);
                mover.Simulated = false;
                int source = CannonFlightState.PoleEntrySource;
                bool accepted;
                if (kind == "ground")
                {
                    var trampoline = root.AddComponent<HumanCannonTrampoline>();
                    typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, trampoline);
                    Set(pilot, "groundSource", trampoline);
                    ((GroundBounceRequest)Get(pilot, "groundRequest")).Begin(1u, 1u);
                    accepted = (bool)Call(cannon, "BeginGroundBounce", actor.OwnerClientId, 1u, 1u, origin + Vector3.down * 10, 10f, 1f);
                }
                else
                {
                    Call(cannon, "BeginPoleFlight", actor.OwnerClientId, 1u);
                    typeof(HumanCannonPilot).GetProperty("IsFlying").SetValue(pilot, true);
                    Set(pilot, "flightStationIndex", source); Set(pilot, "launchRevision", 1u); Set(pilot, "flightSegment", 0u);
                    accepted = (bool)Call(cannon, "ApplyRebound", actor.OwnerClientId, source, 1u, 0u,
                        origin + Vector3.down * 10, 10f, kind == "trampoline");
                }
                bool available = spawned && state == PlayerState.Normal;
                Assert.That(accepted, Is.EqualTo(available), "Late bounce authorization cannot take the body back from death/capture/arrival.");
                Assert.That(pilot.IsDriving || cannon.IsFlying(actor.OwnerClientId), Is.EqualTo(available));
                if (!available)
                {
                    Assert.That(actor.transform.position, Is.EqualTo(origin));
                    if (state == PlayerState.EscapeCaptured) Assert.That(mover.Simulated, Is.False);
                }
                else Assert.That(actor.transform.position.y, Is.EqualTo(origin.y - 10 + .05f).Within(.001f));
            }
            finally
            {
                typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, previousTrampoline);
                if (actor != null) typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, false);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("solid", 0f)]
        [TestCase("moved", -.3f)]
        [TestCase("debris", 0f)]
        [TestCase("stranded", 0f)]
        public void OfficialPathAudit_DropRecoveryChoosesUsableGround(string blocker, float expectedHeight)
        {
            var previous = typeof(EscapeCampaignContent).GetProperty("Current").GetValue(null);
            var root = new GameObject("Course drop recovery");
            root.SetActive(false);
            var supportRoot = new GameObject("Course support");
            var obstacleRoot = new GameObject("Temporary ledge");
            var voidRoot = new GameObject("Authored stranded volume");
            var origin = new Vector3(18000, 1000, 18000);
            EscapeFallVoid pit = null;
            try
            {
                var course = root.AddComponent<EscapeCampaignContent>();
                typeof(EscapeCampaignContent).GetProperty("Current").SetValue(null, course);
                var device = root.AddComponent<HumanCannonTrampoline>();
                var floor = supportRoot.AddComponent<BoxCollider>();
                float extraHeight = blocker == "debris" ? .8f : 0f;
                floor.transform.position = origin + Vector3.up * (expectedHeight - .5f);
                floor.size = new Vector3(8, 1, 8);
                obstacleRoot.transform.position = origin + Vector3.up * (extraHeight - .1f);
                if (blocker == "debris")
                {
                    var obstacle = obstacleRoot.AddComponent<BoxCollider>();
                    obstacle.size = new Vector3(2, .2f, 2);
                    obstacleRoot.AddComponent<EscapePursuerDebris>();
                }
                if (blocker == "stranded")
                {
                    pit = voidRoot.AddComponent<EscapeFallVoid>();
                    Set(pit, "coordinateSpace", voidRoot.transform);
                    voidRoot.transform.position = origin + Vector3.right * 6;
                    Set(pit, "footprint", new Rect(-1, -1, 2, 2));
                    Set(pit, "strandedLandingHeight", 1f);
                    var lower = obstacleRoot.AddComponent<BoxCollider>();
                    lower.size = Vector3.one;
                    lower.transform.position = voidRoot.transform.position - Vector3.up * .5f;
                    Call(pit, "OnEnable");
                }
                Physics.SyncTransforms();
                object[] args = { origin + Vector3.up * 1.1f, Vector3.zero, true };
                Assert.That(Call(device, "TryFindGround", args), Is.True);
                Assert.That(((Vector3)args[1]).y, Is.EqualTo(origin.y + expectedHeight).Within(.02f));
                if (pit != null)
                {
                    args[0] = voidRoot.transform.position + Vector3.up * 1.1f;
                    Assert.That(Call(device, "TryFindGround", args), Is.False, "A cannot return the player to a fatal ledge for their gear.");
                }
            }
            finally
            {
                if (pit != null) Call(pit, "OnDisable");
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(supportRoot);
                Object.DestroyImmediate(obstacleRoot);
                Object.DestroyImmediate(voidRoot);
                typeof(EscapeCampaignContent).GetProperty("Current").SetValue(null, previous);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OfficialPathAudit_TheHostFinalPoseControlsPickupAfterDrop(bool samePosition)
        {
            var previous = typeof(EscapeCampaignContent).GetProperty("Current").GetValue(null);
            var root = new GameObject("Route client drop");
            root.SetActive(false);
            try
            {
                var course = root.AddComponent<EscapeCampaignContent>();
                typeof(EscapeCampaignContent).GetProperty("Current").SetValue(null, course);
                var device = root.AddComponent<HumanCannonTrampoline>();
                var landing = new Vector3(18000, 1000, 18000);
                root.transform.position = samePosition ? landing : landing + Vector3.right * 12;
                ReceiveDrop(1, landing, true);
                Assert.That(Get(device, "dropping"), Is.True);
                var finalPose = landing + Vector3.forward;
                ReceiveDrop(2, finalPose, false);
                Assert.That(Get(device, "dropping"), Is.False);
                Assert.That(root.transform.position, Is.EqualTo(finalPose));
                ReceiveDrop(1, landing, true);
                Assert.That(root.transform.position, Is.EqualTo(finalPose));

                void ReceiveDrop(uint revision, Vector3 point, bool animate)
                {
                    using var writer = new FastBufferWriter(41, Allocator.Temp);
                    writer.WriteValueSafe(revision);
                    writer.WriteValueSafe(CannonSeatState.Nobody);
                    writer.WriteValueSafe(point);
                    writer.WriteValueSafe(Quaternion.identity);
                    writer.WriteValueSafe(animate);
                    using var reader = new FastBufferReader(writer, Allocator.Temp);
                    Call(device, "HandleStateMessage", NetworkManager.ServerClientId, reader);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                typeof(EscapeCampaignContent).GetProperty("Current").SetValue(null, previous);
            }
        }

        [TestCase("target-obstructed")]
        [TestCase("target-removed")]
        [TestCase("target-lowered")]
        [TestCase("target-still-supported")]
        public void OfficialPathAudit_AnApprovedDropResolvesAgainstTheFinalFloor(string change)
        {
            var previous = typeof(EscapeCampaignContent).GetProperty("Current").GetValue(null);
            var root = new GameObject("Host course drop");
            root.SetActive(false);
            var landingFloor = new GameObject("Landing");
            var fallbackFloor = new GameObject("Initial placement");
            var blocker = new GameObject("Entered after approval");
            var position = new Vector3(18000, 1000, 18000);
            var home = position + Vector3.right * 15;
            try
            {
                var course = root.AddComponent<EscapeCampaignContent>();
                typeof(EscapeCampaignContent).GetProperty("Current").SetValue(null, course);
                var session = root.AddComponent<NetworkSessionManager>();
                Set(session, "_networkManager", root.GetComponent<NetworkManager>());
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
                var device = root.AddComponent<HumanCannonTrampoline>();
                Set(device, "session", session);
                Set(device, "initialPosition", home);
                Set(device, "initialRotation", Quaternion.identity);
                AddSupport(landingFloor, position);
                AddSupport(fallbackFloor, home);
                root.AddComponent<BoxCollider>();
                root.SetActive(true);
                Call(device, "RefreshCarryBody", new object[] { null });
                root.transform.position = position + Vector3.up;
                Call(device, "StartDrop", position);
                if (change == "target-obstructed")
                {
                    var ceiling = blocker.AddComponent<BoxCollider>();
                    blocker.transform.position = position + Vector3.up * .5f;
                    ceiling.size = new Vector3(8, .98f, 8);
                }
                if (change == "target-removed") landingFloor.SetActive(false);
                if (change == "target-lowered") landingFloor.transform.position -= Vector3.up * .3f;
                root.transform.position = position;
                Physics.SyncTransforms();
                if (change == "target-obstructed")
                    Assert.That(Call(device, "CanSettleAt", position, Quaternion.identity, null), Is.False);
                Call(device, "CompleteCourseDrop", Quaternion.identity);
                Assert.That(Get(device, "dropping"), Is.False);
                var expected = change switch
                {
                    "target-still-supported" => position,
                    "target-lowered" => position - Vector3.up * .3f,
                    _ => home
                };
                Assert.That(Vector3.Distance(root.transform.position, expected), Is.LessThan(.02f),
                    $"Expected {expected}, got {root.transform.position}.");

                void AddSupport(GameObject node, Vector3 top)
                {
                    node.transform.position = top - Vector3.up * .5f;
                    node.AddComponent<BoxCollider>().size = new Vector3(8, 1, 8);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(landingFloor);
                Object.DestroyImmediate(fallbackFloor);
                Object.DestroyImmediate(blocker);
                typeof(EscapeCampaignContent).GetProperty("Current").SetValue(null, previous);
            }
        }

        [TestCase(0f, false), TestCase(1f, false), TestCase(3f, false)]
        [TestCase(0f, true), TestCase(1f, true), TestCase(3f, true)]
        public void ManualDropSettlesButRelocationDetachPreservesTheOldLoadPose(float height, bool relocation)
        {
            var root = new GameObject("Drop sequence"); root.SetActive(false);
            var floorObject = new GameObject("Drop support");
            var all = (List<NetworkPlayer>)NetworkPlayer.All; var saved = all.ToArray(); all.Clear();
            NetworkPlayer player = null;
            try
            {
                var floor = floorObject.AddComponent<BoxCollider>(); floor.size = new Vector3(10, 1, 10);
                floor.transform.position = new Vector3(18000, 999.5f, 18000);
                var session = root.AddComponent<NetworkSessionManager>();
                Set(session, "_networkManager", root.GetComponent<NetworkManager>());
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
                var playerRoot = new GameObject("Drop owner"); playerRoot.SetActive(false); playerRoot.transform.SetParent(root.transform);
                player = playerRoot.AddComponent<NetworkPlayer>();
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true); all.Add(player);
                player.transform.position = floor.transform.position + Vector3.up * (.5f + height);
                var deviceRoot = new GameObject("Carried trampoline"); deviceRoot.SetActive(false); deviceRoot.transform.SetParent(root.transform);
                deviceRoot.transform.position = player.transform.position;
                var device = deviceRoot.AddComponent<HumanCannonTrampoline>(); Set(device, "session", session);
                Set(device, "initialPosition", new Vector3(18000, 1000, 18000)); Set(device, "initialRotation", Quaternion.identity);
                var ownership = (CannonSeatState)Get(device, "ownership");
                ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision);
                var drop = Enum.Parse(typeof(HumanCannonTrampoline).GetNestedType("TrampolineAction", BindingFlags.NonPublic), "Drop");
                Physics.SyncTransforms();
                if (relocation) device.ServerReleaseForInspection(player.OwnerClientId);
                else Call(device, "HandleRequest", player.OwnerClientId, drop, ownership.Revision, -1, 0u, 0u,
                        device.transform.position, Quaternion.identity, true);
                Assert.That(device.Carrier, Is.EqualTo(CannonSeatState.Nobody));
                Assert.That((Vector3)device.GetType().GetProperty("SharedPosition", F).GetValue(device),
                    Is.EqualTo(new Vector3(18000, 1000 + (relocation ? height : 0), 18000)),
                    "Manual drop shares its landing; relocation must detach at the old pose before the carrier teleports.");
                Assert.That(Get(device, "dropping"), Is.EqualTo(!relocation && height > .1f));
            }
            finally
            {
                if (player != null) typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
                all.Clear(); all.AddRange(saved); Object.DestroyImmediate(root); Object.DestroyImmediate(floorObject);
            }
        }
    }
}
