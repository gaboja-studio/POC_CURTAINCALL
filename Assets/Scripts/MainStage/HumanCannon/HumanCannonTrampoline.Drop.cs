using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>운반자가 공중에서 죽으면 트램펄린이 그 자리에서 바로 아래 지면까지 떨어진다(2026-11-05 사용자 요청).</summary>
    public sealed partial class HumanCannonTrampoline
    {
        const float DropMinHeight = 0.05f;
        // 원격 화면은 운반자 위치 공유 지연만큼 수평으로 어긋날 수 있다. 이 안이면 확정 지점 위에서 떨어뜨린다.
        const float DropHorizontalTolerance = 2f;
        const float DropProbeDistance = 60f;

        readonly RaycastHit[] dropHits = new RaycastHit[16];
        bool dropping;
        Vector3 dropTarget;
        float dropSpeed;

        /// <summary>호스트가 공유하는 위치. 떨어지는 중이면 도착할 지면 위치를 보낸다.</summary>
        Vector3 SharedPosition => dropping ? dropTarget : transform.position;

        /// <summary>트램펄린 바로 아래 가장 가까운 지면. 플레이어·자기 자신·트리거·벽면은 제외한다.</summary>
        bool TryFindGround(out Vector3 ground)
        {
            ground = transform.position;
            Vector3 origin = transform.position + Vector3.up * DropMinHeight;
            float distance = HumanCannonRouteSystem.Current != null
                ? Mathf.Max(0f, origin.y - HumanCannonRouteSystem.Current.FallHeight) : DropProbeDistance;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, dropHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = dropHits[i];
                if (hit.distance >= nearest || hit.normal.y < 0.7f || hit.transform.IsChildOf(transform)
                    || hit.collider.GetComponentInParent<NetworkPlayer>() != null) continue;
                nearest = hit.distance;
                ground = hit.point;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        void StartDrop(Vector3 target)
        {
            Vector3 position = transform.position;
            if (position.y - target.y <= DropMinHeight)
            {
                StopDrop();
                transform.position = target;
                return;
            }
            transform.position = new Vector3(target.x, position.y, target.z);
            dropTarget = target;
            dropSpeed = 0f;
            dropping = true;
        }

        void StopDrop() => dropping = false;

        /// <summary>원격 화면: 확정 지점이 현재 위치 바로 아래면 순간이동 대신 떨어뜨린다.</summary>
        bool TryStartRemoteDrop(Vector3 target)
        {
            Vector3 position = transform.position;
            Vector2 horizontal = new Vector2(target.x - position.x, target.z - position.z);
            if (target.y >= position.y - DropMinHeight || horizontal.sqrMagnitude > DropHorizontalTolerance * DropHorizontalTolerance)
                return false;
            StartDrop(target);
            return true;
        }

        void TickDrop()
        {
            if (!dropping) return;
            dropSpeed += Physics.gravity.magnitude * Time.deltaTime;
            Vector3 position = transform.position;
            position.y = Mathf.Max(dropTarget.y, position.y - dropSpeed * Time.deltaTime);
            transform.position = position;
            if (position.y <= dropTarget.y) dropping = false;
            // 떨어지는 이동을 반동 sweep으로 판정하지 않는다.
            ResetPreviousPose();
        }
    }
}
