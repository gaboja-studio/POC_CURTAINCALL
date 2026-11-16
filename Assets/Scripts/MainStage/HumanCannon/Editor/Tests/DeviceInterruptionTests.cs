using System;
using System.Collections.Generic;
using System.Reflection;
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
                    Call(Get(pilot, "groundRequest"), "Begin", 1u, 1u);
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
                if (actor != null) typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, false);
                Object.DestroyImmediate(root);
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
