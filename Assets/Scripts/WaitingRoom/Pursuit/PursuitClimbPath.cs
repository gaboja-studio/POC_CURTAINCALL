using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 오르기 몸 경로(호스트 전용, 원격은 위치 스냅샷을 따른다). 손이 잡는 실제 가장자리(콜라이더 윗면 시작점)를 찾아
    /// Blender 오르기 클립의 키포즈(Source~/pose-plan.py CLIMB: 접촉·당기기·무릎 넘기기)에서 손이 그 가장자리에 오도록 루트를 놓는다.
    /// 둥글게 튀어나온 옆면(눕힌 원기둥 등)은 몸 캡슐이 겹치지 않는 만큼만 붙고 나머지는 손 IK가 메운다.
    /// 가장자리를 찾지 못하면 기존 <see cref="PursuitActor.ClimbPose"/>를 쓴다.
    /// </summary>
    public sealed class PursuitClimbPath
    {
        /// <summary>오르기 클립 키 시간과 루트 기준 손 위치(앞 거리, 높이). pose-plan.py CLIMB 표와 같아야 한다.</summary>
        internal static readonly float[] Keys = { 0, .12f, .32f, .60f, .82f, 1 };
        internal static readonly float[] HandForward = { .22f, .55f, .53f, .42f, .30f, .22f };
        internal static readonly float[] HandHeight = { 1.02f, 1.34f, 1.10f, .77f, 1.04f, 1.02f };
        /// <summary>손으로 가장자리를 짚는 구간 끝(무릎 넘기기). 이후 몸이 윗면으로 넘어간다.</summary>
        public const float KneeOver = .6f;
        /// <summary>몸 캡슐: 발 위 BodyBottom~BodyTop, 반경 BodyRadius. 다리는 클립이 벽 쪽으로 굽히므로 아래는 조금 띄운다.</summary>
        public const float BodyRadius = .22f, BodyBottom = .35f, BodyTop = 1.6f;
        const int Samples = 40;
        static readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly Collider[] overlaps = new Collider[16];

        readonly Vector3 from, to, forward;
        readonly Vector3[] points;
        /// <summary>출발점에서 찾은 가장자리까지 수평 거리. 못 찾았으면 음수(기존 경로 사용).</summary>
        public float LipDistance { get; }
        public bool Shaped => points != null;

        PursuitClimbPath(Vector3 from, Vector3 to, Transform self)
        {
            this.from = from; this.to = to;
            var flat = Vector3.ProjectOnPlane(to - from, Vector3.up);
            float span = flat.magnitude;
            forward = span > .001f ? flat / span : Vector3.forward;
            LipDistance = span > .05f ? FindLip(from, to, forward, span, self) : -1;
            if (LipDistance < 0) return;
            float lip = LipDistance, height = to.y - from.y;
            // 키포즈별 루트: 손이 가장자리(lip, height)에 오는 자리. 바닥 아래·출발점 뒤로는 가지 않는다(남는 차이는 손 IK).
            var keyAt = new Vector3[Keys.Length];
            for (int k = 0; k < Keys.Length; k++) {
                if (k == 0) { keyAt[k] = Vector3.zero; continue; }
                if (Keys[k] > KneeOver) { keyAt[k] = k == Keys.Length - 1 ? new Vector3(span, height, 0) : new Vector3(Mathf.Min(span, lip + .15f), height, 0); continue; }
                keyAt[k] = new Vector3(Mathf.Clamp(lip - HandForward[k], 0, span), Mathf.Clamp(height - HandHeight[k], 0, height), 0);
            }
            points = new Vector3[Samples + 1];
            var allowed = new float[Samples + 1];
            for (int i = 0; i <= Samples; i++) {
                var key = Sample(keyAt, (float)i / Samples);
                points[i] = key;
                allowed[i] = (float)i / Samples <= KneeOver ? Allowed(from + Vector3.up * key.y, forward, key.x, self) : float.PositiveInfinity;
            }
            // 가장자리를 짚는 동안은 몸이 옆면을 뚫지 않게: 나중에 더 막히는 높이가 있으면 미리 덜 들어간다(밖으로 되돌아가지 않음).
            int knee = Mathf.RoundToInt(KneeOver * Samples);
            for (int i = knee - 1; i >= 0; i--) allowed[i] = Mathf.Min(allowed[i], allowed[i + 1]);
            for (int i = 0; i <= knee; i++) points[i].x = Mathf.Min(points[i].x, allowed[i]);
        }

        public static PursuitClimbPath Build(Vector3 from, Vector3 to, Transform self) => new(from, to, self);

        public Vector3 Pose(float t)
        {
            t = Mathf.Clamp01(t);
            if (points == null) return PursuitActor.ClimbPose(from, to, true, t);
            float x = t * Samples; int i = Mathf.Min(Samples - 1, Mathf.FloorToInt(x));
            var p = Vector3.Lerp(points[i], points[i + 1], x - i);
            if (t >= 1) p = points[Samples];
            return from + forward * p.x + Vector3.up * p.y;
        }

        // 클립과 같은 보간(구간마다 smoothstep).
        static Vector3 Sample(Vector3[] keys, float t)
        {
            for (int k = 0; k + 1 < Keys.Length; k++) {
                if (t > Keys[k + 1]) continue;
                float u = Mathf.Clamp01((t - Keys[k]) / (Keys[k + 1] - Keys[k]));
                return Vector3.Lerp(keys[k], keys[k + 1], u * u * (3 - 2 * u));
            }
            return keys[keys.Length - 1];
        }

        // 착지 높이 근처에서 출발점→착지점 방향으로 처음 나타나는 실제 윗면(경사 50° 이내)까지의 수평 거리.
        static float FindLip(Vector3 from, Vector3 to, Vector3 forward, float span, Transform self)
        {
            for (float d = .05f; d <= span + .001f; d += .05f) {
                var probe = new Vector3(from.x, to.y + .35f, from.z) + forward * d;
                int count = Physics.RaycastNonAlloc(probe, Vector3.down, hits, .7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) {
                    var h = hits[i];
                    if (Skip(h.collider, self) || h.normal.y < .64f) continue;
                    return d;
                }
            }
            return -1;
        }

        // 루트(발)를 forward로 옮길 때 몸 캡슐이 처음 닿기 전까지 갈 수 있는 거리(0~limit).
        static float Allowed(Vector3 feet, Vector3 forward, float limit, Transform self)
        {
            if (limit <= .001f) return 0;
            var bottom = feet + Vector3.up * (BodyBottom + BodyRadius);
            var top = feet + Vector3.up * (BodyTop - BodyRadius);
            int inside = Physics.OverlapCapsuleNonAlloc(bottom, top, BodyRadius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < inside; i++) if (!Skip(overlaps[i], self)) return 0;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, BodyRadius, forward, hits, limit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = limit;
            for (int i = 0; i < count; i++) {
                var h = hits[i];
                if (Skip(h.collider, self)) continue;
                best = Mathf.Min(best, Mathf.Max(0, h.distance - .02f));
            }
            return best;
        }

        static bool Skip(Collider c, Transform self)
            => PursuitDirectClimb.Ignored(c, self) || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic);
    }
}
