namespace CurtainCall.HumanCannon
{
    /// <summary>2인 이상 탑승하면 열리고, 지연 시간이 지난 뒤 모두 비워야 복귀하는 리프트 함정.</summary>
    public sealed class LiftTrapState
    {
        public bool IsOpen { get; private set; }
        public double OpenedAt { get; private set; }
        public float FrozenProgress { get; private set; }

        public bool Tick(int occupants, double now, float travelProgress, float resetDelay = 1.2f)
        {
            if (occupants < 0 || !ValidTime(now) || !ValidProgress(travelProgress)
                || !float.IsFinite(resetDelay) || resetDelay < 0) return false;
            if (!IsOpen)
            {
                if (occupants < 2) return false;
                IsOpen = true;
                OpenedAt = now;
                FrozenProgress = travelProgress;
                return true;
            }
            if (occupants != 0 || now < OpenedAt || now - OpenedAt < resetDelay) return false;
            Reset();
            return true;
        }

        public void Reset()
        {
            IsOpen = false;
            OpenedAt = 0;
            FrozenProgress = 0;
        }

        public bool Receive(bool open, double openedAt, float frozenProgress)
        {
            if (!ValidTime(openedAt) || !ValidProgress(frozenProgress)
                || !open && (openedAt != 0 || frozenProgress != 0)) return false;
            IsOpen = open;
            OpenedAt = openedAt;
            FrozenProgress = frozenProgress;
            return true;
        }

        static bool ValidTime(double value) => double.IsFinite(value) && value >= 0;
        static bool ValidProgress(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
    }
}
