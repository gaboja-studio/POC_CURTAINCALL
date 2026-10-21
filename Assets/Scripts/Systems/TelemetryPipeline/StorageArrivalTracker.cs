using System;
using System.Collections.Generic;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Only observed acquisitions start a transport interval. Initial carry snapshots cannot invent one.</summary>
    public sealed class StorageArrivalTracker
    {
        sealed class Pending { public string Type, Source; public double AcquiredAt; public bool Reached; }
        readonly Dictionary<int, Pending> pending = new();
        public readonly struct Arrival
        {
            public readonly int Uid;
            public readonly string Type, Source;
            public readonly double DurationMs;
            public Arrival(int uid, string type, string source, double durationMs) { Uid = uid; Type = type; Source = source; DurationMs = durationMs; }
        }
        public void Acquired(int uid, string type, string source, double seconds)
        {
            if (uid <= 0 || string.IsNullOrEmpty(type) || !double.IsFinite(seconds)) return;
            if (!pending.TryGetValue(uid, out var old) || old.Type != type)
                pending[uid] = new Pending { Type = type, Source = source, AcquiredAt = seconds };
        }
        public IReadOnlyList<Arrival> Observe(string zone, IEnumerable<CarryLogTracker.Item> held, double seconds)
        {
            var arrivals = new List<Arrival>();
            if (zone != "STORAGE" || !double.IsFinite(seconds)) return arrivals;
            foreach (var item in held)
            {
                if (item.Location != "HELD" || !pending.TryGetValue(item.Uid, out var record) || record.Reached || record.Type != item.Type) continue;
                record.Reached = true;
                arrivals.Add(new(item.Uid, item.Type, record.Source, Math.Max(0, seconds - record.AcquiredAt) * 1000d));
            }
            return arrivals;
        }
        public void Forget(int uid) => pending.Remove(uid);
        public void Reset() => pending.Clear();
    }

    /// <summary>Pairs persisted observations from one runtime; never subtracts clocks from other clients.</summary>
    public sealed class FlowTimingTracker
    {
        const int Capacity = 2048;
        readonly Dictionary<(string Flow, string Event, string Actor), double> starts = new();
        readonly Queue<((string Flow, string Event, string Actor) Key, double At)> oldest = new();
        string room, run;

        static string Text(LogEvent e, string key) => e.payload.TryGetValue(key, out var value) ? value as string : null;
        static string Flow(LogEvent e) => Text(e, "flowId");
        static string Schedule(LogEvent e) => e.breakId == null ? null : "schedule:" + e.breakId;
        bool SameScope(LogEvent e) => room == e.roomId && run == e.runId;
        double? Since(LogEvent e, string flow, string milestone, bool shared = false)
        {
            if (flow == null || !SameScope(e) || !double.IsFinite(e.monotonicMs)
                || !starts.TryGetValue((flow, milestone, shared ? "" : e.playerId), out double at)
                || e.monotonicMs < at) return null;
            return e.monotonicMs - at;
        }

        // Enrichment is read-only. Failed validation or disk writes must not create a start marker.
        public Dictionary<string, object> Enrich(LogEvent e)
        {
            var result = new Dictionary<string, object>(e.payload);
            bool measured = false;
            void Add(string field, string start, string flow, bool shared = false)
            {
                double? duration = Since(e, flow, start, shared);
                result[field] = duration; measured |= duration.HasValue;
            }
            switch (e.eventType)
            {
                case "PURCHASE_COMPLETED": Add("screenToPurchaseMs", "SHOP_OPENED", Text(e, "screenFlowId")); break;
                case "DELIVERY_DISPATCHED": Add("purchaseToDispatchMs", "PURCHASE_COMPLETED", Flow(e), true); break;
                case "DELIVERY_AVAILABLE":
                    Add("dispatchToAvailableMs", "DELIVERY_DISPATCHED", Flow(e), true);
                    Add("purchaseToAvailableMs", "PURCHASE_COMPLETED", Flow(e), true); break;
                case "ITEM_COLLECTED": Add("availableToCollectionMs", "DELIVERY_AVAILABLE", Flow(e), true); break;
                case "ITEM_STORED": Add("storageArrivalToStoreMs", "STORAGE_REACHED", Flow(e)); break;
                case "SCHEDULE_CONFIRMED": Add("screenToConfirmMs", "SCHEDULE_OPENED", Flow(e)); break;
                case "READY_CHANGED" when e.payload.TryGetValue("ready", out var ready) && ready is true:
                    // A break key prevents a previous night's schedule from timing today's ready action.
                    if (Schedule(e) != null)
                    {
                        Add("confirmationToReadyMs", "SCHEDULE_CONFIRMED", Schedule(e));
                        Add("screenToReadyMs", "SCHEDULE_OPENED", Schedule(e));
                    }
                    break;
                case "SERVICE_RESERVED": Add("screenToReservationMs", "SERVICE_OPENED", Text(e, "screenFlowId")); break;
                case "MEDICAL_ROOM_REACHED": Add("reservationToRoomMs", "SERVICE_RESERVED", Flow(e)); break;
                case "MEDICAL_ENTERED":
                    Add("roomToEntryMs", "MEDICAL_ROOM_REACHED", Flow(e)); Add("reservationToEntryMs", "SERVICE_RESERVED", Flow(e)); break;
                case "RESTORATION_COMPLETED": Add("enteredToRestoreMs", "MEDICAL_ENTERED", Flow(e)); break;
                case "MEDICAL_RETURNED": Add("restorationToReturnMs", "RESTORATION_COMPLETED", Flow(e)); break;
            }
            if (measured) result["timingSource"] = "OBSERVED_CLIENT_MONOTONIC";
            return result;
        }

        void Remember(LogEvent e, string flow, string milestone, bool shared = false)
        {
            if (flow == null) return;
            var key = (flow, milestone, shared ? "" : e.playerId);
            starts[key] = e.monotonicMs; oldest.Enqueue((key, e.monotonicMs));
            while (oldest.Count > Capacity)
            {
                var expired = oldest.Dequeue();
                if (starts.TryGetValue(expired.Key, out double at) && at == expired.At) starts.Remove(expired.Key);
            }
        }
        public void Observe(LogEvent e)
        {
            if (!double.IsFinite(e.monotonicMs) || e.monotonicMs < 0) return;
            if (!SameScope(e)) { Reset(); room = e.roomId; run = e.runId; }
            switch (e.eventType)
            {
                case "SHOP_OPENED": case "SERVICE_OPENED": case "SERVICE_RESERVED":
                case "MEDICAL_ROOM_REACHED": case "MEDICAL_ENTERED": case "RESTORATION_COMPLETED":
                case "STORAGE_REACHED": Remember(e, Flow(e), e.eventType); break;
                case "PURCHASE_COMPLETED": case "DELIVERY_DISPATCHED": case "DELIVERY_AVAILABLE":
                    Remember(e, Flow(e), e.eventType, true); break;
                case "SCHEDULE_OPENED": case "SCHEDULE_CONFIRMED":
                    Remember(e, Flow(e), e.eventType);
                    Remember(e, Schedule(e), e.eventType); break;
                case "ITEM_HANDED_OVER": case "ITEM_STORED":
                    // A handover ends this actor's storage workflow; dropping and repicking does not.
                    starts.Remove((Flow(e), "STORAGE_REACHED", e.playerId)); break;
            }
        }
        public void Reset() { starts.Clear(); oldest.Clear(); room = run = null; }
    }
}
