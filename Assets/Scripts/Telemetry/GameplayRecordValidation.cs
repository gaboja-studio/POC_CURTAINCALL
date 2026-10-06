using System;
using System.Collections.Generic;

namespace CurtainCall.Telemetry
{
    /// <summary>디스크 복구와 공개 수집 경계에서 같은 DTO 계약을 검사한다.</summary>
    public static class GameplayRecordValidation
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool TimeValue(float value) => Finite(value) && value >= 0f;
        static bool Timestamp(string value) => DateTimeOffset.TryParse(value, out _);
        static bool Key(string value) => string.IsNullOrEmpty(value) || value.Length <= 100;
        public static bool Cause(string value) => value is "unknown" or "balance" or "rope" or "saw"
            or "fire" or "bodyDamage" or "restart" or "finish" or "developer";
        public static bool State(string value) => value is "Normal" or "Fallen" or "Arrived";
        static bool Outcome(string value) => value is "DirectFinish" or "CompanionSuccess"
            or "Fallen" or "Disconnected" or "Unfinished";

        public static bool Participants(List<ParticipantRecord> people)
        {
            if (people == null || people.Count > 64) return false;
            var keys = new HashSet<string>();
            foreach (var person in people)
                if (person == null || string.IsNullOrEmpty(person.participantKey) || !Key(person.participantKey)
                    || !keys.Add(person.participantKey) || person.slot < -1 || person.slot > 63
                    || (person.startingLostParts & ~15) != 0 || (person.lostParts & ~15) != 0
                    || !Outcome(person.outcome)
                    || (!string.IsNullOrEmpty(person.localPlayerId) && !GameplayRecordStore.IsId(person.localPlayerId))
                    || (person.identitySelfReported && string.IsNullOrEmpty(person.localPlayerId))) return false;
            return true;
        }

        public static bool Result(ConfirmedResultRecord result) => result != null
            && GameplayRecordStore.IsId(result.roomId) && GameplayRecordStore.IsId(result.sessionId)
            && GameplayRecordStore.IsId(result.attemptId) && result.round > 0
            && (result.reason is "Completed" or "AllFallen" or "TimeExpired")
            && result.succeeded == (result.reason == "Completed") && TimeValue(result.elapsed)
            && TimeValue(result.remaining) && TimeValue(result.performanceElapsed)
            && Timestamp(result.finishedUtc) && Participants(result.participants);

        public static bool Sample(GameplaySample sample) => sample != null
            && TimeValue(sample.hostElapsed) && Finite(sample.x) && Finite(sample.y) && Finite(sample.z)
            && Finite(sample.movement) && Math.Abs(sample.movement) <= 1f
            && Finite(sample.balanceInput) && Math.Abs(sample.balanceInput) <= 1f
            && Finite(sample.balance) && Math.Abs(sample.balance) <= 100f && Finite(sample.distance)
            && (!sample.onRope || (!sample.airborne && sample.lane >= 0))
            && sample.lane >= -1 && sample.lane <= 63 && (sample.lostParts & ~15) == 0 && State(sample.state);

        public static bool Record(GameplayRecord record)
        {
            if (record == null || record.schemaVersion != 1 || !GameplayRecordStore.IsId(record.localPlayerId)
                || !GameplayRecordStore.IsId(record.roomId) || !GameplayRecordStore.IsId(record.sessionId)
                || !GameplayRecordStore.IsId(record.attemptId) || !GameplayRecordStore.ValidContent(record.contentId)
                || record.round <= 0 || record.sequence < 0 || !Timestamp(record.startedUtc)
                || !Timestamp(record.updatedUtc) || (!string.IsNullOrEmpty(record.endedUtc) && !Timestamp(record.endedUtc))
                || !Participants(record.participants) || record.events == null || record.events.Count > 512
                || record.samples == null || record.samples.Count > 3000 || record.captureStatus == null) return false;
            var status = record.captureStatus;
            if (status.lastSavedSequence < 0 || status.lastSavedSequence > record.sequence
                || status.droppedEvents < 0 || status.droppedSamples < 0
                || status.firstDroppedSequence < 0 || status.lastDroppedSequence < status.firstDroppedSequence
                || status.lastDroppedSequence > record.sequence || status.unsupported == null
                || status.unsupported.Count > 32) return false;
            foreach (string unsupported in status.unsupported)
                if (string.IsNullOrEmpty(unsupported) || unsupported.Length > 100) return false;
            if (record.confirmedResult != null && (!Result(record.confirmedResult)
                || record.confirmedResult.roomId != record.roomId || record.confirmedResult.sessionId != record.sessionId
                || record.confirmedResult.attemptId != record.attemptId || record.confirmedResult.round != record.round
                || status.recording)) return false;
            var sequences = new HashSet<long>();
            long previous = 0;
            foreach (var entry in record.events)
            {
                if (entry == null || entry.sequence <= previous || entry.sequence > record.sequence
                    || !sequences.Add(entry.sequence) || entry.eventId != record.attemptId + ":" + entry.sequence
                    || !GameplayActions.IsKnown(entry.action) || !Cause(entry.cause) || !Key(entry.participantKey)
                    || entry.observation is not ("LocalIntent" or "Observed" or "HostConfirmed")
                    || !Finite(entry.localElapsed) || entry.localElapsed < -1f || !TimeValue(entry.hostElapsed)
                    || !Finite(entry.value) || !Finite(entry.secondaryValue)) return false;
                previous = entry.sequence;
            }
            previous = 0;
            foreach (var sample in record.samples)
            {
                if (!Sample(sample) || sample.sequence <= previous || sample.sequence > record.sequence
                    || !sequences.Add(sample.sequence) || !TimeValue(sample.localElapsed)) return false;
                previous = sample.sequence;
            }
            return true;
        }
    }
}
