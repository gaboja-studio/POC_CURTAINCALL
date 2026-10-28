using System;
using System.Collections.Generic;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 외줄 추락 매달림 장부(순수 규칙, 호스트 판정). 근거: Harness/Project/Decisions/tightrope-course-rules.md #1-1(M2 2026-10-27).
    /// 매달린 사람은 버티는 시간이 흐르고, 동료가 끌어올리는 동안만 멈춘다. 끌어올리기가 끝나면 줄 위로, 시간이 다 지나면 낙사.
    /// 한 줄에는 가까운 거리에 한 명만 매달린다. 목마 낙하처럼 여러 명이면 먼저 요청한 사람부터 근처 빈 줄을 잡는다.
    /// </summary>
    public sealed class TightropeHangBook
    {
        public const ulong NoRescuer = ulong.MaxValue;

        public sealed class Hang
        {
            public ulong Id;
            public int Lane;
            public float Distance;
            public double Remaining;
            public ulong Rescuer = NoRescuer;
            public double PullRemaining;
            /// <summary>끌어올리기 시작 때 도와주는 동료의 잃은 부위. 바뀌면(다침) 끊긴다.</summary>
            public int RescuerParts;
            public bool IsPulling => Rescuer != NoRescuer;
        }

        public enum Outcome { None, Rescued, Dropped }

        readonly Dictionary<ulong, Hang> hangs = new();
        readonly List<ulong> order = new();

        public int Count => hangs.Count;
        public IEnumerable<Hang> All { get { foreach (ulong id in order) yield return hangs[id]; } }
        public bool IsHanging(ulong id) => hangs.ContainsKey(id);
        public bool TryGet(ulong id, out Hang hang) => hangs.TryGetValue(id, out hang);
        public bool IsRescuing(ulong rescuer)
        {
            foreach (var hang in hangs.Values) if (hang.Rescuer == rescuer) return true;
            return false;
        }

        /// <summary>
        /// 떨어진 줄에서 가까운 순서로 빈 줄을 고른다(같은 줄 → 한 칸 옆 → …, 같은 거리면 왼쪽 먼저). <paramref name="usable"/>은 그 거리에 줄이 있는지.
        /// 이미 다른 사람이 가까이 매달린 줄은 건너뛴다. 없으면 -1.
        /// </summary>
        public int PickLane(int preferred, float distance, int laneCount, int reach, float occupyDistance, Func<int, float, bool> usable)
        {
            for (int offset = 0; offset <= Math.Max(0, reach); offset++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (offset == 0 && side > 0) break;
                    int lane = preferred + offset * side;
                    if (lane < 0 || lane >= laneCount || (usable != null && !usable(lane, distance))) continue;
                    if (IsOccupied(lane, distance, occupyDistance)) continue;
                    return lane;
                }
            }
            return -1;
        }

        bool IsOccupied(int lane, float distance, float occupyDistance)
        {
            foreach (var hang in hangs.Values)
                if (hang.Lane == lane && Math.Abs(hang.Distance - distance) < occupyDistance) return true;
            return false;
        }

        public Hang Begin(ulong id, int lane, float distance, double holdSeconds)
        {
            if (hangs.ContainsKey(id) || lane < 0) return null;
            var hang = new Hang { Id = id, Lane = lane, Distance = distance, Remaining = Math.Max(0d, holdSeconds) };
            hangs.Add(id, hang);
            order.Add(id);
            return hang;
        }

        /// <summary>끌어올리기 시작. 이미 누가 끌어올리는 중이거나, 본인·다른 매달린 사람·다른 사람을 끌어올리는 중이면 false.</summary>
        public bool StartPull(ulong target, ulong rescuer, int rescuerParts, double pullSeconds)
        {
            if (!hangs.TryGetValue(target, out var hang) || hang.IsPulling || rescuer == target
                || hangs.ContainsKey(rescuer) || IsRescuing(rescuer)) return false;
            hang.Rescuer = rescuer;
            hang.RescuerParts = rescuerParts;
            hang.PullRemaining = Math.Max(0d, pullSeconds);
            return true;
        }

        /// <summary>끌어올리기가 끊긴다(동료가 다치거나 죽거나 취소). 남은 버티는 시간이 다시 흐른다.</summary>
        public bool CancelPull(ulong target)
        {
            if (!hangs.TryGetValue(target, out var hang) || !hang.IsPulling) return false;
            hang.Rescuer = NoRescuer;
            hang.PullRemaining = 0d;
            return true;
        }

        public bool Remove(ulong id)
        {
            if (!hangs.Remove(id)) return false;
            order.Remove(id);
            return true;
        }

        public void Clear() { hangs.Clear(); order.Clear(); }

        /// <summary>시간을 흘린다. 끝난 사람(구조·낙사)은 장부에서 빼고 결과로 돌려준다.</summary>
        public List<(ulong id, Outcome outcome, Hang hang)> Tick(double deltaSeconds)
        {
            var finished = new List<(ulong, Outcome, Hang)>();
            double step = Math.Max(0d, deltaSeconds);
            foreach (ulong id in order)
            {
                var hang = hangs[id];
                if (hang.IsPulling)
                {
                    hang.PullRemaining -= step;
                    if (hang.PullRemaining <= 0d) finished.Add((id, Outcome.Rescued, hang));
                }
                else
                {
                    hang.Remaining -= step;
                    if (hang.Remaining <= 0d) { hang.Remaining = 0d; finished.Add((id, Outcome.Dropped, hang)); }
                }
            }
            foreach (var item in finished) Remove(item.Item1);
            return finished;
        }

        /// <summary>클라이언트 사본: 호스트 스냅샷으로 바꾼다.</summary>
        public void Replace(IEnumerable<Hang> snapshot)
        {
            Clear();
            foreach (var hang in snapshot)
                if (!hangs.ContainsKey(hang.Id)) { hangs.Add(hang.Id, hang); order.Add(hang.Id); }
        }
    }
}
