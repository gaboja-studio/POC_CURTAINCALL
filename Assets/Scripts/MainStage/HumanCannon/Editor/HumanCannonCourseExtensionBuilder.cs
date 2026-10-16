using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>기존 관문 뒤 지상 코스를 각각 30m 늘리고 모든 봉을 위쪽 케이블에 고정해 매단다.</summary>
    public static class HumanCannonCourseExtensionBuilder
    {
        const string Marker = "ExtendedCourse044";
        const string Materials = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";
        const float Extension = 30f;

        [MenuItem("Tools/CurtainCall/인간 대포/044 코스 연장과 천장 봉 추가")]
        public static void ExtendOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            var roots = scene.GetRootGameObjects();
            if (roots.Any(root => root.name == Marker))
                throw new InvalidOperationException("044 연장 코스가 이미 있어 덮어쓰지 않았습니다.");
            var course = roots.Single(root => root.name == "SplitRouteCourse").transform;
            var gateCourse = roots.Single(root => root.name == "PoleGateCourse").transform;
            var finalCourse = roots.Single(root => root.name == "FinalCannonCourse").transform;
            var cushion = roots.Single(root => root.name == "LandingCushion").transform;
            var poleSystem = gateCourse.GetComponent<HumanCannonPoleSystem>();
            if (poleSystem == null) throw new InvalidOperationException("기존 봉 시스템이 없습니다.");
            using var poleData = new SerializedObject(poleSystem);
            var poles = poleData.FindProperty("poles");
            if (poles == null || poles.arraySize != 4)
                throw new InvalidOperationException("기존 일반 봉 2개와 연결 봉 2개가 필요합니다.");
            var existing = Enumerable.Range(0, poles.arraySize)
                .Select(index => poles.GetArrayElementAtIndex(index).objectReferenceValue as HumanCannonPole).ToArray();
            if (existing.Any(pole => pole == null) || existing.Count(pole => pole.LinkedGate == null) != 2)
                throw new InvalidOperationException("기존 봉 연결을 확인하세요.");
            var silver = Material("pole-gate-silver");
            var black = Material("pole-gate-black");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("044 기존 코스 연장과 천장 봉");
            foreach (var root in new[] { course, gateCourse, finalCourse, cushion })
                Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "044 코스 연장");
            try
            {
                var extension = new GameObject(Marker).transform;
                Undo.RegisterCreatedObjectUndo(extension.gameObject, "044 추가 코스");

                // 기존 리프트 구멍과 첫 발사/받기/관문 위치는 보존하고 마지막 바닥 타일만 늘린다.
                foreach (string tile in new[] { "Floor_0_3", "Floor_2_3", "FormerLiftFloor" })
                    Stretch(Require(course, "FirstCourseFloor/" + tile), Extension);
                foreach (string tile in new[] { "Floor_0_1", "Floor_1_1", "Floor_2_1" })
                    Stretch(Require(course, "SecondCourseFloor/" + tile), Extension);

                foreach (string name in new[] { "ShelterLift", "ShelterLiftAccess", "ShelterDeck",
                    "ShelterUnlockSwitch", "ShelterDeparture", "ShelterCannon1", "ShelterCannon2",
                    "ShelterCannon3", "SecondCourseFloor", "SecondCatch", "SecondSectionPoleGate", "SecondSectionFire" })
                    Move(Require(course, name), Extension);
                Move(cushion, Extension * 2);
                Move(finalCourse, Extension * 2);
                Move(Require(course, "FirstSectionFire/Boundary"), Extension);
                Move(Require(course, "SecondSectionFire/Boundary"), Extension);

                // 지상 운반자 측면 출구를 z63..66으로 옮기고 새 접근로와 맞춘다.
                Stretch(Require(gateCourse, "SideFences/LeftFence"), Extension);
                Move(Require(gateCourse, "SideFences/LeftFenceAfterLiftAccess"), Extension);
                Stretch(Require(gateCourse, "SideFences/RightFence"), Extension);
                foreach (string side in new[] { "LeftFence", "RightFence" })
                    Stretch(Require(course, "SecondSectionPoleGate/SideFences/" + side), Extension);
                Move(Require(gateCourse, "ReceivingArea"), Extension);
                Move(Require(course, "SecondSectionPoleGate/ReceivingArea"), Extension);

                foreach (var pole in existing)
                {
                    foreach (string name in new[] { "LeftSupport", "RightSupport" })
                    {
                        var support = pole.transform.Find(name);
                        if (support != null) Undo.DestroyObjectImmediate(support.gameObject);
                    }
                    Ceiling(extension, pole, "ExistingCeiling" + Array.IndexOf(existing, pole), silver, black);
                }

                poles.arraySize = existing.Length + 12;
                for (int section = 0; section < 2; section++)
                    for (int index = 0; index < 6; index++)
                    {
                        var position = new Vector3(0, section == 0 ? 6.5f : 12.5f,
                            (section == 0 ? 38f : 114f) + index * 6f);
                        var pole = CreatePole(extension, "CeilingPole" + (section + 1) + "_" + (index + 1), position, silver);
                        Ceiling(extension, pole, pole.name + "Ceiling", silver, black);
                        poles.GetArrayElementAtIndex(existing.Length + section * 6 + index).objectReferenceValue = pole;
                    }
                poleData.ApplyModifiedPropertiesWithoutUndo();
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("044 연장 코스 저장 실패");
                Undo.CollapseUndoOperations(group);
                Debug.Log("[HumanCannonCourse] 관문 뒤 각각 30m 연장·일반 봉 12개 추가·모든 봉 상부 케이블 고정 저장");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static HumanCannonPole CreatePole(Transform parent, string name, Vector3 position, Material silver)
        {
            var root = Point(parent, name, position, Quaternion.identity);
            var pole = root.gameObject.AddComponent<HumanCannonPole>();
            var grip = Point(root, "Grip", position, Quaternion.identity);
            var bar = Part(root, "Bar", Vector3.zero, new Vector3(0.22f, 2.25f, 0.22f), silver, PrimitiveType.Cylinder);
            bar.localRotation = Quaternion.Euler(0, 0, 90);
            // HumanCannonPole의 기존 반동·잡기 기본값을 그대로 사용한다. 관문 연결은 없다.
            using var data = new SerializedObject(pole);
            data.FindProperty("grip").objectReferenceValue = grip;
            data.ApplyModifiedPropertiesWithoutUndo();
            return pole;
        }

        static void Ceiling(Transform parent, HumanCannonPole pole, string name, Material silver, Material black)
        {
            var ceiling = Point(parent, name, pole.Grip.position, pole.Grip.rotation);
            Part(ceiling, "CeilingBeam", new Vector3(0, 8, 0), new Vector3(5.5f, 0.28f, 0.55f), silver);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(ceiling, side < 0 ? "LeftCable" : "RightCable", new Vector3(side * 2.2f, 4, 0),
                    new Vector3(0.1f, 8, 0.1f), black);
                Part(ceiling, side < 0 ? "LeftAnchor" : "RightAnchor", new Vector3(side * 2.2f, 8, 0),
                    new Vector3(0.35f, 0.4f, 0.7f), black);
            }
        }

        static Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Material material,
            PrimitiveType primitive = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static Transform Point(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.SetPositionAndRotation(position, rotation);
            return point;
        }

        static void Move(Transform target, float distance) => target.position += Vector3.forward * distance;
        static void Stretch(Transform target, float distance)
        {
            target.localPosition += Vector3.forward * (distance * 0.5f);
            target.localScale += Vector3.forward * distance;
        }

        static Transform Require(Transform parent, string path) => parent.Find(path)
            ?? throw new InvalidOperationException("기존 코스 연결이 없습니다: " + parent.name + "/" + path);
        static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat")
            ?? throw new InvalidOperationException("기존 코스 머티리얼이 없습니다: " + name);
    }
}
