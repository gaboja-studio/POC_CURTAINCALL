using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public static class ExplorationReach
    {
        // Vector position is retained for editor previews; live actors supply their actual body centre.
        public static float From(Transform root, Vector3 point, Vector3 feet, float range,
            bool spatial = false, bool insideFurniture = false, bool remoteTool = false, Component actor = null)
        {
            // 대기실 기물은 다가가야 하는 거리(수평)만 대기실 배율로 줄인다(2026-11-15 사용자: 패널이 뜨는 거리 절반).
            // 높이까지 줄이면 머리 위 낙서·환풍구처럼 서서 닿던 것들이 점프해야만 닿게 되므로, 3차원 거리는 공통 거리 그대로 본다.
            float scale = remoteTool ? 1f : GameSettings.Base.Interaction.WaitingRoomScale;
            if (!remoteTool) range = GameSettings.Base.Interaction.Distance;
            Vector3 origin = actor != null ? PlayerInteraction.Center(actor)
                : feet + Vector3.up * (GameSettings.Base.Character.CapsuleHeight * .5f);
            float distance = Vector3.Distance(origin, point);
            float flat = new Vector2(point.x - origin.x, point.z - origin.z).magnitude;
            if (!float.IsFinite(distance) || distance > range || flat > range * scale) return -1f;
            if (!remoteTool && actor != null && (PlayerInteraction.RestrictReach(distance, actor) < 0f || PlayerInteraction.RestrictReach(flat / scale, actor) < 0f)) return -1f;
            return PlayerInteraction.ClearPathFrom(origin, point, root, actor) ? distance : -1f;
        }
    }
}
