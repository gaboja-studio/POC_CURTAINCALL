using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 인간대포 코스용 착지 그림자. 표시·외곽선·안전 판정은 공통 <see cref="LandingShadowMarkers"/>를 따르고,
    /// 여기서는 대포 좌석·비행·이동 장치 탑승 상태와 코스 바닥·착지 구역·이동 장치·트램펄린만 착지면으로 쓰는 규칙을 더한다.
    /// 인간대포 시스템이 있는 씬에서만 동작하며, 그동안 공통 자동 표시는 쉰다.
    /// </summary>
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed partial class HumanCannonGroundMarkers : LandingShadowMarkers
    {
        [Tooltip("착지 성공 판정과 별개로 발밑 표시에 사용할 코스 바닥. 이 목록은 게임 규칙을 바꾸지 않는다.")]
        [SerializeField] Collider[] courseFloors = System.Array.Empty<Collider>();

        protected override bool Ready => base.Ready && HumanCannonSystem.Current != null;

        protected override bool IsAirborne(NetworkPlayer player)
        {
            var cannon = HumanCannonSystem.Current;
            if (cannon == null) return base.IsAirborne(player);
            ulong id = player.OwnerClientId;
            return player.State == PlayerState.Normal && !cannon.IsSeated(id)
                && (player.IsAirborne || cannon.IsFlying(id))
                && (HumanCannonRouteSystem.Current == null || !HumanCannonRouteSystem.Current.IsRiding(id));
        }

        /// <summary>코스 바닥·착지 구역·이동 장치 발판만 착지면으로 쓴다. 장식물·구멍 아래에는 가상의 바닥을 만들지 않는다.</summary>
        protected override bool AcceptsSurface(RaycastHit hit)
        {
            if (!base.AcceptsSurface(hit)) return false;
            if (System.Array.IndexOf(courseFloors, hit.collider) >= 0 || HumanCannonLandingZone.ContainsLanding(hit.point)) return true;
            var transport = hit.collider.GetComponentInParent<HumanCannonTransport>();
            return transport != null && transport.isActiveAndEnabled && transport.Platform != null
                && hit.collider.transform.IsChildOf(transport.Platform) && transport.ContainsFeet(hit.point, 0);
        }

        protected override bool TrampolineBlocked(Vector3 candidate) => HumanCannonRouteSystem.Current != null
            && HumanCannonRouteSystem.Current.BlocksTrampolineBounce(candidate);
    }
}
