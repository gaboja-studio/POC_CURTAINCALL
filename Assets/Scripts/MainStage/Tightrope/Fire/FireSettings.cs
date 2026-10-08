using System;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    /// <summary>호스트가 회차 시작에 복사하는 초기 화재 실험값. 플레이 중 편집은 다음 회차에 적용한다.</summary>
    [Serializable]
    public sealed class FireSettings
    {
        [Tooltip("추격 불 사용 여부.")]
        public bool chaseEnabled = true;
        [Tooltip("묘기 시작부터 추격 불 출발까지(초).")]
        [Min(0f)] public float chaseDelay = 10f;
        [Tooltip("추격 불 앞면의 출발 거리(m). 출발선 뒤는 음수.")]
        public float chaseStart = -6f;
        [Tooltip("추격 속도(m/s).")]
        [Min(0f)] public float chaseSpeed = 0.35f;
        [Tooltip("추격 정지 및 화재 안전 구간 시작 거리(m). 이 거리 이상은 모든 화재에서 제외.")]
        public float safeDistance = 95f;
        [Tooltip("구간 발화 전 경고 시간(초).")]
        [Min(0f)] public float warningSeconds = 10f;
        [Tooltip("왼쪽부터 줄 번호 1~4. 구간 끝은 제외하며 점프·목마 높이와 무관하게 닿으면 사망.")]
        public FireSegment[] segments =
        {
            new(1, 55f, 95f, 120f), new(4, 55f, 95f, 120f), new(3, 70f, 95f, 180f),
        };

        public string ToJson() => JsonUtility.ToJson(this);

        public static FireSettings FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<FireSettings>(json); }
            catch (ArgumentException) { return null; }
        }

        /// <summary>네트워크 크기와 유효 범위를 제한한 복사본. 원본 에셋은 수정하지 않는다.</summary>
        public FireSettings Snapshot(float finishDistance)
        {
            var copy = FromJson(ToJson()) ?? new FireSettings();
            copy.chaseDelay = NonNegative(copy.chaseDelay);
            copy.chaseSpeed = NonNegative(copy.chaseSpeed);
            copy.safeDistance = Mathf.Clamp(Finite(copy.safeDistance, 95f), 0f, finishDistance);
            copy.chaseStart = Mathf.Min(Finite(copy.chaseStart, -6f), copy.safeDistance);
            copy.warningSeconds = NonNegative(copy.warningSeconds);
            copy.segments ??= Array.Empty<FireSegment>();
            if (copy.segments.Length > 64) Array.Resize(ref copy.segments, 64);
            for (int i = 0; i < copy.segments.Length; i++)
            {
                var segment = copy.segments[i];
                float from = Finite(segment.from, 0f), to = Finite(segment.to, 0f);
                segment.from = Mathf.Clamp(Mathf.Min(from, to), 0f, copy.safeDistance);
                segment.to = Mathf.Clamp(Mathf.Max(from, to), 0f, copy.safeDistance);
                segment.igniteAt = NonNegative(segment.igniteAt);
                copy.segments[i] = segment;
            }
            return copy;
        }

        static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        static float NonNegative(float value) => Mathf.Max(0f, Finite(value, 0f));
    }

    [Serializable]
    public struct FireSegment
    {
        [Tooltip("왼쪽부터 줄 번호(1부터).")]
        public int lane;
        [Tooltip("구간 시작 거리(m, 포함).")]
        public float from;
        [Tooltip("구간 끝 거리(m, 제외).")]
        public float to;
        [Tooltip("묘기 시작부터 발화까지(초).")]
        [Min(0f)] public float igniteAt;

        public FireSegment(int lane, float from, float to, float igniteAt)
        {
            this.lane = lane;
            this.from = from;
            this.to = to;
            this.igniteAt = igniteAt;
        }
    }

    /// <summary>높이를 사용하지 않는 화재 경계 계산.</summary>
    public static class FireMath
    {
        public static float ChaseDistance(FireSettings settings, float elapsed) =>
            Mathf.Min(settings.safeDistance, settings.chaseStart + settings.chaseSpeed * Mathf.Max(0f, elapsed - settings.chaseDelay));

        public static bool SegmentContains(FireSegment segment, float distance, float side, float laneSide, float halfWidth) =>
            distance >= segment.from && distance < segment.to && Mathf.Abs(side - laneSide) <= halfWidth;
    }
}
