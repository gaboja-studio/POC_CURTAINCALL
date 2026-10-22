using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using NUnit.Framework;
using UnityEngine;
using CurtainCall.HumanCannon;
using CurtainCall.Cameras;

namespace CurtainCall.Telemetry.Pipeline.EditorTests
{
    public sealed class FlowTimingTests
    {
        static LogEvent Entry(string type, double at, string flow = "order", string player = "p1") => new()
        {
            eventType = type, monotonicMs = at, playerId = player, roomId = "room", runId = "run", breakId = "D1T3",
            payload = new() { ["flowId"] = flow }
        };
        [Test] public void Delivery_PairsOrderAcrossActorsOnOneRuntimeWithoutMutatingCaller()
        {
            var tracker = new FlowTimingTracker();
            tracker.Observe(Entry("PURCHASE_COMPLETED", 100)); tracker.Observe(Entry("DELIVERY_DISPATCHED", 110));
            var available = Entry("DELIVERY_AVAILABLE", 310);
            Assert.That(tracker.Enrich(available)["dispatchToAvailableMs"], Is.EqualTo(200));
            Assert.That(tracker.Enrich(available)["purchaseToAvailableMs"], Is.EqualTo(210));
            Assert.That(available.payload.ContainsKey("timingSource"), Is.False);
            tracker.Observe(available);
            Assert.That(tracker.Enrich(Entry("ITEM_COLLECTED", 400, player: "p2"))["availableToCollectionMs"], Is.EqualTo(90));
        }
        [Test] public void MissingUnpersistedReversedAndOtherRunStarts_StayUnknown()
        {
            var tracker = new FlowTimingTracker(); var start = Entry("DELIVERY_AVAILABLE", 100);
            tracker.Enrich(start); // Preparing an entry is not proof that it was persisted.
            var end = Entry("ITEM_COLLECTED", 200);
            Assert.That(tracker.Enrich(end)["availableToCollectionMs"], Is.Null);
            tracker.Observe(start); end.monotonicMs = 90;
            Assert.That(tracker.Enrich(end)["availableToCollectionMs"], Is.Null);
            end.monotonicMs = 200; end.runId = "another-run";
            Assert.That(tracker.Enrich(end)["availableToCollectionMs"], Is.Null);
            tracker.Observe(end); end.runId = "run";
            Assert.That(tracker.Enrich(end)["availableToCollectionMs"], Is.Null);
            tracker.Reset(); Assert.That(tracker.Enrich(end).ContainsKey("timingSource"), Is.False);
        }
        [Test] public void RemoteCollection_UsesHostOrderClockAndNeverTheCollectorsClock()
        {
            var host = new FlowTimingTracker(); var collector = new FlowTimingTracker();
            var available = Entry("DELIVERY_AVAILABLE", 1000, player: "buyer"); available.authority = "HOST";
            host.Observe(available);
            var remote = Entry("ITEM_COLLECTED", 6000, player: "collector"); remote.authority = "HOST";
            Assert.That(host.Enrich(remote)["availableToCollectionMs"], Is.EqualTo(5000));
            remote.monotonicMs = 900000; remote.authority = "LOCAL";
            Assert.That(collector.Enrich(remote)["availableToCollectionMs"], Is.Null);
            // A restored receipt snapshot without an observed availability must also remain unknown.
            host.Reset(); Assert.That(host.Enrich(remote)["availableToCollectionMs"], Is.Null);
        }
        [Test] public void Storage_RequiresSameActorAndEndsOnHandover()
        {
            var tracker = new FlowTimingTracker(); tracker.Observe(Entry("STORAGE_REACHED", 100));
            Assert.That(tracker.Enrich(Entry("ITEM_STORED", 200, player: "p2"))["storageArrivalToStoreMs"], Is.Null);
            tracker.Observe(Entry("ITEM_DROPPED", 150));
            Assert.That(tracker.Enrich(Entry("ITEM_STORED", 200))["storageArrivalToStoreMs"], Is.EqualTo(100));
            tracker.Observe(Entry("ITEM_HANDED_OVER", 210));
            Assert.That(tracker.Enrich(Entry("ITEM_STORED", 220))["storageArrivalToStoreMs"], Is.Null);
        }
        [Test] public void ScreenAndSchedule_RequireObservedMatchingPlayerAndBreak()
        {
            var tracker = new FlowTimingTracker(); tracker.Observe(Entry("SHOP_OPENED", 100, "screen"));
            var purchase = Entry("PURCHASE_COMPLETED", 100); purchase.payload["screenFlowId"] = "screen";
            Assert.That(tracker.Enrich(purchase)["screenToPurchaseMs"], Is.EqualTo(0));
            purchase.playerId = "p2"; Assert.That(tracker.Enrich(purchase)["screenToPurchaseMs"], Is.Null);
            purchase.payload["screenFlowId"] = null; Assert.That(tracker.Enrich(purchase)["screenToPurchaseMs"], Is.Null);
            tracker.Observe(Entry("SCHEDULE_OPENED", 200, "schedule")); tracker.Observe(Entry("SCHEDULE_CONFIRMED", 250, "schedule"));
            var ready = Entry("READY_CHANGED", 300); ready.payload["ready"] = true;
            Assert.That(tracker.Enrich(ready)["confirmationToReadyMs"], Is.EqualTo(50));
            Assert.That(tracker.Enrich(ready)["screenToReadyMs"], Is.EqualTo(100));
            ready.breakId = "D2T3"; Assert.That(tracker.Enrich(ready)["confirmationToReadyMs"], Is.Null);
        }
        [Test] public void Medical_SeparatesReservationArrivalEntryRestorationAndReturn()
        {
            var tracker = new FlowTimingTracker(); tracker.Observe(Entry("SERVICE_OPENED", 10, "screen"));
            var reserved = Entry("SERVICE_RESERVED", 20); reserved.payload["screenFlowId"] = "screen";
            Assert.That(tracker.Enrich(reserved)["screenToReservationMs"], Is.EqualTo(10)); tracker.Observe(reserved);
            var arrival = Entry("MEDICAL_ROOM_REACHED", 40);
            Assert.That(tracker.Enrich(arrival)["reservationToRoomMs"], Is.EqualTo(20)); tracker.Observe(arrival);
            var entered = Entry("MEDICAL_ENTERED", 70);
            Assert.That(tracker.Enrich(entered)["roomToEntryMs"], Is.EqualTo(30));
            Assert.That(tracker.Enrich(entered)["reservationToEntryMs"], Is.EqualTo(50)); tracker.Observe(entered);
            var restored = Entry("RESTORATION_COMPLETED", 100);
            Assert.That(tracker.Enrich(restored)["enteredToRestoreMs"], Is.EqualTo(30)); tracker.Observe(restored);
            var returned = Entry("MEDICAL_RETURNED", 130); returned.payload["enteredToReturnMs"] = 59d;
            var result = tracker.Enrich(returned);
            Assert.That(result["restorationToReturnMs"], Is.EqualTo(30)); Assert.That(result["enteredToReturnMs"], Is.EqualTo(59));
        }
        [Test] public void BoundedHistory_EvictsOldMarkersAsUnknown()
        {
            var tracker = new FlowTimingTracker();
            for (int i = 0; i <= 2048; i++) tracker.Observe(Entry("DELIVERY_AVAILABLE", i, i.ToString()));
            Assert.That(tracker.Enrich(Entry("ITEM_COLLECTED", 3000, "0"))["availableToCollectionMs"], Is.Null);
            Assert.That(tracker.Enrich(Entry("ITEM_COLLECTED", 3000, "2048"))["availableToCollectionMs"], Is.EqualTo(952));
        }
    }
    public sealed class DetailAdapterTests
    {
        [Test] public void WaitingTiming_OnlyObservedSpawnStartsFirstArrivalAndRevisitDoesNotRepeat()
        {
            var timing = new WaitingRoomTiming();
            Assert.That(timing.FirstArrival("BEDROOM", 10), Is.Null);
            timing.Spawn(10);
            Assert.That(timing.FirstArrival("BEDROOM", 10), Is.EqualTo(0));
            Assert.That(timing.FirstArrival("BEDROOM", 20), Is.Null);
            Assert.That(timing.FirstArrival("HALLWAY", 20), Is.EqualTo(10000));
            timing.Spawn(30); Assert.That(timing.FirstArrival("BEDROOM", 32), Is.EqualTo(2000));
            timing.Reset(); Assert.That(timing.FirstArrival("STORAGE", 40), Is.Null);
        }
        [Test] public void WaitingTiming_ReadyCancellationBreakAndRoomBoundariesDoNotLeakIntervals()
        {
            var timing = new WaitingRoomTiming();
            timing.Ready(true, 10); timing.Ready(false, 11); Assert.That(timing.Entrance(20), Is.Null);
            timing.Ready(true, 30); timing.BeginBreak(); Assert.That(timing.Entrance(35), Is.EqualTo(5000));
            Assert.That(timing.Entrance(36), Is.Null);
            timing.FirstLeft(40); timing.FirstLeft(42); timing.Alarm(45);
            Assert.That(timing.Assembly(50).FirstLeftMs, Is.EqualTo(10000)); Assert.That(timing.Assembly(50).AlarmMs, Is.EqualTo(5000));
            timing.BeginBreak(); Assert.That(timing.Assembly(60).FirstLeftMs, Is.Null); Assert.That(timing.Assembly(60).AlarmMs, Is.Null);
        }
        [Test] public void RestExpiry_OnlyTickTimeoutReportsPreTransitionContextAndIsolatesObservers()
        {
            var state = new RunState(); state.StartSession(); state.EndIntro(); state.EndPerformance(); state.DebugSetRemaining(0);
            CampaignSnapshot observed = default; int calls = 0;
            Action<CampaignSnapshot> listener = _ => throw new Exception("test observer");
            listener += step => { observed = step; calls++; };
            Assert.That(state.Tick(.1f, listener), Is.True); Assert.That(calls, Is.EqualTo(1));
            Assert.That(observed.Phase, Is.EqualTo(CampaignPhase.RestTime)); Assert.That(observed.TimeRemaining, Is.Zero);
            Assert.That(DetailLogIds.Break(observed), Is.EqualTo("D1T2"));
            Assert.That(state.Snapshot.Phase, Is.EqualTo(CampaignPhase.Performance));
            state.Tick(.1f, listener); Assert.That(calls, Is.EqualTo(1));
            state.EndPerformance(); // Night; no timeout.
            state.Tick(.1f, listener); Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void RestExpiry_EarlyAssemblyNeverReportsTimeout()
        {
            var state = new RunState(); state.StartSession(); state.EndIntro(); state.EndPerformance();
            int expired = 0; Assert.That(state.NotifyAllAtStageEntrance(), Is.True);
            state.Tick(100000, _ => expired++); Assert.That(expired, Is.Zero);
        }
        [Test] public void RoutePosition_PreservesSourceSnapshotAndExplicitUnknown()
        {
            var source = new Dictionary<string, object> { ["position"] = DetailLogIds.Position(new Vector3(1, 2, 3)) };
            Assert.That(DetailLogIds.RoutePayload("PLAYER_DIED", source, Vector3.one * 100), Is.SameAs(source));
            source["position"] = null;
            Assert.That(DetailLogIds.RoutePayload("PLAYER_DIED", source, Vector3.one * 100)["position"], Is.Null);
        }
        [Test] public void RoutePosition_AddsEmissionObservationWithoutMutatingCaller()
        {
            var original = new Dictionary<string, object> { ["gimmickType"] = "HANDHOLD" };
            var observed = DetailLogIds.RoutePayload("COOP_ACTIVATED", original, new Vector3(1.24f, 2.36f, 9.81f));
            Assert.That(original.ContainsKey("position"), Is.False);
            Assert.That(observed["positionSource"], Is.EqualTo("EMIT_OBSERVATION"));
            Assert.That(LogJson.Serialize(observed["position"]), Is.EqualTo("{\"x\":1.2,\"y\":2.4,\"z\":9.8}"));
            Assert.That(DetailLogIds.RoutePayload("INPUT_KEY", original, Vector3.zero), Is.SameAs(original));
        }
        [Test] public void RoomVolume_UsesOrientedLocalBoxAndIgnoresDisabledVolumes()
        {
            var go = new GameObject("TelemetryRoomVolumeTest");
            try
            {
                var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(2, 4, 8); box.center = new Vector3(1, 2, 0);
                go.transform.SetPositionAndRotation(new Vector3(4, 0, 8), Quaternion.Euler(0, 45, 0));
                go.transform.localScale = new Vector3(2, 1, .5f);
                Vector3 Feet(Vector3 point) => go.transform.TransformPoint(point) - Vector3.up * .2f;
                Assert.That(WaitingRoomZoneMap.Contains(box, Feet(box.center)), Is.True);
                Assert.That(WaitingRoomZoneMap.Contains(box, Feet(box.center + Vector3.right * 1.1f)), Is.False);
                box.enabled = false; Assert.That(WaitingRoomZoneMap.Contains(box, Feet(box.center)), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void RoomMap_RequiresKnownAuthoredVolumesAndDoesNotGuessOverlaps()
        {
            var root = new GameObject("CameraZones");
            try
            {
                CameraViewZone Make(string name)
                {
                    var go = new GameObject(name); go.transform.SetParent(root.transform);
                    var view = go.AddComponent<CameraViewZone>();
                    go.GetComponent<BoxCollider>().isTrigger = true; go.GetComponent<BoxCollider>().size = Vector3.one * 5;
                    return view;
                }
                var bedroom = Make("Zone_Dormitory");
                Assert.That(WaitingRoomZoneMap.Find(new[] { bedroom }, Vector3.zero).Type, Is.EqualTo("BEDROOM"));
                var storage = Make("Zone_PropRoom");
                var ambiguous = WaitingRoomZoneMap.Find(new[] { bedroom, storage }, Vector3.zero);
                Assert.That(ambiguous.Type, Is.EqualTo("WAITINGROOM")); Assert.That(ambiguous.Source, Is.EqualTo("AMBIGUOUS_CAMERA_VOLUMES"));
                root.name = "UnrelatedCameraZones";
                Assert.That(WaitingRoomZoneMap.Find(new[] { bedroom }, Vector3.zero).Source, Is.EqualTo("SCENE_FALLBACK"));
                Assert.That(WaitingRoomZoneMap.RoomType("Zone_Custom"), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void StorageArrival_RequiresObservedAcquisitionAndActualHeldItemInStorage()
        {
            var tracker = new StorageArrivalTracker();
            var held = new[] { Item("HELD") };
            Assert.That(tracker.Observe("STORAGE", held, 5), Is.Empty, "An initial snapshot is not a collection event.");
            tracker.Acquired(1, "tool-a", "ITEM_COLLECTED", 10);
            Assert.That(tracker.Observe("HALLWAY", held, 12), Is.Empty);
            Assert.That(tracker.Observe("STORAGE", new[] { Item("FLOOR") }, 13), Is.Empty);
            tracker.Acquired(1, "tool-a", "ITEM_PICKED_UP", 14); // Drop/pick must not restart the delivery journey.
            var reached = tracker.Observe("STORAGE", held, 15);
            Assert.That(reached.Count, Is.EqualTo(1)); Assert.That(reached[0].DurationMs, Is.EqualTo(5000));
            Assert.That(reached[0].Source, Is.EqualTo("ITEM_COLLECTED"));
            Assert.That(tracker.Observe("STORAGE", held, 16), Is.Empty);
        }
        [Test] public void StorageArrival_HandoverAndResetCannotReusePreviousCarrierInterval()
        {
            var tracker = new StorageArrivalTracker(); var held = new[] { Item("HELD") };
            tracker.Acquired(1, "tool-a", "ITEM_COLLECTED", 1); tracker.Forget(1);
            Assert.That(tracker.Observe("STORAGE", held, 5), Is.Empty);
            tracker.Acquired(1, "tool-a", "ITEM_HANDED_OVER", 10);
            Assert.That(tracker.Observe("STORAGE", held, 12)[0].DurationMs, Is.EqualTo(2000));
            tracker.Reset(); Assert.That(tracker.Observe("STORAGE", held, 20), Is.Empty);
            tracker.Acquired(1, "tool-b", "ITEM_COLLECTED", 30);
            Assert.That(tracker.Observe("STORAGE", held, 31), Is.Empty, "Reused UID with a different item is not the observed acquisition.");
        }
        [Test] public void CannonSectionQuery_UsesConfiguredBoundariesAndHeight()
        {
            var go = new GameObject("TelemetrySectionQueryTest");
            try
            {
                var sections = go.AddComponent<HumanCannonCourseSections>();
                sections.Configure(new HumanCannonCourseSections.Boundaries(10, 20, 30, 40, 5));
                Assert.That(sections.SectionAt(new Vector3(0, 0, 9)), Is.EqualTo(HumanCannonCourseSections.Section.Departure));
                Assert.That(sections.SectionAt(new Vector3(0, 0, 40)), Is.EqualTo(HumanCannonCourseSections.Section.First));
                Assert.That(sections.SectionAt(new Vector3(0, 5, 20)), Is.EqualTo(HumanCannonCourseSections.Section.Shelter));
                Assert.That(sections.SectionAt(new Vector3(0, 5, 30)), Is.EqualTo(HumanCannonCourseSections.Section.Second));
                Assert.That(sections.SectionAt(new Vector3(0, 5, 40)), Is.EqualTo(HumanCannonCourseSections.Section.Final));
                sections.Configure(new HumanCannonCourseSections.Boundaries(100, 200, 300, 400, 50));
                Assert.That(sections.SectionAt(new Vector3(0, 5, 40)), Is.EqualTo(HumanCannonCourseSections.Section.Departure));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static CarryLogTracker.Item Item(string location, ulong owner = 0, int uid = 1) => new(uid, "tool-a", location, owner);
        [Test] public void FirstSnapshot_DoesNotInventHistoricalActions()
        { var tracker = new CarryLogTracker(); Assert.That(tracker.Observe(new[] { Item("HELD") }), Is.Empty); }
        [Test] public void DropPickStoreTransfer_UseUidNotListOrder()
        {
            var tracker = new CarryLogTracker(); tracker.Observe(new[] { Item("HELD"), Item("HELD", 2, 2) });
            var drop = tracker.Observe(new[] { Item("HELD", 2, 2), Item("FLOOR") }); Assert.That(drop.Count, Is.EqualTo(1)); Assert.That(drop[0].Event, Is.EqualTo("ITEM_DROPPED"));
            Assert.That(tracker.Observe(new[] { Item("HELD", 1) })[0].Event, Is.EqualTo("ITEM_PICKED_UP"));
            Assert.That(tracker.Observe(new[] { Item("HELD", 3) })[0].Event, Is.EqualTo("ITEM_HANDED_OVER"));
            Assert.That(tracker.Observe(new[] { Item("SHELF") })[0].Event, Is.EqualTo("ITEM_STORED"));
        }
        [Test] public void DuplicateAndMissingSnapshots_DoNotInventRepeatedActions()
        {
            var tracker = new CarryLogTracker(); tracker.Observe(new[] { Item("HELD") });
            Assert.That(tracker.Observe(new[] { Item("HELD") }), Is.Empty);
            Assert.That(tracker.Observe(Array.Empty<CarryLogTracker.Item>()), Is.Empty);
            Assert.That(tracker.Observe(new[] { Item("FLOOR") }), Is.Empty);
        }
        [Test] public void InvalidSnapshot_IsAtomicAndRetainsPreviousObservation()
        {
            var tracker = new CarryLogTracker(); tracker.Observe(new[] { Item("HELD") });
            Assert.Throws<ArgumentException>(() => tracker.Observe(new[] { Item("FLOOR"), Item("SHELF") }));
            Assert.That(tracker.Observe(new[] { Item("FLOOR") })[0].Event, Is.EqualTo("ITEM_DROPPED"));
        }
        [Test] public void ResetAndReusedUid_DoNotLinkDifferentRoomsOrItemTypes()
        {
            var tracker = new CarryLogTracker(); tracker.Observe(new[] { Item("HELD") }); tracker.Reset();
            Assert.That(tracker.Observe(new[] { Item("SHELF") }), Is.Empty);
            Assert.That(tracker.Observe(new[] { new CarryLogTracker.Item(1, "tool-b", "HELD", 1) }), Is.Empty);
        }
        [Test] public void FlowIds_AreStableWithinRoomAndDistinctAcrossRooms()
        {
            Assert.That(DetailLogIds.Uuid("room-a:order:1"), Is.EqualTo(DetailLogIds.Uuid("room-a:order:1")));
            Assert.That(DetailLogIds.Uuid("room-a:order:1"), Is.Not.EqualTo(DetailLogIds.Uuid("room-b:order:1")));
        }
        [Test] public void BreakIds_KeepNightMorningAndMiddayDistinct()
        {
            Assert.That(DetailLogIds.Break(new() { Day = 2, Phase = CampaignPhase.Night }), Is.EqualTo("D2T3"));
            Assert.That(DetailLogIds.Break(new() { Day = 2, Phase = CampaignPhase.RestTime, Rest = RestKind.Morning }), Is.EqualTo("D2T1"));
            Assert.That(DetailLogIds.Break(new() { Day = 2, Phase = CampaignPhase.RestTime, Rest = RestKind.Between }), Is.EqualTo("D2T2"));
            Assert.That(DetailLogIds.Break(new() { Day = 2, Phase = CampaignPhase.Performance }), Is.Null);
        }
        [Test] public void DisconnectReason_DoesNotCopyArbitraryServerOrCredentialText()
        {
            Assert.That(DetailLogIds.Disconnect("방에서 나갔습니다."), Is.EqualTo("VOLUNTARY"));
            Assert.That(DetailLogIds.Disconnect("exception / private detail"), Is.EqualTo("UNKNOWN"));
        }
        [Test] public void UnavailableCannonSectionAndTarget_AreNullNotFabricated()
        {
            var entry = new LogEvent { occurredAt = "2026-10-21T09:00:00Z", sequence = 1, monotonicMs = 1, environment = "TEST", playerId = "p",
                runId = Guid.NewGuid().ToString("D"), attemptId = Guid.NewGuid().ToString("D"), sceneType = "MAINSTAGE", eventType = "CANNON_FLIGHT_ENDED", authority = "HOST",
                payload = new() { ["launchId"] = Guid.NewGuid().ToString("D"), ["sectionId"] = null, ["position"] = new { x = 0, y = 0, z = 0 }, ["reachedTarget"] = null, ["reason"] = "FLIGHT_STOPPED" } };
            Assert.That(EventContract.IsValid(entry), Is.True);
            entry.payload["sectionId"] = 0; Assert.That(EventContract.IsValid(entry), Is.False);
            entry.payload.Remove("sectionId"); Assert.That(EventContract.IsValid(entry), Is.False);
        }
    }
}
