using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>042 리프트 1인 규칙·첫 출발의 안전한 실패/복귀 경로를 기존 씬에 한 번 적용한다.</summary>
    public static class HumanCannonLiftTrapBuilder
    {
        const string Materials = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";
        public const float DepartureHeight = 4f;
        public const float RecoveryHeight = -4f;

        [MenuItem("Tools/CurtainCall/인간 대포/042 리프트 양문과 안전 복귀 적용")]
        public static void UpgradeOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var course = roots.Single(g => g.name == "SplitRouteCourse").transform;
            if (course.Find("LiftSafetyLesson") != null) throw new InvalidOperationException("안전 복귀 코스가 이미 있어 덮어쓰지 않았습니다.");
            var transports = course.GetComponentsInChildren<HumanCannonTransport>();
            if (transports.Length != 3) throw new InvalidOperationException("042 리프트 3대가 필요합니다.");
            var blue = AssetDatabase.LoadAssetAtPath<Material>(Materials + "route-transport.mat");
            var green = AssetDatabase.LoadAssetAtPath<Material>(Materials + "route-shelter.mat");
            var yellow = AssetDatabase.LoadAssetAtPath<Material>(Materials + "route-switch.mat");
            var floor = AssetDatabase.LoadAssetAtPath<Material>(Materials + "route-floor.mat");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("042 리프트 1인 트랩과 안전 복귀");
            foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "042 리프트 수정");
            try
            {
                var lesson = new GameObject("LiftSafetyLesson").transform;
                lesson.SetParent(course, false);
                foreach (var transport in transports) AddDoors(transport, blue, yellow);
                var departure = transports.Single(t => t.name == "DepartureTransport");
                departure.transform.Find("Start").position += Vector3.up * DepartureHeight;
                departure.Present(0);
                roots.Single(g => g.name == "SpawnPoints").transform.position += Vector3.up * DepartureHeight;
                var trampoline = roots.SelectMany(g => g.GetComponentsInChildren<HumanCannonTrampoline>()).Single();
                trampoline.transform.position += Vector3.up * DepartureHeight;
                var system = roots.SelectMany(g => g.GetComponentsInChildren<HumanCannonSystem>()).Single();
                Vector3 target = course.Find("FirstCatch").position;
                for (int i = 0; i < 3; i++)
                {
                    var station = system.Station(i);
                    station.transform.position += Vector3.up * DepartureHeight;
                    Aim(station, target);
                }
                // 발판 밑 지면까지 비워야 문 개방 시 실제로 떨어진다. 비행/지상 코스는 도보로 이어 붙이지 않는다.
                TileSurface(roots.Single(g => g.name == "Ground"), new Rect(-15, -11, 30, 18), DepartureHeight,
                    new[] { Hole(-1, 7) }, floor, true);
                TileSurface(course.Find("FirstCourseFloor").gameObject, new Rect(-15, 16, 30, 26), 0,
                    new[] { Hole(16, 26), Hole(35, 43) }, floor, false);
                TileSurface(course.Find("ShelterDeck").gameObject, new Rect(-15, 42, 30, 12), 4,
                    new[] { Hole(35, 43), Hole(47, 55) }, green, true);
                TileSurface(course.Find("SecondCourseFloor").gameObject, new Rect(-15, 65, 30, 23), 4,
                    new[] { Hole(64, 72) }, floor, false);

                // 첫 리프트 경로 전체 아래만 받친다. 출발 4m / 첫 코스 0m / 안전 바닥 -4m.
                SafePart(lesson, "RecoveryFloor", new Vector3(-9, RecoveryHeight - 0.25f, 12.5f), new Vector3(12, 0.5f, 31), green);
                SafePart(lesson, "StairBottom", new Vector3(-19, RecoveryHeight - 0.25f, 7), new Vector3(8, 0.5f, 4), green);
                SafePart(lesson, "StairTop", new Vector3(-17, DepartureHeight - 0.25f, -12), new Vector3(12, 0.5f, 2), floor);
                var stairs = new GameObject("ReturnStairs").transform;
                stairs.SetParent(lesson, false);
                const int steps = 40;
                for (int i = 0; i < steps; i++)
                {
                    float rise = (DepartureHeight - RecoveryHeight) * (i + 1) / steps;
                    SafePart(stairs, "Step" + (i + 1).ToString("00"),
                        new Vector3(-19, RecoveryHeight + rise * 0.5f, 4.8f - i * 0.4f), new Vector3(8, rise, 0.4f), floor);
                }
                Part(lesson, "RecoveryDirection", new Vector3(-13.5f, RecoveryHeight + 0.03f, 6), new Vector3(2, 0.06f, 0.3f), yellow, false);
                // 기존 거리 표시는 출발 높이와 맞지 않으므로 출발 무대의 표시만 함께 올린다.
                var markers = roots.FirstOrDefault(g => g.name == "DistanceMarkers");
                if (markers != null)
                    foreach (Transform child in markers.transform)
                        if (child.position.z <= 7) child.position += Vector3.up * DepartureHeight;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("리프트 씬 저장 실패");
                Undo.CollapseUndoOperations(group);
                Debug.Log("[HumanCannonRoute] 1인 리프트 3대·대칭 양문·높은 출발대·하부 안전 바닥·복귀 계단 저장 완료");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static Rect Hole(float near, float far) => new(-12, near, 6, far - near);

        static void AddDoors(HumanCannonTransport transport, Material blue, Material yellow)
        {
            var platform = transport.Platform;
            var old = platform.Find("Deck");
            if (old != null) old.gameObject.SetActive(false);
            var panels = new BoxCollider[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                var hinge = new GameObject(i == 0 ? "LeftHinge" : "RightHinge").transform;
                hinge.SetParent(platform, false);
                hinge.localPosition = new Vector3(side * 3, 0, 0);
                panels[i] = Part(hinge, "FloorPanel", new Vector3(-side * 1.5f, -0.15f, 0), new Vector3(3, 0.3f, 8), blue, true).GetComponent<BoxCollider>();
                SetObject(transport, i == 0 ? "leftHinge" : "rightHinge", hinge);
                Part(hinge, "CenterStripe", new Vector3(-side * 2.9f, 0.015f, 0), new Vector3(0.12f, 0.03f, 8), yellow, false);
            }
            SetArray(transport, "trapColliders", panels);
        }

        static void TileSurface(GameObject root, Rect area, float height, Rect[] holes, Material material, bool safe)
        {
            root.GetComponent<Collider>().enabled = false;
            root.GetComponent<Renderer>().enabled = false;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var x = new[] { area.xMin, area.xMax }.Concat(holes.SelectMany(h => new[] { h.xMin, h.xMax }))
                .Where(v => v >= area.xMin && v <= area.xMax).Distinct().OrderBy(v => v).ToArray();
            var z = new[] { area.yMin, area.yMax }.Concat(holes.SelectMany(h => new[] { h.yMin, h.yMax }))
                .Where(v => v >= area.yMin && v <= area.yMax).Distinct().OrderBy(v => v).ToArray();
            var tiles = new List<BoxCollider>();
            for (int i = 0; i < x.Length - 1; i++)
                for (int j = 0; j < z.Length - 1; j++)
                {
                    var center = new Vector2((x[i] + x[i + 1]) * 0.5f, (z[j] + z[j + 1]) * 0.5f);
                    if (holes.Any(h => h.Contains(center))) continue;
                    tiles.Add(Part(root.transform, $"Floor_{i}_{j}", new Vector3(center.x, height - 0.25f, center.y),
                        new Vector3(x[i + 1] - x[i], 0.5f, z[j + 1] - z[j]), material, true).GetComponent<BoxCollider>());
                }
            if (safe)
            {
                var zone = root.GetComponent<HumanCannonLandingZone>() ?? root.AddComponent<HumanCannonLandingZone>();
                SetArray(zone, "additionalSurfaces", tiles.ToArray());
            }
        }

        static void Aim(HumanCannonStation station, Vector3 target)
        {
            float gravity = GameSettings.Base.Character.Gravity;
            float up = station.LaunchVelocity.y;
            float height = station.Muzzle.position.y - target.y - HumanCannonTrampolineBuilder.SurfaceHeight;
            float time = (up + Mathf.Sqrt(up * up + 2 * gravity * height)) / gravity;
            var data = new SerializedObject(station);
            data.FindProperty("forwardSpeed").floatValue = Vector3.ProjectOnPlane(target - station.Muzzle.position, Vector3.up).magnitude / time;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject SafePart(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var item = Part(parent, name, at, size, material, true);
            item.AddComponent<HumanCannonLandingZone>();
            return item;
        }

        static GameObject Part(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool solid)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = at;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        static void SetObject(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(owner);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray<T>(UnityEngine.Object owner, string field, T[] values) where T : UnityEngine.Object
        {
            var data = new SerializedObject(owner);
            var array = data.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
