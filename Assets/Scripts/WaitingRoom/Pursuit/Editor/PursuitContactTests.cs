using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitContactTests
    {
        const ulong Owner = 42;
        static PursuitContact Sample(double time = 10) => new PursuitContact {
            Generation = 2, Day = 3, Performance = 1, Epoch = 8, Actor = 12, Sequence = 7,
            PoseTime = time, ActorPosition = Vector3.zero, PlayerPosition = Vector3.right * .8f };
        static PursuitContactHistory Trail(double end = 10)
        {
            var h = new PursuitContactHistory();
            for (int i = 0; i <= 12; i++) h.Record(end - .6 + i * .05, Vector3.left * (.6f - i * .05f) * 6.5f, Owner, true);
            return h;
        }
        static PursuitContact RoundTrip(PursuitContact contact)
        {
            using var writer = new FastBufferWriter(PursuitContact.Bytes, Allocator.Temp);
            contact.Write(writer); Assert.AreEqual(PursuitContact.Bytes, writer.Length);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Assert.IsTrue(PursuitContact.Read(reader, out var result)); return result;
        }
        [Test] public void ContactWireRoundTripRejectsTruncationAndOldBuild()
        {
            var c = Sample(); var r = RoundTrip(c);
            Assert.AreEqual((c.Generation, c.Day, c.Performance, c.Epoch, c.Actor, c.Sequence, c.PoseTime),
                (r.Generation, r.Day, r.Performance, r.Epoch, r.Actor, r.Sequence, r.PoseTime));
            Assert.AreEqual(c.ActorPosition, r.ActorPosition); Assert.AreEqual(c.PlayerPosition, r.PlayerPosition);
            using var writer = new FastBufferWriter(PursuitContact.Bytes, Allocator.Temp);
            writer.WriteValueSafe((byte)(PursuitSnapshotCodec.Version - 1));
            for (int i = 1; i < PursuitContact.Bytes; i++) writer.WriteByteSafe(0);
            using var old = new FastBufferReader(writer, Allocator.Temp);
            Assert.IsFalse(PursuitContact.Read(old, out _));
            using var shortWriter = new FastBufferWriter(1, Allocator.Temp); shortWriter.WriteByteSafe(PursuitSnapshotCodec.Version);
            using var truncated = new FastBufferReader(shortWriter, Allocator.Temp);
            Assert.IsFalse(PursuitContact.Read(truncated, out _));
        }
        [TestCase(.05)] [TestCase(.15)] [TestCase(.3)] [TestCase(.6)] [TestCase(1.0)]
        public void ValidDisplayedContactSurvivesTransitWithoutUsingCurrentActorDistance(double transit)
        {
            var c = Sample();
            // The AI has moved metres by receipt; validation uses the pose actually sent/rendered.
            Assert.AreEqual(PursuitContactRejection.None,
                PursuitContactRules.Validate(RoundTrip(c), Trail(), Owner, 10 + transit, c.PlayerPosition, 1.15f));
        }
        [TestCase(.05)] [TestCase(.15)] [TestCase(.3)] [TestCase(.6)]
        public void AvoidedOnVictimScreenNeverBecomesContactBecauseOfHostOldPlayerPosition(double transit)
        {
            var c = Sample(); c.PlayerPosition = Vector3.right * 2;
            var hostOldPlayer = Vector3.right * .2f;
            Assert.IsTrue(PursuitContactRules.Touching(Vector3.zero, hostOldPlayer, 1.15f), "The old host-only check would catch.");
            Assert.IsFalse(PursuitContactRules.Touching(c.ActorPosition, c.PlayerPosition, 1.15f));
            Assert.AreEqual(PursuitContactRejection.NotTouching,
                PursuitContactRules.Validate(RoundTrip(c), Trail(), Owner, 10 + transit, hostOldPlayer, 1.15f));
        }
        [Test] public void DelayJitterTwentyPercentLossAndBurstOutageProduceNoGhostContact()
        {
            var buffer = new PursuitPoseBuffer();
            var pending = new List<(double sent, double arrival, Vector3 pos)>();
            int oldHostHits = 0, ownerReports = 0, samples = 0;
            for (int frame = 0; frame < 480; frame++) {
                double now = frame / 60d;
                if (frame % 3 == 0 && frame % 15 != 0 && (now < 2 || now > 2.35)) {
                    double delay = .08 + (frame % 21 == 0 ? .12 : .02);
                    pending.Add((now, now + delay, Vector3.right * (float)now * 6.5f));
                }
                for (int i = pending.Count - 1; i >= 0; i--) if (pending[i].arrival <= now) {
                    var p = pending[i]; buffer.Push(p.sent, p.pos, Quaternion.identity); pending.RemoveAt(i);
                }
                var player = Vector3.right * ((float)now * 6.5f + 1.5f);
                var hostOldPlayer = Vector3.right * ((float)(now - .18) * 6.5f + 1.5f);
                if (PursuitContactRules.Touching(Vector3.right * (float)now * 6.5f, hostOldPlayer, 1.15f)) oldHostHits++;
                if (buffer.Sample(1 / 60f, out var displayed, out _)) {
                    samples++;
                    if (PursuitContactRules.Touching(displayed, player, 1.15f)) ownerReports++;
                }
            }
            Assert.Greater(samples, 400); Assert.Greater(oldHostHits, 400);
            Assert.AreEqual(0, ownerReports, "No request exists to turn a clean dodge into a host capture.");
        }
        [Test] public void StaleFutureNonFiniteWrongOwnerAndImpossiblePositionsAreRejected()
        {
            var c = Sample(); var h = Trail();
            Assert.AreEqual(PursuitContactRejection.Expired, PursuitContactRules.Validate(c, h, Owner, 11.26, c.PlayerPosition, 1.15f));
            Assert.AreEqual(PursuitContactRejection.Expired, PursuitContactRules.Validate(c, h, Owner, 9.8, c.PlayerPosition, 1.15f));
            Assert.AreEqual(PursuitContactRejection.ActorHistory, PursuitContactRules.Validate(c, h, Owner + 1, 10.2, c.PlayerPosition, 1.15f));
            var notVisible = new PursuitContactHistory(); notVisible.Record(10, Vector3.zero, Owner, false);
            Assert.AreEqual(PursuitContactRejection.ActorHistory, PursuitContactRules.Validate(c, notVisible, Owner, 10.2, c.PlayerPosition, 1.15f));
            Assert.AreEqual(PursuitContactRejection.PlayerPosition, PursuitContactRules.Validate(c, h, Owner, 10.2, Vector3.one * 100, 1.15f));
            c.PoseTime = double.NaN;
            Assert.AreEqual(PursuitContactRejection.Expired, PursuitContactRules.Validate(c, h, Owner, 10.2, Vector3.zero, 1.15f));
            c = Sample(); c.ActorPosition = Vector3.one * float.NaN;
            Assert.AreEqual(PursuitContactRejection.NotTouching, PursuitContactRules.Validate(c, h, Owner, 10.2, Vector3.zero, 1.15f));
            c = Sample(); c.ActorPosition = Vector3.one * 50; c.PlayerPosition = c.ActorPosition;
            Assert.AreEqual(PursuitContactRejection.ActorHistory, PursuitContactRules.Validate(c, h, Owner, 10.2, c.PlayerPosition, 1.15f));
        }
        [Test] public void HistoryResetOverflowAndSequenceWrapDoNotAcceptReplay()
        {
            var h = Trail(); h.Clear();
            Assert.IsFalse(h.Matches(10, Vector3.zero, Owner));
            for (int i = 0; i < 200; i++) h.Record(i * .05, Vector3.right * i, Owner, true);
            Assert.IsFalse(h.Matches(.1, Vector3.right * 2, Owner));
            Assert.IsTrue(h.Matches(199 * .05, Vector3.right * 199, Owner));
            Assert.IsFalse(PursuitContactRules.Newer(5, 5)); Assert.IsFalse(PursuitContactRules.Newer(4, 5));
            Assert.IsTrue(PursuitContactRules.Newer(0, uint.MaxValue));
        }
        [Test] public void FrozenReplicaAndReorderedPoseCannotGenerateFreshContactEvidence()
        {
            var go = new GameObject("ContactFreshnessFixture");
            try {
                var actor = go.AddComponent<PursuitActor>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                bool Fresh() => (bool)typeof(PursuitActor).GetProperty("HasFreshContactPose", flags).GetValue(actor);
                Assert.IsFalse(Fresh());
                actor.ReceivePose(10, Vector3.zero, Quaternion.identity); Assert.IsTrue(Fresh());
                typeof(PursuitActor).GetField("contactPoseReceivedAt", flags).SetValue(actor, Time.realtimeSinceStartupAsDouble - 1);
                Assert.IsFalse(Fresh());
                actor.ReceivePose(9, Vector3.zero, Quaternion.identity); Assert.IsFalse(Fresh(), "An old packet cannot refresh a frozen replica.");
                actor.ReceivePose(10.05, Vector3.zero, Quaternion.identity); Assert.IsTrue(Fresh());
            } finally { Object.DestroyImmediate(go); }
        }
    }
}
