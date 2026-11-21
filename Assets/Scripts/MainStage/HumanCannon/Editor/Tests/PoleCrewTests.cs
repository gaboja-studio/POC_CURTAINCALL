using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("CommonProgressionRegression")]
    public class PoleCrewTests
    {
        const ulong Empty = CannonSeatState.Nobody;

        [Test]
        public void TransitionAudit_AnOldCannonLandingCannotConsumeTheNextSwingDeparture()
        {
            var root = new GameObject("Swing flight book");
            root.SetActive(false);
            try
            {
                var system = root.AddComponent<HumanCannonPoleSystem>();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var origins = (Dictionary<ulong, (int Pole, int Station, uint Launch)>)
                    typeof(HumanCannonPoleSystem).GetField("flightOrigins", fields).GetValue(system);
                var landed = typeof(HumanCannonPoleSystem).GetMethod("OnLandingConfirmed", fields);
                int confirmed = -1;
                system.Landed += (pole, _, _, _) => confirmed = pole;
                origins.Add(19, (4, -2, 11));
                landed.Invoke(system, new object[] { 19UL, -2, 10U, 2U, Vector3.zero, true });
                Assert.That(confirmed, Is.EqualTo(-1));
                Assert.That(origins.ContainsKey(19), Is.True);
                landed.Invoke(system, new object[] { 19UL, -2, 11U, 2U, Vector3.zero, true });
                Assert.That(confirmed, Is.EqualTo(4));
                Assert.That(origins.ContainsKey(19), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void TransitionAudit_DepartureAcceptsItsOwnSnapshotButRejectsAnotherFlight()
        {
            var root = new GameObject("Device snapshot ordering");
            root.SetActive(false);
            try
            {
                var cannon = root.AddComponent<HumanCannonSystem>();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var flights = (CannonFlightState)typeof(HumanCannonSystem).GetField("flying", fields).GetValue(cannon);
                var release = typeof(HumanCannonSystem).GetMethod("ApplyPoleRelease", fields);
                flights.Begin(19, -2, 11);
                Assert.That(flights.TryRebound(19, -2, 11, 0), Is.True);
                bool Apply(uint revision) => (bool)release.Invoke(cannon,
                    new object[] { 19UL, -2, revision, 0U, 3, Vector3.zero, Quaternion.identity, Vector3.forward, 1f });
                Assert.That(Apply(10), Is.False);
                Assert.That(Apply(11), Is.True);
                Assert.That(flights.Matches(19, -2, 11, 1), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TransitionAudit_RemoteCanGrabASwingBetweenRenderedPositions()
        {
            var root = new GameObject("Remote swing approach");
            var playerRoot = new GameObject("Remote rendered position");
            playerRoot.SetActive(false);
            try
            {
                var pole = root.AddComponent<HumanCannonPole>();
                var player = playerRoot.AddComponent<CurtainCall.Network.PlayerSync.NetworkPlayer>();
                var network = playerRoot.AddComponent<Unity.Netcode.Components.NetworkTransform>();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
                typeof(CurtainCall.Network.PlayerSync.NetworkPlayer).GetField("networkTransform", fields).SetValue(player, network);
                var latest = typeof(Unity.Netcode.Components.NetworkTransform).GetField("m_InternalCurrentPosition", fields);
                playerRoot.transform.position = pole.GrabPoint(0, 0) + Vector3.forward * 15f;
                latest.SetValue(network, pole.GrabPoint(0, 0) - Vector3.up * .75f);
                var reach = typeof(HumanCannonPole).GetMethod("IsSeatInReach", fields);
                object[] args = { player, 0f, 0, -1d };
                Assert.That(reach.Invoke(pole, args), Is.True);
                latest.SetValue(network, playerRoot.transform.position);
                Assert.That(reach.Invoke(pole, args), Is.False);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(playerRoot); }
        }

        [Test]
        public void TransitionAudit_DisabledPracticeTrapezeCannotChangeItsInitializedPoleCount()
        {
            var root = new GameObject("Disabled practice trapeze");
            root.SetActive(false);
            try
            {
                root.AddComponent<CurtainCall.Tightrope.TightropeCourse>();
                var run = root.AddComponent<CurtainCall.Tightrope.TightropeRun>();
                run.SetPracticeMode(true);
                var trapeze = root.AddComponent<CurtainCall.Tightrope.TightropeTrapeze>();
                trapeze.ConfigurePracticeLesson(1);
                var poles = (Array)trapeze.GetType().GetField("poles", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(trapeze);
                poles.SetValue(Activator.CreateInstance(poles.GetType().GetElementType(), true), 0);
                trapeze.ConfigurePracticeLesson(3);
                Assert.That(trapeze.PoleCount, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

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
        public void AllPolesUseTwoVerticalSeatsRegardlessOfLegacyCapacityOrGate()
        {
            var root = new GameObject("Crew seat geometry") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var pole = root.AddComponent<HumanCannonPole>();
                Assert.IsNull(typeof(HumanCannonPole).GetField("riderCapacity", BindingFlags.Instance | BindingFlags.NonPublic));
                root.transform.rotation = Quaternion.Euler(0, 37, 0);
                Vector3 span = pole.HangingPosition(25, 1) - pole.HangingPosition(25, 0);
                Assert.That(Vector3.Distance(span, Vector3.down * 1.45f), Is.LessThan(.001f));
                Assert.That(pole.RiderCapacity, Is.EqualTo(2));
                var gate = root.AddComponent<HumanCannonSawGate>();
                typeof(HumanCannonPole).GetField("linkedGate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pole, gate);
                Assert.That(pole.RiderCapacity, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    [Category("CommonProgressionRegression")]
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
            Assert.IsNull(typeof(HumanCannonPole).GetField("riderCapacity", Instance));
            typeof(HumanCannonPoleSystem).GetField("poles", Instance).SetValue(receiver, new[] { pole });
            Type type = typeof(HumanCannonPoleSystem).GetNestedType("Runtime", BindingFlags.NonPublic);
            runtime = Activator.CreateInstance(type, new object[] { 4 });
            Array states = Array.CreateInstance(type, 1); states.SetValue(runtime, 0);
            typeof(HumanCannonPoleSystem).GetField("states", Instance).SetValue(receiver, states);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(6f, 10d, 0d, 6f)]
        [TestCase(0f, 10d, 0d, 0f)]
        [TestCase(6f, 10d, 13d, 3f)]
        [TestCase(0f, 10d, 13d, 3f)]
        public void PassengerUsesRemainingSupportTimeOnlyWhenTheSupportHasADeadline(
            float passengerSeconds, double now, double supportDeadline, float expected)
        {
            var method = typeof(HumanCannonPoleSystem).GetMethod("PassengerHold", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method.Invoke(null, new object[] { passengerSeconds, now, supportDeadline }), Is.EqualTo(expected));
        }

        [Test]
        public void DestroyedPoleStillAcceptsTheHostsAbandonment()
        {
            Deliver(Two, 8);
            Object.DestroyImmediate(root.GetComponent<HumanCannonPole>());
            var after = Two;
            after[0] = Vacant;
            Deliver(after, 9, action: 3, actor: 7, seat: 0);
            Assert.That(receiver.State(0).SeatOf(7), Is.EqualTo(-1));
            Assert.That(receiver.State(0).SeatOf(8), Is.EqualTo(2));
        }

        [Test]
        public void LocalPoleRebuildDoesNotRaiseTheHostsSnapshotFloor()
        {
            var parent = new GameObject("Common pole parent");
            parent.transform.SetParent(root.transform, false);
            HumanCannonPole MakePole()
            {
                var child = new GameObject("Rebuilt pole");
                child.transform.SetParent(parent.transform, false);
                return child.AddComponent<HumanCannonPole>();
            }
            var first = MakePole();
            int index = receiver.Register(first);
            var seat = receiver.State(index);
            Assert.IsTrue(seat.TryGrab(19, seat.Revision, 0d, 5f));
            uint revision = seat.Revision;
            for (int i = 0; i < 3; i++) Assert.That(receiver.Register(MakePole()), Is.EqualTo(index));
            Assert.That(receiver.State(index).Revision, Is.EqualTo(revision));
            Assert.That(receiver.State(index).Count, Is.Zero);
        }

        [TestCase(5u, true)]
        [TestCase(7u, false)]
        public void OnlyLandingOfTheReleasedFlightTriggersThePoleLandingEvent(uint completedLaunch, bool reported)
        {
            var origins = (Dictionary<ulong, (int Pole, int Station, uint Launch)>)typeof(HumanCannonPoleSystem)
                .GetField("flightOrigins", Instance).GetValue(receiver);
            origins[8] = (4, 1, 5u);
            var calls = new List<int>();
            receiver.Landed += (pole, actor, position, safe) => calls.Add(pole);
            typeof(HumanCannonPoleSystem).GetMethod("OnLandingConfirmed", Instance)
                .Invoke(receiver, new object[] { 8UL, 1, completedLaunch, 2u, Vector3.one, true });
            Assert.That(calls, Is.EqualTo(reported ? new[] { 4 } : Array.Empty<int>()));
        }

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
        public void OppositeRidersKeepIndependentFacingAndRejectFlagsForEmptySeats()
        {
            root.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            Deliver(Two, 8, reversedSeats: 4);
            Assert.That(Vector3.Dot(receiver.RiderRotation(0, 7) * Vector3.forward, root.transform.forward), Is.GreaterThan(.999f));
            Assert.That(Vector3.Dot(receiver.RiderRotation(0, 8) * Vector3.forward, root.transform.forward), Is.LessThan(-.999f));
            Deliver(Two, 9, reversedSeats: 2);
            Assert.That(receiver.State(0).Revision, Is.EqualTo(8));
            Assert.That(Field<bool>(2, "ReverseFacing"), Is.True);
        }

        [Test]
        public void GrabFacingUsesViewThenBodyThenApproachForSideOnCatches()
        {
            var choose = typeof(HumanCannonPoleSystem).GetMethod("FacesAgainstPole", BindingFlags.Static | BindingFlags.NonPublic);
            bool Reverse(Vector3 view, Vector3 body, Vector3 approach) => (bool)choose.Invoke(null,
                new object[] { Vector3.forward, view, body, approach });
            Assert.IsTrue(Reverse(Vector3.back + Vector3.up, Vector3.forward, Vector3.forward));
            Assert.IsFalse(Reverse(Vector3.forward, Vector3.back, Vector3.back));
            Assert.IsTrue(Reverse(Vector3.right, Vector3.back, Vector3.forward));
            Assert.IsTrue(Reverse(Vector3.up, Vector3.right, Vector3.back));
        }

        [TestCase(1f, -1f)]
        [TestCase(-1f, 1f)]
        public void ReverseRidersPumpAndLaunchRelativeToTheirGrabDirection(float input, float direction)
        {
            Deliver(Two, 8, reversedSeats: 1, speed: 30);
            var riders = (Array)runtime.GetType().GetField("Riders").GetValue(runtime);
            var rider = riders.GetValue(0);
            rider.GetType().GetField("Input").SetValue(rider, input);
            rider.GetType().GetField("LastInput").SetValue(rider, receiver.Clock);
            var system = typeof(HumanCannonPoleSystem);
            float drive = (float)system.GetMethod("RefreshSwingDrive", Instance).Invoke(receiver, new[] { runtime });
            Assert.That(Mathf.Sign(drive), Is.EqualTo(direction));
            Assert.That(receiver.State(0).TryRelease(7, 8, receiver.Clock, 1), Is.True);
            var released = Enum.Parse(system.GetNestedType("PoleEvent", BindingFlags.NonPublic), "Released");
            system.GetMethod("Publish", Instance).Invoke(receiver, new object[] { 0, released, 7UL, 0, false });
            var rotation = (Quaternion)runtime.GetType().GetField("ReleaseRotation").GetValue(runtime);
            var velocity = (Vector3)runtime.GetType().GetField("ReleaseVelocity").GetValue(runtime);
            Assert.That(Vector3.Dot(rotation * Vector3.forward, root.transform.forward), Is.LessThan(-.999f));
            Assert.That(Mathf.Sign(Vector3.Dot(velocity, root.transform.forward)), Is.EqualTo(direction));
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
        public void MovingSwingPresentationSamplesBetweenPacketsAndSnapsAtDiscontinuities()
        {
            var receive = runtime.GetType().GetMethod("ReceiveAngle");
            var display = runtime.GetType().GetMethod("DisplayAngle");
            void Push(float angle, double now, bool continuous) => receive.Invoke(runtime, new object[] { angle, now, continuous });
            float Sample(double now) => (float)display.Invoke(runtime, new object[] { now });

            Push(10, 1, true); // Late join: no previous angle.
            Assert.That(Sample(1), Is.EqualTo(10));
            Push(20, 1.05, true);
            Assert.That(Sample(1.075), Is.EqualTo(15).Within(.001f));
            Assert.That((float)runtime.GetType().GetField("Angle").GetValue(runtime), Is.EqualTo(20));
            Push(24, 1.075, true); // Early packet must start at the position already on screen.
            Assert.That(Sample(1.075), Is.EqualTo(15).Within(.001f));
            Assert.That(Sample(1.15), Is.EqualTo(24));
            Push(-15, 2, true);
            Assert.That(Sample(2), Is.EqualTo(-15));
            Push(0, 2.01, false);
            Assert.That(Sample(2.01), Is.Zero);
        }

        [Test]
        public void StationarySwingUsesTheSameContinuousPresentationAsMovingSwings()
        {
            Deliver(Two, 8, angle: 10, speed: 0);
            Deliver(Two, 8, angle: 20, speed: 0);
            var display = runtime.GetType().GetMethod("DisplayAngle");
            double now = Time.unscaledTimeAsDouble;
            Assert.That((float)display.Invoke(runtime, new object[] { now }), Is.EqualTo(10).Within(.001f), "A fixed pole must not snap on a regular Sync.");
            Assert.That((float)display.Invoke(runtime, new object[] { now + .025 }), Is.EqualTo(15).Within(.001f));
            Assert.That((float)runtime.GetType().GetField("Angle").GetValue(runtime), Is.EqualTo(20), "Release physics still use the server angle.");
        }

        [Test]
        public void DelayedSwingSnapshotsKeepMovingBrieflyThenStopPredictionWithinAngleLimit()
        {
            var receive = runtime.GetType().GetMethod("ReceiveAngle");
            var display = runtime.GetType().GetMethod("DisplayAngle");
            receive.Invoke(runtime, new object[] { 10f, 1d, false });
            runtime.GetType().GetField("Speed").SetValue(runtime, 40f);
            float Sample(double now) => (float)display.Invoke(runtime, new object[] { now });
            float multiplier = CurtainCall.Settings.GameSettings.Poles.SwingMotionSpeed;
            Assert.That(Sample(1.075), Is.EqualTo(10f + 3f * multiplier).Within(.001f));
            Assert.That(Sample(1.5), Is.EqualTo(10f + 4f * multiplier).Within(.001f), "A lost connection must not predict indefinitely.");
            runtime.GetType().GetField("MaxDisplayAngle").SetValue(runtime, 12f);
            Assert.That(Sample(1.5), Is.EqualTo(12f));
        }

        [Test]
        public void OccupiedPoleSendsEveryTickWhileIdleRecoveryUsesItsOwnPhase()
        {
            var check = runtime.GetType().GetMethod("SnapshotDue");
            bool Due(double now) => (bool)check.Invoke(runtime, new object[] { now, .25d });
            Assert.IsTrue(Due(10));
            Assert.IsFalse(Due(10.10));
            Assert.IsTrue(Due(10.25));
            Assert.IsFalse(Due(10.30));
            Deliver(Two, 8);
            Assert.IsTrue(Due(10.35));
            Assert.IsTrue(Due(10.40));
            Deliver(new[] { Vacant, Vacant, Vacant, Vacant }, 9, action: 4, phase: PoleSawPhase.Active, angle: 0, speed: 0);
            Assert.IsFalse(Due(10.45));
            Assert.IsTrue(Due(11.25));
            Deliver(new[] { Vacant, Vacant, Vacant, Vacant }, 10, phase: PoleSawPhase.Active, angle: 30, speed: 0);
            Assert.IsTrue(Due(11.30), "빈 봉의 남은 반동도 원격으로 계속 전달한다.");
        }

        [Test]
        public void ReleaseDirectionIsSharedAndMalformedDirectionCannotChangeCrew()
        {
            Deliver(Two, 8, releaseDirection: -1f);
            Assert.That(runtime.GetType().GetField("ReleaseDirection").GetValue(runtime), Is.EqualTo(-1f));
            Deliver(Two, 9, releaseDirection: float.NaN);
            Assert.That(receiver.State(0).Revision, Is.EqualTo(8));
            Assert.That(runtime.GetType().GetField("ReleaseDirection").GetValue(runtime), Is.EqualTo(-1f));
        }

        [Test]
        public void ReleaseUsesLatestDriverInputEvenBeforeNextSimulationTick()
        {
            Deliver(Two, 8, speed: -5f, releaseDirection: 1f);
            var riders = (Array)runtime.GetType().GetField("Riders").GetValue(runtime);
            var top = riders.GetValue(0);
            top.GetType().GetField("Input").SetValue(top, -1f);
            top.GetType().GetField("LastInput").SetValue(top, receiver.Clock);
            var passenger = riders.GetValue(1);
            passenger.GetType().GetField("Input").SetValue(passenger, 1f);
            passenger.GetType().GetField("LastInput").SetValue(passenger, receiver.Clock);
            typeof(HumanCannonPoleSystem).GetMethod("RefreshSwingDrive", Instance).Invoke(receiver, new[] { runtime });
            Assert.That(runtime.GetType().GetField("ReleaseDirection").GetValue(runtime), Is.EqualTo(-1f));
        }

        [Test]
        public void ReverseDriveStaysActiveAsThePoleChangesDirection()
        {
            Deliver(Two, 8, speed: 30f, releaseDirection: 1f);
            var type = runtime.GetType();
            var riders = (Array)type.GetField("Riders").GetValue(runtime);
            var top = riders.GetValue(0);
            var input = top.GetType().GetField("Input");
            top.GetType().GetField("LastInput").SetValue(top, receiver.Clock);
            var refresh = typeof(HumanCannonPoleSystem).GetMethod("RefreshSwingDrive", Instance);
            var releaseDirection = type.GetField("ReleaseDirection");

            input.SetValue(top, -1f);
            Assert.That(refresh.Invoke(receiver, new[] { runtime }), Is.EqualTo(-1f));
            Assert.That(releaseDirection.GetValue(runtime), Is.EqualTo(-1f));

            type.GetField("Speed").SetValue(runtime, -20f);
            Assert.That(refresh.Invoke(receiver, new[] { runtime }), Is.EqualTo(-1f));
            Assert.That(releaseDirection.GetValue(runtime), Is.EqualTo(-1f));

            input.SetValue(top, 0f);
            Assert.That(refresh.Invoke(receiver, new[] { runtime }), Is.EqualTo(0f));
            Assert.That(releaseDirection.GetValue(runtime), Is.EqualTo(-1f));
        }

        [Test]
        public void RejectedPacketLeavesLiveSeatFlightUntouchedAndSubsequentSyncReusesSeat()
        {
            Deliver(Two, 8);
            var riders = (Array)runtime.GetType().GetField("Riders").GetValue(runtime);
            object firstSeat = riders.GetValue(0);
            var invalid = Two;
            invalid[0] = new Row(7, 3, 99, 5);
            invalid[3] = new Row(Empty, 0, 1, 0);
            Deliver(invalid, 9);
            Assert.That(Field<uint>(0, "Launch"), Is.EqualTo(12));
            invalid[3] = Vacant;
            Deliver(invalid, 9);
            Assert.That(Field<uint>(0, "Launch"), Is.EqualTo(99));
            Assert.That(riders.GetValue(0), Is.SameAs(firstSeat));
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

        [TestCase(false, 0)] [TestCase(true, 0)]
        [TestCase(false, 1)] [TestCase(true, 1)]
        public void DropHasNoImpulseButSpaceUsesTheCommonLaunchLimit(bool drop, int seat)
        {
            runtime.GetType().GetField("Angle").SetValue(runtime, 80f);
            runtime.GetType().GetField("Speed").SetValue(runtime, 120f);
            runtime.GetType().GetField("ReleaseDirection").SetValue(runtime, 1f);
            var released = Enum.Parse(typeof(HumanCannonPoleSystem).GetNestedType("PoleEvent", BindingFlags.NonPublic), "Released");
            typeof(HumanCannonPoleSystem).GetMethod("Publish", Instance)
                .Invoke(receiver, new object[] { 0, released, 7UL, seat, drop });
            Vector3 velocity = (Vector3)runtime.GetType().GetField("ReleaseVelocity").GetValue(runtime);
            Vector3 position = (Vector3)runtime.GetType().GetField("ReleasePosition").GetValue(runtime);
            Assert.That(position, Is.EqualTo(root.GetComponent<HumanCannonPole>().HangingPosition(80f, seat)));
            if (drop) Assert.That(velocity, Is.EqualTo(Vector3.zero));
            else
            {
                Assert.That(velocity.y, Is.GreaterThan(0f));
                Assert.That(Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude,
                    Is.EqualTo(CurtainCall.Settings.GameSettings.Poles.HorizontalSpeedLimit).Within(.001f));
            }
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ZeroImpulseDropSurvivesTheReleaseSnapshotForEveryRecipient(int recipients)
        {
            for (int i = 0; i < recipients; i++)
            {
                uint revision = (uint)(10 + i * 2);
                Deliver(Two, revision);
                var after = Two; after[0] = Vacant;
                Deliver(after, revision + 1, action: 2, actor: 7, seat: 0,
                    station: -1, launch: 12, segment: 1, releaseVelocity: Vector3.zero);
                Assert.That(receiver.HeldBy(7), Is.EqualTo(-1));
                Assert.That(runtime.GetType().GetField("ReleaseVelocity").GetValue(runtime), Is.EqualTo(Vector3.zero));
            }
        }

        T Field<T>(int seat, string name)
        {
            var riders = (Array)runtime.GetType().GetField("Riders").GetValue(runtime);
            object rider = riders.GetValue(seat);
            return (T)rider.GetType().GetField(name).GetValue(rider);
        }
        void Deliver(Row[] rows, uint revision, byte action = 0, ulong actor = Empty, byte seat = 255,
            int station = 0, uint launch = 0, uint segment = 0, ulong sender = NetworkManager.ServerClientId,
            PoleSawPhase phase = PoleSawPhase.Retracted, byte reversedSeats = 0, float angle = 20f, float speed = 5f,
            float releaseDirection = 0f, Vector3? releaseVelocity = null)
        {
            using var writer = new FastBufferWriter(action == 2 ? 204 : 164, Allocator.Temp);
            writer.WriteValueSafe(action); writer.WriteValueSafe(0); writer.WriteValueSafe(revision);
            writer.WriteValueSafe((byte)phase); writer.WriteValueSafe(0d); writer.WriteValueSafe(angle); writer.WriteValueSafe(speed);
            writer.WriteValueSafe(actor); writer.WriteValueSafe(releaseDirection); writer.WriteValueSafe(seat); writer.WriteValueSafe(station);
            writer.WriteValueSafe(launch); writer.WriteValueSafe(segment); writer.WriteValueSafe((byte)(reversedSeats << 1));
            foreach (Row row in rows)
            {
                writer.WriteValueSafe(row.Actor); writer.WriteValueSafe(0d); writer.WriteValueSafe(row.Station);
                writer.WriteValueSafe(row.Launch); writer.WriteValueSafe(row.Segment);
            }
            if (action == 2)
            {
                writer.WriteValueSafe(Vector3.one); writer.WriteValueSafe(Quaternion.identity); writer.WriteValueSafe(releaseVelocity ?? Vector3.up);
            }
            Assert.That(writer.Length, Is.EqualTo(action == 2 ? 204 : 164));
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            typeof(HumanCannonPoleSystem).GetMethod("ReceiveState", Instance).Invoke(receiver, new object[] { sender, reader });
        }
    }
}
