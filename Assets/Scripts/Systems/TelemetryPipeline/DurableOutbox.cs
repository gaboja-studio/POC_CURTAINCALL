using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Each accepted event is persisted before returning. Upload ACK never clears a newer active batch.</summary>
    public sealed class DurableOutbox
    {
        const int MaxBatchBytes = 100 * 1024;
        const long MaxPendingBytes = 64L * 1024 * 1024;
        readonly string root;
        readonly string activePath;
        readonly string senderId;
        readonly string appSessionId;
        LogBatch active;
        public string SenderId => senderId;
        public string AppSessionId => appSessionId;

        public DurableOutbox(string directory, string applicationSession)
        {
            root = directory;
            Directory.CreateDirectory(root);
            appSessionId = applicationSession;
            string identity = Path.Combine(root, "sender-id.txt");
            if (!File.Exists(identity)) AtomicWrite(identity, Guid.NewGuid().ToString("D"));
            senderId = File.ReadAllText(identity).Trim();
            if (!Guid.TryParseExact(senderId, "D", out _)) throw new InvalidDataException("Invalid telemetry sender identity.");
            activePath = Path.Combine(root, "active.json");
            if (File.Exists(activePath))
            {
                active = LogJson.Parse<LogBatch>(File.ReadAllText(activePath));
                if (active?.schemaVersion != 1 || !Guid.TryParseExact(active.batchId, "D", out _)
                    || active.events == null || active.events.Count > 250)
                    throw new InvalidDataException("Telemetry recovery requires manual inspection; original retained.");
                string recoveredFinal = Path.Combine(root, active.batchId + ".json");
                if (File.Exists(recoveredFinal))
                {
                    if (File.ReadAllText(recoveredFinal) != File.ReadAllText(activePath))
                        throw new InvalidDataException("Telemetry recovery collision; originals retained.");
                    File.Delete(activePath);
                    active = NewBatch();
                }
                else Seal("RECOVERY");
            }
            active = NewBatch();
        }

        LogBatch NewBatch() => new() { senderId = senderId, appSessionId = appSessionId };

        public void Append(LogEvent entry)
        {
            if (Directory.EnumerateFiles(root, "*.json").Sum(f => new FileInfo(f).Length) > MaxPendingBytes)
                throw new IOException("Telemetry outbox full; pending data retained.");
            string eventJson = LogJson.Serialize(entry);
            if (Encoding.UTF8.GetByteCount(eventJson) > 16 * 1024) throw new ArgumentException("Telemetry event too large.");
            if (active.events.Count >= 250 || (active.events.Count > 0
                && Encoding.UTF8.GetByteCount(LogJson.Serialize(active)) + Encoding.UTF8.GetByteCount(eventJson) > MaxBatchBytes))
                Seal("CHECKPOINT");
            // Detach mutable caller payloads before persisting the accepted event snapshot.
            active.events.Add(LogJson.Parse<LogEvent>(eventJson));
            try { AtomicWrite(activePath, LogJson.Serialize(active)); }
            catch { active.events.RemoveAt(active.events.Count - 1); throw; }
        }

        public void Seal(string reason)
        {
            if (active == null || active.events.Count == 0) return;
            active.flushReason = reason;
            AtomicWrite(activePath, LogJson.Serialize(active));
            // Copy + delete is recoverable even if interrupted between the two operations.
            string final = Path.Combine(root, active.batchId + ".json");
            string json = File.ReadAllText(activePath);
            if (!File.Exists(final)) AtomicWrite(final, json);
            else if (File.ReadAllText(final) != json)
                throw new InvalidDataException("Telemetry batch collision; originals retained.");
            File.Delete(activePath);
            active = NewBatch();
        }

        public string NextPath() => Directory.EnumerateFiles(root, "*.json")
            .Where(p => Guid.TryParseExact(Path.GetFileNameWithoutExtension(p), "D", out _))
            .OrderBy(File.GetLastWriteTimeUtc).FirstOrDefault();

        public bool Acknowledge(string path, string batchId, string sha256, int acceptedEvents)
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) != Path.GetFullPath(root)
                || Path.GetFileName(path) != batchId + ".json" || !File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            var batch = LogJson.Parse<LogBatch>(json);
            if (batch.batchId != batchId || batch.events.Count != acceptedEvents || Sha256(json) != sha256) return false;
            File.Delete(path);
            return true;
        }

        public static string Sha256(string text)
        {
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
        }

        static void AtomicWrite(string path, string text)
        {
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
