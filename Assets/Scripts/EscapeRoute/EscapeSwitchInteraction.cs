using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Tightrope;
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
            var center = PlayerInteraction.Center(actor);
            // A cut arm must not disable switches. Restore only the lost-leg reach height;
            // the actual capsule, stack pose, and other interactions remain authoritative.
            if (actor.TryGetComponent<PlayerCondition>(out var condition) && condition.LostLegCount > 0)
                center.y = Mathf.Max(center.y, actor.transform.position.y
                    + GameSettings.Base.Character.CapsuleHeight * Mathf.Abs(actor.transform.lossyScale.y) * .5f);
            if (actor.TryGetComponent<NetworkPlayer>(out var player) && PiggybackSystem.Current != null)
            {
                var below = PiggybackSystem.Current.GetBelow(player);
                for (int i = 0; below != null && i < 3; i++, below = PiggybackSystem.Current.GetBelow(below))
                    if (below.TryGetComponent<PlayerCondition>(out var injury) && injury.LostLegCount > 0
                        && below.TryGetComponent<PlayerMover>(out var lowerMover))
                        center.y += Mathf.Max(0, GameSettings.Base.Character.CapsuleHeight
                            * Mathf.Abs(below.transform.lossyScale.y) - lowerMover.BodyHeight);
            }
            return float.IsFinite(radius) && radius > 0f && (center - Center(button)).sqrMagnitude <= radius * radius;
        }
    }
}
