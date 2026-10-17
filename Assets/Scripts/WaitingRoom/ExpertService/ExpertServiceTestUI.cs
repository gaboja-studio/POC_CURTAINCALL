using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 5단계 테스트 씬 전용 작은 패널(화면 왼쪽 아래). 전문가 서비스를 빨리 확인하려는 것이며 정식 기능이 아니다.
    /// - 오늘 일차·밤 여부·오늘 담당(의료/시설), ⑧ 방 상태(이용자·단계)를 보인다.
    /// - 참가자마다 잃은 팔다리와 상태를 보이고, 호스트는 [절단 1]로 결손을 만든다(복구 확인용).
    /// 밤까지 건너뛰기·준비 완료·테스트 자금은 같은 씬의 4단계 패널(NightScheduleTestUI)로 한다.
    /// </summary>
    public sealed class ExpertServiceTestUI : MonoBehaviour
    {
        bool folded;

        void OnGUI()
        {
            const float width = 300;
            float height = folded ? 34 : 250;
            GUILayout.BeginArea(new Rect(10, Screen.height - height - 10, width, height), GUI.skin.box);
            if (GUILayout.Button(folded ? "전문가 서비스 테스트 패널 펴기" : "전문가 서비스 테스트 패널 접기")) folded = !folded;
            if (folded) { GUILayout.EndArea(); return; }

            var service = ExpertService.Instance;
            if (service == null) { GUILayout.Label("ExpertService 없음 — 씬 구성 메뉴를 실행하세요."); GUILayout.EndArea(); return; }

            int day = ExpertService.Day;
            var staff = ExpertServiceBook.StaffFor(day);
            GUILayout.Label($"{day}일차 · {(ExpertService.IsNight ? "밤 정비" : "밤 아님")} · 오늘 담당: {StaffLabel(staff)}");
            GUILayout.Label($"⑧ {ExpertServicePhonePanel.PhaseLabel(service.Phase)}"
                + (service.Phase != ExpertRoomPhase.Idle ? $" — {WaitingRoomShop.PlayerLabel(service.Buyer)}" : "")
                + $" · 공동 자금 {service.Balance} · 복구 {service.MedicalPrice}/개");

            var players = NetworkPlayer.All;
            if (players.Count == 0) GUILayout.Label("접속한 캐릭터가 없습니다.");
            foreach (var player in players)
            {
                if (!player.IsSpawned) continue;
                GUILayout.BeginHorizontal();
                string me = player.IsLocal ? " (나)" : "";
                GUILayout.Label($"{WaitingRoomShop.PlayerLabel(player.OwnerClientId)}{me}: 잃은 팔다리 {PlayerCondition.CountLimbs(player.LostParts)} {Parts(player.LostParts)} · {player.State}");
                GUI.enabled = service.HasAuthority && player.State == PlayerState.Normal && PlayerCondition.CountLimbs(player.LostParts) < 3;
                if (GUILayout.Button("절단 1", GUILayout.Width(60))) player.ServerCut();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (!service.HasAuthority) GUILayout.Label("절단 버튼은 호스트만 씁니다.");
            if (!string.IsNullOrEmpty(service.LastMessage)) GUILayout.Label(service.LastMessage);
            GUILayout.EndArea();
        }

        static string StaffLabel(ExpertKind kind) => kind switch
        {
            ExpertKind.Medical => "의료",
            ExpertKind.Facility => "시설(의료 부재중)",
            _ => "없음",
        };

        static string Parts(BodyPart lost)
        {
            if (lost == BodyPart.None) return "";
            string text = "";
            if ((lost & BodyPart.LeftLeg) != 0) text += "왼다리 ";
            if ((lost & BodyPart.RightLeg) != 0) text += "오른다리 ";
            if ((lost & BodyPart.LeftArm) != 0) text += "왼팔 ";
            if ((lost & BodyPart.RightArm) != 0) text += "오른팔 ";
            return "(" + text.TrimEnd() + ")";
        }
    }
}
