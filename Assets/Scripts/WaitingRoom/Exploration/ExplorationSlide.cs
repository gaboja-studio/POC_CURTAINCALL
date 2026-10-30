using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>지정 대상이 열리면 옆으로 밀려 열리는 판(숨겨진 문). 상태는 ExplorationState를 따른다.</summary>
    public sealed class ExplorationSlide : MonoBehaviour
    {
        [SerializeField] string openedBy;
        [SerializeField] Vector3 openOffset;
        [SerializeField, Min(.1f)] float seconds = 1.2f;
        Vector3 closed;
        float t;

        public bool FullyOpen => t >= 1f;
        void Awake() => closed = transform.localPosition;
        void Update()
        {
            bool open = ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(openedBy);
            t = Mathf.MoveTowards(t, open ? 1f : 0f, Time.deltaTime / seconds);
            transform.localPosition = closed + openOffset * Mathf.SmoothStep(0f, 1f, t);
        }
    }
}
