namespace CurtainCall.HumanCannon
{
    public enum DeparturePhase : byte { Waiting, Moving, Arrived }

    /// <summary>출발 발판의 단방향 이동. 호스트 시각과 revision으로 재시작 전 요청을 거른다.</summary>
    public sealed class DepartureTravelState
    {
        public DeparturePhase Phase { get; private set; } = DeparturePhase.Waiting;
        public uint Revision { get; private set; }
        public ulong Rider { get; private set; } = CannonSeatState.Nobody;
        public double StartedAt { get; private set; }
        public float Duration { get; private set; }

        public bool TryStart(ulong actor, uint expectedRevision, double now, float duration)
        {
            if (Phase != DeparturePhase.Waiting || actor == CannonSeatState.Nobody
                || expectedRevision != Revision || !ValidTravel(now, duration)) return false;
            Phase = DeparturePhase.Moving;
            Rider = actor;
            StartedAt = now;
            Duration = duration;
            Revision++;
            return true;
        }

        public bool Tick(double now)
        {
            if (Phase != DeparturePhase.Moving || !ValidTime(now)
                || now < StartedAt + Duration) return false;
            Phase = DeparturePhase.Arrived;
            Revision++;
            return true;
        }

        public void Reset()
        {
            Phase = DeparturePhase.Waiting;
            Rider = CannonSeatState.Nobody;
            StartedAt = 0;
            Duration = 0;
            // 출발하지 않은 발판도 이전 라운드의 요청과 snapshot을 무효화한다.
            Revision++;
        }

        public bool Receive(uint revision, DeparturePhase phase, ulong rider, double startedAt, float duration)
        {
            if (revision < Revision) return false;
            bool valid = phase switch
            {
                DeparturePhase.Waiting => rider == CannonSeatState.Nobody && startedAt == 0 && duration == 0,
                DeparturePhase.Moving or DeparturePhase.Arrived => rider != CannonSeatState.Nobody
                    && ValidTravel(startedAt, duration),
                _ => false
            };
            if (!valid) return false;
            Revision = revision;
            Phase = phase;
            Rider = rider;
            StartedAt = startedAt;
            Duration = duration;
            return true;
        }

        public float Progress(double now)
        {
            if (Phase == DeparturePhase.Arrived) return 1;
            if (Phase != DeparturePhase.Moving || !ValidTime(now) || now <= StartedAt) return 0;
            if (now >= StartedAt + Duration) return 1;
            return (float)((now - StartedAt) / Duration);
        }

        static bool ValidTravel(double now, float duration)
        {
            double arrival = now + duration;
            return ValidTime(now) && !float.IsNaN(duration) && !float.IsInfinity(duration)
                && duration > 0 && ValidTime(arrival) && arrival > now;
        }

        static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
