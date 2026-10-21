using System;
using System.Linq;
using System.Text;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Original client IDs and clocks survive a host relay; Google deduplicates original event IDs.</summary>
    public static class TransportPolicy
    {
        public static bool CanUpload(bool host, bool client, bool hasHosted) => !client && (host || hasHosted);

        public static bool RelayBatch(LogBatch batch) => batch != null && batch.schemaVersion == 1
            && Guid.TryParseExact(batch.batchId, "D", out _) && Guid.TryParseExact(batch.senderId, "D", out _)
            && Guid.TryParseExact(batch.appSessionId, "D", out _) && batch.events != null
            && batch.events.Count is > 0 and <= 250
            && batch.flushReason is "CHECKPOINT" or "SCENE_CHANGED" or "RUN_ENDED" or "PLAYER_EXITED" or "GAME_EXITED" or "RECOVERY"
            && batch.events.All(e => e != null && e.authority is "LOCAL" or "OBSERVED" && EventContract.IsValid(e))
            && batch.events.Select(e => e.eventId).Distinct().Count() == batch.events.Count
            && batch.events.Zip(batch.events.Skip(1), (a, b) => a.sequence < b.sequence).All(increasing => increasing)
            && Encoding.UTF8.GetByteCount(LogJson.Serialize(batch)) <= 120 * 1024;
    }
}
