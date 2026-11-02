namespace CurtainCall.HumanCannon
{
    public enum DeparturePhase : byte { Waiting, Moving, Arrived, Returning }

    /// <summary>출발 발판의 왕복 이동. 호스트 시각과 revision으로 재시작 전 요청을 거른다.</summary>
    public sealed class DepartureTravelState
    {
        public DeparturePhase Phase { get; private set; } = DeparturePhase.Waiting;
        public uint Revision { get; private set; }
        public ulong Rider { get; private set; } = CannonSeatState.Nobody;
        public double StartedAt { get; private set; }
        public float Duration { get; private set; }
        /// <summary>이번 라운드에 한 번이라도 도착했는지. 빈 발판이 제자리로 돌아와도 유지한다.</summary>
        public bool Delivered { get; private set; }

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

        /// <summary>도착한 빈 발판을 같은 이동 시간으로 출발점에 되돌린다.</summary>
        public bool TryReturn(double now)
        {
            if (Phase != DeparturePhase.Arrived || !ValidTravel(now, Duration)) return false;
            Phase = DeparturePhase.Returning;
            StartedAt = now;
            Revision++;
            return true;
        }

        public bool Tick(double now)
        {
            if (Phase != DeparturePhase.Moving && Phase != DeparturePhase.Returning || !ValidTime(now)
                || now < StartedAt + Duration) return false;
            if (Phase == DeparturePhase.Returning) { Home(); return true; }
            Phase = DeparturePhase.Arrived;
            Delivered = true;
            Revision++;
            return true;
        }

        public void Reset()
        {
            Home();
            Delivered = false;
        }

        /// <summary>출발점 대기로 되돌린다. 도착 기록은 유지한다.</summary>
        public void Home()
        {
            Phase = DeparturePhase.Waiting;
            Rider = CannonSeatState.Nobody;
            StartedAt = 0;
            Duration = 0;
            // 출발하지 않은 발판도 이전 라운드의 요청과 snapshot을 무효화한다.
            Revision++;
        }

        public bool Receive(uint revision, DeparturePhase phase, ulong rider, double startedAt, float duration, bool delivered = false)
        {
            if (revision < Revision) return false;
            bool valid = phase switch
            {
                DeparturePhase.Waiting => rider == CannonSeatState.Nobody && startedAt == 0 && duration == 0,
                DeparturePhase.Moving or DeparturePhase.Arrived or DeparturePhase.Returning => rider != CannonSeatState.Nobody
                    && ValidTravel(startedAt, duration),
                _ => false
            };
            if (!valid) return false;
            Revision = revision;
            Phase = phase;
            Rider = rider;
            StartedAt = startedAt;
            Duration = duration;
            Delivered = delivered || phase is DeparturePhase.Arrived or DeparturePhase.Returning;
            return true;
        }

        public float Progress(double now)
        {
            if (Phase == DeparturePhase.Arrived) return 1;
            if (Phase == DeparturePhase.Returning) return 1 - Forward(now);
            if (Phase != DeparturePhase.Moving) return 0;
            return Forward(now);
        }

        float Forward(double now)
        {
            if (!ValidTime(now) || now <= StartedAt) return 0;
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
