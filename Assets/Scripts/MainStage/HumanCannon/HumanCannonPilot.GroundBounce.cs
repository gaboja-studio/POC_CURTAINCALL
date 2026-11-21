using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        readonly GroundBounceRequest groundRequest = new();
        Vector3 groundFrameStart, groundContact;
        HumanCannonTrampoline groundSource;
        bool groundFrameAirborne, groundFrameJump;

        void SampleGroundJump()
        {
            groundFrameStart = transform.position;
            groundFrameAirborne = mover != null && mover.IsAirborne;
            groundFrameJump = mover != null && (mover.CurrentJump == JumpKind.InPlace || mover.CurrentJump == JumpKind.Lane);
        }

        void DetectGroundBounce()
        {
            if (groundRequest.Pending)
            {
                var current = HumanCannonTrampoline.Current;
                double now = Time.unscaledTimeAsDouble;
                if (current == null || current != groundSource || groundRequest.Expired(now))
                {
                    system?.RequestGroundBounceCancellation(groundRequest.Sequence);
                    ReleaseMovement();
                }
                else if (groundRequest.TrySend(now))
                    current.RequestGroundBounce(groundRequest.Sequence, groundRequest.Epoch, groundContact);
                return;
            }
            if (IsDriving || mover == null || !mover.Simulated || !mover.Body.enabled
                || player == null || player.State != PlayerState.Normal) return;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline == null || !trampoline.CanRequestGroundBounce || HumanCannonCarry.Held(player.OwnerClientId) != null || trampoline.GrabPending(player)) return;
            bool jumped = groundFrameJump || mover.CurrentJump == JumpKind.InPlace || mover.CurrentJump == JumpKind.Lane;
            // 지상 접근/작은 단차 이탈은 잡기용이다. 점프 또는 장치 위에서 내려온 낙하만 전체 외형 반동을 허용한다.
            if (!jumped && !(groundFrameAirborne && groundFrameStart.y >= trampoline.Surface.y)) return;
            if (!trampoline.TryContact(groundFrameStart, transform.position, mover.BodyRadius, mover.BodyHeight, out var contact)) return;
            TakeMovement();
            double requestedAt = Time.unscaledTimeAsDouble;
            groundSource = trampoline;
            groundContact = contact;
            groundRequest.Begin(groundRequest.NextSequence(), trampoline.Revision, requestedAt);
            Teleport(contact + Vector3.up * 0.05f, transform.rotation);
            if (groundRequest.TrySend(requestedAt))
                trampoline.RequestGroundBounce(groundRequest.Sequence, groundRequest.Epoch, contact);
        }

        internal bool BeginGroundBounce(uint sequence, uint epoch, Vector3 contact, float speed, float controlSpeed)
        {
            if (!groundRequest.Matches(sequence, epoch) || groundSource == null
                || groundSource != HumanCannonTrampoline.Current) return false;
            player?.ResetEquipmentAirCycle();
            Launch(-1, sequence, contact + Vector3.up * 0.05f, transform.rotation, Vector3.up * speed);
            groundRequest.Cancel();
            airControlSpeed = controlSpeed;
            trampolineFlight = true;
            return true;
        }

        internal void RejectGroundBounce(uint sequence, uint epoch)
        {
            if (groundRequest.Matches(sequence, epoch)) ReleaseMovement();
        }
    }
}
