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

    /// <summary>조건이 끊기면 초기화. 연속 조건의 경과만 세며 긴 프레임에 중복 발행하지 않는다.</summary>
    public sealed class AudienceDuration
    {
        double started = -1;
        bool issued;
        public bool Tick(bool active, double now, double seconds, bool repeat)
        {
            if (!active) { started = -1; issued = false; return false; }
            if (started < 0 || now < started) { started = now; issued = false; }
            if (issued || now - started < seconds) return false;
            if (repeat) started = now;
            else issued = true;
            return true;
        }
    }
}
