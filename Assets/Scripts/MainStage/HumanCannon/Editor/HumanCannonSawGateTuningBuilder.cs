using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>두 협동 관문을 구간 중간으로 옮기고 벽 표시를 지상 전기톱 열로 교체한다.</summary>
    public static class HumanCannonSawGateTuningBuilder
    {
        const string Materials = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";

        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 HumanCannon 전용 씬을 여세요.");
            var roots = scene.GetRootGameObjects();
            var gates = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonSawGate>(true))
                .OrderBy(gate => gate.transform.position.z).ToArray();
            var poles = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonPole>(true)).ToArray();
            if (gates.Length != 2 || gates.Any(gate => poles.Count(pole => pole.LinkedGate == gate) != 1))
                throw new InvalidOperationException("연결 봉 하나씩을 갖는 기존 협동 관문 두 개가 필요합니다.");
            foreach (var gate in gates)
            {
                if (gate.transform.Find("Barrier")?.GetComponent<Collider>() == null
                    || gate.transform.Find("SawVisual") == null)
                    throw new InvalidOperationException("기존 관문 Barrier/SawVisual 연결이 없습니다.");
                if (gate.transform.Find("SawVisual/GateSawDisc0") != null)
                    throw new InvalidOperationException("관문 전기톱 외형이 이미 적용되어 덮어쓰지 않았습니다.");
            }
            var silver = LoadMaterial("pole-gate-silver");
            var black = LoadMaterial("pole-gate-black");
            for (int index = 0; index < gates.Length; index++)
            {
                var gate = gates[index];
                Vector3 position = gate.transform.position;
                position.z = index == 0 ? 47f : 123f;
                gate.transform.position = position;
                var linked = poles.Single(pole => pole.LinkedGate == gate);
                linked.transform.position += Vector3.forward * (position.z - 3f - linked.Grip.position.z);

                var barrier = gate.transform.Find("Barrier");
                foreach (var renderer in barrier.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var stripe in barrier.Cast<Transform>().Where(child => child.name.StartsWith("WarningStripe")).ToArray())
                    UnityEngine.Object.DestroyImmediate(stripe.gameObject);
                var visual = gate.transform.Find("SawVisual");
                foreach (var old in visual.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
                visual.localPosition = new Vector3(0, 1.2f, 0);
                for (int saw = 0; saw < 13; saw++)
                    Saw(visual, "GateSawDisc" + saw, new Vector3(-14.4f + saw * 2.4f, 0, 0), silver, black);
            }
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void Saw(Transform parent, string name, Vector3 position, Material silver, Material black)
        {
            var rotor = new GameObject(name).transform;
            rotor.SetParent(parent, false);
            rotor.localPosition = position;
            Part(rotor, "Disc", Vector3.zero, new Vector3(2.12f, 0.06f, 2.12f), silver, PrimitiveType.Cylinder)
                .localRotation = Quaternion.Euler(90, 0, 0);
            for (int tooth = 0; tooth < 24; tooth++)
            {
                float angle = tooth * 15f;
                Vector3 radial = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);
                Part(rotor, "Tooth" + tooth, radial * 1.08f, new Vector3(0.18f, 0.2f, 0.14f), silver)
                    .localRotation = Quaternion.Euler(0, 0, angle + 25f);
            }
            Part(rotor, "Hub", new Vector3(0, 0, -0.08f), new Vector3(0.36f, 0.09f, 0.36f), black, PrimitiveType.Cylinder)
                .localRotation = Quaternion.Euler(90, 0, 0);
        }

        static Transform Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
            PrimitiveType primitive = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat")
            ?? throw new InvalidOperationException("기존 관문 머티리얼이 없습니다: " + name);
    }
}
