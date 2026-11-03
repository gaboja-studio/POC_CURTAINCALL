using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Dev.Editor
{
    public sealed class WaitingRoomSceneWindow : EditorWindow
    {
        [MenuItem("Tools/Dev Mode/대기실 씬 설정")]
        static void Open() => GetWindow<WaitingRoomSceneWindow>("대기실 씬 설정");
        void OnGUI()
        {
            var settings = GameSettings.Base;
            var serialized = new SerializedObject(settings);
            var variant = serialized.FindProperty("spaceTransition.waitingRoomVariant");
            // Field name follows BaseGameSettings' serialized container.
            if (variant == null) { EditorGUILayout.HelpBox("장소 전환 설정을 찾지 못했습니다.", MessageType.Error); return; }
            EditorGUILayout.HelpBox("실제 플레이와 Dev 시작이 함께 사용하는 대기실입니다. 변경 후 런을 다시 시작하세요. 호스트가 접속자 전체의 씬 이동을 결정합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) {
                EditorGUILayout.PropertyField(variant, new GUIContent("대기실 씬"));
                serialized.ApplyModifiedProperties();
                if (GUI.changed) AssetDatabase.SaveAssetIfDirty(settings);
            }
            var selection = settings.SpaceTransition.WaitingRoomVariant;
            EditorGUILayout.LabelField("로드 대상", settings.SpaceTransition.WaitingRoomScene);
            EditorGUILayout.SelectableLabel(WaitingRoomScenes.Path(selection), EditorStyles.wordWrappedLabel, GUILayout.Height(42));
            EditorGUILayout.LabelField("추격 시스템", WaitingRoomScenes.PursuitEnabled ? "사용" : "사용 안 함");
            if (GUILayout.Button("공통 설정 에셋 선택")) Selection.activeObject = settings;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("선택한 대기실 열기") && UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(WaitingRoomScenes.Path(selection));
        }
    }
}