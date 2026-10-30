namespace CurtainCall.HumanCannon
{
    public enum PoleSawPhase : byte { Active, Retracted, Warning }

    /// <summary>봉의 고정 자리 점유와 톱 예고 시각. 빈자리를 다시 써도 남은 단원의 자리는 이동하지 않는다.</summary>
    public sealed class PoleGateState
    {
        public const ulong Nobody = CannonSeatState.Nobody;
        public const int MaxRiders = 4;
        readonly ulong[] occupants = { Nobody, Nobody, Nobody, Nobody };
        readonly double[] deadlines = new double[MaxRiders];
        public int Capacity { get; }
        public int Count { get; private set; }
        public ulong Occupant => FirstSeat >= 0 ? occupants[FirstSeat] : Nobody;
        public uint Revision { get; private set; }
        public double HoldUntil => FirstSeat >= 0 ? deadlines[FirstSeat] : 0;
        public double RearmAt { get; private set; }
        public PoleSawPhase Phase { get; private set; } = PoleSawPhase.Active;
        public bool IsOccupied => Occupant != Nobody;
        public bool IsUnlimitedHold => IsOccupied && HoldUntil == 0;
        public bool HasFreeSeat => Count < Capacity;
        int FirstSeat { get { for (int i = 0; i < Capacity; i++) if (occupants[i] != Nobody) return i; return -1; } }
        public PoleGateState() : this(1) { }
        public PoleGateState(int capacity) => Capacity = System.Math.Clamp(capacity, 1, MaxRiders);
        public ulong OccupantAt(int seat) => seat >= 0 && seat < Capacity ? occupants[seat] : Nobody;
        public double DeadlineAt(int seat) => seat >= 0 && seat < Capacity ? deadlines[seat] : 0;
        public int SeatOf(ulong actor)
        {
            if (actor != Nobody) for (int i = 0; i < Capacity; i++) if (occupants[i] == actor) return i;
            return -1;
        }

        public bool TryGrab(ulong actor, uint expectedRevision, double now, float holdSeconds, bool fixedGateWindow = false, int seat = -1)
        {
            if (actor == Nobody || !HasFreeSeat || SeatOf(actor) >= 0 || expectedRevision != Revision || !ValidTime(now)
                || !float.IsFinite(holdSeconds) || holdSeconds < 0 || (fixedGateWindow && (holdSeconds <= 0 || Capacity != 1))) return false;
            if (seat == -1) for (int i = 0; i < Capacity; i++) if (occupants[i] == Nobody) { seat = i; break; }
            if (seat < 0 || seat >= Capacity || occupants[seat] != Nobody) return false;
            // 0은 무제한 점유다. 유한 제한을 쓰는 기존 호출은 같은 만료 시각 규칙을 유지한다.
            double deadline = 0;
            if (holdSeconds > 0 && !TryDeadline(now, holdSeconds, out deadline)) return false;
            occupants[seat] = actor;
            deadlines[seat] = deadline;
            Count++;
            // 연결 관문은 기존 유지 시간과 재가동 시각으로 잡은 순간의 정지 구간을 고정한다.
            RearmAt = fixedGateWindow ? deadline : 0;
            Phase = PoleSawPhase.Retracted;
            Revision++;
            return true;
        }

        public bool TryRelease(ulong actor, uint expectedRevision, double now, float warningSeconds)
        {
            int seat = SeatOf(actor);
            if (seat < 0 || expectedRevision != Revision
                || !TryDeadline(now, warningSeconds, out double deadline)) return false;
            RemoveSeat(seat);
            if (Count > 0 || RearmAt > 0)
            {
                Revision++;
            }
            else BeginWarning(deadline);
            return true;
        }

        public bool Tick(double now, float warningSeconds)
        {
            if (!ValidTime(now) || !ValidDuration(warningSeconds)) return false;
            bool changed = false;
            // 다인 봉은 각자 잡은 시점부터 제한 시간을 센다. 한 호출당 한 명만 만료시켜
            // 호스트가 각자의 비행 식별자·좌석에서 별도의 놓기 승인을 보낼 수 있게 한다.
            if (Capacity > 1 && Phase == PoleSawPhase.Retracted)
            {
                for (int i = 0; i < Capacity; i++)
                {
                    if (occupants[i] == Nobody || deadlines[i] <= 0 || now < deadlines[i]) continue;
                    if (!TryDeadline(deadlines[i], warningSeconds, out double deadline)) return false;
                    RemoveSeat(i);
                    if (Count > 0) Revision++;
                    else BeginWarning(deadline);
                    return true;
                }
            }
            if (RearmAt > 0 && Phase == PoleSawPhase.Retracted)
            {
                if (now < RearmAt - warningSeconds) return false;
                // 예고는 정지 구간의 마지막에 포함한다. 아직 잡고 있으면 만료 때까지 유지한다.
                Phase = PoleSawPhase.Warning;
                Revision++;
                changed = true;
            }
            if (Phase == PoleSawPhase.Retracted && RearmAt == 0 && HoldUntil > 0 && now >= HoldUntil)
            {
                // 늦은 프레임이 유지 시간이나 예고 시간을 늘리지 않도록 실제 만료 시각을 기준으로 한다.
                if (!TryDeadline(HoldUntil, warningSeconds, out double deadline)) return false;
                BeginWarning(deadline);
                changed = true;
            }
            if (Phase == PoleSawPhase.Warning && now >= RearmAt)
            {
                ClearSeats();
                Phase = PoleSawPhase.Active;
                RearmAt = 0;
                Revision++;
                changed = true;
            }
            return changed;
        }

        public void Reset()
        {
            ClearSeats();
            Phase = PoleSawPhase.Active;
            RearmAt = 0;
            // 빈 봉도 재시작 전에 보낸 잡기 요청을 무효화한다.
            Revision++;
        }

        public bool Receive(uint revision, ulong occupant, PoleSawPhase phase, double holdUntil, double rearmAt)
        {
            if (revision < Revision || !ValidTime(holdUntil) || !ValidTime(rearmAt)) return false;
            bool valid = phase switch
            {
                PoleSawPhase.Active => occupant == Nobody && holdUntil == 0 && rearmAt == 0,
                PoleSawPhase.Retracted => (occupant != Nobody && rearmAt == 0)
                    || (rearmAt > 0 && ((occupant == Nobody && holdUntil == 0) || (occupant != Nobody && holdUntil == rearmAt))),
                PoleSawPhase.Warning => rearmAt > 0 && ((occupant == Nobody && holdUntil == 0)
                    || (occupant != Nobody && holdUntil == rearmAt)),
                _ => false
            };
            if (!valid) return false;
            Revision = revision;
            ClearSeats();
            if (occupant != Nobody) { occupants[0] = occupant; deadlines[0] = holdUntil; Count = 1; }
            Phase = phase;
            RearmAt = rearmAt;
            return true;
        }

        public bool ReceiveCrew(uint revision, ulong[] actors, double[] holds, PoleSawPhase phase, double rearmAt)
        {
            if (actors == null || holds == null || actors.Length != MaxRiders || holds.Length != MaxRiders
                || revision < Revision || !ValidTime(rearmAt)) return false;
            int count = 0;
            for (int i = 0; i < MaxRiders; i++)
            {
                if (!ValidTime(holds[i]) || (actors[i] == Nobody && holds[i] != 0)) return false;
                if (actors[i] == Nobody) continue;
                if (i >= Capacity) return false;
                for (int j = 0; j < i; j++) if (actors[j] == actors[i]) return false;
                count++;
            }
            if (Capacity == 1) return Receive(revision, actors[0], phase, holds[0], rearmAt);
            if (!(phase == PoleSawPhase.Active && count == 0 && rearmAt == 0
                || phase == PoleSawPhase.Retracted && count > 0 && rearmAt == 0
                || phase == PoleSawPhase.Warning && count == 0 && rearmAt > 0)) return false;
            for (int i = 0; i < MaxRiders; i++) { occupants[i] = actors[i]; deadlines[i] = holds[i]; }
            Count = count; Revision = revision; Phase = phase; RearmAt = rearmAt;
            return true;
        }

        void BeginWarning(double deadline)
        {
            ClearSeats();
            RearmAt = deadline;
            Phase = PoleSawPhase.Warning;
            Revision++;
        }

        void RemoveSeat(int seat) { occupants[seat] = Nobody; deadlines[seat] = 0; Count--; }
        void ClearSeats()
        {
            for (int i = 0; i < MaxRiders; i++) { occupants[i] = Nobody; deadlines[i] = 0; }
            Count = 0;
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
