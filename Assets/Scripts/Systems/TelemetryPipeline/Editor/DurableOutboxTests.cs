using System;
using System.IO;
using NUnit.Framework;

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
