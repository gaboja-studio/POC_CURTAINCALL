using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>연습 동료. 실제 입력·부상·장치 판정을 사용하며 순간이동/무적/자동 부활은 하지 않는다.</summary>
    public sealed class HumanCannonBotBrain
    {
        readonly NetworkPlayer player;
        readonly HumanCannonPilot pilot;
        readonly PlayerMover mover;
        float nextTap;
        Vector3 previousPartner, partnerVelocity;
        bool sampledPartner;
        int held = -1;
        float heldSince;
        public string Status { get; private set; } = "준비 중";

        public HumanCannonBotBrain(NetworkPlayer player, HumanCannonPilot pilot, PlayerMover mover)
        { this.player = player; this.pilot = pilot; this.mover = mover; }

        public void Reset()
        {
            nextTap = Time.time + .5f;
            sampledPartner = false;
            partnerVelocity = Vector3.zero;
            held = -1;
            Status = "출발점에서 준비 중";
        }

        public void Tick(CannonBotRole role)
        {
            if (player.State != PlayerState.Normal) { Status = "봇 사망 · R로 다시 시작"; return; }
            var other = Partner();
            if (other == null || other.State != PlayerState.Normal) { Status = "동료 대기 · 사망하면 R"; return; }
            var position = other.transform.position;
            if (sampledPartner && Time.deltaTime > 0)
                partnerVelocity = Vector3.Lerp(partnerVelocity,
                    Vector3.ClampMagnitude((position - previousPartner) / Time.deltaTime, 35f), .35f);
            previousPartner = position;
            sampledPartner = true;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline == null || HumanCannonSystem.Current == null) return;
            PlayerInputFrame input = role == CannonBotRole.Carrier ? Carry(other, trampoline) : Fly(other, trampoline);
            if (!pilot.IsDriving)
            {
                mover.FreeMovement = true;
                mover.SetMoveInput(input.Posture, input.Move);
                if (input.ActionPressed) mover.RequestJump();
            }
            pilot.ReadInput(input);
        }

        NetworkPlayer Partner()
        {
            foreach (var other in NetworkPlayer.All)
                if (other != null && other != player && other.IsSpawned) return other;
            return null;
        }

        PlayerInputFrame Carry(NetworkPlayer other, HumanCannonTrampoline trampoline)
        {
            var route = HumanCannonRouteSystem.Current;
            if (trampoline.Carrier != player.OwnerClientId)
            {
                if (trampoline.Carrier != CannonSeatState.Nobody) { Status = "트램폴린을 내려놓아 주세요"; return default; }
                Status = "트램폴린 잡으러 이동 중";
                var grab = Walk(trampoline.Handle.position);
                if (trampoline.CanGrab(player)) grab.InteractTapped = Tap();
                return grab;
            }
            if (route != null && route.IsRiding(player.OwnerClientId)) { Status = "리프트 이동 중"; return default; }
            if (route != null && route.TransportState(0).Phase == DeparturePhase.Waiting)
            { Status = "출발 리프트 탑승 중"; return Walk(route.Transport(0).StartPosition); }
            if (route != null && route.TransportState(1).Phase == DeparturePhase.Arrived
                && route.TransportState(2).Phase == DeparturePhase.Waiting)
            { Status = "두 번째 출발 리프트로 이동"; return Walk(route.Transport(2).StartPosition); }

            bool second = player.transform.position.y > 3f && player.transform.position.z > 85f;
            if (!second && other.transform.position.z >= 72f && !HumanCannonSystem.Current.IsFlying(other.OwnerClientId)
                && route != null && route.TransportState(1).Phase == DeparturePhase.Waiting)
            {
                Status = "쉼터 승강기로 이동 중";
                Vector3 lift = route.Transport(1).StartPosition;
                // 왼쪽 연결 바닥을 따라 접근한다. 코스 한가운데에서 대각선으로 구멍을 가로지르지 않는다.
                if (player.transform.position.x > lift.x + 3f)
                {
                    // 측벽의 출입구(z63..66)를 먼저 통과한 뒤 리프트로 접근한다.
                    if (Mathf.Abs(player.transform.position.z - 64.5f) > .5f)
                        return Walk(new Vector3(-19f, lift.y, 64.5f));
                    return Walk(new Vector3(lift.x, lift.y, 64.5f));
                }
                return Walk(lift);
            }

            Vector3 surface = HumanCannonBotSteering.PredictCatch(other.transform.position, partnerVelocity,
                trampoline.Surface.y, GameSettings.Base.Character.Gravity);
            var poles = HumanCannonPoleSystem.Current;
            int grip = poles != null ? poles.HeldBy(other.OwnerClientId) : -1;
            if (grip >= 0 && poles.Pole(grip).LinkedGate != null && !poles.Pole(grip).LinkedGate.CarrierPassed)
            {
                var gate = poles.Pole(grip).LinkedGate;
                surface = gate.transform.position + Vector3.forward * 6;
                Status = "봉 관문 통과 중";
            }
            else Status = "동료 아래로 트램폴린 이동 중";
            surface.x = Mathf.Clamp(surface.x, -19, 19);
            surface.z = Mathf.Clamp(surface.z, second ? 98 : 22, second ? 143 : 70);
            surface.y = player.transform.position.y;
            float handleDistance = Vector3.ProjectOnPlane(trampoline.Surface - trampoline.Handle.position, Vector3.up).magnitude;
            Vector3 carryFacing = trampoline.Surface - trampoline.Handle.position;
            Vector3 target = HumanCannonBotSteering.CarrierFeet(surface, carryFacing, handleDistance);
            // 막힌 관문에 계속 몸을 비비며 부상하지 않고, 동료가 연결 봉을 잡을 때까지 기다린다.
            if (poles != null)
                for (int i = 0; i < poles.PoleCount; i++)
                {
                    var gate = poles.Pole(i).LinkedGate;
                    if (gate != null && gate.Phase == PoleSawPhase.Active && gate.Side(player.transform.position) < 0
                        && gate.Side(target) > -2f) target.z = Mathf.Min(target.z, gate.transform.position.z - 2f);
                }
            return Walk(target, .25f);
        }

        PlayerInputFrame Fly(NetworkPlayer other, HumanCannonTrampoline trampoline)
        {
            var system = HumanCannonSystem.Current;
            var route = HumanCannonRouteSystem.Current;
            var poles = HumanCannonPoleSystem.Current;
            if (pilot.IsHoldingPole)
            {
                if (held != pilot.HeldPole) { held = pilot.HeldPole; heldSince = Time.time; }
                var pole = poles.Pole(held);
                Status = "봉에서 반동 · 운반자 통과 대기";
                bool release = pole.LinkedGate == null ? Time.time - heldSince > 2f : pole.LinkedGate.CarrierPassed;
                return new PlayerInputFrame { Move = release ? 1f : Mathf.Sin((Time.time - heldSince) * 2f), ActionPressed = release };
            }
            held = -1;
            if (pilot.IsFlying)
            {
                Status = "공중 이동 · 트램폴린으로 받쳐 주세요";
                Vector3 target = trampoline.Surface;
                if (other.transform.position.z >= 65 && other.transform.position.z < 80) target = new Vector3(0, 6, 75);
                else if (other.transform.position.z >= 137) target = new Vector3(0, 6.2f, 144);
                if (player.transform.position.z > 150) target = new Vector3(player.transform.position.x, 6.2f, 174);
                if (poles != null)
                    for (int i = 0; i < poles.PoleCount; i++)
                    {
                        var pole = poles.Pole(i);
                        if (pole.LinkedGate == null || pole.LinkedGate.CarrierPassed || pole.Grip.position.z < player.transform.position.z - 2
                            || pole.Grip.position.z - player.transform.position.z > 10) continue;
                        target = pole.HangingPosition(0);
                        break;
                    }
                var input = Air(target);
                if (poles != null && poles.FindGrabbable(player) >= 0) input.InteractTapped = Tap();
                return input;
            }
            if (system.IsSeated(player.OwnerClientId))
            {
                bool ready = trampoline.Carrier == other.OwnerClientId;
                if (route != null && player.transform.position.z < 10) ready &= route.TransportState(0).Phase == DeparturePhase.Arrived;
                else if (route != null && player.transform.position.z < 90) ready &= route.TransportState(2).Phase == DeparturePhase.Arrived;
                else ready = true;
                Status = ready ? "대포 발사" : "대포 대기 · 트램폴린을 들고 출발해 주세요";
                return new PlayerInputFrame { InteractTapped = ready && Tap() };
            }
            if (player.transform.position.z > 163) { Status = "목적지 도착"; return default; }
            int station = player.transform.position.z < 65 ? 0 : player.transform.position.z < 140 ? 3 : 6;
            var targetStation = system.Station(station);
            Status = "대포 탑승 준비 중";
            var approach = Walk(targetStation.BoardingPoint.position);
            if (system.FindBoardableStation(player) >= 0) approach.InteractTapped = Tap();
            return approach;
        }

        PlayerInputFrame Walk(Vector3 target, float stop = .35f)
        {
            Vector2 input = HumanCannonBotSteering.PlanarInput(player.transform.position, target, mover.CourseForward, stop);
            return new PlayerInputFrame { Posture = input.x, Move = input.y };
        }

        PlayerInputFrame Air(Vector3 target)
        {
            float height = player.transform.position.y - target.y;
            float gravity = GameSettings.Base.Character.Gravity;
            float vertical = pilot.FlightVelocity.y;
            float time = Mathf.Clamp((vertical + Mathf.Sqrt(Mathf.Max(0, vertical * vertical + 2 * gravity * height))) / gravity, .2f, 3f);
            Vector3 desired = Vector3.ProjectOnPlane((target - player.transform.position) / time - pilot.FlightVelocity, Vector3.up);
            Vector3 forward = CameraViewDirector.Current != null && CameraViewDirector.Current.UsesMouseLook
                ? CameraViewDirector.Current.ViewForward : mover.CourseForward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector2 input = Vector2.ClampMagnitude(new Vector2(Vector3.Dot(desired, Vector3.Cross(Vector3.up, forward)),
                Vector3.Dot(desired, forward)) / 4.5f, 1);
            return new PlayerInputFrame { Posture = input.x, Move = input.y };
        }

        bool Tap()
        {
            if (Time.time < nextTap) return false;
            nextTap = Time.time + .7f;
            return true;
        }
    }
}
