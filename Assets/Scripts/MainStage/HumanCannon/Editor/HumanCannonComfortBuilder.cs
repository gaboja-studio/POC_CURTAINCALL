using System;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class HumanCannonComfortBuilder
    {
        [MenuItem("Tools/CurtainCall/인간 대포/일반 점프 1.2m 설정 적용")]
        public static void ApplyJumpSettings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play 종료 후 실행하세요.");
            const string path = "Assets/Resources/Settings/GameSettings/Tricks/HumanCannonSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<HumanCannonSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<HumanCannonSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            Undo.RecordObject(settings, "인간대포 일반 점프 높이");
            using (var data = new SerializedObject(settings))
            {
                data.FindProperty("jumpHeight").floatValue = 1.2f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var game = GameSettings.Current;
            Undo.RecordObject(game, "인간대포 세팅 연결");
            using (var data = new SerializedObject(game))
            {
                data.FindProperty("humanCannon").objectReferenceValue = settings;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.SaveAssetIfDirty(game);
        }

        [MenuItem("Tools/CurtainCall/인간 대포/공중 발밑 표시와 불꽃 30초 대기 적용")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var enhancement = roots.Single(r => r.name == "HumanCannonEnhancement");
            var cannon = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonSystem>(true)).Single();
            var floorRoots = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "FirstCourseFloor" || t.name == "SecondCourseFloor").ToArray();
            if (floorRoots.Length != 2) throw new InvalidOperationException("첫/두 번째 코스 바닥이 필요합니다.");
            var courseFloors = floorRoots.SelectMany(t => t.GetComponentsInChildren<Collider>())
                .Where(c => c.enabled && !c.isTrigger).ToArray();
            var fires = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonFireWall>(true)).ToArray();
            if (fires.Length != 2) throw new InvalidOperationException("기존 불꽃 구간 2개가 필요합니다.");
            var shader = Shader.Find("CurtainCall/HumanCannon/GroundMarker");
            if (shader == null) throw new InvalidOperationException("발밑 원형 표시 셰이더가 필요합니다.");
            const string materialPath = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/human-cannon-ground-marker.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "human-cannon-ground-marker" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("인간 대포 발밑 표시와 불꽃 30초 대기");
            try
            {
                Undo.RecordObject(cannon, "부상 속도 최소 80% 저장");
                using (var data = new SerializedObject(cannon))
                {
                    foreach (string field in new[] { "oneLegMove", "oneLegAir", "twoLegAir" })
                    {
                        var value = data.FindProperty("injuries").FindPropertyRelative(field);
                        value.floatValue = Mathf.Max(0.8f, value.floatValue);
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var markers = enhancement.GetComponent<HumanCannonGroundMarkers>()
                    ?? Undo.AddComponent<HumanCannonGroundMarkers>(enhancement);
                Undo.RecordObject(markers, "발밑 표시 재질 연결");
                using (var data = new SerializedObject(markers))
                {
                    data.FindProperty("markerMaterial").objectReferenceValue = material;
                    var floors = data.FindProperty("courseFloors");
                    floors.arraySize = courseFloors.Length;
                    for (int i = 0; i < courseFloors.Length; i++) floors.GetArrayElementAtIndex(i).objectReferenceValue = courseFloors[i];
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var fire in fires)
                {
                    Undo.RecordObject(fire, "불꽃 재활성화와 시작 대기 연장");
                    using var data = new SerializedObject(fire);
                    data.FindProperty("available").boolValue = true;
                    data.FindProperty("warningSeconds").floatValue = 30f;
                    data.FindProperty("speed").floatValue = 0.7f;
                    var visual = (Transform)data.FindProperty("visual").objectReferenceValue;
                    Undo.RecordObject(visual.gameObject, "불꽃 표시 숨김");
                    visual.gameObject.SetActive(false);
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("편의성 변경 씬 저장 실패");
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
    }
}
