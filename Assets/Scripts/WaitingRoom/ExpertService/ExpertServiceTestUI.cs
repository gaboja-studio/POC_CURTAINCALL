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
        /// <summary>화면 아래쪽에서 이 패널과 3단계 소지 안내(최대 2줄)가 차지하는 높이. 커서 잠금 예외 영역도 이 값을 쓴다.</summary>
        public const float BottomReserve = 380f;

        /// <summary>3단계 소지 안내(PropCarry) 상자 위로 띄우는 높이.</summary>
        const float CarryGap = 72f;

        bool folded;

        void OnGUI()
        {
            const float width = 300;
            float height = folded ? 34 : 300;
            GUILayout.BeginArea(new Rect(10, Screen.height - height - CarryGap, width, height), GUI.skin.box);
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
            GUILayout.Label(service.Obstacle != ObstacleAdjust.None
                ? $"시설 조정: {service.ObstacleDay}일차 {ExpertServiceBook.AdjustLabel(service.Obstacle)}"
                : "시설 조정: 없음");

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
            GUILayout.Label(NextStep(service));
            if (!string.IsNullOrEmpty(service.LastMessage)) GUILayout.Label(service.LastMessage);
            GUILayout.Label(Cursor.lockState == CursorLockMode.Locked ? "ESC: 커서 풀기(패널 클릭)" : "화면 빈 곳 클릭: 마우스 시점 다시 잠금");
            GUILayout.EndArea();
        }

        /// <summary>지금 무엇을 하면 의료 서비스를 쓸 수 있는지 한 줄 안내(테스트 순서).</summary>
        static string NextStep(ExpertService service)
        {
            if (service.Phase == ExpertRoomPhase.Booked)
                return service.LocalIsBuyer ? "다음: ⑧ 붉은 커튼 앞으로 가서 F" : "⑧: 구매자 입장 대기 중";
            if (service.Phase != ExpertRoomPhase.Idle) return "⑧ 이용 중";
            return service.LocalMedicalRefusal() switch
            {
                ExpertRefusal.NotNight => "다음: 오른쪽 패널 [밤 정비까지 건너뛰기]",
                ExpertRefusal.WholeBody => "다음: [절단 1]로 팔다리를 잃은 뒤 ③ 수화기",
                ExpertRefusal.AlreadyBooked => "다음: ⑧ 붉은 커튼 앞으로 가서 F",
                ExpertRefusal.Absent => service.FacilityRefusal() == ExpertRefusal.None
                    ? "의료 부재중 — ③ 수화기에서 시설 담당자(장애물 강화/약화) 이용 가능"
                    : "오늘 밤은 의료 담당자 부재중 — 1·3일차 밤에 이용",
                ExpertRefusal.NotAlive => "살아 있을 때만 이용할 수 있습니다",
                ExpertRefusal.None => "다음: ③ 수화기(F) → 오른쪽 '전문가 서비스'에서 구매",
                var other => ExpertServiceBook.Explain(other),
            };
        }

        static string StaffLabel(ExpertKind kind) => kind switch
        {
            ExpertKind.Medical => "의료",
            ExpertKind.Facility => "시설 담당자(의료 부재중)",
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
