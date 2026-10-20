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
            var guideAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Guides/Guides.prefab");
            var tooltipAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Tooltips/Tooltips.prefab");
            var fallback = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
            if (guideAsset == null || tooltipAsset == null || fallback == null) throw new InvalidOperationException("Tested prefabs and existing game font required.");
            var scene = EditorSceneManager.OpenScene(PerformanceSceneInstaller.CannonScenePath, OpenSceneMode.Single);
            var ui = scene.GetRootGameObjects().Single(r => r.name == "UI");
            if (ui.GetComponent<HumanCannonUiBinding>() != null) throw new InvalidOperationException("Already installed; edit scene instances instead.");
            var legacy = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonCourseSections>(true)).Single();
            var values = new SerializedObject(legacy).FindProperty("boundaries");
            var boundaries = new HumanCannonCourseSections.Boundaries(values.FindPropertyRelative("FirstStartZ").floatValue,
                values.FindPropertyRelative("ShelterStartZ").floatValue, values.FindPropertyRelative("SecondStartZ").floatValue,
                values.FindPropertyRelative("FinalStartZ").floatValue, values.FindPropertyRelative("RaisedMinY").floatValue);
            var canvas = new GameObject("HumanCannonSectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(ui.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 1;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var label = new GameObject("CurrentSection", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(canvas.transform, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0);
            label.rectTransform.pivot = new Vector2(.5f, 0);
            label.rectTransform.anchoredPosition = new Vector2(0, 48);
            label.rectTransform.sizeDelta = new Vector2(544, 48);
            label.font = fallback; label.fontSize = 28; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false; label.text = string.Empty;
            // TMP creation can initialize the default font after AddComponent. Persist the explicit reference.
            var textData = new SerializedObject(label);
            textData.FindProperty("m_fontAsset").objectReferenceValue = fallback;
            textData.FindProperty("m_sharedMaterial").objectReferenceValue = fallback.material;
            textData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(label);
            var binding = ui.AddComponent<HumanCannonUiBinding>();
            binding.Configure(legacy, boundaries, label);
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
