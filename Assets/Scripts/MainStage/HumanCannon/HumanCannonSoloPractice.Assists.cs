using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSoloPractice
    {
        // 이 LateUpdate는 기존 장치 Update 뒤, trampoline/pilot LateUpdate 전에 실행한다.
        void LateUpdate()
        {
            if (!IsPracticing || busy || !SoloHost) return;
            var player = NetworkPlayer.Local;
            if (player == null) return;
            if (!fireEnabled)
                foreach (var fire in runtime.GetComponentsInChildren<HumanCannonFireWall>()) fire.ResetWall();
            if (IsCarrierSection && selectedGate != null)
            {
                double now = Time.timeAsDouble;
                helperHold.Tick(now, selectedPole.WarningSeconds);
                selectedGate.Present(helperHold.Phase, selectedGate.CarrierPassed, now);
                selectedGate.HostTick(helperHold.Phase, HelperActor);
            }
            else if (partnerAssist && poles != null && poles.HeldBy(player.OwnerClientId) >= 0)
            {
                // 실제로 봉을 잡은 후에만 상대 운반자의 통과를 대신한다. 반동·잡기·도약은 직접 조작한다.
                var gate = poles.Pole(poles.HeldBy(player.OwnerClientId)).LinkedGate;
                if (gate != null) gate.Present(gate.Phase, true, poles.Clock);
            }
            if (catchAssist && section is 0 or 2 or 7 && trampoline.Carrier == CannonSeatState.Nobody
                && cannon.IsFlying(player.OwnerClientId))
            {
                // 두 번째 사람의 받기 위치만 대신한다. 실제 sweep/반동 판정은 그대로 사용한다.
                Vector3 position = player.transform.position;
                position.y = floorY;
                trampoline.transform.position = position;
            }
        }

        public void TogglePartnerHold()
        {
            if (!IsPracticing || busy || !SoloHost || !IsCarrierSection || selectedPole == null) return;
            if (helperHold.IsOccupied)
                helperHold.TryRelease(HelperActor, helperHold.Revision, Time.timeAsDouble, selectedPole.WarningSeconds);
            else helperHold.TryGrab(HelperActor, helperHold.Revision, Time.timeAsDouble, selectedPole.HoldSeconds);
        }
    }
}
