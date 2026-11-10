using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>점프/봉/대포 비행 모두 동일한 실제 지지면에서 일반 이동으로 돌아온다.</summary>
    public static class WalkableSupport
    {
        static readonly RaycastHit[] hits = new RaycastHit[32];
        public static bool Contains(PlayerMover mover, Vector3 feet)
        {
            var body = mover != null ? mover.GetComponent<CharacterController>() : null;
            float radius = body != null ? Mathf.Max(.02f, body.radius - .02f) : .28f;
            int count = Physics.SphereCastNonAlloc(feet + Vector3.up * (radius + .12f), radius, Vector3.down,
                hits, .3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float slope = body != null ? body.slopeLimit : 45f;
            float minNormal = Mathf.Cos(slope * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.normal.y < minNormal || hit.collider.GetComponentInParent<NetworkPlayer>() != null
                    || mover != null && hit.collider.transform.IsChildOf(mover.transform)) continue;
                // 구형 캐스트의 모서리/초기 겹침 법선은 면의 실제 경사와 다를 수 있다.
                if (!hit.collider.Raycast(new Ray(hit.point + Vector3.up * .08f, Vector3.down), out var face, .18f)
                    || face.normal.y < minNormal) continue;
                return true;
            }
            return false;
        }
    }
}
