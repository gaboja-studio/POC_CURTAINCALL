using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>Small host-time buffer. Packet loss freezes briefly; old packets never rewind the actor.</summary>
    public sealed class PursuitPoseBuffer
    {
        struct Pose { public double Time; public Vector3 Position; public Quaternion Rotation; }
        readonly List<Pose> samples = new(32);
        double cursor;
        public const float Delay = .15f;
        public void Clear() { samples.Clear(); cursor = 0; }
        public bool Push(double time, Vector3 position, Quaternion rotation)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w)) return false;
            if (samples.Count > 0 && time <= samples[^1].Time) return false;
            if (samples.Count == 0) cursor = time - Delay;
            samples.Add(new Pose { Time = time, Position = position, Rotation = rotation });
            if (samples.Count > 32) samples.RemoveAt(0);
            return true;
        }
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public bool Sample(float deltaTime, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (samples.Count == 0) return false;
            var last = samples[^1];
            double gap = last.Time - cursor;
            // Gradually refill after a stall, catch up without skipping to a newly arrived pose.
            float rate = gap > Delay + .1 ? 1.15f : gap < Delay - .08 ? .85f : 1;
            cursor = System.Math.Min(cursor + Mathf.Max(0, deltaTime) * rate, last.Time + .10);
            while (samples.Count > 2 && samples[1].Time < cursor) samples.RemoveAt(0);
            var a = samples[0];
            if (samples.Count == 1 || cursor <= a.Time) { position = a.Position; rotation = a.Rotation; return true; }
            var b = samples[1];
            float t = (float)((cursor - a.Time) / (b.Time - a.Time));
            position = Vector3.Lerp(a.Position, b.Position, t);
            rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
            if (t > 1) {
                var velocity = Vector3.ClampMagnitude((b.Position - a.Position) / (float)(b.Time - a.Time), 12);
                position = b.Position + velocity * Mathf.Min(.1f, (float)(cursor - b.Time));
            }
            return true;
        }
    }
}
