using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 여러 탐색 대상이 모두 열리면 대상 하나를 장부에 연다(대기실 최종본: 관리실 컴퓨터 + 탈출로 배선 → 탈출로 길).
    /// 문·슬라이드는 이 대상 하나만 보면 된다. 기록은 판정 화면에서만 된다(<see cref="ExplorationState.MarkVisited"/>).
    /// </summary>
    public sealed class ExplorationAllOf : MonoBehaviour
    {
        [SerializeField] string[] required;
        [SerializeField] string openTarget = "";
        [Tooltip("켜면 하나만 열려도 연다(관리실 문: 배선 절단·안쪽 빗장·키 카드 중 하나).")]
        [SerializeField] bool any;

        void Update()
        {
            var state = ExplorationState.Instance;
            if (state == null || string.IsNullOrEmpty(openTarget) || required == null || required.Length == 0 || state.IsOpen(openTarget)) return;
            if (any) { bool one = false; foreach (var id in required) one |= state.IsOpen(id); if (!one) return; }
            else foreach (var id in required) if (!state.IsOpen(id)) return;
            state.MarkVisited(openTarget);
        }
    }
}
