using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 공연 배치 기물 세트의 맵 기물 하나(인간 대포·트램펄린). 가까이서 외곽선이 보이고 F로 세트를 획득한다(<see cref="PerformancePropDiscovery"/>).
    /// 도구 없이 쓰며, 해금되면 기물이 사라지므로 더 닿지 않는다.
    /// </summary>
    public sealed class PerformancePropPickup : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [SerializeField] PerformancePropDiscovery owner;
        [Tooltip("F가 닿는 거리(m). 큰 기물이라 일반 대상보다 넓다.")]
        [SerializeField, Min(.5f)] float reach = 3f;

        bool inReach;

        public void Bind(PerformancePropDiscovery set) => owner = set;

        public Transform VisualRoot => transform;
        // 기물은 움직이지 않으므로 안내 위치는 한 번만 잰다(매 프레임 렌더러를 찾지 않는다).
        Vector3? point;
        public Vector3 PresentationPoint => point ??= Measure();
        Vector3 Measure()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return transform.position + Vector3.up * .6f;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return new Vector3(bounds.center.x, Mathf.Min(bounds.max.y, transform.position.y + 1.2f), bounds.center.z);
        }
        public bool IsAvailable => owner != null && owner.CanAcquire;
        public string ActionLabel => owner == null ? "" : WaitingRoomText.Format("prompt.performance-prop-pickup", "{trick} 기물 획득 (공연 해금)", ("trick", owner.TrickName));

        void OnEnable() => ShopReach.Add(this);
        void OnDisable() { ShopReach.Remove(this); inReach = false; }

        public float Reach(Vector3 feet) => IsAvailable ? ExplorationReach.From(transform, PresentationPoint, feet, reach) : -1f;

        public void Interact() { if (IsAvailable) owner.Acquire(); }

        void Update()
        {
            ShopReach.Pump();
            inReach = ShopReach.Focused == (IShopTarget)this;
        }

        void OnGUI()
        {
            if (inReach && IsAvailable && !ShopReach.HasExternalPresentation && !InputGate.IsBlocked)
                GUI.Box(new Rect((Screen.width - 360) / 2f, Screen.height * .62f, 360, 28), "[F] " + ActionLabel);
        }
    }
}
