using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        internal bool MatchesFlight(ulong actor, int index, uint launch, uint segment) => flying.Matches(actor, index, launch, segment);
        internal void BeginPoleFlight(ulong actor, uint launch) => flying.Begin(actor, CannonFlightState.PoleEntrySource, launch);

        /// <summary>검토 모드가 호스트 승인 뒤 호출한다. 기존 점유/비행 해제 메시지를 사용하고 부상/출발점은 바꾸지 않는다.</summary>
        public void ServerReleaseForInspection(ulong actor)
        {
            if (session == null || !session.IsHost) return;
            HumanCannonPoleSystem.Current?.ServerReleaseForInspection(actor);
            HumanCannonTrampoline.Current?.ServerReleaseForInspection(actor);
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].State.Occupant == actor && seats[i].State.TryApply(CannonAction.Release, actor, seats[i].State.Revision))
                    Publish(i, CannonAction.Release, actor, Vector3.zero, Quaternion.identity, Vector3.zero);
            flying.RemoveWhere(id => id == actor, SendFlightRemoved);
            if (boundPlayer != null && boundPlayer.OwnerClientId == actor && pilot != null) pilot.ReleaseMovement();
        }

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
