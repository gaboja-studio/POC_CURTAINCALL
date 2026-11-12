using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 몸에서 떨어져 날아간 팔다리 조각 표시. 발밑 지지면 검사가 조각을 바닥으로 오인하지 않게 구분한다(2026-11-05).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DismemberedLimb : MonoBehaviour
    {
        static readonly List<DismemberedLimb> active = new();
        Collider[] shapes;

        void OnEnable()
        {
            shapes = GetComponentsInChildren<Collider>(true);
            active.Add(this);
        }

        void OnDisable() => active.Remove(this);

        internal static void IgnoreFor(Collider body)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) return;
            foreach (var limb in active)
                foreach (var shape in limb.shapes)
                    if (shape != null && shape.enabled && shape.gameObject.activeInHierarchy)
                        Physics.IgnoreCollision(body, shape);
        }

        /// <summary>이 충돌체가 떨어진 팔다리 조각에 속하는지.</summary>
        public static bool Contains(Collider collider) =>
            collider != null && collider.GetComponentInParent<DismemberedLimb>() != null;
    }
}
