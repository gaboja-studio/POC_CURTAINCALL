using System;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>저장된 041 테스트 씬을 042의 두 구간 코스로 확장한다. 재실행으로 기존 코스를 덮어쓰지 않는다.</summary>
    public static class HumanCannonRouteBuilder
    {
        const string Materials = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";

        [MenuItem("Tools/CurtainCall/인간 대포/042 분리 코스와 쉼터 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            var roots = scene.GetRootGameObjects();
            if (roots.Any(g => g.GetComponentInChildren<HumanCannonRouteSystem>(true) != null || g.name == "SplitRouteCourse"))
                throw new InvalidOperationException("기존 042 코스가 있어 덮어쓰지 않았습니다.");
            var system = roots.SelectMany(g => g.GetComponentsInChildren<HumanCannonSystem>(true)).Single();
            var station = roots.SelectMany(g => g.GetComponentsInChildren<HumanCannonStation>(true)).Single();
            var trampoline = roots.SelectMany(g => g.GetComponentsInChildren<HumanCannonTrampoline>(true)).Single();
            var poleSystem = roots.SelectMany(g => g.GetComponentsInChildren<HumanCannonPoleSystem>(true)).Single();
            var ground = roots.Single(g => g.name == "Ground");
            var cushion = roots.Single(g => g.name == "LandingCushion");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit 셰이더가 없습니다.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("042 분리 코스와 쉼터");
            foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "042 기존 씬 연결");
            try
            {
                var floor = Material("route-floor", new Color(0.2f, 0.25f, 0.32f), shader);
                var blue = Material("route-transport", new Color(0.1f, 0.55f, 0.95f), shader);
                var yellow = Material("route-switch", new Color(1f, 0.8f, 0.1f), shader);
                var green = Material("route-shelter", new Color(0.13f, 0.54f, 0.4f), shader);
                var fire = Material("route-fire", new Color(1f, 0.14f, 0.02f, 0.32f), shader, true);
                var root = new GameObject("SplitRouteCourse");
                Undo.RegisterCreatedObjectUndo(root, "042 코스 추가");
                var route = root.AddComponent<HumanCannonRouteSystem>();
                Vector3 firstCatch = trampoline.transform.position;

                // 원래 041 코스와 봉/톱 위치는 보존하고 출발 무대와 바닥만 분리한다.
                ground.transform.position = new Vector3(0, -0.5f, -2f);
                ground.transform.localScale = new Vector3(30f, 1f, 18f); // z -11..7
                ground.AddComponent<HumanCannonLandingZone>();
                Part(root.transform, "FirstCourseFloor", new Vector3(0, -0.5f, 29f), new Vector3(30, 1, 26), floor, true); // z 16..42
                MarkReceiving(root.transform, "FirstCatch", firstCatch, green);

                var departure = Transport(root.transform, "DepartureTransport", new Vector3(-9, 0, 3), new Vector3(-9, 0, 22), 5f, blue);
                var lift = Transport(root.transform, "ShelterLift", new Vector3(-9, 0, 39), new Vector3(-9, 4, 39), 3f, blue);
                var restart = Transport(root.transform, "ShelterDeparture", new Vector3(-9, 4, 51), new Vector3(-9, 4, 68), 5f, blue);
                trampoline.transform.SetPositionAndRotation(new Vector3(-9, 0, 2.8f), Quaternion.Euler(0, -90, 0));

                var shelter = Part(root.transform, "ShelterDeck", new Vector3(0, 3.85f, 48f), new Vector3(30, 0.3f, 12), green, true)
                    .AddComponent<HumanCannonLandingZone>(); // z 42..54, top y4
                var switchPoint = Point(root.transform, "ShelterUnlockSwitch", new Vector3(-9, 4, 44));
                Part(switchPoint, "LowSwitch", new Vector3(0, 0.45f, 0), new Vector3(0.65f, 0.9f, 0.4f), yellow);
                Part(root.transform, "SecondCourseFloor", new Vector3(0, 3.5f, 76.5f), new Vector3(30, 1, 23), floor, true); // z 65..88

                Vector3 secondCatch = firstCatch + new Vector3(0, 4, 46);
                MarkReceiving(root.transform, "SecondCatch", secondCatch, green);
                var stations = new HumanCannonStation[6];
                stations[0] = station;
                for (int i = 1; i < stations.Length; i++)
                {
                    var clone = UnityEngine.Object.Instantiate(station.gameObject, root.transform);
                    clone.name = i < 3 ? "DepartureCannon" + (i + 1) : "ShelterCannon" + (i - 2);
                    stations[i] = clone.GetComponent<HumanCannonStation>();
                }
                float[] x = { 0, -3, 3 };
                for (int i = 0; i < stations.Length; i++)
                {
                    stations[i].transform.position = i < 3 ? new Vector3(x[i], 0, 3) : new Vector3(x[i - 3], 4, 49);
                    Aim(stations[i], i < 3 ? firstCatch : secondCatch);
                }
                SetArray(system, "stations", stations);

                // 기존 041 봉·톱 구성을 같은 시스템의 두 번째 구간으로 연결한다.
                var secondGate = UnityEngine.Object.Instantiate(poleSystem.gameObject, root.transform);
                secondGate.name = "SecondSectionPoleGate";
                secondGate.transform.position += new Vector3(0, 4, 46);
                UnityEngine.Object.DestroyImmediate(secondGate.GetComponent<HumanCannonPoleSystem>());
                var poles = poleSystem.GetComponentsInChildren<HumanCannonPole>(true)
                    .Concat(secondGate.GetComponentsInChildren<HumanCannonPole>(true)).ToArray();
                SetArray(poleSystem, "poles", poles);
                cushion.transform.position = new Vector3(0, 4.1f, 84);
                cushion.transform.localScale = new Vector3(12, 0.2f, 5); // 안전 경계 z81.5
                var pressure = new[]
                {
                    Fire(root.transform, "FirstSectionFire", new Vector3(0, 0, -11), new Vector3(0, 0, 42), fire),
                    Fire(root.transform, "SecondSectionFire", new Vector3(0, 4, 42), new Vector3(0, 4, 81.5f), fire)
                };
                SetArray(route, "transports", new[] { departure, lift, restart });
                SetArray(route, "fires", pressure);
                SetObject(route, "shelter", shelter);
                switchPoint.gameObject.SetActive(false); // 자동 승강으로 대체된 구형 스위치 표시.
                foreach (var wall in pressure) wall.ResetWall();

                // 공중에 남아 도보 다리처럼 보일 수 있는 기존 거리 눈금만 감춘다. 원본 오브젝트는 보존한다.
                var markers = roots.FirstOrDefault(g => g.name == "DistanceMarkers");
                if (markers != null)
                    foreach (var renderer in markers.GetComponentsInChildren<Renderer>(true))
                        if (renderer.bounds.center.z > 7 && renderer.bounds.center.z < 16) renderer.gameObject.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("042 테스트 씬 저장 실패");
                Undo.CollapseUndoOperations(group);
                Debug.Log("[HumanCannonRoute] 042 코스 저장: 대포 6대·운송 2대·쉼터 승강기·불꽃 2구간. F 출발, R 초기화.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static void Aim(HumanCannonStation station, Vector3 catchPoint)
        {
            Vector3 heading = Vector3.ProjectOnPlane(catchPoint - station.transform.position, Vector3.up).normalized;
            station.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            float gravity = GameSettings.Base.Character.Gravity;
            float up = station.LaunchVelocity.y;
            float height = station.Muzzle.position.y - (catchPoint.y + HumanCannonTrampolineBuilder.SurfaceHeight);
            float time = (up + Mathf.Sqrt(up * up + 2 * gravity * height)) / gravity;
            var data = new SerializedObject(station);
            data.FindProperty("forwardSpeed").floatValue = Vector3.ProjectOnPlane(catchPoint - station.Muzzle.position, Vector3.up).magnitude / time;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static HumanCannonTransport Transport(Transform parent, string name, Vector3 from, Vector3 to, float seconds, Material material)
        {
            var root = Point(parent, name, Vector3.zero);
            var transport = root.gameObject.AddComponent<HumanCannonTransport>();
            var start = Point(root, "Start", from);
            var destination = Point(root, "Destination", to);
            var platform = Point(root, "Platform", from);
            Part(platform, "Deck", new Vector3(0, -0.15f, 0), new Vector3(6, 0.3f, 8), material, true);
            SetObject(transport, "platform", platform);
            SetObject(transport, "start", start);
            SetObject(transport, "destination", destination);
            var data = new SerializedObject(transport);
            data.FindProperty("travelSeconds").floatValue = seconds;
            data.ApplyModifiedPropertiesWithoutUndo();
            return transport;
        }

        static HumanCannonFireWall Fire(Transform parent, string name, Vector3 from, Vector3 to, Material material)
        {
            var root = Point(parent, name, Vector3.zero);
            var wall = root.gameObject.AddComponent<HumanCannonFireWall>();
            SetObject(wall, "start", Point(root, "Start", from));
            SetObject(wall, "boundary", Point(root, "Boundary", to));
            SetObject(wall, "visual", Part(root, "FlameFront", from, Vector3.one, material).transform);
            return wall;
        }

        static void MarkReceiving(Transform parent, string name, Vector3 position, Material material)
        {
            var root = Point(parent, name, position);
            Part(root, "Left", new Vector3(-3, 0.02f, 0), new Vector3(0.12f, 0.04f, 6), material);
            Part(root, "Right", new Vector3(3, 0.02f, 0), new Vector3(0.12f, 0.04f, 6), material);
            Part(root, "Near", new Vector3(0, 0.02f, -3), new Vector3(6, 0.04f, 0.12f), material);
            Part(root, "Far", new Vector3(0, 0.02f, 3), new Vector3(6, 0.04f, 0.12f), material);
        }

        static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = position;
            return point;
        }

        static GameObject Part(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider = false)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        static Material Material(string name, Color color, Shader shader, bool transparent = false)
        {
            string path = Materials + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
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
            var list = data.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
