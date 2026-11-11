#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.UI.GuidesV1;
using CurtainCall.UI.TooltipsV1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class HumanCannonUiInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Install Human Cannon hints")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var guideAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Retained/Guides/Guides.prefab");
            var tooltipAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Retained/Tooltips/Tooltips.prefab");
            var fallback = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
            if (guideAsset == null || tooltipAsset == null || fallback == null) throw new InvalidOperationException("Tested prefabs and existing game font required.");
            var scene = EditorSceneManager.OpenScene(PerformanceSceneInstaller.CannonScenePath, OpenSceneMode.Single);
            var ui = scene.GetRootGameObjects().Single(r => r.name == "UI");
            if (ui.GetComponent<HumanCannonUiBinding>() != null) throw new InvalidOperationException("Already installed; edit scene instances instead.");
            var legacy = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonCourseSections>(true)).Single();
            var binding = ui.AddComponent<HumanCannonUiBinding>();
            binding.Configure(legacy);
            EditorUtility.SetDirty(binding);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var commonScene = EditorSceneManager.OpenScene(CommonPerformanceUI.ScenePath, OpenSceneMode.Additive);
            var common = commonScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CommonPerformanceUI>(true)).Single();
            var guideRoot = (GameObject)PrefabUtility.InstantiatePrefab(guideAsset, common.transform);
            var tooltipRoot = (GameObject)PrefabUtility.InstantiatePrefab(tooltipAsset, common.transform);
            guideRoot.name = "CommonGuideCanvas"; tooltipRoot.name = "CommonTooltipCanvas";
            common.ConfigureHints(guideRoot.GetComponentInChildren<GuideView>(true), tooltipRoot.GetComponentInChildren<TooltipView>(true));
            var fonts = common.gameObject.AddComponent<UiRuntimeFontFallback>(); fonts.Configure(fallback);
            fonts.ConfigureSources(AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-Medium.ttf"),
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-SemiBold.ttf"));
            common.HideHints();
            EditorUtility.SetDirty(common); EditorUtility.SetDirty(fonts);
            EditorSceneManager.MarkSceneDirty(commonScene);
            EditorSceneManager.SaveScene(commonScene);
            EditorSceneManager.CloseScene(commonScene, true);
        }
    }
}
#endif
