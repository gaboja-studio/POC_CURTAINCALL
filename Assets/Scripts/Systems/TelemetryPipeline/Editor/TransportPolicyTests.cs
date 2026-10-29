using System;
using System.IO;
using NUnit.Framework;

namespace CurtainCall.Telemetry.Pipeline.EditorTests
{
    public sealed class TransportPolicyTests
    {
        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, false, false)]
        [TestCase(false, true, true, false)]
        [TestCase(false, false, true, true)]
        public void OnlyHostOrFormerHostUploads(bool host, bool client, bool hosted, bool expected)
            => Assert.That(TransportPolicy.CanUpload(host, client, hosted), Is.EqualTo(expected));

        [Test] public void HostPersistsOriginalClientBatchBeforeReceiptAndDuplicateIsIdempotent()
        {
            string root = Path.Combine(Path.GetTempPath(), "curtaincall-relay-tests", Guid.NewGuid().ToString("N"));
            try
            {
                var client = new DurableOutbox(Path.Combine(root, "client"), Guid.NewGuid().ToString("D"));
                var entry = new LogEvent { occurredAt = "2026-10-22T00:00:00Z", sequence = 1, monotonicMs = 0,
                    environment = "TEST", gameVersion = "0.1.0", playerId = "shared-login", playerNumber = 1,
                    networkClientId = "1", sceneType = "TITLE", eventType = "GAME_STARTED", authority = "LOCAL" };
                client.Append(entry); client.Seal("CHECKPOINT");
                string path = client.NextPath(), raw = File.ReadAllText(path);
                var host = new DurableOutbox(Path.Combine(root, "host"), Guid.NewGuid().ToString("D"));
                Assert.That(host.AcceptRelay(raw), Is.True);
                Assert.That(host.AcceptRelay(raw), Is.True);
                Assert.That(host.PendingBatchCount, Is.EqualTo(1));
                Assert.That(File.ReadAllText(host.NextPath()), Is.EqualTo(raw));
                var recovered = new DurableOutbox(Path.Combine(root, "host"), Guid.NewGuid().ToString("D"));
                var batch = LogJson.Parse<LogBatch>(raw);
                Assert.That(client.Acknowledge(path, batch.batchId, "wrong", 1), Is.False);
                Assert.That(client.Acknowledge(path, batch.batchId, DurableOutbox.Sha256(raw), 1), Is.True);
                Assert.That(File.ReadAllText(recovered.NextPath()), Is.EqualTo(raw));
                batch.events[0].authority = "HOST";
                Assert.That(host.AcceptRelay(LogJson.Serialize(batch)), Is.False);
            }
            finally
            {
                string boundary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "curtaincall-relay-tests")) + Path.DirectorySeparatorChar;
                Assert.That(Path.GetFullPath(root).StartsWith(boundary, StringComparison.OrdinalIgnoreCase), Is.True);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test] public void PackagedConfigurationLoadsWithoutMachineEnvironment()
        {
            string root = Path.Combine(Path.GetTempPath(), "curtaincall-config-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, TelemetryConfiguration.FileName);
            try
            {
                var config = new TelemetryConfiguration { endpoint = "https://script.google.com/macros/s/test/exec", token = new string('x', 32) };
                File.WriteAllText(path, LogJson.Serialize(config));
                Assert.That(TelemetryConfiguration.FromFile(root).IsValid, Is.True);
            }
            finally { File.Delete(path); Directory.Delete(root); }
        }
    }
}
