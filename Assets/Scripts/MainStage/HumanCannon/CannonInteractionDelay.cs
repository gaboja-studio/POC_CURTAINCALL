using System;
using System.Collections.Generic;

namespace CurtainCall.HumanCannon
{
    /// <summary>호스트의 잡기/탑승 대기. 재요청은 시간을 단축/연장하지 않고, 완료 전 매 프레임 조건을 검사한다.</summary>
    public sealed class CannonInteractionDelay
    {
        sealed class Entry
        {
            public int Target;
            public uint Revision;
            public double ReadyAt;
            public Func<bool> IsValid;
            public Action Complete;
        }

        readonly Dictionary<ulong, Entry> pending = new();
        readonly List<KeyValuePair<ulong, Entry>> snapshot = new();
        public int Count => pending.Count;

        public bool HasPending(ulong actor, int target, uint revision) =>
            pending.TryGetValue(actor, out var entry) && entry.Target == target && entry.Revision == revision && entry.IsValid();

        public void Schedule(ulong actor, int target, uint revision, double now, double duration,
            Func<bool> isValid, Action complete)
        {
            if (pending.TryGetValue(actor, out var old) && old.Target == target && old.Revision == revision) return;
            pending[actor] = new Entry { Target = target, Revision = revision, ReadyAt = now + Math.Max(0, duration),
                IsValid = isValid, Complete = complete };
        }

        public void Tick(double now)
        {
            snapshot.Clear();
            snapshot.AddRange(pending);
            foreach (var item in snapshot)
            {
                // 앞선 확정 콜백이 취소/재설정한 항목은 실행하지 않는다.
                if (!pending.TryGetValue(item.Key, out var current) || current != item.Value) continue;
                if (!current.IsValid()) { pending.Remove(item.Key); continue; }
                if (now < current.ReadyAt) continue;
                pending.Remove(item.Key);
                current.Complete();
            }
            snapshot.Clear();
        }

        public void Cancel(ulong actor) => pending.Remove(actor);
        public void Clear() => pending.Clear();
    }
}
