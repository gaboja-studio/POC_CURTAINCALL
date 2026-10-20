#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class PerformanceResultsInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Install C12 results")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes first.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/ResultsUi/ResultsPanel.prefab");
            if (prefab == null) throw new InvalidOperationException("Tested Assets ResultsPanel required.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(PerformanceEndingPresentation.ScenePath);
                var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PerformanceEndingPresentation>(true)).Single();
                var data = new SerializedObject(owner);
                var ui = owner.transform.Find("UI");
                var original = ui.GetComponentInChildren<CurtainCall.UI.ResultsPanelView>(true);
                var integrated = ui.GetComponentInChildren<ResultsPanelView>(true);
                if (integrated != null && new SerializedObject(integrated).FindProperty("visuals").objectReferenceValue == null)
                {
                    UnityEngine.Object.DestroyImmediate(integrated.GetComponentInParent<Canvas>().gameObject);
                    integrated = null; original = null;
                }
                if (integrated == null)
                {
                    if (original == null)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ui);
                        instance.name = "C12ResultsCanvas";
                        original = instance.GetComponentInChildren<CurtainCall.UI.ResultsPanelView>(true);
                    }
                    integrated = original.gameObject.AddComponent<ResultsPanelView>();
                    var source = new SerializedObject(original);
                    var destination = new SerializedObject(integrated);
                    foreach (string field in new[] { "visuals", "layoutStage", "ribbon", "grandCrest", "maximumMarker",
                        "failureRibbon", "successRibbon", "grandRibbon", "selfMarker", "teammateMarker",
                        "textLayers", "memberLayers", "acknowledge", "rewardTransfer" })
                        destination.CopyFromSerializedProperty(source.FindProperty(field));
                    destination.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Object.DestroyImmediate(original);
                }
                var canvas = integrated.GetComponentInParent<Canvas>();
                canvas.sortingOrder = 200;
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
                data.FindProperty("resultsView").objectReferenceValue = integrated;
                var curtain = ui.GetComponentInChildren<TransitionCurtainView>(true);
                if (curtain == null)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/TransitionCurtain/TransitionCurtain.prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, ui);
                    instance.name = "C16TransitionCurtainCanvas";
                    curtain = instance.GetComponentInChildren<TransitionCurtainView>(true);
                    var cc = instance.GetComponent<Canvas>(); cc.sortingOrder = 2000;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cc);
                }
                data.FindProperty("curtain").objectReferenceValue = curtain;
                if (owner.GetComponent<SceneCurtainTransition>() == null) owner.gameObject.AddComponent<SceneCurtainTransition>();
                foreach (string name in new[] { "DevSoloResultCanvas", "ResultCanvas", "BlackTransitionCanvas" })
                {
                    var old = ui.Find(name);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
#endif
