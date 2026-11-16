using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>지정 대상이 열리면 옆으로 밀려 열리는 판(숨겨진 문). 상태는 ExplorationState를 따른다.</summary>
    public sealed class ExplorationSlide : MonoBehaviour
    {
        [SerializeField] string openedBy;
        [SerializeField] Vector3 openOffset;
        [SerializeField, Min(.1f)] float seconds = 1.2f;
        [Tooltip("지정하면 이 문(대상)의 열림 상태를 그대로 따른다. 통과 판정(막음 충돌)과 보이는 문짝이 어긋나지 않게 한다(2026-11-14 사용자: 닫혀 보이는데 지나갈 수 있던 탈출로 문).")]
        [SerializeField] ExplorationGate followGate;
        [Tooltip("이 대상이 열리는 순간(회로 고장) 잠깐 열렸다 닫혔다 오작동한 뒤 완전히 열린 채로 굳는다(2026-11-14 사용자). 비우면 오작동 없음.")]
        [SerializeField] string glitchBy = "";
        [SerializeField, Min(0f)] float glitchSeconds = 3f;
        Vector3 closed;
        float t, glitchUntil;
        bool glitchSeen, glitchWas;

        public bool FullyOpen => t >= 1f;
        void Awake() => closed = transform.localPosition;
        void Update()
        {
            bool open = followGate != null ? followGate.Opened : ExplorationGate.IsAnyOpen(openedBy); // "|"로 여러 대상을 적으면 하나만 열려도 열린다
            float speed = 1f;
            if (!string.IsNullOrEmpty(glitchBy) && ExplorationState.Instance != null)
            {
                // 들어왔을 때 이미 고장 나 있던 문은 오작동 없이 열린 채로 둔다.
                bool broken = ExplorationGate.IsAnyOpen(glitchBy);
                if (glitchSeen && broken && !glitchWas) glitchUntil = Time.time + glitchSeconds;
                glitchSeen = true; glitchWas = broken;
                float left = glitchUntil - Time.time;
                // 불규칙하게 닫혔다 열렸다 한다(모든 화면이 비슷한 박자). 마지막 0.4초는 열리는 쪽으로 간다.
                if (left > 0f) { open = left < .4f || Mathf.PerlinNoise(left * 2.3f, 7.7f) > .5f; speed = 4f; }
            }
            t = Mathf.MoveTowards(t, open ? 1f : 0f, Time.deltaTime / seconds * speed);
            transform.localPosition = closed + openOffset * Mathf.SmoothStep(0f, 1f, t);
        }
    }
}
