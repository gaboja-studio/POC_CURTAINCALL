using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>연장된 기존 코스에만 044 함정/송풍을 추가한다. 별도 연습 코스는 생성하지 않는다.</summary>
    public static class HumanCannonHazardBuilder
    {
        const string MaterialFolder = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";
        [MenuItem("Tools/CurtainCall/인간 대포/044 일반 함정과 송풍 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            if (!scene.GetRootGameObjects().Any(root => root.name == "ExtendedCourse044"))
                throw new InvalidOperationException("044 코스 연장을 먼저 적용하세요.");
            if (scene.GetRootGameObjects().Any(root => root.GetComponent<HumanCannonHazardCourse>() != null))
                throw new InvalidOperationException("일반 함정이 이미 배치되어 덮어쓰지 않았습니다.");
            var silver = Material("pole-gate-silver"); var black = Material("pole-gate-black");
            var orange = Material("route-fire"); var cyan = Material("pole-gate-receiving");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("044 일반 함정/송풍");
            try
            {
                var root = new GameObject("GeneralHazards044");
                Undo.RegisterCreatedObjectUndo(root, "044 일반 함정");
                var system = root.AddComponent<HumanCannonHazardCourse>();
                var hazards = new List<HumanCannonHazard>();
                var winds = new List<HumanCannonWindZone>();
                for (int section = 0; section < 2; section++)
                {
                    float floor = section == 0 ? 0 : 6;
                    float airZ = section == 0 ? 15 : 90;
                    float extensionZ = section == 0 ? 50 : 126;
                    hazards.Add(Hazard(root.transform, "AirMace" + (section + 1), CannonHazardKind.SpikeMace,
                        new Vector3(0, section == 0 ? 13.5f : 16, airZ), new Vector3(3.5f, 0, 0), 8, 0, silver, black, orange));
                    hazards.Add(Hazard(root.transform, "RotatingBlades" + (section + 1), CannonHazardKind.RotatingBlades,
                        new Vector3(0, floor + 7.5f, extensionZ - 3), Vector3.zero, 8, 0, silver, black, orange));
                    // 교차 운동의 중앙 외에도 x±8 부근에 트램폴린 운반이 가능한 통로를 남긴다.
                    hazards.Add(Hazard(root.transform, "GroundSaw" + (section + 1) + "A", CannonHazardKind.GroundSaw,
                        new Vector3(0, floor + 0.6f, extensionZ), new Vector3(5, 0, 2), 12, 0, silver, black, orange));
                    hazards.Add(Hazard(root.transform, "GroundSaw" + (section + 1) + "B", CannonHazardKind.GroundSaw,
                        new Vector3(0, floor + 0.6f, extensionZ + 2), new Vector3(-5, 0, 2), 12, Mathf.PI * 0.5f, silver, black, orange));
                    winds.Add(Wind(root.transform, "AirWind" + (section + 1),
                        new Vector3(0, floor + 10, section == 0 ? 19 : 96), new Vector3(28, 14, 6), false, section == 0 ? 1 : -1, cyan, black));
                    winds.Add(Wind(root.transform, "GroundWind" + (section + 1),
                        new Vector3(0, floor + 1.5f, extensionZ + 7), new Vector3(28, 3, 6), true, section == 0 ? -1 : 1, cyan, black));
                }
                using var data = new SerializedObject(system);
                References(data, "hazards", hazards.Cast<UnityEngine.Object>().ToArray());
                References(data, "windZones", winds.Cast<UnityEngine.Object>().ToArray());
                data.FindProperty("difficulty").intValue = 1;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("044 일반 함정 저장 실패");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[HumanCannonHazards] 기존 코스에 가시추2·회전날개2·지상톱4·송풍4 저장, 난도1 기본 시험값");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static HumanCannonHazard Hazard(Transform parent, string name, CannonHazardKind kind, Vector3 position,
            Vector3 travel, float period, float phase, Material silver, Material black, Material orange)
        {
            var root = Point(parent, name, position);
            var hazard = root.gameObject.AddComponent<HumanCannonHazard>();
            var view = Point(root, "Visual", position);
            const float radius = 0.65f;
            if (kind == CannonHazardKind.SpikeMace)
            {
                Part(view, "Core", Vector3.zero, Vector3.one * 0.9f, silver, PrimitiveType.Sphere);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    var tooth = Part(view, "Spike" + i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.5f,
                        new Vector3(0.22f, 0.22f, 0.22f), orange);
                    tooth.localRotation = Quaternion.Euler(0, 0, i * 45 + 45);
                }
            }
            else if (kind == CannonHazardKind.RotatingBlades)
            {
                Part(view, "BladeA", Vector3.zero, new Vector3(2.6f, 0.24f, 0.24f), silver);
                Part(view, "BladeB", Vector3.zero, new Vector3(0.24f, 2.6f, 0.24f), silver);
                Part(view, "Hub", Vector3.zero, Vector3.one * 0.35f, orange, PrimitiveType.Sphere);
            }
            else
            {
                var disc = Part(view, "Disc", Vector3.zero, new Vector3(radius * 1.8f, 0.12f, radius * 1.8f), silver, PrimitiveType.Cylinder);
                disc.localRotation = Quaternion.Euler(90, 0, 0);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    var tooth = Part(view, "Tooth" + i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.53f,
                        new Vector3(0.15f, 0.15f, 0.22f), black);
                    tooth.localRotation = Quaternion.Euler(0, 0, i * 45 + 45);
                }
            }
            if (travel.sqrMagnitude > 0)
            {
                var rail = root.gameObject.AddComponent<LineRenderer>();
                rail.useWorldSpace = false; rail.positionCount = 2;
                rail.SetPositions(new[] { -travel, travel });
                rail.startWidth = rail.endWidth = 0.04f; rail.sharedMaterial = orange;
            }
            using var data = new SerializedObject(hazard);
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.FindProperty("visual").objectReferenceValue = view;
            data.FindProperty("travel").vector3Value = travel;
            data.FindProperty("periodSeconds").floatValue = period;
            data.FindProperty("phase").floatValue = phase;
            data.FindProperty("radius").floatValue = radius;
            data.ApplyModifiedPropertiesWithoutUndo();
            hazard.Present(0, 1);
            return hazard;
        }

        static HumanCannonWindZone Wind(Transform parent, string name, Vector3 position, Vector3 size, bool ground,
            int direction, Material cyan, Material black)
        {
            var root = Point(parent, name, position);
            var zone = root.gameObject.AddComponent<HumanCannonWindZone>();
            var area = root.gameObject.AddComponent<BoxCollider>(); area.isTrigger = true; area.size = size;
            // 화면에서 읽을 수 있는 수평 화살표. 실제 힘은 범위 안 플레이어에만 적용한다.
            for (int i = -1; i <= 1; i++)
            {
                var arrow = Point(root, "Arrow" + i, position + new Vector3(i * 7, 0, 0));
                arrow.localRotation = Quaternion.Euler(0, direction > 0 ? 0 : 180, 0);
                Part(arrow, "Shaft", Vector3.zero, new Vector3(2, 0.1f, 0.1f), cyan);
                foreach (int side in new[] { -1, 1 })
                {
                    var tip = Part(arrow, "Tip" + side, new Vector3(0.75f, 0, side * 0.3f), new Vector3(0.8f, 0.1f, 0.1f), cyan);
                    tip.localRotation = Quaternion.Euler(0, side * 45, 0);
                }
            }
            Part(root, "BlowerHousing", new Vector3(-direction * 16, 0, 0), new Vector3(0.8f, 1.5f, 1.5f), black);
            using var data = new SerializedObject(zone);
            data.FindProperty("area").objectReferenceValue = area;
            data.FindProperty("direction").vector3Value = Vector3.right * direction;
            data.FindProperty("affectsGround").boolValue = ground;
            data.FindProperty("affectsAir").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform; point.SetParent(parent, false); point.position = position; return point;
        }
        static Transform Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
            PrimitiveType primitive = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(primitive); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material; return part.transform;
        }
        static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat")
            ?? throw new InvalidOperationException("머티리얼 없음: " + name);
        static void References(SerializedObject data, string field, UnityEngine.Object[] values)
        {
            var property = data.FindProperty(field); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
