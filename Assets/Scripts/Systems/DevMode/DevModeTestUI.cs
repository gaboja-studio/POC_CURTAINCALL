using CurtainCall.Network.Session;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Dev
{
    /// <summary>
    /// 테스트 씬(Tests/Systems/DevMode/DevMode) 전용 임시 화면. 개발자 모드 상태와 예외가 적용된 인원 규칙을 보여 준다.
    /// 생존자 판정은 공연 결과(Task-031)가 붙기 전까지 버튼으로 흉내 낸다. 접속·런 진행은 같은 씬의 NetworkSessionTestUI·CampaignFlowTestUI로 한다.
    /// </summary>
    public sealed class DevModeTestUI : MonoBehaviour
    {
        // run-rules.md #2: 생존 2명 미만이면 묘기 실패. 정식값은 공연 결과(Task-031)가 가진다.
        const int OfficialMinSurvivors = 2;

        [SerializeField] Rect _panelRect = new Rect(770, 50, 300, 260);

        int _survivors = 1;

        void OnGUI()
        {
            GUILayout.BeginArea(_panelRect, "개발자 모드", GUI.skin.window);

            GUILayout.Label(DevMode.Enabled
                ? "켜짐 (Play를 멈추고 Tools/CurtainCall 메뉴에서 끔)"
                : "꺼짐 (Play를 멈추고 Tools/CurtainCall 메뉴에서 켬)");

            int officialPlayers = GameSettings.Base.Session.MinPlayers;
            GUILayout.Label($"시작 최소 인원: {DevMode.MinPlayers(officialPlayers)}명 (정식 {officialPlayers}명)");

            var session = NetworkSessionManager.Instance;
            if (session != null)
                GUILayout.Label($"현재 {session.PlayerCount}명 · 시작 가능: {(session.CanStartGame ? "예" : "아니오")}");

            int minSurvivors = DevMode.MinSurvivors(OfficialMinSurvivors);
            GUILayout.Label($"묘기 계속 최소 생존자: {minSurvivors}명 (정식 {OfficialMinSurvivors}명)");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"생존자 {_survivors}명 →", GUILayout.Width(90));
            if (GUILayout.Button("-", GUILayout.Width(28))) _survivors = Mathf.Max(0, _survivors - 1);
            if (GUILayout.Button("+", GUILayout.Width(28))) _survivors++;
            GUILayout.Label(_survivors < minSurvivors ? "묘기 실패" : "계속 진행");
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
