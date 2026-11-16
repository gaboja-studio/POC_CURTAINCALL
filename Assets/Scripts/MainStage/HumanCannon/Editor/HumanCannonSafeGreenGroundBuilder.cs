using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>
    /// 2026-11-13 담당자 요청: 마지막 대포 구역의 초록 지면(FinalGreenGround)과 같은 초록을
    /// 시작 지점·쉼터(다음 인간대포)·도착 지점 안전 착지대 윗면에 깐다. 외형 전용이며 충돌·착지 판정은 바꾸지 않는다.
    /// 착지대에 등록된 바닥 면만 칠하고, 상태 색을 보여 주는 이동 장치 발판은 칠하지 않는다.
    /// 인간대포의 콜라이더가 지면에 닿아 있으면 그 자리(가로·세로 범위)는 비운다.
    /// </summary>
    public static class HumanCannonSafeGreenGroundBuilder
    {
        const string MeshFolder = "Assets/Scenes/Tests/MainStage/HumanCannon/Meshes/";
        // 바닥 윗면과 겹쳐 깜빡이지 않도록 띄우는 높이(m).
        const float Lift = 0.01f;
        // 콜라이더 아래끝이 바닥 윗면에서 이 거리 안이면 지면에 닿은 것으로 본다(m).
        const float GroundContact = 0.05f;

        static readonly (string zone, string name, string mesh)[] Targets =
        {
            ("Ground", "StartGreenGround", "start-green-ground"),
            ("SplitRouteCourse/ShelterDeck", "ShelterGreenGround", "shelter-green-ground"),
            ("FinalCannonCourse/FlightLandingPad", "ArrivalGreenGround", "arrival-green-ground"),
        };

        [MenuItem("Tools/CurtainCall/인간 대포/안전지대 초록 지면 적용")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 HumanCannon 전용 씬에서 실행하세요.");
            var source = GameObject.Find("FinalGreenGround");
            if (source == null || !source.TryGetComponent(out MeshRenderer sourceRenderer) || !source.TryGetComponent(out MeshFilter sourceFilter)
                || sourceFilter.sharedMesh == null || sourceFilter.sharedMesh.uv.Length == 0)
                throw new InvalidOperationException("기준 초록 지면(FinalGreenGround)이 필요합니다.");
            Material material = sourceRenderer.sharedMaterial;
            Vector2 green = sourceFilter.sharedMesh.uv[0];
            var cannonColliders = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HumanCannonStation>(true))
                .SelectMany(station => station.GetComponentsInChildren<Collider>(true))
                .Where(item => item.enabled && item.gameObject.activeInHierarchy && !item.isTrigger)
                .ToArray();

            foreach (var (zonePath, name, meshName) in Targets)
            {
                var zoneObject = GameObject.Find(zonePath);
                if (zoneObject == null || !zoneObject.TryGetComponent(out HumanCannonLandingZone zone))
                    throw new InvalidOperationException(zonePath + " 안전 착지대가 없습니다.");
                string path = MeshFolder + meshName + ".asset";
                // 기존 에셋은 같은 객체를 다시 채운다(복사로 덮으면 화면용 메시 데이터가 갱신되지 않는다).
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool created = mesh == null;
                if (created) mesh = new Mesh();
                Fill(mesh, Surfaces(zone), cannonColliders, green);
                mesh.name = meshName;
                if (created) AssetDatabase.CreateAsset(mesh, path);
                else EditorUtility.SetDirty(mesh);

                var target = scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
                if (target == null)
                {
                    target = new GameObject(name);
                    SceneManager.MoveGameObjectToScene(target, scene);
                }
                target.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                target.transform.localScale = Vector3.one;
                // 에디터의 GetComponent는 없는 컴포넌트에 가짜 null을 돌려주므로 ??를 쓰지 않는다.
                if (!target.TryGetComponent(out MeshFilter filter)) filter = target.AddComponent<MeshFilter>();
                if (!target.TryGetComponent(out MeshRenderer renderer)) renderer = target.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var collider in target.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>착지대에 등록된 켜진 바닥 상자. 이동 장치 발판은 상태 색을 가리지 않도록 뺀다.</summary>
        static IEnumerable<BoxCollider> Surfaces(HumanCannonLandingZone zone)
        {
            var data = new SerializedObject(zone).FindProperty("additionalSurfaces");
            var all = new List<BoxCollider>();
            if (zone.TryGetComponent(out BoxCollider main)) all.Add(main);
            for (int i = 0; i < data.arraySize; i++)
                if (data.GetArrayElementAtIndex(i).objectReferenceValue is BoxCollider tile) all.Add(tile);
            return all.Where(item => item != null && item.enabled && item.gameObject.activeInHierarchy
                && item.GetComponentInParent<HumanCannonTransport>(true) == null);
        }

        static void Fill(Mesh mesh, IEnumerable<BoxCollider> surfaces, Collider[] cannons, Vector2 uv)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var surface in surfaces)
            {
                Bounds top = surface.bounds;
                float y = top.max.y + Lift;
                var holes = cannons.Select(item => item.bounds)
                    .Where(item => item.min.y <= top.max.y + GroundContact && item.max.y >= top.max.y - GroundContact
                        && item.max.x > top.min.x && item.min.x < top.max.x && item.max.z > top.min.z && item.min.z < top.max.z)
                    .ToArray();
                // 구멍 가장자리로 바닥을 격자로 나누고, 구멍 안 칸만 뺀다.
                var xs = Cuts(top.min.x, top.max.x, holes.SelectMany(item => new[] { item.min.x, item.max.x }));
                var zs = Cuts(top.min.z, top.max.z, holes.SelectMany(item => new[] { item.min.z, item.max.z }));
                for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < zs.Count; j++)
                {
                    float cx = (xs[i] + xs[i + 1]) * 0.5f, cz = (zs[j] + zs[j + 1]) * 0.5f;
                    if (holes.Any(item => cx > item.min.x && cx < item.max.x && cz > item.min.z && cz < item.max.z)) continue;
                    int start = vertices.Count;
                    vertices.Add(new Vector3(xs[i], y, zs[j]));
                    vertices.Add(new Vector3(xs[i], y, zs[j + 1]));
                    vertices.Add(new Vector3(xs[i + 1], y, zs[j + 1]));
                    vertices.Add(new Vector3(xs[i + 1], y, zs[j]));
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, Enumerable.Repeat(uv, vertices.Count).ToList());
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, vertices.Count).ToList());
            mesh.RecalculateBounds();
        }

        static List<float> Cuts(float min, float max, IEnumerable<float> edges)
        {
            var cuts = edges.Where(value => value > min && value < max).Append(min).Append(max).Distinct().ToList();
            cuts.Sort();
            return cuts;
        }
    }
}
