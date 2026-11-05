using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.UI.SceneIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>Copies native devices including scene-only visual overrides. Creates no new art or materials.</summary>
    public static class RehearsalCannonSourceBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
        public const string PrefabPath = "Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/RehearsalCourseFromPerformance.prefab";

        [MenuItem("CurtainCall/Waiting Room/Copy cannon rehearsal source from performance")]
        public static void CopySource()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Copy the source outside Play mode.");
            var opened = SceneManager.GetSceneByPath(ScenePath);
            bool close = !opened.isLoaded;
            if (close) opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            GameObject root = null;
            Scene commonUi = default;
            try
            {
                var source = Find<HumanCannonSystem>(opened);
                var sourceRoute = Find<HumanCannonRouteSystem>(opened);
                var sourceTrampoline = Find<HumanCannonTrampoline>(opened);
                // Read the serialized native order: source systems need not be enabled in Edit mode.
                var stationProperty = new SerializedObject(source).FindProperty("stations");
                var sourceStation = (HumanCannonStation)stationProperty.GetArrayElementAtIndex(0).objectReferenceValue;
                var sourceTransport = (HumanCannonTransport)new SerializedObject(sourceRoute).FindProperty("transports").GetArrayElementAtIndex(0).objectReferenceValue;
                root = new GameObject("RehearsalCannonCourse"); root.SetActive(false);
                var course = root.AddComponent<RehearsalCannonCourse>();
                course.Cannon = root.AddComponent<HumanCannonSystem>(); EditorUtility.CopySerialized(source, course.Cannon);
                course.Route = root.AddComponent<HumanCannonRouteSystem>(); EditorUtility.CopySerialized(sourceRoute, course.Route);
                T Copy<T>(T component, string name) where T : Component
                {
                    var clone = Object.Instantiate(component.gameObject, root.transform, false);
                    clone.name = name; clone.transform.localScale = component.transform.lossyScale;
                    return clone.GetComponent<T>();
                }
                course.Stations = Enumerable.Range(0, 4).Select(i => Copy(sourceStation, "Cannon_" + (i + 1))).ToArray();
                course.Trampoline = Copy(sourceTrampoline, "OriginalRoundTrampoline");
                course.Transport = Copy(sourceTransport, "OriginalDepartureTransport");
                commonUi = EditorSceneManager.OpenPreviewScene(CommonPerformanceUI.ScenePath);
                var sourceUi = Find<CommonPerformanceUI>(commonUi);
                course.Guide = Copy(sourceUi.Guide.GetComponentInParent<Canvas>(), "OriginalPerformanceGuides")
                    .GetComponentInChildren<CurtainCall.UI.GuidesV1.GuideView>(true);
                course.Tooltip = Copy(sourceUi.Tooltip.GetComponentInParent<Canvas>(), "OriginalPerformanceTooltips")
                    .GetComponentInChildren<CurtainCall.UI.TooltipsV1.TooltipView>(true);
                var sourceFonts = sourceUi.GetComponent<UiRuntimeFontFallback>();
                if (sourceFonts == null) throw new InvalidOperationException("The performance's contextual font policy is missing.");
                EditorUtility.CopySerialized(sourceFonts, root.AddComponent<UiRuntimeFontFallback>());
                // The native contextual binder must not replace the waiting room's shared UI registration.
                root.AddComponent<PerformanceSceneBinding>().enabled = false;
                course.DeviceUi = root.AddComponent<HumanCannonUiBinding>();
                var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Objects/ETCs/PerformanceArtKit/Prepared/FloorModulePrepared.prefab");
                Transform Floor(string name, out Collider collider, bool safe = true)
                {
                    var floor = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab, root.transform);
                    floor.name = name;
                    collider = floor.GetComponentInChildren<BoxCollider>(true);
                    if (collider == null) throw new InvalidOperationException("The original floor collider is missing.");
                    if (safe && collider.GetComponent<HumanCannonLandingZone>() == null) collider.gameObject.AddComponent<HumanCannonLandingZone>();
                    if (!safe) foreach (var zone in floor.GetComponentsInChildren<HumanCannonLandingZone>(true)) Object.DestroyImmediate(zone);
                    return floor.transform;
                }
                course.StartDeck = Floor("StartDeck", out course.StartFloor);
                course.MiddleDeck = Floor("MiddleDeck", out course.MiddleFloor);
                // A missed catch on the lower floor is a native bad landing: practice converts the injury to recovery.
                course.LowerDeck = Floor("LowerDeck", out course.LowerFloor, false);
                course.FinishDeck = Floor("FinishDeck", out course.FinishFloor);
                // Remove scene references from the copied systems before saving a self-contained prefab.
                var cannon = new SerializedObject(course.Cannon);
                var stations = cannon.FindProperty("stations"); stations.arraySize = 4;
                for (int i = 0; i < 4; i++) stations.GetArrayElementAtIndex(i).objectReferenceValue = course.Stations[i];
                cannon.FindProperty("flightCoordinateSpace").objectReferenceValue = null;
                cannon.ApplyModifiedPropertiesWithoutUndo();
                var route = new SerializedObject(course.Route);
                var transports = route.FindProperty("transports"); transports.arraySize = 1;
                transports.GetArrayElementAtIndex(0).objectReferenceValue = course.Transport;
                route.FindProperty("shelter").objectReferenceValue = null;
                route.FindProperty("fires").arraySize = 0;
                route.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CannonRehearsal] Copied Start performance devices and scene art overrides: " + PrefabPath);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (commonUi.IsValid()) EditorSceneManager.ClosePreviewScene(commonUi);
                if (close) EditorSceneManager.CloseScene(opened, true);
            }
        }

        static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    }
}
