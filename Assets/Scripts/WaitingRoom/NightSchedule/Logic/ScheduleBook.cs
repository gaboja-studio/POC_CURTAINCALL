using System;
using System.Collections.Generic;

namespace CurtainCall.WaitingRoom
{
    public enum ScheduleRefusal : byte
    {
        None,
        NotNight,
        NotEditable,
        AlreadyConfirmed,
        UnknownTrick,
        BadSlot,
        Incomplete,
        /// <summary>고를 수 없는 난이도 단계(깬 단계의 다음 단계까지만, 최대 3).</summary>
        BadTier,
        /// <summary>쉬는 시간 단계 조정은 바로 다음 공연 칸만 할 수 있다.</summary>
        NotNextPerformance,
        /// <summary>아직 발견하지 않은 공연(배치 기물 발견 전).</summary>
        Locked,
        /// <summary>The human cannon lesson must be mastered before its first schedule confirmation.</summary>
        RehearsalRequired,
    }

    /// <summary>
    /// 스케줄표(세트리스트) 규칙. 판정은 호스트 한 곳에서 이것으로만 한다.
    /// 2026-10-21 사용자 확정: 공연은 1~3일만. 4일차는 아침 뒤 종료하므로 편성하지 않는다.
    /// - 1일차 밤에 2·3일차를 쓰고 확정하면 2일차가 잠긴다. 3일차는 2일차 밤에 다시 열려 고친 뒤 확정하면 잠긴다.
    /// - 그날 밤 확정 전에는 (호스트가) 준비 완료할 수 없다 — <see cref="IsConfirmedTonight"/>.
    /// - 같은 공연을 하루 2번 골라도 된다.
    /// - 난이도(M2): 칸마다 단계 1~3을 고른다. 그 묘기를 깬 단계의 다음 단계까지만 고를 수 있다(부르는 쪽이 최대 단계를 알려 준다).
    ///   1일차는 1단계 고정. 실제 공연 단계는 런 진행이 시작할 때 다시 허용 범위로 줄인다.
    ///   쉬는 시간에는 잠긴 칸이라도 바로 다음 공연의 단계만 다시 고칠 수 있다(<see cref="AdjustTier"/>).
    /// 세대 교체·런 재시작 때 <see cref="Clear"/>로 처음 상태로 돌린다.
    /// </summary>
    public sealed class ScheduleBook
    {
        public const int Days = 4; // 기존 네트워크 배열 길이 유지. 마지막 칸은 항상 빈 값.
        public const int PerformanceDays = 3;
        public const int SlotsPerDay = 2;
        public const int MaxDifficulty = 3;

        /// <summary>공연 편성이 없는 마지막 아침.</summary>
        public const int UnplayedDay = 4;

        readonly string[] firstDay;
        readonly HashSet<string> choices;
        readonly string[,] slots = new string[Days, SlotsPerDay];
        readonly int[,] tiers = new int[Days, SlotsPerDay];
        readonly bool[] locked = new bool[Days];
        readonly bool[,] chosen = new bool[Days, SlotsPerDay];
        // 첫날 둘째 공연(CC-215): 첫 공연 뒤 대기실 안내가 끝나면 호스트가 고르고 확정한다. 첫 공연 칸은 늘 고정.
        bool firstDayChoiceOpen, firstDayConfirmed;

        public ScheduleBook(IReadOnlyList<string> firstDayTricks, IEnumerable<string> selectableTricks)
        {
            if (firstDayTricks == null || firstDayTricks.Count != SlotsPerDay) throw new ArgumentException("1일차 공연은 2개여야 합니다.", nameof(firstDayTricks));
            firstDay = new[] { firstDayTricks[0] ?? "", firstDayTricks[1] ?? "" };
            choices = new HashSet<string>(selectableTricks ?? Array.Empty<string>());
            choices.Remove("");
            Clear();
        }

        /// <summary>지금 밤 정비 중인 일차(그 날 밤). 밤이 아니면 0.</summary>
        public int Night { get; private set; }

        /// <summary>이번 밤 확정을 마쳤는가.</summary>
        public bool IsConfirmedTonight { get; private set; }

        /// <summary>바뀔 때마다 오른다(동기화 비교용).</summary>
        public long Revision { get; private set; }

        public bool IsChoice(string trickId) => trickId != null && choices.Contains(trickId);

        /// <summary><paramref name="day"/>일차(1부터) <paramref name="slot"/>번째(0부터) 공연. 비었으면 빈 문자열.</summary>
        public string Get(int day, int slot) => InRange(day, slot) ? slots[day - 1, slot] : "";

        /// <summary>그 일차 공연 순서 사본.</summary>
        public string[] Setlist(int day)
        {
            var list = new string[SlotsPerDay];
            for (int i = 0; i < SlotsPerDay; i++) list[i] = Get(day, i);
            return list;
        }

        public bool IsLocked(int day) => day >= 1 && day <= Days && locked[day - 1];

        /// <summary>첫날 둘째 공연을 고를 수 있는 시간대인가(호스트가 런 상태로 켜고 끈다).</summary>
        public bool FirstDayChoiceOpen => firstDayChoiceOpen;
        /// <summary>첫날 둘째 공연을 확정했다.</summary>
        public bool FirstDayConfirmed => firstDayConfirmed;
        public bool IsFirstDaySecondEditable => firstDayChoiceOpen;

        public void SetFirstDayChoiceOpen(bool open)
        {
            if (firstDayChoiceOpen == open) return;
            firstDayChoiceOpen = open;
            Revision++;
        }

        /// <summary>이번 밤에 고칠 수 있는 일차인가(확정 전·잠기지 않음). 1일차는 둘째 공연 선택 시간대에만.</summary>
        public bool IsEditable(int day)
        {
            if (day == 1) return IsFirstDaySecondEditable;
            return day > 1 && day <= PerformanceDays;
        }

        /// <summary>1일차 밤 2·3, 2일차 밤 3. 이후는 편성 없음.</summary>
        public static int[] EditableDays(int night) => night switch
        {
            1 => new[] { 2, 3 },
            2 => new[] { 3 },
            _ => Array.Empty<int>(),
        };

        /// <summary>
        /// 확정하면 잠기는 일차. 1일차 밤엔 2일차만(3일차는 2일차 밤에 다시 연다), 그 뒤엔 그 밤에 쓴 일차.
        /// </summary>
        public static int LockedOnConfirm(int night) => night switch
        {
            1 => 2,
            2 => 3,
            _ => 0,
        };

        /// <summary>확정 때 실제 공연 순서로 넘길 일차(4일차는 실행하지 않아 뺀다).</summary>
        public static int[] DaysToApply(int night)
        {
            var list = new List<int>();
            foreach (int d in EditableDays(night))
                if (d < UnplayedDay) list.Add(d);
            return list.ToArray();
        }

        /// <summary><paramref name="day"/>일차 밤 정비를 시작한다. 같은 밤을 다시 알려도 확정 여부는 그대로 둔다.</summary>
        public void BeginNight(int day)
        {
            if (Night == day) return;
            Night = day;
            IsConfirmedTonight = false;
            Revision++;
        }

        /// <summary>밤 정비가 끝났다(다음 날 아침).</summary>
        public void EndNight()
        {
            if (Night == 0) return;
            Night = 0;
            IsConfirmedTonight = false;
            Revision++;
        }

        /// <summary>칸에 공연을 고른다. 단계는 1단계.</summary>
        public ScheduleRefusal Set(int day, int slot, string trickId) => Set(day, slot, trickId, 1, 1);

        /// <summary>
        /// 칸에 공연과 난이도 단계를 고른다. <paramref name="maxTier"/>는 그 묘기에 지금 고를 수 있는 가장 높은 단계(깬 단계 + 1).
        /// </summary>
        public ScheduleRefusal Set(int day, int slot, string trickId, int tier, int maxTier)
        {
            if (day == 1) return SetFirstDaySecond(slot, trickId);
            if (!InRange(day, slot)) return ScheduleRefusal.BadSlot;
            if (!IsEditable(day)) return ScheduleRefusal.NotEditable;
            if (string.IsNullOrEmpty(trickId) || !IsChoice(trickId)) return ScheduleRefusal.UnknownTrick;
            string value = trickId ?? "";
            if (value != "" && (tier < 1 || tier > Math.Min(MaxDifficulty, Math.Max(1, maxTier)))) return ScheduleRefusal.BadTier;

            int level = value == "" ? 1 : tier;
            chosen[day - 1, slot] = true;
            if (slots[day - 1, slot] == value && tiers[day - 1, slot] == level) return ScheduleRefusal.None;
            slots[day - 1, slot] = value;
            tiers[day - 1, slot] = level;
            Revision++;
            return ScheduleRefusal.None;
        }

        /// <summary>첫날 둘째 칸만 고른다(1단계 고정, 빈 칸 불가). 첫 공연 칸은 고정이다.</summary>
        ScheduleRefusal SetFirstDaySecond(int slot, string trickId)
        {
            if (slot != 1) return ScheduleRefusal.BadSlot;
            if (!firstDayChoiceOpen) return ScheduleRefusal.NotEditable;
            if (string.IsNullOrEmpty(trickId) || !IsChoice(trickId)) return ScheduleRefusal.UnknownTrick;
            chosen[0, 1] = true;
            if (slots[0, 1] == trickId) return ScheduleRefusal.None;
            slots[0, 1] = trickId;
            tiers[0, 1] = 1;
            Revision++;
            return ScheduleRefusal.None;
        }

        /// <summary>첫날 둘째 공연을 확정한다. 그 뒤에는 바꿀 수 없다.</summary>
        public ScheduleRefusal ConfirmFirstDay()
        {
            if (!firstDayChoiceOpen) return ScheduleRefusal.NotEditable;
            return ScheduleRefusal.None;
        }

        /// <summary>
        /// 쉬는 시간: 잠긴 칸이라도 단계만 바꾼다(묘기는 그대로). 바로 다음 공연인지는 부르는 쪽이 확인한다.
        /// </summary>
        public ScheduleRefusal AdjustTier(int day, int slot, int tier, int maxTier)
        {
            if (!InRange(day, slot) || day <= 1 || day > PerformanceDays) return ScheduleRefusal.BadSlot;
            if (string.IsNullOrEmpty(slots[day - 1, slot])) return ScheduleRefusal.NotEditable;
            if (tier < 1 || tier > Math.Min(MaxDifficulty, Math.Max(1, maxTier))) return ScheduleRefusal.BadTier;
            if (tiers[day - 1, slot] == tier) return ScheduleRefusal.None;
            tiers[day - 1, slot] = tier;
            Revision++;
            return ScheduleRefusal.None;
        }

        /// <summary>이번 밤 작성을 확정한다. 쓸 칸이 모두 차 있어야 하고, 확정하면 <see cref="LockedOnConfirm"/> 일차가 잠긴다.</summary>
        public ScheduleRefusal Confirm()
        {
            // 구 도구의 호출 호환성만 유지한다. 선택은 즉시 적용되며 확정·잠금 상태를 만들지 않는다.
            return ScheduleRefusal.None;
        }

        /// <summary>아직 직접 고르지 않은 미래 칸은 최근 해금·리허설을 마친 공연을 기본으로 쓴다.</summary>
        public bool FillUpcomingDefaults(string trickId, int day, int slot)
        {
            if (!IsChoice(trickId)) return false;
            bool changed = false;
            for (int d = Math.Max(1, day); d <= PerformanceDays; d++)
                for (int i = 0; i < SlotsPerDay; i++)
                {
                    if (d == 1 && i == 0 || d == day && i < slot || chosen[d - 1, i]) continue;
                    if (slots[d - 1, i] == trickId) continue;
                    slots[d - 1, i] = trickId; tiers[d - 1, i] = 1; changed = true;
                }
            if (changed) Revision++;
            return changed;
        }

        /// <summary>그 칸 공연의 고른 난이도 단계(1~3). 1일차는 1, 빈 칸은 0.</summary>
        public int Difficulty(int day, int slot)
        {
            if (string.IsNullOrEmpty(Get(day, slot))) return 0;
            return day <= 1 ? 1 : Math.Clamp(tiers[day - 1, slot], 1, MaxDifficulty);
        }

        /// <summary>그 일차 칸별 난이도 단계 사본(런 진행에 넘긴다).</summary>
        public int[] Tiers(int day)
        {
            var list = new int[SlotsPerDay];
            for (int i = 0; i < SlotsPerDay; i++) list[i] = Math.Max(1, Difficulty(day, i));
            return list;
        }

        /// <summary>동기화용: 칸을 일차·순서대로 펼친 사본(길이 Days × SlotsPerDay).</summary>
        public string[] ExportSlots()
        {
            var flat = new string[Days * SlotsPerDay];
            for (int d = 0; d < Days; d++)
                for (int i = 0; i < SlotsPerDay; i++) flat[d * SlotsPerDay + i] = slots[d, i];
            return flat;
        }

        /// <summary>동기화용: 칸별 난이도 단계 사본(길이 Days × SlotsPerDay).</summary>
        public int[] ExportTiers()
        {
            var flat = new int[Days * SlotsPerDay];
            for (int d = 0; d < Days; d++)
                for (int i = 0; i < SlotsPerDay; i++) flat[d * SlotsPerDay + i] = tiers[d, i];
            return flat;
        }

        /// <summary>동기화용: 잠김 사본(길이 Days).</summary>
        public bool[] ExportLocked() => (bool[])locked.Clone();

        /// <summary>
        /// 참가자: 호스트가 보낸 상태를 그대로 받는다. 1일차는 늘 고정 공연·잠김으로 둔다. 잘못된 길이면 받지 않는다.
        /// </summary>
        public bool Load(int night, bool confirmedTonight, IReadOnlyList<string> flatSlots, IReadOnlyList<bool> lockedDays)
            => Load(night, confirmedTonight, flatSlots, lockedDays, null);

        /// <summary>단계까지 받는다. <paramref name="flatTiers"/>가 없으면 모든 칸 1단계. 첫날 둘째 칸은 호스트가 고른 값을 받는다.</summary>
        public bool Load(int night, bool confirmedTonight, IReadOnlyList<string> flatSlots, IReadOnlyList<bool> lockedDays,
            IReadOnlyList<int> flatTiers, bool firstDayOpen = false, bool firstDayDone = false)
        {
            if (flatSlots == null || flatSlots.Count != Days * SlotsPerDay || lockedDays == null || lockedDays.Count != Days) return false;
            if (flatTiers != null && flatTiers.Count != Days * SlotsPerDay) return false;
            for (int d = 0; d < Days; d++)
            {
                for (int i = 0; i < SlotsPerDay; i++)
                {
                    string value = flatSlots[d * SlotsPerDay + i] ?? "";
                    slots[d, i] = d == 0 ? (i == 1 && IsChoice(value) ? value : firstDay[i]) : d >= PerformanceDays ? "" : (IsChoice(value) ? value : firstDay[0]);
                    tiers[d, i] = d == 0 || flatTiers == null ? 1 : Math.Clamp(flatTiers[d * SlotsPerDay + i], 1, MaxDifficulty);
                }
                locked[d] = false;
            }
            Night = night >= 0 && night <= Days ? night : 0;
            IsConfirmedTonight = false;
            firstDayChoiceOpen = firstDayOpen;
            firstDayConfirmed = false;
            Revision++;
            return true;
        }

        /// <summary>처음 상태: 1일차 고정·잠김, 나머지 빈 칸, 밤 아님.</summary>
        public void Clear()
        {
            for (int d = 0; d < Days; d++)
            {
                for (int i = 0; i < SlotsPerDay; i++)
                {
                    slots[d, i] = d == 0 ? firstDay[i] : d < PerformanceDays ? firstDay[0] : "";
                    tiers[d, i] = 1;
                    chosen[d, i] = false;
                }
                locked[d] = false;
            }
            Night = 0;
            IsConfirmedTonight = false;
            firstDayChoiceOpen = false;
            firstDayConfirmed = false;
            Revision++;
        }

        static bool InRange(int day, int slot) => day >= 1 && day <= Days && slot >= 0 && slot < SlotsPerDay;
    }
}
