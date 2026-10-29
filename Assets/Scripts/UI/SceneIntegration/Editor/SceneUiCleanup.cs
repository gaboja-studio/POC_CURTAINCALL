#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.UI;
using CurtainCall.UI.PanelsV1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class SceneUiCleanup
    {
        [MenuItem("Tools/Curtain Call UI/Remove replaced performance UI")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            SceneUiLayoutPreview.Stop();
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(PerformanceSceneInstaller.ScenePath);
                // 이미 새 UI가 설치된 씬에서만 구형 UI를 제거한다.
                var space=All<PerformanceSceneBinding>(scene).Single();
                if (space.LocalBalance==null) throw new InvalidOperationException("P1 replacement required.");
                ClearReference(space,"legacyBalance");
                foreach(var item in All<BalanceGaugeView>(scene)) Object.DestroyImmediate(item.gameObject);
                foreach(var item in All<PlayerCommandDebugHud>(scene)) Object.DestroyImmediate(item.gameObject);
                foreach(var item in All<TightropeRunDebug>(scene)) Object.DestroyImmediate(item.gameObject);
                foreach(var item in All<LocalPlayerViews>(scene)) Object.DestroyImmediate(item);
                var tools=ToolsRoot(scene);
                foreach(var item in All<NetworkSessionTestUI>(scene)) item.transform.SetParent(tools,false);
                foreach(var item in All<TightropeCampaignHud>(scene)) item.transform.SetParent(tools,false);
                Save(scene);

                scene=EditorSceneManager.OpenScene(PerformanceSceneInstaller.CannonScenePath);
                var binding=All<HumanCannonUiBinding>(scene).Single();
                // 구간 경계는 새 어댑터에 이미 직렬화되어 있다. 물리 장치는 보존한다.
                ClearReference(binding,"legacy");
                foreach(var item in All<HumanCannonCourseSections>(scene)) Object.DestroyImmediate(item);
                tools=ToolsRoot(scene);
                MoveTools<NetworkSessionTestUI>(scene,tools,"SessionUI");
                MoveTools<HumanCannonTestHud>(scene,tools,"CannonPracticeControls");
                foreach(var label in All<PerformanceWorldLabelBinding>(scene))
                    if(label.Source!=null && label.Source.TryGetComponent<Renderer>(out var renderer))
                    {
                        // 기존 TMP는 동적 문구/위치의 데이터 원천이며 렌더러만 중복이다.
                        renderer.enabled=false;
                        if(PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                Save(scene);

                scene=EditorSceneManager.OpenScene(PerformanceEndingPresentation.ScenePath);
                foreach(var menus in All<CurtainCallMenuPanels>(scene))
                {
                    var root=menus.transform;
                    var dialogs=root.Find("70_ConfirmDialogs");
                    if(dialogs!=null) Object.DestroyImmediate(dialogs.gameObject);
                    var actions=root.Find("60_Panels/Menu_C13/50_Content/ActionsAndFooter");
                    if(actions!=null) Object.DestroyImmediate(actions.gameObject);
                    Object.DestroyImmediate(menus);
                }
                Save(scene);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        static Transform ToolsRoot(Scene scene) => (scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="DevelopmentTools") ?? new GameObject("DevelopmentTools")).transform;
        static void MoveTools<T>(Scene scene,Transform parent,string name) where T:MonoBehaviour
        {
            foreach(var item in All<T>(scene))
            {
                if(item.transform.IsChildOf(parent)) continue;
                var target=new GameObject(name); target.transform.SetParent(parent,false);
                var copy=target.AddComponent<T>();
                EditorUtility.CopySerialized(item,copy);
                Object.DestroyImmediate(item);
            }
        }
        static void ClearReference(Object owner,string property)
        {
            var data=new SerializedObject(owner);
            data.FindProperty(property).objectReferenceValue=null;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save "+scene.path);
        }
    }
}
#endif
