using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 디버그 전용 탈출로 열쇠(대기실 로비, 디버그 돈 기물 옆, 2026-10-31 사용자 요청). F를 누르면 탈출로 열쇠 1 또는 2를 얻은 것으로
    /// (<see cref="StructureAKeyRoute.EscapeKey1"/>/<see cref="StructureAKeyRoute.EscapeKey2"/> 대상 열림) 처리해, 탈출로 문까지 바로 시험할 수 있다.
    /// 판정 화면(호스트)이 처리해 모두에게 같은 상태가 간다. 에디터·개발 빌드에서만 동작하고, 정식 빌드에서는 스스로 사라진다.
    /// </summary>
    public sealed class DebugEscapeKeyProp : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [Tooltip("1 또는 2: 탈출로 열쇠 번호.")]
        [SerializeField, Range(1, 2)] int keyNumber = 1;

        [Tooltip("기물에 닿는 수평 거리(m).")]
        [SerializeField, Min(.5f)] float reach = 1.2f;

        public int KeyNumber { get => keyNumber; set => keyNumber = Mathf.Clamp(value, 1, 2); }
        string TargetId => keyNumber == 1 ? StructureAKeyRoute.EscapeKey1 : StructureAKeyRoute.EscapeKey2;
        bool Opened => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(TargetId);
        public Transform VisualRoot => transform;
        public Vector3 PresentationPoint => transform.position;
        public string ActionLabel => Opened ? $"탈출로 열쇠 {keyNumber} 얻음 (디버그)" : $"탈출로 열쇠 {keyNumber} 얻기 (디버그)";
        public bool IsAvailable => ExplorationState.Instance != null && !Opened;

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
            if (IsAvailable) ExplorationState.Instance.RequestDebugOpen(TargetId);
        }
#else
        void Awake() => Destroy(gameObject);
        public float Reach(Vector3 feet) => -1f;
        public void Interact() { }
#endif
    }
}
