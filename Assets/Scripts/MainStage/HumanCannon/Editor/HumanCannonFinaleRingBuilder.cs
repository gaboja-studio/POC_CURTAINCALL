using System;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>
    /// 피날레 대포 4대의 무조작 탄도(공중 보정 입력 없음) 위에 대포마다 상승·정점·하강 링 3개를 둔다.
    /// 링 중심은 몸통 중심 경로(발 기준 탄도 + 캡슐 절반 높이)이고 링 면은 그 지점의 진행 방향을 마주본다. 충돌은 없다.
    /// </summary>
    public static class HumanCannonFinaleRingBuilder
    {
        public const string RootName = "FinaleRings";
        public const float RingRadius = 1.3f;
        public const float TubeRadius = 0.1f;
        /// <summary>정점 시간에 곱하는 배율: 상승 중간, 정점, 하강 중간.</summary>
        public static readonly float[] ApexFractions = { 0.5f, 1f, 1.5f };
        const string AssetFolder = "Assets/Scenes/Tests/MainStage/HumanCannon";
        const string MeshFolder = AssetFolder + "/Meshes";
        const string MeshPath = MeshFolder + "/finale-ring.asset";
        const string MaterialPath = AssetFolder + "/Materials/finale-ring.mat";

        [MenuItem("Tools/CurtainCall/인간 대포/피날레 탄도 링 배치")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
                throw new InvalidOperationException("Play 종료 후 현재 씬을 저장하세요.");
            if (scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("HumanCannon 전용 테스트 씬을 먼저 여세요.");
            GameObject course = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "FinalCannonCourse") course = root;
            if (course == null) throw new InvalidOperationException("FinalCannonCourse가 없습니다.");
            if (course.transform.Find(RootName) != null) throw new InvalidOperationException("기존 피날레 링은 덮어쓰지 않습니다.");
            Build(course.transform);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("피날레 링 씬 저장 실패");
        }

        public static Transform Build(Transform course)
        {
            var stations = course.GetComponentsInChildren<HumanCannonStation>(true);
            if (stations.Length == 0) throw new InvalidOperationException("피날레 대포가 없습니다.");
            float gravity = GameSettings.Base.Character.Gravity;
            var mesh = RingMesh();
            var material = RingMaterial();
            var root = new GameObject(RootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "피날레 탄도 링 배치");
            root.SetParent(course, false);
            foreach (var station in stations)
            {
                Vector3 velocity = station.LaunchVelocity;
                if (velocity.y <= 0) throw new InvalidOperationException(station.name + " 탄도에 정점이 없습니다.");
                float apex = velocity.y / gravity;
                for (int i = 0; i < ApexFractions.Length; i++)
                {
                    float time = apex * ApexFractions[i];
                    var ring = new GameObject(station.name + "_Ring" + (i + 1));
                    ring.transform.SetParent(root, false);
                    ring.transform.SetPositionAndRotation(BodyCenter(station, time, gravity),
                        Quaternion.LookRotation(Velocity(station, time, gravity), Vector3.up));
                    ring.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = ring.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            return root;
        }

        /// <summary>조작 없이 날아간 몸통 중심 위치. 발 기준 포구 위치에서 시작한다.</summary>
        public static Vector3 BodyCenter(HumanCannonStation station, float time, float gravity) =>
            station.Muzzle.position + station.LaunchVelocity * time + Vector3.down * (0.5f * gravity * time * time)
            + Vector3.up * (GameSettings.Base.Character.CapsuleHeight * 0.5f);

        public static Vector3 Velocity(HumanCannonStation station, float time, float gravity) =>
            station.LaunchVelocity + Vector3.down * (gravity * time);

        static Mesh RingMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh != null) return mesh;
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(AssetFolder, "Meshes");
            const int around = 64, tube = 12;
            var vertices = new Vector3[(around + 1) * (tube + 1)];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[around * tube * 6];
            for (int a = 0, v = 0; a <= around; a++)
            {
                float u = a * Mathf.PI * 2 / around;
                var direction = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0);
                for (int t = 0; t <= tube; t++, v++)
                {
                    float w = t * Mathf.PI * 2 / tube;
                    normals[v] = direction * Mathf.Cos(w) + Vector3.forward * Mathf.Sin(w);
                    vertices[v] = direction * RingRadius + normals[v] * TubeRadius;
                }
            }
            for (int a = 0, i = 0; a < around; a++)
                for (int t = 0; t < tube; t++)
                {
                    int current = a * (tube + 1) + t, next = current + tube + 1;
                    triangles[i++] = current; triangles[i++] = current + 1; triangles[i++] = next;
                    triangles[i++] = next; triangles[i++] = current + 1; triangles[i++] = next + 1;
                }
            mesh = new Mesh { name = "finale-ring", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        static Material RingMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP 셰이더가 없습니다.");
            material = new Material(shader) { name = "finale-ring" };
            material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.12f));
            material.SetFloat("_Metallic", 0.6f);
            material.SetFloat("_Smoothness", 0.45f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
