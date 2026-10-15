using Unity.Netcode;

namespace CurtainCall.Campaign
{
    /// <summary>
    /// 런의 현재 시간대. 방 상태(<c>GameSessionState</c>: 로비 / 런 진행 / 런 종료)와 별개다.
    /// 숫자로 전송하므로 값은 끝에만 추가한다.
    /// </summary>
    public enum CampaignPhase : byte
    {
        /// <summary>런이 시작되지 않았다(로비).</summary>
        None,
        /// <summary>도입 컷씬(세대 2부터는 세대 교체 연출).</summary>
        Intro,
        /// <summary>공연 중.</summary>
        Performance,
        /// <summary>쉬는 시간(아침·가이드·공연 사이).</summary>
        RestTime,
        /// <summary>밤 정비. 타이머 없음, 전원 준비 완료로 끝난다.</summary>
        Night,
        /// <summary>탈출로. 쉬는 시간 타이머는 멈춘다.</summary>
        Escape,
        /// <summary>탈출 성공 엔딩.</summary>
        Ending,
    }

    /// <summary>쉬는 시간 종류.</summary>
    public enum RestKind : byte
    {
        None,
        /// <summary>2일차부터 첫 공연 전. 마지막 날 다음 날 아침이면 끝날 때 런 실패.</summary>
        Morning,
        /// <summary>1일차 첫 공연 뒤(첫 대기실 진입). 길이는 공통 세팅의 가이드 쉬는 시간.</summary>
        Guide,
        /// <summary>2일차부터 공연 사이.</summary>
        Between,
    }

    /// <summary>쉬는 시간 타이머 상태.</summary>
    public enum RestTimerState : byte
    {
        /// <summary>쉬는 시간이 아니다.</summary>
        None,
        /// <summary>B안: 아직 아무도 대기실에서 나오지 않아 멈춰 있다.</summary>
        WaitingForExit,
        /// <summary>흐르는 중.</summary>
        Running,
        /// <summary>탈출 개시로 멈췄다.</summary>
        Stopped,
    }

    /// <summary>런이 끝난 이유. 숫자로 전송하므로 값은 끝에만 추가한다.</summary>
    public enum RunEndReason : byte
    {
        None,
        /// <summary>마지막 날 다음 날 아침 쉬는 시간이 끝남(기한 초과) → 런 실패.</summary>
        Deadline,
        /// <summary>탈출로에서 전원 리타이어 → 런 실패.</summary>
        EscapeAllRetired,
        /// <summary>한 명 이상 탈출로 목적지 도착 → 엔딩.</summary>
        EscapeSucceeded,
    }

    /// <summary>
    /// 모두가 같게 보는 런 상태 한 장. 호스트가 만들어 모두에게 보낸다(<see cref="CampaignRun"/>).
    /// 세션 범위(세대가 바뀌어도 유지)는 <see cref="Generation"/>, 나머지는 런 범위(세대 교체 때 초기화).
    /// </summary>
    public struct CampaignSnapshot
    {
        /// <summary>세대 번호(1부터). 런 실패마다 1 오른다.</summary>
        public int Generation;
        /// <summary>일차(1부터). 공연하는 날 수 다음 날은 아침 쉬는 시간만 있다.</summary>
        public int Day;
        public CampaignPhase Phase;
        public RestKind Rest;
        /// <summary>그날 지금(공연 중) 또는 다음(쉬는 시간) 공연 순번(0부터).</summary>
        public int PerformanceIndex;
        /// <summary>지금 또는 다음 공연 묘기 이름. 정해지지 않았으면 빈 문자열.</summary>
        public string TrickId;
        public RestTimerState Timer;
        /// <summary>쉬는 시간 남은 시간(초).</summary>
        public float TimeRemaining;
        /// <summary>이번 쉬는 시간에 소집 경보가 울렸는지.</summary>
        public bool AlarmRaised;
        /// <summary>마지막 탈출 기회(공연하는 날 수 다음 날 아침 쉬는 시간)인지.</summary>
        public bool IsFinalMorning;
        /// <summary>밤 정비 준비 완료 인원 / 필요 인원(접속 중 전원).</summary>
        public int ReadyCount;
        public int RequiredCount;
        /// <summary>직전 런이 끝난 이유. 세대 1 첫 런은 <see cref="RunEndReason.None"/>.</summary>
        public RunEndReason LastEnd;

        public bool IsRunning => Phase != CampaignPhase.None;

        /// <summary>세대 교체 연출(세대 2부터의 도입) 중인지.</summary>
        public bool IsGenerationChangeIntro => Phase == CampaignPhase.Intro && Generation > 1;

        /// <summary>남은 시간만 다르면 같은 시간대로 본다.</summary>
        public bool SameStep(in CampaignSnapshot other) =>
            Generation == other.Generation && Day == other.Day && Phase == other.Phase && Rest == other.Rest
            && PerformanceIndex == other.PerformanceIndex && Timer == other.Timer && AlarmRaised == other.AlarmRaised
            && ReadyCount == other.ReadyCount && RequiredCount == other.RequiredCount && LastEnd == other.LastEnd
            && TrickId == other.TrickId;

        internal void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe(Generation);
            writer.WriteValueSafe(Day);
            writer.WriteValueSafe((byte)Phase);
            writer.WriteValueSafe((byte)Rest);
            writer.WriteValueSafe(PerformanceIndex);
            writer.WriteValueSafe(TrickId ?? "");
            writer.WriteValueSafe((byte)Timer);
            writer.WriteValueSafe(TimeRemaining);
            writer.WriteValueSafe(AlarmRaised);
            writer.WriteValueSafe(IsFinalMorning);
            writer.WriteValueSafe(ReadyCount);
            writer.WriteValueSafe(RequiredCount);
            writer.WriteValueSafe((byte)LastEnd);
        }

        internal static CampaignSnapshot Read(FastBufferReader reader)
        {
            var s = new CampaignSnapshot();
            reader.ReadValueSafe(out s.Generation);
            reader.ReadValueSafe(out s.Day);
            reader.ReadValueSafe(out byte phase);
            reader.ReadValueSafe(out byte rest);
            reader.ReadValueSafe(out s.PerformanceIndex);
            reader.ReadValueSafe(out s.TrickId);
            reader.ReadValueSafe(out byte timer);
            reader.ReadValueSafe(out s.TimeRemaining);
            reader.ReadValueSafe(out s.AlarmRaised);
            reader.ReadValueSafe(out s.IsFinalMorning);
            reader.ReadValueSafe(out s.ReadyCount);
            reader.ReadValueSafe(out s.RequiredCount);
            reader.ReadValueSafe(out byte lastEnd);
            s.Phase = (CampaignPhase)phase;
            s.Rest = (RestKind)rest;
            s.Timer = (RestTimerState)timer;
            s.LastEnd = (RunEndReason)lastEnd;
            return s;
        }
    }
}
