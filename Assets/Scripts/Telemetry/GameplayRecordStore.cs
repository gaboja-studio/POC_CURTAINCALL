using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CurtainCall.Telemetry
{
    /// <summary>콘텐츠마다 최근 10개. temp/backup을 한 시도로 묶고 마지막 유효 checkpoint를 복구한다.</summary>
    public sealed class GameplayRecordStore
    {
        const int RetainedAttempts = 10;
        const long MaxFileBytes = 4 * 1024 * 1024;
        readonly string root;
        readonly HashSet<string> removed = new();
        public string LocalPlayerId { get; }
        public bool IdentityPersistence { get; }
        public string LastError { get; private set; }
        public string RootPath => root;

        [Serializable]
        sealed class Identity { public string localPlayerId; }

        public GameplayRecordStore(string persistentRoot)
        {
            string telemetryRoot = Path.Combine(persistentRoot, "Telemetry");
            string identityPath = Path.Combine(telemetryRoot, "identity.json");
            string id = Guid.NewGuid().ToString("N");
            bool persisted = false;
            try
            {
                Directory.CreateDirectory(telemetryRoot);
                bool validMain = false;
                foreach (string suffix in new[] { "", ".tmp", ".bak" })
                {
                    string path = identityPath + suffix;
                    if (!File.Exists(path) || new FileInfo(path).Length > 1024) continue;
                    Identity identity;
                    try { identity = JsonUtility.FromJson<Identity>(File.ReadAllText(path)); }
                    catch (ArgumentException) { continue; }
                    if (identity == null || !IsId(identity.localPlayerId)) continue;
                    id = identity.localPlayerId;
                    persisted = true;
                    validMain = suffix == "";
                    break;
                }
                if (!validMain)
                {
                    SafeWrite(identityPath, JsonUtility.ToJson(new Identity { localPlayerId = id }));
                    persisted = true;
                }
            }
            catch (Exception e) when (StorageException(e))
            {
                // 영속 저장을 보장하지 못하면 기존 ID를 빌려 쓰지 않는다.
                id = Guid.NewGuid().ToString("N");
                persisted = false;
                LastError = e.GetType().Name;
            }
            LocalPlayerId = id;
            IdentityPersistence = persisted;
            root = Path.Combine(telemetryRoot, id);
        }

        public bool Save(GameplayRecord record)
        {
            if (!CanSave(record)) return false;
            try
            {
                string path = RecordPath(record.contentId, record.attemptId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                long previous = record.captureStatus.lastSavedSequence;
                record.captureStatus.lastSavedSequence = record.sequence;
                record.captureStatus.storageFailed = false;
                record.captureStatus.storageError = null;
                try { SafeWrite(path, JsonUtility.ToJson(record, true)); }
                catch
                {
                    record.captureStatus.lastSavedSequence = previous;
                    throw;
                }
                LastError = null;
                return true;
            }
            catch (Exception e) when (StorageException(e))
            {
                LastError = e.GetType().Name;
                record.captureStatus.storageFailed = true;
                record.captureStatus.incomplete = true;
                record.captureStatus.storageError = LastError;
                return false;
            }
        }

        public bool CanSave(GameplayRecord record) => Valid(record) && record.localPlayerId == LocalPlayerId
            && !IsRetired(record.contentId, record.attemptId);

        public bool IsRetired(string contentId, string attemptId) => !ValidContent(contentId) || !IsId(attemptId)
            || removed.Contains(Key(contentId, attemptId)) || File.Exists(RecordPath(contentId, attemptId) + ".retired");

        public GameplayRecord Load(string contentId, string attemptId)
        {
            if (IsRetired(contentId, attemptId)) return null;
            GameplayRecord best = null;
            string path = RecordPath(contentId, attemptId);
            foreach (string suffix in new[] { "", ".tmp", ".bak" })
            {
                try
                {
                    if (!File.Exists(path + suffix) || new FileInfo(path + suffix).Length > MaxFileBytes) continue;
                    var record = JsonUtility.FromJson<GameplayRecord>(File.ReadAllText(path + suffix));
                    if (!Valid(record) || record.contentId != contentId || record.attemptId != attemptId
                        || record.localPlayerId != LocalPlayerId) continue;
                    if (best == null || record.sequence > best.sequence
                        || (record.sequence == best.sequence && DateTimeOffset.Parse(record.updatedUtc).UtcDateTime
                            > DateTimeOffset.Parse(best.updatedUtc).UtcDateTime))
                        best = record;
                }
                catch (Exception e) when (StorageException(e) || e is ArgumentException) { LastError = e.GetType().Name; }
            }
            return best;
        }

        public void RecoverAndTrim(Action<GameplayRecord> onSaveFailed = null)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                foreach (string directory in Directory.GetDirectories(root))
                {
                    string content = Path.GetFileName(directory);
                    if (!ValidContent(content)) continue;
                    foreach (string attempt in Attempts(content))
                    {
                        var record = Load(content, attempt);
                        if (record == null) continue;
                        if (record.captureStatus.recording)
                        {
                            record.captureStatus.recording = false;
                            record.captureStatus.processInterrupted = true;
                            record.captureStatus.incomplete = true;
                            record.captureStatus.stopReason = "processInterrupted";
                            record.updatedUtc = DateTime.UtcNow.ToString("O");
                        }
                        if (!Save(record) && CanSave(record)) onSaveFailed?.Invoke(record);
                    }
                    Trim(content, null);
                }
            }
            catch (Exception e) when (StorageException(e)) { LastError = e.GetType().Name; }
        }

        public void Trim(string contentId, string protectedAttempt, IEnumerable<GameplayRecord> memory = null)
        {
            if (!ValidContent(contentId)) return;
            try
            {
                var attempts = new List<(string id, DateTime started)>();
                foreach (string attempt in Attempts(contentId))
                {
                    string path = RecordPath(contentId, attempt);
                    if (IsRetired(contentId, attempt)) { DeleteRecordFiles(path); continue; }
                    var record = Load(contentId, attempt);
                    DateTime started = DateTime.MinValue;
                    if (record != null && DateTimeOffset.TryParse(record.startedUtc, out var timestamp))
                        started = timestamp.UtcDateTime;
                    else
                        foreach (string suffix in new[] { "", ".tmp", ".bak" })
                            if (File.Exists(path + suffix))
                                started = File.GetLastWriteTimeUtc(path + suffix) > started
                                    ? File.GetLastWriteTimeUtc(path + suffix) : started;
                    attempts.Add((attempt, started));
                }
                if (memory != null)
                    foreach (var record in memory)
                    {
                        if (record.contentId != contentId || IsRetired(contentId, record.attemptId)
                            || attempts.Exists(a => a.id == record.attemptId)) continue;
                        DateTimeOffset.TryParse(record.startedUtc, out var started);
                        attempts.Add((record.attemptId, started.UtcDateTime));
                    }
                attempts.Sort((a, b) =>
                {
                    int order = b.started.CompareTo(a.started);
                    return order != 0 ? order : string.CompareOrdinal(a.id, b.id);
                });
                int keep = protectedAttempt != null && attempts.Exists(a => a.id == protectedAttempt) ? 1 : 0;
                foreach (var attempt in attempts)
                {
                    if (attempt.id == protectedAttempt) continue;
                    if (keep++ < RetainedAttempts) continue;
                    Retire(contentId, attempt.id);
                }
            }
            catch (Exception e) when (StorageException(e)) { LastError = e.GetType().Name; }
        }

        public void Retire(string contentId, string attemptId)
        {
            if (!ValidContent(contentId) || !IsId(attemptId)) return;
            // 쓰기 불가여도 이번 실행에서는 다시 저장하지 않는다.
            removed.Add(Key(contentId, attemptId));
            try
            {
                string path = RecordPath(contentId, attemptId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // 삭제 표시를 먼저 확정해 재실행 후 늦은 결과의 부활도 막는다.
                using (var marker = new FileStream(path + ".retired", FileMode.Create, FileAccess.Write, FileShare.None))
                    marker.Flush(true);
                DeleteRecordFiles(path);
            }
            catch (Exception e) when (StorageException(e)) { LastError = e.GetType().Name; }
        }

        IEnumerable<string> Attempts(string contentId)
        {
            string directory = Path.Combine(root, contentId);
            var ids = new HashSet<string>();
            if (!Directory.Exists(directory)) return ids;
            foreach (string path in Directory.GetFiles(directory, "*.json*"))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".tmp", StringComparison.Ordinal) || name.EndsWith(".bak", StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - 4);
                if (!name.EndsWith(".json", StringComparison.Ordinal)) continue;
                string id = name.Substring(0, name.Length - 5);
                if (IsId(id)) ids.Add(id);
            }
            return ids;
        }

        static void DeleteRecordFiles(string path)
        {
            foreach (string suffix in new[] { "", ".tmp", ".bak" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        string RecordPath(string content, string attempt) => Path.Combine(root, content, attempt + ".json");
        static string Key(string content, string attempt) => content + ":" + attempt;
        public static bool IsId(string id) => Guid.TryParseExact(id, "N", out _);
        public static bool ValidContent(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_') return false;
            return true;
        }

        static bool Valid(GameplayRecord record) => GameplayRecordValidation.Record(record);

        static void SafeWrite(string path, string contents)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            if (bytes.Length > MaxFileBytes) throw new IOException("recordTooLarge");
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (!File.Exists(path)) { File.Move(temporary, path); return; }
            try { File.Replace(temporary, path, path + ".bak"); }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, path + ".bak", true);
                File.Delete(path);
                File.Move(temporary, path);
            }
        }

        static bool StorageException(Exception e) => e is IOException || e is UnauthorizedAccessException
            || e is System.Security.SecurityException || e is NotSupportedException;
    }
}
