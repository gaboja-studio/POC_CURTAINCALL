using System.Collections.Generic;
using CurtainCall.Settings;

namespace CurtainCall.Campaign
{
    /// <summary>
    /// 런 진행 판정(호스트 또는 오프라인에서만 돌린다). 네트워크·Unity 오브젝트와 분리한 순수 로직.
    /// 흐름(기획 게임 흐름 2-2, 9-1):
    ///   1일차: 도입 → 공연 → 가이드 쉬는 시간 → 공연 → 밤 정비
    ///   2일차~: 아침 쉬는 시간 → 공연 → 쉬는 시간 → 공연 → 밤 정비
    ///   공연하는 날 수 다음 날: 아침 쉬는 시간만. 끝나면 런 실패 → 세대 교체 → 1일차 첫 공연.
    ///   0.1.0: 4일차 아침은 탐색 없이 데모 종료 연출로 전환한다.
    ///   쉬는 시간 중 탈출 개시 → 탈출로 → 성공이면 엔딩, 전원 리타이어면 런 실패.
    /// 유한 제한시간은 단계 진입부터 흐른다(M2). 제한시간은 진입할 때, 나머지 수치는 필요할 때 공통 세팅에서 읽는다.
    /// 각 메서드는 지금 시간대에 맞지 않는 신호면 아무것도 하지 않고 false를 돌려준다.
    /// </summary>
    public sealed class RunState
    {
        public const int MaxDifficulty = 3;

        readonly HashSet<ulong> ready = new();
        readonly Dictionary<int, string[]> setlists = new();
        readonly Dictionary<int, int[]> levels = new();
        // 묘기별로 이번 런에서 성공한 가장 높은 단계(M2 난이도 선택). 런 범위라 세대 교체 때 지운다.
        readonly Dictionary<string, int> cleared = new();
        readonly bool demoVersion;
        readonly bool dayPresentation;
        CampaignSnapshot s;

        public RunState() : this(false) { }
        public RunState(bool demoVersion) : this(demoVersion, false) { }
        public RunState(bool demoVersion, bool dayPresentation)
        {
            this.demoVersion = demoVersion;
            this.dayPresentation = dayPresentation;
        }

        static BaseGameSettings.RunSettings Rules => GameSettings.Base.Run;

        public CampaignSnapshot Snapshot => s;

        /// <summary>세대 1부터 새로 시작한다(게임 시작).</summary>
        public void StartSession() => StartRun(1, RunEndReason.None);

        /// <summary>도입 컷씬(세대 교체 연출)이 끝났다 → 1일차 첫 공연.</summary>
        public bool EndIntro()
        {
            if (s.Phase != CampaignPhase.Intro) return false;
            EnterPerformance(0);
            return true;
        }

        /// <summary>공연이 끝났다 → 다음 쉬는 시간, 또는 그날 마지막 공연이면 밤 정비.</summary>
        public bool EndPerformance()
        {
            if (s.Phase != CampaignPhase.Performance) return false;
            int next = s.PerformanceIndex + 1;
            if (next >= Rules.PerformancesPerDay)
                EnterNight();
            else
                EnterRest(s.Day == 1 && next == 1 ? RestKind.Guide : RestKind.Between, next);
            return true;
        }

        /// <summary>전원이 무대 입구 통로에 들어갔다 → 쉬는 시간 조기 종료. 마지막 아침에는 공연이 없으므로 무시한다.</summary>
        public bool NotifyAllAtStageEntrance()
        {
            if (s.Phase != CampaignPhase.RestTime || s.IsFinalMorning) return false;
            EndRest();
            return true;
        }

        /// <summary>시간을 흘린다. 쉬는 시간 타이머가 흐르는 중이 아니면 false.</summary>
        public bool Tick(float deltaTime, System.Action<CampaignSnapshot> onExpired = null)
        {
            if ((s.Phase != CampaignPhase.RestTime && s.Phase != CampaignPhase.Night)
                || s.Timer != RestTimerState.Running || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) return false;
            s.TimeRemaining -= deltaTime;
            if (s.TimeRemaining <= 0f)
            {
                s.TimeRemaining = 0f;
                // 밤 정비 만료는 수면으로만 넘긴다. 쉬는 시간 소집·잡힘 이벤트는 호출하지 않는다.
                if (s.Phase == CampaignPhase.Night)
                {
                    CompleteNight();
                    return true;
                }
                // 쉬는 시간 상태가 지워지기 전에 실제 만료를 알린다.
                if (onExpired != null)
                    foreach (System.Action<CampaignSnapshot> observer in onExpired.GetInvocationList())
                        try { observer(s); } catch { /* Observers cannot change progression. */ }
                EndRest();
                return true;
            }
            CheckAlarm();
            return true;
        }

        /// <summary>밤 준비 / 아침 전환 가림 완료 / 데모 종료 확인. 그 외 시간대면 무시한다.</summary>
        public bool SetReady(ulong clientId, bool value)
        {
            if (s.Phase != CampaignPhase.Night && s.Phase != CampaignPhase.Dawn && s.Phase != CampaignPhase.DemoEnding) return false;
            if (s.Phase != CampaignPhase.Night && !value) return false;
            bool changed = value ? ready.Add(clientId) : ready.Remove(clientId);
            return changed;
        }

        /// <summary>
        /// 밤 정비 인원을 다시 센다. 나간 사람은 준비 목록에서 빼고, 남은 전원이 준비됐으면 다음 날 아침 쉬는 시간으로 넘어간다.
        /// </summary>
        public bool RefreshNight(IReadOnlyCollection<ulong> players)
        {
            if (s.Phase != CampaignPhase.Night && s.Phase != CampaignPhase.Dawn && s.Phase != CampaignPhase.DemoEnding) return false;
            ready.RemoveWhere(id => !Contains(players, id));
            int before = s.ReadyCount, beforeRequired = s.RequiredCount;
            s.ReadyCount = ready.Count;
            s.RequiredCount = players.Count;

            if (s.RequiredCount > 0 && s.ReadyCount >= s.RequiredCount)
            {
                if (s.Phase == CampaignPhase.DemoEnding)
                    FailRun(RunEndReason.Deadline);
                else if (s.Phase == CampaignPhase.Night)
                    CompleteNight();
                else
                {
                    s.Day++;
                    EnterRest(RestKind.Morning, 0);
                }
                return true;
            }
            return before != s.ReadyCount || beforeRequired != s.RequiredCount;
        }

        /// <summary>쉬는 시간 중 탈출 개시(전원 입구 집결) → 탈출로. 쉬는 시간 타이머는 멈춘다.</summary>
        public bool StartEscape()
        {
            if (s.Phase != CampaignPhase.RestTime) return false;
            s.Phase = CampaignPhase.Escape;
            s.Timer = RestTimerState.Stopped;
            return true;
        }

        /// <summary>탈출로가 끝났다. 한 명 이상 도착이면 엔딩, 전원 리타이어면 런 실패 → 세대 교체.</summary>
        public bool EndEscape(bool anyoneArrived)
        {
            if (s.Phase != CampaignPhase.Escape) return false;
            if (anyoneArrived)
            {
                s.Phase = CampaignPhase.Ending;
                s.Timer = RestTimerState.None;
                s.LastEnd = RunEndReason.EscapeSucceeded;
            }
            else
            {
                FailRun(RunEndReason.EscapeAllRetired);
            }
            return true;
        }

        /// <summary>
        /// <paramref name="day"/>일차 공연 순서(세트리스트)를 정한다. 1일차는 공통 세팅의 고정 순서를 쓰므로 무시한다.
        /// 런 범위라 세대 교체 때 지워진다.
        /// </summary>
        public bool SetSetlist(int day, string[] tricks) => SetSetlist(day, tricks, null);

        /// <summary>
        /// 공연 순서와 묘기별 난이도 단계(밤 정비에서 고름)를 함께 정한다. 단계가 없거나 0이면 1단계.
        /// 실제 공연 단계는 시작할 때 다시 "깬 단계의 다음 단계"까지로 줄인다(<see cref="MaxSelectableTier"/>).
        /// </summary>
        public bool SetSetlist(int day, string[] tricks, int[] tiers)
        {
            if (!s.IsRunning || day <= 1) return false;
            setlists[day] = tricks != null ? (string[])tricks.Clone() : null;
            levels[day] = tiers != null ? (int[])tiers.Clone() : null;
            RefreshCurrent();
            return true;
        }

        /// <summary>이번 런에서 그 묘기를 성공한 가장 높은 단계(없으면 0).</summary>
        public int ClearedTier(string trickId) => !string.IsNullOrEmpty(trickId) && cleared.TryGetValue(trickId, out int tier) ? tier : 0;

        /// <summary>그 묘기에 고를 수 있는 가장 높은 단계 = 깬 단계의 다음 단계(최대 3). 못 깨면 1단계만.</summary>
        public int MaxSelectableTier(string trickId) => System.Math.Min(MaxDifficulty, ClearedTier(trickId) + 1);

        /// <summary>공연 성공을 기록한다. 그 단계가 지금까지보다 높으면 다음 단계가 열린다.</summary>
        public bool RecordClear(string trickId, int tier)
        {
            if (!s.IsRunning || string.IsNullOrEmpty(trickId) || tier < 1 || tier > MaxDifficulty) return false;
            if (ClearedTier(trickId) >= tier) return false;
            cleared[trickId] = tier;
            return true;
        }

        /// <summary>디버그: 쉬는 시간·밤 정비를 명시한 유한 제한시간으로 바꾼다.</summary>
        public bool DebugSetRemaining(float seconds)
        {
            if ((s.Phase != CampaignPhase.RestTime && s.Phase != CampaignPhase.Night)
                || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
            s.TimeRemaining = seconds < 0f ? 0f : seconds;
            s.Timer = RestTimerState.Running;
            s.AlarmRaised = false;
            CheckAlarm();
            return true;
        }

        /// <summary>확인창을 연 시간대가 그대로일 때만 현재 런을 실패 처리한다.</summary>
        public bool AbandonRun(CampaignSnapshot expected)
        {
            if ((s.Phase != CampaignPhase.RestTime && s.Phase != CampaignPhase.Night)
                || s.Generation == int.MaxValue || s.Generation != expected.Generation
                || s.Day != expected.Day || s.Phase != expected.Phase || s.Rest != expected.Rest
                || s.PerformanceIndex != expected.PerformanceIndex) return false;
            FailRun(RunEndReason.Abandoned);
            return true;
        }

        /// <summary>개발용 전체 리셋. 새 세대 식별자로 명부·자금·부상 초기화를 알리되 실패로 기록하지 않는다.</summary>
        public bool DebugRestartRun()
        {
            if (!s.IsRunning || s.Generation == int.MaxValue) return false;
            StartRun(s.Generation + 1, RunEndReason.None);
            return true;
        }

        /// <summary>유효한 개발 시작 지점으로 새 세대를 만든다. 건너뛴 공연의 종료/보상 이벤트는 만들지 않는다.</summary>
        public bool DebugStartAt(RunStartPoint point)
        {
            if (!point.IsValid(out _) || s.Generation == int.MaxValue) return false;
            StartRun(s.Generation > 0 ? s.Generation + 1 : 1, RunEndReason.None);
            s.Day = point.Day;
            // 후속 공연도 미정으로 떨어지지 않도록 테스트 런에만 기본 편성을 둔다.
            for (int day = 1; day <= Rules.DayCount; day++)
            {
                var list = new string[Rules.PerformancesPerDay];
                for (int index = 0; index < list.Length; index++) list[index] = Rules.FirstDayTrick(index);
                setlists[day] = list;
            }
            int next = point.Phase == RunStartPhase.Morning ? 0 : point.PerformanceIndex;
            if (point.Day <= Rules.DayCount && point.Phase != RunStartPhase.Night) setlists[point.Day][next] = point.TrickId;
            switch (point.Phase)
            {
                case RunStartPhase.Performance: EnterPerformance(next); break;
                case RunStartPhase.Morning: EnterRest(RestKind.Morning, 0); break;
                case RunStartPhase.Between: EnterRest(point.Day == 1 && next == 1 ? RestKind.Guide : RestKind.Between, next); break;
                case RunStartPhase.Night: s.PerformanceIndex = Rules.PerformancesPerDay - 1; EnterNight(); break;
            }
            return true;
        }

        void StartRun(int generation, RunEndReason previousEnd)
        {
            ready.Clear();
            setlists.Clear();
            levels.Clear();
            cleared.Clear();
            s = new CampaignSnapshot
            {
                Generation = generation,
                Day = 1,
                Phase = CampaignPhase.Intro,
                LastEnd = previousEnd,
            };
            RefreshCurrent(1, 0);
        }

        void RefreshCurrent() => RefreshCurrent(s.Day, s.PerformanceIndex);

        // 지금(또는 다음) 공연 묘기와 단계를 함께 맞춘다.
        void RefreshCurrent(int day, int index)
        {
            s.TrickId = TrickFor(day, index);
            s.Difficulty = string.IsNullOrEmpty(s.TrickId) ? 0 : TierFor(day, index, s.TrickId);
        }

        // 1일차는 1단계 고정. 그 뒤는 고른 단계를 깬 단계의 다음 단계까지로 줄인다.
        int TierFor(int day, int index, string trickId)
        {
            if (day <= 1) return 1;
            int chosen = levels.TryGetValue(day, out var list) && list != null && index >= 0 && index < list.Length ? list[index] : 1;
            return System.Math.Clamp(chosen, 1, MaxSelectableTier(trickId));
        }

        void FailRun(RunEndReason reason)
        {
            StartRun(s.Generation + 1, reason);
            EnterPerformance(0);
        }

        void EnterPerformance(int index)
        {
            s.Phase = CampaignPhase.Performance;
            s.Rest = RestKind.None;
            s.PerformanceIndex = index;
            RefreshCurrent(s.Day, index);
            ClearTimer();
        }

        void EnterRest(RestKind kind, int nextPerformance)
        {
            if (demoVersion && kind == RestKind.Morning && s.Day == 4)
            {
                ClearTimer();
                s.Phase = CampaignPhase.DemoEnding;
                s.Rest = RestKind.Morning;
                s.PerformanceIndex = 0;
                s.TrickId = ""; s.Difficulty = 0;
                ready.Clear();
                s.ReadyCount = 0;
                return;
            }
            var rules = Rules;
            s.Phase = CampaignPhase.RestTime;
            s.Rest = kind;
            s.PerformanceIndex = nextPerformance;
            s.IsFinalMorning = kind == RestKind.Morning && s.Day > rules.DayCount;
            if (s.IsFinalMorning) { s.TrickId = ""; s.Difficulty = 0; } else RefreshCurrent(s.Day, nextPerformance);
            // 제한시간은 진입할 때만 적용한다. 도중 설정 변경으로 남은 시간을 재시작하지 않는다.
            StartTimer(rules.TimeLimit(kind));
        }

        void EnterNight()
        {
            s.Phase = CampaignPhase.Night;
            s.Rest = RestKind.None;
            s.TrickId = ""; s.Difficulty = 0;
            ClearTimer();
            StartTimer(Rules.NightMaintenance);
            ready.Clear();
            s.ReadyCount = 0;
        }

        // 전원 준비와 제한시간 만료는 같은 밤 종료 경로를 쓴다.
        void CompleteNight()
        {
            ClearTimer();
            ready.Clear();
            s.ReadyCount = 0;
            if (dayPresentation)
                s.Phase = CampaignPhase.Dawn;
            else
            {
                s.Day++;
                EnterRest(RestKind.Morning, 0);
            }
        }

        void StartTimer(BaseGameSettings.RunSettings.RestTimeLimitSettings limit)
        {
            s.Timer = limit.Unlimited ? RestTimerState.Unlimited : RestTimerState.Running;
            s.TimeRemaining = limit.Unlimited ? 0f : limit.Seconds;
            s.AlarmRaised = false;
            CheckAlarm();
        }

        void EndRest()
        {
            if (s.IsFinalMorning)
                FailRun(RunEndReason.Deadline);
            else
                EnterPerformance(s.PerformanceIndex);
        }

        void ClearTimer()
        {
            s.Timer = RestTimerState.None;
            s.TimeRemaining = 0f;
            s.AlarmRaised = false;
            s.IsFinalMorning = false;
        }

        void CheckAlarm()
        {
            if (s.Phase == CampaignPhase.RestTime && s.Timer == RestTimerState.Running
                && !s.AlarmRaised && s.TimeRemaining <= Rules.RecallAlarmLeadTime)
                s.AlarmRaised = true;
        }

        string TrickFor(int day, int index)
        {
            if (setlists.TryGetValue(day, out var list) && list != null && index >= 0 && index < list.Length) return list[index] ?? "";
            return day == 1 ? Rules.FirstDayTrick(index) : "";
        }

        static bool Contains(IReadOnlyCollection<ulong> players, ulong id)
        {
            foreach (var p in players)
                if (p == id) return true;
            return false;
        }
    }
}
