using System;
using System.Collections.Generic;

namespace CurtainCall.Telemetry
{
    public enum ObservationKind { LocalIntent, Observed, HostConfirmed }

    /// <summary>자유 텍스트 대신 승인된 사건 이름만 기록한다. 참가 키는 연결 단위이며 인증 ID가 아니다.</summary>
    public static class GameplayActions
    {
        public const string AttemptStarted = "attemptStarted";
        public const string AttemptStopped = "attemptStopped";
        public const string ResultConfirmed = "resultConfirmed";
        public const string InputChanged = "inputChanged";
        public const string InputDirectionChanged = "inputDirectionChanged";
        public const string Jump = "jump";
        public const string Landing = "landing";
        public const string LaneChanged = "laneChanged";
        public const string InteractionRequested = "interactionRequested";
        public const string InteractionReleased = "interactionReleased";
        public const string Handhold = "handhold";
        public const string Piggyback = "piggyback";
        public const string Platform = "platform";
        public const string Saw = "saw";
        public const string BodyDamage = "bodyDamage";
        public const string Fire = "fire";
        public const string PlayerState = "playerState";
        public const string ParticipantJoined = "participantJoined";
        public const string ParticipantLeft = "participantLeft";
        public const string Teleported = "teleported";
        public const string DeveloperCommand = "developerCommand";
        public const string SettingsChanged = "settingsChanged";
        public const string Gap = "gap";

        public static bool IsKnown(string action) => action is AttemptStarted or AttemptStopped or ResultConfirmed
            or InputChanged or InputDirectionChanged or Jump or Landing or LaneChanged or InteractionRequested or InteractionReleased
            or Handhold or Piggyback or Platform or Saw or BodyDamage or Fire or PlayerState
            or ParticipantJoined or ParticipantLeft or Teleported or DeveloperCommand or SettingsChanged or Gap;
    }

    [Serializable]
    public sealed class ParticipantRecord
    {
        public string participantKey;
        public string localPlayerId;
        public bool identitySelfReported;
        public ulong clientId;
        public int slot;
        public bool connected;
        public int startingLostParts;
        public int lostParts;
        public string outcome;
    }

    [Serializable]
    public sealed class ConfirmedResultRecord
    {
        public string roomId;
        public string sessionId;
        public string attemptId;
        public int round;
        public bool succeeded;
        public string reason;
        public float elapsed;
        public float remaining;
        public float performanceElapsed;
        public string finishedUtc;
        public List<ParticipantRecord> participants = new();
    }

    [Serializable]
    public sealed class GameplayEvent
    {
        public string eventId;
        public long sequence;
        public float localElapsed;
        public float hostElapsed;
        public bool hostTimeSynchronized;
        public string action;
        public string observation;
        public string participantKey;
        public float value;
        public float secondaryValue;
        public string cause = "unknown";
    }

    [Serializable]
    public sealed class GameplaySample
    {
        public long sequence;
        public float localElapsed;
        public float hostElapsed;
        public bool hostTimeSynchronized;
        public float x;
        public float y;
        public float z;
        public float movement;
        public float balanceInput;
        public float balance;
        public float distance;
        public bool airborne;
        public bool onRope;
        public int lane;
        public int lostParts;
        public string state;
    }

    [Serializable]
    public sealed class CaptureStatus
    {
        public bool recording;
        public bool processInterrupted;
        public bool incomplete;
        public bool storageFailed;
        public long lastSavedSequence;
        public long droppedEvents;
        public long droppedSamples;
        public long firstDroppedSequence;
        public long lastDroppedSequence;
        public string stopReason;
        public string storageError;
        public string ordering = "localSequence; hostTimeApproximate; noGlobalTotalOrder";
        public List<string> unsupported = new();
    }

    [Serializable]
    public sealed class GameplayRecord
    {
        public int schemaVersion = 1;
        public string appVersion;
        public string localPlayerId;
        public bool identityPersistence;
        public string contentId;
        public string roomId;
        public string sessionId;
        public string attemptId;
        public int round;
        public string startedUtc;
        public string updatedUtc;
        public string endedUtc;
        public string startingSettings;
        public string settingsHash;
        public long sequence;
        public CaptureStatus captureStatus = new();
        public List<ParticipantRecord> participants = new();
        public ConfirmedResultRecord confirmedResult;
        public List<GameplayEvent> events = new();
        public List<GameplaySample> samples = new();
    }
}
