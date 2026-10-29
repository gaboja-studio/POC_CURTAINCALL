using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.LevelPreview;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.UI.PerformanceHudV1;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string OriginalReview = "A03_OriginalMechanics";

        [MenuItem("Tools/CurtainCall/탈출로/A03 기존 묘기 연결 (현재 A 씬)")]
        public static void InstallOriginalA03()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("현재 A 씬의 편집 모드에서 실행하세요.");
            if (scene.GetRootGameObjects().Any(g => g.name == OriginalReview))
                throw new InvalidOperationException("기존 A03 연결을 직접 편집하세요.");
            var system = UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>();
            var camera = Camera.main;
            var follow = AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefab);
            if (system == null || camera == null || follow == null) throw new InvalidOperationException("기존 대포와 카메라가 필요합니다.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A03 original mechanics in current scene");
            foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "EscapeSoloReview_A" || g.name == "PlaySetup"))
            { Undo.RecordObject(root, "Disable superseded preview"); root.SetActive(false); }
            var hazards = GameObject.Find(RootName)?.transform.Find("25_FinaleHazards_A");
            if (hazards != null) { Undo.RecordObject(hazards.gameObject, "Disable temporary hazard logic"); hazards.gameObject.SetActive(false); }
            foreach (var old in camera.GetComponents<Behaviour>().Where(b => b is EscapeRouteFlyCamera || b is LevelPreviewCamera))
            { Undo.RecordObject(old, "Disable old preview camera"); old.enabled = false; }
            Undo.RecordObject(system, "Restore original cannon"); system.enabled = true;
            if (camera.GetComponent<CinemachineBrain>() == null) Undo.AddComponent<CinemachineBrain>(camera.gameObject);
            Undo.AddComponent<EscapeInspectionCamera>(camera.gameObject);

            var rootObject = new GameObject(OriginalReview);
            Undo.RegisterCreatedObjectUndo(rootObject, "A03 original session");
            var followInstance = (GameObject)PrefabUtility.InstantiatePrefab(follow, rootObject.transform);
            Undo.RegisterCreatedObjectUndo(followInstance, "Shared player camera");
            rootObject.AddComponent<EscapeCannonReviewHud>();
            foreach (var spectator in followInstance.GetComponentsInChildren<CurtainCall.Cameras.CameraSpectator>(true))
                spectator.enabled = false;
            var roster = rootObject.AddComponent<PlayerSpawnPoints>();
            var rosterData = new SerializedObject(roster);
            var points = rosterData.FindProperty("points"); points.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var point = new GameObject("Player" + (i + 1)).transform;
                point.SetParent(rootObject.transform);
                point.SetPositionAndRotation(new Vector3(26, .05f, 73.5f + i), Quaternion.Euler(0, 90, 0));
                points.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            rosterData.ApplyModifiedPropertiesWithoutUndo();

            var ropeRoot = new GameObject("03_OriginalTightrope");
            ropeRoot.transform.SetParent(rootObject.transform);
            Vector3 from = new(51, -6, 77), to = new(64, -6, 82);
            float length = Vector3.Distance(from, to);
            ropeRoot.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
            var supportObject = new GameObject("RopeSupport_0");
            supportObject.transform.SetParent(ropeRoot.transform, false);
            var support = supportObject.AddComponent<BoxCollider>();
            var settings = GameSettings.Tightrope.Course;
            support.center = new Vector3(0, -settings.RopeThickness * .5f, length * .5f);
            support.size = new Vector3(settings.RopeWalkWidth, settings.RopeThickness, length);
            var course = ropeRoot.AddComponent<TightropeCourse>();
            var data = new SerializedObject(course);
            data.FindProperty("useSceneGeometry").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            // Copy the original shape first; only the authored lane/length/gaps differ.
            var shapeField = typeof(TightropeCourse).GetField("sceneShape", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            JsonUtility.FromJsonOverwrite(settings.ToJson(), shapeField.GetValue(course));
            data.Update();
            var shape = data.FindProperty("sceneShape");
            shape.FindPropertyRelative("laneCount").intValue = 1;
            shape.FindPropertyRelative("finishDistance").floatValue = length;
            shape.FindPropertyRelative("laneEndDistances").arraySize = 0;
            shape.FindPropertyRelative("laneChangeZones").arraySize = 0;
            shape.FindPropertyRelative("swingGapEnabled").boolValue = false;
            shape.FindPropertyRelative("relayGapEnabled").boolValue = false;
            shape.FindPropertyRelative("trapezeEnabled").boolValue = false;
            var ropes = data.FindProperty("sceneRopes"); ropes.arraySize = 1;
            ropes.GetArrayElementAtIndex(0).objectReferenceValue = support;
            data.ApplyModifiedPropertiesWithoutUndo();
            ropeRoot.AddComponent<CourseControlSwitcher>();
            ropeRoot.AddComponent<TightropeRun>();
            ropeRoot.AddComponent<TightropeHangSystem>();
            ropeRoot.AddComponent<TightropeRopeSway>();
            ropeRoot.AddComponent<TightropeCatchSystem>();
            ropeRoot.AddComponent<PiggybackSystem>();
            ropeRoot.AddComponent<PiggybackFollow>();
            ropeRoot.AddComponent<TightropeBalancePoseView>();
            InstallOriginalBalanceDisplay(rootObject.transform);
            course.Rebuild();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
        }

        public static void InstallOriginalBalanceDisplay(Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/PerformanceHud/PerformanceHud.prefab");
            if (asset == null) throw new InvalidOperationException("원본 묘기 균형 UI 프리팹이 없습니다.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Original tightrope balance display");
            instance.name = "OriginalRopeBalance";
            var hud = instance.GetComponent<PerformanceHUDView>();
            hud.Timer.gameObject.SetActive(false); hud.Progress.gameObject.SetActive(false);
            hud.Balance.SetContext(false, false);
            var display = instance.AddComponent<EscapeRopeBalanceDisplay>();
            var data = new SerializedObject(display);
            data.FindProperty("view").objectReferenceValue = hud;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
