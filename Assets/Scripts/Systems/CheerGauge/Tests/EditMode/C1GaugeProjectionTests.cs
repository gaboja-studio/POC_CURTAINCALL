using System;
using System.Collections.Generic;
using CurtainCall.Results;
using CurtainCall.Reward;
using NUnit.Framework;

namespace CurtainCall.CheerGauge.Tests
{
    public sealed class C1GaugeProjectionTests
    {
        const string Session = "11111111111111111111111111111111";
        const string OtherSession = "22222222222222222222222222222222";

        static RewardSegment Segment(string id, long amount = 0, int slot = 0,
            bool dead = false, bool connected = true) =>
            new(id, 0, slot, 0, 0, dead, connected, amount);

        static RewardSnapshot Snapshot(int cap, RewardSegment[] segments, int index = 0,
            int generation = 1, string session = Session) =>
            new(session, generation, 1, 0, new PerformanceIdentity(session, generation, 1, index, "Test"),
                true, new RewardRules(10, 5, cap, 0, 0), segments, null, 0, 0, Array.Empty<FundEntry>());

        [Test]
        public void Total155_IsExact_NotNormalizedToPercentageOrCap()
        {
            var snapshot = Snapshot(150, new[] { Segment("b", 55, 1), Segment("a", 100) });
            Assert.IsTrue(new C1GaugeProjection().TryProject(snapshot, out var frame));
            CollectionAssert.AreEqual(new long[] { 100, 55 }, frame.Scores);
            Assert.AreEqual(155L, frame.Total);
            Assert.AreEqual(300L, frame.Capacity);
            Assert.AreEqual(2, frame.Count);
            Assert.AreEqual(snapshot.Performance.Id, frame.Identity);
            Assert.AreEqual("b", snapshot.Segments[0].ParticipantId, "Input order must not be changed.");
            Assert.AreEqual(55L, snapshot.Segments[0].Amount);
        }

        [TestCase(100)]
        [TestCase(150)]
        [TestCase(200)]
        public void SoloZero_StillHasOneSegmentAndFullCapacity(int cap)
        {
            Assert.IsTrue(new C1GaugeProjection().TryProject(
                Snapshot(cap, new[] { Segment("solo") }), out var frame));
            CollectionAssert.AreEqual(new long[] { 0 }, frame.Scores);
            Assert.AreEqual(1, frame.Count);
            Assert.AreEqual(0L, frame.Total);
            Assert.AreEqual((long)cap, frame.Capacity);
        }

        [Test]
        public void Cap200_PreservesActual155Points()
        {
            Assert.IsTrue(new C1GaugeProjection().TryProject(
                Snapshot(200, new[] { Segment("solo", 155) }), out var frame));
            Assert.AreEqual(155L, frame.Scores[0]);
            Assert.AreEqual(155L, frame.Total);
            Assert.AreEqual(200L, frame.Capacity);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SupportsEveryRosterSize_AndLongArithmetic(int count)
        {
            var segments = new RewardSegment[count];
            for (int i = 0; i < count; i++) segments[i] = Segment("p" + i, int.MaxValue, i);
            Assert.IsTrue(new C1GaugeProjection().TryProject(Snapshot(int.MaxValue, segments), out var frame));
            Assert.AreEqual(count, frame.Count);
            Assert.AreEqual((long)int.MaxValue * count, frame.Total);
            Assert.AreEqual((long)int.MaxValue * count, frame.Capacity);
        }

        [Test]
        public void InvalidInput_ReturnsFalseAndNull()
        {
            var projection = new C1GaugeProjection();
            Assert.IsFalse(projection.TryProject(null, out var frame));
            Assert.IsNull(frame);
            var noPerformance = new RewardSnapshot(Session, 1, 1, 0, null, false,
                new RewardRules(10, 5, 150, 0, 0), Array.Empty<RewardSegment>(), null,
                0, 0, Array.Empty<FundEntry>());
            Assert.IsFalse(projection.TryProject(noPerformance, out frame));
            Assert.IsNull(frame);
            Assert.IsFalse(projection.TryProject(Snapshot(0, new[] { Segment("a") }), out frame));
            Assert.IsNull(frame);
            Assert.IsFalse(projection.TryProject(Snapshot(150, Array.Empty<RewardSegment>()), out frame));
            Assert.IsNull(frame);
            Assert.IsFalse(projection.TryProject(Snapshot(150, new[]
            {
                Segment("a"), Segment("b"), Segment("c"), Segment("d"), Segment("e")
            }), out frame));
            Assert.IsNull(frame);
        }

        [Test]
        public void UnassignedDuplicateAndHighSlots_AreHints_WithOrdinalTieBreak()
        {
            Assert.IsTrue(new C1GaugeProjection().TryProject(Snapshot(150, new[]
            {
                Segment("z", 40, 63), Segment("b", 30, 2),
                Segment("a", 20, 2), Segment("unassigned", 10, -1)
            }), out var frame));
            CollectionAssert.AreEqual(new long[] { 10, 20, 30, 40 }, frame.Scores);
        }

        [Test]
        public void SamePerformance_ArraySlotAndClientOrderCannotChangeColorIndices()
        {
            var projection = new C1GaugeProjection();
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("b", 20, 1), Segment("a", 10, 0)
            }), out var first));
            Assert.IsTrue(projection.TryProject(Snapshot(200, new[]
            {
                new RewardSegment("b", 99, -1, 0, 0, false, true, 50), Segment("a", 30, 63)
            }), out var next));
            CollectionAssert.AreEqual(new long[] { 30, 50 }, next.Scores);
            CollectionAssert.AreEqual(new long[] { 10, 20 }, first.Scores, "Previously emitted frames stay immutable.");
            Assert.AreEqual(first.Identity, next.Identity);
            Assert.AreEqual(400L, next.Capacity);
            Assert.Throws<NotSupportedException>(() => ((IList<long>)next.Scores)[0] = 99);
        }

        [Test]
        public void DeathAndDisconnection_KeepRosterAndZeroScores()
        {
            var projection = new C1GaugeProjection();
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("a", 10, 0), Segment("b", 20, 1), Segment("c", 30, 2)
            }), out _));
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("c", 40, 2), Segment("b", 0, 1, connected: false), Segment("a", 0, 0, dead: true)
            }), out var frame));
            CollectionAssert.AreEqual(new long[] { 0, 0, 40 }, frame.Scores);
            Assert.AreEqual(3, frame.Count);
            Assert.AreEqual(450L, frame.Capacity);
            Assert.AreEqual(40L, frame.Total);
        }

        [Test]
        public void MissingReplacedOrAddedParticipant_IsInvalid_WithoutPoisoningCache()
        {
            var projection = new C1GaugeProjection();
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("a", 10, 0), Segment("b", 20, 1)
            }), out _));
            foreach (var invalid in new[]
            {
                new[] { Segment("a") },
                new[] { Segment("a"), Segment("c") },
                new[] { Segment("a"), Segment("b"), Segment("c") }
            })
            {
                Assert.IsFalse(projection.TryProject(Snapshot(150, invalid), out var rejected));
                Assert.IsNull(rejected);
            }
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("b", 50, -1), Segment("a", 30, 63)
            }), out var frame));
            CollectionAssert.AreEqual(new long[] { 30, 50 }, frame.Scores);
        }

        [TestCase(Session, 1, 1)]
        [TestCase(Session, 2, 0)]
        [TestCase(OtherSession, 1, 0)]
        public void PerformanceSessionOrGenerationChange_RebuildsRoster(string session, int generation, int index)
        {
            var projection = new C1GaugeProjection();
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("a", 10, 0), Segment("b", 20, 1)
            }), out var first));
            Assert.IsTrue(projection.TryProject(Snapshot(200, new[]
            {
                Segment("new", 60, 0)
            }, index, generation, session), out var next));
            Assert.AreNotEqual(first.Identity, next.Identity);
            CollectionAssert.AreEqual(new long[] { 60 }, next.Scores);
            Assert.AreEqual(1, next.Count);
        }

        [Test]
        public void Reset_AllowsNewInitialOrderEvenForSameIdentity()
        {
            var projection = new C1GaugeProjection();
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("a", 10, 0), Segment("b", 20, 1)
            }), out _));
            projection.Reset();
            Assert.IsTrue(projection.TryProject(Snapshot(150, new[]
            {
                Segment("b", 50, -1), Segment("a", 30, 63)
            }), out var frame));
            CollectionAssert.AreEqual(new long[] { 50, 30 }, frame.Scores);
        }
    }
}
