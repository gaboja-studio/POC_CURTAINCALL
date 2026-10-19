using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        public int HeldPole { get; private set; } = -1;
        public bool IsHoldingPole => HeldPole >= 0;

        bool ReadPoleInput(in PlayerInputFrame input)
        {
            var poles = HumanCannonPoleSystem.Current;
            if (poles == null) return false;
            if (IsHoldingPole)
            {
                poles.RequestSwing(HeldPole, input.Move);
                if (input.ActionPressed ||
                    latch.Read(input.InteractTapped, input.InteractLongPressed, input.InteractHoldProgress) == CannonAction.Cancel)
                    poles.RequestRelease(HeldPole);
                return true;
            }
            if (IsFlying && !awaitingBounce && !awaitingLanding && input.InteractTapped)
            {
                int nearest = poles.FindGrabbable(player);
                if (nearest >= 0)
                {
                    poles.RequestGrab(nearest, flightStationIndex, launchRevision, flightSegment);
                    return true;
                }
            }
            return false;
        }

        internal void HoldPole(int index, int station, uint launch, uint segment)
        {
            if (HeldPole == index) return;
            TakeMovement();
            HeldPole = index;
            stationIndex = -1;
            flightStationIndex = station;
            launchRevision = launch;
            flightSegment = segment;
            IsFlying = true;
            awaitingBounce = awaitingLanding = rejectedSurface = false;
            groundRequest.Cancel();
            airControl.Clear();
            velocity = Vector3.zero;
            steerPoleMomentum = false;
            latch.Reset();
            UpdatePolePose();
        }

        internal void LeavePole(int station, uint launch, uint segment, Vector3 position,
            Quaternion rotation, Vector3 launchVelocity, float controlSpeed)
        {
            HeldPole = -1;
            Launch(station, launch, position, rotation, launchVelocity);
            flightSegment = segment;
            airControlSpeed = controlSpeed;
            steerPoleMomentum = true;
            latch.Reset();
        }

        void UpdatePolePose()
        {
            if (!IsHoldingPole) return;
            var poles = HumanCannonPoleSystem.Current;
            var pole = poles != null ? poles.Pole(HeldPole) : null;
            if (pole == null) return;
            Teleport(poles.HangingPosition(HeldPole), pole.Grip.rotation);
        }
    }
}
