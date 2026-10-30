using CurtainCall.Player;
using CurtainCall.Settings;
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
            return input.InteractTapped && TryRequestNearbyPole();
        }

        public bool TryRequestNearbyPole()
        {
            var poles = HumanCannonPoleSystem.Current;
            if (poles == null || IsHoldingPole) return false;
            if (IsFlying && !awaitingBounce && !awaitingLanding)
            {
                int nearest = poles.FindGrabbable(player);
                if (nearest >= 0)
                {
                    poles.RequestGrab(nearest, flightStationIndex, launchRevision, flightSegment);
                    return true;
                }
            }
            if (!IsDriving && poles.CanEnterFromPlatform(player))
            {
                int nearest = poles.FindGrabbable(player);
                if (nearest >= 0) { poles.RequestPlatformGrab(nearest); return true; }
            }
            return false;
        }

        internal void HoldPole(int index, int station, uint launch, uint segment)
        {
            if (HeldPole == index) return;
            CurtainCall.Tightrope.PiggybackSystem.Current?.GetComponent<CurtainCall.Tightrope.PiggybackFollow>()?.ReleaseForTrapeze(player);
            TakeMovement();
            // 외줄 옆점프/일반 점프의 옛 목표가 다음 착지에 다시 적용되지 않게 한다.
            mover.ResetExternalMotion(true);
            HeldPole = index;
            if (TryGetComponent(out PlayerBalance balance)) balance.IsSuspended = true;
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
            if (TryGetComponent(out PlayerBalance balance)) balance.IsSuspended = false;
            launchVelocity = CannonAirControl.LimitPoleHorizontalSpeed(launchVelocity, GameSettings.HumanCannon.PoleJumpHorizontalSpeedLimit);
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
            pole.RefreshMovingPose();
            Teleport(poles.HangingPosition(HeldPole, player.OwnerClientId), pole.Grip.rotation);
        }
    }
}
