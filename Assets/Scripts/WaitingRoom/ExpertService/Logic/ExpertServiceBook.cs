using System;

namespace CurtainCall.WaitingRoom
{
    /// <summary>⑧ 전문가 대기실 진행 단계.</summary>
    public enum ExpertRoomPhase : byte
    {
        /// <summary>커튼 닫힘. 아무도 이용하지 않는다.</summary>
        Idle,
        /// <summary>구매 완료. 구매자가 커튼 앞에 오면 커튼이 열리고, 구매자만 들어갈 수 있다.</summary>
        Booked,
        /// <summary>구매자가 들어갔다. 커튼 닫힘·쇠붙이 소리·신체 복구.</summary>
        Working,
        /// <summary>복구 끝. 커튼이 열리며 구매자가 다시 나온다.</summary>
        Exiting,
    }

    public enum ExpertRefusal : byte
    {
        None,
        /// <summary>공연 중이다. 신체 복구는 쉬는 시간·밤 정비에만 쓴다.</summary>
        InPerformance,
        WholeBody,
        NotAlive,
        /// <summary>다른 사람이 이미 예약·이용 중.</summary>
        Busy,
        /// <summary>내가 이미 사서 예약·이용 중.</summary>
        AlreadyBooked,
        NotEnoughFunds,
        NotBuyer,
        WrongPhase,
        /// <summary>복구할 팔다리 수가 1~최대 또는 잃은 수를 벗어났다.</summary>
        BadChoice,
    }

    /// <summary>
    /// 신체 복구 서비스 규칙. 판정은 호스트 한 곳에서 이것으로만 한다.
    /// 기준(M2 게임 시스템 변경점 4-1·4-2, 보상 확정 규칙): 서비스는 신체 복구 하나(시설 서비스·전문가 일정 삭제),
    /// 쉬는 시간·밤 정비 언제든(공연 중 제외), 한 번에 1~3개를 절단 역순으로, 온전한 몸·사망자는 이용 불가.
    /// 가격은 1개당 기본가에서 시작해 팀 전체 이용 1회마다 다음 단가가 한 번 오른다(여러 개를 고쳐도 1회). 런이 새로 시작되면 처음 가격.
    /// ⑧ 방은 한 번에 한 명만 쓴다(구매 → 입장 → 복구 → 퇴실). 시간 흐름은 부르는 쪽이 <see cref="Tick"/>으로 준다.
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

        /// <summary>이번 이용에서 복구할 팔다리 수(구매 때 고른 수).</summary>
        public int BookedLimbs { get; private set; }

        public bool HasBuyer => Phase != ExpertRoomPhase.Idle;

        /// <summary>이번 런에서 팀 전체가 신체 복구를 이용한 횟수. 다음 단가를 정한다.</summary>
        public int TeamUses { get; private set; }

        /// <summary>상태가 바뀔 때마다 1씩 오른다(참가자 동기화용).</summary>
        public long Revision { get; private set; }

        /// <summary>공연 중이 아니면(쉬는 시간·밤 정비) 이용할 수 있다.</summary>
        public static ExpertRefusal Availability(bool serviceTime) => serviceTime ? ExpertRefusal.None : ExpertRefusal.InPerformance;

        /// <summary>팀 이용 <paramref name="teamUses"/>회 뒤의 1개당 가격.</summary>
        public static long UnitPrice(int teamUses, long basePrice, long priceStep) => basePrice + priceStep * Math.Max(0, teamUses);

        /// <summary>복구 비용 = 지금 1개당 가격 × 고른 수.</summary>
        public static long Cost(int parts, int teamUses, long basePrice, long priceStep) =>
            parts <= 0 ? 0 : parts * UnitPrice(teamUses, basePrice, priceStep);

        /// <summary>고를 수 있는 최대 개수 = 잃은 수와 한 번 최대치 중 작은 값.</summary>
        public static int MaxParts(int lostLimbs, int maxPerUse) => Math.Max(0, Math.Min(lostLimbs, maxPerUse));

        /// <summary>신체 복구를 살 수 있는지. 자금 확인은 <see cref="BookMedical"/>의 spend가 한다.</summary>
        public ExpertRefusal CanBuyMedical(ulong buyer, bool serviceTime, bool alive, int lostLimbs, int parts, int maxPerUse)
        {
            var refusal = Availability(serviceTime);
            if (refusal != ExpertRefusal.None) return refusal;
            if (HasBuyer) return Buyer == buyer ? ExpertRefusal.AlreadyBooked : ExpertRefusal.Busy;
            if (!alive) return ExpertRefusal.NotAlive;
            if (lostLimbs <= 0) return ExpertRefusal.WholeBody;
            if (parts < 1 || parts > MaxParts(lostLimbs, maxPerUse)) return ExpertRefusal.BadChoice;
            return ExpertRefusal.None;
        }

        /// <summary>
        /// 신체 복구 구매. 조건을 모두 통과하면 <paramref name="spend"/>(공동 자금에서 지금 단가 × 개수 차감)를 부르고,
        /// 성공하면 방을 예약하고 팀 이용 횟수를 한 번 올린다.
        /// </summary>
        public ExpertRefusal BookMedical(ulong buyer, bool serviceTime, bool alive, int lostLimbs, int parts, int maxPerUse, Func<bool> spend)
        {
            var refusal = CanBuyMedical(buyer, serviceTime, alive, lostLimbs, parts, maxPerUse);
            if (refusal != ExpertRefusal.None) return refusal;
            if (spend == null || !spend()) return ExpertRefusal.NotEnoughFunds;
            Buyer = buyer;
            BookedLimbs = parts;
            TeamUses++;
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
        /// 시간을 흘린다. 복구 시간이 끝나면 true를 한 번 돌려준다(그때 부르는 쪽이 고른 수만큼 복구한다).
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

        /// <summary>구매자가 나갔거나(접속 끊김) 공연이 시작되면 방을 비운다. 이미 낸 비용은 돌려주지 않는다(임시 규칙).</summary>
        public void Clear()
        {
            if (Phase == ExpertRoomPhase.Idle) return;
            Buyer = 0;
            BookedLimbs = 0;
            SetPhase(ExpertRoomPhase.Idle);
        }

        /// <summary>세대 교체·방 바뀜: 방을 비우고 가격을 처음으로.</summary>
        public void Reset()
        {
            if (Phase == ExpertRoomPhase.Idle && TeamUses == 0) return;
            Clear();
            TeamUses = 0;
            Revision++;
        }

        /// <summary>참가자: 호스트가 보낸 상태를 그대로 받는다.</summary>
        public void Apply(ExpertRoomPhase phase, ulong buyer, int bookedLimbs, int teamUses, long revision)
        {
            Phase = phase;
            Buyer = phase == ExpertRoomPhase.Idle ? 0 : buyer;
            BookedLimbs = phase == ExpertRoomPhase.Idle ? 0 : bookedLimbs;
            TeamUses = Math.Max(0, teamUses);
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
            ExpertRefusal.InPerformance => "쉬는 시간·밤 정비에만 이용할 수 있습니다.",
            ExpertRefusal.WholeBody => "잃은 팔다리가 없습니다.",
            ExpertRefusal.NotAlive => "살아 있는 사람만 이용할 수 있습니다.",
            ExpertRefusal.Busy => "다른 사람이 신체 복구 승강기를 이용 중입니다.",
            ExpertRefusal.AlreadyBooked => "이미 구매했습니다 — 로비 ⑧ 신체 복구 승강기 앞에서 F를 누르세요.",
            ExpertRefusal.NotEnoughFunds => "공동 자금이 모자라거나 지금은 쓸 수 없습니다.",
            ExpertRefusal.NotBuyer => "서비스를 구매한 사람만 들어갈 수 있습니다.",
            ExpertRefusal.WrongPhase => "지금은 들어갈 수 없습니다.",
            ExpertRefusal.BadChoice => "복구할 팔다리 수를 다시 고르세요.",
            _ => "",
        };
    }
}
