namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 끈끈이 장갑(기물 시스템 31784961, Task-092) 공통 규칙. 판정 화면(호스트)이 매달림 장치마다 부른다.
    /// 2026-11-01 사용자: 제한 시간이 있는 매달림 전부(외줄 추락 매달림, 트라페즈·해제봉, 인간 대포 톱날 연동 봉)를 1.5배(초안)로 늘린다.
    /// - 매달림 시작 때 장갑이 있고(남은 횟수, 쿨타임 아님) 기본 제한이 있으면 추가 시간을 준다(<see cref="ExtraSeconds"/>).
    /// - 기본 시간을 다 써서 추가 시간에 들어서면 발동 문구를 띄운다(<see cref="Cue"/>).
    /// - 그 매달림이 끝나면(구조·낙하·놓기·사망) 추가 시간에 들어섰던 때만 1 소모하고 쿨타임(초안 3초)을 시작한다(<see cref="Finish"/>).
    ///   마지막 회차도 그 매달림이 끝난 뒤 파괴된다. 기본 시간 안에 끝나면 소모하지 않는다.
    /// - 동료가 끌어올리는 동안에는 기존처럼 버티는 시간이 멈춘다(추가 시간도 같이 멈춤).
    /// </summary>
    public static class StickyGloves
    {
        /// <summary>판정 화면: 이번 매달림에 더해 줄 시간(초). 장갑이 없거나 다 썼거나 쿨타임이면 0.</summary>
        public static double ExtraSeconds(ulong player, double baseSeconds)
        {
            var carry = PropCarry.Instance;
            if (carry == null || !carry.HasAuthority || baseSeconds <= 0d || !carry.EquipmentReady(player, ItemUses.StickyGloves)) return 0d;
            float scale = ItemCatalog.Get(ItemUses.StickyGloves)?.effect ?? 1.5f;
            return scale > 1f ? baseSeconds * (scale - 1f) : 0d;
        }

        /// <summary>판정 화면: 기본 시간을 넘겨 추가 시간에 들어섰다. 주인 화면에 발동 문구만 띄운다.</summary>
        public static void Cue(ulong player) => PropCarry.Instance?.ShowEquipmentCue(player, ItemUses.StickyGloves);

        /// <summary>판정 화면: 매달림이 끝났다. 추가 시간에 들어섰던 매달림이면 1 소모·쿨타임 시작.</summary>
        public static void Finish(ulong player, bool extraUsed)
        {
            if (extraUsed) PropCarry.Instance?.UseEquipment(player, ItemUses.StickyGloves, cue: false);
        }
    }

    /// <summary>
    /// 잡기 제한 시간이 있는 장치(트라페즈·해제봉·인간 대포 봉)의 끈끈이 장갑 추적(판정 화면 전용).
    /// 잡을 때 <see cref="Begin"/>으로 추가 시간을 받아 장치의 제한 시간에 더하고, 매 프레임 <see cref="Tick"/>에 "아직 잡고 있는지"를 넘긴다.
    /// 놓는 경로(스스로 놓기·만료·사망·라운드 정리)를 장치마다 따로 고치지 않고, 잡고 있지 않게 된 순간을 매달림 종료로 본다.
    /// </summary>
    public sealed class StickyGloveHolds
    {
        sealed class Entry { public double BaseEnd; public bool Cued; }

        readonly System.Collections.Generic.Dictionary<ulong, Entry> entries = new();
        readonly System.Collections.Generic.List<ulong> ended = new();

        /// <summary>잡은 순간. 더할 추가 시간(초)을 돌려준다. 장갑이 없거나 제한이 없으면 0.</summary>
        public double Begin(ulong actor, double now, double baseSeconds)
        {
            entries.Remove(actor);
            double extra = StickyGloves.ExtraSeconds(actor, baseSeconds);
            if (extra > 0d) entries[actor] = new Entry { BaseEnd = now + baseSeconds };
            return extra;
        }

        /// <summary>매 프레임. 기본 시간을 넘기면 발동 문구, 잡고 있지 않게 되면 매달림 종료로 처리한다.</summary>
        public void Tick(double now, System.Func<ulong, bool> stillHolding)
        {
            if (entries.Count == 0) return;
            ended.Clear();
            foreach (var pair in entries)
            {
                if (stillHolding == null || !stillHolding(pair.Key)) { ended.Add(pair.Key); continue; }
                if (!pair.Value.Cued && now >= pair.Value.BaseEnd) { pair.Value.Cued = true; StickyGloves.Cue(pair.Key); }
            }
            foreach (ulong actor in ended)
            {
                StickyGloves.Finish(actor, now >= entries[actor].BaseEnd);
                entries.Remove(actor);
            }
        }

        /// <summary>라운드 정리 등으로 상태를 버린다(소모 없음).</summary>
        public void Clear() => entries.Clear();
    }
}
