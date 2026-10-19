using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeTrapeze
    {
        sealed class LocalControl
        {
            public NetworkPlayer Player;
            public PlayerMover Mover;
            public PlayerBalance Balance;
            public bool Simulation, BodyEnabled;
        }
        readonly CannonAirControl air = new();
        readonly HumanCannonInjurySettings defaultInjuries = new();
        LocalControl localControl;
        bool localFlying;
        uint localFlightToken, finishedFlightToken;
        Vector3 flightVelocity;
        double flightDeadline;

        void ReconcileLocal()
        {
            var local = NetworkPlayer.Local;
            if (!Active || local == null || !local.IsSpawned || local.State != PlayerState.Normal)
            { ResetLocal(false); return; }
            bool held = FindHeld(local, out int index, out bool lower);
            bool flying = flights.TryGetValue(local.OwnerClientId, out var flight);
            if (localAborted)
            {
                if (!held && !flying && !localAbortWaiting) localAborted = false;
                return;
            }
            if (localControl != null) localControl.Mover.Simulated = false;
            if (held)
            {
                if (!TakeControl(local)) return;
                localFlying = false; localFlightToken = 0; air.Clear();
                localControl.Mover.Body.enabled = false;
                localControl.Mover.ClearPendingInput();
                localControl.Mover.transform.SetPositionAndRotation(Feet(index, lower), Quaternion.LookRotation(course.Forward));
                localControl.Balance?.SetAirborne(true); pendingUntil = 0d;
            }
            else if (flying && flight.Token > finishedFlightToken)
            {
                if (!localFlying || localFlightToken != flight.Token) BeginFlight(local, flight);
            }
            else if (localControl != null) ReleaseControl(false);
        }
        bool TakeControl(NetworkPlayer player)
        {
            if (localControl != null) return localControl.Player == player;
            if (!player.TryGetComponent(out PlayerMover mover) || mover.Body == null) return false;
            localControl = new LocalControl { Player = player, Mover = mover, Balance = player.GetComponent<PlayerBalance>(),
                Simulation = mover.Simulated, BodyEnabled = mover.Body.enabled };
            mover.Teleported += OnTeleported; mover.ClearPendingInput(); mover.Simulated = false;
            mover.ResetExternalMotion(true); localControl.Balance?.SetAirborne(true);
            TightropeCatchSystem.Current?.CancelLocal();
            return true;
        }
        void BeginFlight(NetworkPlayer player, FlightState flight)
        {
            if (!TakeControl(player)) return;
            var mover = localControl.Mover;
            mover.Body.enabled = false;
            mover.transform.SetPositionAndRotation(flight.Position, Quaternion.LookRotation(course.Forward));
            mover.Body.enabled = localControl.BodyEnabled; RestoreCollisions(mover);
            mover.ResetExternalMotion(true); localControl.Balance?.SetAirborne(true);
            localFlying = true; localFlightToken = flight.Token;
            flightVelocity = flight.Velocity; flightDeadline = flight.Until; pendingUntil = 0d;
            Tell("공중 WASD 조절 · 다음 봉 또는 매달린 동료에게 가까이 가서 F");
        }
        void StepFlight()
        {
            if (!localFlying || localControl == null) return;
            var held = localControl;
            if (!Active || held.Player == null || held.Player.State != PlayerState.Normal || held.Mover == null || !held.Mover.Body.enabled)
            { ResetLocal(false); return; }
            // Locally valid catches await the host at the requested position.
            // Otherwise the low final pole can be approved after local fall death.
            // ReconcileLocal clears the wait on approval; rejection times out.
            if (Clock < pendingUntil) { air.Clear(); return; }
            var injuries = HumanCannonSystem.Current != null ? HumanCannonSystem.Current.Injuries : defaultInjuries;
            int lostLegs = held.Player.TryGetComponent(out PlayerCondition condition) ? condition.LostLegCount : 0;
            Vector3 correction = air.ConsumeVelocity(Time.frameCount, InputGate.IsBlocked, course.Forward, AirControlSpeed * injuries.AirMultiplier(lostLegs));
            Vector3 displacement = BallisticMotion.Step(ref flightVelocity, correction, GameSettings.Base.Character.Gravity, Time.deltaTime);
            var collision = held.Mover.Body.Move(displacement);
            if ((collision & CollisionFlags.Above) != 0 && flightVelocity.y > 0f) flightVelocity.y = 0f;
            if ((collision & CollisionFlags.Below) != 0 && flightVelocity.y <= 0f)
            {
                float distance = course.GetDistance(held.Mover.transform.position);
                if (course.TryGetSupportingRope(held.Mover, out int lane) && course.IsLaneUsable(lane, distance))
                {
                    NetworkPlayer helper = null; float reduction = 0f;
                    var catches = TightropeCatchSystem.Current;
                    catches?.TryGetSwingLandingHelper(held.Player, lane, distance, out helper, out reduction);
                    held.Balance?.SetLandingReduction(reduction);
                    finishedFlightToken = localFlightToken;
                    Send(Command.Landed);
                    ReleaseControl(true);
                    if (helper != null) catches.ReportLanding(helper);
                    Tell(distance >= course.TrapezeGapTo ? "줄에 착지했어요 · 남은 동료는 마지막 낮은 봉으로 건널 수 있어요" : "줄에 착지했어요 · 다시 목마로 높은 봉에 올라가세요");
                }
                else FailFlight();
            }
            else if (Clock > flightDeadline || held.Mover.transform.position.y < course.transform.position.y - course.Shape.FallDepth)
                FailFlight();
        }
        void FailFlight()
        {
            if (localControl == null) return;
            finishedFlightToken = localFlightToken;
            localControl.Balance?.ForceFall(); Send(Command.Abort); ReleaseControl(false);
        }
        void ReleaseControl(bool grounded)
        {
            var held = localControl; localControl = null;
            localFlying = false; localFlightToken = 0; flightVelocity = Vector3.zero; air.Clear();
            if (held == null || held.Mover == null) return;
            var mover = held.Mover;
            mover.Teleported -= OnTeleported; mover.ClearPendingInput();
            mover.Body.enabled = held.BodyEnabled; RestoreCollisions(mover); mover.Simulated = held.Simulation;
            if (grounded) mover.ResetExternalMotion(false);
            else if (held.Player != null && held.Player.State == PlayerState.Normal && held.Balance != null && !held.Balance.HasFallen)
            {
                // R/외부 텔레포트가 이미 정한 공중 상태를 덮어쓰지 않는다.
                held.Balance.SetLandingReduction(1f); held.Balance.SetAirborne(mover.IsAirborne);
            }
        }
        static void RestoreCollisions(PlayerMover mover)
        {
            if (mover.Body == null || !mover.Body.enabled) return;
            foreach (var other in PlayerMover.All)
                if (other != mover && other.Body != null) Physics.IgnoreCollision(mover.Body, other.Body, true);
        }
        void OnTeleported()
        {
            localAborted = true; finishedFlightToken = localFlightToken;
            localAbortWaiting = session != null && !session.IsHost;
            ReleaseControl(false); Send(Command.Abort);
        }
        void ResetLocal(bool resetTicket = true)
        {
            ReleaseControl(false); pendingUntil = nextDrive = feedbackUntil = 0d; feedback = null;
            if (resetTicket) { finishedFlightToken = 0; localAborted = localAbortWaiting = false; }
        }

        /// <summary>현재 반동과 전진 공중 입력으로 다음 잡기 범위에 접근 가능한지 추정한다.</summary>
        public bool CanLaunchTowardNext(int index, bool lower)
        {
            if (!Active || index < 0 || index >= PoleTotal || poles[index].Upper == Nobody) return false;
            Sample(index, out float angle, out float speed);
            Vector3 position = Feet(index, lower), velocity = poles[index].Physics.ReleaseVelocity(angle, speed);
            Vector3 target = index + 1 < PoleTotal ? Feet(index + 1, poles[index + 1].Upper != Nobody)
                : course.GetLanePosition(Lane, course.TrapezeGapTo + 1f);
            float gravity = GameSettings.Base.Character.Gravity;
            for (float time = .08f; time <= 3f; time += .04f)
            {
                Vector3 predicted = position + (velocity + course.Forward * AirControlSpeed) * time + Vector3.down * (.5f * gravity * time * time);
                if (index == PoleTotal - 1)
                {
                    if (velocity.y - gravity * time <= 0f && predicted.y <= target.y + .15f)
                        return course.GetDistance(predicted) >= course.TrapezeGapTo + .35f;
                }
                else if (Mathf.Abs(predicted.y - target.y) <= .4f
                    && Vector3.ProjectOnPlane(predicted - target, Vector3.up).sqrMagnitude <= .7f * .7f) return true;
            }
            return false;
        }
    }
}
