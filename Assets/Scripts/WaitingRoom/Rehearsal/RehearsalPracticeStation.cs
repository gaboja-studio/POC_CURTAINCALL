using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 연습실 링의 외줄(옛 연습 줄) 아래에서 F → "리허설 다시 하기" 투표(사용자 2026-11-04).
    /// 줄 아래 바닥 어디서든 닿는다. 인간 대포는 리허설이 생기면 같은 방식으로 붙인다.
    /// 리허설 진행기가 연습실을 찾을 때 줄 오브젝트에 붙인다.
    /// </summary>
    public sealed class RehearsalPracticeStation : MonoBehaviour, IShopTarget, IShopPresentation
    {
        const float ReachDistance = 2.5f;
        Renderer rope;
        Vector3 point;

        public Transform VisualRoot => transform;
        public Vector3 PresentationPoint => point;
        public string ActionLabel => WaitingRoomText.Get("rehearsal.practice-station.action", "리허설 다시 하기");
        public bool IsAvailable => isActiveAndEnabled && WaitingRoomRehearsal.CanProposePractice;

        void Awake() => rope = GetComponentInChildren<Renderer>(true);
        void OnEnable() => ShopReach.Add(this);
        void OnDisable() => ShopReach.Remove(this);
        void Update() => ShopReach.Pump();

        public float Reach(Vector3 feet)
        {
            if (!IsAvailable || rope == null) return -1f;
            var b = rope.bounds;
            // 줄 아래 바닥(가로 범위)에서 가장 가까운 점까지의 수평 거리.
            point = new Vector3(Mathf.Clamp(feet.x, b.min.x, b.max.x), feet.y + 1f, Mathf.Clamp(feet.z, b.min.z, b.max.z));
            float distance = new Vector2(feet.x - point.x, feet.z - point.z).magnitude;
            return distance <= ReachDistance ? distance : -1f;
        }

        public void Interact()
        {
            if (IsAvailable) WaitingRoomRehearsal.ProposePractice();
        }
    }
}
