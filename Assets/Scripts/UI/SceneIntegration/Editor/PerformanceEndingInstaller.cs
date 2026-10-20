#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.Settings;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.UI.PanelsV1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class PerformanceEndingInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Install performance ending scene")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PerformanceEndingPresentation.ScenePath) != null)
                throw new InvalidOperationException("Ending scene exists; edit its instances.");
            var bannerAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Notifications/SystemNotifications.prefab");
            var panelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/BasicPanels/BasicPanels.prefab");
            if (bannerAsset == null || panelAsset == null) throw new InvalidOperationException("Tested Assets prefabs required.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("PerformanceTransitionUI");
                var owner = root.AddComponent<PerformanceEndingPresentation>();
                var ui = new GameObject("UI"); ui.transform.SetParent(root.transform, false);
                var notifications = (GameObject)PrefabUtility.InstantiatePrefab(bannerAsset, scene);
                notifications.name = "DeathNotificationCanvas"; notifications.transform.SetParent(ui.transform, false);
                notifications.GetComponent<Canvas>().sortingOrder = 210;
                notifications.GetComponent<SystemNotificationsView>().Banner.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -120);
                var panels = (GameObject)PrefabUtility.InstantiatePrefab(panelAsset, scene);
                panels.name = "ResultCanvas"; panels.transform.SetParent(ui.transform, false);
                panels.GetComponent<Canvas>().sortingOrder = 200;
                panels.GetComponent<CurtainCallMenuPanels>().enabled = false;
                UnityEngine.Object.DestroyImmediate(panels.GetComponent<GraphicRaycaster>());
                panels.transform.Find("70_ConfirmDialogs/LeaveConfirmation").gameObject.SetActive(false);
                var panel = panels.transform.Find("60_Panels/Menu_C13").GetComponent<CurtainCallPanelView>();
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(640, 600);
                panel.Configure(rect, panel.GetComponent<CanvasGroup>(), false);
                var content = panel.transform.Find("50_Content");
                content.Find("ActionsAndFooter").gameObject.SetActive(false);
                var title = content.Find("TitleTMP").GetComponent<TMP_Text>();
                title.text = "공연 결과";
                var summary = new GameObject("ResultSummary", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                summary.transform.SetParent(content, false);
                summary.font = title.font; summary.fontSize = 26; summary.color = title.color;
                summary.alignment = TextAlignmentOptions.TopLeft;
                summary.rectTransform.anchorMin = summary.rectTransform.anchorMax = new Vector2(.5f, 1);
                summary.rectTransform.pivot = new Vector2(.5f, 1);
                summary.rectTransform.anchoredPosition = new Vector2(0, -92);
                summary.rectTransform.sizeDelta = new Vector2(544, 440);
                summary.richText = false;
                panel.SetInputAllowed(false); panel.SetVisible(false, true);
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                var blackout = new GameObject("BlackTransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                blackout.transform.SetParent(ui.transform, false);
                var canvas = blackout.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
                var fill = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                fill.transform.SetParent(blackout.transform, false); fill.color = Color.black;
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                var group = blackout.GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
                var serialized = new SerializedObject(owner);
                serialized.FindProperty("notifications").objectReferenceValue = notifications.GetComponent<SystemNotificationsView>();
                serialized.FindProperty("resultPanel").objectReferenceValue = panel;
                serialized.FindProperty("title").objectReferenceValue = title;
                serialized.FindProperty("summary").objectReferenceValue = summary;
                serialized.FindProperty("black").objectReferenceValue = group;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var fonts = root.AddComponent<UiRuntimeFontFallback>();
                fonts.Configure(Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF"));
                fonts.ConfigureSources(AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-Medium.ttf"),
                    AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-SemiBold.ttf"));
                const string settingsPath = "Assets/Resources/" + PerformanceEndingSettings.ResourcePath + ".asset";
                if (AssetDatabase.LoadAssetAtPath<PerformanceEndingSettings>(settingsPath) == null)
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PerformanceEndingSettings>(), settingsPath);
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorSceneManager.SaveScene(scene, PerformanceEndingPresentation.ScenePath);
                var build = EditorBuildSettings.scenes.ToList();
                if (!build.Any(s => s.path == PerformanceEndingPresentation.ScenePath)) build.Add(new EditorBuildSettingsScene(PerformanceEndingPresentation.ScenePath, true));
                EditorBuildSettings.scenes = build.ToArray();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
#endif
