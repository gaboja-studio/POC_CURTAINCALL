using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Networking;

namespace CurtainCall.Telemetry.Pipeline.PlayTests
{
    /// <summary>Public API runtime integration. Physical keyboard/pointer play is a separate human check.</summary>
    public sealed class TelemetryIntegrationTests
    {
        readonly List<Observation> events = new();
        Component runtime, session;
        Delegate listener;
        IDisposable minimumPlayers;
#if UNITY_EDITOR
        string logTagPreference;
        bool previousLogTag;
        [UnitySetUp] public IEnumerator EnableLogsForThisFixture()
        {
            logTagPreference = (string)Game("CurtainCall.Telemetry.Pipeline.PlaytestBuildTags").GetMethod("PreferenceKey").Invoke(null, new object[] { "LOGS" });
            previousLogTag = UnityEditor.EditorPrefs.GetBool(logTagPreference, false);
            UnityEditor.EditorPrefs.SetBool(logTagPreference, true);
            yield return null;
        }
#endif
        static Type Game(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        static object Static(string type, string name) => Game(type).GetProperty(name).GetValue(null);
        static IEnumerator RemoveAutomaticSceneRouting()
        {
            // Event subscriptions remain active when a MonoBehaviour is merely disabled.
            var director = UnityEngine.Object.FindAnyObjectByType(Game("CurtainCall.SpaceLink.SpaceLinkDirector")) as MonoBehaviour;
            if (director != null) { director.StopAllCoroutines(); UnityEngine.Object.Destroy(director); yield return null; }
        }
        void Record(object entry)
        {
            object Field(string name) => entry.GetType().GetField(name).GetValue(entry);
            events.Add(new Observation { Type = (string)Field("eventType"), Room = (string)Field("roomId"),
                Slot = (int?)Field("playerNumber"), Payload = (IDictionary)Field("payload"), Attempt = (string)Field("attemptId"),
                Run = (string)Field("runId"), Break = (string)Field("breakId"), Authority = (string)Field("authority") });
        }
        void Capture()
        {
            runtime = (Component)Static("CurtainCall.Telemetry.Pipeline.TelemetryRuntime", "Instance");
            Assert.That(runtime, Is.Not.Null);
            var recorded = runtime.GetType().GetEvent("Recorded");
            var parameter = Expression.Parameter(recorded.EventHandlerType.GetGenericArguments()[0]);
            listener = Expression.Lambda(recorded.EventHandlerType,
                Expression.Call(Expression.Constant(this), GetType().GetMethod(nameof(Record), BindingFlags.NonPublic | BindingFlags.Instance), Expression.Convert(parameter, typeof(object))), parameter).Compile();
            recorded.AddEventHandler(runtime, listener);
        }
        [Serializable] sealed class UploadProbeBody { public string token, batchJson; }
        [Serializable] sealed class UploadProbeAck { public bool ok; public string batchId, sha256; public int acceptedEvents; }
        [Serializable] sealed class UploadProbeBatch { public string batchId, appSessionId; public UploadProbeEvent[] events; }
        [Serializable] sealed class UploadProbeEvent { public string eventId, gameVersion; }

        [UnityTest]
        public IEnumerator OfflineOutbox_RestartsRetriesAndGoogleAcknowledgesIdenticalReplay()
        {
            yield return SceneManager.LoadSceneAsync("Start", LoadSceneMode.Single); yield return null;
            runtime = (Component)Static("CurtainCall.Telemetry.Pipeline.TelemetryRuntime", "Instance");
            var behaviour = (MonoBehaviour)runtime;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var outboxField = runtime.GetType().GetField("outbox", flags);
            var endpointField = runtime.GetType().GetField("endpoint", flags);
            var tokenField = runtime.GetType().GetField("token", flags);
            var originalOutbox = outboxField.GetValue(runtime);
            string originalEndpoint = (string)endpointField.GetValue(runtime);
            string secret = (string)tokenField.GetValue(runtime);
            Assert.That(Read(runtime, "RemoteConfigured"), Is.EqualTo(true), "Configure the existing telemetry endpoint first.");
            bool wasEnabled = behaviour.enabled;
            string probe = Guid.NewGuid().ToString("D");
            string directory = Path.Combine(Application.temporaryCachePath, "TelemetryReplayProbe", probe);
            var boxType = originalOutbox.GetType();
            var upload = runtime.GetType().GetMethod("UploadLoop", flags);
            behaviour.StopAllCoroutines(); behaviour.enabled = false;
            try
            {
                var isolated = Activator.CreateInstance(boxType, directory, Guid.NewGuid().ToString("D"));
                outboxField.SetValue(runtime, isolated);
                Assert.That(Call(runtime, "Emit", "GAME_STARTED", new Dictionary<string, object> { ["verificationId"] = probe, ["purpose"] = "OFFLINE_REPLAY_PROBE" }, "LOCAL"), Is.EqualTo(true));
                string active = File.ReadAllText(Path.Combine(directory, "active.json"));
                var initial = JsonUtility.FromJson<UploadProbeBatch>(active);
                Assert.That(initial.events.Length, Is.EqualTo(1));
                Assert.That(initial.events[0].gameVersion, Is.EqualTo(Application.version));
                Assert.That(initial.events[0].gameVersion, Is.EqualTo("0.1.0"));
                // Reopen an unsealed queue: the same disk recovery path used by the next game launch.
                var recovered = Activator.CreateInstance(boxType, directory, Guid.NewGuid().ToString("D"));
                outboxField.SetValue(runtime, recovered);
                string pending = (string)Call(recovered, "NextPath");
                string raw = File.ReadAllText(pending);
                var batch = JsonUtility.FromJson<UploadProbeBatch>(raw);
                Assert.That(batch.batchId, Is.EqualTo(initial.batchId));
                Assert.That(batch.appSessionId, Is.EqualTo(initial.appSessionId));
                Assert.That(batch.events[0].eventId, Is.EqualTo(initial.events[0].eventId));
                Assert.That(batch.events[0].gameVersion, Is.EqualTo(initial.events[0].gameVersion));
                endpointField.SetValue(runtime, "https://127.0.0.1:1"); // Fail only this uploader, not the PC's network.
                tokenField.SetValue(runtime, "offline-probe-placeholder-not-a-credential");
                behaviour.StartCoroutine((IEnumerator)upload.Invoke(runtime, null));
                float deadline = Time.realtimeSinceStartup + 12;
                while ((string)Read(runtime, "LastError") != "Telemetry upload not acknowledged; retry pending." && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Read(runtime, "LastError"), Is.EqualTo("Telemetry upload not acknowledged; retry pending."));
                Assert.That(File.ReadAllText(pending), Is.EqualTo(raw), "Failed upload must preserve exact bytes.");
                behaviour.StopAllCoroutines();
                endpointField.SetValue(runtime, originalEndpoint);
                tokenField.SetValue(runtime, secret);
                behaviour.StartCoroutine((IEnumerator)upload.Invoke(runtime, null));
                deadline = Time.realtimeSinceStartup + 60;
                while (File.Exists(pending) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(File.Exists(pending), Is.False, "Only a matching real Google ACK may remove the pending batch.");
                Assert.That(Read(runtime, "LastError"), Is.Null);
                // Simulate a lost ACK by resending the exact retained request, never a new event ID.
                using var replay = new UnityWebRequest(originalEndpoint, "POST");
                replay.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new UploadProbeBody { token = secret, batchJson = raw })));
                replay.downloadHandler = new DownloadHandlerBuffer(); replay.SetRequestHeader("Content-Type", "application/json"); replay.timeout = 30;
                yield return replay.SendWebRequest();
                Assert.That(replay.result, Is.EqualTo(UnityWebRequest.Result.Success), "Google replay transport failed.");
                var ack = JsonUtility.FromJson<UploadProbeAck>(replay.downloadHandler.text);
                Assert.That(ack.ok, Is.True); Assert.That(ack.batchId, Is.EqualTo(batch.batchId));
                Assert.That(ack.acceptedEvents, Is.EqualTo(1));
                string hash = (string)boxType.GetMethod("Sha256").Invoke(null, new object[] { raw });
                Assert.That(ack.sha256, Is.EqualTo(hash));
                Assert.That(Call(recovered, "NextPath"), Is.Null);
                string proof = JsonUtility.ToJson(new ReplayProof { verificationId = probe, batchId = batch.batchId, eventId = batch.events[0].eventId, sha256 = hash,
                    diskRecovery = true, offlineRetained = true, uploadAcknowledged = true, identicalReplayAcknowledged = true });
                string proofPath = Path.Combine(Application.dataPath, "..", ".harness", "local", "telemetry-replay-proof.json");
                File.WriteAllText(proofPath, proof);
            }
            finally
            {
                behaviour.StopAllCoroutines(); outboxField.SetValue(runtime, originalOutbox); endpointField.SetValue(runtime, originalEndpoint); tokenField.SetValue(runtime, secret);
                behaviour.enabled = wasEnabled; behaviour.StartCoroutine((IEnumerator)upload.Invoke(runtime, null));
            }
        }
        [Serializable] sealed class ReplayProof
        {
            public string verificationId, batchId, eventId, sha256;
            public bool diskRecovery, offlineRetained, uploadAcknowledged, identicalReplayAcknowledged;
        }
        [UnityTest]
        public IEnumerator LanLobby_RecordsRoomSignaturesAndVoluntaryHostExit()
        {
            yield return SceneManager.LoadSceneAsync("Start", LoadSceneMode.Single);
            yield return null;
            Capture();
            session = (Component)Game("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            var host = (Task<bool>)Call(session, "HostLanAsync", (ushort)7789);
            float deadline = Time.realtimeSinceStartup + 30;
            while (!host.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(host.IsCompleted && host.Result, Is.True);
            deadline = Time.realtimeSinceStartup + 20;
            while (!events.Any(e => e.Type == "ROOM_ENTERED") && Time.realtimeSinceStartup < deadline) yield return null;
            var entered = events.Single(e => e.Type == "ROOM_ENTERED");
            Assert.That(entered.Room, Does.Match("^[0-9a-f]{32}$")); Assert.That(entered.Slot, Is.EqualTo(0));
            Assert.That(events.Count(e => e.Type == "ROOM_CREATED"), Is.EqualTo(1));
            var flow = Game("CurtainCall.StartLobby.StartLobbyFlow").GetMethod("EnsureExists").Invoke(null, null);
            Assert.That(Call(flow, "OpenLobbyAsHost"), Is.EqualTo(true));
            Component board = null; deadline = Time.realtimeSinceStartup + 30;
            while ((board == null || SceneManager.GetActiveScene().name != "Lobby") && Time.realtimeSinceStartup < deadline)
            { board = UnityEngine.Object.FindAnyObjectByType(Game("CurtainCall.StartLobby.LobbyReadyBoard")) as Component; yield return null; }
            Assert.That(board, Is.Not.Null); yield return new WaitForSecondsRealtime(1);
            Call(board, "Use"); yield return null; Call(board, "Use"); yield return null;
            var signatures = events.Where(e => e.Type == "SIGNATURE_CHANGED").ToArray();
            Assert.That(signatures.Length, Is.EqualTo(2));
            Assert.That(signatures[0].Payload["signed"].ToString().ToLowerInvariant(), Is.EqualTo("true"));
            Assert.That(signatures[1].Payload["signed"].ToString().ToLowerInvariant(), Is.EqualTo("false"));
            Assert.That(signatures.All(e => e.Room == entered.Room), Is.True);
            // Two real public damage operations in one frame must remain two distinct changes.
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var part = Game("CurtainCall.Player.BodyPart");
            Call(player, "ServerCutPart", Enum.Parse(part, "LeftLeg"));
            Call(player, "ServerCutPart", Enum.Parse(part, "RightLeg"));
            var injuries = events.Where(e => e.Type == "INJURY_CHANGED").ToArray();
            Assert.That(injuries.Length, Is.EqualTo(2));
            Assert.That(injuries[0].Payload["afterLostCount"].ToString(), Is.EqualTo("1"));
            Assert.That(injuries[1].Payload["beforeLostCount"].ToString(), Is.EqualTo("1"));
            Assert.That(injuries[1].Payload["afterLostCount"].ToString(), Is.EqualTo("2"));
            var leaving = (Task)Call(session, "LeaveAsync"); deadline = Time.realtimeSinceStartup + 20;
            while ((!leaving.IsCompleted || !events.Any(e => e.Type == "PLAYER_EXITED")) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(events.Single(e => e.Type == "PLAYER_EXITED").Payload["reason"].ToString(), Is.EqualTo("HOST_CLOSED"));
            Assert.That(Read(runtime, "CurrentRoomId"), Is.Null);
            Assert.That(events.Single(e => e.Type == "PLAYER_EXITED").Slot, Is.EqualTo(0));
        }
        [UnityTest]
        public IEnumerator Tightrope_PublicSettlement_UsesSharedAttemptAndSingleHostOutcome()
        {
            yield return SceneManager.LoadSceneAsync("Tightrope", LoadSceneMode.Single); yield return null;
            yield return RemoveAutomaticSceneRouting();
            Capture();
            session = (Component)Game("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            minimumPlayers = (IDisposable)Call(session, "RequestMinimumStartPlayers", 1);
            var host = (Task<bool>)Call(session, "HostLanAsync", (ushort)7789);
            float deadline = Time.realtimeSinceStartup + 15;
            while (!host.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(host.IsCompleted && host.Result, Is.True);
            deadline = Time.realtimeSinceStartup + 15;
            while (!events.Any(e => e.Type == "ROOM_ENTERED") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Call(session, "StartGame"), Is.EqualTo(true)); yield return null;
            var campaign = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Assert.That(campaign, Is.Not.Null); Assert.That(Call(campaign, "EndIntro"), Is.EqualTo(true));
            deadline = Time.realtimeSinceStartup + 15;
            while (!events.Any(e => e.Type == "PERFORMANCE_STARTED") && Time.realtimeSinceStartup < deadline) yield return null;
            var started = events.Single(e => e.Type == "PERFORMANCE_STARTED");
            Assert.That(started.Attempt, Is.Not.Null); Assert.That(started.Run, Is.Not.Null);
            Assert.That(started.Payload.Contains("position"), Is.True);
            Assert.That(started.Payload["positionSource"].ToString(), Is.EqualTo("EMIT_OBSERVATION"));
            var rewards = Static("CurtainCall.Reward.PerformanceRewards", "Instance");
            // A public settlement boundary verifies logging; this is a synthetic result, not a played failure.
            Assert.That(Call(rewards, "FinishPerformance", false, "TELEMETRY_TEST", null), Is.EqualTo(true));
            yield return null;
            var failed = events.Single(e => e.Type == "PERFORMANCE_FAILED");
            Assert.That(failed.Attempt, Is.EqualTo(started.Attempt)); Assert.That(failed.Run, Is.EqualTo(started.Run));
            Assert.That(failed.Authority, Is.EqualTo("HOST"));
            Assert.That(Call(rewards, "FinishPerformance", false, "TELEMETRY_TEST", null), Is.EqualTo(false));
            Assert.That(events.Count(e => e.Type == "PERFORMANCE_FAILED"), Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator WaitingRoom_RecordsTypedFailuresAndActualDeliveryStages()
        {
            yield return SceneManager.LoadSceneAsync("WaitingRoomBase", LoadSceneMode.Single); yield return null;
            // Keep the fixture in this actual scene while exercising production public request methods.
            yield return RemoveAutomaticSceneRouting();
            Capture();
            session = (Component)Game("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            minimumPlayers = (IDisposable)Call(session, "RequestMinimumStartPlayers", 1);
            var host = (Task<bool>)Call(session, "HostLanAsync", (ushort)7789);
            float deadline = Time.realtimeSinceStartup + 20;
            while ((!host.IsCompleted || !events.Any(e => e.Type == "ROOM_ENTERED")) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(host.IsCompleted && host.Result, Is.True);
            Assert.That(Call(session, "StartGame"), Is.EqualTo(true)); yield return null;
            var campaign = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Assert.That(Call(campaign, "EndIntro"), Is.EqualTo(true));
            Assert.That(Call(campaign, "EndPerformance"), Is.EqualTo(true));
            yield return new WaitForSecondsRealtime(.8f);
            var shop = Static("CurtainCall.WaitingRoom.WaitingRoomShop", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var service = Static("CurtainCall.WaitingRoom.ExpertService", "Instance");
            Assert.That(shop, Is.Not.Null); Assert.That(carry, Is.Not.Null); Assert.That(service, Is.Not.Null);
            Assert.That(Read(shop, "HasAuthority"), Is.EqualTo(true));
            Call(shop, "RequestPurchase", "telemetry-unknown-item");
            Call(shop, "RequestPurchase", "telemetry-unknown-item");
            var rejected = events.Where(e => e.Type == "PURCHASE_FAILED").ToArray();
            Assert.That(rejected.Length, Is.EqualTo(2), "Repeated rejected actions are distinct attempts.");
            Assert.That(rejected.All(e => e.Authority == "HOST" && e.Slot == 0 && e.Payload["reason"].ToString() == "SHOP_UnknownItem"), Is.True);
            Call(shop, "RequestClaim", 9999);
            Assert.That(events.Single(e => e.Type == "COLLECTION_FAILED").Payload["reason"].ToString(), Is.EqualTo("SHOP_NotFound"));
            Call(carry, "RequestShelf");
            Assert.That(events.Single(e => e.Type == "STORAGE_FAILED").Payload["reason"].ToString(), Is.EqualTo("CARRY_ShelfEmpty"));
            Call(carry, "RequestPickUp", 9999);
            Assert.That(events.Single(e => e.Type == "INTERACTION_FAILED").Payload["operation"].ToString(), Is.EqualTo("PICK_UP"));
            Call(service, "RequestMedical"); Call(service, "RequestEnter");
            var failures = events.Where(e => e.Type == "SERVICE_FAILED").ToArray();
            Assert.That(failures.Length, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { "MEDICAL_RESERVE", "MEDICAL_ENTER" }, failures.Select(e => e.Payload["operation"].ToString()).ToArray());
            Assert.That(failures.All(e => e.Payload["reason"].ToString().StartsWith("EXPERT_")), Is.True);
            var rewards = Static("CurtainCall.Reward.PerformanceRewards", "Instance");
            var model = rewards.GetType().GetField("model", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(rewards);
            // Synthetic test funds only; purchase/animation/claim/storage below use unchanged production code.
            model.GetType().GetProperty("Balance").SetValue(model, 1000L);
            var item = ((IEnumerable)Read(shop, "Catalog")).Cast<object>().First();
            string itemId = (string)Read(item, "Id");
            Call(shop, "RequestPurchase", itemId);
            yield return null;
            Assert.That(events.Count(e => e.Type == "DELIVERY_DISPATCHED"), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Type == "DELIVERY_AVAILABLE"), Is.Zero, "Ledger arrival is not animation completion.");
            yield return new WaitForSecondsRealtime(1);
            var purchase = events.Single(e => e.Type == "PURCHASE_COMPLETED");
            var dispatched = events.Single(e => e.Type == "DELIVERY_DISPATCHED");
            var available = events.Single(e => e.Type == "DELIVERY_AVAILABLE");
            Assert.That(dispatched.Payload["flowId"].ToString(), Is.EqualTo(purchase.Payload["flowId"].ToString()));
            Assert.That(available.Payload["flowId"].ToString(), Is.EqualTo(purchase.Payload["flowId"].ToString()));
            Assert.That(dispatched.Payload["purchaseToDispatchMs"], Is.Not.Null);
            Assert.That(Convert.ToDouble(dispatched.Payload["purchaseToDispatchMs"]), Is.GreaterThanOrEqualTo(0));
            Assert.That(Convert.ToDouble(available.Payload["dispatchToAvailableMs"]), Is.GreaterThan(0));
            var delivery = ((IEnumerable)Read(shop, "Waiting")).Cast<object>().Single();
            int order = (int)Read(delivery, "Order");
            Call(shop, "RequestClaim", order); yield return null;
            // Read the actual authored camera volumes. This is synthetic movement, not a physical-input test.
            var local = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = local.GetComponent(Game("CurtainCall.Player.PlayerMover"));
            var volumes = UnityEngine.Object.FindObjectsByType(Game("CurtainCall.Cameras.CameraViewZone"), FindObjectsSortMode.None).Cast<Component>().ToArray();
            Assert.That(volumes.Count(v => v.transform.parent?.name == "CameraZones"), Is.EqualTo(6));
            void MoveToRoom(string name)
            {
                var box = volumes.Single(v => v.name == name).GetComponent<BoxCollider>();
                var position = box.transform.TransformPoint(box.center + Vector3.down * (box.size.y * .5f - .1f));
                mover.GetType().GetMethod("SetStartPose", new[] { typeof(Vector3), typeof(Quaternion) }).Invoke(mover, new object[] { position, Quaternion.identity });
            }
            MoveToRoom("Zone_Dormitory"); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(events.Any(e => e.Type == "ZONE_ENTERED" && e.Payload["zoneType"].ToString() == "BEDROOM"), Is.True);
            Assert.That(events.Any(e => e.Type == "STORAGE_REACHED"), Is.False);
            MoveToRoom("Zone_PropRoom"); yield return new WaitForSecondsRealtime(.2f);
            var arrival = events.Single(e => e.Type == "STORAGE_REACHED");
            Assert.That(arrival.Payload["flowId"].ToString(), Is.EqualTo(purchase.Payload["flowId"].ToString()));
            Assert.That(arrival.Payload["boundarySource"].ToString(), Is.EqualTo("CAMERA_ZONE_VOLUME"));
            Assert.That(Convert.ToDouble(arrival.Payload["durationMs"]), Is.GreaterThan(0));
            MoveToRoom("Zone_Dormitory"); yield return new WaitForSecondsRealtime(.2f);
            MoveToRoom("Zone_PropRoom"); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(events.Count(e => e.Type == "ZONE_ENTERED" && e.Payload["zoneType"].ToString() == "BEDROOM"), Is.EqualTo(2));
            Assert.That(events.Where(e => e.Type == "ZONE_EXITED" && e.Payload["zoneType"].ToString() == "BEDROOM").All(e => Convert.ToDouble(e.Payload["durationMs"]) > 0), Is.True);
            Assert.That(events.Count(e => e.Type == "STORAGE_REACHED"), Is.EqualTo(1), "Revisiting a room does not create another delivery arrival.");
            Call(carry, "RequestShelf"); yield return null;
            Assert.That(events.Single(e => e.Type == "ITEM_COLLECTED").Payload["flowId"].ToString(), Is.EqualTo(purchase.Payload["flowId"].ToString()));
            Assert.That(events.Single(e => e.Type == "ITEM_STORED").Payload["flowId"].ToString(), Is.EqualTo(purchase.Payload["flowId"].ToString()));
            Assert.That(events.Single(e => e.Type == "ITEM_COLLECTED").Payload["availableToCollectionMs"], Is.Not.Null);
            Assert.That(events.Single(e => e.Type == "ITEM_COLLECTED").Authority, Is.EqualTo("LOCAL"), "Host collection keeps one local usage event.");
            Assert.That(events.Single(e => e.Type == "ITEM_COLLECTED").Payload["source"].ToString(), Is.EqualTo("LOCAL_RECEIPT_SNAPSHOT"));
            Assert.That(events.Single(e => e.Type == "ITEM_STORED").Payload["storageArrivalToStoreMs"], Is.Not.Null);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(events.Count(e => e.Type == "DELIVERY_AVAILABLE"), Is.EqualTo(1));
            Assert.That(events.Any(e => e.Type == "ZONE_ENTERED" && e.Payload["spawnToZoneMs"] != null), Is.True);
            // Exercise the real host Tick timeout. State after timeout has already cleared its timer.
            Call(campaign, "DebugSetRemaining", .15f);
            deadline = Time.realtimeSinceStartup + 5;
            while (!events.Any(e => e.Type == "RECALL_EXPIRED") && Time.realtimeSinceStartup < deadline) yield return null;
            var expired = events.Single(e => e.Type == "RECALL_EXPIRED");
            Assert.That(events.Count(e => e.Type == "BREAK_ENDED" && e.Break == "D1T2"), Is.EqualTo(1));
            Assert.That(expired.Break, Is.EqualTo("D1T2"));
            Assert.That(expired.Payload["timerSource"].ToString(), Is.EqualTo("HOST_TICK_EXPIRED"));
            var snapshot = Read(campaign, "State");
            Assert.That(snapshot.GetType().GetField("Phase").GetValue(snapshot).ToString(), Is.EqualTo("Performance"));
            Assert.That(Call(campaign, "EndPerformance"), Is.EqualTo(true)); yield return null;
            Call(local, "ServerCut", 2);
            yield return new WaitForSecondsRealtime(.2f);
            Call(service, "RequestMedical"); yield return null;
            var reservation = events.Single(e => e.Type == "SERVICE_RESERVED");
            Call(service, "RequestEnter");
            deadline = Time.realtimeSinceStartup + 12;
            while (!events.Any(e => e.Type == "RESTORATION_VERIFIED") && Time.realtimeSinceStartup < deadline) yield return null;
            var returned = events.Single(e => e.Type == "MEDICAL_RETURNED");
            Assert.That(returned.Payload["returnSource"].ToString(), Is.EqualTo("LOCAL_POSE_APPLIED"));
            Assert.That(returned.Payload["flowId"].ToString(), Is.EqualTo(reservation.Payload["flowId"].ToString()));
            Assert.That(Convert.ToDouble(returned.Payload["enteredToReturnMs"]), Is.GreaterThan(0));
            Assert.That(returned.Payload["restorationToReturnMs"], Is.Not.Null);
            Assert.That(events.Single(e => e.Type == "RESTORATION_COMPLETED").Payload["enteredToRestoreMs"], Is.Not.Null);
            var verification = events.Single(e => e.Type == "RESTORATION_VERIFIED");
            foreach (string field in new[] { "movementRestored", "jumpRestored", "capacityRestored" })
                Assert.That(Convert.ToBoolean(verification.Payload[field]), Is.True, field);
            Assert.That(Convert.ToInt32(verification.Payload["lostCount"]), Is.Zero);
            // Last ready player transitions immediately; observe the ready event before that transition.
            MoveToRoom("Zone_Dormitory"); yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(Call(campaign, "SetReady", true), Is.EqualTo(true)); yield return new WaitForSecondsRealtime(.2f);
            var ready = events.Single(e => e.Type == "READY_CHANGED" && Convert.ToBoolean(e.Payload["ready"]));
            Assert.That(ready.Break, Is.EqualTo("D1T3"));
            var gather = UnityEngine.Object.FindAnyObjectByType(Game("CurtainCall.WaitingRoom.StageEntranceGather")) as Component;
            var exit = UnityEngine.Object.FindAnyObjectByType(Game("CurtainCall.WaitingRoom.WaitingRoomExitWatch")) as Component;
            var entranceBox = gather.GetComponent<BoxCollider>();
            var exitBoxes = exit.GetComponentsInChildren<BoxCollider>().Where(b => b.enabled).ToArray();
            var outPosition = new Vector3(exitBoxes.Max(b => b.bounds.max.x) + .5f, local.transform.position.y, local.transform.position.z);
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            mover.GetType().GetMethod("SetStartPose", new[] { typeof(Vector3), typeof(Quaternion) }).Invoke(mover, new object[] { outPosition, Quaternion.identity });
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(events.Count(e => e.Type == "FIRST_PLAYER_LEFT" && e.Break == "D2T1"), Is.EqualTo(1), "Observe inside, then cross every exit volume before assembly.");
            Call(campaign, "DebugSetRemaining", 10f); yield return null;
            var destination = entranceBox.bounds.center; destination.y = entranceBox.bounds.min.y + .1f;
            mover.GetType().GetMethod("SetStartPose", new[] { typeof(Vector3), typeof(Quaternion) }).Invoke(mover, new object[] { destination, Quaternion.identity });
            deadline = Time.realtimeSinceStartup + 5;
            while (!events.Any(e => e.Type == "ALL_PLAYERS_ASSEMBLED") && Time.realtimeSinceStartup < deadline) yield return null;
            var assembled = events.Single(e => e.Type == "ALL_PLAYERS_ASSEMBLED");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(assembled.Break, Is.EqualTo("D2T1"));
            Assert.That(assembled.Payload["firstLeftToAssemblyMs"], Is.Not.Null);
            Assert.That(Convert.ToDouble(assembled.Payload["firstLeftToAssemblyMs"]), Is.GreaterThan(0));
            Assert.That(Convert.ToDouble(assembled.Payload["alarmToAssemblyMs"]), Is.GreaterThanOrEqualTo(0));
            Assert.That(events.Count(e => e.Type == "FIRST_PLAYER_LEFT" && e.Break == "D2T1"), Is.EqualTo(1));
            var entrance = events.Single(e => e.Type == "STAGE_ENTRANCE_REACHED" && e.Break == "D2T1");
            Assert.That(entrance.Payload["readyToEntranceMs"], Is.Not.Null);
            Assert.That(Convert.ToDouble(entrance.Payload["readyToEntranceMs"]), Is.GreaterThan(0));
            Assert.That(events.Count(e => e.Type == "RECALL_EXPIRED"), Is.EqualTo(1), "Early assembly must not appear as a second timeout.");
            Assert.That(Call(campaign, "EndPerformance"), Is.EqualTo(true));
            Assert.That(Call(campaign, "NotifyAllAtStageEntrance"), Is.EqualTo(true));
            Assert.That(Call(campaign, "EndPerformance"), Is.EqualTo(true)); yield return null;
            // Day 2 night: facility purchase is confirmed on the host and must not create a medical workflow.
            var adjustment = Enum.Parse(service.GetType().GetMethod("RequestFacility").GetParameters()[0].ParameterType, "Strengthen");
            Call(service, "RequestFacility", adjustment);
            var facility = events.Single(e => e.Type == "SERVICE_RESERVED" && e.Payload["serviceType"].ToString() == "FACILITY");
            Assert.That(facility.Authority, Is.EqualTo("HOST"));
            Assert.That(Convert.ToInt32(facility.Payload["appliesOnDay"]), Is.EqualTo(3));
            Assert.That(Convert.ToInt64(facility.Payload["amount"]), Is.EqualTo(Convert.ToInt64(Read(service, "FacilityPrice"))));
        }
        [UnityTest]
        public IEnumerator Cannon_ActualHazardContactsPreserveObstacleIdentityAndDoNotRepeatWhileTouching()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return RemoveAutomaticSceneRouting(); Capture();
            session = (Component)Game("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            minimumPlayers = (IDisposable)Call(session, "RequestMinimumStartPlayers", 1);
            var host = (Task<bool>)Call(session, "HostLanAsync", (ushort)7789);
            float deadline = Time.realtimeSinceStartup + 20;
            while ((!host.IsCompleted || !events.Any(e => e.Type == "ROOM_ENTERED")) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(host.IsCompleted && host.Result, Is.True);
            Assert.That(Call(session, "StartGame"), Is.EqualTo(true)); yield return null;
            var campaign = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Call(campaign, "EndIntro"); yield return null;
            Call(campaign, "EndPerformance"); yield return null;
            Call(campaign, "NotifyAllAtStageEntrance");
            deadline = Time.realtimeSinceStartup + 10;
            while (!events.Any(e => e.Type == "PERFORMANCE_STARTED" && e.Payload["trickType"].ToString() == "CANNON") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(events.Any(e => e.Type == "PERFORMANCE_STARTED" && e.Payload["trickType"].ToString() == "CANNON"), Is.True);
            var hazards = (Component)Static("CurtainCall.HumanCannon.HumanCannonHazardCourse", "Current");
            var all = ((IEnumerable)hazards.GetType().GetField("hazards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hazards)).Cast<Component>().ToArray();
            var saw = all.First(h => Read(h, "Kind").ToString() == "GroundSaw"
                && (bool)Call(hazards, "IsHazardEnabled", Array.IndexOf(all, h)));
            foreach (Behaviour h in all) h.enabled = h == saw;
            saw.GetType().GetField("travel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(saw, Vector3.zero);
            var local = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = local.GetComponent(Game("CurtainCall.Player.PlayerMover"));
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            foreach (string type in new[] { "CurtainCall.Player.PlayerController", "CurtainCall.Player.PlayerInputReader" })
                if (local.GetComponent(Game(type)) is Behaviour input) input.enabled = false;
            deadline = Time.realtimeSinceStartup + 5;
            while (!(bool)Read(hazards, "IsSynchronized") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Read(hazards, "IsSynchronized"), Is.EqualTo(true));
            ((IDictionary)hazards.GetType().GetField("restartPositions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hazards)).Clear();
            var contact = saw.transform.position - Vector3.up * .4f;
            void Move(Vector3 position) => mover.GetType().GetMethod("SetStartPose", new[] { typeof(Vector3), typeof(Quaternion) }).Invoke(mover, new object[] { position, Quaternion.identity });
            Move(contact);
            deadline = Time.realtimeSinceStartup + 5;
            while (!events.Any(e => e.Type == "LIMB_SEVERED") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(events.Count(e => e.Type == "LIMB_SEVERED"), Is.EqualTo(1),
                "Actual contact; state=" + Read(local, "State") + ", lost=" + Read(local, "LostParts") + ", active=" + ((Behaviour)saw).isActiveAndEnabled
                + ", enabled=" + Call(hazards, "IsHazardEnabled", Array.IndexOf(all, saw))
                + ", resolved=" + hazards.GetType().GetProperty("Resolved", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hazards)
                + ", touches=" + Call(saw, "Touches", contact, Read(mover, "BodyHeight"), Read(mover, "BodyRadius"), Read(hazards, "Elapsed"), 1f));
            var cut = events.Single(e => e.Type == "LIMB_SEVERED");
            string obstacle = (string)Game("CurtainCall.Telemetry.Pipeline.DetailLogIds").GetMethod("ObjectId").Invoke(null, new object[] { saw, "OB_" });
            Assert.That(cut.Payload["obstacleId"].ToString(), Is.EqualTo(obstacle));
            Assert.That(cut.Payload["cause"].ToString(), Is.EqualTo("CANNON_GROUNDSAW"));
            var contactEvent = events.Single(e => e.Type == "CANNON_HAZARD_CONTACT");
            Assert.That(contactEvent.Payload["obstacleId"].ToString(), Is.EqualTo(obstacle));
            Assert.That(Convert.ToBoolean(contactEvent.Payload["cutApplied"]), Is.True);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(events.Count(e => e.Type == "LIMB_SEVERED"), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Type == "CANNON_HAZARD_CONTACT"), Is.EqualTo(1));
            Move(contact + Vector3.right * 8); yield return new WaitForSecondsRealtime(.1f);
            Move(contact); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(events.Count(e => e.Type == "LIMB_SEVERED"), Is.EqualTo(2));
            Assert.That(events.Where(e => e.Type == "LIMB_SEVERED").All(e => e.Payload["obstacleId"].ToString() == obstacle), Is.True);
            for (int count = 3; count <= 4; count++)
            {
                Move(contact + Vector3.right * 8); yield return new WaitForSecondsRealtime(.1f);
                Move(contact); yield return new WaitForSecondsRealtime(.2f);
            }
            Assert.That(events.Count(e => e.Type == "LIMB_SEVERED"), Is.EqualTo(4));
            Assert.That(events.Count(e => e.Type == "CANNON_HAZARD_CONTACT"), Is.EqualTo(4));
            var death = events.Single(e => e.Type == "PLAYER_DIED");
            Assert.That(death.Payload["cause"].ToString(), Is.EqualTo("CANNON_GROUNDSAW"));
            Assert.That(death.Payload["obstacleId"].ToString(), Is.EqualTo(obstacle));
            Assert.That(death.Attempt, Is.EqualTo(cut.Attempt));
        }
        [UnityTest] public IEnumerator DeathRagdoll_TightropeStaysDetachedAndFalls() => DeathRagdoll(false);
        [UnityTest] public IEnumerator DeathRagdoll_CannonStaysDetachedAndFalls() => DeathRagdoll(true);
        IEnumerator DeathRagdoll(bool cannon)
        {
            yield return SceneManager.LoadSceneAsync(cannon ? "HumanCannon" : "Tightrope", LoadSceneMode.Single);
            yield return null; yield return RemoveAutomaticSceneRouting(); Capture();
            session = (Component)Game("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            minimumPlayers = (IDisposable)Call(session, "RequestMinimumStartPlayers", 1);
            var host = (Task<bool>)Call(session, "HostLanAsync", (ushort)7789);
            float until = Time.realtimeSinceStartup + 15;
            while ((!host.IsCompleted || !events.Any(e => e.Type == "ROOM_ENTERED")) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(host.IsCompleted && host.Result, Is.True);
            Call(session, "StartGame"); yield return null;
            var campaign = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Call(campaign, "EndIntro"); yield return null;
            if (cannon) { Call(campaign, "EndPerformance"); yield return null; Call(campaign, "NotifyAllAtStageEntrance"); }
            until = Time.realtimeSinceStartup + 12;
            while (!events.Any(e => e.Type == "PERFORMANCE_STARTED" && e.Payload["trickType"].ToString() == (cannon ? "CANNON" : "TIGHTROPE")) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(events.Any(e => e.Type == "PERFORMANCE_STARTED"), Is.True);
            var local = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = local.GetComponent(Game("CurtainCall.Player.PlayerMover"));
            var balance = local.GetComponent(Game("CurtainCall.Player.PlayerBalance"));
            var ragdoll = local.GetComponent(Game("CurtainCall.Player.PlayerRagdoll"));
            var slot = local.GetComponent(Game("CurtainCall.Player.PlayerModelSlot"));
            yield return new WaitForSecondsRealtime(.5f);
            // Public confirmed death; the live course/controller remains enabled during the observation.
            var model = (GameObject)Read(slot, "CurrentModel"); Assert.That(model, Is.Not.Null);
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            local.transform.position = new Vector3(1000f, 30f, 1000f);
            Call(local, "ServerKill");
            Assert.That(Read(local, "State").ToString(), Is.EqualTo("Fallen"));
            Assert.That(Read(ragdoll, "IsLimp"), Is.EqualTo(true), "Confirmed death must detach the model immediately");
            Assert.That(Read(slot, "CurrentModel"), Is.Null);
            var body = model.GetComponent<Rigidbody>(); Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.False); Assert.That(body.useGravity, Is.True);
            float startY = body.position.y;
            yield return new WaitForSeconds(.6f);
            Assert.That(Read(ragdoll, "IsLimp"), Is.EqualTo(true), "Course cleanup must not revive a dead model");
            Assert.That(Read(slot, "CurrentModel"), Is.Null);
            Assert.That(model, Is.Not.Null);
            Assert.That(body.position.y, Is.LessThan(startY - .5f), "Detached body must keep falling under gravity");
            var guard = Game("CurtainCall.Network.PlayerSync.NetworkPlayer").GetProperty("ServerCanChangeState");
            object previousGuard = guard.GetValue(null);
            try
            {
                // Isolate confirmed death presentation after the solo course has already ended.
                guard.SetValue(null, null);
                foreach (string cause in cannon ? new[] { "CUT" } : new[] { "CUT", "BALANCE" })
                {
                    Call(local, "ServerRestoreAll"); yield return null;
                    Assert.That(Read(ragdoll, "IsLimp"), Is.EqualTo(false), "An explicit revive still restores the model");
                    local.transform.position = new Vector3(1000f, 30f, 1000f);
                    model = (GameObject)Read(slot, "CurrentModel"); Assert.That(model, Is.Not.Null);
                    if (cause == "CUT") Call(local, "ServerCut", 4);
                    else Call(balance, "ForceFall");
                    Assert.That(Read(local, "State").ToString(), Is.EqualTo("Fallen"), cause);
                    Assert.That(Read(ragdoll, "IsLimp"), Is.EqualTo(true), cause);
                    body = model.GetComponent<Rigidbody>(); Assert.That(body, Is.Not.Null, cause);
                    startY = body.position.y;
                    // Neither a repeated balance reset nor duplicate death confirmation may revive the corpse.
                    Call(balance, "ResetBalance");
                    yield return new WaitForSeconds(.6f);
                    Assert.That(Read(ragdoll, "IsLimp"), Is.EqualTo(true), cause);
                    Assert.That(Read(slot, "CurrentModel"), Is.Null, cause);
                    Assert.That(body.useGravity && !body.isKinematic, Is.True, cause);
                    Assert.That(body.position.y, Is.LessThan(startY - .5f), cause);
                }
            }
            finally { guard.SetValue(null, previousGuard); }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return RemoveAutomaticSceneRouting();
            if (runtime != null && listener != null) runtime.GetType().GetEvent("Recorded").RemoveEventHandler(runtime, listener);
            if (session != null && Read(session, "ConnectionState").ToString() != "Offline")
            { var task = (Task)Call(session, "LeaveAsync"); float until = Time.realtimeSinceStartup + 10; while (!task.IsCompleted && Time.realtimeSinceStartup < until) yield return null; }
            events.Clear(); listener = null;
            minimumPlayers?.Dispose(); minimumPlayers = null;
#if UNITY_EDITOR
            if (logTagPreference != null) UnityEditor.EditorPrefs.SetBool(logTagPreference, previousLogTag);
#endif
        }
        sealed class Observation { public string Type, Room, Attempt, Run, Break, Authority; public int? Slot; public IDictionary Payload; }
    }
}
