using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 추격자 길찾기 공용 규칙. 경로는 동기 <see cref="NavMesh.CalculatePath"/>로 즉시 계산한다.
    /// 다수 추격자가 SetDestination을 반복하면 비동기 경로 예산을 서로 초기화해 pathPending 상태로 제자리 걸음을 한다.
    /// </summary>
    public static class PursuitNavigation
    {
        static readonly float[] SampleRadii = { .6f, 2f, 4f, 8f };
        /// <summary>도달 불가(부분 경로) 대상의 거리 가산값. 도달 가능한 대상을 먼저 고른다.</summary>
        public const float PartialPathPenalty = 1000;

        /// <summary>점에서 가까운 NavMesh 위치. 소품 안·벽 속 지점도 점점 넓게 찾아 근방으로 옮긴다.</summary>
        public static bool TrySample(Vector3 position, int areaMask, out Vector3 point)
        {
            foreach (float radius in SampleRadii)
                if (NavMesh.SamplePosition(position, out var hit, radius, areaMask)) { point = hit.position; return true; }
            point = position;
            return false;
        }

        /// <summary>
        /// from에서 goal 근방까지의 최단 경로. 목표에 닿을 수 없으면 경로 끝(목표에 가장 가까운 도달 지점)으로 조정한다.
        /// </summary>
        public static bool TryPlan(Vector3 from, Vector3 goal, int areaMask, NavMeshPath path, out Vector3 reachable)
        {
            long started = Stopwatch.GetTimestamp();
            try {
                reachable = goal;
                if (!TrySample(goal, areaMask, out var sampled)) return false;
                if (!NavMesh.CalculatePath(from, sampled, areaMask, path) || path.status == NavMeshPathStatus.PathInvalid) return false;
                var corners = path.corners;
                if (corners.Length == 0) return false;
                reachable = path.status == NavMeshPathStatus.PathComplete ? sampled : corners[corners.Length - 1];
                return true;
            } finally { Record(Stopwatch.GetTimestamp() - started); }
        }

        // ---- 프레임 예산·계측 ----
        // 동시에 추격으로 바뀐 추격자들의 계산이 한 프레임에 몰리지 않게 프레임당 계산 수를 제한한다.
        // 고정 목표·추격 재계산·대상 선택은 예산이 없으면 다음 프레임으로 미루고 이전 경로를 유지한다.
        public const int FrameBudget = 8;
        /// <summary>NavMesh가 바뀌면(문 잠김/열림 깎기) 증가한다. 고정 목표 경로는 이 값이 바뀔 때만 다시 계산한다.</summary>
        public static int Version { get; private set; }
        public static void Invalidate() => Version++;
        static int frame = -1, frameCalls;
        static double frameMs;
        /// <summary>이번 프레임에 미룰 필요 없이 계산할 수 있는지.</summary>
        public static bool HasBudget { get { Roll(); return frameCalls < FrameBudget; } }
        public static long TotalCalls { get; private set; }
        public static double TotalMs { get; private set; }
        public static int MaxFrameCalls { get; private set; }
        public static double MaxFrameMs { get; private set; }
        public static void ResetStats() { TotalCalls = 0; TotalMs = MaxFrameMs = 0; MaxFrameCalls = 0; }
        static void Roll()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount; frameCalls = 0; frameMs = 0;
        }
        static void Record(long ticks)
        {
            Roll();
            double ms = ticks * 1000.0 / Stopwatch.Frequency;
            frameCalls++; frameMs += ms; TotalCalls++; TotalMs += ms;
            if (frameCalls > MaxFrameCalls) MaxFrameCalls = frameCalls;
            if (frameMs > MaxFrameMs) MaxFrameMs = frameMs;
        }

        /// <summary>경로 길이. 부분 경로는 <see cref="PartialPathPenalty"/>를 더하고 경로가 없으면 무한대.</summary>
        public static float PathCost(Vector3 from, Vector3 goal, int areaMask, NavMeshPath path)
        {
            if (!TryPlan(from, goal, areaMask, path, out _)) return float.PositiveInfinity;
            float length = Length(path.corners);
            return path.status == NavMeshPathStatus.PathComplete ? length : length + PartialPathPenalty;
        }

        public static float Length(Vector3[] corners)
        {
            float length = 0;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }
    }
}
