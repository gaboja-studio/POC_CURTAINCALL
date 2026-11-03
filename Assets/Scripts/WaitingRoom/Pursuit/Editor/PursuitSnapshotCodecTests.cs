using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitSnapshotCodecTests
    {
        static PursuitDirector.Snapshot Sample(int actors)
        {
            var s = new PursuitDirector.Snapshot { generation = 3, day = 2, performance = 1, stage = 2, stageForced = true, poseEpoch = 17, debugInvulnerable = true, debugIgnorePlayers = true, elapsed = 125.25f, lightRoom = 7, lightAt = 120.5f,
                actors = new PursuitDirector.ActorState[actors], victims = new[] {
                    new PursuitDirector.VictimState { id = 5, elapsed = 2.5f, retired = true, room = 4, anchor = new Vector3(1.25f, 2, -3), upright = Quaternion.Euler(0, 33, 0) } } };
            for (int i = 0; i < actors; i++)
                s.actors[i] = new PursuitDirector.ActorState { position = new Vector3(-36.123f + i, 5.5f, 105.987f), rotation = Quaternion.Euler(i * 7 - 40, i * 31, 0),
                    look = new Vector3(1, 2, 3), hasLook = i % 2 == 0, motion = i % 6, behaviour = i % 5, captures = i * 300,
                    hasTarget = i % 3 == 0, target = i % 3 == 0 ? 2u : ulong.MaxValue, victim = i == 1 ? 9u : ulong.MaxValue, capture = i == 1 ? 1.75f : 0 };
            return s;
        }
        static PursuitDirector.Snapshot RoundTrip(PursuitDirector.Snapshot s, out int bytes)
        {
            using var writer = new FastBufferWriter(PursuitSnapshotCodec.MaxSize(s), Allocator.Temp);
            PursuitSnapshotCodec.Write(writer, s);
            bytes = writer.Length;
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            return PursuitSnapshotCodec.Read(reader);
        }

        [Test]
        public void RoundTripKeepsStateWithinPrecision()
        {
            var s = Sample(26);
            var r = RoundTrip(s, out _);
            Assert.NotNull(r);
            Assert.AreEqual((s.poseEpoch, s.debugInvulnerable, s.debugIgnorePlayers), (r.poseEpoch, r.debugInvulnerable, r.debugIgnorePlayers));
            Assert.AreEqual((s.generation, s.day, s.performance, s.stage, s.stageForced, s.lightRoom), (r.generation, r.day, r.performance, r.stage, r.stageForced, r.lightRoom));
            Assert.AreEqual(s.elapsed, r.elapsed); Assert.AreEqual(s.lightAt, r.lightAt);
            Assert.AreEqual(26, r.actors.Length);
            for (int i = 0; i < 26; i++) {
                var a = s.actors[i]; var b = r.actors[i];
                Assert.Less(Vector3.Distance(a.position, b.position), .006f, "1cm 이내");
                Assert.Less(Quaternion.Angle(a.rotation, b.rotation), .1f, "회전 0.1도 이내");
                Assert.AreEqual((a.motion, a.behaviour, a.captures, a.hasLook, a.hasTarget, a.victim), (b.motion, b.behaviour, b.captures, b.hasLook, b.hasTarget, b.victim));
                if (a.hasLook) Assert.Less(Vector3.Distance(a.look, b.look), .006f);
                if (a.hasTarget) Assert.AreEqual(a.target, b.target);
                if (a.victim != ulong.MaxValue) Assert.AreEqual(a.capture, b.capture);
            }
            var v = r.victims[0];
            Assert.AreEqual((5ul, 2.5f, true, 4), (v.id, v.elapsed, v.retired, v.room));
            Assert.AreEqual(new Vector3(1.25f, 2, -3), v.anchor);
        }

        [Test]
        public void TwentySixActorsFitOneUnreliablePacketAndShrinkVersusJson()
        {
            var s = Sample(26);
            RoundTrip(s, out int bytes);
            int json = FastBufferWriter.GetWriteSize(JsonUtility.ToJson(s));
            Assert.LessOrEqual(bytes, PursuitSnapshotCodec.UnreliableLimit, $"binary={bytes}");
            Assert.Less(bytes * 10, json, $"binary={bytes}, json={json}");
        }

        [Test]
        public void FarPositionsUseFullPrecisionAndBadVersionIsRejected()
        {
            var s = Sample(1);
            s.actors[0].position = new Vector3(512.5f, -400, 1000);
            Assert.AreEqual(s.actors[0].position, RoundTrip(s, out _).actors[0].position);
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe((byte)1); writer.WriteValueSafe(42);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Assert.IsNull(PursuitSnapshotCodec.Read(reader));
            using var empty = new FastBufferWriter(1, Allocator.Temp);
            empty.WriteValueSafe(PursuitSnapshotCodec.Version);
            using var truncated = new FastBufferReader(empty, Allocator.Temp);
            Assert.IsNull(PursuitSnapshotCodec.Read(truncated), "잘린 데이터는 예외 없이 무시");
        }
    }
}
