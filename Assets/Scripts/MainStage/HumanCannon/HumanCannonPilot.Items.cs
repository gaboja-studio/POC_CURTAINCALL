using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 광대 벌룬 바지(기물 시스템 31784961, Task-092). 소유자 화면이 비행을 계산하므로 여기서 적용한다.
    /// - 대포 발사·트램펄린(스프링 신발 포함) 반동으로 시작한 비행 구간에서 하강할 때만 수직 하강 변위를 줄인다(초안 30%). 상승·수평 조작은 그대로.
    /// - 봉에서 놓은 비행은 대상이 아니다.
    /// - 구간당 한 번 켜지고, 그 구간의 하강이 끝나면(착지·반동·봉 잡기·비행 종료) 판정 화면에 알려 1 소모·쿨타임(초안 3초)을 시작한다.
    ///   마지막 회차도 감속을 끝까지 준 뒤 파괴된다. 0회·쿨타임 중이면 켜지지 않는다.
    /// 위치는 기존 NetworkTransform으로 공유되므로 다른 화면에도 느린 하강이 보인다.
    /// </summary>
    public sealed partial class HumanCannonPilot
    {
        bool pantsActive;
        int pantsStation;
        uint pantsRevision, pantsSegment;
        float pantsLocalReadyAt;

        /// <summary>이번 하강에 벌룬 바지 감속이 걸려 있다.</summary>
        public bool BalloonPantsActive => pantsActive;

        static float PantsSlowdown => Mathf.Clamp01(ItemCatalog.Get(ItemUses.BalloonPants)?.effect ?? .3f);

        Vector3 ApplyBalloonPants(Vector3 displacement)
        {
            if (velocity.y >= 0f || steerPoleMomentum) return displacement;
            if (!pantsActive && !TryStartBalloonPants()) return displacement;
            if (displacement.y < 0f) displacement.y *= 1f - PantsSlowdown;
            return displacement;
        }

        bool TryStartBalloonPants()
        {
            var carry = PropCarry.Instance;
            if (carry == null || player == null || Time.unscaledTime < pantsLocalReadyAt
                || !carry.EquipmentReady(player.OwnerClientId, ItemUses.BalloonPants)) return false;
            pantsActive = true;
            pantsStation = flightStationIndex;
            pantsRevision = launchRevision;
            pantsSegment = flightSegment;
            carry.ShowLocalEquipmentCue(ItemUses.BalloonPants);
            return true;
        }

        void WatchBalloonPantsEnd()
        {
            if (!pantsActive) return;
            bool sameDescent = IsFlying && !IsHoldingPole && !awaitingLanding && !awaitingBounce
                && flightStationIndex == pantsStation && launchRevision == pantsRevision && flightSegment == pantsSegment;
            if (!sameDescent) FinishBalloonPants();
        }

        void FinishBalloonPants()
        {
            if (!pantsActive) return;
            pantsActive = false;
            // 판정 화면의 쿨타임이 돌아오기 전에 다음 구간에서 다시 켜지지 않게 이 화면도 같은 시간만큼 기다린다.
            pantsLocalReadyAt = Time.unscaledTime + (ItemCatalog.Get(ItemUses.BalloonPants)?.cooldown ?? 0f);
            PropCarry.Instance?.ReportEquipmentUsed(ItemUses.BalloonPants);
        }
    }
}
