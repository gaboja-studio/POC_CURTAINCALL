using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>대포 발판/쉼터/쿠션 등 정상 착지를 허용할 수평 BoxCollider에 붙인다.</summary>
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
    }
}
