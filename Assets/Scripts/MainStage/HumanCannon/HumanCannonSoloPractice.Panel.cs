using CurtainCall.Network.Session;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSoloPractice
    {
        static readonly string[] SectionNames =
        {
            "1. 출발 대포 · 트램폴린 반동", "2. 첫 구간 운반 · 톱 통과", "3. 첫 구간 봉 잡기 · 반동",
            "4. 출발 이동 발판", "5. 쉼터 승강기", "6. 쉼터 재출발 발판",
            "7. 둘째 구간 운반 · 톱 통과", "8. 둘째 구간 봉 잡기 · 반동",
            "9. 쿠션 낙하 · 부상 유지", "10. 최종 대포 · 톱 · 목적지"
        };
        static readonly string[] ShortNames =
        {
            "1 대포 · 반동", "2 첫 운반 · 톱", "3 첫 봉 · 반동", "4 출발 발판", "5 쉼터 승강기",
            "6 재출발 발판", "7 둘째 운반 · 톱", "8 둘째 봉 · 반동", "9 쿠션 낙하", "10 최종 대포"
        };
        Vector2 scroll;
        bool showSettings;

        void SetPanel(bool value)
        {
            panel = value;
            // 마우스 조작 중 플레이어의 이동/발사 입력을 막고 닫힐 때 새 입력을 기다린다.
            InputGate.SetBlocking(this, value);
            foreach (var root in originalRoots)
            {
                if (root == null) continue;
                var networkUi = root.GetComponent<NetworkSessionTestUI>();
                if (networkUi != null) networkUi.enabled = !value;
                var hud = root.GetComponent<HumanCannonTestHud>();
                if (hud != null) hud.enabled = !value;
            }
        }

        void OnGUI()
        {
            if (!Application.isEditor || practiceTemplate == null) return;
            if (!panel)
            {
                if (GUI.Button(new Rect(10, Screen.height - 48, 300, 38), "F8 · 구간별 혼자 테스트 / 구간 선택")) SetPanel(true);
                if (IsPracticing)
                {
                    GUI.Box(new Rect(10, 10, 420, IsCarrierSection || section == 9 ? 166 : 105), "");
                    GUI.Label(new Rect(20, 18, 400, 25), SectionNames[section] + " — 1인 연습 중");
                    GUI.Label(new Rect(20, 43, 400, 25), "R: 같은 구간 재시작 / F·Space·WASD: 기존 조작");
                    GUI.Label(new Rect(20, 68, 400, 25), IsCarrierSection ? "손잡이 F → F9 봉 유지 도우미 → 톱 통과"
                        : section is 2 or 7 ? "Space 반동 → 공중 F로 봉 잡기 → W/S → Space 놓기"
                        : section == 8 ? "자동 낙하 후 부상 상태·안전 착지를 확인하세요."
                        : "보조 기능과 테스트 몸 상태는 구간 선택에서 변경");
                    if (IsCarrierSection && GUI.Button(new Rect(20, 98, 395, 28),
                        helperHold.IsOccupied ? "봉 도우미 놓기 (예고 후 톱 재동작)" : "봉 유지 도우미 시작")) TogglePartnerHold();
                    if (IsCarrierSection)
                        GUI.Label(new Rect(20, 130, 400, 26), "F 잡기: 자유 3인칭 / F 길게 놓기: 1인칭");
                    if (section == 9 && HumanCannonFinalSaws.Current != null)
                        GUI.Label(new Rect(20, 100, 400, 26), HumanCannonFinalSaws.Current.StatusText);
                    if (section == 9 && HumanCannonDestination.Current != null)
                        GUI.Label(new Rect(20, 130, 400, 26), HumanCannonDestination.Current.StatusText);
                }
                return;
            }
            GUILayout.BeginArea(new Rect(10, 10, Mathf.Min(440, Screen.width - 20), Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("구간별 혼자 테스트 · F8 닫기");
            GUILayout.Label("원하는 구간에서 바로 시작 / R 다시 시작");
            GUILayout.Label("전체 코스: 기본 1인칭 / 트램폴린 운반 중 자유 3인칭");
            GUI.enabled = !busy;
            for (int i = 0; i < SectionNames.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(ShortNames[i], GUILayout.Height(26))) SelectSection(i);
                if (GUILayout.Button(ShortNames[i + 1], GUILayout.Height(26))) SelectSection(i + 1);
                GUILayout.EndHorizontal();
            }
            if (IsPracticing && GUILayout.Button("기존 연속 코스로 돌아가기", GUILayout.Height(26))) ReturnToCourse();
            GUI.enabled = true;
            showSettings = GUILayout.Toggle(showSettings, "몸 상태 · 테스트 보조 설정");
            if (showSettings)
            {
                GUILayout.Label("다음 구간 시작/R 재시도에 적용할 몸 상태");
                bodyPreset = GUILayout.SelectionGrid(bodyPreset, new[] { "정상", "한 다리 없음", "두 다리 없음", "외팔" }, 2);
                catchAssist = GUILayout.Toggle(catchAssist, "비행: 트램폴린 자동 받기");
                partnerAssist = GUILayout.Toggle(partnerAssist, "봉을 잡으면 상대의 통과 대신 처리");
                fireEnabled = GUILayout.Toggle(fireEnabled, "연습 코스 불꽃 진행 켜기");
                GUILayout.Label("보조는 연습에서만 적용 / 실제 협동 검증 별도");
            }
            if (busy) GUILayout.Label("구간을 준비하고 있습니다…");
            else if (!string.IsNullOrEmpty(message))
                GUILayout.Label(message);
            if (GUILayout.Button("닫고 조작하기", GUILayout.Height(26))) SetPanel(false);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
