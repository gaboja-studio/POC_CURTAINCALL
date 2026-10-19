using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>운송 발판과 출구를 비워 두고 지상 톱 12개를 분산하며, 공중 레일과 색을 구분한다.</summary>
    public static class HumanCannonGroundSawBuilder
    {
        const string RailMaterialPath = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/ground-saw-rail.mat";
        static readonly string[] Suffixes = { "C", "D", "AExtra", "A", "B", "BExtra" };
        static readonly float[] Positions = { 28f, 33.5f, 39f, 50f, 55.5f, 61f };

        [MenuItem("Tools/CurtainCall/인간 대포/지상 톱 분산 및 청록 경로 적용")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var course = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonHazardCourse>(true)).Single();
            var parent = roots.Single(r => r.name == "GeneralHazards044").transform;
            var ground = parent.GetComponentsInChildren<HumanCannonHazard>(true)
                .Where(h => h.Kind == CannonHazardKind.GroundSaw && h.GetComponent<HumanCannonPoleSaw>() == null)
                .ToDictionary(h => h.name);
            if (ground.Count != 8 && ground.Count != 12)
                throw new InvalidOperationException("기존 지상 톱 8개 또는 분산된 지상 톱 12개가 필요합니다.");
            foreach (int section in new[] { 1, 2 })
                foreach (string suffix in new[] { "A", "B", "AExtra", "BExtra" })
                    if (!ground.ContainsKey("GroundSaw" + section + suffix))
                        throw new InvalidOperationException("기존 지상 톱 연결이 없습니다.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(RailMaterialPath);
            if (material == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/route-fire.mat");
                if (source == null) throw new InvalidOperationException("레일 원본 재질이 없습니다.");
                material = new Material(source) { name = "ground-saw-rail" };
                material.SetColor("_BaseColor", new Color(0.15f, 0.85f, 1f, 1f));
                AssetDatabase.CreateAsset(material, RailMaterialPath);
                AssetDatabase.SaveAssetIfDirty(material);
            }

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("지상 톱 분산과 청록 경로");
            Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "지상 톱 배치");
            Undo.RecordObject(course, "지상 톱 등록");
            try
            {
                using var courseData = new SerializedObject(course);
                var registered = courseData.FindProperty("hazards");
                for (int section = 0; section < 2; section++)
                {
                    var template = ground["GroundSaw" + (section + 1) + "A"];
                    for (int i = 0; i < Suffixes.Length; i++)
                    {
                        string name = "GroundSaw" + (section + 1) + Suffixes[i];
                        if (!ground.TryGetValue(name, out var hazard))
                        {
                            hazard = Object.Instantiate(template, parent, false);
                            hazard.name = name;
                            Undo.RegisterCreatedObjectUndo(hazard.gameObject, "지상 톱 추가");
                            using var data = new SerializedObject(hazard);
                            data.FindProperty("phase").floatValue = Mathf.PI * (i == 0 ? 1f / 3f : 4f / 3f);
                            data.ApplyModifiedPropertiesWithoutUndo();
                            registered.arraySize++;
                            registered.GetArrayElementAtIndex(registered.arraySize - 1).objectReferenceValue = hazard;
                        }
                        hazard.transform.position = new Vector3(0, section * 6f + 0.6f, Positions[i] + section * 76f);
                        hazard.ConfigureHorizontalSweep(Vector3.right * (hazard.Travel.x < 0 ? -24f : 24f), 4f);
                        var rail = hazard.GetComponent<LineRenderer>();
                        rail.sharedMaterial = material;
                        rail.startColor = rail.endColor = Color.white;
                        rail.startWidth = rail.endWidth = 0.06f;
                        rail.useWorldSpace = false;
                        rail.positionCount = 2;
                        Vector3 localTravel = hazard.transform.InverseTransformVector(hazard.Travel);
                        rail.SetPositions(new[] { -localTravel, localTravel });
                        hazard.Present(0, 1);
                        EditorUtility.SetDirty(hazard);
                    }
                }
                courseData.ApplyModifiedPropertiesWithoutUndo();
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("지상 톱 배치 저장 실패");
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
    }
}
