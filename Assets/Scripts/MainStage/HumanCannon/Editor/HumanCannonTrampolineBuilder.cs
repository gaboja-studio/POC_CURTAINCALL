using System;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class HumanCannonTrampolineBuilder
    {
        const string MaterialFolder = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials";
        public const float SurfaceHeight = 0.5f;

        [MenuItem("Tools/CurtainCall/인간 대포/테스트 트램폴린 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
                throw new InvalidOperationException("Play 종료 후 씬을 저장하세요.");
            if (scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("HumanCannon 전용 씬을 여세요.");
            HumanCannonStation station = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<HumanCannonTrampoline>() != null)
                    throw new InvalidOperationException("기존 트램폴린은 덮어쓰지 않습니다.");
                if (root.TryGetComponent<HumanCannonStation>(out var found)) station = found;
            }
            Build(station);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("트램폴린 씬 저장 실패");
        }

        public static HumanCannonTrampoline Build(HumanCannonStation station)
        {
            if (station == null) throw new InvalidOperationException("대포가 없습니다.");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP 셰이더가 없습니다.");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Scenes/Tests/MainStage/HumanCannon", "Materials");
            var fabric = Material("trampoline-fabric", new Color(0.08f, 0.55f, 0.45f), shader);
            var frame = Material("trampoline-frame", new Color(0.16f, 0.22f, 0.28f), shader);
            var grip = Material("trampoline-handle", new Color(1f, 0.8f, 0.08f), shader);
            Vector3 velocity = station.LaunchVelocity;
            float gravity = GameSettings.Base.Character.Gravity;
            float time = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * (station.Muzzle.position.y - SurfaceHeight))) / gravity;
            Vector3 position = station.Muzzle.position + Vector3.ProjectOnPlane(velocity, Vector3.up) * time;
            position.y = 0f;
            var root = new GameObject("Trampoline");
            root.transform.position = position;
            var trampoline = root.AddComponent<HumanCannonTrampoline>();
            Part(root.transform, "BounceSurface", new Vector3(0, SurfaceHeight - 0.05f, 0), new Vector3(4f, 0.1f, 4f), fabric);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Part(root.transform, "Leg", new Vector3(x * 1.85f, (SurfaceHeight - 0.1f) / 2f, z * 1.85f), new Vector3(0.15f, SurfaceHeight - 0.1f, 0.15f), frame);
            var handle = new GameObject("Handle").transform;
            handle.SetParent(root.transform, false);
            handle.localPosition = new Vector3(-2.8f, 0, 0);
            Part(handle, "YellowGrip", new Vector3(0, SurfaceHeight - 0.1f, 0), new Vector3(0.2f, 0.15f, 1.4f), grip);
            Part(handle, "HandleArm", new Vector3(0.5f, SurfaceHeight - 0.15f, 0), new Vector3(1f, 0.12f, 0.12f), frame);
            var data = new SerializedObject(trampoline);
            data.FindProperty("handle").objectReferenceValue = handle;
            data.FindProperty("surfaceHeight").floatValue = SurfaceHeight;
            data.ApplyModifiedPropertiesWithoutUndo();
            return trampoline;
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

        static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
