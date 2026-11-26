using System;
using System.Collections.Generic;
using UnityEngine;
using CurtainCall.Network.Session;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 공연용 장비(구조 장갑·끈끈이 장갑·광대 벌룬 바지·스프링 신발, Task-092) 공통 판정.
    /// - 공연 장비는 Q/E 슬롯 입력으로 수동 발동한다. Equipped는 소지 여부 조회이며 자동 발동하지 않는다.
    /// - 실제 효과가 났을 때만 판정 화면이 <see cref="UseEquipment"/>로 1 소모한다. 0회인 장비는 새 효과를 내지 못한다.
    /// - 쿨타임은 기물 목록(ItemCatalog.cooldown)을 쓰고, 소지 상태와 함께 모든 화면에 공유한다.
    ///   2026-11-02 사용자: 공연 기물 쿨타임은 모두 0(없음). 값을 다시 넣으면 같은 경로로 되살아난다.
    ///   공중 효과는 NetworkPlayer의 소유자 적용 확인 뒤 정확한 UID를 소모한다.
    /// - 발동 문구·파괴 알림은 장비 주인 화면에만 띄운다.
    /// </summary>
    public sealed partial class PropCarry
    {
        /// <summary>(플레이어, 기물 ID) → 공유 서버 시각 기준 쿨타임 종료.</summary>
        readonly Dictionary<(ulong player, string item), double> cooldownUntil = new();
        readonly Queue<RequestData> equipmentReports = new();
        readonly Dictionary<ulong, (int generation, uint sequence)> equipmentReceipts = new();
        uint equipmentSequence;
        double equipmentRetryAt;
        int EquipmentGeneration => CurtainCall.Campaign.CampaignRun.Instance != null
            ? CurtainCall.Campaign.CampaignRun.Instance.State.Generation : 0;
        double EquipmentClock => NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.NetworkManager != null
            && NetworkSessionManager.Instance.NetworkManager.IsListening
            ? NetworkSessionManager.Instance.NetworkManager.ServerTime.Time : Time.unscaledTimeAsDouble;

        /// <summary>내 화면에 장비 발동·파괴 알림이 떴다(기물 이름, 문구, 파괴 여부). 테스트·연출 확인용.</summary>
        public event Action<string, string, bool> LocalItemNotice;

        /// <summary>공연 기물을 쓰는 장소 규칙(2026-11-18 사용자).</summary>
        public enum EquipmentPlace
        {
            /// <summary>공연·탈출로 등 대기실 밖: 정상 발동·내구도 소모.</summary>
            Normal,
            /// <summary>대기실 연습실 구역: 발동하지만 내구도를 쓰지 않는다.</summary>
            Practice,
            /// <summary>그 밖의 대기실 구역: 쓸 수 없다.</summary>
            Blocked,
        }

        static bool WaitingRoomScene => CurtainCall.Campaign.CampaignRun.Instance?.State.Phase is not (CurtainCall.Campaign.CampaignPhase.Escape or CurtainCall.Campaign.CampaignPhase.Performance)
            && CurtainCall.Settings.WaitingRoomScenes.IsWaitingRoom(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // 2026-11-26 M3 피드백: 런 성공 뒤 로비(시작 씬)로 돌아가면 대기실 밖이라 공연 기물이 그대로 발동됐다.
        // 시작 씬(Hub)이 떠 있고 공연·대기실·탈출로 같은 공간 씬이 없으면 막는다. 장소 지정이 없는 개발 확인 씬은 그대로 둔다.
        internal static bool HubScene
        {
            get
            {
                var settings = CurtainCall.Settings.GameSettings.Base.SpaceTransition;
                var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!CurtainCall.SpaceLink.SpaceRoute.TryFind(active.name, settings, out var space) || space != CurtainCall.SpaceLink.SpaceId.Hub)
                    return false;
                var director = CurtainCall.SpaceLink.SpaceLinkDirector.Instance;
                return director == null || director.Current == CurtainCall.SpaceLink.SpaceId.Hub;
            }
        }

        /// <summary>발 위치 기준 공연 기물 장소. 대기실이면 지역명 "연습실" 구역만 연습으로 허용한다. 로비(시작 씬)에서는 쓸 수 없다.</summary>
        public static EquipmentPlace PlaceAt(Vector3 feet) =>
            !WaitingRoomScene ? (HubScene ? EquipmentPlace.Blocked : EquipmentPlace.Normal)
                : CurtainCall.Campaign.CampaignRun.RehearsalPendingOrActive || ExplorationTimerZones.IsPracticeRoom(feet)
                    ? EquipmentPlace.Practice : EquipmentPlace.Blocked;

        /// <summary>이 플레이어가 지금 공연 기물을 쓰는 장소. 대기실에서 캐릭터를 못 찾으면 막는다.</summary>
        public static EquipmentPlace PlaceOf(ulong player)
        {
            if (!WaitingRoomScene) return HubScene ? EquipmentPlace.Blocked : EquipmentPlace.Normal;
            var body = FindBody(player);
            return body != null ? PlaceAt(body.position) : EquipmentPlace.Blocked;
        }

        static float lastPlaceGuide = -10f;

        /// <summary>내 화면에만: 공연 기물은 연습실이나 공연장에서 쓸 수 있다고 안내한다(연타해도 1.5초에 한 번).</summary>
        public static void ShowEquipmentPlaceGuide(Transform owner)
        {
            if (Time.unscaledTime - lastPlaceGuide < 1.5f) return;
            lastPlaceGuide = Time.unscaledTime;
            var profile = WaitingRoomHudProfile.Current;
            WaitingRoomHudProfile.Emit(profile != null ? profile.equipmentPlaceBlocked : RoomNotice.Feedback, owner,
                WaitingRoomText.Get("equipment.place-blocked.title", "여기서는 쓸 수 없습니다"),
                WaitingRoomText.Get("equipment.place-blocked.body", "공연 기물은 연습실이나 공연장에서 사용할 수 있습니다."), 3f);
        }

        static bool IsPerformanceItem(string itemId) => ItemCatalog.Get(itemId)?.use == ItemUse.Performance;

        /// <summary>남은 쿨타임(초). 없으면 0.</summary>
        public float CooldownLeft(ulong player, string itemId) =>
            cooldownUntil.TryGetValue((player, itemId), out double until) ? (float)Math.Max(0, until - EquipmentClock) : 0f;

        /// <summary>모든 화면: 이 장비를 지녔고(남은 횟수 있음) 쿨타임이 아니라 새 효과를 낼 수 있는가.</summary>
        public bool EquipmentReady(ulong player, string itemId) =>
            Equipped(player, itemId).Uid != 0 && CooldownLeft(player, itemId) <= 0f
                && (player != LocalClientId || !EquipmentReportPending(itemId));

        bool EquipmentReportPending(string itemId)
        {
            foreach (var report in equipmentReports)
                if (report.item == itemId && report.generation == EquipmentGeneration) return true;
            return false;
        }

        /// <summary>
        /// 판정 화면: 장비 효과가 실제로 났다. 1 소모하고, <paramref name="startCooldown"/>이면 쿨타임을 시작한다.
        /// <paramref name="cue"/>가 있으면 주인 화면에 발동 문구를 띄우고, 마지막 사용이면 파괴 알림을 띄운다.
        /// 지니지 않았거나 다 썼거나 쿨타임 중이면 아무것도 바꾸지 않고 false.
        /// </summary>
        public bool UseEquipment(ulong player, string itemId, bool cue = true, bool startCooldown = true)
            => UseEquipmentUid(player, Equipped(player,itemId).Uid, cue, startCooldown);

        public bool UseEquipmentUid(ulong player, int uid, bool cue = true, bool startCooldown = true)
        {
            PropItem item = default;
            foreach(var held in Held(player)) if(held.Uid==uid) { item=held; break; }
            if(item.Uid==0 || item.Uses<=0 || ItemCatalog.Get(item.ItemId)?.kind != ItemKind.Equipment) return false;
            string itemId=item.ItemId;
            if (!HasAuthority || !EquipmentReady(player, itemId)) return false;
            if (IsPerformanceItem(itemId))
            {
                // 대기실: 연습실에서는 내구도 없이 발동만 하고, 다른 구역에서는 발동하지 않는다. 공연·탈출로는 아래 정상 소모.
                var place = PlaceOf(player);
                if (place == EquipmentPlace.Blocked) return false;
                if (place == EquipmentPlace.Practice)
                {
                    Debug.Log($"[Carry] 연습 발동 {ItemCatalog.DisplayName(item)} (내구도 유지) — {WaitingRoomShop.PlayerLabel(player)}");
                    return true;
                }
            }
            if (book.Use(player, item.Uid, out var after, out bool destroyed) != CarryRefusal.None) return false;
            MarkUsed(player, item.Uid, destroyed);
            if (startCooldown) SetCooldown(player, itemId);
            string name = ItemCatalog.DisplayName(destroyed ? item : after);
            Debug.Log($"[Carry] 장비 발동 {name} 남은 {(destroyed ? 0 : after.Uses)}{(destroyed ? " — 다 써서 사라짐" : "")} — {WaitingRoomShop.PlayerLabel(player)}");
            Publish();
            SendItemNotice(player, name, cue ? itemId : "", destroyed);
            return true;
        }

        /// <summary>판정 화면: 효과가 끝난 시점부터 세는 쿨타임(끈끈이 장갑·벌룬 바지)을 시작한다.</summary>
        public void StartCooldown(ulong player, string itemId)
        {
            if (!HasAuthority) return;
            SetCooldown(player, itemId);
            Publish();
        }

        /// <summary>판정 화면: 소모 없이 발동 문구만 주인 화면에 띄운다(끈끈이 장갑이 기본 시간을 넘긴 순간 등).</summary>
        public void ShowEquipmentCue(ulong player, string itemId)
        {
            if (!HasAuthority) return;
            var item = Equipped(player, itemId);
            SendItemNotice(player, ItemCatalog.DisplayName(item.Uid != 0 ? item : new PropItem(0, itemId, itemId)), itemId, false);
        }

        /// <summary>
        /// 내 화면이 계산한 장비 효과가 끝났다고 판정 화면에 알린다(벌룬 바지: 소유자 화면이 비행을 계산한다).
        /// 판정 화면이 1 소모하고 쿨타임을 시작한다. 마지막 회차는 이때 파괴된다(효과가 끝난 뒤 파괴).
        /// </summary>
        public void ReportEquipmentUsed(string itemId)
        {
            if (HasAuthority) HandleEquipmentReport(LocalClientId, itemId);
            else if (itemId == ItemUses.BalloonPants)
                equipmentReports.Enqueue(new RequestData { kind = EquipmentRequest, item = itemId,
                    generation = EquipmentGeneration, sequence = ++equipmentSequence });
        }

        RequestData NextEquipmentReport(double now)
        {
            while (equipmentReports.Count > 0 && equipmentReports.Peek().generation != EquipmentGeneration) equipmentReports.Dequeue();
            if (equipmentReports.Count == 0 || now < equipmentRetryAt) return null;
            equipmentRetryAt = now + .25;
            return equipmentReports.Peek();
        }

        void UpdateEquipmentReports()
        {
            if (HasAuthority || channel == null || !channel.IsClient) return;
            var report = NextEquipmentReport(Time.unscaledTimeAsDouble);
            if (report != null) channel.Request(JsonUtility.ToJson(report));
        }

        void AcknowledgeEquipmentReport(uint sequence, int generation)
        {
            if (equipmentReports.Count == 0 || equipmentReports.Peek().sequence != sequence || equipmentReports.Peek().generation != generation) return;
            equipmentReports.Dequeue(); equipmentRetryAt = 0;
        }

        bool AcceptEquipmentReport(ulong player, uint sequence, int generation)
        {
            if (sequence == 0 || generation != EquipmentGeneration
                || equipmentReceipts.TryGetValue(player, out var previous) && previous.generation == generation && sequence <= previous.sequence) return false;
            equipmentReceipts[player] = (generation, sequence);
            return true;
        }

        void HandleEquipmentReportRequest(ulong player, RequestData report)
        {
            if (report.item != ItemUses.BalloonPants || report.sequence == 0) return;
            WatchGeneration();
            if (AcceptEquipmentReport(player, report.sequence, report.generation)) HandleEquipmentReport(player, report.item);
            // A retried report is acknowledged again, but never spends a second charge.
            channel.SendResult(player, JsonUtility.ToJson(new ResultData { equipmentAck = report.sequence, equipmentGeneration = report.generation }));
        }

        /// <summary>내 화면에만 장비 발동 문구를 띄운다(소유자 화면이 효과를 계산하는 장비).</summary>
        public void ShowLocalEquipmentCue(string itemId)
        {
            var item = Equipped(LocalClientId, itemId);
            ShowItemNotice(ItemCatalog.DisplayName(item.Uid != 0 ? item : new PropItem(0, itemId, itemId)), itemId, false);
        }

        void HandleEquipmentReport(ulong player, string itemId)
        {
            // 소유자 화면이 효과를 계산하는 장비만 받는다. 나머지는 판정 화면이 직접 판정한다.
            if (itemId != ItemUses.BalloonPants) return;
            UseEquipment(player, itemId, cue: false);
        }

        void SetCooldown(ulong player, string itemId)
        {
            float seconds = ItemCatalog.Get(itemId)?.cooldown ?? 0f;
            if (seconds > 0f) cooldownUntil[(player, itemId)] = EquipmentClock + seconds;
            else cooldownUntil.Remove((player, itemId));
        }

        CooldownData[] CooldownsOf(ulong player)
        {
            List<CooldownData> list = null;
            foreach (var pair in cooldownUntil)
            {
                if (pair.Key.player != player) continue;
                double left = pair.Value - EquipmentClock;
                if (left <= 0f) continue;
                (list ??= new List<CooldownData>()).Add(new CooldownData { id = pair.Key.item, until = pair.Value });
            }
            return list != null ? list.ToArray() : Array.Empty<CooldownData>();
        }

        void ReadCooldowns(PlayerData[] players)
        {
            cooldownUntil.Clear();
            if (players == null) return;
            foreach (var p in players)
                foreach (var c in p.cooldowns ?? Array.Empty<CooldownData>())
                    if (!string.IsNullOrEmpty(c.id) && !double.IsNaN(c.until) && !double.IsInfinity(c.until)
                        && c.until > EquipmentClock) cooldownUntil[(p.client, c.id)] = c.until;
        }

        void SendItemNotice(ulong player, string itemName, string cue, bool broken)
        {
            if (string.IsNullOrEmpty(cue) && !broken) return;
            if (player == LocalClientId || !channel.IsHost) { ShowItemNotice(itemName, cue, broken); return; }
            channel.SendResult(player, JsonUtility.ToJson(new ResultData { ok = true, noticeItem = itemName, noticeCue = cue, noticeBroken = broken }));
        }

        void ShowItemNotice(string itemName, string cue, bool broken)
        {
            if (!string.IsNullOrEmpty(cue))
            {
                string line = ItemCatalog.UseCue(cue); // 기물 목록의 발동 문구(useCue)
                if (!string.IsNullOrEmpty(line)) WaitingRoomItemBrokenNotice.ShowMessage(itemName, line);
            }
            // 파괴 알림이 발동 문구보다 중요하므로 뒤에 띄워 남긴다.
            if (broken) WaitingRoomItemBrokenNotice.Show(itemName);
            if (LocalItemNotice != null)
                foreach (Action<string, string, bool> observer in LocalItemNotice.GetInvocationList())
                    try { observer(itemName, cue, broken); }
                    catch (Exception error) { Debug.LogException(error, this); }
        }
    }
}
