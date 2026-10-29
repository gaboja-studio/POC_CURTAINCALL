using System;
using System.IO;
using System.Linq;
using CurtainCall.UI.RestTimerV28.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.EditorTools
{
    /// <summary>제공 조립기로 프리팹을 생성하고 공통 대기실 HUD에 직렬화된 참조로 연결한다.</summary>
    public static class RestTimerProjectBuilder
    {
        public const string Output = "Assets/Resources/Prefabs/UIs/RestTimerV28";
        public const string Prefab = Output + "/W1_RestTimer_v2_8.prefab";

        [MenuItem("Tools/Curtain Call/대기실 타이머/v2.8 생성 및 연결")]
        public static void BuildAndInstall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("편집 모드에서 임포트·컴파일 완료 후 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("열린 씬의 미저장 변경을 먼저 보존하세요.");
            if (File.Exists(Prefab) || File.Exists(Prefab + ".meta"))
                throw new InvalidOperationException("기존 프리팹은 덮어쓰지 않습니다: " + Prefab);
            var semiBold = Font("SemiBold");
            var bold = Font("Bold");
            RestTimerPrefabBuilder.BuildProject(Output, semiBold, bold);
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                root.AddComponent<RestTimerView>();
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                // 이전 W1 다이얼 중심 (144,-156)을 유지한다.
                rect.anchoredPosition = new Vector2(48, -64);
                if (PrefabUtility.SaveAsPrefabAsset(root, Prefab) == null) throw new IOException("타이머 저장 실패");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Install();
        }

        static TMP_FontAsset Font(string weight)
        {
            string required = weight == "SemiBold" ? "0123456789D-?" : "0123456789:-+";
            foreach (string folder in new[] { "DayClock/V24", "SettingsUi", "BackstageHud", "WorldLabels" })
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/" + folder + "/Pretendard-" + weight + " SDF.asset");
                if (font != null && font.HasCharacters(required)) return font;
            }
            throw new InvalidOperationException("타이머 글리프가 있는 기존 Pretendard 폰트가 필요합니다: " + weight);
        }

        public static void Install()
        {
            var existing = SceneManager.GetSceneByPath(CommonWaitingRoomUI.ScenePath);
            bool wasLoaded = existing.isLoaded;
            var scene = wasLoaded ? existing : EditorSceneManager.OpenScene(CommonWaitingRoomUI.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("공통 대기실 UI 씬에 미저장 변경이 있습니다.");
                var ui = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CommonWaitingRoomUI>(true)).Single();
                var serialized = new SerializedObject(ui);
                var timer = serialized.FindProperty("restTimer");
                if (timer.objectReferenceValue != null) throw new InvalidOperationException("이미 연결된 타이머를 덮어쓰지 않습니다.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (prefab == null || ui.Hud?.Clock == null) throw new InvalidOperationException("타이머 프리팹 또는 HUD 참조 누락");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ui.Hud.Clock.transform.parent);
                timer.objectReferenceValue = instance.GetComponent<RestTimerView>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ui.ArrangeTimerLayout();
                if (ui.DayClockDisplay != null)
                {
                    ui.DayClockDisplay.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ui.DayClockDisplay.gameObject);
                }
                ui.Hud.Clock.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ui.Hud.Clock.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("공통 대기실 UI 씬 저장 실패");
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
