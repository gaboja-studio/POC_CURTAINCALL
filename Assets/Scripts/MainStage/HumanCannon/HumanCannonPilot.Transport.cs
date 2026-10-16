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
            if (onTransport) transform.SetPositionAndRotation(position, rotation);
        }

        internal void EndTransport()
        {
            if (!onTransport) return;
            Teleport(transform.position, transform.rotation);
            ReleaseMovement();
        }
    }
}
