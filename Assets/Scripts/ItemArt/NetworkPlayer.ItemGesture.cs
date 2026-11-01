using CurtainCall.Player;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        public void SendItemGesture(int kind, Vector3 target, bool allowMovement)
        {
            if (IsSpawned && IsOwner && kind >= 0 && kind <= 4) ItemGestureRequestRpc(kind, target, allowMovement);
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ItemGestureRequestRpc(int kind, Vector3 target, bool allowMovement)
        {
            if (kind < 0 || kind > 4 || State != PlayerState.Normal ||
                !float.IsFinite(target.x) || !float.IsFinite(target.y) || !float.IsFinite(target.z) ||
                (target-transform.position).sqrMagnitude > 25) return;
            ItemGestureShowRpc(kind,target,allowMovement);
        }
        [Rpc(SendTo.Everyone)]
        void ItemGestureShowRpc(int kind, Vector3 target, bool allowMovement)
        {
            // The owner starts immediately instead of restarting when its RPC returns.
            if (!IsOwner) GetComponent<HumanoidPlayerPresentation>()?.ReceiveItemMotion(kind,target,allowMovement);
        }
    }
}
