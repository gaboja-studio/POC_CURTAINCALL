#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.UI.PerformanceHudV1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class PerformanceDataInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Connect audience and solo progress")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                const string asset = "Assets/Resources/" + AudienceReactionSettings.ResourcePath + ".asset";
                if (AssetDatabase.LoadAssetAtPath<AudienceReactionSettings>(asset) == null)
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AudienceReactionSettings>(), asset);
                var common = EditorSceneManager.OpenScene(CommonPerformanceUI.ScenePath, OpenSceneMode.Single);
                var progress = common.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PerformanceProgressView>(true)).Single();
                var adapter = progress.GetComponent<SoloPerformanceProgress>() ?? progress.gameObject.AddComponent<SoloPerformanceProgress>();
                var src = new SerializedObject(progress);
                var dst = new SerializedObject(adapter);
                dst.FindProperty("source").objectReferenceValue = progress;
                foreach (string name in new[] { "playerRoot", "playerImage", "playerUnderlay", "playerNumber" })
                    dst.FindProperty(name).objectReferenceValue = src.FindProperty(name + "s").GetArrayElementAtIndex(0).objectReferenceValue;
                foreach (string name in new[] { "fireRoot", "fireMarker", "arrivalMarker", "maximumMarker", "remainingText", "selfSprite", "teammateSprite" })
                    dst.FindProperty(name).objectReferenceValue = src.FindProperty(name).objectReferenceValue;
                dst.FindProperty("fallMarker").objectReferenceValue = src.FindProperty("fallTemplate").objectReferenceValue;
                dst.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(common); EditorSceneManager.SaveScene(common);
                foreach (string path in new[] { PerformanceSceneInstaller.ScenePath, PerformanceSceneInstaller.CannonScenePath })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var binding = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PerformanceSceneBinding>(true)).Single();
                    var audience = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PerformanceAudience>(true)).SingleOrDefault();
                    if (audience == null) audience = new GameObject("PerformanceAudience").AddComponent<PerformanceAudience>();
                    var values = new SerializedObject(audience);
                    var space = new SerializedObject(binding);
                    values.FindProperty("humanCannon").boolValue = path == PerformanceSceneInstaller.CannonScenePath;
                    foreach (string name in new[] { "cannonStart", "cannonFinish" })
                        values.FindProperty(name).objectReferenceValue = space.FindProperty(name).objectReferenceValue;
                    var fires = space.FindProperty("cannonFires");
                    values.FindProperty("cannonFires").arraySize = fires.arraySize;
                    for (int i = 0; i < fires.arraySize; i++) values.FindProperty("cannonFires").GetArrayElementAtIndex(i).objectReferenceValue = fires.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (path == PerformanceSceneInstaller.CannonScenePath)
                    {
                        var sections = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonCourseSections>(true)).Single();
                        var bounds = new SerializedObject(sections).FindProperty("boundaries");
                        values.FindProperty("lowerFloorMaxY").floatValue = bounds.FindPropertyRelative("RaisedMinY").floatValue;
                        values.FindProperty("lowerFloorStartZ").floatValue = bounds.FindPropertyRelative("FirstStartZ").floatValue;
                    }
                    values.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
#endif
