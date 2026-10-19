using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class CannonFlightSyncTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        GameObject root;
        HumanCannonSystem receiver;
        CannonFlightState flights;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Flight Snapshot Receiver") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false); // 실제 세션·씬 singleton을 변경하지 않는 수신 프로토콜 검사.
            receiver = root.AddComponent<HumanCannonSystem>();
            Set("stations", new HumanCannonStation[2]);
            Set("awaitingFlightSnapshot", true);
            flights = (CannonFlightState)typeof(HumanCannonSystem).GetField("flying", Instance).GetValue(receiver);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Snapshot_RestoresCannonAndGroundFlightsWithCurrentPoleSegments_BeforePlayerSpawn()
        {
            var host = new CannonFlightState();
            host.Begin(7, 1, 12);
            host.TryRebound(7, 1, 12, 0); // 봉 잡기
            host.Begin(8, -1, 3);
            host.TryRebound(8, -1, 3, 0);
            host.TryRebound(8, -1, 3, 1);
            ReceiveSnapshot(host.Capture());
            Assert.IsTrue(receiver.IsFlying(7));
            Assert.IsTrue(flights.Matches(7, 1, 12, 1));
            Assert.IsTrue(flights.Matches(8, -1, 3, 2));
            Call("PruneFlights"); // 수신자는 아직 원격 NetworkPlayer를 갖고 있지 않아도 호스트 목록을 유지한다.
            Assert.IsTrue(receiver.IsFlying(7));
            Assert.IsTrue(flights.TryRebound(7, 1, 12, 1), "이어지는 봉 놓기 이벤트가 같은 segment로 처리된다.");
            Assert.IsFalse(flights.TryComplete(7, 1, 12, 1), "잡기 전/놓기 전의 늦은 착지는 거절한다.");
            Assert.IsTrue(flights.TryComplete(7, 1, 12, 2));
            Assert.IsTrue(receiver.IsFlying(8), "다른 비행은 그대로 남는다.");
        }

        [Test]
        public void Snapshot_OnlyServerReplyIsAccepted_AndDuplicateCannotRestoreEndedFlight()
        {
            var snapshot = new[] { new CannonFlightState.Flight(7, 0, 2, 0) };
            ReceiveSnapshot(snapshot, 99);
            Assert.IsFalse(receiver.IsFlying(7));
            ReceiveSnapshot(snapshot);
            Assert.IsTrue(flights.TryComplete(7, 0, 2));
            ReceiveSnapshot(snapshot);
            Assert.IsFalse(receiver.IsFlying(7), "초기 목록은 한 번만 받아 이미 끝난 비행을 재생하지 않는다.");
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(int.MaxValue)]
        public void InvalidCount_DoesNotConsumeSnapshotOrChangeExistingFlights(int count)
        {
            flights.Begin(5, 0, 2);
            using (var writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(count);
                Deliver("HandleFlightSnapshot", writer);
            }
            Assert.IsTrue(receiver.IsFlying(5));
            ReceiveSnapshot(Array.Empty<CannonFlightState.Flight>());
            Assert.IsFalse(receiver.IsFlying(5), "잘못된 패킷 뒤에도 정상 빈 목록을 받을 수 있다.");
        }

        [Test]
        public void InvalidEntries_DoNotPartiallyReplaceRegistry()
        {
            flights.Begin(5, 0, 2);
            ReceiveSnapshot(new[] { new CannonFlightState.Flight(7, 0, 2, 0), new CannonFlightState.Flight(7, 1, 4, 1) });
            Assert.IsTrue(receiver.IsFlying(5));
            Assert.IsFalse(receiver.IsFlying(7));
            ReceiveSnapshot(new[] { new CannonFlightState.Flight(7, 2, 2, 0) });
            ReceiveSnapshot(new[] { new CannonFlightState.Flight(ulong.MaxValue, 0, 2, 0) });
            Assert.IsTrue(receiver.IsFlying(5));
            ReceiveSnapshot(new[] { new CannonFlightState.Flight(7, -1, 2, 0) });
            Assert.IsFalse(receiver.IsFlying(5));
            Assert.IsTrue(receiver.IsFlying(7));
        }

        [Test]
        public void HostRemoval_AppliesOnlyToMatchingFlight_AndCannotRemoveNewLaunch()
        {
            var oldFlight = new CannonFlightState.Flight(7, 0, 2, 1);
            ReceiveSnapshot(new[] { oldFlight });
            ReceiveRemoval(oldFlight, 99);
            ReceiveRemoval(new CannonFlightState.Flight(7, 0, 2, 0));
            Assert.IsTrue(receiver.IsFlying(7));
            ReceiveRemoval(oldFlight);
            Assert.IsFalse(receiver.IsFlying(7));
            flights.Begin(7, 0, 4);
            ReceiveRemoval(oldFlight);
            Assert.IsTrue(flights.Matches(7, 0, 4, 0));
        }

        [Test]
        public void Restart_InvalidatesPendingSnapshotAndClearsExistingFlights()
        {
            flights.Begin(7, 0, 2);
            Call("Apply", 0, CannonAction.Reset, CannonSeatState.Nobody, Vector3.zero, Quaternion.identity, Vector3.zero);
            ReceiveSnapshot(new[] { new CannonFlightState.Flight(7, 0, 2, 0) });
            Assert.IsFalse(receiver.IsFlying(7));
            Assert.IsFalse(flights.TryComplete(7, 0, 2));
        }

        [Test]
        public void Disconnect_InvalidatesPendingReplyAndResetsRegistry()
        {
            flights.Begin(7, 0, 2);
            Call("Unregister");
            Call("ResetSeats");
            ReceiveSnapshot(new[] { new CannonFlightState.Flight(7, 0, 2, 0) });
            Assert.IsFalse(receiver.IsFlying(7));
        }

        [Test]
        public void Pruning_PublishesRemovedIdentity_WithoutRemovingOtherFlights()
        {
            flights.Begin(7, 0, 2);
            flights.TryRebound(7, 0, 2, 0);
            flights.Begin(8, 1, 5);
            var before = flights.Capture();
            CannonFlightState.Flight removed = default;
            int count = 0;
            flights.RemoveWhere(actor => actor == 7, flight => { removed = flight; count++; });
            Assert.AreEqual(1, count);
            Assert.AreEqual(7ul, removed.Actor);
            Assert.AreEqual(0, removed.StationIndex);
            Assert.AreEqual(2u, removed.LaunchRevision);
            Assert.AreEqual(1u, removed.Segment);
            Assert.IsFalse(receiver.IsFlying(7));
            Assert.IsTrue(receiver.IsFlying(8));
            Assert.AreEqual(2, before.Length, "내보낸 스냅샷은 이후 제거와 독립적이다.");
        }

        void ReceiveSnapshot(CannonFlightState.Flight[] snapshot, ulong sender = NetworkManager.ServerClientId)
        {
            using var writer = new FastBufferWriter(sizeof(int) + snapshot.Length * 20, Allocator.Temp);
            typeof(HumanCannonSystem).GetMethod("WriteFlightSnapshot", Static).Invoke(null, new object[] { writer, snapshot });
            Deliver("HandleFlightSnapshot", writer, sender);
        }

        void ReceiveRemoval(CannonFlightState.Flight flight, ulong sender = NetworkManager.ServerClientId)
        {
            using var writer = new FastBufferWriter(20, Allocator.Temp);
            typeof(HumanCannonSystem).GetMethod("WriteFlight", Static).Invoke(null, new object[] { writer, flight });
            Deliver("HandleFlightRemoved", writer, sender);
        }

        void Deliver(string method, FastBufferWriter writer, ulong sender = NetworkManager.ServerClientId)
        {
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Call(method, sender, reader);
        }

        void Call(string method, params object[] args) => typeof(HumanCannonSystem).GetMethod(method, Instance).Invoke(receiver, args);
        void Set(string field, object value) => typeof(HumanCannonSystem).GetField(field, Instance).SetValue(receiver, value);
    }
}
