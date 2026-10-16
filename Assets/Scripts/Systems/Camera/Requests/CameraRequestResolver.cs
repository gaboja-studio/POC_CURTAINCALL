using System;
using System.Collections.Generic;

namespace CurtainCall.Cameras.Requests
{
    /// <summary>높은 우선순위, 동률이면 최근 등록. 같은 출처의 갱신은 등록 순서를 보존한다.</summary>
    public sealed class CameraRequestResolver<TSource, TValue>
    {
        struct Entry
        {
            public TValue Value;
            public int Priority;
            public long Sequence;
        }

        readonly Dictionary<TSource, Entry> entries = new();
        readonly List<TSource> removed = new();
        long sequence;

        public int Count => entries.Count;

        public void Set(TSource source, TValue value, int priority)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            long registered = entries.TryGetValue(source, out var previous) ? previous.Sequence : ++sequence;
            entries[source] = new Entry { Value = value, Priority = priority, Sequence = registered };
        }

        public bool Remove(TSource source) => source != null && entries.Remove(source);

        public void RemoveWhere(Func<TSource, TValue, bool> predicate)
        {
            removed.Clear();
            foreach (var pair in entries)
                if (predicate(pair.Key, pair.Value.Value)) removed.Add(pair.Key);
            foreach (var source in removed) entries.Remove(source);
            removed.Clear();
        }

        public bool TryResolve(out TSource source, out TValue value, Func<TValue, bool> include = null)
        {
            source = default;
            value = default;
            Entry best = default;
            bool found = false;
            foreach (var pair in entries)
            {
                var entry = pair.Value;
                if (include != null && !include(entry.Value)) continue;
                if (found && (entry.Priority < best.Priority ||
                              (entry.Priority == best.Priority && entry.Sequence < best.Sequence))) continue;
                found = true;
                best = entry;
                source = pair.Key;
                value = entry.Value;
            }
            return found;
        }

        public void Clear()
        {
            entries.Clear();
            removed.Clear();
            sequence = 0;
        }
    }
}
