using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>저장된 인간 대포 테스트 씬에 041 봉·톱 관문만 추가한다.</summary>
    public static class HumanCannonPoleGateBuilder
    {
        const string MaterialFolder = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials";

        [MenuItem("Tools/CurtainCall/인간 대포/봉과 톱 관문 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
                throw new InvalidOperationException("Play 종료 후 씬을 저장하세요.");
            if (scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("HumanCannon 전용 씬을 여세요.");
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<HumanCannonPoleSystem>(true) != null || root.name == "PoleGateCourse")
                    throw new InvalidOperationException("기존 봉·톱 관문은 덮어쓰지 않습니다.");

            Build();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("봉·톱 관문 씬 저장 실패");
        }

        static void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP 셰이더가 없습니다.");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Scenes/Tests/MainStage/HumanCannon", "Materials");
            var silver = Material("pole-gate-silver", new Color(0.72f, 0.78f, 0.83f), shader);
            var yellow = Material("pole-gate-yellow", new Color(1f, 0.79f, 0.06f), shader);
            var black = Material("pole-gate-black", new Color(0.08f, 0.1f, 0.13f), shader);
            var landing = Material("pole-gate-receiving", new Color(0.12f, 0.63f, 0.55f), shader);

            var course = new GameObject("PoleGateCourse");
            var system = course.AddComponent<HumanCannonPoleSystem>();
            var gate = Gate(course.transform, silver, yellow, black);
            var normal = Pole(course.transform, "NormalPole", new Vector3(-6f, 6.5f, 25f),
                null, silver, black);
            var linked = Pole(course.transform, "LinkedPole", new Vector3(0f, 6.5f, 29f),
                gate, yellow, black);
            var systemData = new SerializedObject(system);
            var poles = systemData.FindProperty("poles");
            poles.arraySize = 2;
            poles.GetArrayElementAtIndex(0).objectReferenceValue = normal;
            poles.GetArrayElementAtIndex(1).objectReferenceValue = linked;
            systemData.ApplyModifiedPropertiesWithoutUndo();

            var cable = new GameObject("GateConnectionCable").AddComponent<LineRenderer>();
            cable.transform.SetParent(course.transform, false);
            cable.useWorldSpace = false;
            cable.positionCount = 3;
            cable.SetPosition(0, new Vector3(2.25f, 6.5f, 29f));
            cable.SetPosition(1, new Vector3(2.25f, 6.5f, 32f));
            cable.SetPosition(2, new Vector3(2.25f, 2.4f, 32f));
            cable.startWidth = cable.endWidth = 0.08f;
            cable.sharedMaterial = yellow;

            var fences = Point("SideFences", course.transform, Vector3.zero);
            // 기존 바닥은 x ±15, z 42까지다. 도약으로 넘지 못하는 연속 측벽을 둔다.
            foreach (int side in new[] { -1, 1 })
            {
                var fence = Part(fences, side < 0 ? "LeftFence" : "RightFence",
                    new Vector3(side * 15f, 9f, 31f), new Vector3(0.35f, 18f, 22f), black, true);
                Part(fence.transform, "YellowEdge", new Vector3(-side * 0.52f, 0f, 0f),
                    new Vector3(0.08f, 1f, 1f), yellow);
            }

            var receiving = Point("ReceivingArea", course.transform, Vector3.zero);
            // 표시는 충돌 없는 바닥 테두리뿐이며, 기존 쿠션·바닥은 수정하지 않는다.
            Part(receiving, "NearEdge", new Vector3(0f, 0.025f, 35f), new Vector3(12f, 0.05f, 0.15f), landing);
            Part(receiving, "FarEdge", new Vector3(0f, 0.025f, 40f), new Vector3(12f, 0.05f, 0.15f), landing);
            Part(receiving, "LeftEdge", new Vector3(-6f, 0.025f, 37.5f), new Vector3(0.15f, 0.05f, 5f), landing);
            Part(receiving, "RightEdge", new Vector3(6f, 0.025f, 37.5f), new Vector3(0.15f, 0.05f, 5f), landing);
        }

        static HumanCannonPole Pole(Transform parent, string name, Vector3 position,
            HumanCannonSawGate gate, Material barMaterial, Material supportMaterial)
        {
            var root = Point(name, parent, position);
            var pole = root.gameObject.AddComponent<HumanCannonPole>();
            var grip = Point("Grip", root, Vector3.zero);
            var bar = Part(root, "Bar", Vector3.zero, new Vector3(0.22f, 2.25f, 0.22f),
                barMaterial, false, PrimitiveType.Cylinder);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (gate != null)
                for (int i = -2; i <= 2; i++)
                {
                    var band = Part(root, "BlackBand" + i, new Vector3(i * 0.8f, 0f, 0f),
                        new Vector3(0.23f, 0.15f, 0.23f), supportMaterial, false, PrimitiveType.Cylinder);
                    band.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            foreach (int side in new[] { -1, 1 })
                Part(root, side < 0 ? "LeftSupport" : "RightSupport", new Vector3(side * 2.3f, -3.25f, 0f),
                    new Vector3(0.16f, 6.5f, 0.16f), supportMaterial);

            var data = new SerializedObject(pole);
            data.FindProperty("grip").objectReferenceValue = grip;
            data.FindProperty("linkedGate").objectReferenceValue = gate;
            data.FindProperty("hangLength").floatValue = 1.5f;
            data.FindProperty("grabDistance").floatValue = 2f;
            data.FindProperty("holdSeconds").floatValue = 8f;
            data.FindProperty("warningSeconds").floatValue = 2f;
            data.FindProperty("swingAcceleration").floatValue = 300f;
            data.FindProperty("maxSwingAngle").floatValue = 80f;
            data.FindProperty("maxSwingSpeed").floatValue = 180f;
            data.FindProperty("swingDamping").floatValue = 0.4f;
            data.FindProperty("releaseUpwardSpeed").floatValue = 6f;
            data.FindProperty("airControlSpeed").floatValue = 3f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return pole;
        }

        static HumanCannonSawGate Gate(Transform parent, Material silver, Material yellow, Material black)
        {
            var root = Point("SawGate", parent, new Vector3(0f, 0f, 32f));
            var gate = root.gameObject.AddComponent<HumanCannonSawGate>();
            var barrier = Part(root, "Barrier", new Vector3(0f, 1.2f, 0f),
                new Vector3(30f, 2.4f, 0.6f), yellow, true);
            for (int i = -7; i <= 7; i++)
                Part(barrier.transform, "WarningStripe" + i, new Vector3(i * 2f / 30f, 0f, -0.51f),
                    new Vector3(0.035f, 1f, 0.025f), black);

            var visual = Point("SawVisual", root, new Vector3(0f, 1.2f, -0.4f));
            // 관문은 SawVisual의 직접 자식을 회전시킨다. X자 각도는 Rotor 아래에서 유지한다.
            var rotor = Point("Rotor", visual, Vector3.zero);
            var firstBlade = Part(rotor, "BladeA", Vector3.zero, new Vector3(3.2f, 0.28f, 0.12f), silver);
            firstBlade.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var secondBlade = Part(rotor, "BladeB", Vector3.zero, new Vector3(3.2f, 0.28f, 0.12f), silver);
            secondBlade.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            var hub = Part(rotor, "Hub", new Vector3(0f, 0f, -0.1f),
                new Vector3(0.5f, 0.1f, 0.5f), black, false, PrimitiveType.Cylinder);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var data = new SerializedObject(gate);
            data.FindProperty("barrier").objectReferenceValue = barrier.GetComponent<Collider>();
            data.FindProperty("sawVisual").objectReferenceValue = visual;
            data.FindProperty("contactPadding").floatValue = 0.15f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        static Transform Point(string name, Transform parent, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = position;
            return point;
        }

        static GameObject Part(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool keepCollider = false, PrimitiveType primitive = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        static Material Material(string name, Color color, Shader shader)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
