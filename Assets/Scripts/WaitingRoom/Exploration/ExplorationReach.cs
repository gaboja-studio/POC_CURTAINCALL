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
            if (!remoteTool) range = GameSettings.Base.Interaction.Distance;
            Vector3 origin = actor != null ? PlayerInteraction.Center(actor)
                : feet + Vector3.up * (GameSettings.Base.Character.CapsuleHeight * .5f);
            float distance = Vector3.Distance(origin, point);
            if (!float.IsFinite(distance) || distance > range) return -1f;
            if (!remoteTool && actor != null && PlayerInteraction.RestrictReach(distance, actor) < 0f) return -1f;
            return PlayerInteraction.ClearPathFrom(origin, point, root, actor) ? distance : -1f;
        }
    }
}
