using System;
using CurtainCall.Campaign;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 스케줄표(세트리스트) 상태. 런 동안 장소 전환에도 남는다(DontDestroyOnLoad, 씬 사본은 버림).
    /// - 판정은 런 진행 권한이 있는 호스트(오프라인 런 포함)만 한다. 편집·확정도 호스트만 한다(2026-10-17 담당자 결정).
    /// - 참가자는 호스트가 보내는 상태를 그대로 받아 같은 규칙(<see cref="ScheduleBook"/>)으로 난도를 계산해 보여 준다.
    /// - 호스트는 런 상태를 지켜 밤 시작·끝을 장부에 알리고, 세대 교체·런 종료·런 재시작 때 장부를 비운다.
    /// - 확정하면 실행할 일차(2·3일차) 공연 순서를 <see cref="CampaignRun.SetSetlist"/>로 넘긴다(4일차는 넘기지 않음).
    /// - 쉬는 시간에는 바로 다음 공연의 단계만 호스트가 다시 고를 수 있다(<see cref="AdjustTier"/>, Task-082).
    /// </summary>
    public sealed class ScheduleBoard : MonoBehaviour
    {
        [Tooltip("2일차부터 고를 수 있는 공연(묘기 ID, 런 상태 TrickId와 같은 값). 임시: 지금 있는 묘기 2개.")]
        [SerializeField] string[] selectableTricks = { "Tightrope", "HumanCannon" };

        [Tooltip("화면에 보일 공연 이름. selectableTricks와 같은 순서. 비우면 ID를 그대로 보인다.")]
        [SerializeField] string[] trickNames = { "줄타기", "인간 대포" };

        ScheduleBook book;
        ScheduleChannel channel;
        int seenGeneration;
        bool seenRunning;

        public static ScheduleBoard Instance { get; private set; }

        /// <summary>장부(읽기 전용으로 쓴다). 호스트는 판정 원본, 참가자는 받은 사본.</summary>
        public ScheduleBook Book => book;

        /// <summary>편집·확정할 수 있는가(런 진행 권한이 있는 호스트).</summary>
        public bool HasAuthority => CampaignRun.Instance != null && CampaignRun.Instance.HasAuthority;

        public string[] SelectableTricks => (string[])selectableTricks.Clone();

        /// <summary>이번 런에서 고를 수 있는 공연인지(빈 칸은 항상 가능). 인간 대포는 배치 기물을 발견해야 열린다.</summary>
        public static bool IsUnlocked(string trickId) =>
            string.IsNullOrEmpty(trickId) || CurtainCall.Campaign.CampaignRun.Instance == null || CurtainCall.Campaign.CampaignRun.Instance.IsTrickUnlocked(trickId);

        /// <summary>마지막 편집·확정 결과 문구(호스트 화면용).</summary>
        public string LastMessage { get; private set; } = "";

        /// <summary>상태가 바뀌었다(받은 상태 포함).</summary>
        public event Action Changed;

        /// <summary>호스트가 이번 밤 작성을 확정했다(밤 일차).</summary>
        public event Action<int> Confirmed;

        public string TrickName(string trickId)
        {
            if (string.IsNullOrEmpty(trickId)) return "";
            int index = Array.IndexOf(selectableTricks, trickId);
            return index >= 0 && trickNames != null && index < trickNames.Length && !string.IsNullOrEmpty(trickNames[index])
                ? trickNames[index] : trickId;
        }

        void Awake()
        {
            // 대기실 씬이 다시 열리면 씬에 놓인 사본은 버리고, 런 동안 남아 있던 스케줄표를 그대로 쓴다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            else Debug.LogWarning("[NightSchedule] 스케줄표 상태는 씬 최상위 오브젝트에 둬야 장소 전환 뒤에도 남습니다.", this);

            var run = GameSettings.Base.Run;
            book = new ScheduleBook(new[] { run.FirstDayTrick(0), run.FirstDayTrick(1) }, selectableTricks);
            channel = new ScheduleChannel("CurtainCall.WaitingRoom.NightSchedule", HandleState, SendTo, HandleReset);
            RunState.NightEndGate = NightCanEnd;
        }

        /// <summary>오늘 밤 고칠 일차가 있으면 호스트가 확정해야 밤이 끝난다(전원 침대와 함께).</summary>
        public bool NightCanEnd()
        {
            var run = CampaignRun.Instance;
            int day = run != null ? run.State.Day : 0;
            return ScheduleBook.EditableDays(day).Length == 0 || book.IsConfirmedTonight;
        }

        void OnDestroy()
        {
            channel?.Dispose();
            if (Instance == this) { Instance = null; RunState.NightEndGate = null; }
        }

        void Update()
        {
            channel.Update();
            if (HasAuthority) WatchRun();
        }

        /// <summary>호스트: 칸 하나를 고른다(1단계). 빈 문자열이면 비운다.</summary>
        public ScheduleRefusal Set(int day, int slot, string trickId) => Set(day, slot, trickId, 1);

        /// <summary>호스트: 칸 하나에 공연과 난이도 단계를 고른다. 깬 단계의 다음 단계까지만 고를 수 있다.</summary>
        public ScheduleRefusal Set(int day, int slot, string trickId, int tier)
        {
            if (!HasAuthority) return Answer(ScheduleRefusal.NotEditable);
            if (!IsUnlocked(trickId)) return Answer(ScheduleRefusal.Locked);
            var refusal = book.Set(day, slot, trickId, tier, MaxTier(trickId));
            if (refusal == ScheduleRefusal.None) Publish();
            return Answer(refusal);
        }

        /// <summary>
        /// 그 칸이 쉬는 시간에 단계를 다시 고를 수 있는 바로 다음 공연인가(2일차부터, 스케줄표로 편성한 칸).
        /// 참가자도 같은 판단으로 표시만 한다. 실제 변경은 <see cref="AdjustTier"/>에서 호스트만.
        /// </summary>
        public bool IsNextPerformance(int day, int slot)
        {
            var run = CampaignRun.Instance;
            if (run == null || day <= 1 || day > ScheduleBook.PerformanceDays) return false;
            var s = run.State;
            return s.Phase == CampaignPhase.RestTime && !s.IsFinalMorning && s.Day == day && s.PerformanceIndex == slot
                && !string.IsNullOrEmpty(book.Get(day, slot)) && book.Get(day, slot) == s.TrickId;
        }

        /// <summary>호스트: 쉬는 시간에 바로 다음 공연의 단계만 바꾼다. 런 진행 단계와 스케줄표를 함께 고친다.</summary>
        public ScheduleRefusal AdjustTier(int day, int slot, int tier)
        {
            if (!HasAuthority) return Answer(ScheduleRefusal.NotEditable);
            if (!IsNextPerformance(day, slot)) return Answer(ScheduleRefusal.NotNextPerformance);
            if (book.Difficulty(day, slot) == tier) return ScheduleRefusal.None;
            if (tier < 1 || tier > MaxTier(book.Get(day, slot)) || !CampaignRun.Instance.AdjustNextTier(tier)) return Answer(ScheduleRefusal.BadTier);
            var refusal = book.AdjustTier(day, slot, tier, MaxTier(book.Get(day, slot)));
            if (refusal == ScheduleRefusal.None)
            {
                Debug.Log($"[NightSchedule] {day}일차 공연 {slot + 1} 단계 조정: {tier}단계");
                Publish();
            }
            return Answer(refusal);
        }

        /// <summary>그 공연에 지금 고를 수 있는 가장 높은 단계(호스트 판정, 깬 단계 + 1, 최대 3).</summary>
        public int MaxTier(string trickId) => CampaignRun.Instance != null ? CampaignRun.Instance.MaxSelectableTier(trickId) : 1;

        /// <summary>단계별 보상 안내(1인 호응 상한 · 도착자 완주 보너스). 보상 확정 규칙 표와 같은 값이다.</summary>
        public static string TierRewardText(int tier)
        {
            var rules = Reward.RewardRules.Temporary(Mathf.Clamp(tier, 1, ScheduleBook.MaxDifficulty));
            return WaitingRoomText.Format("schedule.tier-reward", "호응 상한 {cap} · 완주 보너스 {bonus}", ("cap", rules.Cap), ("bonus", rules.CompletionBonus));
        }

        /// <summary>호스트: 이번 밤 작성을 확정한다. 실행할 일차는 런 공연 순서로 넘긴다.</summary>
        public ScheduleRefusal Confirm()
        {
            if (!HasAuthority) return Answer(ScheduleRefusal.NotEditable);
            int night = book.Night;
            var refusal = book.Confirm();
            if (refusal != ScheduleRefusal.None) return Answer(refusal);

            var run = CampaignRun.Instance;
            foreach (int day in ScheduleBook.DaysToApply(night))
            {
                var setlist = book.Setlist(day);
                var tiers = book.Tiers(day);
                bool applied = run.SetSetlist(day, setlist, tiers);
                Debug.Log($"[NightSchedule] {night}일차 밤 확정: {day}일차 {string.Join(" → ", setlist)} (단계 {string.Join("/", tiers)}){(applied ? "" : " (런에 넘기지 못함)")}");
            }
            Publish();
            LastMessage = WaitingRoomText.Get("schedule.confirmed", "스케줄표를 확정했습니다.");
            Confirmed?.Invoke(night);
            return ScheduleRefusal.None;
        }

        // 밤 정비 준비 완료는 M2에서 숙소 침대(NightBed)로 옮겼다. 스케줄표 확정은 밤 종료 조건이 아니다(Task-079).

        public static string Explain(ScheduleRefusal refusal) => refusal switch
        {
            ScheduleRefusal.None => "",
            ScheduleRefusal.NotNight => WaitingRoomText.Get("schedule.refuse-not-night", "밤 정비 때만 쓸 수 있습니다."),
            ScheduleRefusal.NotEditable => WaitingRoomText.Get("schedule.refuse-not-editable", "지금은 고칠 수 없는 일차입니다."),
            ScheduleRefusal.AlreadyConfirmed => WaitingRoomText.Get("schedule.refuse-already-confirmed", "이번 밤에는 이미 확정했습니다."),
            ScheduleRefusal.UnknownTrick => WaitingRoomText.Get("schedule.refuse-unknown-trick", "고를 수 없는 공연입니다."),
            ScheduleRefusal.BadSlot => WaitingRoomText.Get("schedule.refuse-bad-slot", "없는 칸입니다."),
            ScheduleRefusal.Incomplete => WaitingRoomText.Get("schedule.refuse-incomplete", "빈 칸을 모두 채워야 확정할 수 있습니다."),
            ScheduleRefusal.BadTier => WaitingRoomText.Get("schedule.refuse-bad-tier", "깬 단계의 다음 단계까지만 고를 수 있습니다."),
            ScheduleRefusal.NotNextPerformance => WaitingRoomText.Get("schedule.refuse-not-next", "쉬는 시간에는 바로 다음 공연의 단계만 바꿀 수 있습니다."),
            ScheduleRefusal.Locked => WaitingRoomText.Get("schedule.refuse-locked", "아직 발견하지 않은 공연입니다. 대기실에서 공연 기물을 찾아야 열립니다."),
            _ => WaitingRoomText.Get("schedule.refuse-other", "할 수 없습니다."),
        };

        ScheduleRefusal Answer(ScheduleRefusal refusal)
        {
            if (refusal != ScheduleRefusal.None) LastMessage = Explain(refusal);
            return refusal;
        }

        // 호스트: 밤 시작·끝, 세대 교체·런 종료/재시작을 장부에 반영한다.
        void WatchRun()
        {
            var s = CampaignRun.Instance.State;
            bool changed = false;

            // 세대가 바뀌거나(올라가든 내려가든) 런이 끝났다 다시 시작하면 처음부터 쓴다.
            if ((seenGeneration > 0 && s.Generation != seenGeneration) || (seenRunning && !s.IsRunning))
            {
                book.Clear();
                LastMessage = "";
                Debug.Log($"[NightSchedule] 세대 {seenGeneration} → {s.Generation}(런 {(s.IsRunning ? "진행" : "끝")}): 스케줄표를 비웠습니다.");
                changed = true;
            }
            seenGeneration = s.Generation;
            seenRunning = s.IsRunning;

            if (s.Phase == CampaignPhase.Night && book.Night != s.Day)
            {
                book.BeginNight(s.Day);
                LastMessage = "";
                changed = true;
            }
            else if (s.Phase != CampaignPhase.Night && book.Night != 0)
            {
                book.EndNight();
                changed = true;
            }

            if (changed) Publish();
        }

        void Publish()
        {
            channel.SendState(JsonUtility.ToJson(StateData.From(book)));
            Changed?.Invoke();
        }

        void SendTo(ulong client) => channel.SendState(JsonUtility.ToJson(StateData.From(book)), client);

        void HandleState(string json)
        {
            if (HasAuthority) return;
            var state = JsonUtility.FromJson<StateData>(json);
            if (state == null || !book.Load(state.night, state.confirmed, state.slots, state.locked,
                state.tiers != null && state.tiers.Length > 0 ? state.tiers : null)) return;
            Changed?.Invoke();
        }

        void HandleReset()
        {
            // 방이 바뀌면 이전 방의 스케줄을 남기지 않는다.
            book.Clear();
            seenGeneration = 0;
            seenRunning = false;
            LastMessage = "";
            Changed?.Invoke();
        }

        [Serializable]
        sealed class StateData
        {
            public int night;
            public bool confirmed;
            public string[] slots;
            public bool[] locked;
            public int[] tiers;

            public static StateData From(ScheduleBook book) => new()
            {
                night = book.Night,
                confirmed = book.IsConfirmedTonight,
                slots = book.ExportSlots(),
                locked = book.ExportLocked(),
                tiers = book.ExportTiers(),
            };
        }
    }
}
