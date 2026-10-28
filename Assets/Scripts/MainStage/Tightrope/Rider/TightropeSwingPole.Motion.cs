using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeSwingPole
    {
        sealed class Binding
        {
            public NetworkPlayer Player;
            public PlayerMover Mover;
            public PlayerBalance Balance;
            public bool BodyEnabled, Simulated;
        }
        Binding binding;
        PlayerMover flightMover;
        PlayerBalance flightBalance;
        NetworkPlayer flightPlayer;
        bool flightSimulation;
        Vector3 flightVelocity;
        float flightSpeedLimit;
        double flightUntil;

        void Bind(NetworkPlayer player)
        {
            if (!player.TryGetComponent(out PlayerMover mover)) return;
            binding = new Binding { Player = player, Mover = mover, Balance = player.GetComponent<PlayerBalance>(),
                BodyEnabled = mover.Body.enabled, Simulated = mover.Simulated };
            mover.Teleported += OnTeleported;
            mover.ClearPendingInput(); mover.Simulated = false; mover.Body.enabled = false;
            if (binding.Balance != null) binding.Balance.IsSuspended = true;
            binding.Balance?.SetAirborne(true); pendingUntil = 0d;
        }
        void PlaceLocal()
        {
            if (binding == null || binding.Mover == null) return;
            binding.Mover.ClearPendingInput();
            binding.Mover.transform.SetPositionAndRotation(WorldFeet(OwnAngle, lane), Quaternion.LookRotation(course.Forward));
            binding.Balance?.SetAirborne(true);
        }
        void EndBinding(bool launch)
        {
            var held = binding; binding = null;
            if (held?.Balance != null) held.Balance.IsSuspended = false;
            if (held == null || held.Mover == null) return;
            var mover = held.Mover;
            if (launch) mover.transform.SetPositionAndRotation(WorldFeet(returnAngle, lane), Quaternion.LookRotation(course.Forward));
            mover.Teleported -= OnTeleported; mover.Body.enabled = held.BodyEnabled; mover.ClearPendingInput();
            // 몸통을 다시 켜면 인간대포와 같이 플레이어끼리 충돌 무시를 복원한다.
            foreach (var other in PlayerMover.All)
                if (other != mover && other.Body != null) Physics.IgnoreCollision(mover.Body, other.Body, true);
            if (!launch || !Active || held.Player == null || held.Player.State != PlayerState.Normal)
            { mover.Simulated = held.Simulated; held.Balance?.SetAirborne(mover.IsAirborne); return; }
            ResetFlight();
            flightMover = mover; flightBalance = held.Balance; flightPlayer = held.Player;
            flightSimulation = held.Simulated; mover.Simulated = false;
            flightVelocity = physicalPole.ReleaseVelocity(returnAngle, angularSpeed);
            flightSpeedLimit = SwingPoleMath.FlightSpeedLimit(mover.transform.position.y - course.transform.position.y,
                flightVelocity.y, GameSettings.Base.Character.Gravity, Mathf.Max(.1f, LandingDistance - course.GetDistance(mover.transform.position)));
            flightVelocity = SwingPoleMath.LimitFlightVelocity(flightVelocity, flightSpeedLimit);
            flightUntil = Clock + 6d; mover.Teleported += OnTeleported;
            Tell("인간대포와 같은 WASD 공중 조절 · 동료는 F 받기 준비");
        }

        Vector3 PredictLanding(bool forwardControl)
        {
            if (!OwnOccupied && flightMover == null) return course.GetLanePosition(assignedLane, LandingDistance);
            Sample(out float value, out float speed);
            Vector3 position = OwnOccupied ? WorldFeet(value, lane) : flightMover != null ? flightMover.transform.position : course.GetLanePosition(assignedLane, LandingDistance);
            Vector3 velocity = OwnOccupied ? physicalPole.ReleaseVelocity(value, speed) : flightVelocity;
            float gravity = GameSettings.Base.Character.Gravity;
            float speedLimit = OwnOccupied ? SwingPoleMath.FlightSpeedLimit(position.y - course.transform.position.y,
                velocity.y, gravity, Mathf.Max(.1f, LandingDistance - course.GetDistance(position))) : flightSpeedLimit;
            velocity = SwingPoleMath.LimitFlightVelocity(velocity, speedLimit);
            float square = velocity.y * velocity.y + 2f * gravity * (position.y - course.transform.position.y);
            if (square < 0f || gravity <= 0f) return position;
            float time = (velocity.y + Mathf.Sqrt(square)) / gravity;
            float control = TightropeCannonAirControl.Current != null ? TightropeCannonAirControl.Current.ControlSpeedFor(Find(holder)) : AirControlSpeed;
            Vector3 controlled = SwingPoleMath.LimitFlightVelocity(velocity + (forwardControl ? course.Forward * control : Vector3.zero), speedLimit);
            Vector3 step = Vector3.ProjectOnPlane(controlled, Vector3.up) * time
                + Vector3.down * (position.y - course.transform.position.y);
            return position + step;
        }
        void SimulateFlight()
        {
            if (flightMover == null) return;
            if (!Active || flightPlayer == null || flightPlayer.State != PlayerState.Normal) { ResetFlight(); return; }
            Vector3 correction = TightropeCannonAirControl.Current != null ? TightropeCannonAirControl.Current.Consume() : Vector3.zero;
            correction = SwingPoleMath.LimitFlightVelocity(flightVelocity + correction, flightSpeedLimit) - flightVelocity;
            Vector3 displacement = BallisticMotion.Step(ref flightVelocity, correction, GameSettings.Base.Character.Gravity, Time.deltaTime);
            var collision = flightMover.Body.Move(displacement);
            if ((collision & CollisionFlags.Above) != 0 && flightVelocity.y > 0f) flightVelocity.y = 0f;
            if ((collision & CollisionFlags.Below) != 0 && flightVelocity.y <= 0f)
            {
                var catches = TightropeCatchSystem.Current;
                NetworkPlayer helper = null;
                float reduction = 0f;
                float distance = course.GetDistance(flightMover.transform.position);
                if (course.TryGetSupportingRope(flightMover, out int supported) && course.IsLaneUsable(supported, distance))
                {
                    catches?.TryGetSwingLandingHelper(flightPlayer, supported, distance, out helper, out reduction);
                    flightBalance?.SetLandingReduction(reduction);
                    flightBalance?.SetAirborne(false);
                    Send(Operation.Landed);
                    if (helper != null) catches.ReportLanding(helper);
                    Tell(distance >= course.SwingGapTo ? "회전봉으로 건넜어요 · 이제 F로 동료 받기" : "반동을 더 키워 건너세요 · W/S 반동, 공중 W 전진");
                }
                else flightBalance?.ForceFall();
                ResetFlight();
            }
            else if (Clock > flightUntil || flightMover.transform.position.y < course.transform.position.y - course.Shape.FallDepth)
            { flightBalance?.ForceFall(); ResetFlight(); }
        }
        void ResetFlight()
        {
            if (flightMover != null)
            {
                TightropeCannonAirControl.Current?.ClearInput();
                flightMover.Teleported -= OnTeleported; flightMover.ClearPendingInput();
                flightMover.Simulated = flightSimulation;
            }
            if (flightBalance != null) { flightBalance.SetLandingReduction(0f); flightBalance.SetAirborne(false); }
            flightMover = null; flightBalance = null; flightPlayer = null; flightVelocity = Vector3.zero;
        }
        void OnTeleported()
        {
            bool wasCrossing = flightMover != null || (mode == PoleMode.Holding && holder == NetworkPlayer.Local?.OwnerClientId);
            localAbort = true; EndBinding(false); ResetFlight();
            if (Active && wasCrossing) Send(Operation.Abort);
        }
        void ResetLocal()
        { EndBinding(false); ResetFlight(); pendingUntil = feedbackUntil = nextDriveAt = 0d; feedback = null; localAbort = false; }
    }
}
