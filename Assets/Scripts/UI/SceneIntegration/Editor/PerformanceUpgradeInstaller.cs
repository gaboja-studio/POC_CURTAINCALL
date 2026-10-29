#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Reward;
using CurtainCall.UI.SpectatorV1;
using CurtainCall.UI.WorldLabelsV2;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class PerformanceUpgradeInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Upgrade performance presentation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var common=EditorSceneManager.OpenScene(CommonPerformanceUI.ScenePath);
                var ui=Find<CommonPerformanceUI>(common).gameObject;
                if (ui.GetComponent<PerformanceSpectatorBinding>() == null)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Spectator/Spectator.prefab");
                    if (prefab == null) throw new InvalidOperationException("Tested Spectator prefab required.");
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,ui.transform);
                    instance.name="CommonSpectatorCanvas"; instance.GetComponent<Canvas>().sortingOrder=90;
                    var view=instance.GetComponentInChildren<SpectatorView>(true);
                    view.HideImmediate(); ui.AddComponent<PerformanceSpectatorBinding>().Configure(view);
                    Record(instance);
                }
                Save(common);
                var cannon=EditorSceneManager.OpenScene(PerformanceSceneInstaller.CannonScenePath);
                RestoreCannon(cannon);
                InstallLabels(cannon);
                Save(cannon);
                // 결과 본문은 4명 정산/잔액/자동 전환을 수용하고 기존 정본 폰트는 보존한다.
                var ending=EditorSceneManager.OpenScene(PerformanceEndingPresentation.ScenePath);
                var presentation=Find<PerformanceEndingPresentation>(ending);
                var data=new SerializedObject(presentation);
                var summary=(TMP_Text)data.FindProperty("summary").objectReferenceValue;
                summary.fontSize=23; summary.rectTransform.sizeDelta=new Vector2(544,440);
                Save(ending);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static T Find<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        static void RestoreCannon(Scene scene)
        {
            var ui=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="UI") ?? new GameObject("UI");
            var fonts=ui.GetComponent<UiRuntimeFontFallback>() ?? ui.AddComponent<UiRuntimeFontFallback>();
            fonts.Configure(Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF"));
            fonts.ConfigureSources(AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-Medium.ttf"),
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-SemiBold.ttf"));
            var binding=ui.GetComponent<PerformanceSceneBinding>() ?? ui.AddComponent<PerformanceSceneBinding>();
            binding.ConfigureSpace(null,null);
            var destination=Find<HumanCannonDestination>(scene);
            var finish=(Component)new SerializedObject(destination).FindProperty("destination").objectReferenceValue;
            var system=Find<HumanCannonSystem>(scene);
            var fires=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HumanCannonFireWall>(true)).ToArray();
            binding.ConfigureCannon(system.Station(0).Muzzle,finish.transform,fires);
            var legacy=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HumanCannonCourseSections>(true)).SingleOrDefault();
            UnityEngine.Object boundaryOwner=legacy!=null ? legacy : ui.GetComponent<HumanCannonUiBinding>();
            if(boundaryOwner==null) throw new InvalidOperationException("Existing course boundaries required.");
            var b=new SerializedObject(boundaryOwner).FindProperty("boundaries");
            var bounds=new HumanCannonCourseSections.Boundaries(b.FindPropertyRelative("FirstStartZ").floatValue,
                b.FindPropertyRelative("ShelterStartZ").floatValue,b.FindPropertyRelative("SecondStartZ").floatValue,
                b.FindPropertyRelative("FinalStartZ").floatValue,b.FindPropertyRelative("RaisedMinY").floatValue);
            if (ui.GetComponent<HumanCannonUiBinding>()==null)
            {
                var canvas=new GameObject("HumanCannonSectionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
                canvas.transform.SetParent(ui.transform,false); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
                var label=new GameObject("CurrentSection",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(canvas.transform,false); label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(.5f,0);
                label.rectTransform.anchoredPosition=new Vector2(0,48); label.rectTransform.sizeDelta=new Vector2(544,48);
                label.font=Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
                label.fontSize=28; label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false; label.text="";
                ui.AddComponent<HumanCannonUiBinding>().Configure(legacy,bounds,label);
            }
            var audience=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PerformanceAudience>(true)).SingleOrDefault()
                ?? new GameObject("PerformanceAudience").AddComponent<PerformanceAudience>();
            var values=new SerializedObject(audience);
            values.FindProperty("humanCannon").boolValue=true;
            values.FindProperty("cannonStart").objectReferenceValue=system.Station(0).Muzzle;
            values.FindProperty("cannonFinish").objectReferenceValue=finish.transform;
            values.ApplyModifiedPropertiesWithoutUndo();
            foreach(var root in scene.GetRootGameObjects())
                if(root!=ui && (root.GetComponent<Canvas>()!=null || root.name=="SessionUI" || root.name=="DebugHud")) root.transform.SetParent(ui.transform,true);
        }
        static void InstallLabels(Scene scene)
        {
            var ui=scene.GetRootGameObjects().Single(r=>r.name=="UI");
            var parent=ui.transform.Find("WorldLabels");
            if(parent!=null) return;
            parent=new GameObject("WorldLabels").transform; parent.SetParent(ui.transform,false);
            var sources=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TextMeshPro>(true)).ToArray();
            foreach(var source in sources)
            {
                bool distance=source.name.StartsWith("Distance_");
                bool finish=source.name.StartsWith("Finish");
                bool section=source.name=="Label" && source.text.Contains("·");
                if(!distance && !finish && !section) continue;
                string kind=finish?"Cloth":"Wood";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/WorldLabels/WorldLabel"+kind+".prefab");
                if(prefab==null) throw new InvalidOperationException("Tested world label required.");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
                instance.name=source.name+"_UI";
                var label=instance.GetComponent<WorldLabelView>();
                if(!label.TrySetSize(new Vector2(section?640:320,96)) || !label.TrySetContent(source.text)) throw new InvalidOperationException(label.LastIssue);
                // 기존 바닥/표지판 소재는 유지하고 그 위의 글자만 정본으로 통일한다.
                label.TrySetSurfaceVisible(false);
                instance.transform.localScale=Vector3.one*(section?.009f:finish?.015f:.009f);
                instance.transform.SetPositionAndRotation(source.transform.position-source.transform.forward*.025f,source.transform.rotation);
                instance.AddComponent<PerformanceWorldLabelBinding>().Configure(source,label);
                Record(instance);
            }
        }
        static void Record(GameObject root)
        {
            foreach(var c in root.GetComponentsInChildren<Component>(true))
                if(c!=null && PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
        static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save "+scene.path);
        }
    }
}
#endif
