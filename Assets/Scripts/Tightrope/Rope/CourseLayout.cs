using System;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 외줄 코스의 모양 계산(MonoBehaviour 없음). 좌표는 코스 기준이다: 옆(side)은 코스 오른쪽 방향, 거리(distance)는 출발선(0)에서 전진 방향.
    /// 줄은 왼쪽부터 0번이며 간격만큼 나란하고, 줄마다 0에서 시작해 끝 거리에서 끝난다(도착 거리까지 가는 줄이 마지막 한 줄).
    /// </summary>
    public sealed class CourseLayout
    {
        /// <summary>줄 없음.</summary>
        public const int NoLane = -1;

        readonly float[] laneEnds;
        readonly Vector2[] laneChangeZones;

        public CourseLayout(int laneCount, float laneSpacing, float finishDistance, float[] laneEndDistances, Vector2[] laneChangeZones)
        {
            LaneCount = Mathf.Max(1, laneCount);
            LaneSpacing = Mathf.Max(0.01f, laneSpacing);
            FinishDistance = Mathf.Max(0.01f, finishDistance);

            laneEnds = new float[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                // 값이 없거나 0 이하이면 도착까지 가는 줄로 본다
                float end = laneEndDistances != null && i < laneEndDistances.Length ? laneEndDistances[i] : FinishDistance;
                laneEnds[i] = end <= 0f ? FinishDistance : Mathf.Min(end, FinishDistance);
            }

            this.laneChangeZones = laneChangeZones != null ? (Vector2[])laneChangeZones.Clone() : Array.Empty<Vector2>();
        }

        public int LaneCount { get; }
        public float LaneSpacing { get; }
        public float FinishDistance { get; }

        /// <summary>그 줄이 끝나는 거리.</summary>
        public float GetLaneEnd(int lane) => IsValidLane(lane) ? laneEnds[lane] : 0f;

        /// <summary>그 줄의 옆 좌표(코스 가운데가 0).</summary>
        public float GetLaneSide(int lane) => (lane - (LaneCount - 1) * 0.5f) * LaneSpacing;

        public bool IsValidLane(int lane) => lane >= 0 && lane < LaneCount;

        /// <summary>그 거리에 줄이 놓여 있는지(0 ~ 끝 거리).</summary>
        public bool LaneExists(int lane, float distance) =>
            IsValidLane(lane) && distance >= 0f && distance <= laneEnds[lane];

        /// <summary>도착 거리까지 가는 줄이 하나라도 있는지.</summary>
        public bool HasLaneToFinish()
        {
            foreach (float end in laneEnds)
                if (end >= FinishDistance) return true;
            return false;
        }

        /// <summary>그 거리에 놓인 줄 중 옆 좌표가 가장 가까운 줄. 그 거리에 줄이 없으면 <see cref="NoLane"/>.</summary>
        public int FindNearestLane(float side, float distance)
        {
            int best = NoLane;
            float bestGap = float.MaxValue;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                if (!LaneExists(lane, distance)) continue;
                float gap = Mathf.Abs(GetLaneSide(lane) - side);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = lane;
                }
            }
            return best;
        }

        /// <summary>
        /// 옆 좌표가 가장 가까운 줄 번호(그 거리에 줄이 놓였는지는 보지 않음). 줄이 끝난 뒤에도 "몇 번 줄 자리"인지 알 때 쓴다.
        /// </summary>
        public int GetLaneSlotAt(float side) =>
            Mathf.Clamp(Mathf.RoundToInt(side / LaneSpacing + (LaneCount - 1) * 0.5f), 0, LaneCount - 1);

        /// <summary>그 거리가 옆줄 이동 가능 구간 안인지. 구간이 하나도 없으면 어디서도 안 된다.</summary>
        public bool IsLaneChangeAllowed(float distance)
        {
            foreach (var zone in laneChangeZones)
                if (distance >= Mathf.Min(zone.x, zone.y) && distance <= Mathf.Max(zone.x, zone.y)) return true;
            return false;
        }

        /// <summary>옆줄 이동 가능 구간 목록(복사본). x = 시작 거리, y = 끝 거리.</summary>
        public Vector2[] GetLaneChangeZones() => (Vector2[])laneChangeZones.Clone();
    }
}
