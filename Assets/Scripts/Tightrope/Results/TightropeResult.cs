using System;
using System.Collections.Generic;
using CurtainCall.Player;

namespace CurtainCall.Tightrope.Results
{
    public enum TightropeEndReason : byte { Completed, AllFallen, TimeExpired }
    public enum ParticipantOutcome : byte { DirectFinish, CompanionSuccess, Fallen, Disconnected, Unfinished }

    /// <summary>호스트가 고정한 참가자 결과. slot은 표시용이고 동일성은 참가 키로 구분한다.</summary>
    public sealed class TightropeParticipantResult
    {
        public string ParticipantKey { get; }
        public ulong ClientId { get; }
        public int Slot { get; }
        public bool Connected { get; }
        public BodyPart StartingLostParts { get; }
        public BodyPart LostParts { get; }
        public ParticipantOutcome Outcome { get; }

        internal TightropeParticipantResult(ParticipantData data)
        {
            ParticipantKey = data.participantKey;
            ClientId = data.clientId;
            Slot = data.slot;
            Connected = data.connected;
            StartingLostParts = (BodyPart)data.startingLostParts;
            LostParts = (BodyPart)data.lostParts;
            Outcome = (ParticipantOutcome)data.outcome;
        }
    }

    /// <summary>UI·개인 기록이 함께 읽는 종료 직전 불변 결과. 시간이 다시 흐르지 않는다.</summary>
    public sealed class TightropeResult
    {
        public string RoomId { get; }
        public string SessionId { get; }
        public string AttemptId { get; }
        public int Round { get; }
        public bool Succeeded { get; }
        public TightropeEndReason Reason { get; }
        public float Elapsed { get; }
        public float Remaining { get; }
        public float PerformanceElapsed { get; }
        public string FinishedUtc { get; }
        public IReadOnlyList<TightropeParticipantResult> Participants { get; }

        internal TightropeResult(ResultData data)
        {
            RoomId = data.roomId;
            SessionId = data.sessionId;
            AttemptId = data.attemptId;
            Round = data.round;
            Succeeded = data.succeeded;
            Reason = (TightropeEndReason)data.reason;
            Elapsed = data.elapsed;
            Remaining = data.remaining;
            PerformanceElapsed = data.performanceElapsed;
            FinishedUtc = data.finishedUtc;
            var people = new TightropeParticipantResult[data.participants.Length];
            for (int i = 0; i < people.Length; i++) people[i] = new TightropeParticipantResult(data.participants[i]);
            Participants = Array.AsReadOnly(people);
        }
    }

    [Serializable]
    internal sealed class ParticipantData
    {
        public string participantKey;
        public ulong clientId;
        public int slot;
        public int startingLostParts;
        public int lostParts;
        public byte state;
        public bool connected;
        public byte outcome;

        public ParticipantData Copy() => (ParticipantData)MemberwiseClone();
    }

    [Serializable]
    internal sealed class ResultData
    {
        public string roomId;
        public string sessionId;
        public string attemptId;
        public int round;
        public bool succeeded;
        public byte reason;
        public float elapsed;
        public float remaining;
        public float performanceElapsed;
        public string finishedUtc;
        public ParticipantData[] participants;
    }

    [Serializable]
    internal sealed class ResultEnvelope
    {
        public string roomId;
        public string sessionId;
        public string attemptId;
        public int round;
        public ParticipantData[] roster;
        public ResultData latest;
    }
}
