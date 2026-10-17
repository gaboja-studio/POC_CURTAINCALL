namespace CurtainCall.WaitingRoom
{
    /// <summary>밤 정비에 오는 전문가. 기준: night-maintenance.md 전문가 서비스 표.</summary>
    public enum ExpertKind : byte
    {
        None,
        /// <summary>의료 — 신체 복구(팔다리 1개당 비용). 1·3일차.</summary>
        Medical,
        /// <summary>시설 담당자 — 다음 날 장애물 강화/약화. 2일차. 5단계 범위 밖(표시만).</summary>
        Facility,
    }

    /// <summary>⑧ 전문가 대기실 진행 단계.</summary>
    public enum ExpertRoomPhase : byte
    {
        /// <summary>문 닫힘. 아무도 이용하지 않는다.</summary>
        Idle,
        /// <summary>구매 완료. 문이 열리고 구매자만 들어갈 수 있다.</summary>
        Booked,
        /// <summary>구매자가 들어갔다. 문 닫힘·쇠붙이 소리·신체 복구.</summary>
        Working,
        /// <summary>복구 끝. 문이 열리며 구매자가 다시 나온다.</summary>
        Exiting,
    }

    public enum ExpertRefusal : byte
    {
        None,
        NotNight,
        /// <summary>오늘 스케줄에 없는 전문가 — "담당자 부재중".</summary>
        Absent,
        /// <summary>오늘 오지만 이번 단계에서 만들지 않은 서비스(시설).</summary>
        NotAvailable,
        WholeBody,
        NotAlive,
        /// <summary>다른 사람이 이미 예약·이용 중.</summary>
        Busy,
        /// <summary>내가 이미 사서 예약·이용 중.</summary>
        AlreadyBooked,
        NotEnoughFunds,
        NotBuyer,
        WrongPhase,
    }

    /// <summary>
    /// 전문가 서비스 규칙(⑧ 전문가 대기실 한 칸). 판정은 호스트 한 곳에서 이것으로만 한다.
    /// 기준: night-maintenance.md(밤 전용, 1·3일차 의료 / 2일차 시설, 스케줄에 없으면 비활성 + "담당자 부재중", 연출 A안),
    /// injury-rules.md #6(살아 있는 사람만, 온전한 몸 불가, 1개당 공동 자금, 역순 복구는 공용 NetworkPlayer가 한다).
    /// 방은 한 번에 한 명만 쓴다(구매 → 입장 → 복구 → 퇴실). 시간 흐름은 부르는 쪽이 <see cref="Tick"/>으로 준다.
    /// </summary>
    public sealed class ExpertServiceBook
    {
        readonly float workSeconds;
        readonly float exitSeconds;
        float phaseTime;

        public ExpertServiceBook(float workSeconds, float exitSeconds)
        {
            this.workSeconds = workSeconds;
            this.exitSeconds = exitSeconds;
        }

        public ExpertRoomPhase Phase { get; private set; }

        /// <summary>예약·이용 중인 사람(접속 번호). <see cref="HasBuyer"/>가 false면 의미 없다.</summary>
        public ulong Buyer { get; private set; }

        public bool HasBuyer => Phase != ExpertRoomPhase.Idle;

        /// <summary>상태가 바뀔 때마다 1씩 오른다(참가자 동기화용).</summary>
        public long Revision { get; private set; }

        /// <summary>그날 밤에 오는 전문가. 1·3일차 의료, 2일차 시설, 그 밖은 없음.</summary>
        public static ExpertKind StaffFor(int day) => day switch
        {
            1 or 3 => ExpertKind.Medical,
            2 => ExpertKind.Facility,
            _ => ExpertKind.None,
        };

        /// <summary>그 전문가의 서비스를 지금 살 수 있는 날인지(방 사용 여부·자금·몸 상태는 보지 않는다).</summary>
        public static ExpertRefusal Availability(ExpertKind kind, int day, bool night)
        {
            if (!night) return ExpertRefusal.NotNight;
            if (StaffFor(day) != kind) return ExpertRefusal.Absent;
            return kind == ExpertKind.Medical ? ExpertRefusal.None : ExpertRefusal.NotAvailable;
        }

        /// <summary>의료 신체 복구 1개를 살 수 있는지. 자금 확인은 <see cref="Book"/>의 spend가 한다.</summary>
        public ExpertRefusal CanBuyMedical(int day, bool night, bool alive, int lostLimbs)
        {
            var refusal = Availability(ExpertKind.Medical, day, night);
            if (refusal != ExpertRefusal.None) return refusal;
            if (!alive) return ExpertRefusal.NotAlive;
            if (lostLimbs <= 0) return ExpertRefusal.WholeBody;
            if (HasBuyer) return ExpertRefusal.Busy;
            return ExpertRefusal.None;
        }

        /// <summary>
        /// 의료 신체 복구 1개 구매. 조건을 모두 통과하면 <paramref name="spend"/>(공동 자금 차감)를 부르고, 성공하면 방을 예약한다.
        /// </summary>
        public ExpertRefusal Book(ulong buyer, int day, bool night, bool alive, int lostLimbs, System.Func<bool> spend)
        {
            if (HasBuyer && Buyer == buyer) return ExpertRefusal.AlreadyBooked;
            var refusal = CanBuyMedical(day, night, alive, lostLimbs);
            if (refusal != ExpertRefusal.None) return refusal;
            if (spend == null || !spend()) return ExpertRefusal.NotEnoughFunds;
            Buyer = buyer;
            SetPhase(ExpertRoomPhase.Booked);
            return ExpertRefusal.None;
        }

        /// <summary>입구에서 F: 구매자만 들어간다.</summary>
        public ExpertRefusal Enter(ulong who)
        {
            if (Phase != ExpertRoomPhase.Booked) return ExpertRefusal.WrongPhase;
            if (who != Buyer) return ExpertRefusal.NotBuyer;
            SetPhase(ExpertRoomPhase.Working);
            return ExpertRefusal.None;
        }

        /// <summary>
        /// 시간을 흘린다. 복구 시간이 끝나면 true를 한 번 돌려준다(그때 부르는 쪽이 신체 1개를 복구한다).
        /// 퇴실 시간이 끝나면 방을 비운다.
        /// </summary>
        public bool Tick(float deltaSeconds)
        {
            if (Phase != ExpertRoomPhase.Working && Phase != ExpertRoomPhase.Exiting) return false;
            phaseTime += deltaSeconds;
            if (Phase == ExpertRoomPhase.Working && phaseTime >= workSeconds)
            {
                SetPhase(ExpertRoomPhase.Exiting);
                return true;
            }
            if (Phase == ExpertRoomPhase.Exiting && phaseTime >= exitSeconds) Clear();
            return false;
        }

        /// <summary>구매자가 나갔거나(접속 끊김) 밤이 끝나면 방을 비운다. 이미 낸 비용은 돌려주지 않는다(임시 규칙).</summary>
        public void Clear()
        {
            if (Phase == ExpertRoomPhase.Idle) return;
            Buyer = 0;
            SetPhase(ExpertRoomPhase.Idle);
        }

        /// <summary>참가자: 호스트가 보낸 상태를 그대로 받는다.</summary>
        public void Apply(ExpertRoomPhase phase, ulong buyer, long revision)
        {
            Phase = phase;
            Buyer = phase == ExpertRoomPhase.Idle ? 0 : buyer;
            Revision = revision;
            phaseTime = 0f;
        }

        void SetPhase(ExpertRoomPhase phase)
        {
            Phase = phase;
            phaseTime = 0f;
            Revision++;
        }

        public static string Explain(ExpertRefusal refusal) => refusal switch
        {
            ExpertRefusal.NotNight => "밤 정비 때만 이용할 수 있습니다.",
            ExpertRefusal.Absent => "담당자 부재중",
            ExpertRefusal.NotAvailable => "준비 중인 서비스입니다(5단계 범위 밖).",
            ExpertRefusal.WholeBody => "잃은 팔다리가 없습니다.",
            ExpertRefusal.NotAlive => "살아 있는 사람만 이용할 수 있습니다.",
            ExpertRefusal.Busy => "다른 사람이 전문가 대기실을 이용 중입니다.",
            ExpertRefusal.AlreadyBooked => "이미 구매했습니다 — ⑧ 전문가 대기실 입구에서 F를 누르세요.",
            ExpertRefusal.NotEnoughFunds => "공동 자금이 모자라거나 지금은 쓸 수 없습니다.",
            ExpertRefusal.NotBuyer => "서비스를 구매한 사람만 들어갈 수 있습니다.",
            ExpertRefusal.WrongPhase => "지금은 들어갈 수 없습니다.",
            _ => "",
        };
    }
}
