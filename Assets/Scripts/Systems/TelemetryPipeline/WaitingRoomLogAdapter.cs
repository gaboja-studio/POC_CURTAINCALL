using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Survival;
using CurtainCall.WaitingRoom;
using CurtainCall.UI.ShopV1;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Telemetry.Pipeline
{
    sealed class WaitingRoomLogAdapter : IDisposable
    {
        readonly TelemetryRuntime log;
        readonly HashSet<int> orders = new(), receipts = new();
        readonly HashSet<string> deliveryStages = new();
        WaitingRoomShop shop;
        PropCarry carry;
        readonly CarryLogTracker carryTracker = new();
        readonly StorageArrivalTracker storageArrivals = new();
        readonly WaitingRoomTiming timing = new();
        NetworkPlayer placedPlayer;
        double? placedAt, medicalEnteredAt, movedInsideAt, movedOutsideAt;
        Vector3 movedInsidePosition, movedOutsidePosition;
        bool medicalReturned;
        string verifyFlow;
        int verifyAfterFrame;
        CampaignSnapshot breakStep;
        string breakScope;
        string closedBreakScope;
        CameraViewZone[] roomVolumes = Array.Empty<CameraViewZone>();
        WaitingRoomZoneMap.Region region;
        ExpertService medical;
        ScheduleBoard schedule;
        CampaignRun campaign;
        ShopPhone phone;
        ScheduleBoardView scheduleView;
        WaitingRoomShopUI shopView;
        ExpertRoomView medicalRoom;
        StageEntranceGather gather;
        WaitingRoomExitWatch exit;
        string zone, shopFlow, scheduleFlow, medicalFlow, serviceScreenFlow, serviceEntry, scope;
        bool shopOpen, scheduleOpen, entrance, serviceOpen, medicalReached;
        float zoneAt, nextFind;
        int medicalBefore;
        public WaitingRoomLogAdapter(TelemetryRuntime runtime)
        {
            log = runtime;
            ShopDeliverySlot.DeliveryStageChanged += DeliveryStage;
            WaitingRoomSpawn.PlayerPlaced += Placed;
            ExpertRoomView.LocalMoved += MedicalMoved;
        }
        bool InWaitingRoom => SceneManager.GetActiveScene().name == "WaitingRoomBase";
        public void Tick()
        {
            if (scope != log.CurrentRoomId) { scope = log.CurrentRoomId; orders.Clear(); receipts.Clear(); deliveryStages.Clear(); carryTracker.Reset(); storageArrivals.Reset(); medicalFlow = null; timing.Reset(); breakScope = closedBreakScope = null; }
            Bind();
            if (campaign != null) Step(campaign.State);
            if (Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 0.5f;
                phone = UnityEngine.Object.FindAnyObjectByType<ShopPhone>(); scheduleView = UnityEngine.Object.FindAnyObjectByType<ScheduleBoardView>();
                shopView = UnityEngine.Object.FindAnyObjectByType<WaitingRoomShopUI>(); medicalRoom = UnityEngine.Object.FindAnyObjectByType<ExpertRoomView>();
                roomVolumes = InWaitingRoom ? UnityEngine.Object.FindObjectsByType<CameraViewZone>(FindObjectsSortMode.None)
                    .Where(v => v.gameObject.scene == SceneManager.GetActiveScene()).ToArray() : Array.Empty<CameraViewZone>();
                var g = UnityEngine.Object.FindAnyObjectByType<StageEntranceGather>();
                if (gather != g) { if (gather != null) gather.Gathered -= Gathered; gather = g; if (gather != null) gather.Gathered += Gathered; }
                var e = UnityEngine.Object.FindAnyObjectByType<WaitingRoomExitWatch>();
                if (exit != e) { if (exit != null) exit.PlayerLeft -= FirstLeft; exit = e; if (exit != null) exit.PlayerLeft += FirstLeft; }
            }
            bool newShop = InWaitingRoom && phone != null && phone.IsOpen;
            if (newShop && !shopOpen)
            {
                shopFlow = Guid.NewGuid().ToString("D"); log.Emit("SHOP_OPENED", new() { ["flowId"] = shopFlow });
            }
            shopOpen = newShop;
            string selectedService = shopView != null && shopView.View != null ? shopView.View.SelectedEntryId : null;
            bool newService = InWaitingRoom && shopView != null && shopView.View != null && shopView.View.IsVisible
                && shopView.View.CurrentTab == ShopTab.Services && selectedService is "medical" or "facility";
            if (newService && (!serviceOpen || serviceEntry != selectedService))
            { serviceScreenFlow = Guid.NewGuid().ToString("D"); log.Emit("SERVICE_OPENED", new() { ["flowId"] = serviceScreenFlow,
                ["serviceType"] = selectedService == "facility" ? "FACILITY" : "RESTORE_BODY" }); }
            serviceEntry = newService ? selectedService : null;
            serviceOpen = newService;
            bool newSchedule = InWaitingRoom && scheduleView != null && scheduleView.IsOpen;
            if (newSchedule && !scheduleOpen) { scheduleFlow = Guid.NewGuid().ToString("D"); log.Emit("SCHEDULE_OPENED", new() { ["flowId"] = scheduleFlow }); }
            scheduleOpen = newSchedule;
            var local = NetworkPlayer.Local;
            if (placedAt.HasValue && local == placedPlayer && local != null && InWaitingRoom)
            { timing.Spawn(placedAt.Value); placedAt = null; }
            MedicalMoves();
            VerifyRestoration();
            if (medicalFlow != null && !medicalReached && local != null && medicalRoom != null && medicalRoom.InReach(local.transform.position))
            { medicalReached = true; log.Emit("MEDICAL_ROOM_REACHED", new() { ["flowId"] = medicalFlow, ["arrivalBoundary"] = "CURTAIN_INTERACTION_REACH" }); }
            bool atEntrance = InWaitingRoom && local != null && gather != null
                && gather.TryGetComponent(out BoxCollider area) && area.bounds.Contains(local.transform.position + Vector3.up * 0.5f);
            if (atEntrance && !entrance) log.EmitFor(local, "STAGE_ENTRANCE_REACHED", new() {
                ["readyToEntranceMs"] = timing.Entrance(Time.unscaledTimeAsDouble), ["position"] = DetailLogIds.Position(local.transform.position) },
                "LOCAL", breakScope != null ? breakStep : campaign != null ? campaign.State : null);
            entrance = atEntrance;
            region = Region(local);
            string nextZone = InWaitingRoom && local != null ? (atEntrance ? "STAGE_ENTRANCE" : region.Type) : null;
            if (zone != nextZone)
            {
                CloseZone("MOVED"); zone = nextZone; zoneAt = Time.unscaledTime;
                if (zone != null) log.Emit("ZONE_ENTERED", new() { ["zoneType"] = zone,
                    ["boundarySource"] = atEntrance ? "STAGE_ENTRANCE_GATHER" : region.Source,
                    ["zoneId"] = DetailLogIds.ObjectId(atEntrance ? (Component)gather : region.Boundary, "IN_"),
                    ["spawnToZoneMs"] = timing.FirstArrival(zone, Time.unscaledTimeAsDouble), ["spawnSource"] = "PLAYER_PLACED",
                    ["position"] = DetailLogIds.Position(local.transform.position) });
                log.Flush();
            }
            if (local != null && carry != null) StorageReached(local, carry.Held(local.OwnerClientId)
                .Select(i => new CarryLogTracker.Item(i.Uid, i.ItemId, "HELD", local.OwnerClientId)));
        }
        WaitingRoomZoneMap.Region Region(NetworkPlayer local) => InWaitingRoom && local != null
            ? WaitingRoomZoneMap.Find(roomVolumes, local.transform.position) : default;
        void StorageReached(NetworkPlayer local, IEnumerable<CarryLogTracker.Item> held)
        {
            if (!InWaitingRoom) return;
            var room = Region(local);
            foreach (var arrival in storageArrivals.Observe(room.Type, held, Time.unscaledTimeAsDouble))
                log.Emit("STORAGE_REACHED", new() { ["flowId"] = OrderFlow(arrival.Uid), ["itemUid"] = arrival.Uid,
                    ["itemType"] = arrival.Type, ["durationMs"] = arrival.DurationMs, ["acquisitionSource"] = arrival.Source,
                    ["boundarySource"] = room.Source, ["zoneId"] = DetailLogIds.ObjectId(room.Boundary, "IN_"),
                    ["position"] = DetailLogIds.Position(local.transform.position) });
        }
        void Bind()
        {
            if (shop != WaitingRoomShop.Instance)
            {
                if (shop != null) { shop.Changed -= ShopChanged; shop.OperationRejected -= ShopRejected; }
                shop = WaitingRoomShop.Instance;
                if (shop != null)
                {
                    foreach (var delivery in shop.Waiting) orders.Add(delivery.Order);
                    foreach (var receipt in shop.Receipts) receipts.Add(receipt.Delivery.Order);
                    shop.Changed += ShopChanged; shop.OperationRejected += ShopRejected;
                }
            }
            if (medical != ExpertService.Instance)
            {
                if (medical != null) { medical.PhaseChanged -= MedicalPhase; medical.OperationRejected -= ServiceRejected; medical.FacilityReserved -= FacilityReserved; }
                medical = ExpertService.Instance;
                if (medical != null) { medical.PhaseChanged += MedicalPhase; medical.OperationRejected += ServiceRejected; medical.FacilityReserved += FacilityReserved; }
            }
            if (schedule != ScheduleBoard.Instance) { if (schedule != null) schedule.Confirmed -= Confirmed; schedule = ScheduleBoard.Instance; if (schedule != null) schedule.Confirmed += Confirmed; }
            if (carry != PropCarry.Instance)
            {
                if (carry != null) { carry.Changed -= CarryChanged; carry.OperationRejected -= CarryRejected; }
                carry = PropCarry.Instance; carryTracker.Reset(); storageArrivals.Reset();
                if (carry != null) { CarryChanged(); carry.Changed += CarryChanged; carry.OperationRejected += CarryRejected; }
            }
            if (campaign != CampaignRun.Instance)
            {
                UnbindCampaign(); campaign = CampaignRun.Instance;
                if (campaign != null) { campaign.RecallAlarm += Alarm; campaign.RestExpired += Expired; campaign.LocalReadyChanged += Ready; campaign.StepChanged += Step; }
            }
        }
        string OrderFlow(int order) => DetailLogIds.Uuid((log.CurrentRoomId ?? log.CurrentRunId ?? log.FallbackPlayerId) + ":order:" + order);
        void CarryChanged()
        {
            if (carry == null) return;
            var snapshot = new List<CarryLogTracker.Item>();
            foreach (var player in NetworkPlayer.All)
                foreach (var item in carry.Held(player.OwnerClientId)) snapshot.Add(new(item.Uid, item.ItemId, "HELD", player.OwnerClientId));
            foreach (var item in carry.Floor) snapshot.Add(new(item.Item.Uid, item.Item.ItemId, "FLOOR"));
            foreach (var item in carry.Shelf) if (item.Uid > 0) snapshot.Add(new(item.Uid, item.ItemId, "SHELF"));
            var local = NetworkPlayer.Local;
            foreach (var change in carryTracker.Observe(snapshot))
            {
                if (local == null) continue;
                bool acquired = change.After.Location == "HELD" && change.After.Owner == local.OwnerClientId;
                if (acquired) storageArrivals.Acquired(change.After.Uid, change.After.Type, change.Event, Time.unscaledTimeAsDouble);
                if (change.Before.Location == "HELD" && change.Before.Owner == local.OwnerClientId)
                {
                    if (change.Event == "ITEM_STORED")
                    {
                        StorageReached(local, new[] { change.Before });
                        storageArrivals.Forget(change.Before.Uid);
                    }
                    else if (change.Event == "ITEM_HANDED_OVER") storageArrivals.Forget(change.Before.Uid);
                }
                ulong actor = change.Event is "ITEM_DROPPED" or "ITEM_STORED" or "ITEM_HANDED_OVER" ? change.Before.Owner : change.After.Owner;
                if (actor != local.OwnerClientId) continue;
                var payload = new Dictionary<string, object> { ["flowId"] = OrderFlow(change.After.Uid), ["itemType"] = change.After.Type, ["quantity"] = 1,
                    ["transitionSource"] = "CARRY_SNAPSHOT", ["previousLocation"] = change.Before.Location };
                if (change.Event == "ITEM_HANDED_OVER") payload["toPlayerId"] = log.PlayerCode(change.After.Owner);
                log.Emit(change.Event, payload);
            }
        }
        void ShopChanged()
        {
            if (shop == null) return;
            foreach (var delivery in shop.Waiting)
            {
                if (!orders.Add(delivery.Order) || !shop.HasAuthority) continue;
                var actor = Find(delivery.Buyer); var item = shop.Catalog.FirstOrDefault(i => i.Id == delivery.ItemId);
                if (actor == null || item.Price <= 0) continue;
                string flow = OrderFlow(delivery.Order);
                log.EmitFor(actor, "PURCHASE_COMPLETED", new() { ["flowId"] = flow, ["screenFlowId"] = actor.IsLocal && shopOpen ? shopFlow : null,
                    ["itemType"] = delivery.ItemId, ["quantity"] = 1, ["balanceBefore"] = shop.Balance + item.Price, ["balanceAfter"] = shop.Balance }, "HOST");
            }
            foreach (var receipt in shop.Receipts)
            {
                if (!receipts.Add(receipt.Delivery.Order)) continue;
                var local = NetworkPlayer.Local;
                bool localCollector = local != null && receipt.Claimer == local.OwnerClientId;
                if (!localCollector && !shop.HasAuthority) continue;
                var actor = localCollector ? local : Find(receipt.Claimer);
                if (actor == null) continue; // Never attribute an unavailable collector to the host.
                if (localCollector)
                    storageArrivals.Acquired(receipt.Delivery.Order, receipt.Delivery.ItemId, "ITEM_COLLECTED", Time.unscaledTimeAsDouble);
                // The host observes delivery availability and remote collection on the same clock.
                // The collector still records its local copy; only LOCAL contributes to item usage.
                log.EmitFor(actor, "ITEM_COLLECTED", new() { ["flowId"] = OrderFlow(receipt.Delivery.Order), ["itemType"] = receipt.Delivery.ItemId, ["quantity"] = 1,
                    ["buyerId"] = log.PlayerCode(receipt.Delivery.Buyer),
                    ["source"] = localCollector ? "LOCAL_RECEIPT_SNAPSHOT" : "HOST_RECEIPT_SNAPSHOT" }, localCollector ? "LOCAL" : "HOST");
            }
        }
        void DeliveryStage(ShopDeliverySlot slot, ShopDelivery delivery, bool available)
        {
            if (WaitingRoomShop.Instance?.HasAuthority != true || !deliveryStages.Add(delivery.Order + ":" + available)) return;
            // The delivery view can receive Changed before this adapter. Read the confirmed order
            // first so subscriber registration order does not invert purchase and dispatch logs.
            ShopChanged();
            var actor = Find(delivery.Buyer); if (actor == null) return;
            log.EmitFor(actor, available ? "DELIVERY_AVAILABLE" : "DELIVERY_DISPATCHED", new() { ["flowId"] = OrderFlow(delivery.Order),
                ["orderId"] = delivery.Order, ["quantity"] = 1, ["source"] = "DELIVERY_SLOT_ANIMATION" }, "HOST");
        }
        void ShopRejected(ulong actorId, string operation, string item, int order, string reason)
        {
            var actor = Find(actorId); if (actor == null || shop?.HasAuthority != true) return;
            log.EmitFor(actor, operation == "PURCHASE" ? "PURCHASE_FAILED" : "COLLECTION_FAILED", new() {
                ["flowId"] = order > 0 ? OrderFlow(order) : actor.IsLocal && shopFlow != null ? shopFlow : Guid.NewGuid().ToString("D"),
                ["operation"] = operation, ["orderId"] = order > 0 ? order : null, ["itemType"] = item, ["reason"] = reason }, "HOST");
        }
        void CarryRejected(ulong actorId, string operation, int uid, string reason)
        {
            var actor = Find(actorId); if (actor == null || carry?.HasAuthority != true) return;
            var payload = new Dictionary<string, object> { ["flowId"] = uid > 0 ? OrderFlow(uid) : Guid.NewGuid().ToString("D"),
                ["itemUid"] = uid > 0 ? uid : null, ["operation"] = operation, ["reason"] = reason,
                ["interactionId"] = DetailLogIds.ObjectId(carry, "IN_") };
            log.EmitFor(actor, operation == "SHELF" ? "STORAGE_FAILED" : "INTERACTION_FAILED", payload, "HOST");
        }
        void ServiceRejected(ulong actorId, string operation, ExpertRefusal reason)
        {
            var actor = Find(actorId); if (actor == null || medical?.HasAuthority != true) return;
            log.EmitFor(actor, "SERVICE_FAILED", new() { ["flowId"] = actor.IsLocal && medicalFlow != null ? medicalFlow : Guid.NewGuid().ToString("D"),
                ["operation"] = operation, ["serviceType"] = operation == "FACILITY_RESERVE" ? "FACILITY" : "RESTORE_BODY",
                ["reason"] = "EXPERT_" + reason }, "HOST");
        }
        void MedicalPhase(ExpertRoomPhase before, ExpertRoomPhase after)
        {
            if (medical == null) return;
            if (after == ExpertRoomPhase.Booked && medical.LocalIsBuyer)
            {
                medicalFlow = Guid.NewGuid().ToString("D"); medicalBefore = medical.BookedLimbs; medicalReached = medicalReturned = false;
                medicalEnteredAt = movedInsideAt = movedOutsideAt = null;
                log.Emit("SERVICE_RESERVED", new() { ["flowId"] = medicalFlow, ["serviceType"] = "RESTORE_BODY", ["lostCount"] = medicalBefore,
                    ["targetCount"] = medicalBefore, ["amount"] = medicalBefore * medical.MedicalPrice,
                    ["screenFlowId"] = serviceOpen && serviceEntry == "medical" ? serviceScreenFlow : null });
            }
            if (medicalFlow == null) return;
            if (after == ExpertRoomPhase.Exiting)
            {
                var local = NetworkPlayer.Local;
                if (local != null) log.Emit("RESTORATION_COMPLETED", new() { ["flowId"] = medicalFlow, ["beforeLostCount"] = medicalBefore, ["afterLostCount"] = DetailLogIds.Lost(local.LostParts) });
            }
            else if (after == ExpertRoomPhase.Idle)
            {
                MedicalMoves();
                if (!medicalReturned) log.Emit("SERVICE_ABANDONED", new() { ["flowId"] = medicalFlow,
                    ["reason"] = before == ExpertRoomPhase.Exiting ? "RETURN_NOT_OBSERVED" : "RESERVATION_CLEARED" });
                medicalFlow = null;
            }
        }
        void FacilityReserved(ulong buyer, ObstacleAdjust choice, int day, long amount)
        {
            var actor = Find(buyer); if (actor == null || medical?.HasAuthority != true) return;
            log.EmitFor(actor, "SERVICE_RESERVED", new() { ["flowId"] = Guid.NewGuid().ToString("D"), ["serviceType"] = "FACILITY",
                ["lostCount"] = 0, ["targetCount"] = 0, ["amount"] = amount, ["adjustment"] = choice.ToString(), ["appliesOnDay"] = day,
                ["screenFlowId"] = actor.IsLocal && serviceOpen && serviceEntry == "facility" ? serviceScreenFlow : null }, "HOST");
        }
        void Confirmed(int day) { if (scheduleFlow != null) log.Emit("SCHEDULE_CONFIRMED", new() { ["flowId"] = scheduleFlow, ["gameDay"] = day }); }
        void Placed(NetworkPlayer player) { if (!InWaitingRoom) return; placedPlayer = player; placedAt = Time.unscaledTimeAsDouble; }
        void Step(CampaignSnapshot step)
        {
            bool rest = step.Phase is CampaignPhase.RestTime or CampaignPhase.Night;
            string next = rest ? step.Generation + ":" + DetailLogIds.Break(step) : null;
            if (breakScope != null && breakScope != next && closedBreakScope != breakScope)
            {
                closedBreakScope = breakScope;
                log.EmitFor(null, "BREAK_ENDED", new() { ["reason"] = "BREAK_ENDED",
                    ["presentNetworkClientIds"] = campaign.HasAuthority ? NetworkPlayer.All
                        .Where(p => p != null && p.IsSpawned).Select(p => p.OwnerClientId.ToString()).ToArray() : null },
                    campaign.HasAuthority ? "HOST" : "LOCAL", breakStep);
            }
            if (step.Phase is not (CampaignPhase.RestTime or CampaignPhase.Night)) return;
            string key = step.Generation + ":" + DetailLogIds.Break(step);
            if (key != breakScope) { timing.BeginBreak(); if (step.Phase == CampaignPhase.Night) timing.Ready(false, Time.unscaledTimeAsDouble); breakScope = key; }
            breakStep = step;
        }
        void Ready(bool value)
        {
            Step(campaign.State); timing.Ready(value, Time.unscaledTimeAsDouble);
            log.EmitFor(null, "READY_CHANGED", new() { ["ready"] = value }, "LOCAL", campaign.State);
        }
        void Alarm()
        {
            Step(campaign.State); timing.Alarm(Time.unscaledTimeAsDouble);
            log.EmitFor(null, "RECALL_ALARM", null, "LOCAL", breakStep);
        }
        void FirstLeft(Transform player)
        {
            if (campaign == null || !campaign.HasAuthority) return;
            Step(campaign.State);
            var before = timing.Assembly(Time.unscaledTimeAsDouble);
            if (before.FirstLeftMs.HasValue) return;
            timing.FirstLeft(Time.unscaledTimeAsDouble);
            log.EmitFor(player.GetComponent<NetworkPlayer>(), "FIRST_PLAYER_LEFT", new() { ["position"] = DetailLogIds.Position(player.position) }, "HOST", breakStep);
        }
        void Gathered()
        {
            var elapsed = timing.Assembly(Time.unscaledTimeAsDouble);
            log.EmitFor(null, "ALL_PLAYERS_ASSEMBLED", new() { ["firstLeftToAssemblyMs"] = elapsed.FirstLeftMs,
                ["alarmToAssemblyMs"] = elapsed.AlarmMs, ["insideCount"] = gather.InsideCount, ["aliveCount"] = gather.AliveCount }, "HOST", breakStep);
        }
        void Expired(CampaignSnapshot step)
        {
            if (!InWaitingRoom || campaign?.HasAuthority != true) return;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.Slot != NetworkPlayer.NoSlot)
                    log.EmitFor(player, "RECALL_EXPIRED", new() { ["position"] = DetailLogIds.Position(player.transform.position),
                        ["zoneType"] = Region(player).Type, ["timerSource"] = "HOST_TICK_EXPIRED", ["timeRemainingMs"] = 0,
                        ["positionSource"] = player.IsLocal ? "HOST_LOCAL_TRANSFORM" : "HOST_REPLICATED_TRANSFORM" }, "HOST", step);
        }
        void MedicalMoved(ExpertRoomView room, PlayerMover mover, bool returned)
        {
            if (!InWaitingRoom || medicalFlow == null || NetworkPlayer.Local == null || mover.gameObject != NetworkPlayer.Local.gameObject) return;
            if (returned) { movedOutsideAt = Time.unscaledTimeAsDouble; movedOutsidePosition = mover.transform.position; }
            else { movedInsideAt = Time.unscaledTimeAsDouble; movedInsidePosition = mover.transform.position; }
        }
        void MedicalMoves()
        {
            if (medicalFlow == null) return;
            if (movedInsideAt.HasValue)
            {
                medicalEnteredAt = movedInsideAt; movedInsideAt = null;
                log.Emit("MEDICAL_ENTERED", new() { ["flowId"] = medicalFlow, ["position"] = DetailLogIds.Position(movedInsidePosition), ["entrySource"] = "LOCAL_POSE_APPLIED" });
            }
            if (!movedOutsideAt.HasValue) return;
            double at = movedOutsideAt.Value; movedOutsideAt = null; medicalReturned = true;
            log.Emit("MEDICAL_RETURNED", new() { ["flowId"] = medicalFlow, ["position"] = DetailLogIds.Position(movedOutsidePosition),
                ["returnSource"] = "LOCAL_POSE_APPLIED", ["enteredToReturnMs"] = medicalEnteredAt.HasValue ? (at - medicalEnteredAt.Value) * 1000d : null });
            verifyFlow = medicalFlow; verifyAfterFrame = Time.frameCount + 1;
        }
        void VerifyRestoration()
        {
            // Damage effects apply in Update. Wait a full frame after the real return pose.
            if (verifyFlow == null || Time.frameCount < verifyAfterFrame) return;
            var local = NetworkPlayer.Local; var mover = local != null ? local.GetComponent<PlayerMover>() : null;
            if (local == null || mover == null || carry == null) return;
            int slots = carry.Slots(local.OwnerClientId), expected = SurvivalRules.ItemSlots(2, carry.HasBackpack(local.OwnerClientId));
            log.Emit("RESTORATION_VERIFIED", new() { ["flowId"] = verifyFlow, ["lostCount"] = DetailLogIds.Lost(local.LostParts),
                ["movementRestored"] = Mathf.Approximately(mover.SpeedMultiplier, 1),
                ["jumpRestored"] = mover.JumpAllowed && Mathf.Approximately(mover.JumpHeightMultiplier, 1), ["capacityRestored"] = slots == expected,
                ["verificationSource"] = "APPLIED_CONFIGURATION", ["speedMultiplier"] = mover.SpeedMultiplier,
                ["jumpHeightMultiplier"] = mover.JumpHeightMultiplier, ["itemCapacity"] = slots, ["expectedItemCapacity"] = expected });
            verifyFlow = null;
        }
        void UnbindCampaign()
        {
            if (campaign == null) return;
            campaign.RecallAlarm -= Alarm; campaign.RestExpired -= Expired; campaign.LocalReadyChanged -= Ready; campaign.StepChanged -= Step;
        }
        void CloseZone(string reason) { if (zone != null) log.Emit("ZONE_EXITED", new() { ["zoneType"] = zone, ["durationMs"] = (Time.unscaledTime - zoneAt) * 1000d, ["reason"] = reason }); zone = null; }
        public void SceneLeaving()
        {
            CloseZone("SCENE_CHANGED");
            roomVolumes = Array.Empty<CameraViewZone>(); storageArrivals.Reset(); nextFind = 0;
            timing.Reset(); breakScope = closedBreakScope = null; placedAt = null; placedPlayer = null;
            movedInsideAt = movedOutsideAt = medicalEnteredAt = null; verifyFlow = null;
            if (medicalFlow != null) { if (!medicalReturned) log.Emit("SERVICE_ABANDONED", new() { ["flowId"] = medicalFlow, ["reason"] = "SCENE_CHANGED" }); medicalFlow = null; }
            shopOpen = scheduleOpen = entrance = serviceOpen = false;
        }
        static NetworkPlayer Find(ulong id) => NetworkPlayer.All.FirstOrDefault(p => p != null && p.OwnerClientId == id);
        public void Dispose()
        {
            ShopDeliverySlot.DeliveryStageChanged -= DeliveryStage;
            WaitingRoomSpawn.PlayerPlaced -= Placed; ExpertRoomView.LocalMoved -= MedicalMoved;
            if (shop != null) { shop.Changed -= ShopChanged; shop.OperationRejected -= ShopRejected; }
            if (carry != null) { carry.Changed -= CarryChanged; carry.OperationRejected -= CarryRejected; }
            if (medical != null) { medical.PhaseChanged -= MedicalPhase; medical.OperationRejected -= ServiceRejected; medical.FacilityReserved -= FacilityReserved; }
            if (schedule != null) schedule.Confirmed -= Confirmed; UnbindCampaign();
            if (gather != null) gather.Gathered -= Gathered; if (exit != null) exit.PlayerLeft -= FirstLeft;
        }
    }
}
