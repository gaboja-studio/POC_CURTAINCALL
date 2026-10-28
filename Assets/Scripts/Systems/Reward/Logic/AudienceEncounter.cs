namespace CurtainCall.Reward
{
    /// <summary>같은 장애물 통과는 공연당 한 번. 접촉/절단한 통과에는 회피 보상을 주지 않는다.</summary>
    public sealed class AudienceEncounter
    {
        bool approached, resolved, close, damaged, stacked, changedLane;
        int initialParts;
        public bool Sample(float signedDistance, float approach, float clearance, int parts,
            bool touching, bool near, bool inStack, bool leftLane)
        {
            if (resolved) return false;
            if (!approached)
            {
                if (signedDistance >= 0 || signedDistance < -approach) return false;
                approached = true;
                initialParts = parts;
            }
            damaged |= touching || parts != initialParts;
            close |= near;
            if (signedDistance <= clearance) stacked |= inStack;
            changedLane |= leftLane;
            if (signedDistance < clearance) return false;
            resolved = true;
            return !damaged;
        }
        public bool WasClose => close;
        public bool WasStacked => stacked;
        public bool ChangedLane => changedLane;
    }

    /// <summary>
    /// 시간 끌기 판정. 진행 가능한 상태에서 <c>step</c>만큼 앞으로 가지 못한 채 <c>seconds</c>가 지나면 한 번 알리고 다시 센다.
    /// 제외 상태(받기 대기·강제 이동·비행 등)에서는 타이머를 지운다.
    /// </summary>
    public sealed class AudienceStall
    {
        double since = double.NaN;
        float best;
        public bool Sample(bool active, float progress, double now, double seconds, float step)
        {
            if (!active || double.IsNaN(since) || now < since) { since = active ? now : double.NaN; best = progress; return false; }
            if (progress >= best + step) { best = progress; since = now; return false; }
            if (now - since < seconds) return false;
            since = now;
            return true;
        }
    }
}
