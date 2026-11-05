using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 몸에서 떨어져 날아간 팔다리 조각 표시. 발밑 지지면 검사가 조각을 바닥으로 오인하지 않게 구분한다(2026-11-05).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DismemberedLimb : MonoBehaviour
    {
        /// <summary>이 충돌체가 떨어진 팔다리 조각에 속하는지.</summary>
        public static bool Contains(Collider collider) =>
            collider != null && collider.GetComponentInParent<DismemberedLimb>() != null;
    }
}
