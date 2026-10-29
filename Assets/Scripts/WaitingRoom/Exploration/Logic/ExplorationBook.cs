using System.Collections.Generic;

namespace CurtainCall.WaitingRoom
{
    public enum ExplorationRefusal : byte
    {
        None,
        UnknownTarget,
        AlreadyOpen,
        WrongTool,
        TooFar,
        TooFast,
    }

    /// <summary>
    /// 런 동안 남는 탐색 진행(판정 화면 전용 순수 로직, Task-079 3단계).
    /// 부술 수 있는 벽의 타격 수와 열린 대상을 기록한다. 손을 떼도 진행은 그대로 남고 팀 모두에게 같다(2026-10-29 사용자).
    /// 세대 교체 때 비운다. 같은 런 안 장소 전환·재입장에는 남는다.
    /// </summary>
    public sealed class ExplorationBook
    {
        readonly Dictionary<string, int> hits = new();
        readonly HashSet<string> opened = new();

        public long Revision { get; private set; }

        public int Hits(string target) => target != null && hits.TryGetValue(target, out int n) ? n : 0;
        public bool IsOpen(string target) => target != null && opened.Contains(target);
        public IEnumerable<string> Targets => hits.Keys;
        public IEnumerable<string> Opened => opened;

        /// <summary>
        /// 한 번 내려친다. 필요한 타격 수에 닿으면 열린다(<paramref name="openedNow"/>). 이미 열린 대상은 거절한다.
        /// 도구 사용 횟수는 열린 때에만 부르는 쪽이 1 줄인다.
        /// </summary>
        public ExplorationRefusal Hit(string target, int required, out int count, out bool openedNow)
        {
            if (required < 1) { count = Hits(target); openedNow = false; return ExplorationRefusal.UnknownTarget; }
            return Attempt(target, Hits(target) + 1 >= required, out count, out openedNow);
        }

        /// <summary>
        /// 한 번 시도한다(머리핀 자물쇠·말뚝 뽑개·UV 비추기 공통). 시도 수가 하나 오르고, <paramref name="success"/>면 열린다(발견된다).
        /// 시도 수는 팀 공유·런 유지라 같은 좌물쇠의 "세 번째 시도 보장"에 쓴다. 이미 열린 대상은 거절한다.
        /// </summary>
        public ExplorationRefusal Attempt(string target, bool success, out int count, out bool openedNow)
        {
            count = Hits(target);
            openedNow = false;
            if (string.IsNullOrEmpty(target)) return ExplorationRefusal.UnknownTarget;
            if (opened.Contains(target)) return ExplorationRefusal.AlreadyOpen;
            hits[target] = ++count;
            if (success) { opened.Add(target); openedNow = true; }
            Revision++;
            return ExplorationRefusal.None;
        }

        /// <summary>
        /// 머리핀 규칙(초안): 보장 번째(<paramref name="guaranteeAt"/>) 시도는 반드시 열리고, 그 전에는 <paramref name="chance"/> 확률.
        /// <paramref name="roll"/>은 0~1 난수(판정 화면이 굴린다).
        /// </summary>
        public static bool LockOpens(int attemptNumber, float chance, int guaranteeAt, double roll) =>
            (guaranteeAt > 0 && attemptNumber >= guaranteeAt) || roll < chance;

        /// <summary>받은 상태로 바꾼다(참가자 화면).</summary>
        public void Load(IReadOnlyList<string> targets, IReadOnlyList<int> counts, IReadOnlyList<string> open)
        {
            hits.Clear();
            opened.Clear();
            if (targets != null && counts != null)
                for (int i = 0; i < targets.Count && i < counts.Count; i++)
                    if (!string.IsNullOrEmpty(targets[i])) hits[targets[i]] = counts[i];
            if (open != null) foreach (var t in open) if (!string.IsNullOrEmpty(t)) opened.Add(t);
            Revision++;
        }

        /// <summary>세대 교체·방 바뀜: 모두 비운다.</summary>
        public void Clear()
        {
            if (hits.Count == 0 && opened.Count == 0) return;
            hits.Clear();
            opened.Clear();
            Revision++;
        }
    }
}
