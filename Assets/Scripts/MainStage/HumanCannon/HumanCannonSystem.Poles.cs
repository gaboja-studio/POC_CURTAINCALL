using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        internal bool MatchesFlight(ulong actor, int index, uint launch, uint segment) => flying.Matches(actor, index, launch, segment);

        internal void ApplyPoleGrab(ulong actor, int station, uint launch, uint segment, int pole)
        {
            if (!flying.TryRebound(actor, station, launch, segment)) return;
            if (boundPlayer != null && actor == boundPlayer.OwnerClientId && pilot != null)
                pilot.HoldPole(pole, station, launch, segment + 1);
        }

        internal void ApplyPoleRelease(ulong actor, int station, uint launch, uint segment,
            Vector3 position, Quaternion rotation, Vector3 velocity, float controlSpeed)
        {
            if (!flying.TryRebound(actor, station, launch, segment)) return;
            if (boundPlayer != null && actor == boundPlayer.OwnerClientId && pilot != null)
                pilot.LeavePole(station, launch, segment + 1, position, rotation, velocity, controlSpeed);
        }

        internal void SyncPoleHold(ulong actor, int station, uint launch, uint segment, int pole)
        {
            // 늦은 참가자도 봉 점유를 볼 수 있다. 소유자의 기존 비행 식별자는 새 상태와 일치할 때만 고정한다.
            if (boundPlayer != null && actor == boundPlayer.OwnerClientId && pilot != null
                && flying.Matches(actor, station, launch, segment)) pilot.HoldPole(pole, station, launch, segment);
        }

        internal void AbandonPole(ulong actor)
        {
            flying.Remove(actor);
            if (boundPlayer != null && actor == boundPlayer.OwnerClientId && pilot != null) pilot.ReleaseMovement();
        }

        internal void CancelLocalPole(int index)
        {
            if (pilot != null && pilot.HeldPole == index)
            {
                // 기능 비활성화/연결 해제 때 이동 잠금이 남지 않는다. R은 기존 재시작 경로로 복구한다.
                if (boundPlayer != null) flying.Remove(boundPlayer.OwnerClientId);
                pilot.ReleaseMovement();
            }
        }
    }
}
