using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CurtainCall.Telemetry.Pipeline
{
    [Serializable]
    public sealed class LogEvent
    {
        public string eventId = Guid.NewGuid().ToString("D");
        public string occurredAt;
        public long sequence;
        public double monotonicMs;
        public string environment;
        public string gameVersion;
        public string playerId;
        public int? playerNumber;
        public string networkClientId;
        public string roomId;
        public string runId;
        public string performanceId;
        public string attemptId;
        public string breakId;
        public string sceneType;
        public string eventType;
        public string authority;
        public Dictionary<string, object> payload = new();
    }

    [Serializable]
    public sealed class LogBatch
    {
        public int schemaVersion = 1;
        public string batchId = Guid.NewGuid().ToString("D");
        public string senderId;
        public string appSessionId;
        public string flushReason = "CHECKPOINT";
        public List<LogEvent> events = new();
    }

    public static class LogJson
    {
        static readonly JsonSerializerSettings Settings = new()
        {
            DateParseHandling = DateParseHandling.None,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 24,
            FloatFormatHandling = FloatFormatHandling.String,
            Culture = System.Globalization.CultureInfo.InvariantCulture
        };
        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.None, Settings);
        public static T Parse<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
