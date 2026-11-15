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
            // 대기실 기물은 공통 거리에 대기실 배율을 곱한 거리에서 닿는다(2026-11-15 사용자: 패널이 뜨는 거리 절반).
            float scale = remoteTool ? 1f : GameSettings.Base.Interaction.WaitingRoomScale;
            if (!remoteTool) range = GameSettings.Base.Interaction.Distance * scale;
            Vector3 origin = actor != null ? PlayerInteraction.Center(actor)
                : feet + Vector3.up * (GameSettings.Base.Character.CapsuleHeight * .5f);
            float distance = Vector3.Distance(origin, point);
            if (!float.IsFinite(distance) || distance > range) return -1f;
            if (!remoteTool && actor != null && PlayerInteraction.RestrictReach(distance / scale, actor) < 0f) return -1f;
            return PlayerInteraction.ClearPathFrom(origin, point, root, actor) ? distance : -1f;
        }
    }
}
