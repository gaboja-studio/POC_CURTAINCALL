using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>공통 안전 착지대. 실제 수평 윗면의 낙하 피해 면제는 BaseGameSettings에서 조정한다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class HumanCannonLandingZone : MonoBehaviour
    {
        static readonly HashSet<HumanCannonLandingZone> zones = new();
        BoxCollider surface;
        [Tooltip("리프트 구멍을 피해 나눈 안전 바닥. 비우면 이 오브젝트의 BoxCollider만 사용한다.")]
        [SerializeField] BoxCollider[] additionalSurfaces = System.Array.Empty<BoxCollider>();
        const float HeightTolerance = 0.12f; // CharacterController skin 접촉 오차만 허용한다.

        void OnEnable() { surface = GetComponent<BoxCollider>(); zones.Add(this); }
        void OnDisable() => zones.Remove(this);

        public bool SupportsLanding(Vector3 feet)
        {
            // EditMode 검사에서도 Play의 OnEnable 호출에 의존하지 않고 실제 Collider를 읽는다.
            if (surface == null) surface = GetComponent<BoxCollider>();
            if (!isActiveAndEnabled) return false;
            if (Supports(surface, feet)) return true;
            foreach (var tile in additionalSurfaces) if (Supports(tile, feet)) return true;
            return false;
        }

        static bool Supports(BoxCollider tile, Vector3 feet)
        {
            if (tile == null || !tile.enabled || !tile.gameObject.activeInHierarchy || tile.isTrigger
                || Vector3.Dot(tile.transform.up, Vector3.up) < 0.999f || tile.transform.lossyScale.y <= 0f) return false;
            return CannonLandingSurface.Contains(tile.transform.InverseTransformPoint(feet), tile.center,
                tile.size, HeightTolerance / tile.transform.lossyScale.y);
        }

        internal static bool ContainsLanding(Vector3 feet)
        {
            foreach (var zone in zones)
                if (zone != null && zone.SupportsLanding(feet)) return true;
            return false;
        }

        /// <summary>몸 중심이 플랫폼 밖에 있어도 실제 발밑 접촉면이 안전대면 같은 착지다.</summary>
        internal static bool ContainsLanding(PlayerMover mover, Vector3 feet)
        {
            if (!WalkableSupport.TryGetSupport(mover, feet, out var support)) return false;
            foreach (var zone in zones)
            {
                if (zone == null || !zone.isActiveAndEnabled) continue;
                if (zone.surface == null) zone.surface = zone.GetComponent<BoxCollider>();
                if (support.collider == zone.surface && Supports(zone.surface, support.point)) return true;
                foreach (var tile in zone.additionalSurfaces)
                    if (support.collider == tile && Supports(tile, support.point)) return true;
            }
            return false;
        }

        /// <summary>대포 아래 착지판. 한 플랫폼에 대포가 여러 개 있어도 같은 Zone을 돌려준다.</summary>
        public static HumanCannonLandingZone PlatformBelow(Vector3 point)
        {
            HumanCannonLandingZone best = null;
            float nearest = float.PositiveInfinity;
            foreach (var zone in zones)
            {
                if (zone == null || !zone.isActiveAndEnabled) continue;
                Check(zone, zone.surface);
                foreach (var tile in zone.additionalSurfaces) Check(zone, tile);
            }
            return best;

            void Check(HumanCannonLandingZone zone, BoxCollider tile)
            {
                if (tile == null) return;
                var feet = new Vector3(point.x, tile.bounds.max.y, point.z);
                float gap = point.y - feet.y;
                if (gap < -HeightTolerance || gap >= nearest || !Supports(tile, feet)) return;
                best = zone; nearest = gap;
            }
        }
    }
}
