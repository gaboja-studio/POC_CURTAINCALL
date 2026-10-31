using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 디버그 전용 돈 기물(대기실 로비). 가까이서 F를 누를 때마다 공동 자금에 <see cref="amount"/>를 넣는다. 기물은 사라지지 않는다.
    /// 판정 화면(호스트)이 요청 1건당 한 번만 넣고, 잔액·장부("디버그 지급")는 공용 보상 시스템이 모두에게 보낸다.
    /// 에디터·개발 빌드에서만 동작하고, 정식 빌드에서는 스스로 사라진다.
    /// </summary>
    public sealed class DebugMoneyProp : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [SerializeField, Min(1)] long amount = 1000;

        [Tooltip("기물에 닿는 수평 거리(m).")]
        [SerializeField, Min(.5f)] float reach = 1.6f;

        public long Amount => amount;
        public Transform VisualRoot => transform;
        public Vector3 PresentationPoint => transform.position;
        public string ActionLabel => $"돈 줍기 (+{amount}, 디버그)";
        public bool IsAvailable => WaitingRoomShop.Instance != null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnEnable() => ShopReach.Add(this);
        void OnDisable() => ShopReach.Remove(this);
        void Update() => ShopReach.Pump();

        public float Reach(Vector3 feet)
        {
            Vector3 v = feet - transform.position;
            float distance = new Vector2(v.x, v.z).magnitude;
            return distance <= reach ? distance : -1f;
        }

        public void Interact()
        {
            if (WaitingRoomShop.Instance != null) WaitingRoomShop.Instance.RequestDebugGrant(amount);
        }

        void OnGUI()
        {
            if (ShopReach.HasExternalPresentation || !ReferenceEquals(ShopReach.Focused, this)) return;
            GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height * .62f, 240, 24), $"[F] {ActionLabel}");
        }
#else
        void Awake() => Destroy(gameObject);
        public float Reach(Vector3 feet) => -1f;
        public void Interact() { }
#endif
    }
}
