using System;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 추격 스냅샷 바이너리 직렬화. 이전 JSON 문자열(추격자 26체 ≈ 16KB, 패킷 분할)을 추격자당 약 20바이트로 줄인다.
    /// 위치는 cm 단위 short(±327m 밖이면 float), 회전은 w≥0 정규화 후 x/y/z short. 알림 문구는 보내지 않는다(수신 측 설정에서 조회).
    /// </summary>
    public static class PursuitSnapshotCodec
    {
        public const byte Version = 2;
        /// <summary>비신뢰성 순서 전송으로 보낼 최대 크기. 한 패킷(MTU)에 들어가도록 여유를 둔다.</summary>
        public const int UnreliableLimit = 1100;
        const float PositionScale = 100f, PositionLimit = 327f;
        [Flags] enum ActorFlags : byte { None = 0, Look = 1, Target = 2, Victim = 4, Wide = 8 }

        public static int MaxSize(PursuitDirector.Snapshot s)
            => 40 + (s.actors?.Length ?? 0) * 64 + (s.victims?.Length ?? 0) * 64;

        public static void Write(FastBufferWriter w, PursuitDirector.Snapshot s)
        {
            w.WriteValueSafe(Version);
            w.WriteValueSafe(s.generation); w.WriteValueSafe(s.day); w.WriteValueSafe(s.performance);
            w.WriteValueSafe((short)s.stage); w.WriteValueSafe(s.stageForced);
            w.WriteValueSafe(s.elapsed);
            w.WriteValueSafe((short)s.lightRoom); w.WriteValueSafe(s.lightAt);
            var actors = s.actors ?? Array.Empty<PursuitDirector.ActorState>();
            w.WriteValueSafe((ushort)actors.Length);
            foreach (var a in actors) {
                bool wide = !Fits(a.position) || (a.hasLook && !Fits(a.look));
                bool victim = a.victim != ulong.MaxValue;
                var flags = (a.hasLook ? ActorFlags.Look : 0) | (a.hasTarget ? ActorFlags.Target : 0) | (victim ? ActorFlags.Victim : 0) | (wide ? ActorFlags.Wide : 0);
                w.WriteValueSafe((byte)flags);
                w.WriteValueSafe((byte)a.motion); w.WriteValueSafe((byte)a.behaviour);
                WritePosition(w, a.position, wide);
                WriteRotation(w, a.rotation);
                if (a.hasLook) WritePosition(w, a.look, wide);
                if (a.hasTarget) w.WriteValueSafe(a.target);
                if (victim) { w.WriteValueSafe(a.victim); w.WriteValueSafe(a.capture); }
            }
            var victims = s.victims ?? Array.Empty<PursuitDirector.VictimState>();
            w.WriteValueSafe((byte)Mathf.Min(victims.Length, 255));
            for (int i = 0; i < Mathf.Min(victims.Length, 255); i++) {
                var v = victims[i];
                w.WriteValueSafe(v.id); w.WriteValueSafe(v.elapsed); w.WriteValueSafe(v.retired); w.WriteValueSafe((short)v.room);
                w.WriteValueSafe(v.anchor); w.WriteValueSafe(v.upright);
            }
        }

        /// <summary>버전이 다르거나 손상되면 null.</summary>
        public static PursuitDirector.Snapshot Read(FastBufferReader r)
        {
            try {
                r.ReadValueSafe(out byte version);
                if (version != Version) return null;
                var s = new PursuitDirector.Snapshot();
                r.ReadValueSafe(out s.generation); r.ReadValueSafe(out s.day); r.ReadValueSafe(out s.performance);
                r.ReadValueSafe(out short stage); s.stage = stage; r.ReadValueSafe(out s.stageForced);
                r.ReadValueSafe(out s.elapsed);
                r.ReadValueSafe(out short lightRoom); s.lightRoom = lightRoom; r.ReadValueSafe(out s.lightAt);
                r.ReadValueSafe(out ushort count);
                s.actors = new PursuitDirector.ActorState[count];
                for (int i = 0; i < count; i++) {
                    var a = s.actors[i] = new PursuitDirector.ActorState();
                    r.ReadValueSafe(out byte raw); var flags = (ActorFlags)raw;
                    bool wide = (flags & ActorFlags.Wide) != 0;
                    r.ReadValueSafe(out byte motion); r.ReadValueSafe(out byte behaviour); a.motion = motion; a.behaviour = behaviour;
                    a.position = ReadPosition(r, wide);
                    a.rotation = ReadRotation(r);
                    a.hasLook = (flags & ActorFlags.Look) != 0;
                    if (a.hasLook) a.look = ReadPosition(r, wide);
                    a.hasTarget = (flags & ActorFlags.Target) != 0;
                    if (a.hasTarget) r.ReadValueSafe(out a.target);
                    if ((flags & ActorFlags.Victim) != 0) { r.ReadValueSafe(out a.victim); r.ReadValueSafe(out a.capture); }
                }
                r.ReadValueSafe(out byte victimCount);
                s.victims = new PursuitDirector.VictimState[victimCount];
                for (int i = 0; i < victimCount; i++) {
                    var v = s.victims[i] = new PursuitDirector.VictimState();
                    r.ReadValueSafe(out v.id); r.ReadValueSafe(out v.elapsed); r.ReadValueSafe(out v.retired);
                    r.ReadValueSafe(out short room); v.room = room;
                    r.ReadValueSafe(out v.anchor); r.ReadValueSafe(out v.upright);
                }
                return s;
            } catch (OverflowException) { return null; }
        }

        static bool Fits(Vector3 p) => Mathf.Abs(p.x) < PositionLimit && Mathf.Abs(p.y) < PositionLimit && Mathf.Abs(p.z) < PositionLimit;
        static void WritePosition(FastBufferWriter w, Vector3 p, bool wide)
        {
            if (wide) { w.WriteValueSafe(p); return; }
            w.WriteValueSafe((short)Mathf.RoundToInt(p.x * PositionScale));
            w.WriteValueSafe((short)Mathf.RoundToInt(p.y * PositionScale));
            w.WriteValueSafe((short)Mathf.RoundToInt(p.z * PositionScale));
        }
        static Vector3 ReadPosition(FastBufferReader r, bool wide)
        {
            if (wide) { r.ReadValueSafe(out Vector3 p); return p; }
            r.ReadValueSafe(out short x); r.ReadValueSafe(out short y); r.ReadValueSafe(out short z);
            return new Vector3(x / PositionScale, y / PositionScale, z / PositionScale);
        }
        static void WriteRotation(FastBufferWriter w, Quaternion q)
        {
            q = q.normalized;
            if (q.w < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            w.WriteValueSafe((short)Mathf.RoundToInt(q.x * short.MaxValue));
            w.WriteValueSafe((short)Mathf.RoundToInt(q.y * short.MaxValue));
            w.WriteValueSafe((short)Mathf.RoundToInt(q.z * short.MaxValue));
        }
        static Quaternion ReadRotation(FastBufferReader r)
        {
            r.ReadValueSafe(out short sx); r.ReadValueSafe(out short sy); r.ReadValueSafe(out short sz);
            float x = sx / (float)short.MaxValue, y = sy / (float)short.MaxValue, z = sz / (float)short.MaxValue;
            float w = Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y - z * z));
            return new Quaternion(x, y, z, w).normalized;
        }
    }
}
