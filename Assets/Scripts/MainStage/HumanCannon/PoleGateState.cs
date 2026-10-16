namespace CurtainCall.HumanCannon
{
    public enum PoleSawPhase : byte { Active, Retracted, Warning }

    /// <summary>봉 한 자리의 점유와 톱 예고 시각. 호스트 시각을 쓰며 과거 요청은 revision으로 거른다.</summary>
    public sealed class PoleGateState
    {
        public const ulong Nobody = CannonSeatState.Nobody;
        public ulong Occupant { get; private set; } = Nobody;
        public uint Revision { get; private set; }
        public double HoldUntil { get; private set; }
        public double RearmAt { get; private set; }
        public PoleSawPhase Phase { get; private set; } = PoleSawPhase.Active;
        public bool IsOccupied => Occupant != Nobody;

        public bool TryGrab(ulong actor, uint expectedRevision, double now, float holdSeconds)
        {
            if (actor == Nobody || IsOccupied || expectedRevision != Revision
                || !TryDeadline(now, holdSeconds, out double deadline)) return false;
            Occupant = actor;
            HoldUntil = deadline;
            RearmAt = 0;
            Phase = PoleSawPhase.Retracted;
            Revision++;
            return true;
        }

        public bool TryRelease(ulong actor, uint expectedRevision, double now, float warningSeconds)
        {
            if (actor == Nobody || actor != Occupant || expectedRevision != Revision
                || !TryDeadline(now, warningSeconds, out double deadline)) return false;
            BeginWarning(deadline);
            return true;
        }

        public bool Tick(double now, float warningSeconds)
        {
            if (!ValidTime(now) || !ValidDuration(warningSeconds)) return false;
            bool changed = false;
            if (Phase == PoleSawPhase.Retracted && now >= HoldUntil)
            {
                // 늦은 프레임이 유지 시간이나 예고 시간을 늘리지 않도록 실제 만료 시각을 기준으로 한다.
                if (!TryDeadline(HoldUntil, warningSeconds, out double deadline)) return false;
                BeginWarning(deadline);
                changed = true;
            }
            if (Phase == PoleSawPhase.Warning && now >= RearmAt)
            {
                Phase = PoleSawPhase.Active;
                RearmAt = 0;
                Revision++;
                changed = true;
            }
            return changed;
        }

        public void Reset()
        {
            Occupant = Nobody;
            Phase = PoleSawPhase.Active;
            HoldUntil = RearmAt = 0;
            // 빈 봉도 재시작 전에 보낸 잡기 요청을 무효화한다.
            Revision++;
        }

        public bool Receive(uint revision, ulong occupant, PoleSawPhase phase, double holdUntil, double rearmAt)
        {
            if (revision < Revision || !ValidTime(holdUntil) || !ValidTime(rearmAt)) return false;
            bool valid = phase switch
            {
                PoleSawPhase.Active => occupant == Nobody && holdUntil == 0 && rearmAt == 0,
                PoleSawPhase.Retracted => occupant != Nobody && holdUntil > 0 && rearmAt == 0,
                PoleSawPhase.Warning => occupant == Nobody && holdUntil == 0 && rearmAt > 0,
                _ => false
            };
            if (!valid) return false;
            Revision = revision;
            Occupant = occupant;
            Phase = phase;
            HoldUntil = holdUntil;
            RearmAt = rearmAt;
            return true;
        }

        void BeginWarning(double deadline)
        {
            Occupant = Nobody;
            HoldUntil = 0;
            RearmAt = deadline;
            Phase = PoleSawPhase.Warning;
            Revision++;
        }

        static bool TryDeadline(double now, float duration, out double deadline)
        {
            deadline = now + duration;
            return ValidTime(now) && ValidDuration(duration) && ValidTime(deadline) && deadline > now;
        }

        static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        static bool ValidDuration(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
    }
}
