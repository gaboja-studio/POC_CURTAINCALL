using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator TrapezeSnapshotPreservesTheLaunchTargetAndRejectsInvalidTargets()
        {
            EnableTrapezeForEntryTest();
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2f, "공중그네 준비");
            var poles = (Array)Get(trapeze, "poles");
            var first = poles.GetValue(0);
            ulong owner = (ulong)Get(player, "OwnerClientId");
            Set(first, "Upper", owner);
            Set(first, "Until", (double)Get(trapeze, "Clock") + 30d);
            Set(first, "Speed", 180f);
            Call(trapeze, "ReconcileLocal");
            var command = trapeze.GetType().GetNestedType("Command", BindingFlags.NonPublic);
            Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Release"), 0, (uint)Get(first, "Revision"), 0f, 0u);
            Call(trapeze, "ReconcileLocal");
            var flights = (IDictionary)Get(trapeze, "flights");
            Vector3 expected = (Vector3)Get(flights[owner], "Target");
            uint version = (uint)Get(trapeze, "version") + 1;
            byte[] packet = TrapezeFlightPacket(trapeze, owner, version, expected);
            Assert.That(packet.Length, Is.EqualTo(17 + 3 * 48 + 68));
            var connection = Get(session, "ConnectionState");
            try
            {
                Set(session, "<ConnectionState>k__BackingField", Enum.Parse(connection.GetType(), "Client"));
                ReplayTrapezeState(trapeze, packet);
                Assert.That((uint)Get(trapeze, "version"), Is.EqualTo(version));
                Assert.That((Vector3)Get(flights[owner], "Target"), Is.EqualTo(expected));
                ReplayTrapezeState(trapeze, TrapezeFlightPacket(trapeze, owner, version + 1, new Vector3(float.NaN, 0f, 0f)));
                Assert.That((uint)Get(trapeze, "version"), Is.EqualTo(version));
                Assert.That((Vector3)Get(flights[owner], "Target"), Is.EqualTo(expected));
            }
            finally { Set(session, "<ConnectionState>k__BackingField", connection); }
        }

        [UnityTest]
        public IEnumerator TrapezeEarlyFDoesNotPullAFallingPlayerUpAndAbortClearsTheRequest()
        {
            EnableTrapezeForEntryTest();
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2f, "공중그네 준비");
            var first = ((Array)Get(trapeze, "poles")).GetValue(0);
            ulong owner = (ulong)Get(player, "OwnerClientId");
            Set(first, "Upper", owner);
            Set(first, "Until", (double)Get(trapeze, "Clock") + 30d);
            Call(trapeze, "ReconcileLocal");
            Vector3 origin = player.transform.position;
            var command = trapeze.GetType().GetNestedType("Command", BindingFlags.NonPublic);
            Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Release"), 0, (uint)Get(first, "Revision"), 0f, 0u);
            Call(trapeze, "ReconcileLocal");
            float limit = (float)Call(trapeze, "FlightDistanceLimit", 0, origin);
            var body = (CharacterController)Get(mover, "Body");
            body.enabled = false;
            player.transform.position = origin + (Vector3)Get(course, "Forward") * (limit * .6f) + Vector3.down * 3f;
            body.enabled = true;
            Vector3 before = player.transform.position;
            Assert.IsTrue((bool)Call(trapeze, "TryRequestCatch", player));
            Call(trapeze, "ProcessPendingRequests");
            Call(trapeze, "ReconcileLocal");
            Assert.IsFalse((bool)Call(trapeze, "IsHeld", player));
            Assert.That(Vector3.Distance(player.transform.position, before), Is.LessThan(.001f), "봉 아래에서 위로 순간 이동하지 않는다.");
            Assert.That(((IDictionary)Get(trapeze, "pendingRequests")).Count, Is.EqualTo(1));
            Call(trapeze, "OnRestarted", player);
            Assert.That(((IDictionary)Get(trapeze, "pendingRequests")).Count, Is.Zero);
            Assert.IsFalse((bool)Call(trapeze, "IsInFlight", player));
        }

        byte[] TrapezeFlightPacket(object trapeze, ulong owner, uint version, Vector3 target)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((int)Get(run, "Round")); writer.Write(version); writer.Write((double)Get(trapeze, "Clock"));
            foreach (var pole in (Array)Get(trapeze, "poles"))
            {
                writer.Write((uint)Get(pole, "Revision")); writer.Write((ulong)Get(pole, "Upper")); writer.Write((ulong)Get(pole, "Lower"));
                writer.Write((float)Get(pole, "Angle")); writer.Write((float)Get(pole, "Speed")); writer.Write((float)Get(pole, "Drive"));
                writer.Write((double)Get(pole, "Until")); writer.Write((double)Get(pole, "SampledAt"));
            }
            var flight = ((IDictionary)Get(trapeze, "flights"))[owner];
            writer.Write((byte)1); writer.Write(owner); writer.Write((uint)Get(flight, "Token")); writer.Write((int)Get(flight, "From"));
            WriteVector((Vector3)Get(flight, "Position")); WriteVector((Vector3)Get(flight, "Velocity")); WriteVector(target);
            writer.Write((double)Get(flight, "Started")); writer.Write((double)Get(flight, "Until"));
            return stream.ToArray();

            void WriteVector(Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        }

        static void ReplayTrapezeState(object trapeze, byte[] bytes)
        {
            object allocator = Enum.Parse(GameType("Unity.Collections.Allocator"), "Temp");
            var reader = Activator.CreateInstance(GameType("Unity.Netcode.FastBufferReader"), bytes, allocator, -1, 0);
            try { Call(trapeze, "ReadState", 0UL, reader); }
            finally { ((IDisposable)reader).Dispose(); }
        }
    }
}
