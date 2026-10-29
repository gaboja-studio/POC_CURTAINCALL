#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.CheerGauge;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.UI.PerformanceHudV1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class PerformanceSceneInstaller
    {
        public const string ScenePath = "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity";
        public const string CannonScenePath = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
        public const string HudPath = "Assets/Resources/Prefabs/UIs/PerformanceHud/PerformanceHud.prefab";
        public const string CheerPath = "Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab";

        [MenuItem("Tools/Curtain Call UI/Install shared performance UI scenes")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Editor must be idle.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before installation.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CommonPerformanceUI.ScenePath) != null)
                throw new InvalidOperationException("Shared UI already installed. Edit existing scene instances; do not duplicate them.");
            var hudAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var cheerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CheerPath);
            if (hudAsset == null || cheerAsset == null) throw new InvalidOperationException("Tested source prefabs are required.");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/UI")) AssetDatabase.CreateFolder("Assets/Scenes", "UI");
            var common = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var commonRoot = new GameObject("UI");
            SceneManager.MoveGameObjectToScene(commonRoot, common);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(hudAsset, commonRoot.transform);
            root.name = "CommonPerformanceCanvas";
            var view = root.GetComponent<PerformanceHUDView>();
            // Removed-child overrides preserve the source prefab and all remaining graphical references.
            UnityEngine.Object.DestroyImmediate(view.Balance.gameObject);
            var gauge = (GameObject)PrefabUtility.InstantiatePrefab(cheerAsset, root.transform);
            var stock = gauge.GetComponent<C1CheerGaugeBinding>();
            stock.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(stock);
            var commonView = commonRoot.AddComponent<CommonPerformanceUI>();
            commonView.Configure(view, gauge.GetComponent<C1CheerGaugeView>());
            commonView.Hide();
            EditorUtility.SetDirty(commonView);
            Save(common, CommonPerformanceUI.ScenePath);
            EditorSceneManager.CloseScene(common, true);

            foreach (string path in new[] { ScenePath, CannonScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var previous = roots.SelectMany(r => r.GetComponentsInChildren<BalanceGaugeView>(true)).SingleOrDefault();
                var old = roots.SelectMany(r => r.GetComponentsInChildren<PerformanceSceneBinding>(true)).SingleOrDefault();
                // Only the previous integration root is migrated; existing game objects stay intact.
                if (old != null)
                {
                    if (old.gameObject.name != "PerformanceHud") throw new InvalidOperationException("Unexpected integration root.");
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                var ui = new GameObject("UI");
                SceneManager.MoveGameObjectToScene(ui, scene);
                TightropeBalanceView balance = null;
                if (path == ScenePath)
                {
                    var local = (GameObject)PrefabUtility.InstantiatePrefab(hudAsset, ui.transform);
                    local.name = "TightropeBalanceCanvas";
                    var localView = local.GetComponent<PerformanceHUDView>();
                    balance = localView.Balance;
                    UnityEngine.Object.DestroyImmediate(localView.Timer.gameObject);
                    UnityEngine.Object.DestroyImmediate(localView.Progress.gameObject);
                    UnityEngine.Object.DestroyImmediate(localView);
                    balance.SetContext(false, false);
                    balance.SetUnavailable();
                }
                var binding = ui.AddComponent<PerformanceSceneBinding>();
                binding.ConfigureSpace(balance, previous);
                if (path == CannonScenePath)
                {
                    var destination = roots.SelectMany(r => r != null ? r.GetComponentsInChildren<HumanCannonDestination>(true) : Array.Empty<HumanCannonDestination>()).Single();
                    var landing = new SerializedObject(destination).FindProperty("destination").objectReferenceValue as Component;
                    var system = roots.SelectMany(r => r != null ? r.GetComponentsInChildren<HumanCannonSystem>(true) : Array.Empty<HumanCannonSystem>()).Single();
                    if (landing == null || system.Station(0)?.Muzzle == null) throw new InvalidOperationException("Missing cannon endpoints.");
                    binding.ConfigureCannon(system.Station(0).Muzzle, landing.transform,
                        roots.Where(r => r != null).SelectMany(r => r.GetComponentsInChildren<HumanCannonFireWall>(true)).ToArray());
                }
                foreach (var existing in roots.Where(r => r != null))
                    if (existing.GetComponent<Canvas>() != null || new[] { "SessionUI", "DebugHud", "TightropeRunDebug", "TightropeCampaignHud" }.Contains(existing.name))
                        existing.transform.SetParent(ui.transform, true);
                EditorUtility.SetDirty(binding);
                Save(scene, path);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(CommonPerformanceUI.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void Save(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
        }
    }
}
#endif
