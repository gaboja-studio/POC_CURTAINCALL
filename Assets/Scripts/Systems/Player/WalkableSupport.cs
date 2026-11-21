using System;
using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Player
{
    /// <summary>점프/봉/대포 비행 모두 동일한 실제 지지면에서 일반 이동으로 돌아온다.</summary>
    public static class WalkableSupport
    {
        static readonly RaycastHit[] hits = new RaycastHit[32];
        public static bool Contains(PlayerMover mover, Vector3 feet) => TryGetSupport(mover, feet, out _);

        /// <summary>안전 착지면인지. 외줄 진행 중이면 실제 외줄 조작 전환과 같은 접촉을, 아니면 일반 지지면을 본다.</summary>
        public static bool IsSafeLanding(PlayerMover mover, Vector3 feet)
        {
            var course = CurtainCall.Tightrope.TightropeCourse.Current;
            // 좁은 줄의 가장자리에 몸통이 지지되어도 발 중심은 박스 밖일 수 있다.
            // 좌표상 줄 근처라는 이유가 아니라 실제 외줄 조작 전환과 같은 접촉으로 승인한다.
            if (course != null && course.IsTraversalActive && course.TryGetSupportingRopeAt(mover, feet, out _)) return true;
            return Contains(mover, feet);
        }

        public static bool TryGetSupport(PlayerMover mover, Vector3 feet, out RaycastHit support)
            => TryGetSupport(mover, feet, default, out support);

        internal static bool TryGetSupport(PlayerMover mover, Vector3 feet, Scene requiredScene, out RaycastHit support,
            Predicate<Collider> preferIfCoplanar = null)
        {
            support = default;
            if (!float.IsFinite(feet.x) || !float.IsFinite(feet.y) || !float.IsFinite(feet.z)) return false;
            bool found = false;
            float nearest = float.PositiveInfinity;
            var body = mover != null ? mover.GetComponent<CharacterController>() : null;
            var scale = body != null ? body.transform.lossyScale : Vector3.one;
            float radius = body != null
                ? Mathf.Max(.02f, body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z))) : .28f;
            Vector3 origin = feet + Vector3.up * (radius + .12f);
            const float distance = .3f;
            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down,
                hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var candidates = count == hits.Length
                ? Physics.SphereCastAll(origin, radius, Vector3.down, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                : hits;
            if (candidates != hits) count = candidates.Length;
            float slope = body != null ? body.slopeLimit : 45f;
            float minNormal = Mathf.Cos(slope * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                var hit = candidates[i];
                bool rope = hit.collider != null && CurtainCall.Tightrope.TightropeCourse.IsRopeSurface(hit.collider);
                if (hit.distance <= .0001f || hit.normal.y < (rope ? .05f : minNormal)
                    || !IsSupportCollider(hit.collider, body)
                    || requiredScene.IsValid() && hit.collider.gameObject.scene != requiredScene) continue;
                // 구형 캐스트의 모서리/초기 겹침 법선은 면의 실제 경사와 다를 수 있다.
                if (!hit.collider.Raycast(new Ray(hit.point + Vector3.up * .08f, Vector3.down), out var face, .18f)
                    || face.normal.y < minNormal) continue;
                // Prefer the mapped finish surface only when the actual support heights tie.
                // An ordinary floor even slightly above it still owns the contact.
                if (found)
                {
                    bool tied = Mathf.Abs(hit.distance - nearest) <= .001f;
                    if (tied ? preferIfCoplanar == null || !preferIfCoplanar(face.collider) || preferIfCoplanar(support.collider)
                        : hit.distance >= nearest) continue;
                }
                nearest = hit.distance;
                support = face;
                found = true;
            }
            return found;
        }

        internal static bool IsSupportCollider(Collider collider, CharacterController body)
            => CanCollideWithBody(collider, body) && !DismemberedLimb.Contains(collider)
                && collider.GetComponentInParent<NetworkPlayer>() == null;

        internal static bool CanCollideWithBody(Collider collider, CharacterController body)
            => collider != null && (body == null || collider != body
                && !collider.transform.IsChildOf(body.transform)
                && !Physics.GetIgnoreLayerCollision(body.gameObject.layer, collider.gameObject.layer)
                && (!body.enabled || !Physics.GetIgnoreCollision(body, collider)));
    }
}
