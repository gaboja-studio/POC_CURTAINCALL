using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 균형바 안의 구간 색(초록·노랑·빨강)을 원본 파스텔보다 짙게 칠한다. 테두리는 원래 흰색 그대로 둔다.
    /// 외부 HUD(P1 Balance) 코드·프리팹은 바꾸지 않고 실행 중에만 색을 바꾼다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BalanceZoneColors : MonoBehaviour
    {
        const string ZonesName = "10_Zones";

        [SerializeField] Color safeFill = new(0.08f, 0.6f, 0.34f, 1f);
        [SerializeField] Color warningFill = new(0.95f, 0.62f, 0f, 1f);
        [SerializeField] Color dangerFill = new(0.8f, 0.1f, 0.14f, 1f);

        void Awake()
        {
            var zones = transform.Find(ZonesName);
            if (zones == null) return;
            Paint(zones, "Safe", safeFill);
            Paint(zones, "LeftWarning", warningFill);
            Paint(zones, "RightWarning", warningFill);
            Paint(zones, "LeftDanger", dangerFill);
            Paint(zones, "RightDanger", dangerFill);
        }

        static void Paint(Transform zones, string name, Color color)
        {
            var zone = zones.Find(name);
            if (zone != null && zone.TryGetComponent(out Graphic graphic)) graphic.color = color;
        }
    }
}
