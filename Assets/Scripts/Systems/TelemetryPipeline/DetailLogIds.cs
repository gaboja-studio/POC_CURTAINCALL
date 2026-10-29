using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline
{
    public static class DetailLogIds
    {
        public static string Uuid(string scope) => Guid.ParseExact(DurableOutbox.Sha256(scope).Substring(0, 32), "N").ToString("D");
        public static string ObjectId(Component item, string prefix)
        {
            if (item == null) return null;
            string path = item.gameObject.scene.path;
            for (var t = item.transform; t != null; t = t.parent) path += "/" + t.name + "[" + t.GetSiblingIndex() + "]";
            return prefix + DurableOutbox.Sha256(path).Substring(0, 12);
        }
        public static object Position(Vector3 position) => new { x = Math.Round(position.x, 1), y = Math.Round(position.y, 1), z = Math.Round(position.z, 1) };
        public static Dictionary<string, object> RoutePayload(string type, Dictionary<string, object> payload, Vector3 position)
        {
            bool route = type is "PERFORMANCE_STARTED" or "PERFORMANCE_LEFT" or "SECTION_ENTERED" or "SECTION_EXITED"
                or "PLAYER_DIED" or "LIMB_SEVERED" or "INJURY_CHANGED" or "TIGHTROPE_FELL" or "JUMP_RESULT"
                or "COOP_ACTIVATED" or "CANNON_FIRED" or "CANNON_FLIGHT_ENDED" or "INTERACTION_FAILED";
            if (!route || payload.ContainsKey("position")) return payload;
            // Existing source snapshots (including explicit unknown coordinates) always take precedence.
            return new Dictionary<string, object>(payload) { ["position"] = Position(position), ["positionSource"] = "EMIT_OBSERVATION" };
        }
        public static int Lost(BodyPart parts) => PlayerCondition.CountLimbs(parts);
        public static string Break(CampaignSnapshot step) => step.Day < 1 ? null
            : step.Phase == CampaignPhase.Night ? $"D{step.Day}T3"
            : step.Phase == CampaignPhase.RestTime ? $"D{step.Day}T{(step.Rest == RestKind.Morning ? 1 : 2)}" : null;
        public static string Disconnect(string reason) => reason switch
        {
            "방을 닫았습니다." => "HOST_CLOSED", "방에서 나갔습니다." => "VOLUNTARY",
            "호스트와 연결이 끊겼습니다." => "CONNECTION_LOST", _ => "UNKNOWN"
        };
    }
}
