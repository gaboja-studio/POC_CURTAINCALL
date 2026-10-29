#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.CommonIcons;
using CurtainCall.UI.BackstageHudV1;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.WaitingRoom;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class ContextualHudInstaller
    {
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(CommonMenuUI.ScenePath);
                var root = scene.GetRootGameObjects().First(x => x.name == "UI");
                if (root.GetComponentInChildren<CommonInventoryUI>(true) == null)
                {
                    var canvas = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/BackstageHud/InventorySlotTemplate.prefab"),root.transform);
                    canvas.name = "C5_InventoryCanvas";
                    canvas.AddComponent<CanvasGroup>();
                    canvas.GetComponent<Canvas>().sortingOrder = 110;
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                    var slots = canvas.GetComponentInChildren<InventorySlotsView>(true);
                    var rect = (RectTransform)slots.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f,0); rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(-176,28); rect.sizeDelta = new Vector2(352,64);
                    var label = new GameObject("SelectedProp", typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    label.transform.SetParent(canvas.transform,false);
                    var lr = label.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(.5f,0); lr.pivot = new Vector2(.5f,0);
                    lr.anchoredPosition = new Vector2(0,96); lr.sizeDelta = new Vector2(600,32);
                    label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset");
                    label.fontSize = 20; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                    var owner = canvas.AddComponent<CommonInventoryUI>();
                    owner.slots = slots; owner.group = canvas.GetComponent<CanvasGroup>(); owner.selectedLabel = label;
                    foreach(var c in canvas.GetComponentsInChildren<Component>(true)) if(c != null && PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    owner.icons = AssetDatabase.LoadAssetAtPath<CommonIconCatalog>("Assets/Resources/Prefabs/UIs/CommonIcons/CommonIconCatalog.asset");
                }
                EditorSceneManager.SaveScene(scene);
                foreach (string path in new[]{"Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity", "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity"})
                {
                    scene = EditorSceneManager.OpenScene(path);
                    if (!scene.GetRootGameObjects().Any(r=>r.GetComponentInChildren<HeldPropsView>(true)!=null)) new GameObject("SelectedPropViews",typeof(HeldPropsView));
                    if (path.Contains("Tightrope"))
                    {
                        var old = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TightropeControlsPanel>(true)).FirstOrDefault();
                        if (old != null && old.banner == null)
                        {
                            var parent = old.GetComponentInParent<Canvas>().transform.parent;
                            UnityEngine.Object.DestroyImmediate(old.gameObject);
                            var bannerObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Notifications/SystemBanner.prefab"), parent);
                            bannerObject.name = "ContextualControlsBanner";
                            var banner = bannerObject.GetComponentInChildren<SystemBannerView>(true);
                            var bs = bannerObject.GetComponent<CanvasScaler>(); bs.referenceResolution = new Vector2(1920,1080); bs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; bs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                            var br = (RectTransform)banner.transform; br.anchorMin = br.anchorMax = new Vector2(.5f,1); br.pivot = new Vector2(.5f,1); br.anchoredPosition = new Vector2(0,-110);
                            var guideFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset");
                            foreach (var label in bannerObject.GetComponentsInChildren<TMP_Text>(true))
                            { label.font = guideFont; label.fontSharedMaterial = guideFont.material; }
                            var owner = bannerObject.AddComponent<TightropeControlsPanel>(); owner.banner = banner;
                            foreach (var c in bannerObject.GetComponentsInChildren<Component>(true)) if (c != null && PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                        }
                    }
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
#endif
