using System;
using System.Collections.Generic;

namespace CurtainCall.WaitingRoom
{
    public static class ExplorationPuzzleRules
    {
        public static bool IsFourDigits(string value)
        {
            if (value == null || value.Length != 4) return false;
            foreach (char c in value) if (c < '0' || c > '9') return false;
            return true;
        }
    }

    /// <summary>서로 다른 두 플레이어의 F 입력. 같은 플레이어 연타로는 완료되지 않는다.</summary>
    public sealed class ExplorationCooperation
    {
        readonly Dictionary<string, Dictionary<ulong, double>> presses = new();
        public bool Press(string target, ulong player, double now, double window, Func<ulong, bool> eligible)
        {
            if (string.IsNullOrEmpty(target) || window <= 0 || !eligible(player)) return false;
            if (!presses.TryGetValue(target, out var group)) presses[target] = group = new();
            var stale = new List<ulong>();
            foreach (var entry in group)
                if (now < entry.Value || now - entry.Value > window || !eligible(entry.Key)) stale.Add(entry.Key);
            foreach (var id in stale) group.Remove(id);
            group[player] = now;
            if (group.Count < 2) return false;
            presses.Remove(target);
            return true;
        }
        public void Clear() => presses.Clear();
    }
}
