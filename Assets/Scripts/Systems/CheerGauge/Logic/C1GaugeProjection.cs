using System;
using System.Collections.Generic;
using CurtainCall.Reward;

namespace CurtainCall.CheerGauge
{
    /// <summary>공연 동안 단원 순서를 고정하는 읽기 전용 표시 값.</summary>
    public sealed class C1GaugeFrame
    {
        public IReadOnlyList<long> Scores { get; }
        public long Total { get; }
        public long Capacity { get; }
        public int Count => Scores.Count;
        public string Identity { get; }

        internal C1GaugeFrame(long[] scores, long total, long capacity, string identity)
        {
            Scores = Array.AsReadOnly(scores);
            Total = total;
            Capacity = capacity;
            Identity = identity;
        }
    }

    /// <summary>Unity나 보상 판정에 의존하지 않고 보상 상태를 표시 값으로 변환한다.</summary>
    public sealed class C1GaugeProjection
    {
        string identity;
        string[] participantOrder;

        public bool TryProject(RewardSnapshot snapshot, out C1GaugeFrame frame)
        {
            frame = null;
            if (snapshot == null || snapshot.Performance == null)
            {
                Reset();
                return false;
            }

            string nextIdentity = snapshot.Performance.Id;
            if (!string.Equals(identity, nextIdentity, StringComparison.Ordinal)) Reset();

            var segments = snapshot.Segments;
            if (snapshot.Rules.Cap <= 0 || segments == null || segments.Count < 1 || segments.Count > 4)
                return false;

            var byId = new Dictionary<string, RewardSegment>(StringComparer.Ordinal);
            foreach (var segment in segments)
            {
                if (segment == null || string.IsNullOrWhiteSpace(segment.ParticipantId)
                    || segment.Amount < 0 || segment.Amount > snapshot.Rules.Cap
                    || byId.ContainsKey(segment.ParticipantId))
                    return false;
                byId.Add(segment.ParticipantId, segment);
            }

            string[] order = participantOrder;
            if (order == null)
            {
                // Slot은 정렬 힌트일 뿐이며 -1이나 63도 배열 인덱스로 사용하지 않는다.
                var sorted = new List<RewardSegment>(segments);
                sorted.Sort((left, right) =>
                {
                    int slot = left.Slot.CompareTo(right.Slot);
                    return slot != 0 ? slot : StringComparer.Ordinal.Compare(left.ParticipantId, right.ParticipantId);
                });
                order = new string[sorted.Count];
                for (int i = 0; i < order.Length; i++) order[i] = sorted[i].ParticipantId;
            }
            else
            {
                // 사망·연결 해제에도 명부는 유지된다. 참가자 누락·교체는 잘못된 상태다.
                if (order.Length != byId.Count) return false;
                foreach (string id in order)
                    if (!byId.ContainsKey(id)) return false;
            }

            var scores = new long[order.Length];
            long total = 0;
            long capacity;
            try
            {
                checked
                {
                    capacity = (long)snapshot.Rules.Cap * order.Length;
                    for (int i = 0; i < order.Length; i++)
                    {
                        var segment = byId[order[i]];
                        // 사망자는 게이지를 지우지 않고 절반 인정으로 보여 준다(보상 확정 규칙). 이탈자는 0.
                        scores[i] = !segment.Connected ? 0 : segment.Dead ? segment.Amount / 2 : segment.Amount;
                        total += scores[i];
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            participantOrder = order;
            identity = nextIdentity;
            frame = new C1GaugeFrame(scores, total, capacity, identity);
            return true;
        }

        public void Reset()
        {
            identity = null;
            participantOrder = null;
        }
    }
}
