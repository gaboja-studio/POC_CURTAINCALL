using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>로컬 F 선택과 호스트 판정이 함께 사용하는 실제 거리·가림 검사.</summary>
    public static class ExplorationReach
    {
        static readonly RaycastHit[] hits = new RaycastHit[64];
        /// <param name="insideFurniture">true면 대상이 들어 있는 가구(충돌체 범위 안에 대상이 있는 것)는 가림으로 보지 않는다. 선반 밑·안에 놓인 바닥 기물용.</param>
        public static float From(Transform root, Vector3 point, Vector3 feet, float range, bool spatial = false, bool insideFurniture = false)
        {
            float height = GameSettings.Base.Character.CapsuleHeight;
            if (point.y < feet.y - .6f || point.y > feet.y + height + .6f) return -1f;
            float distance = spatial ? Vector3.Distance(feet, point) : Vector2.Distance(new Vector2(feet.x, feet.z), new Vector2(point.x, point.z));
            if (distance > range) return -1f;
            // Lower hand-height also permits a key beneath furniture; floors and full-height partitions still block it.
            if (Clear(root, feet + Vector3.up * Mathf.Min(.35f, height * .3f), point, insideFurniture)
                || Clear(root, feet + Vector3.up * (height * .8f), point, insideFurniture)) return distance;
            return -1f;
        }
        static bool Clear(Transform root, Vector3 from, Vector3 point, bool insideFurniture)
        {
            var delta = point - from;
            if (delta.magnitude < .08f) return true;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, delta.magnitude - .05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].collider;
                if (hit.transform.IsChildOf(root) || hit.GetComponentInParent<PlayerMover>() != null) continue;
                if (insideFurniture && hit.bounds.Contains(point)) continue;
                return false;
            }
            return true;
        }
    }
}
