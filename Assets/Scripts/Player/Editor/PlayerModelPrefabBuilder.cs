using UnityEditor;
using UnityEngine;

namespace CurtainCall.Player.EditorTools
{
    /// <summary>
    /// 테스트용 플레이어 모델 프리팹 2개(기본 캡슐, 교체 확인용 블록 인형)를 만든다.
    /// 메뉴: Tools/CurtainCall/Build Player Test Models. 실제 모델이 오면 같은 폴더에 프리팹을 추가해 PlayerModelSlot에 끼운다.
    /// 추락 래그돌용으로 부위마다 콜라이더·Rigidbody(평소엔 Kinematic)를 두고, 블록 인형은 CharacterJoint로 몸통에 잇는다.
    /// 실제 모델은 Unity Ragdoll Wizard(GameObject/3D Object/Ragdoll...)로 같은 구성을 만든다.
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
            var body = Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 1f), Mat("Gray", new Color(0.85f, 0.85f, 0.85f)), 1f);
            // 정면 표시: 앞쪽(+Z)에 작은 코. 몸과 함께 넘어지도록 몸의 자식으로 둔다
            var nose = Part(body.transform, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.5f), new Vector3(0.2f, 0.2f, 0.2f), Mat("Dark", new Color(0.2f, 0.2f, 0.2f)), 0f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            return root;
        }

        static GameObject BuildBlockDoll()
        {
            var root = new GameObject("BlockDoll");
            var red = Mat("Red", new Color(0.75f, 0.2f, 0.2f));
            var skin = Mat("Skin", new Color(0.95f, 0.8f, 0.65f));
            var navy = Mat("Navy", new Color(0.15f, 0.2f, 0.4f));

            var torso = Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.6f, 0.65f, 0.3f), red, 3f);
            var head = Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.75f, 0f), new Vector3(0.4f, 0.4f, 0.4f), skin, 1f);
            var face = Part(head.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.5f), new Vector3(0.25f, 0.25f, 0.25f), navy, 0f); // 정면 표시
            Object.DestroyImmediate(face.GetComponent<Collider>());
            var legL = Part(root.transform, PrimitiveType.Cube, new Vector3(-0.15f, 0.45f, 0f), new Vector3(0.22f, 0.9f, 0.25f), navy, 1.5f);
            var legR = Part(root.transform, PrimitiveType.Cube, new Vector3(0.15f, 0.45f, 0f), new Vector3(0.22f, 0.9f, 0.25f), navy, 1.5f);
            var armL = Part(root.transform, PrimitiveType.Cube, new Vector3(-0.65f, 1.35f, 0f), new Vector3(0.7f, 0.12f, 0.12f), red, 0.7f); // 균형 자세로 벌린 팔
            var armR = Part(root.transform, PrimitiveType.Cube, new Vector3(0.65f, 1.35f, 0f), new Vector3(0.7f, 0.12f, 0.12f), red, 0.7f);

            Joint(head, torso, new Vector3(0f, 1.55f, 0f), Vector3.right, 30f);
            Joint(legL, torso, new Vector3(-0.15f, 0.9f, 0f), Vector3.right, 70f);
            Joint(legR, torso, new Vector3(0.15f, 0.9f, 0f), Vector3.right, 70f);
            Joint(armL, torso, new Vector3(-0.3f, 1.35f, 0f), Vector3.forward, 80f);
            Joint(armR, torso, new Vector3(0.3f, 1.35f, 0f), Vector3.forward, 80f);
            return root;
        }

        /// <summary>부위 하나를 만든다. mass가 0보다 크면 래그돌용 Rigidbody(평소 Kinematic)를 붙인다.</summary>
        static GameObject Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, float mass)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (mass > 0f)
            {
                var body = part.AddComponent<Rigidbody>();
                body.mass = mass;
                body.isKinematic = true;
            }
            return part;
        }

        /// <summary>부위를 몸통에 관절로 잇는다. anchorWorld는 모델 기준 관절 위치, limit는 흔들 수 있는 각도(도).</summary>
        static void Joint(GameObject part, GameObject torso, Vector3 anchorWorld, Vector3 axis, float limit)
        {
            var joint = part.AddComponent<CharacterJoint>();
            joint.connectedBody = torso.GetComponent<Rigidbody>();
            joint.anchor = part.transform.InverseTransformPoint(anchorWorld);
            joint.axis = axis;
            joint.lowTwistLimit = new SoftJointLimit { limit = -limit };
            joint.highTwistLimit = new SoftJointLimit { limit = limit };
            joint.swing1Limit = new SoftJointLimit { limit = limit };
            joint.swing2Limit = new SoftJointLimit { limit = limit * 0.5f };
            joint.enablePreprocessing = false;
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
