using CurtainCall.Player;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        float nextItemGesture;
        public void SendItemGesture(int kind, Vector3 target)
        {
            if (IsSpawned && IsOwner && kind >= 0 && kind <= 3) ItemGestureRequestRpc(kind, target);
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ItemGestureRequestRpc(int kind, Vector3 target)
        {
            if (kind < 0 || kind > 3 || State != PlayerState.Normal || Time.time < nextItemGesture ||
                !float.IsFinite(target.x) || !float.IsFinite(target.y) || !float.IsFinite(target.z) ||
                (target-transform.position).sqrMagnitude > 25) return;
            nextItemGesture = Time.time + .2f;
            ItemGestureShowRpc(kind,target);
        }
        [Rpc(SendTo.Everyone)]
        void ItemGestureShowRpc(int kind, Vector3 target) => GetComponent<HumanoidPlayerPresentation>()?.ReceiveItemMotion(kind,target);
    }
}
