using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 이동은 막지만 상호작용 시야는 막지 않는 충돌체 표시(2026-11-14). 선반 전체를 덮는 통짜 충돌체(RackCollider)처럼
    /// 그 안에 놓인 물건을 손으로 집을 수 있어야 하는 곳에 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPassThrough : MonoBehaviour { }
}
