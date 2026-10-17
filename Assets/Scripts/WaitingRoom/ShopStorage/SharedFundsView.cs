using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 화면 오른쪽 위: 공동 자금 잔액만 항상 보인다(대기실 2차 기획서 4.3, 게임 흐름 5-2).
    /// 사용 내역(장부)은 즉시 알리지 않고 수화기 창의 [장부 보기]에서 나중에 열어 본다.
    /// 잔액은 공용 공동 자금(<see cref="PerformanceRewards"/>)이 모두에게 같게 보낸 값이다.
    /// </summary>
    public sealed class SharedFundsView : MonoBehaviour
    {
        [SerializeField] bool show = true;

        void OnGUI()
        {
            if (!show) return;
            var rewards = PerformanceRewards.Instance;
            const float width = 200;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10, 10, width, 34), GUI.skin.box);
            GUILayout.Label($"공동 자금 {(rewards != null ? rewards.Balance : 0)}");
            GUILayout.EndArea();
        }
    }
}
