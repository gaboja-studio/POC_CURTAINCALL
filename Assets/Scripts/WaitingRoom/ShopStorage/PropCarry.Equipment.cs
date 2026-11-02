using System;
using System.Collections.Generic;
using UnityEngine;
using CurtainCall.Network.Session;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 공연용 장비(구조 장갑·끈끈이 장갑·광대 벌룬 바지·스프링 신발, Task-092) 공통 판정.
    /// - 장비는 1·2로 고르지 않아도 슬롯에 있으면 자동 적용한다(<see cref="Equipped"/>).
    /// - 실제 효과가 났을 때만 판정 화면이 <see cref="UseEquipment"/>로 1 소모한다. 0회인 장비는 새 효과를 내지 못한다.
    /// - 쿨타임은 기물 목록(ItemCatalog.cooldown)을 쓰고, 소지 상태와 함께 모든 화면에 공유한다.
    ///   2026-11-02 사용자: 공연 기물 쿨타임은 모두 0(없음). 값을 다시 넣으면 같은 경로로 되살아난다.
    ///   효과를 소유자 화면이 계산하는 장비(벌룬 바지·구조 장갑의 착지 완화)도 같은 값으로 미리 판단한다.
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
        {
            if (!HasAuthority || !EquipmentReady(player, itemId)) return false;
            var item = Equipped(player, itemId);
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
