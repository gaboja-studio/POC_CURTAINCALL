using UnityEditor;
using UnityEngine;

namespace CurtainCall.Player.EditorTools
{
    /// <summary>
    /// 테스트용 플레이어 모델 프리팹 2개(기본 캡슐, 교체 확인용 블록 인형)를 만든다.
    /// 메뉴: Tools/CurtainCall/Build Player Test Models. 실제 모델이 오면 같은 폴더에 프리팹을 추가해 PlayerModelSlot에 끼운다.
    /// </summary>
    static class PlayerModelPrefabBuilder
    {
        const string Folder = "Assets/Resources/Prefabs/Characters/Players/Models";
        const string MaterialFolder = Folder + "/Materials";

        [MenuItem("Tools/CurtainCall/Build Player Test Models")]
        static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs/Characters/Players", "Models");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(Folder, "Materials");

            Save(BuildCapsule(), "DefaultCapsule");
            Save(BuildBlockDoll(), "BlockDoll");
            Debug.Log($"[PlayerModelPrefabBuilder] 저장: {Folder}/DefaultCapsule.prefab, BlockDoll.prefab");
        }

        static GameObject BuildCapsule()
        {
            var root = new GameObject("DefaultCapsule");
            Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 1f), Mat("Gray", new Color(0.85f, 0.85f, 0.85f)));
            // 정면 표시: 앞쪽(+Z)에 작은 코
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.5f, 0.5f), new Vector3(0.2f, 0.2f, 0.2f), Mat("Dark", new Color(0.2f, 0.2f, 0.2f)));
            return root;
        }

        static GameObject BuildBlockDoll()
        {
            var root = new GameObject("BlockDoll");
            var red = Mat("Red", new Color(0.75f, 0.2f, 0.2f));
            var skin = Mat("Skin", new Color(0.95f, 0.8f, 0.65f));
            var navy = Mat("Navy", new Color(0.15f, 0.2f, 0.4f));
            Part(root.transform, PrimitiveType.Cube, new Vector3(-0.15f, 0.45f, 0f), new Vector3(0.22f, 0.9f, 0.25f), navy); // 왼다리
            Part(root.transform, PrimitiveType.Cube, new Vector3(0.15f, 0.45f, 0f), new Vector3(0.22f, 0.9f, 0.25f), navy);  // 오른다리
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.6f, 0.65f, 0.3f), red);        // 몸
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(1.4f, 0.12f, 0.12f), red);      // 팔(균형 자세)
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.75f, 0f), new Vector3(0.4f, 0.4f, 0.4f), skin);     // 머리
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.75f, 0.2f), new Vector3(0.1f, 0.1f, 0.1f), navy);     // 정면 표시
            return root;
        }

        static void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>테스트 모델용 단색 머티리얼을 Models/Materials에 만들거나 다시 쓴다.</summary>
        static Material Mat(string name, Color color)
        {
            string path = $"{MaterialFolder}/PlayerTest_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Save(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
