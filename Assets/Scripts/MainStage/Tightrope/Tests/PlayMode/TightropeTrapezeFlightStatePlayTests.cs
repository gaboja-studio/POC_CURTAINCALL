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
            var requests = (IDictionary)Get(trapeze, "pendingRequests");
            Assert.IsFalse((bool)Call(trapeze, "InCatchRange", player, 1, false));
            Assert.IsFalse((bool)Call(trapeze, "TryRequestCatch", player), "봉 아래의 이른 F는 비거리와 무관하게 거절한다.");
            Assert.That(requests.Count, Is.Zero, "로컬 범위 밖 F는 예약을 만들지 않는다.");
            // 접촉한 클라이언트의 F보다 서버 위치가 늦는 경우만 짧은 위치 대기를 허용한다.
            var next = ((Array)Get(trapeze, "poles")).GetValue(1);
            uint revision = (uint)Get(next, "Revision"), token = (uint)Get(trapeze, "localFlightToken");
            Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Grab"), 1, revision, 0f, token);
            Assert.That(requests.Count, Is.EqualTo(1));
            object waiting = requests[owner];
            Assert.IsFalse((bool)Get(waiting, "Armed"));
            double until = (double)Get(waiting, "Until");
            Assert.That(until, Is.EqualTo((double)Get(trapeze, "Clock") + .5d).Within(.001d));
            Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Grab"), 1, revision, 0f, token);
            Assert.AreSame(waiting, requests[owner]);
            Assert.That((double)Get(waiting, "Until"), Is.EqualTo(until), "중복 F는 원래 서버 위치 대기 기한을 늘리지 않는다.");
            Call(trapeze, "ProcessPendingRequests");
            Call(trapeze, "ReconcileLocal");
            Assert.IsFalse((bool)Call(trapeze, "IsHeld", player));
            Assert.That(Vector3.Distance(player.transform.position, before), Is.LessThan(.001f), "봉 아래에서 위로 순간 이동하지 않는다.");
            Assert.That(requests.Count, Is.EqualTo(1));
            Set(waiting, "Until", (double)Get(trapeze, "Clock") - .01d);
            Call(trapeze, "ProcessPendingRequests");
            Assert.That(requests.Count, Is.Zero, "위치가 도착하지 않은 요청은 비행 중이어도 원래 짧은 기한에 만료한다.");
            Assert.IsTrue((bool)Call(trapeze, "IsInFlight", player));
            Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Grab"), 1, revision, 0f, token);
            Assert.That(requests.Count, Is.EqualTo(1), "abort 직전 실제 대기 요청을 다시 만든다.");
            Call(trapeze, "OnRestarted", player);
            Assert.That(requests.Count, Is.Zero);
            Assert.IsFalse((bool)Call(trapeze, "IsInFlight", player));
            Assert.IsFalse((bool)Call(trapeze, "IsHeld", player));
            Assert.That(((IDictionary)Get(trapeze, "flights")).Count, Is.Zero);
            Assert.IsTrue((bool)Get(mover, "Simulated"));
            Assert.IsTrue(body.enabled);
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
