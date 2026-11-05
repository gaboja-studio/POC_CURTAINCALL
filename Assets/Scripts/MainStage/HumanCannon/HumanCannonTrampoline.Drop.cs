using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>운반자가 공중에서 죽으면 트램펄린이 그 자리에서 바로 아래 지면까지 떨어진다(2026-11-05 사용자 요청).
    /// 낙사하면 시작 지점에 가장 가까운 생존자 발밑 지면으로, 대포 안이거나 발밑 지면이 없으면 2m 뒤로 옮긴다.</summary>
    public sealed partial class HumanCannonTrampoline
    {
        const float DropMinHeight = 0.05f;
        // 원격 화면은 운반자 위치 공유 지연만큼 수평으로 어긋날 수 있다. 이 안이면 확정 지점 위에서 떨어뜨린다.
        const float DropHorizontalTolerance = 2f;
        const float DropProbeDistance = 60f;
        const float SurvivorBehindDistance = 2f;
        const int SurvivorBehindSteps = 2;

        readonly RaycastHit[] dropHits = new RaycastHit[16];
        bool dropping;
        Vector3 dropTarget;
        float dropSpeed;

        /// <summary>호스트가 공유하는 위치. 떨어지는 중이면 도착할 지면 위치를 보낸다.</summary>
        Vector3 SharedPosition => dropping ? dropTarget : transform.position;

        /// <summary>낙사 때 옮길 곳: 시작 지점에 가장 가까운 생존자 발밑 지면. 공중 생존자도 그 아래 지면을 쓴다.
        /// 대포 안이거나 발밑에 지면이 없으면 생존자 2m 뒤, 거기도 지면이 없으면 4m 뒤에 둔다.</summary>
        bool TryFindSurvivorGround(out Vector3 ground)
        {
            ground = default;
            NetworkPlayer survivor = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in NetworkPlayer.All)
            {
                if (candidate == null || !candidate.IsSpawned || candidate.State != PlayerState.Normal) continue;
                Vector3 feet = candidate.transform.position;
                if (!float.IsFinite(feet.x) || !float.IsFinite(feet.y) || !float.IsFinite(feet.z)) continue;
                float distance = new Vector2(feet.x - initialPosition.x, feet.z - initialPosition.z).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                survivor = candidate;
            }
            if (survivor == null) return false;
            Vector3 position = survivor.transform.position;
            var cannon = HumanCannonSystem.Current;
            bool seated = cannon != null && cannon.IsSeated(survivor.OwnerClientId);
            if (!seated && TryFindGround(position, out ground)) return true;
            Vector3 forward = Vector3.ProjectOnPlane(survivor.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            // 2m 뒤 아래에 지면이 있으면 그 위에, 없으면 2m 더 뒤(4m)를 본다. 그래도 없으면 4m 뒤 생존자 높이에 둔다.
            Vector3 behind = position;
            for (int step = 0; step < SurvivorBehindSteps; step++)
            {
                behind -= forward.normalized * SurvivorBehindDistance;
                if (TryFindGround(behind, out ground)) return true;
            }
            ground = behind;
            return true;
        }

        /// <summary>주어진 위치 바로 아래 가장 가까운 지면. 플레이어·자기 자신·트리거·벽면은 제외한다.</summary>
        bool TryFindGround(Vector3 from, out Vector3 ground)
        {
            ground = from;
            Vector3 origin = from + Vector3.up * DropMinHeight;
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
