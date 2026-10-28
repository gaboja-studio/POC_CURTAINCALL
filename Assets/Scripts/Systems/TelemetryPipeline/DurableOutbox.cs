using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Profiling;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Each accepted event is persisted before returning. Upload ACK never clears a newer active batch.</summary>
    public sealed class DurableOutbox
    {
        static readonly ProfilerMarker AppendMarker = new("Telemetry.Outbox.Append");
        static readonly ProfilerMarker ScanMarker = new("Telemetry.Outbox.PendingBytes");
        static readonly ProfilerMarker WriteMarker = new("Telemetry.Outbox.AtomicWrite");
        const int MaxBatchBytes = 100 * 1024;
        const long MaxPendingBytes = 64L * 1024 * 1024;
        readonly string root;
        readonly string activePath;
        readonly string senderId;
        readonly string appSessionId;
        LogBatch active;
        readonly ConcurrentDictionary<string, byte> currentBatches = new();
        readonly ConcurrentDictionary<string, byte> fileMutations = new(StringComparer.OrdinalIgnoreCase);
        public string SenderId => senderId;
        public string AppSessionId => appSessionId;
        readonly object indexGate = new();
        readonly Dictionary<string, long> fileSizes = new(StringComparer.OrdinalIgnoreCase);
        long pendingBytes;
        int pendingBatches;
        public int PendingBatchCount { get { lock (indexGate) return pendingBatches; } }

        public DurableOutbox(string directory, string applicationSession)
        {
            root = directory;
            Directory.CreateDirectory(root);
            // 시작할 때 한 번만 읽는다. 입력마다 누적 파일 전체를 디스크에서 재조회하지 않는다.
            foreach (string path in Directory.EnumerateFiles(root, "*.json"))
            {
                try { IndexFile(path, new FileInfo(path).Length); }
                catch (FileNotFoundException) { /* 직전 실행의 유효 ACK 삭제가 끝나는 경우를 허용한다. */ }
            }
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
                    DeleteFile(activePath);
                    active = NewBatch();
                }
                else Seal("RECOVERY");
            }
            active = NewBatch();
        }

        LogBatch NewBatch() => new() { senderId = senderId, appSessionId = appSessionId };

        public void Append(LogEvent entry)
        {
            using var sample = AppendMarker.Auto();
            if (PendingBytes() > MaxPendingBytes)
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
            DeleteFile(activePath);
            if (active.appSessionId == appSessionId) currentBatches.TryAdd(active.batchId, 0);
            active = NewBatch();
        }

        public string NextPath() => Directory.EnumerateFiles(root, "*.json")
            .Where(p => Guid.TryParseExact(Path.GetFileNameWithoutExtension(p), "D", out _))
            .OrderBy(p => currentBatches.ContainsKey(Path.GetFileNameWithoutExtension(p)) ? 0 : 1)
            .ThenBy(File.GetLastWriteTimeUtc).FirstOrDefault();

        // ACK is sent only after the host has persisted this exact immutable client batch.
        public string NextRelayPath() => Directory.EnumerateFiles(root, "*.json")
            .Where(p => Guid.TryParseExact(Path.GetFileNameWithoutExtension(p), "D", out _))
            .OrderBy(p => currentBatches.ContainsKey(Path.GetFileNameWithoutExtension(p)) ? 0 : 1)
            .ThenBy(File.GetLastWriteTimeUtc).FirstOrDefault(p =>
            { try { return TransportPolicy.RelayBatch(LogJson.Parse<LogBatch>(File.ReadAllText(p))); } catch { return false; } });

        public bool AcceptRelay(string raw)
        {
            if (Encoding.UTF8.GetByteCount(raw) > 120 * 1024) return false;
            var batch = LogJson.Parse<LogBatch>(raw);
            if (!TransportPolicy.RelayBatch(batch)) return false;
            string path = Path.Combine(root, batch.batchId + ".json");
            // 전송 ACK 삭제와 relay 재수신이 겹치면 기다리지 않고 다음 재시도에 맡긴다.
            if (!fileMutations.TryAdd(batch.batchId, 0)) return false;
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path) == raw;
                if (PendingBytes() > MaxPendingBytes) return false;
                AtomicWrite(path, raw);
                currentBatches.TryAdd(batch.batchId, 0);
                return true;
            }
            finally { fileMutations.TryRemove(batch.batchId, out _); }
        }

        public bool Acknowledge(string path, string batchId, string sha256, int acceptedEvents)
            => Acknowledge(path, batchId, sha256, acceptedEvents, CancellationToken.None);

        bool Acknowledge(string path, string batchId, string sha256, int acceptedEvents, CancellationToken cancellation)
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) != Path.GetFullPath(root)
                || Path.GetFileName(path) != batchId + ".json"
                || !fileMutations.TryAdd(batchId, 0)) return false;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                var batch = LogJson.Parse<LogBatch>(json);
                if (batch.batchId != batchId || batch.events.Count != acceptedEvents || Sha256(json) != sha256) return false;
                cancellation.ThrowIfCancellationRequested();
                DeleteFile(path);
                currentBatches.TryRemove(batchId, out _);
                return true;
            }
            finally { fileMutations.TryRemove(batchId, out _); }
        }

        public sealed class UploadPreparation
        {
            public string Path { get; }
            public byte[] Body { get; }
            public Exception Error { get; }
            internal UploadPreparation(string path = null, byte[] body = null, Exception error = null)
            { Path = path; Body = body; Error = error; }
        }

        // active와 Unity 객체는 작업 스레드에서 접근하지 않는다. 봉인 파일과 동시성 컬렉션만 사용한다.
        public Task<UploadPreparation> PrepareUploadAsync(string token, CancellationToken cancellation = default)
            => Task.Run(() =>
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    string path = NextPath();
                    if (path == null) return new UploadPreparation();
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (!fileMutations.TryAdd(id, 0)) return new UploadPreparation();
                    try
                    {
                        string raw = File.ReadAllText(path);
                        byte[] body = Encoding.UTF8.GetBytes(LogJson.Serialize(new { token, batchJson = raw }));
                        cancellation.ThrowIfCancellationRequested();
                        return new UploadPreparation(path, body);
                    }
                    finally { fileMutations.TryRemove(id, out _); }
                }
                catch (OperationCanceledException) { return new UploadPreparation(); }
                catch (FileNotFoundException) { return new UploadPreparation(); } // relay ACK가 먼저 지웠으면 다시 탐색한다.
                catch (Exception error) { return new UploadPreparation(error: error); }
            });

        public Task<bool> AcknowledgeUploadAsync(string path, string responseJson, CancellationToken cancellation = default)
            => Task.Run(() =>
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    var response = JObject.Parse(responseJson);
                    return response.Value<bool?>("ok") == true && Acknowledge(path,
                        response.Value<string>("batchId"), response.Value<string>("sha256"),
                        response.Value<int>("acceptedEvents"), cancellation);
                }
                catch { return false; } // 무효·취소·디스크 오류 시 원본을 보존한다.
            });

        long PendingBytes()
        {
            using var sample = ScanMarker.Auto();
            lock (indexGate) return pendingBytes;
        }

        void IndexFile(string path, long bytes)
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return;
            lock (indexGate)
            {
                if (fileSizes.TryGetValue(path, out long previous)) pendingBytes -= previous;
                else if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out _)) pendingBatches++;
                fileSizes[path] = bytes;
                pendingBytes += bytes;
            }
        }

        void DeleteFile(string path)
        {
            File.Delete(path);
            // 파일 I/O 중에는 메인스레드도 사용하는 색인 잠금을 보유하지 않는다.
            lock (indexGate)
            {
                if (!fileSizes.TryGetValue(path, out long previous)) return;
                fileSizes.Remove(path);
                pendingBytes -= previous;
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out _)) pendingBatches--;
            }
        }

        public static string Sha256(string text)
        {
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
        }

        void AtomicWrite(string path, string text)
        {
            using var sample = WriteMarker.Auto();
            string temp = path + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            IndexFile(path, bytes.Length);
        }
    }
}
