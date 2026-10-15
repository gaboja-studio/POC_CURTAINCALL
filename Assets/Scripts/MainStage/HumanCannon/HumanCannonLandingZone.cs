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
        const float HeightTolerance = 0.12f; // CharacterController skin 접촉 오차만 허용한다.

        void OnEnable() { surface = GetComponent<BoxCollider>(); zones.Add(this); }
        void OnDisable() => zones.Remove(this);

        public bool SupportsLanding(Vector3 feet)
        {
            // EditMode 검사에서도 Play의 OnEnable 호출에 의존하지 않고 실제 Collider를 읽는다.
            if (surface == null) surface = GetComponent<BoxCollider>();
            if (!isActiveAndEnabled || surface == null || !surface.enabled || surface.isTrigger
                || Vector3.Dot(transform.up, Vector3.up) < 0.999f || transform.lossyScale.y <= 0f) return false;
            return CannonLandingSurface.Contains(transform.InverseTransformPoint(feet), surface.center,
                surface.size, HeightTolerance / transform.lossyScale.y);
        }

        internal static bool ContainsLanding(Vector3 feet)
        {
            foreach (var zone in zones)
                if (zone != null && zone.SupportsLanding(feet)) return true;
            return false;
        }
    }
}
