using UnityEditor;
using UnityEngine;

namespace CurtainCall.Settings.EditorTools
{
    /// <summary>사람이 자주 바꾸는 실제 빌드 설정을 Inspector에서 바로 연다.</summary>
    public static class PlaytestSettingsMenu
    {
        [MenuItem("CurtainCall/게임 조정/구매 시간·신체 복구 가격")]
        public static void WaitingRoom() => Select("WaitingRoomSettings");

        [MenuItem("CurtainCall/게임 조정/쉬는 시간")]
        public static void RestTimes() => Select("BaseGameSettings");

        static void Select(string name)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Resources/Settings/GameSettings/" + name + ".asset");
            if (asset == null) { Debug.LogError("설정 에셋을 찾을 수 없습니다: " + name); return; }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }
    }
}
