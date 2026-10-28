using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ③ 수화기 창 옆 "전문가 서비스" 칸(게임 흐름 5-3-1: 전화부스 팝업 구매). 3단계 수화기 창을 고치지 않으려고
    /// 같은 오브젝트의 입력 막기(<see cref="UIInputBlocker"/>)가 켜져 있을 때 = 수화기 창이 열렸을 때 오른쪽에 붙여 그린다.
    /// 서비스는 신체 복구 하나이며 쉬는 시간·밤 정비 언제든 쓴다(M2). 테스트용 IMGUI 창이다.
    /// </summary>
    public sealed class ExpertServicePhonePanel : MonoBehaviour
    {
        // 3단계 수화기 창 크기(ShopPhone.OnGUI)와 맞춘다.
        const float ShopWidth = 380, ShopHeight = 330;
        const float Width = 300, Height = 270;

        UIInputBlocker phoneBlocker;

        void OnGUI()
        {
            if (phoneBlocker == null) phoneBlocker = GetComponent<UIInputBlocker>();
            var service = ExpertService.Instance;
            if (service == null || phoneBlocker == null || !phoneBlocker.IsBlocking) return;

            GUI.depth = -101; // 수화기 창(-100)보다 앞에 그린다
            var area = Placement();
            var tint = GUI.color;
            GUI.color = new Color(.12f, .12f, .14f, 1f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = tint;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"신체 복구 서비스 — {(ExpertService.IsServiceTime ? "이용 가능" : "공연 중 이용 불가")}");

            DrawMedical(service);

            if (service.Phase != ExpertRoomPhase.Idle)
                GUILayout.Label($"⑧ 이용: {WaitingRoomShop.PlayerLabel(service.Buyer)} ({PhaseLabel(service.Phase)})");
            if (!string.IsNullOrEmpty(service.LastMessage)) GUILayout.Label(service.LastMessage);
            GUILayout.EndArea();
        }

        // 수화기 창 오른쪽에 붙인다. 화면이 좁아 겹치면 수화기 창 아래(그래도 모자라면 위)로 옮겨 가리지 않게 한다.
        static Rect Placement()
        {
            float shopX = (Screen.width - ShopWidth) / 2f, shopY = (Screen.height - ShopHeight) / 2f;
            float right = shopX + ShopWidth + 8f;
            if (right + Width <= Screen.width - 4f) return new Rect(right, shopY, Width, Height);
            float x = Mathf.Clamp(shopX + (ShopWidth - Width) / 2f, 4f, Mathf.Max(4f, Screen.width - Width - 4f));
            float below = shopY + ShopHeight + 8f;
            if (below + Height <= Screen.height - 4f) return new Rect(x, below, Width, Height);
            return new Rect(x, Mathf.Max(4f, shopY - Height - 8f), Width, Height);
        }

        // 신체 복구: 1~3개를 골라 산다. 비용 = 지금 1개당 가격 × 개수, 팀 이용 1회마다 다음 단가가 오른다.
        static void DrawMedical(ExpertService service)
        {
            int max = ExpertService.LocalMaxParts;
            GUILayout.Label($"신체 복구 — 1개당 {service.MedicalPrice} (팀 이용 {service.TeamUses}회)");
            GUILayout.BeginHorizontal();
            for (int parts = 1; parts <= ExpertService.MaxPartsPerUse; parts++)
            {
                var refusal = service.LocalMedicalRefusal(parts);
                long cost = service.MedicalCost(parts);
                bool canBuy = refusal == ExpertRefusal.None && service.Balance >= cost;
                GUI.enabled = canBuy;
                if (GUILayout.Button($"{parts}개\n{cost}", GUILayout.Height(40))) service.RequestMedical(parts);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            var reason = service.LocalMedicalRefusal();
            if (reason != ExpertRefusal.None) GUILayout.Label(ExpertServiceBook.Explain(reason));
            else if (service.Balance < service.MedicalCost(1)) GUILayout.Label("공동 자금 부족");
            else GUILayout.Label($"잃은 팔다리 {ExpertService.LocalLostLimbs}개 · 이번에 최대 {max}개");
        }
        public static string PhaseLabel(ExpertRoomPhase phase) => phase switch
        {
            ExpertRoomPhase.Booked => "입장 대기",
            ExpertRoomPhase.Working => "복구 중",
            ExpertRoomPhase.Exiting => "퇴실 중",
            _ => "비어 있음",
        };
    }
}
