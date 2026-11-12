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
        internal static readonly float[] Keys = { 0, ContactClip, PullClip, .60f, .82f, 1 };
        internal static readonly float[] HandForward = { .22f, .55f, .53f, .42f, .30f, .22f };
        internal static readonly float[] HandHeight = { 1.02f, ContactHandHeight, 1.10f, .77f, 1.04f, 1.02f };
        /// <summary>클립 접촉(손이 가장자리에 닿음)·당기기 키 시간과 접촉 때 루트 기준 손 높이.</summary>
        public const float ContactClip = .12f, PullClip = .32f, ContactHandHeight = 1.34f;
        /// <summary>손으로 가장자리를 짚는 구간 끝(무릎 넘기기). 이후 몸이 윗면으로 넘어간다.</summary>
        public const float KneeOver = .6f;
        /// <summary>몸 캡슐: 발 위 BodyBottom~BodyTop, 반경 BodyRadius. 다리는 클립이 벽 쪽으로 굽히므로 아래는 조금 띄운다.</summary>
        public const float BodyRadius = .22f, BodyBottom = .35f, BodyTop = 1.6f;
        /// <summary>
        /// 이 높이까지는 클립 한 동작으로 오른다(2026-11-12 사용자 확인 동작 유지). 넘으면 손 뻗기 뒤에 벽 타기 구간을 넣어
        /// 초과분을 같은 속도(1.5m/<see cref="PursuitSettings.ClimbSeconds"/>)로 오른다.
        /// </summary>
        public const float WallClimbFrom = 2.5f;
        /// <summary>벽 타기 한 번(손 짚고 당기기 → 다시 뻗기)에 오르는 높이(m).</summary>
        public const float WallCycle = .6f;
        const int Samples = 40, MaxSamples = 400;
        static readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly Collider[] overlaps = new Collider[16];

        /// <summary>진행률별 클립 시간과 벽 타기 위치. Wall은 벽 타기 구간 진행률(구간 밖이면 -1), Phase는 한 번 안의 진행률(0~0.5 짚고 당기기, 0.5~1 다시 뻗기).</summary>
        public struct Clock
        {
            public float Clip, Wall, Phase;
            public int Cycle;
            public bool OnWall => Wall >= 0;
        }

        /// <summary>전체 시간 중 벽 타기 구간 비율. 2.5m 이하는 0.</summary>
        public static float WallFraction(float height) => height > WallClimbFrom ? 1 - WallClimbFrom / height : 0;
        public static int WallCycles(float height) => Mathf.Max(1, Mathf.RoundToInt((height - WallClimbFrom) / WallCycle));

        /// <summary>
        /// 진행률 t → 오르기 클립 정규 시간. 2.5m 이하는 t 그대로. 넘으면 손 뻗기(0~0.12) → 벽 타기(접촉 0.12 ↔ 당기기 0.32 반복, 접촉에서 끝남) → 남은 클립(0.12~1).
        /// 호스트·원격이 같은 (높이, 진행률)로 같은 값을 얻는다.
        /// </summary>
        public static Clock ClockAt(float t, float height)
        {
            t = Mathf.Clamp01(t);
            float w = WallFraction(height);
            if (w <= 0) return new Clock { Clip = t, Wall = -1 };
            float rest = 1 - w, reach = Keys[1] * rest, done = reach + w;
            if (t < reach) return new Clock { Clip = t / rest, Wall = -1 };
            if (t >= done) return new Clock { Clip = Mathf.Min(1, Keys[1] + (t - done) / rest), Wall = -1 };
            float wall = (t - reach) / w, cycles = wall * WallCycles(height);
            int cycle = Mathf.FloorToInt(cycles);
            float phase = cycles - cycle, swing = phase < .5f ? phase * 2 : 2 - phase * 2;
            return new Clock { Clip = Mathf.Lerp(Keys[1], Keys[2], Mathf.SmoothStep(0, 1, swing)), Wall = wall, Phase = phase, Cycle = cycle };
        }
        public static float ClipTime(float t, float height) => ClockAt(t, height).Clip;

        readonly Vector3 from, to, forward;
        readonly Vector3[] points;
        readonly int samples;
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
            // 벽 타기: 손 뻗기 끝 자세(2.5m 오르기의 접촉 높이)에서 가장자리 접촉 자세까지 같은 앞 거리로 수직 상승.
            var wallStart = new Vector3(keyAt[1].x, Mathf.Clamp(WallClimbFrom - HandHeight[1], 0, height), 0);
            samples = WallFraction(height) > 0 ? Mathf.Clamp(Mathf.CeilToInt(Samples * height / WallClimbFrom), Samples, MaxSamples) : Samples;
            points = new Vector3[samples + 1];
            var allowed = new float[samples + 1];
            int knee = samples;
            for (int i = 0; i <= samples; i++) {
                float t = (float)i / samples;
                var clock = ClockAt(t, height);
                Vector3 key;
                if (WallFraction(height) <= 0) key = Sample(keyAt, t);
                else if (clock.OnWall) key = Vector3.Lerp(wallStart, keyAt[1], clock.Wall);
                else if (clock.Clip < Keys[1]) { float u = clock.Clip / Keys[1]; key = Vector3.Lerp(keyAt[0], wallStart, u * u * (3 - 2 * u)); }
                else key = Sample(keyAt, clock.Clip);
                points[i] = key;
                bool hanging = clock.OnWall || clock.Clip <= KneeOver;
                if (!hanging && knee == samples) knee = i - 1;
                allowed[i] = hanging && knee == samples ? Allowed(from + Vector3.up * key.y, forward, key.x, self) : float.PositiveInfinity;
            }
            // 가장자리를 짚는 동안은 몸이 옆면을 뚫지 않게: 나중에 더 막히는 높이가 있으면 미리 덜 들어간다(밖으로 되돌아가지 않음).
            for (int i = knee - 1; i >= 0; i--) allowed[i] = Mathf.Min(allowed[i], allowed[i + 1]);
            for (int i = 0; i <= knee; i++) points[i].x = Mathf.Min(points[i].x, allowed[i]);
        }

        public static PursuitClimbPath Build(Vector3 from, Vector3 to, Transform self) => new(from, to, self);

        // ---- 내려오기 ----
        /// <summary>내려오기 전에 윗면 가장자리까지 걷는 속도(m/s).</summary>
        public const float DropWalkSpeed = 3f;
        /// <summary>뛰어내린 뒤 이 비율(낙하 구간 기준) 안에 가장자리 밖 착지점 위로 나간다. 물체 옆면을 따라 미끄러지지 않게 먼저 바깥으로 나간다.</summary>
        const float DropOut = .45f;
        readonly bool drop;
        /// <summary>내려오기: 출발점에서 윗면이 끝나는 가장자리까지 수평 거리.</summary>
        public float DropEdge { get; }
        /// <summary>내려오기: 전체 시간 중 가장자리까지 걷는 비율(0이면 바로 뛰어내림).</summary>
        public float DropWalk { get; }
        /// <summary>내려오기 전체 초(걷기 + 뛰어내리기).</summary>
        public float Seconds { get; }

        PursuitClimbPath(Vector3 from, Vector3 to, Transform self, float dropSeconds)
        {
            this.from = from; this.to = to; drop = true; LipDistance = -1;
            var flat = Vector3.ProjectOnPlane(to - from, Vector3.up);
            float span = flat.magnitude;
            forward = span > .001f ? flat / span : Vector3.forward;
            DropEdge = Mathf.Min(span, PursuitDirectClimb.EdgeDistance(from, to, self));
            float walk = DropEdge / DropWalkSpeed;
            Seconds = walk + Mathf.Max(.1f, dropSeconds);
            DropWalk = walk / Seconds;
        }

        /// <summary>내려오기 경로: 가장자리까지 걸은 뒤 바깥으로 뛰어 착지점에 내린다(직선으로 물체 몸통을 지나지 않음).</summary>
        public static PursuitClimbPath BuildDrop(Vector3 from, Vector3 to, Transform self, float dropSeconds) => new(from, to, self, dropSeconds);

        /// <summary>내려오기 진행률 → 뛰어내리기 구간 진행률(걷는 동안 0).</summary>
        public static float DropPhase(float t, float walk) => walk <= 0 ? Mathf.Clamp01(t) : Mathf.Clamp01((t - walk) / Mathf.Max(.0001f, 1 - walk));

        Vector3 DropPose(float t)
        {
            var flat = Vector3.ProjectOnPlane(to - from, Vector3.up);
            float span = flat.magnitude;
            if (t < DropWalk) return from + forward * (DropEdge * t / Mathf.Max(.0001f, DropWalk));
            float v = DropPhase(t, DropWalk);
            float x = Mathf.Lerp(DropEdge, span, Mathf.SmoothStep(0, 1, Mathf.Clamp01(v / DropOut)));
            float y = Mathf.Lerp(from.y, to.y, v * v) + Mathf.Sin(v * Mathf.PI) * .25f;
            return new Vector3(from.x, y, from.z) + forward * x;
        }

        public Vector3 Pose(float t)
        {
            t = Mathf.Clamp01(t);
            if (drop) return t >= 1 ? to : DropPose(t);
            if (points == null) return PursuitActor.ClimbPose(from, to, true, t);
            float x = t * samples; int i = Mathf.Min(samples - 1, Mathf.FloorToInt(x));
            var p = Vector3.Lerp(points[i], points[i + 1], x - i);
            if (t >= 1) p = points[samples];
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
