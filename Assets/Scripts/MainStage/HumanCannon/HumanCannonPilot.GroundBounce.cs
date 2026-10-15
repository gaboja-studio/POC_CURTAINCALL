using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        readonly GroundBounceRequest groundRequest = new();
        Vector3 groundFrameStart;
        bool sampledGroundJump;

        void SampleGroundJump()
        {
            groundFrameStart = transform.position;
            sampledGroundJump = !IsDriving && mover != null && mover.Simulated
                && mover.CurrentJump == JumpKind.InPlace;
        }

        void DetectGroundBounce()
        {
            if (groundRequest.Pending && HumanCannonTrampoline.Current == null) ReleaseMovement();
            if (IsDriving || mover == null || !mover.Simulated || !mover.Body.enabled
                || player == null || player.State != PlayerState.Normal) return;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline == null || trampoline.Carrier == player.OwnerClientId) return;
            // 일반 이동 Update 전후 발 위치를 비교한다. 착지 프레임에 JumpKind가 지워져도 검출한다.
            if ((!sampledGroundJump && mover.CurrentJump != JumpKind.InPlace)
                || transform.position.y >= groundFrameStart.y) return;
            if (!trampoline.TryContact(groundFrameStart, transform.position, mover.BodyRadius, out var contact)) return;
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
