using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        readonly GroundBounceRequest groundRequest = new();
        Vector3 groundFrameStart;

        void SampleGroundJump()
        {
            groundFrameStart = transform.position;
        }

        void DetectGroundBounce()
        {
            if (groundRequest.Pending && HumanCannonTrampoline.Current == null) ReleaseMovement();
            if (IsDriving || mover == null || !mover.Simulated || !mover.Body.enabled
                || player == null || player.State != PlayerState.Normal) return;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline == null || trampoline.Carrier == player.OwnerClientId) return;
            // 걷기·점프·낙하 모두 몸통이 장치 전체 외형에 닿으면 기존 반동을 요청한다.
            if (!trampoline.TryContact(groundFrameStart, transform.position, mover.BodyRadius, mover.BodyHeight, out var contact)) return;
            TakeMovement();
            groundRequest.Begin(trampoline.NextGroundSequence(), trampoline.Revision);
            Teleport(contact + Vector3.up * 0.05f, transform.rotation);
            trampoline.RequestGroundBounce(groundRequest.Sequence, contact);
        }

        internal bool BeginGroundBounce(uint sequence, uint epoch, Vector3 contact, float speed, float controlSpeed)
        {
            if (!groundRequest.Matches(sequence, epoch)) return false;
            Launch(-1, sequence, contact + Vector3.up * 0.05f, transform.rotation, Vector3.up * speed);
            groundRequest.Cancel();
            airControlSpeed = controlSpeed;
            return true;
        }

        internal void RejectGroundBounce(uint sequence, uint epoch)
        {
            if (groundRequest.Matches(sequence, epoch)) ReleaseMovement();
        }
    }
}
