using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>버튼 자체의 중심을 기준으로 하는 공통 구형 판정. 시선/진입 방향/박스 모서리에 의존하지 않는다.</summary>
    public static class EscapeSwitchInteraction
    {
        public const float DefaultRadius = 2.5f;

        // Only the button's own renderer defines its centre; surrounding machinery is not included.
        public static Vector3 Center(Transform button) => button != null
            ? button.TryGetComponent<Renderer>(out var visual) ? visual.bounds.center : button.position
            : Vector3.zero;

        public static bool InRange(Component actor, Transform button, float radius = DefaultRadius)
        {
            if (actor == null || button == null || !button.gameObject.activeInHierarchy) return false;
            float reach = actor.TryGetComponent<PlayerInteraction>(out var interaction) ? interaction.ReachMultiplier : 1f;
            return PlayerInteraction.InRange(actor, Center(button), radius * reach);
        }
    }
}
