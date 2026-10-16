namespace CurtainCall.HumanCannon
{
    /// <summary>같은 한 명의 유효한 탑승이 유지된 시간. 이탈·과승·자격 상실은 출발 대기를 취소한다.</summary>
    public sealed class LiftBoardingState
    {
        public bool IsCounting => Rider != CannonSeatState.Nobody;
        public ulong Rider { get; private set; } = CannonSeatState.Nobody;
        public double BoardedAt { get; private set; }

        public bool Tick(ulong soleRider, bool eligible, double now, float delay = 1.5f)
        {
            if (!ValidTime(now) || !ValidDelay(delay)) return false;
            if (!eligible || soleRider == CannonSeatState.Nobody)
            {
                if (!IsCounting) return false;
                Reset();
                return true;
            }
            if (Rider == soleRider) return false;
            Rider = soleRider;
            BoardedAt = now;
            return true;
        }

        public bool Ready(double now, float delay = 1.5f) => IsCounting && ValidTime(now) && ValidDelay(delay)
            && now >= BoardedAt && now - BoardedAt >= delay;

        public void Reset()
        {
            Rider = CannonSeatState.Nobody;
            BoardedAt = 0;
        }

        public bool Receive(ulong rider, double boardedAt)
        {
            if (!ValidTime(boardedAt) || rider == CannonSeatState.Nobody && boardedAt != 0) return false;
            Rider = rider;
            BoardedAt = boardedAt;
            return true;
        }

        static bool ValidTime(double value) => double.IsFinite(value) && value >= 0;
        static bool ValidDelay(float value) => float.IsFinite(value) && value >= 0;
    }
}
