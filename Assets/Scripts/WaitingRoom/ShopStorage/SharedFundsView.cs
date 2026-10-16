using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 화면 오른쪽 위: 공동 자금 잔액·최근 장부·최근 수령 기록(공동 테스트 확인용 IMGUI).
    /// 잔액·장부는 공용 공동 자금(<see cref="PerformanceRewards"/>)이 모두에게 같게 보낸 값을, 수령 기록은 <see cref="WaitingRoomShop"/> 값을 그대로 그린다.
    /// </summary>
    public sealed class SharedFundsView : MonoBehaviour
    {
        [SerializeField] bool show = true;
        [SerializeField, Range(1, 10)] int ledgerLines = 5;
        [SerializeField, Range(0, 10)] int receiptLines = 3;

        void OnGUI()
        {
            if (!show) return;
            var rewards = PerformanceRewards.Instance;
            var shop = WaitingRoomShop.Instance;
            var state = rewards != null ? rewards.State : null;

            const float width = 300;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10, 10, width, Screen.height * .5f));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"공동 자금 {(rewards != null ? rewards.Balance : 0)}");
            if (state != null && state.Ledger.Count > 0)
            {
                for (int i = state.Ledger.Count - 1, n = 0; i >= 0 && n < ledgerLines; i--, n++)
                {
                    var entry = state.Ledger[i];
                    GUILayout.Label($"#{entry.Number} {entry.Day}일차 {entry.Amount:+#;-#;0} → {entry.Balance}  {entry.Description}");
                }
            }
            else GUILayout.Label("장부 없음 — 공연 수입이 들어오면 표시됩니다.");

            if (shop != null && receiptLines > 0 && shop.Receipts.Count > 0)
            {
                GUILayout.Label("최근 수령");
                for (int i = shop.Receipts.Count - 1, n = 0; i >= 0 && n < receiptLines; i--, n++)
                {
                    var receipt = shop.Receipts[i];
                    GUILayout.Label($"#{receipt.Delivery.Order} {receipt.Delivery.ItemName}: "
                        + $"{WaitingRoomShop.PlayerLabel(receipt.Delivery.Buyer)} 주문 → {WaitingRoomShop.PlayerLabel(receipt.Claimer)} 수령");
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
