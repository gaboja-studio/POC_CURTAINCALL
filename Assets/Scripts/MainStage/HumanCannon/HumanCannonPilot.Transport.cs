using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        bool onTransport;

        internal void BeginTransport()
        {
            if (onTransport) return;
            TakeMovement();
            onTransport = true;
            latch.Reset();
        }

        internal void SetTransportPose(Vector3 position, Quaternion rotation)
        {
            if (!onTransport) return;
            var trampoline = HumanCannonTrampoline.Current;
            // 도착 순간 Teleport도 마지막 시선 방향을 공유하도록 운반자의 회전을 보존한다.
            if (player != null && trampoline != null && trampoline.Carrier == player.OwnerClientId)
                rotation = transform.rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        internal void EndTransport()
        {
            if (!onTransport) return;
            Teleport(transform.position, transform.rotation);
            ReleaseMovement();
        }
    }
}
