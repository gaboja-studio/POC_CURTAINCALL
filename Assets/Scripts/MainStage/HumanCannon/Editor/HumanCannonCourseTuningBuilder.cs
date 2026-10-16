using System;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>승인된 기존 코스의 전략 봉·밀도·바람 가시성을 한 번 적용한다.</summary>
    public static class HumanCannonCourseTuningBuilder
    {
        const string Marker = "StrategicPolesAndDoubleHazards";
        const string WindMaterial = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/route-wind-visible.mat";

        [MenuItem("Tools/CurtainCall/인간 대포/044 전략 봉과 장애물 밀도 적용")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            var roots = scene.GetRootGameObjects();
            var extension = roots.Single(root => root.name == "ExtendedCourse044").transform;
            if (extension.Find(Marker) != null) throw new InvalidOperationException("전략 배치가 이미 적용되어 있습니다.");
            var system = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonPoleSystem>()).Single();
            var hazards = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonHazardCourse>()).Single();
            var trampoline = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonTrampoline>()).Single();
            using var poleData = new SerializedObject(system);
            var array = poleData.FindProperty("poles");
            if (array.arraySize != 16 || hazards.HazardCount != 8)
                throw new InvalidOperationException("봉16·일반함정8인 기존044 코스에 적용하세요.");
            var poles = Enumerable.Range(0, 16).Select(i => (HumanCannonPole)array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            var keep = new[] { poles[0], poles[1], poles[2], poles[3], poles[7], poles[13] };
            float surfaceOffset = trampoline.Surface.y - trampoline.transform.position.y;
            float gripHeight = surfaceOffset + trampoline.UpwardSpeed * trampoline.UpwardSpeed
                / (2 * GameSettings.Base.Character.Gravity) - 0.5f;
            var positions = new[] { new Vector3(-7, gripHeight, 31), new Vector3(0, gripHeight, 44),
                new Vector3(7, 6 + gripHeight, 107), new Vector3(0, 6 + gripHeight, 120),
                new Vector3(7, gripHeight, 61), new Vector3(-7, 6 + gripHeight, 137) };
            var material = VisibleWindMaterial();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("044 전략 봉·함정 두 배·송풍 시각화");
            foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "044 코스 조정");
            try
            {
                HumanCannonSawGateTuningBuilder.ApplyToOpenScene();
                HumanCannonFinalSawBuilder.DoubleExistingFinalSaws();
                for (int i = 4; i < poles.Length; i++)
                {
                    if (keep.Contains(poles[i])) continue;
                    Undo.DestroyObjectImmediate(extension.Find(poles[i].name + "Ceiling").gameObject);
                    Undo.DestroyObjectImmediate(poles[i].gameObject);
                }
                for (int i = 0; i < keep.Length; i++)
                {
                    var pole = keep[i];
                    pole.transform.position += positions[i] - pole.Grip.position;
                    var upper = extension.Find(i < 4 ? "ExistingCeiling" + i : pole.name + "Ceiling");
                    upper.SetPositionAndRotation(pole.Grip.position, pole.Grip.rotation);
                }
                array.arraySize = keep.Length;
                for (int i = 0; i < keep.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                poleData.ApplyModifiedPropertiesWithoutUndo();
                DoubleHazardsAndWinds(hazards, material);
                HumanCannonPoleSwingVisualBuilder.ApplyToOpenScene();
                var marker = new GameObject(Marker); marker.transform.SetParent(extension, false);
                Undo.RegisterCreatedObjectUndo(marker, "044 배치 완료 표시");
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("044 코스 저장 실패");
                Undo.CollapseUndoOperations(undo);
                Debug.Log($"[HumanCannon] 봉6(구간별3) 높이{gripHeight:F2}m, 일반함정16/송풍8, 중앙톱관문·정지해제 저장");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void DoubleHazardsAndWinds(HumanCannonHazardCourse course, Material material)
        {
            using var data = new SerializedObject(course);
            var hazards = data.FindProperty("hazards");
            int count = hazards.arraySize;
            hazards.arraySize *= 2;
            for (int i = 0; i < count; i++)
            {
                var source = (HumanCannonHazard)hazards.GetArrayElementAtIndex(i).objectReferenceValue;
                var clone = UnityEngine.Object.Instantiate(source, source.transform.parent);
                Undo.RegisterCreatedObjectUndo(clone.gameObject, "일반 함정 추가");
                clone.name = source.name + "Extra";
                clone.transform.position += Vector3.forward * 9;
                using var values = new SerializedObject(clone);
                values.FindProperty("phase").floatValue += Mathf.PI * 0.75f;
                values.ApplyModifiedPropertiesWithoutUndo();
                clone.Present(0, 1);
                hazards.GetArrayElementAtIndex(count + i).objectReferenceValue = clone;
            }
            var winds = data.FindProperty("windZones"); count = winds.arraySize;
            winds.arraySize *= 2;
            for (int i = 0; i < count; i++)
            {
                var source = (HumanCannonWindZone)winds.GetArrayElementAtIndex(i).objectReferenceValue;
                var clone = UnityEngine.Object.Instantiate(source, source.transform.parent);
                Undo.RegisterCreatedObjectUndo(clone.gameObject, " 송풍 추가");
                clone.name = source.name + "Extra";
                clone.transform.position += Vector3.forward * 8;
                winds.GetArrayElementAtIndex(count + i).objectReferenceValue = clone;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < winds.arraySize; i++) VisibleWind((HumanCannonWindZone)winds.GetArrayElementAtIndex(i).objectReferenceValue, material);
        }

        static void VisibleWind(HumanCannonWindZone zone, Material material)
        {
            foreach (Transform child in zone.transform)
                if (child.name.StartsWith("Arrow", StringComparison.Ordinal))
                {
                    child.localScale = Vector3.one * 1.8f;
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                }
            var flow = new GameObject("VisibleFlow"); flow.transform.SetParent(zone.transform, false);
            Undo.RegisterCreatedObjectUndo(flow, "바람 흐름 표시");
            var streams = new LineRenderer[16];
            for (int i = 0; i < streams.Length; i++)
            {
                var item = new GameObject("WindStreak" + i); item.transform.SetParent(flow.transform, false);
                var line = item.AddComponent<LineRenderer>(); streams[i] = line;
                line.useWorldSpace = false; line.positionCount = 2;
                line.startWidth = 0.09f; line.endWidth = 0.02f; line.sharedMaterial = material;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            }
            var boundary = new GameObject("WindRange"); boundary.transform.SetParent(zone.transform, false);
            Undo.RegisterCreatedObjectUndo(boundary, "바람 범위 표시");
            var outline = boundary.AddComponent<LineRenderer>();
            var half = zone.Area.size * 0.5f; float y = -half.y + 0.05f;
            outline.useWorldSpace = false; outline.positionCount = 4; outline.loop = true;
            outline.SetPositions(new[] { new Vector3(-half.x, y, -half.z), new Vector3(half.x, y, -half.z),
                new Vector3(half.x, y, half.z), new Vector3(-half.x, y, half.z) });
            outline.startWidth = outline.endWidth = 0.06f; outline.sharedMaterial = material;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            using var data = new SerializedObject(zone); var field = data.FindProperty("streams"); field.arraySize = streams.Length;
            for (int i = 0; i < streams.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = streams[i];
            data.ApplyModifiedPropertiesWithoutUndo(); zone.PresentFlow(0);
        }

        static Material VisibleWindMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(WindMaterial);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("바람 표시용 Unlit 셰이더가 없습니다.");
            var material = new Material(shader) { name = "route-wind-visible", color = new Color(0.05f, 0.95f, 1) };
            AssetDatabase.CreateAsset(material, WindMaterial);
            return material;
        }
    }
}
