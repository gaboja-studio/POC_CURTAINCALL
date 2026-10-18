using System;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    public enum FireTriggerMode { Time, LeadDistance }
    public enum FireLaneOrder { Configured, RandomAll }

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
        [Tooltip("시간 또는 이번 회차 선두의 최고 진행 거리로 다음 줄의 경고를 시작한다.")]
        public FireTriggerMode triggerMode;
        [Tooltip("설정된 줄 순서 또는 호스트가 회차마다 정하는 무작위 순서. 무작위는 마지막 한 줄을 남긴다.")]
        public FireLaneOrder laneOrder;
        [Tooltip("배열 순서대로 한 줄씩 경고 후 점화. 왼쪽부터 줄 번호는 1부터이며, 마지막 안전줄은 배열에서 제외한다.")]
        public FireSegment[] segments =
        {
            new(1, 55f, 95f, 120f), new(4, 55f, 95f, 120f), new(3, 70f, 95f, 180f),
        };

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>소각 예정에 없는 줄이 정확히 하나면 1-based 번호, 없거나 여러 개면 -1.</summary>
        public int FindSurvivingLane(int laneCount)
        {
            int survivor = -1;
            for (int lane = 1; lane <= laneCount; lane++)
            {
                bool scheduled = false;
                if (segments != null)
                    foreach (var segment in segments)
                        if (segment.lane == lane) { scheduled = true; break; }
                if (scheduled) continue;
                if (survivor >= 0) return -1;
                survivor = lane;
            }
            return survivor;
        }

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
            if (copy.triggerMode != FireTriggerMode.Time && copy.triggerMode != FireTriggerMode.LeadDistance)
                copy.triggerMode = FireTriggerMode.Time;
            if (copy.laneOrder != FireLaneOrder.Configured && copy.laneOrder != FireLaneOrder.RandomAll)
                copy.laneOrder = FireLaneOrder.Configured;
            copy.segments ??= Array.Empty<FireSegment>();
            if (copy.segments.Length > 64) Array.Resize(ref copy.segments, 64);
            for (int i = 0; i < copy.segments.Length; i++)
            {
                var segment = copy.segments[i];
                float from = Finite(segment.from, 0f), to = Finite(segment.to, 0f);
                segment.from = Mathf.Clamp(Mathf.Min(from, to), 0f, copy.safeDistance);
                segment.to = Mathf.Clamp(Mathf.Max(from, to), 0f, copy.safeDistance);
                segment.igniteAt = NonNegative(segment.igniteAt);
                segment.triggerDistance = Mathf.Clamp(NonNegative(segment.triggerDistance), 0f, finishDistance);
                copy.segments[i] = segment;
            }
            return copy;
        }

        /// <summary>
        /// 호스트가 이번 회차의 줄 순서를 확정한다. 구간·시각·진행 거리는 템플릿 그대로 둔다.
        /// N개 줄을 한 줄까지 줄이려면 N-1개 템플릿이 필요하며, 초과 템플릿은 마지막 줄 보호를 위해 제외한다.
        /// </summary>
        public FireSettings Snapshot(float finishDistance, int laneCount, int seed)
        {
            var copy = Snapshot(finishDistance);
            if (copy.laneOrder != FireLaneOrder.RandomAll) return copy;
            var order = FireMath.RandomLaneOrder(laneCount, seed);
            int count = Mathf.Min(copy.segments.Length, order.Length - 1);
            if (copy.segments.Length != count) Array.Resize(ref copy.segments, count);
            for (int i = 0; i < count; i++) copy.segments[i].lane = order[i];
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
        [Tooltip("시간 기준의 발화 예정 시각(묘기 시작 후 초). 앞선 줄 경고가 끝나지 않았으면 순서대로 늦춰진다.")]
        [Min(0f)] public float igniteAt;
        [Tooltip("거리 기준에서 이 거리(m)에 선두가 도달하면 경고를 시작한다.")]
        [Min(0f)] public float triggerDistance;

        public FireSegment(int lane, float from, float to, float igniteAt, float triggerDistance = 0f)
        {
            this.lane = lane;
            this.from = from;
            this.to = to;
            this.igniteAt = igniteAt;
            this.triggerDistance = triggerDistance;
        }
    }

    /// <summary>높이를 사용하지 않는 화재 경계 계산.</summary>
    public static class FireMath
    {
        /// <summary>왼쪽부터 1-based 줄 번호를 한 번씩 섞는다. 마지막 번호가 최종 생존줄이다.</summary>
        public static int[] RandomLaneOrder(int laneCount, int seed)
        {
            var order = new int[Mathf.Max(1, laneCount)];
            for (int i = 0; i < order.Length; i++) order[i] = i + 1;
            var random = new System.Random(seed);
            for (int i = order.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (order[i], order[other]) = (order[other], order[i]);
            }
            return order;
        }

        /// <summary>
        /// 호스트 전용 순차 상태 계산. 한 줄이 발화한 뒤에만 다음 줄 경고를 시작한다.
        /// 이미 여러 기준을 넘겼어도 각 줄에 경고 시간을 주며, 한 번 호출에 최대 한 줄만 발화한다.
        /// </summary>
        public static bool AdvanceSequence(FireSettings settings, float elapsed, float furthestDistance, int laneCount,
            ref ulong activeMask, ref int pendingSegment, ref float pendingIgniteAt)
        {
            bool changed = false;
            if (pendingSegment >= 0)
            {
                if (elapsed < pendingIgniteAt) return false;
                activeMask |= 1UL << pendingSegment;
                pendingSegment = -1;
                pendingIgniteAt = -1f;
                changed = true;
            }
            for (int i = 0; i < settings.segments.Length; i++)
            {
                var segment = settings.segments[i];
                if ((activeMask & (1UL << i)) != 0 || segment.lane < 1 || segment.lane > laneCount || segment.to <= segment.from) continue;
                bool ready = settings.triggerMode == FireTriggerMode.LeadDistance
                    ? furthestDistance >= segment.triggerDistance : elapsed >= segment.igniteAt - settings.warningSeconds;
                if (!ready) return changed;
                pendingSegment = i;
                // 시간 모드도 늦게 시작한 경고를 생략하지 않는다. 정상 진행 시 기존 예정 시각에 발화한다.
                pendingIgniteAt = settings.triggerMode == FireTriggerMode.Time
                    ? Mathf.Max(segment.igniteAt, elapsed + settings.warningSeconds) : elapsed + settings.warningSeconds;
                return true;
            }
            return changed;
        }

        /// <summary>호스트 확정 전에는 0초 경고를 유지한다. 클라이언트 시간이 먼저 지나도 발화로 바꾸지 않는다.</summary>
        public static float WarningRemaining(int index, int pendingSegment, float pendingIgniteAt, float elapsed) =>
            index == pendingSegment && pendingSegment >= 0 ? Mathf.Max(0f, pendingIgniteAt - elapsed) : -1f;

        public static float ChaseDistance(FireSettings settings, float elapsed) =>
            Mathf.Min(settings.safeDistance, settings.chaseStart + settings.chaseSpeed * Mathf.Max(0f, elapsed - settings.chaseDelay));

        public static bool SegmentContains(FireSegment segment, float distance, float side, float laneSide, float halfWidth) =>
            distance >= segment.from && distance < segment.to && Mathf.Abs(side - laneSide) <= halfWidth;
    }
}
