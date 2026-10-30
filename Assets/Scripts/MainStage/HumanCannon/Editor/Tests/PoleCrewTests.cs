using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class PoleCrewTests
    {
        const ulong Empty = CannonSeatState.Nobody;
        [Test]
        public void FourRiders_KeepTheirSeatsWhenTheFirstRiderLeaves()
        {
            var state = new PoleGateState(4);
            for (ulong actor = 1; actor <= 4; actor++) Assert.IsTrue(state.TryGrab(actor, state.Revision, 0, 0));
            Assert.IsFalse(state.HasFreeSeat);
            Assert.IsFalse(state.TryGrab(5, state.Revision, 0, 0));
            Assert.IsTrue(state.TryRelease(1, state.Revision, 1, 2));
            Assert.That(state.Count, Is.EqualTo(3));
            Assert.That(state.Phase, Is.EqualTo(PoleSawPhase.Retracted));
            Assert.That(state.SeatOf(4), Is.EqualTo(3));
            Assert.IsTrue(state.TryGrab(5, state.Revision, 2, 0));
            Assert.That(state.SeatOf(5), Is.Zero);
            Assert.That(state.SeatOf(2), Is.EqualTo(1));
        }

        [Test]
        public void EachRiderExpiresFromTheirOwnGrabTime_WithoutExtendingAnotherRider()
        {
            var state = new PoleGateState(4);
            state.TryGrab(1, 0, 10, 5, seat: 2);
            state.TryGrab(2, 1, 13, 5, seat: 3);
            Assert.IsTrue(state.Tick(15, 2));
            Assert.That(state.SeatOf(1), Is.EqualTo(-1));
            Assert.That(state.SeatOf(2), Is.EqualTo(3));
            Assert.That(state.DeadlineAt(3), Is.EqualTo(18));
            Assert.IsFalse(state.Tick(17.99, 2));
            Assert.IsTrue(state.Tick(18, 2));
            Assert.That(state.Count, Is.Zero);
            Assert.That(state.RearmAt, Is.EqualTo(20));
            Assert.IsTrue(state.Tick(20, 2));
            Assert.That(state.Phase, Is.EqualTo(PoleSawPhase.Active));
        }

        [Test]
        public void SimultaneousExpiry_ProducesOneDistinctRevisionPerRider()
        {
            var state = new PoleGateState(4);
            state.TryGrab(1, 0, 10, 5); state.TryGrab(2, 1, 10, 5);
            Assert.IsTrue(state.Tick(15, 2));
            Assert.That(state.Count, Is.EqualTo(1)); Assert.That(state.Revision, Is.EqualTo(3));
            Assert.IsTrue(state.Tick(15, 2));
            Assert.That(state.Count, Is.Zero); Assert.That(state.Revision, Is.EqualTo(4));
        }

        [Test]
        public void DuplicateActorAndOccupiedSeatCannotDisplaceAnotherRider()
        {
            var state = new PoleGateState(4);
            state.TryGrab(1, 0, 1, 0, seat: 2);
            Assert.IsFalse(state.TryGrab(1, 1, 2, 0, seat: 3));
            Assert.IsFalse(state.TryGrab(2, 1, 2, 0, seat: 2));
            Assert.IsFalse(state.TryGrab(2, 0, 2, 0));
            Assert.That(state.Revision, Is.EqualTo(1));
        }

        [Test]
        public void ResetClearsAllRidersAndRejectsOldOccupancySnapshot()
        {
            var state = new PoleGateState(4);
            var actors = new[] { 1ul, Empty, 2ul, 3ul };
            Assert.IsTrue(state.ReceiveCrew(7, actors, new double[4], PoleSawPhase.Retracted, 0));
            state.Reset();
            Assert.That(state.Count, Is.Zero);
            Assert.IsFalse(state.ReceiveCrew(7, actors, new double[4], PoleSawPhase.Retracted, 0));
        }

        [Test]
        public void InvalidCrewSnapshotsAreAtomicAndDoNotConsumeRevision()
        {
            var state = new PoleGateState(4);
            state.TryGrab(1, 0, 1, 0);
            Assert.IsFalse(state.ReceiveCrew(50, new[] { 2ul, 2ul, Empty, Empty }, new double[4], PoleSawPhase.Retracted, 0));
            Assert.IsFalse(state.ReceiveCrew(50, new[] { 2ul, Empty, Empty, Empty }, new[] { 0d, double.NaN, 0d, 0d }, PoleSawPhase.Retracted, 0));
            Assert.IsFalse(state.ReceiveCrew(50, new[] { 2ul, Empty, Empty, Empty }, new double[4], PoleSawPhase.Active, 0));
            Assert.IsFalse(state.ReceiveCrew(50, new[] { 2ul, Empty, Empty, Empty }, new double[4], PoleSawPhase.Warning, 10));
            Assert.That(state.Revision, Is.EqualTo(1)); Assert.That(state.Occupant, Is.EqualTo(1ul));
        }

        [Test]
        public void SingleSeatAndTimedSawGateKeepExistingCapacity()
        {
            var state = new PoleGateState();
            Assert.That(state.Capacity, Is.EqualTo(1));
            Assert.IsFalse(state.ReceiveCrew(1, new[] { Empty, 2ul, Empty, Empty }, new double[4], PoleSawPhase.Retracted, 0));
            Assert.IsFalse(new PoleGateState(4).TryGrab(1, 0, 1, 10, true));
            Assert.IsTrue(state.TryGrab(1, 0, 1, 10, true));
            Assert.That(state.RearmAt, Is.EqualTo(11));
        }

        [TestCase(4f, 4, 1f)]
        [TestCase(0f, 2, 0f)]
        [TestCase(1f, 4, .25f)]
        [TestCase(-3f, 3, -1f)]
        public void SharedDriveRewardsCoordinationWithoutMultiplyingOriginalForce(float sum, int count, float expected)
            => Assert.That(HumanCannonPoleSystem.SharedDrive(sum, count), Is.EqualTo(expected).Within(.0001f));

        [Test]
        public void SeatSpacingUsesBarRightAxisAndDoesNotChangeSwingHeight()
        {
            var root = new GameObject("Crew seat geometry") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var pole = root.AddComponent<HumanCannonPole>();
                typeof(HumanCannonPole).GetField("riderCapacity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pole, 4);
                root.transform.rotation = Quaternion.Euler(0, 37, 0);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 span = pole.HangingPosition(25, i + 1) - pole.HangingPosition(25, i);
                    Assert.That(Vector3.Distance(span, root.transform.right * .8f), Is.LessThan(.001f));
                    Assert.That(pole.HangingPosition(25, i).y, Is.EqualTo(pole.HangingPosition(25).y).Within(.001f));
                }
                var gate = root.AddComponent<HumanCannonSawGate>();
                typeof(HumanCannonPole).GetField("linkedGate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pole, gate);
                Assert.That(pole.RiderCapacity, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    public class PoleCrewSnapshotTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const ulong Empty = CannonSeatState.Nobody;
        GameObject root;
        HumanCannonPoleSystem receiver;
        object runtime;
        struct Row
        {
            public ulong Actor;
            public int Station;
            public uint Launch, Segment;
            public Row(ulong actor, int station, uint launch, uint segment)
            { Actor = actor; Station = station; Launch = launch; Segment = segment; }
        }
        static Row Vacant => new Row(Empty, 0, 0, 0);
        Row[] Two => new[] { new Row(7, -1, 12, 1), Vacant, new Row(8, 2, 45, 4), Vacant };

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Crew snapshot receiver") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            receiver = root.AddComponent<HumanCannonPoleSystem>();
            var pole = root.AddComponent<HumanCannonPole>();
            typeof(HumanCannonPole).GetField("riderCapacity", Instance).SetValue(pole, 4);
            typeof(HumanCannonPoleSystem).GetField("poles", Instance).SetValue(receiver, new[] { pole });
            Type type = typeof(HumanCannonPoleSystem).GetNestedType("Runtime", BindingFlags.NonPublic);
            runtime = Activator.CreateInstance(type, new object[] { 4 });
            Array states = Array.CreateInstance(type, 1); states.SetValue(runtime, 0);
            typeof(HumanCannonPoleSystem).GetField("states", Instance).SetValue(receiver, states);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void LateJoinRestoresSeparateFlightTokensAndStableSeats()
        {
            Deliver(Two, 8);
            Assert.That(receiver.State(0).Count, Is.EqualTo(2));
            Assert.That(receiver.HeldBy(7), Is.Zero); Assert.That(receiver.HeldBy(8), Is.Zero);
            Assert.That(receiver.State(0).SeatOf(8), Is.EqualTo(2));
            Assert.That(Field<uint>(0, "Launch"), Is.EqualTo(12));
            Assert.That(Field<uint>(2, "Launch"), Is.EqualTo(45));
            Assert.That(Field<uint>(2, "Segment"), Is.EqualTo(4));
            Vector3 before = receiver.HangingPosition(0, 8);
            Row[] after = Two; after[0] = Vacant;
            Deliver(after, 9, action: 2, actor: 7, seat: 0, station: -1, launch: 12, segment: 1);
            Assert.That(receiver.State(0).Count, Is.EqualTo(1));
            Assert.That(receiver.HeldBy(7), Is.EqualTo(-1));
            Assert.That(Field<uint>(2, "Segment"), Is.EqualTo(4));
            Assert.That(Vector3.Distance(before, receiver.HangingPosition(0, 8)), Is.LessThan(.0001f));
        }

        [Test]
        public void NonServerAndOldCrewSnapshotsCannotReplaceCurrentRiders()
        {
            Deliver(Two, 8, sender: 44);
            Assert.That(receiver.State(0).Count, Is.Zero);
            Deliver(Two, 8);
            Deliver(new[] { Vacant, new Row(9, -1, 1, 1), Vacant, Vacant }, 7);
            Assert.That(receiver.HeldBy(7), Is.Zero); Assert.That(receiver.HeldBy(9), Is.EqualTo(-1));
        }

        [Test]
        public void DuplicateActorOrMalformedGrabDoesNotConsumeRevision()
        {
            Deliver(Two, 8);
            Row[] duplicate = Two; duplicate[2] = duplicate[0];
            Deliver(duplicate, 99);
            Assert.That(receiver.State(0).Revision, Is.EqualTo(8));
            Deliver(Two, 99, action: 1, actor: 9, seat: 0, station: -1, launch: 12, segment: 0);
            Assert.That(receiver.State(0).Revision, Is.EqualTo(8));
            Deliver(Two, 99, action: 2, actor: 7, seat: 0, station: -1, launch: 12, segment: 1);
            Assert.That(receiver.State(0).Revision, Is.EqualTo(8), "놓기 메시지의 단원이 점유 목록에 남아 있으면 거부한다.");
        }

        [Test]
        public void ResetRejectsDelayedOccupancyForAllSeats()
        {
            Deliver(Two, 8);
            Deliver(new[] { Vacant, Vacant, Vacant, Vacant }, 9, action: 4, phase: PoleSawPhase.Active);
            Deliver(Two, 8);
            Assert.That(receiver.State(0).Count, Is.Zero);
            Assert.That(receiver.State(0).Revision, Is.EqualTo(9));
        }

        T Field<T>(int seat, string name)
        {
            var riders = (Array)runtime.GetType().GetField("Riders").GetValue(runtime);
            object rider = riders.GetValue(seat);
            return (T)rider.GetType().GetField(name).GetValue(rider);
        }
        void Deliver(Row[] rows, uint revision, byte action = 0, ulong actor = Empty, byte seat = 255,
            int station = 0, uint launch = 0, uint segment = 0, ulong sender = NetworkManager.ServerClientId,
            PoleSawPhase phase = PoleSawPhase.Retracted)
        {
            using var writer = new FastBufferWriter(action == 2 ? 200 : 160, Allocator.Temp);
            writer.WriteValueSafe(action); writer.WriteValueSafe(0); writer.WriteValueSafe(revision);
            writer.WriteValueSafe((byte)phase); writer.WriteValueSafe(0d); writer.WriteValueSafe(20f); writer.WriteValueSafe(5f);
            writer.WriteValueSafe(actor); writer.WriteValueSafe(seat); writer.WriteValueSafe(station);
            writer.WriteValueSafe(launch); writer.WriteValueSafe(segment); writer.WriteValueSafe((byte)0);
            foreach (Row row in rows)
            {
                writer.WriteValueSafe(row.Actor); writer.WriteValueSafe(0d); writer.WriteValueSafe(row.Station);
                writer.WriteValueSafe(row.Launch); writer.WriteValueSafe(row.Segment);
            }
            if (action == 2)
            {
                writer.WriteValueSafe(Vector3.one); writer.WriteValueSafe(Quaternion.identity); writer.WriteValueSafe(Vector3.up);
            }
            Assert.That(writer.Length, Is.EqualTo(action == 2 ? 200 : 160));
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            typeof(HumanCannonPoleSystem).GetMethod("ReceiveState", Instance).Invoke(receiver, new object[] { sender, reader });
        }
    }
}
