using System;
using CurtainCall.Settings;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>The sender is the victim. There is deliberately no client-supplied victim ID.</summary>
    public struct PursuitContact
    {
        public const int Bytes = 57;
        public int Generation, Day, Performance, Epoch, Actor;
        public uint Sequence;
        public double PoseTime;
        public Vector3 ActorPosition, PlayerPosition;

        public void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe(PursuitSnapshotCodec.Version);
            writer.WriteValueSafe(Generation); writer.WriteValueSafe(Day); writer.WriteValueSafe(Performance);
            writer.WriteValueSafe(Epoch); writer.WriteValueSafe(Actor); writer.WriteValueSafe(Sequence);
            writer.WriteValueSafe(PoseTime); writer.WriteValueSafe(ActorPosition); writer.WriteValueSafe(PlayerPosition);
        }
        public static bool Read(FastBufferReader reader, out PursuitContact contact)
        {
            contact = default;
            if (reader.Length - reader.Position != Bytes) return false;
            reader.ReadValueSafe(out byte version);
            if (version != PursuitSnapshotCodec.Version) return false;
            reader.ReadValueSafe(out contact.Generation); reader.ReadValueSafe(out contact.Day); reader.ReadValueSafe(out contact.Performance);
            reader.ReadValueSafe(out contact.Epoch); reader.ReadValueSafe(out contact.Actor); reader.ReadValueSafe(out contact.Sequence);
            reader.ReadValueSafe(out contact.PoseTime); reader.ReadValueSafe(out contact.ActorPosition); reader.ReadValueSafe(out contact.PlayerPosition);
            return true;
        }
    }

    public enum PursuitContactRejection { None, State, Owner, Epoch, Sequence, Expired, NotTouching, ActorHistory, PlayerPosition, Occluded }

    /// <summary>Fixed-size host history. Compare the reported presentation to a recent chase trail,
    /// rather than comparing a delayed rendered actor to the host's present position.
    /// Any player touching a chasing actor is a valid victim, so samples are not tied to the chased target.</summary>
    public sealed class PursuitContactHistory
    {
        struct Sample { public double Time; public Vector3 Position; public bool Chasing; }
        readonly Sample[] samples = new Sample[64];
        int next, count;
        public void Clear() { next = count = 0; }
        public void Record(double time, Vector3 position, bool chasing)
        {
            if (!PursuitContactRules.Finite(time) || !PursuitContactRules.Finite(position)) return;
            if (count > 0 && time <= At(count - 1).Time) return;
            samples[next] = new Sample { Time = time, Position = position, Chasing = chasing };
            next = (next + 1) % samples.Length; count = Math.Min(count + 1, samples.Length);
        }
        Sample At(int index) => samples[(next - count + index + samples.Length) % samples.Length];
        public bool Matches(double poseTime, Vector3 presented)
        {
            bool anchor = false, match = false, previousValid = false;
            Vector3 previous = default;
            for (int i = 0; i < count; i++) {
                var s = At(i);
                if (Math.Abs(s.Time - poseTime) < .000001 && s.Chasing) anchor = true;
                if (s.Time < poseTime - .75 || s.Time > poseTime + .000001 || !s.Chasing) { previousValid = false; continue; }
                var closest = s.Position;
                if (previousValid) {
                    var segment = s.Position - previous;
                    closest = previous + segment * Mathf.Clamp01(Vector3.Dot(presented - previous, segment) / Mathf.Max(.000001f, segment.sqrMagnitude));
                }
                match |= (presented - closest).sqrMagnitude <= .6f * .6f;
                previous = s.Position; previousValid = true;
            }
            return anchor && match;
        }
    }

    public static class PursuitContactRules
    {
        public const double MaxAge = 1.25;
        public const float MaxPoseSilence = .35f;
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        public static bool Newer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;
        /// <summary>수평 거리 radius 이내이고, 플레이어 발이 추격자 발 기준 below 아래 ~ above 위 사이일 때 닿은 것이다.
        /// 트램펄린 표면·낮은 소품 위는 잡고, 높은 반동·점프 중인 공중은 잡지 않는다.</summary>
        public static bool Touching(Vector3 actor, Vector3 player, float radius,
            float below = PursuitSettings.DefaultCaptureBelow, float above = PursuitSettings.DefaultCaptureAbove)
        {
            if (!Finite(actor) || !Finite(player)) return false;
            var delta = player - actor;
            if (delta.y < -below || delta.y > above) return false;
            delta.y = 0;
            return delta.sqrMagnitude <= radius * radius;
        }
        public static bool Touching(Vector3 actor, Vector3 player, PursuitSettings rules)
            => Touching(actor, player, rules.CaptureRadius, rules.CaptureBelow, rules.CaptureAbove);
        public static PursuitContactRejection Validate(PursuitContact contact, PursuitContactHistory history,
            double now, Vector3 serverPlayer, float radius,
            float below = PursuitSettings.DefaultCaptureBelow, float above = PursuitSettings.DefaultCaptureAbove)
        {
            if (!Finite(contact.PoseTime) || !Finite(now) || contact.PoseTime < 0 || contact.PoseTime > now + .05
                || now - contact.PoseTime > MaxAge) return PursuitContactRejection.Expired;
            if (!Touching(contact.ActorPosition, contact.PlayerPosition, radius, below, above)) return PursuitContactRejection.NotTouching;
            if (history == null || !history.Matches(contact.PoseTime, contact.ActorPosition)) return PursuitContactRejection.ActorHistory;
            // The owner's movement already has owner authority. Allow bounded transit/movement error,
            // but never expand the actual contact radius on the victim's screen.
            float tolerance = 1 + 12 * (float)Math.Max(0, now - contact.PoseTime + .1);
            if (!Finite(serverPlayer) || (serverPlayer - contact.PlayerPosition).sqrMagnitude > tolerance * tolerance)
                return PursuitContactRejection.PlayerPosition;
            return PursuitContactRejection.None;
        }
    }
}
