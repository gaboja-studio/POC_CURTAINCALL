using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Reject invalid adapter events before they can block a whole remote batch.</summary>
    public static class EventContract
    {
        static readonly JObject Catalog = JObject.Parse(EventCatalog.Json);
        static bool Id(string text) => Guid.TryParseExact(text, "D", out _);
        static bool Integer(JToken v, long min, long max) => v?.Type == JTokenType.Integer
            && v.Value<double>() >= min && v.Value<double>() <= max;
        static bool Text(JToken v) => v?.Type == JTokenType.String && v.Value<string>().Length is > 0 and <= 128;
        static bool Number(JToken v) => v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float)
            && !double.IsNaN(v.Value<double>()) && !double.IsInfinity(v.Value<double>());
        static bool OneOf(JToken v, params string[] allowed) => v?.Type == JTokenType.String && allowed.Contains(v.Value<string>());
        static bool Utc(JToken v) => v?.Type == JTokenType.String && v.Value<string>().EndsWith("Z", StringComparison.Ordinal)
            && DateTimeOffset.TryParse(v.Value<string>(), out _);
        static bool OptionalPattern(string value, string pattern) => value == null || System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
        static bool Value(JToken v, string type) => type switch
        {
            "optionalPositive" => v?.Type == JTokenType.Null || Integer(v, 1, 9007199254740991L),
            "optionalBool" => v?.Type == JTokenType.Null || v?.Type == JTokenType.Boolean,
            "text" => Text(v), "uuid" => v?.Type == JTokenType.String && Id(v.Value<string>()),
            "utc" => Utc(v), "bool" => v?.Type == JTokenType.Boolean,
            "duration" => Number(v) && v.Value<double>() >= 0,
            "position" => v is JObject && new[] { "x", "y", "z" }.All(k => Number(v[k])),
            "ids" => v is JArray list && list.Count is >= 1 and <= 4 && list.All(Text)
                && list.Values<string>().Distinct().Count() == list.Count,
            "roster" => v is JArray people && people.Count is >= 1 and <= 4 && people.All(p => p is JObject
                && Text(p["playerId"]) && p["networkClientId"]?.Type == JTokenType.String
                && System.Text.RegularExpressions.Regex.IsMatch(p.Value<string>("networkClientId"), "^[0-9]{1,20}$")
                && (p["playerNumber"]?.Type == JTokenType.Null || Integer(p["playerNumber"], 0, 3)))
                && people.Select(p => p.Value<string>("networkClientId")).Distinct().Count() == people.Count,
            "roomType" => OneOf(v, "CODE", "LAN"), "trick" => OneOf(v, "TIGHTROPE", "CANNON"),
            "fundCategory" => OneOf(v, "REWARD", "PURCHASE", "SERVICE", "EXPENSE", "OTHER", "RESET"),
            "limbs" => Integer(v, 0, 4), "players" => Integer(v, 1, 4), "difficulty" => Integer(v, 1, 3),
            "positive" => Integer(v, 1, 9007199254740991L), "count" => Integer(v, 0, 9007199254740991L), _ => false
        };

        public static bool IsValid(LogEvent entry)
        {
            if (entry == null || !Id(entry.eventId) || entry.sequence < 1 || entry.sequence > 9007199254740991L
                || !Utc(entry.occurredAt) || double.IsNaN(entry.monotonicMs) || double.IsInfinity(entry.monotonicMs)
                || entry.monotonicMs < 0 || entry.environment is not ("LIVE" or "TEST")
                || entry.gameVersion != null && (entry.gameVersion.Length > 64
                    || !OptionalPattern(entry.gameVersion, "^[0-9]+\\.[0-9]+\\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$"))
                || string.IsNullOrEmpty(entry.playerId) || entry.playerId.Length > 128
                || entry.playerNumber is < 0 or > 3 || entry.payload == null
                || !OptionalPattern(entry.roomId, "^[0-9a-f]{32}$")
                || !OptionalPattern(entry.networkClientId, "^[0-9]{1,20}$")
                || !OptionalPattern(entry.performanceId, "^D[1-9][0-9]*P[1-9][0-9]*$")
                || !OptionalPattern(entry.breakId, "^D[1-9][0-9]*T[1-9][0-9]*$")
                || entry.authority is not ("LOCAL" or "HOST" or "OBSERVED")
                || entry.sceneType is not ("TITLE" or "LOBBY" or "WAITINGROOM" or "MAINSTAGE" or "BACKSTAGE" or "EXITROUTE" or "RESULTS" or "UNKNOWN")
                || entry.runId != null && !Id(entry.runId) || entry.attemptId != null && !Id(entry.attemptId)
                || entry.eventType == null || Catalog[entry.eventType] is not JObject schema) return false;
            var payload = JObject.FromObject(entry.payload);
            if (payload.ToString(Newtonsoft.Json.Formatting.None).Length > 8192) return false;
            if (schema.Properties().Any(p => !Value(payload[p.Name], p.Value.Value<string>()))) return false;
            if (System.Text.RegularExpressions.Regex.IsMatch(entry.eventType, "^(PERFORMANCE_|SECTION_|PLAYER_DIED|PLAYER_ARRIVED|LIMB_SEVERED|TIGHTROPE_|JUMP_|COOP_|CANNON_)")
                && (!Id(entry.runId) || !Id(entry.attemptId))) return false;
            return entry.eventType != "MICROPHONE_INPUT"
                || DateTimeOffset.Parse(payload.Value<string>("endedAt")) >= DateTimeOffset.Parse(payload.Value<string>("startedAt"));
        }
    }
}
