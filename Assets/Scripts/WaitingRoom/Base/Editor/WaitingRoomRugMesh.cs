using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>외부 모델의 정점 복제 없이 독립적인 격자로 만드는 얇은 낡은 천.</summary>
    public static class WaitingRoomRugMesh
    {
        public static Mesh Create(string name, float width = 4, float depth = 2)
        {
            const int nx = 20, nz = 10;
            var vertices = new Vector3[(nx + 1) * (nz + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new System.Collections.Generic.List<int>();
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
            {
                int i = z * (nx + 1) + x; float u = x / (float)nx, v = z / (float)nz;
                float px = (u - .5f) * width, pz = (v - .5f) * depth;
                if (x == 0 || x == nx) px += Mathf.Sin(z * 2.1f) * .025f;
                if (z == 0 || z == nz) pz += Mathf.Sin(x * 1.7f) * .018f;
                vertices[i] = new Vector3(px, .008f * Mathf.Sin(u * 17) * Mathf.Sin(v * 11), pz);
                uv[i] = new Vector2(px * 1.5f, pz * 1.5f);
                if (x < nx && z < nz) { int b = i + nx + 1; triangles.AddRange(new[] { i, b, i + 1, i + 1, b, b + 1 }); }
            }
            var mesh = new Mesh { name = "Authored_" + name }; mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        public static void ReplaceStagedCopies()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity") throw new System.InvalidOperationException("대상 씬 편집 모드에서만 실행합니다.");
            var replaced = new System.Collections.Generic.HashSet<Mesh>();
            foreach (var mf in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)))
            {
                var original = mf.sharedMesh; if (!original || original.name.StartsWith("Authored_") || !original.name.Contains("Pattern")) continue;
                string path = AssetDatabase.GetAssetPath(original);
                if (path != "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomDormitoryLayout.asset" && path != "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomRoomStages.asset") continue;
                var renderer = mf.GetComponent<Renderer>(); float bottom = renderer.bounds.min.y;
                var authored = Create(original.name); AssetDatabase.AddObjectToAsset(authored, path);
                Undo.RecordObject(mf, "독립 제작 러그 메시"); mf.sharedMesh = authored;
                Undo.RecordObject(mf.transform, "러그 바닥 높이 유지"); mf.transform.position += Vector3.up * (bottom - renderer.bounds.min.y);
                replaced.Add(original);
            }
            foreach (var mesh in replaced) Object.DestroyImmediate(mesh, true);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); Debug.Log("독립 제작 러그 메시 교체: " + replaced.Count);
        }
    }
}
