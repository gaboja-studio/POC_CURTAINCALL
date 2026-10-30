namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 탐색 타이머 구역 규칙(2026-10-30 확정). 재생 구역에 한 명이라도 있으면 흐르고, 전원이 멈춤 구역(또는 어느 구역도 아님)에 있으면 멈춘다.
    /// 재생·멈춤 구역이 겹치는 자리는 멈춤이 우선한다(계단실 구역이 로비를 덮는 경우).
    /// </summary>
    public static class ExplorationTimerZoneRule
    {
        public static bool IsExploring(bool inRunningZone, bool inStoppingZone) => inRunningZone && !inStoppingZone;

        /// <summary>한 명이라도 탐색 중이면 false(타이머 재생), 아니면 true(멈춤). 플레이어가 없으면 멈춤.</summary>
        public static bool Paused(System.Collections.Generic.IEnumerable<(bool running, bool stopping)> players)
        {
            foreach (var (running, stopping) in players)
                if (IsExploring(running, stopping)) return false;
            return true;
        }
    }
}
