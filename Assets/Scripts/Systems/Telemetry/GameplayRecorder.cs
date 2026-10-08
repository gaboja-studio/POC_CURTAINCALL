using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CurtainCall.Telemetry
{
    /// <summary>개인 시도의 bounded 수집기. 게임 판정과 외부 전송은 하지 않는다.</summary>
    public sealed class GameplayRecorder : MonoBehaviour
    {
        const int MaxEvents = 512;
        const int MaxSamples = 3000;
        const int MaxMemoryAttempts = 40;
        const float CheckpointInterval = 5f;
        readonly Dictionary<string, GameplayRecord> retainedMemory = new();
        readonly Dictionary<string, GameplayRecord> pendingSaves = new();
        GameplayRecordStore store;
        float startedAt;
        float checkpointAt;
        public GameplayRecord Current { get; private set; }
        public string LocalPlayerId => Store.LocalPlayerId;
        public bool IdentityPersistence => Store.IdentityPersistence;
        public string StorageError
        {
            get
            {
                if (UnsavedAttemptsDiscarded > 0) return "memoryRetryLimit";
                if (recorderError != null) return recorderError;
                foreach (var record in pendingSaves.Values)
                    if (record.captureStatus.storageFailed) return record.captureStatus.storageError;
                return Store.LastError;
            }
        }
        public int UnsavedAttemptsDiscarded { get; private set; }
        string recorderError;
        string recorderErrorKey;
        public event Action StatusChanged;
        GameplayRecordStore Store => store ??= new GameplayRecordStore(Application.persistentDataPath);

        void Awake()
        {
            Store.RecoverAndTrim(record =>
            {
                string key = Key(record.contentId, record.attemptId);
                retainedMemory[key] = record;
                pendingSaves[key] = record;
                Trim(record.contentId);
            });
            RetryPending();
        }

        void Update()
        {
            if (Time.unscaledTime < checkpointAt) return;
            checkpointAt = Time.unscaledTime + CheckpointInterval;
            RetryPending();
            if (Current != null && Current.captureStatus.recording) Checkpoint();
        }

        public void Begin(string contentId, string roomId, string sessionId, string attemptId, int round,
            string settings, IEnumerable<string> unsupported)
        {
            if (!GameplayRecordStore.ValidContent(contentId) || !GameplayRecordStore.IsId(roomId)
                || !GameplayRecordStore.IsId(sessionId) || !GameplayRecordStore.IsId(attemptId) || round <= 0
                || Store.IsRetired(contentId, attemptId)) return;
            if (Current != null && Current.contentId == contentId && Current.attemptId == attemptId) return;
            string key = Key(contentId, attemptId);
            if (retainedMemory.ContainsKey(key) || Store.Load(contentId, attemptId) != null) return;
            var unavailable = unsupported == null ? new List<string>() : new List<string>(unsupported);
            if (unavailable.Count > 32 || unavailable.Exists(s => string.IsNullOrEmpty(s) || s.Length > 100)) return;
            Stop("nextAttempt");
            startedAt = Time.unscaledTime;
            Current = new GameplayRecord
            {
                appVersion = Application.version, localPlayerId = LocalPlayerId, identityPersistence = IdentityPersistence,
                contentId = contentId, roomId = roomId, sessionId = sessionId, attemptId = attemptId, round = round,
                startedUtc = DateTime.UtcNow.ToString("O"), startingSettings = settings, settingsHash = Hash(settings),
                captureStatus = new CaptureStatus { recording = true, unsupported = unavailable }
            };
            retainedMemory[key] = Current;
            Record(GameplayActions.AttemptStarted, ObservationKind.Observed, null, 0f, false, round);
            Checkpoint();
            Trim(contentId);
        }

        public void SetParticipants(List<ParticipantRecord> participants)
        {
            if (Current == null || !Current.captureStatus.recording || !GameplayRecordValidation.Participants(participants)) return;
            Current.participants = CopyParticipants(participants);
        }

        public void Record(string action, ObservationKind observation, string participantKey,
            float hostElapsed, bool synchronized, float value = 0f, float secondaryValue = 0f, string cause = "unknown")
        {
            if (Current == null || !Current.captureStatus.recording || !GameplayActions.IsKnown(action)
                || !GameplayRecordValidation.Cause(cause) || !Enum.IsDefined(typeof(ObservationKind), observation)
                || (participantKey != null && (participantKey.Length == 0 || participantKey.Length > 100))
                || !GameplayRecordValidation.Finite(hostElapsed) || hostElapsed < 0f
                || !GameplayRecordValidation.Finite(value) || !GameplayRecordValidation.Finite(secondaryValue)) return;
            MakeEventSpace(Current);
            long sequence = ++Current.sequence;
            Current.events.Add(new GameplayEvent
            {
                eventId = Current.attemptId + ":" + sequence, sequence = sequence,
                localElapsed = Time.unscaledTime - startedAt, hostElapsed = hostElapsed,
                hostTimeSynchronized = synchronized, action = action, observation = observation.ToString(),
                participantKey = participantKey, value = value, secondaryValue = secondaryValue, cause = cause
            });
        }

        public void Sample(GameplaySample sample)
        {
            if (Current == null || !Current.captureStatus.recording || !GameplayRecordValidation.Sample(sample)) return;
            // 호출자가 다음 sample을 재사용해도 저장된 값을 변경하지 못한다.
            var copy = JsonUtility.FromJson<GameplaySample>(JsonUtility.ToJson(sample));
            copy.sequence = ++Current.sequence;
            copy.localElapsed = Time.unscaledTime - startedAt;
            if (Current.samples.Count >= MaxSamples)
            {
                Dropped(Current, Current.samples[0].sequence, false);
                Current.samples.RemoveAt(0);
            }
            Current.samples.Add(copy);
        }

        static void MakeEventSpace(GameplayRecord record)
        {
            if (record.events.Count < MaxEvents) return;
            int expendable = record.events.FindIndex(e => e.action == GameplayActions.InputChanged
                || e.action == GameplayActions.InputDirectionChanged);
            if (expendable < 0) expendable = 0;
            Dropped(record, record.events[expendable].sequence, true);
            record.events.RemoveAt(expendable);
        }

        static void Dropped(GameplayRecord record, long sequence, bool meaningful)
        {
            var status = record.captureStatus;
            status.incomplete = true;
            if (meaningful) status.droppedEvents++; else status.droppedSamples++;
            if (status.firstDroppedSequence == 0 || sequence < status.firstDroppedSequence)
                status.firstDroppedSequence = sequence;
            status.lastDroppedSequence = Math.Max(status.lastDroppedSequence, sequence);
        }

        public void Confirm(ConfirmedResultRecord result, string contentId = null)
        {
            if (!GameplayRecordValidation.Result(result)) return;
            GameplayRecord record = null;
            foreach (var candidate in retainedMemory.Values)
                if (candidate.attemptId == result.attemptId && (contentId == null || candidate.contentId == contentId))
                { record = candidate; break; }
            if (record == null)
            {
                contentId ??= Current?.contentId;
                if (contentId == null) return;
                record = Store.Load(contentId, result.attemptId);
            }
            if (record == null || Store.IsRetired(record.contentId, record.attemptId)
                || record.confirmedResult != null || record.roomId != result.roomId
                || record.sessionId != result.sessionId || record.round != result.round) return;
            var copy = JsonUtility.FromJson<ConfirmedResultRecord>(JsonUtility.ToJson(result));
            record.confirmedResult = copy;
            record.participants = CopyParticipants(copy.participants);
            record.endedUtc = copy.finishedUtc;
            record.captureStatus.recording = false;
            record.captureStatus.stopReason = "hostConfirmed";
            MakeEventSpace(record);
            long sequence = ++record.sequence;
            record.events.Add(new GameplayEvent
            {
                eventId = result.attemptId + ":" + sequence, sequence = sequence,
                action = GameplayActions.ResultConfirmed, observation = ObservationKind.HostConfirmed.ToString(),
                hostElapsed = result.elapsed, hostTimeSynchronized = true,
                localElapsed = record == Current ? Time.unscaledTime - startedAt : -1f,
                value = result.succeeded ? 1f : 0f
            });
            record.updatedUtc = DateTime.UtcNow.ToString("O");
            retainedMemory[Key(record.contentId, record.attemptId)] = record;
            Save(record);
            Trim(record.contentId);
            StatusChanged?.Invoke();
        }

        public void Checkpoint()
        {
            if (Current == null) return;
            checkpointAt = Time.unscaledTime + CheckpointInterval;
            Current.updatedUtc = DateTime.UtcNow.ToString("O");
            Save(Current);
            StatusChanged?.Invoke();
        }

        void Save(GameplayRecord record)
        {
            string key = Key(record.contentId, record.attemptId);
            if (!GameplayRecordValidation.Record(record))
            {
                recorderError = "invalidRecord";
                recorderErrorKey = key;
                pendingSaves.Remove(key);
                return;
            }
            if (recorderErrorKey == key) { recorderError = recorderErrorKey = null; }
            if (Store.IsRetired(record.contentId, record.attemptId)) { pendingSaves.Remove(key); return; }
            if (record.localPlayerId != Store.LocalPlayerId)
            {
                recorderError = "identityMismatch";
                recorderErrorKey = key;
                pendingSaves.Remove(key);
                return;
            }
            if (Store.Save(record)) pendingSaves.Remove(key);
            else pendingSaves[key] = record;
        }

        void RetryPending()
        {
            if (pendingSaves.Count == 0) return;
            var pending = new List<GameplayRecord>(pendingSaves.Values);
            foreach (var record in pending) Save(record);
            StatusChanged?.Invoke();
        }

        void Trim(string contentId)
        {
            string protectedAttempt = Current?.contentId == contentId ? Current.attemptId : null;
            Store.Trim(contentId, protectedAttempt, retainedMemory.Values);
            var records = new List<GameplayRecord>(retainedMemory.Values);
            records.Sort((a, b) => DateTimeOffset.Parse(b.startedUtc).UtcDateTime
                .CompareTo(DateTimeOffset.Parse(a.startedUtc).UtcDateTime));
            int keep = protectedAttempt == null ? 0 : 1;
            foreach (var record in records)
            {
                if (record.contentId != contentId || record.attemptId == protectedAttempt) continue;
                if (!Store.IsRetired(contentId, record.attemptId) && keep++ < 10) continue;
                Store.Retire(contentId, record.attemptId);
                RemoveMemory(record);
            }
            // 여러 콘텐츠를 사용해도 실패 재시도와 메모리는 실행당 40개로 제한한다.
            records.Reverse();
            foreach (var record in records)
            {
                if (retainedMemory.Count <= MaxMemoryAttempts && pendingSaves.Count <= MaxMemoryAttempts) break;
                if (record == Current) continue;
                // 디스크에 남은 시도는 삭제하지 않고 메모리만 내린다.
                if (pendingSaves.ContainsKey(Key(record.contentId, record.attemptId)))
                {
                    UnsavedAttemptsDiscarded++;
                }
                RemoveMemory(record);
            }
        }

        void RemoveMemory(GameplayRecord record)
        {
            string key = Key(record.contentId, record.attemptId);
            retainedMemory.Remove(key);
            pendingSaves.Remove(key);
        }

        public void Stop(string reason)
        {
            if (Current == null || !Current.captureStatus.recording) return;
            Record(GameplayActions.AttemptStopped, ObservationKind.Observed, null, 0f, false);
            Current.captureStatus.recording = false;
            Current.captureStatus.incomplete = true;
            Current.captureStatus.stopReason = reason;
            Current.endedUtc = DateTime.UtcNow.ToString("O");
            Checkpoint();
            Trim(Current.contentId);
        }

        void OnApplicationPause(bool paused) { if (paused) { Checkpoint(); RetryPending(); } }
        void OnApplicationQuit() { Stop("applicationQuit"); RetryPending(); }
        void OnDisable() { Stop("captureDisabled"); RetryPending(); }

        static string Key(string content, string attempt) => content + ":" + attempt;
        static List<ParticipantRecord> CopyParticipants(List<ParticipantRecord> people)
        {
            var result = new List<ParticipantRecord>(people.Count);
            foreach (var person in people)
                result.Add(new ParticipantRecord
                {
                    participantKey = person.participantKey, localPlayerId = person.localPlayerId,
                    identitySelfReported = person.identitySelfReported, clientId = person.clientId, slot = person.slot,
                    connected = person.connected, startingLostParts = person.startingLostParts,
                    lostParts = person.lostParts, outcome = person.outcome
                });
            return result;
        }

        static string Hash(string value)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
