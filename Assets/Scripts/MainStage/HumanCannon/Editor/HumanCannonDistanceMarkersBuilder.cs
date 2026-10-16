using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>전용 테스트 바닥에 포구 기준 미터 눈금을 만든다. 물리 충돌은 추가하지 않는다.</summary>
    public static class HumanCannonDistanceMarkersBuilder
    {
        const string RootName = "DistanceMarkers";
        const string AssetFolder = "Assets/Scenes/Tests/MainStage/HumanCannon";
        const string MaterialFolder = AssetFolder + "/Materials";

        [MenuItem("Tools/CurtainCall/인간 대포/테스트 바닥 거리 눈금 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
                throw new InvalidOperationException("Play 종료 후 현재 씬을 저장하세요.");
            if (scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("HumanCannon 전용 테스트 씬을 먼저 여세요.");
            GameObject ground = null;
            HumanCannonStation station = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == RootName) throw new InvalidOperationException("기존 거리 눈금은 덮어쓰지 않습니다.");
                if (root.name == "Ground") ground = root;
                if (root.TryGetComponent<HumanCannonStation>(out var candidate)) station = candidate;
            }
            Build(ground, station);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("거리 눈금 씬 저장 실패");
        }

        public static void Build(GameObject ground, HumanCannonStation station)
        {
            if (ground == null || station == null) throw new InvalidOperationException("발판과 대포가 필요합니다.");
            if (Quaternion.Angle(ground.transform.rotation, Quaternion.identity) > 0.01f ||
                Vector3.Angle(station.Muzzle.forward, Vector3.forward) > 0.01f)
                throw new InvalidOperationException("현재 테스트 눈금은 회전 없는 발판과 +Z 방향 대포용입니다.");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP 셰이더가 없습니다.");
            var font = LoadLabelFont();
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(AssetFolder, "Materials");
            var minor = Material("distance-minor", new Color(0.43f, 0.49f, 0.55f), shader);
            var major = Material("distance-major", new Color(1f, 0.68f, 0.16f), shader);
            var axis = Material("distance-zero", new Color(0.1f, 0.85f, 0.95f), shader);
            var plate = Material("distance-label", new Color(0.07f, 0.1f, 0.14f), shader);
            var root = new GameObject(RootName).transform;
            var bounds = ground.GetComponent<Renderer>().bounds;
            Vector3 origin = station.Muzzle.position;
            origin.y = bounds.max.y + 0.012f;
            DrawGrid(root, bounds, origin, minor, major, axis);
            DrawLabels(root, bounds, origin, font, plate);
        }

        static void DrawGrid(Transform root, Bounds bounds, Vector3 origin, Material minor, Material major, Material axis)
        {
            int first = Mathf.CeilToInt(bounds.min.z - origin.z);
            int last = Mathf.FloorToInt(bounds.max.z - origin.z);
            for (int distance = first; distance <= last; distance++)
            {
                bool five = distance % 5 == 0;
                Strip("Forward_" + distance + "m", root, new Vector3(bounds.center.x, origin.y, origin.z + distance),
                    new Vector3(bounds.size.x, 0.008f, five ? 0.07f : 0.025f),
                    distance == 0 ? axis : five ? major : minor);
            }
            first = Mathf.CeilToInt(bounds.min.x - origin.x);
            last = Mathf.FloorToInt(bounds.max.x - origin.x);
            for (int distance = first; distance <= last; distance++)
                Strip("Side_" + distance + "m", root, new Vector3(origin.x + distance, origin.y, bounds.center.z),
                    new Vector3(distance == 0 ? 0.07f : 0.025f, 0.008f, bounds.size.z), distance == 0 ? axis : minor);
        }

        static void DrawLabels(Transform root, Bounds bounds, Vector3 origin, TMP_FontAsset font, Material plate)
        {
            int first = Mathf.CeilToInt((bounds.min.z - origin.z) / 5f) * 5;
            int last = Mathf.FloorToInt((bounds.max.z - origin.z - 1.3f) / 5f) * 5;
            for (int distance = first; distance <= last; distance += 5)
            {
                for (int side = -10; side <= 10; side += 10)
                {
                    Vector3 position = origin + new Vector3(side, 0.012f, distance + 0.7f);
                    Strip("LabelPlate", root, position, new Vector3(3f, 0.008f, 1.1f), plate);
                    var labelObject = new GameObject("Distance_" + distance + "m");
                    labelObject.SetActive(false);
                    var label = labelObject.AddComponent<TextMeshPro>();
                    label.transform.SetParent(root, false);
                    label.transform.SetPositionAndRotation(position + Vector3.up * 0.012f, Quaternion.Euler(90, 0, 0));
                    label.font = font;
                    label.fontSize = 9f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.rectTransform.sizeDelta = new Vector2(3f, 1.1f);
                    label.text = distance.ToString("+0;-0;0") + " m";
                    label.color = distance == 0 ? new Color(0.1f, 0.85f, 0.95f) : Color.white;
                    labelObject.SetActive(true);
                    label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        public static TMP_FontAsset LoadLabelFont()
        {
            const string folder = AssetFolder + "/Labels";
            const string path = folder + "/distance-font.asset";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(AssetFolder, "Labels");
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) == null &&
                !AssetDatabase.CopyAsset("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset", path))
                throw new InvalidOperationException("거리 라벨 전용 폰트 복사 실패");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
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

        static void Strip(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent, false);
            strip.transform.position = position;
            strip.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
            var renderer = strip.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
