using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CurtainCall.Telemetry.Pipeline.EditorTests
{
    public sealed class DurableOutboxTests
    {
        string directory;
        [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "curtaincall-telemetry-tests", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown()
        {
            string expectedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "curtaincall-telemetry-tests")) + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(directory).StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        DurableOutbox New() => new(directory, Guid.NewGuid().ToString("D"));
        static LogEvent Event() => new() { sequence = 1, eventType = "GAME_STARTED", occurredAt = "2026-10-21T08:00:00.000Z" };

        [Test] public void MppmQueues_DoNotOverwriteParentOrEachOther_AndRecoverIndependently()
        {
            string mainRoot = TelemetryRuntime.StorageDirectory(directory, "D:/Game/Assets");
            string cloneRoot = TelemetryRuntime.StorageDirectory(directory, "D:/Game/Library/VP/mppm1/Assets");
            string otherRoot = TelemetryRuntime.StorageDirectory(directory, "D:/Game/Library/VP/mppm2/Assets");
            Assert.That(mainRoot, Is.EqualTo(Path.Combine(directory, "TelemetryPipeline")));
            Assert.That(cloneRoot, Is.Not.EqualTo(mainRoot));
            Assert.That(otherRoot, Is.Not.EqualTo(cloneRoot));
            Assert.That(TelemetryRuntime.StorageDirectory(directory, "d:\\game\\library\\vp\\mppm1\\assets"), Is.EqualTo(cloneRoot));
            var main = new DurableOutbox(mainRoot, Guid.NewGuid().ToString("D"));
            var clone = new DurableOutbox(cloneRoot, Guid.NewGuid().ToString("D"));
            var parentEvent = Event(); var cloneEvent = Event();
            main.Append(parentEvent); clone.Append(cloneEvent);
            main.Seal("CHECKPOINT"); clone.Seal("CHECKPOINT");
            Assert.That(main.PendingBatchCount, Is.EqualTo(1));
            Assert.That(clone.PendingBatchCount, Is.EqualTo(1));
            Assert.That(main.SenderId, Is.Not.EqualTo(clone.SenderId));
            var recovered = new DurableOutbox(cloneRoot, Guid.NewGuid().ToString("D"));
            Assert.That(LogJson.Parse<LogBatch>(File.ReadAllText(main.NextPath())).events[0].eventId, Is.EqualTo(parentEvent.eventId));
            Assert.That(LogJson.Parse<LogBatch>(File.ReadAllText(recovered.NextPath())).events[0].eventId, Is.EqualTo(cloneEvent.eventId));
            string pending = recovered.NextPath(), raw = File.ReadAllText(pending);
            var batch = LogJson.Parse<LogBatch>(raw);
            Assert.That(recovered.Acknowledge(pending, batch.batchId, DurableOutbox.Sha256(raw), 1), Is.True);
            Assert.That(recovered.PendingBatchCount, Is.Zero);
            Assert.That(main.PendingBatchCount, Is.EqualTo(1));
        }

        [Test] public void CrashRecovery_RetainsOriginalAppAndEventIds()
        {
            var first = New(); var entry = Event(); first.Append(entry);
            var recovered = New(); string path = recovered.NextPath();
            Assert.That(path, Is.Not.Null);
            var batch = LogJson.Parse<LogBatch>(File.ReadAllText(path));
            Assert.That(batch.appSessionId, Is.EqualTo(first.AppSessionId));
            Assert.That(batch.events[0].eventId, Is.EqualTo(entry.eventId));
            Assert.That(recovered.SenderId, Is.EqualTo(first.SenderId));
        }
        [Test] public void NewPlayUploadsBeforeHistoricalBacklog_WithoutDiscardingOlderBatches()
        {
            var old = New(); old.Append(Event()); old.Seal("CHECKPOINT");
            string oldPath = old.NextPath(), original = File.ReadAllText(oldPath);
            var current = New(); var entry = Event(); current.Append(entry); current.Seal("SCENE_CHANGED");
            string pending = current.NextPath(), raw = File.ReadAllText(pending);
            var batch = LogJson.Parse<LogBatch>(raw);
            Assert.That(batch.events[0].eventId, Is.EqualTo(entry.eventId));
            Assert.That(current.PendingBatchCount, Is.EqualTo(2));
            Assert.That(current.Acknowledge(pending, batch.batchId, DurableOutbox.Sha256(raw), 1), Is.True);
            Assert.That(current.NextPath(), Is.EqualTo(oldPath));
            Assert.That(File.ReadAllText(oldPath), Is.EqualTo(original));
        }
        [Test] public void Ack_RequiresMatchingIdCountAndContentHash()
        {
            var queue = New(); queue.Append(Event()); queue.Seal("SCENE_CHANGED");
            string path = queue.NextPath(), json = File.ReadAllText(path);
            var batch = LogJson.Parse<LogBatch>(json);
            Assert.That(queue.Acknowledge(path, Guid.NewGuid().ToString("D"), DurableOutbox.Sha256(json), 1), Is.False);
            Assert.That(queue.Acknowledge(path, batch.batchId, "invalid", 1), Is.False);
            Assert.That(queue.Acknowledge(path, batch.batchId, DurableOutbox.Sha256(json), 2), Is.False);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(queue.Acknowledge(path, batch.batchId, DurableOutbox.Sha256(json), 1), Is.True);
            Assert.That(File.Exists(path), Is.False);
        }
        [Test] public void Ack_DoesNotDeleteNewActiveEvents()
        {
            var queue = New(); queue.Append(Event()); queue.Seal("CHECKPOINT");
            string path = queue.NextPath(), json = File.ReadAllText(path); var batch = LogJson.Parse<LogBatch>(json);
            queue.Append(Event());
            Assert.That(queue.Acknowledge(path, batch.batchId, DurableOutbox.Sha256(json), 1), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "active.json")), Is.True);
            Assert.That(New().NextPath(), Is.Not.Null);
        }
        [Test] public void CrashBetweenCopyAndDelete_DoesNotConflictOrDuplicate()
        {
            var queue = New(); queue.Append(Event()); queue.Seal("CHECKPOINT");
            string final = queue.NextPath();
            File.Copy(final, Path.Combine(directory, "active.json"));
            Assert.DoesNotThrow(() => New());
            Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));
        }
        [Test] public void CorruptActive_IsRetainedForInspection()
        {
            New(); string path = Path.Combine(directory, "active.json"); File.WriteAllText(path, "broken json");
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => New());
            Assert.That(File.ReadAllText(path), Is.EqualTo("broken json"));
        }
        [Test] public void SharedRunId_IsStableAcrossClientsAndChangesPerGeneration()
        {
            Assert.That(TelemetryRuntime.SharedRunId("shared", 1), Is.EqualTo(TelemetryRuntime.SharedRunId("shared", 1)));
            Assert.That(TelemetryRuntime.SharedRunId("shared", 1), Is.Not.EqualTo(TelemetryRuntime.SharedRunId("shared", 2)));
        }
        [Test] public void CallerMutation_CannotRewriteAcceptedEvent()
        {
            var queue = New(); var entry = Event(); entry.payload["sceneName"] = "Start"; queue.Append(entry);
            entry.eventType = "CHANGED"; entry.payload["sceneName"] = "Changed";
            queue.Seal("CHECKPOINT");
            string json = File.ReadAllText(queue.NextPath());
            Assert.That(json, Does.Contain("GAME_STARTED"));
            Assert.That(json, Does.Not.Contain("CHANGED"));
            Assert.That(json, Does.Not.Contain("Changed"));
        }
        void AssertIndex(DurableOutbox queue)
        {
            long actual = Directory.EnumerateFiles(directory, "*.json").Sum(p => new FileInfo(p).Length);
            long indexed = (long)typeof(DurableOutbox).GetMethod("PendingBytes", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(queue, null);
            Assert.That(indexed, Is.EqualTo(actual));
            Assert.That(queue.PendingBatchCount, Is.EqualTo(Directory.EnumerateFiles(directory, "*.json")
                .Count(p => Guid.TryParseExact(Path.GetFileNameWithoutExtension(p), "D", out _))));
        }

        [Test] public void FileIndex_TracksUtf8OverwriteSealAckAndRecovery()
        {
            var queue = New(); var entry = Event(); entry.payload["text"] = "한글 기록";
            queue.Append(entry); AssertIndex(queue);
            queue.Append(Event()); AssertIndex(queue);
            queue.Seal("CHECKPOINT"); AssertIndex(queue);
            string path = queue.NextPath(), raw = File.ReadAllText(path);
            queue.Append(Event()); AssertIndex(queue);
            var batch = LogJson.Parse<LogBatch>(raw);
            Assert.That(queue.Acknowledge(path, batch.batchId, DurableOutbox.Sha256(raw), batch.events.Count), Is.True);
            AssertIndex(queue);
            var recovered = New(); AssertIndex(recovered);
            Assert.That(recovered.PendingBatchCount, Is.EqualTo(1));
        }

        [Test] public void FileIndex_InitialCapacityCheckRetainsOversizedBacklog()
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Guid.NewGuid().ToString("D") + ".json");
            using (var stream = File.Create(path)) stream.SetLength(64L * 1024 * 1024 + 1);
            var queue = New(); AssertIndex(queue);
            Assert.Throws<IOException>(() => queue.Append(Event()));
            AssertIndex(queue);
            Assert.That(new FileInfo(path).Length, Is.EqualTo(64L * 1024 * 1024 + 1));
            Assert.That(File.Exists(Path.Combine(directory, "active.json")), Is.False);
        }

        [Test] public void FileIndex_HotPathDoesNotRescanDirectoryAndFailedWriteDoesNotAdvanceIndex()
        {
            var queue = New(); queue.Append(Event()); AssertIndex(queue);
            long bytes = new FileInfo(Path.Combine(directory, "active.json")).Length;
            string moved = directory + "-moved";
            Directory.Move(directory, moved);
            try
            {
                Assert.That(queue.PendingBatchCount, Is.Zero);
                Assert.That((long)typeof(DurableOutbox).GetMethod("PendingBytes", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(queue, null), Is.EqualTo(bytes), "입력 경로에서 디렉터리를 다시 읽지 않는다");
                Assert.Throws<DirectoryNotFoundException>(() => queue.Append(Event()));
            }
            finally { Directory.Move(moved, directory); }
            AssertIndex(queue);
            queue.Seal("CHECKPOINT"); AssertIndex(queue);
            Assert.That(LogJson.Parse<LogBatch>(File.ReadAllText(queue.NextPath())).events.Count, Is.EqualTo(1));
        }

        static string Ack(string raw) => LogJson.Serialize(new { ok = true,
            batchId = LogJson.Parse<LogBatch>(raw).batchId, sha256 = DurableOutbox.Sha256(raw),
            acceptedEvents = LogJson.Parse<LogBatch>(raw).events.Count });

        [UnityTest] public IEnumerator AsyncPreparation_PreservesExactBytesAndCurrentSessionPriority()
        {
            var old = New(); old.Append(Event()); old.Seal("CHECKPOINT");
            string historical = old.NextPath(), historicalRaw = File.ReadAllText(historical);
            var queue = New(); var entry = Event(); entry.payload["quoted"] = "한글 \\\" \\n";
            queue.Append(entry); queue.Seal("CHECKPOINT");
            string path = queue.NextPath(), raw = File.ReadAllText(path);
            var work = queue.PrepareUploadAsync("test-placeholder");
            // 준비 중에도 새 active를 기록할 수 있고 요청은 이미 봉인된 파일만 포함한다.
            queue.Append(Event());
            while (!work.IsCompleted) yield return null;
            var result = work.GetAwaiter().GetResult();
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Path, Is.EqualTo(path));
            var body = JObject.Parse(Encoding.UTF8.GetString(result.Body));
            Assert.That(body.Value<string>("batchJson"), Is.EqualTo(raw));
            Assert.That(body.Value<string>("token"), Is.EqualTo("test-placeholder"));
            Assert.That(File.ReadAllText(historical), Is.EqualTo(historicalRaw));
            Assert.That(File.Exists(Path.Combine(directory, "active.json")), Is.True);
        }

        [UnityTest] public IEnumerator AsyncAck_RejectsMalformedMismatchedAndActivePaths()
        {
            var queue = New(); queue.Append(Event()); queue.Seal("CHECKPOINT");
            string path = queue.NextPath(), raw = File.ReadAllText(path);
            var valid = JObject.Parse(Ack(raw));
            var wrongId = (JObject)valid.DeepClone(); wrongId["batchId"] = Guid.NewGuid().ToString("D");
            var wrongHash = (JObject)valid.DeepClone(); wrongHash["sha256"] = "invalid";
            var wrongCount = (JObject)valid.DeepClone(); wrongCount["acceptedEvents"] = 2;
            foreach (string response in new[] { "broken", "{}", "{\"ok\":false}", wrongId.ToString(), wrongHash.ToString(), wrongCount.ToString() })
            {
                var work = queue.AcknowledgeUploadAsync(path, response);
                while (!work.IsCompleted) yield return null;
                Assert.That(work.GetAwaiter().GetResult(), Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo(raw));
            }
            queue.Append(Event());
            string active = Path.Combine(directory, "active.json"), activeRaw = File.ReadAllText(active);
            var activeAck = queue.AcknowledgeUploadAsync(active, Ack(activeRaw));
            while (!activeAck.IsCompleted) yield return null;
            Assert.That(activeAck.GetAwaiter().GetResult(), Is.False);
            Assert.That(File.ReadAllText(active), Is.EqualTo(activeRaw));
        }

        [UnityTest] public IEnumerator AsyncAck_DoesNotBlockAppendSealOrDeleteNewerBatches()
        {
            var queue = New(); queue.Append(Event()); queue.Seal("CHECKPOINT");
            string path = queue.NextPath(), raw = File.ReadAllText(path);
            var work = queue.AcknowledgeUploadAsync(path, Ack(raw));
            for (int i = 0; i < 20; i++)
            { queue.Append(Event()); queue.Seal("CHECKPOINT"); }
            queue.Append(Event());
            while (!work.IsCompleted) yield return null;
            Assert.That(work.GetAwaiter().GetResult(), Is.True);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(queue.PendingBatchCount, Is.EqualTo(20));
            AssertIndex(queue);
            Assert.That(File.Exists(Path.Combine(directory, "active.json")), Is.True);
            Assert.That(New().PendingBatchCount, Is.EqualTo(21), "종료 직전 active도 복구되어야 한다");
        }

        [UnityTest] public IEnumerator AsyncAck_ContentChangedAfterPreparationIsRetained()
        {
            var queue = New(); queue.Append(Event()); queue.Seal("CHECKPOINT");
            string path = queue.NextPath(), raw = File.ReadAllText(path);
            var work = queue.PrepareUploadAsync("test-placeholder");
            while (!work.IsCompleted) yield return null;
            Assert.That(work.GetAwaiter().GetResult().Path, Is.EqualTo(path));
            string changed = raw + " "; File.WriteAllText(path, changed);
            var ack = queue.AcknowledgeUploadAsync(path, Ack(raw));
            while (!ack.IsCompleted) yield return null;
            Assert.That(ack.GetAwaiter().GetResult(), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(changed));
        }

        [UnityTest] public IEnumerator AsyncCancellation_RetainsPendingAndActiveWithoutFaulting()
        {
            var queue = New(); queue.Append(Event()); queue.Seal("CHECKPOINT");
            string path = queue.NextPath(), raw = File.ReadAllText(path);
            queue.Append(Event());
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var preparation = queue.PrepareUploadAsync("test-placeholder", cancellation.Token);
            var ack = queue.AcknowledgeUploadAsync(path, Ack(raw), cancellation.Token);
            while (!preparation.IsCompleted || !ack.IsCompleted) yield return null;
            Assert.That(preparation.GetAwaiter().GetResult().Body, Is.Null);
            Assert.That(ack.GetAwaiter().GetResult(), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(raw));
            Assert.That(File.Exists(Path.Combine(directory, "active.json")), Is.True);
        }

        [UnityTest] public IEnumerator AsyncMutationConflict_FailsWithoutWaitingThenRetries()
        {
            var source = new DurableOutbox(Path.Combine(directory, "client"), Guid.NewGuid().ToString("D"));
            source.Append(new LogEvent { occurredAt = "2026-10-22T00:00:00Z", sequence = 1,
                environment = "TEST", gameVersion = "0.1.0", playerId = "test-player", playerNumber = 1,
                networkClientId = "1", sceneType = "TITLE", eventType = "GAME_STARTED", authority = "LOCAL" });
            source.Seal("CHECKPOINT");
            string raw = File.ReadAllText(source.NextPath());
            var queue = New(); Assert.That(queue.AcceptRelay(raw), Is.True);
            string path = queue.NextPath(), id = LogJson.Parse<LogBatch>(raw).batchId;
            var mutations = (ConcurrentDictionary<string, byte>)typeof(DurableOutbox)
                .GetField("fileMutations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(queue);
            Assert.That(mutations.TryAdd(id, 0), Is.True);
            try
            {
                var preparation = queue.PrepareUploadAsync("test-placeholder");
                var blocked = queue.AcknowledgeUploadAsync(path, Ack(raw));
                while (!blocked.IsCompleted || !preparation.IsCompleted) yield return null;
                Assert.That(preparation.GetAwaiter().GetResult().Body, Is.Null);
                Assert.That(blocked.GetAwaiter().GetResult(), Is.False);
                Assert.That(queue.AcceptRelay(raw), Is.False);
                Assert.That(queue.Acknowledge(path, id, DurableOutbox.Sha256(raw), 1), Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo(raw));
            }
            finally { mutations.TryRemove(id, out _); }
            var retry = queue.AcknowledgeUploadAsync(path, Ack(raw));
            while (!retry.IsCompleted) yield return null;
            Assert.That(retry.GetAwaiter().GetResult(), Is.True);
            Assert.That(queue.PendingBatchCount, Is.Zero);
            Assert.That(source.PendingBatchCount, Is.EqualTo(1));
            AssertIndex(queue);
        }

        [UnityTest] public IEnumerator AsyncPreparation_EmptyAndUnavailableQueueReturnWithoutUnhandledFaults()
        {
            var queue = New();
            var empty = queue.PrepareUploadAsync("test-placeholder");
            while (!empty.IsCompleted) yield return null;
            Assert.That(empty.GetAwaiter().GetResult().Body, Is.Null);
            // 테스트 전용 빈 outbox의 저장소를 제거해 worker 오류 전달을 확인한다.
            Directory.Delete(directory, true);
            var unavailable = queue.PrepareUploadAsync("test-placeholder");
            while (!unavailable.IsCompleted) yield return null;
            var result = unavailable.GetAwaiter().GetResult();
            Assert.That(result.Body, Is.Null);
            Assert.That(result.Error, Is.TypeOf<DirectoryNotFoundException>());
        }

        [Test] public void Recovery_PreservesRecordedVersionsWithoutRelabelingLegacyEvents()
        {
            var queue = New(); var old = Event(); old.gameVersion = "0.1.0";
            queue.Append(old); queue.Seal("CHECKPOINT");
            string path = queue.NextPath(), raw = File.ReadAllText(path);
            old.gameVersion = "0.2.0";
            var recovered = New();
            Assert.That(File.ReadAllText(recovered.NextPath()), Is.EqualTo(raw));
            Assert.That(LogJson.Parse<LogBatch>(raw).events[0].gameVersion, Is.EqualTo("0.1.0"));
            Assert.That(recovered.Acknowledge(path, LogJson.Parse<LogBatch>(raw).batchId, DurableOutbox.Sha256(raw), 1), Is.True);
            recovered.Append(Event()); recovered.Seal("CHECKPOINT");
            Assert.That(LogJson.Parse<LogBatch>(File.ReadAllText(recovered.NextPath())).events[0].gameVersion, Is.Null);
        }
    }
}
