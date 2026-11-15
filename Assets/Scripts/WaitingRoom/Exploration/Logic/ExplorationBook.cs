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
        /// 맨몸·도구 겸용 대상을 한 번 친다(<see cref="StrikeProgress"/> 단위). 진행이 <paramref name="total"/>에 닿으면 열린다.
        /// 기록 값은 타격 수가 아니라 진행 단위다.
        /// </summary>
        public ExplorationRefusal Strike(string target, int add, int total, out int units, out bool openedNow)
        {
            units = Hits(target);
            openedNow = false;
            if (string.IsNullOrEmpty(target) || add < 1 || total < 1) return ExplorationRefusal.UnknownTarget;
            if (opened.Contains(target)) return ExplorationRefusal.AlreadyOpen;
            units = System.Math.Min(total, units + add);
            hits[target] = units;
            if (units >= total) { opened.Add(target); openedNow = true; }
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

        /// <summary>열린 대상을 다시 닫는다(동력 소켓에서 배터리를 뺀 경우). 시도 수는 남긴다.</summary>
        public bool Close(string target)
        {
            if (target == null || !opened.Remove(target)) return false;
            Revision++;
            return true;
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

    /// <summary>
    /// 맨몸·도구 겸용 타격 진행(통로 기믹 CC-255, 2026-11-10 사용자). 맨몸 <c>bare</c>대 또는 도구 <c>tool</c>대에 열리고,
    /// 섞어 치면 비율로 합산한다(예: 벽 맨몸 15·도구 6 → 맨몸 1대 = 1/15, 도구 1대 = 1/6). 진행은 두 횟수의 최소공배수 단위로 센다.
    /// </summary>
    public static class StrikeProgress
    {
        public static int Total(int bare, int tool) => Lcm(System.Math.Max(1, bare), System.Math.Max(1, tool));

        /// <summary>한 번 칠 때 오르는 진행 단위.</summary>
        public static int Step(int bare, int tool, bool withTool) => Total(bare, tool) / System.Math.Max(1, withTool ? tool : bare);

        /// <summary>지금 방식(맨몸/도구)으로 남은 타격 수.</summary>
        public static int Remaining(int units, int bare, int tool, bool withTool)
        {
            int left = System.Math.Max(0, Total(bare, tool) - units), step = Step(bare, tool, withTool);
            return (left + step - 1) / step;
        }

        /// <summary>지금 방식 기준으로 친 것으로 보이는 수(전체 - 남은 수).</summary>
        public static int Done(int units, int bare, int tool, bool withTool) =>
            System.Math.Max(1, withTool ? tool : bare) - Remaining(units, bare, tool, withTool);

        static int Lcm(int a, int b) => a / Gcd(a, b) * b;
        static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }
    }
}
