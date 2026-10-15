using UnityEditor;
using UnityEngine;

namespace CurtainCall.Dev
{
    /// <summary>
    /// 에디터 메뉴로 개발자 모드를 켜고 끈다. 값은 이 PC의 EditorPrefs에만 저장돼 다른 팀원에게 영향이 없다.
    /// Play 중에는 바꿀 수 없다(게임 진행 중 규칙이 바뀌지 않게).
    /// </summary>
    static class DevModeMenu
    {
        const string MenuPath = "Tools/CurtainCall/Dev Mode (1인 테스트)";

        [MenuItem(MenuPath, priority = 0)]
        static void Toggle()
        {
            DevMode.SetEditorEnabled(!DevMode.Enabled);
            Debug.Log($"[DevMode] 개발자 모드 {(DevMode.Enabled ? "켜짐 — 최소 인원 1명, 전원 사망 때만 묘기 실패" : "꺼짐 — 정식 인원 규칙")}");
        }

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            Menu.SetChecked(MenuPath, DevMode.Enabled);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
