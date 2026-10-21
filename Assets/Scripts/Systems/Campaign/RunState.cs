using System.Collections.Generic;
using CurtainCall.Settings;

namespace CurtainCall.Campaign
{
    /// <summary>
    /// 런 진행 판정(호스트 또는 오프라인에서만 돌린다). 네트워크·Unity 오브젝트와 분리한 순수 로직.
    /// 흐름(기획 게임 흐름 2-2, 9-1):
    ///   1일차: 도입 → 공연 → 가이드 쉬는 시간 → 공연 → 밤 정비
    ///   2일차~: 아침 쉬는 시간 → 공연 → 쉬는 시간 → 공연 → 밤 정비
    ///   공연하는 날 수 다음 날: 아침 쉬는 시간만. 끝나면 런 실패 → 세대 교체 → 1일차 도입부터.
    ///   쉬는 시간 중 탈출 개시 → 탈출로 → 성공이면 엔딩, 전원 리타이어면 런 실패.
    /// 수치(일수·공연 수·쉬는 시간 길이·타이머 방식)는 공통 세팅에서 쓸 때마다 읽는다.
    /// 각 메서드는 지금 시간대에 맞지 않는 신호면 아무것도 하지 않고 false를 돌려준다.
    /// </summary>
    public sealed class RunState
    {
        readonly HashSet<ulong> ready = new();
        readonly Dictionary<int, string[]> setlists = new();
        CampaignSnapshot s;

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

        /// <summary>누군가 대기실에서 백스테이지로 나왔다. B안이면 이때부터 타이머가 흐른다.</summary>
        public bool NotifyLeftWaitingRoom()
        {
            if (s.Phase != CampaignPhase.RestTime || s.Timer != RestTimerState.WaitingForExit) return false;
            s.Timer = RestTimerState.Running;
            CheckAlarm();
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
            if (s.Phase != CampaignPhase.RestTime || s.Timer != RestTimerState.Running) return false;
            s.TimeRemaining -= deltaTime;
            if (s.TimeRemaining <= 0f)
            {
                s.TimeRemaining = 0f;
                // Observe the actual timeout before EndRest clears its timer/context.
                if (onExpired != null)
                    foreach (System.Action<CampaignSnapshot> observer in onExpired.GetInvocationList())
                        try { observer(s); } catch { /* Observers cannot change progression. */ }
                EndRest();
                return true;
            }
            CheckAlarm();
            return true;
        }

        /// <summary>밤 정비 준비 완료 표시. 밤 정비가 아니면 무시한다.</summary>
        public bool SetReady(ulong clientId, bool value)
        {
            if (s.Phase != CampaignPhase.Night) return false;
            bool changed = value ? ready.Add(clientId) : ready.Remove(clientId);
            return changed;
        }

        /// <summary>
        /// 밤 정비 인원을 다시 센다. 나간 사람은 준비 목록에서 빼고, 남은 전원이 준비됐으면 다음 날 아침 쉬는 시간으로 넘어간다.
        /// </summary>
        public bool RefreshNight(IReadOnlyCollection<ulong> players)
        {
            if (s.Phase != CampaignPhase.Night) return false;
            ready.RemoveWhere(id => !Contains(players, id));
            int before = s.ReadyCount, beforeRequired = s.RequiredCount;
            s.ReadyCount = ready.Count;
            s.RequiredCount = players.Count;

            if (s.RequiredCount > 0 && s.ReadyCount >= s.RequiredCount)
            {
                s.Day++;
                EnterRest(RestKind.Morning, 0);
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
        public bool SetSetlist(int day, string[] tricks)
        {
            if (!s.IsRunning || day <= 1) return false;
            setlists[day] = tricks != null ? (string[])tricks.Clone() : null;
            s.TrickId = TrickFor(s.Day, s.PerformanceIndex);
            return true;
        }

        /// <summary>디버그: 쉬는 시간 남은 시간을 바꾼다(타이머가 멈춰 있으면 흐르게 한다).</summary>
        public bool DebugSetRemaining(float seconds)
        {
            if (s.Phase != CampaignPhase.RestTime) return false;
            s.TimeRemaining = seconds < 0f ? 0f : seconds;
            s.Timer = RestTimerState.Running;
            s.AlarmRaised = false;
            CheckAlarm();
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
            s = new CampaignSnapshot
            {
                Generation = generation,
                Day = 1,
                Phase = CampaignPhase.Intro,
                LastEnd = previousEnd,
            };
            s.TrickId = TrickFor(1, 0);
        }

        void FailRun(RunEndReason reason) => StartRun(s.Generation + 1, reason);

        void EnterPerformance(int index)
        {
            s.Phase = CampaignPhase.Performance;
            s.Rest = RestKind.None;
            s.PerformanceIndex = index;
            s.TrickId = TrickFor(s.Day, index);
            ClearTimer();
        }

        void EnterRest(RestKind kind, int nextPerformance)
        {
            var rules = Rules;
            s.Phase = CampaignPhase.RestTime;
            s.Rest = kind;
            s.PerformanceIndex = nextPerformance;
            s.IsFinalMorning = kind == RestKind.Morning && s.Day > rules.DayCount;
            s.TrickId = s.IsFinalMorning ? "" : TrickFor(s.Day, nextPerformance);
            s.TimeRemaining = kind == RestKind.Guide ? rules.GuideRestDuration : rules.RestDuration;
            s.AlarmRaised = false;
            s.Timer = rules.RestTimerMode == RestTimerMode.FromRestStart ? RestTimerState.Running : RestTimerState.WaitingForExit;
            CheckAlarm();
        }

        void EnterNight()
        {
            s.Phase = CampaignPhase.Night;
            s.Rest = RestKind.None;
            s.TrickId = "";
            ClearTimer();
            ready.Clear();
            s.ReadyCount = 0;
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
            if (s.Timer == RestTimerState.Running && !s.AlarmRaised && s.TimeRemaining <= Rules.RecallAlarmLeadTime)
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
