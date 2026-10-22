using System;
using System.Collections.Generic;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Snapshot differences are observations, never inferred input or failure reasons.</summary>
    public sealed class CarryLogTracker
    {
        public readonly struct Item
        {
            public readonly int Uid;
            public readonly string Type, Location;
            public readonly ulong Owner;
            public Item(int uid, string type, string location, ulong owner = ulong.MaxValue) { Uid = uid; Type = type; Location = location; Owner = owner; }
        }
        public readonly struct Change
        {
            public readonly Item Before, After;
            public readonly string Event;
            public Change(Item before, Item after, string type) { Before = before; After = after; Event = type; }
        }
        Dictionary<int, Item> previous = new();
        bool initialized;
        public IReadOnlyList<Change> Observe(IEnumerable<Item> snapshot)
        {
            var current = new Dictionary<int, Item>();
            foreach (var item in snapshot)
            {
                if (item.Uid <= 0 || string.IsNullOrEmpty(item.Type) || item.Location is not ("HELD" or "FLOOR" or "SHELF") || !current.TryAdd(item.Uid, item))
                    throw new ArgumentException("Invalid carry snapshot.");
            }
            var changes = new List<Change>();
            if (initialized)
                foreach (var pair in current)
                {
                    if (!previous.TryGetValue(pair.Key, out var old) || old.Type != pair.Value.Type) continue;
                    var next = pair.Value;
                    string type = old.Location == "HELD" && next.Location == "FLOOR" ? "ITEM_DROPPED"
                        : old.Location == "HELD" && next.Location == "SHELF" ? "ITEM_STORED"
                        : old.Location is "FLOOR" or "SHELF" && next.Location == "HELD" ? "ITEM_PICKED_UP"
                        : old.Location == "HELD" && next.Location == "HELD" && old.Owner != next.Owner ? "ITEM_HANDED_OVER" : null;
                    if (type != null) changes.Add(new Change(old, next, type));
                }
            previous = current; initialized = true; return changes;
        }
        public void Reset() { previous.Clear(); initialized = false; }
    }
}
