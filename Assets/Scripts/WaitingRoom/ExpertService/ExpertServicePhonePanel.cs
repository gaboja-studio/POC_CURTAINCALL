using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ③ 수화기 창 옆 "전문가 서비스" 칸(게임 흐름 5-3-1: 전화부스 팝업 구매). 3단계 수화기 창을 고치지 않으려고
    /// 같은 오브젝트의 입력 막기(<see cref="UIInputBlocker"/>)가 켜져 있을 때 = 수화기 창이 열렸을 때 오른쪽에 붙여 그린다.
    /// 오늘 스케줄에 없는 서비스는 비활성 + "담당자 부재중". 테스트용 IMGUI 창이다.
    /// </summary>
    public sealed class ExpertServicePhonePanel : MonoBehaviour
    {
        // 3단계 수화기 창 크기(ShopPhone.OnGUI)와 맞춘다.
        const float ShopWidth = 380, ShopHeight = 330;
        const float Width = 300, Height = 230;

        UIInputBlocker phoneBlocker;

        void OnGUI()
        {
            if (phoneBlocker == null) phoneBlocker = GetComponent<UIInputBlocker>();
            var service = ExpertService.Instance;
            if (service == null || phoneBlocker == null || !phoneBlocker.IsBlocking) return;

            GUI.depth = -100;
            float x = Mathf.Min((Screen.width + ShopWidth) / 2f + 8f, Screen.width - Width);
            var area = new Rect(x, (Screen.height - ShopHeight) / 2f, Width, Height);
            var tint = GUI.color;
            GUI.color = new Color(.12f, .12f, .14f, 1f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = tint;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"전문가 서비스 — {(ExpertService.Day > 0 ? ExpertService.Day + "일차 " : "")}{(ExpertService.IsNight ? "밤" : "밤 아님")} (임시 가격)");

            // 의료: 신체 복구 1개
            var medical = ExpertService.Availability(ExpertKind.Medical);
            if (medical == ExpertRefusal.None) medical = service.LocalMedicalRefusal();
            bool canBuy = medical == ExpertRefusal.None && service.Balance >= service.MedicalPrice;
            GUI.enabled = canBuy;
            string medicalLabel = $"의료 — 신체 복구 1개 — {service.MedicalPrice}";
            if (!canBuy) medicalLabel += $"\n({(medical != ExpertRefusal.None ? ExpertServiceBook.Explain(medical) : "공동 자금 부족")})";
            if (GUILayout.Button(medicalLabel, GUILayout.Height(40))) service.RequestMedical();

            // 시설: 장애물 강화/약화 — 5단계 범위 밖, 담당 표시만
            var facility = ExpertService.Availability(ExpertKind.Facility);
            GUI.enabled = false;
            GUILayout.Button($"시설 담당자 — 장애물 강화/약화\n({ExpertServiceBook.Explain(facility == ExpertRefusal.None ? ExpertRefusal.NotAvailable : facility)})",
                GUILayout.Height(40));
            GUI.enabled = true;

            if (service.Phase != ExpertRoomPhase.Idle)
                GUILayout.Label($"⑧ 이용: {WaitingRoomShop.PlayerLabel(service.Buyer)} ({PhaseLabel(service.Phase)})");
            if (!string.IsNullOrEmpty(service.LastMessage)) GUILayout.Label(service.LastMessage);
            GUILayout.EndArea();
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
