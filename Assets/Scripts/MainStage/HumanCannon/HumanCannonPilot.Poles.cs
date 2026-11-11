using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        public int HeldPole { get; private set; } = -1;
        public bool IsHoldingPole => HeldPole >= 0;
        float poleFlightSpeedLimit;
        float polePumpInput;

        bool ReadPoleInput(in PlayerInputFrame input)
        {
            var poles = HumanCannonPoleSystem.Current;
            if (poles == null) return false;
            if (IsHoldingPole)
            {
                polePumpInput = input.Move;
                poles.RequestSwing(HeldPole, input.Move);
                // Space spends the stored swing; holding F only lets go below the grip.
                // Resolve the latch even on a jump frame so a stale hold cannot release the next pole.
                bool drop = latch.Read(input.InteractTapped, input.InteractLongPressed,
                    input.InteractHoldProgress) == CannonAction.Cancel;
                if (input.ActionPressed) poles.RequestRelease(HeldPole);
                else if (drop) poles.RequestDrop(HeldPole);
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
                if (nearest >= 0 && CommonInteractionSelection.Consume(player, poles.Pole(nearest)))
                {
                    poles.RequestGrab(nearest, flightStationIndex, launchRevision, flightSegment);
                    return true;
                }
            }
            if (!IsDriving && poles.CanEnterFromPlatform(player))
            {
                int nearest = poles.FindGrabbable(player);
                if (nearest >= 0 && CommonInteractionSelection.Consume(player, poles.Pole(nearest))) { poles.RequestPlatformGrab(nearest); return true; }
            }
            return false;
        }

        internal void HoldPole(int index, int station, uint launch, uint segment)
        {
            if (HeldPole == index) return;
            CurtainCall.Tightrope.PiggybackSystem.Current?.GetComponent<CurtainCall.Tightrope.PiggybackFollow>()?.ReleaseForDevice(player);
            TakeMovement();
            // 외줄 옆점프/일반 점프의 옛 목표가 다음 착지에 다시 적용되지 않게 한다.
            mover.ResetExternalMotion(true);
            HeldPole = index;
            polePumpInput = 0f;
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
            UpdatePolePose(true);
        }

        internal void LeavePole(int station, uint launch, uint segment, Vector3 position,
            Quaternion rotation, Vector3 launchVelocity, float controlSpeed)
        {
            var pole = HumanCannonPoleSystem.Current != null ? HumanCannonPoleSystem.Current.Pole(HeldPole) : null;
            poleFlightSpeedLimit = pole != null ? pole.FlightSpeedLimit(GameSettings.HumanCannon.PoleJumpHorizontalSpeedLimit)
                : GameSettings.HumanCannon.PoleJumpHorizontalSpeedLimit;
            HeldPole = -1;
            if (TryGetComponent(out PlayerBalance balance)) balance.IsSuspended = false;
            launchVelocity = CannonAirControl.LimitPoleHorizontalSpeed(launchVelocity, poleFlightSpeedLimit);
            Launch(station, launch, position, rotation, launchVelocity);
            flightSegment = segment;
            airControlSpeed = controlSpeed;
            steerPoleMomentum = true;
            airControl.BeginPoleFlight(polePumpInput);
            latch.Reset();
        }

        void UpdatePolePose(bool teleport = false)
        {
            if (!IsHoldingPole) return;
            var poles = HumanCannonPoleSystem.Current;
            var pole = poles != null ? poles.Pole(HeldPole) : null;
            if (pole == null) return;
            pole.RefreshMovingPose();
            // A held rider follows a continuous path. Reserve controller resets and
            // network teleports for discrete grab/release transitions.
            Vector3 position = poles.HangingPosition(HeldPole, player.OwnerClientId);
            Quaternion rotation = poles.RiderRotation(HeldPole, player.OwnerClientId);
            if (teleport) Teleport(position, rotation);
            else transform.SetPositionAndRotation(position, rotation);
        }
    }
}
