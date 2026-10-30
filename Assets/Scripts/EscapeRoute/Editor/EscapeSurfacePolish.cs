using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const float SurfaceTolerance = .001f;

        sealed class PolishBox
        {
            public MeshFilter filter;
            public Bounds bounds;
            public Vector3[] world;
            public Vector2[] uv;
            public int[] triangles;
        }

        // Subtract hidden rectangular faces, never move walking surfaces or change collision.
        // Original shared meshes remain untouched; each scene owns one mesh library asset.
        public static string PolishStructuralSurfaces()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !new[] { ScenePath, "Assets/Scenes/Levels/escape_route_b.unity", "Assets/Scenes/Levels/escape_route_c.unity" }.Contains(scene.path))
                throw new InvalidOperationException("탈출로 A/B/C 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName);
            if (background.transform.Find("20_SurfacePolish") != null) return "Surface polish already applied; preserve manual edits.";
            var boxes = background.GetComponentsInChildren<MeshFilter>().Where(IsPolishBox).Select(f => new PolishBox {
                filter = f, bounds = f.GetComponent<Renderer>().bounds,
                world = f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray(),
                uv = f.sharedMesh.uv, triangles = f.sharedMesh.triangles
            }).ToArray();
            string libraryPath = BrutalMeshes + "/" + scene.name + "-polished-surfaces.asset";
            if (AssetDatabase.LoadMainAssetAtPath(libraryPath) != null)
                throw new InvalidOperationException("폴리싱 메시가 이미 있습니다. 기존 씬/Undo 상태를 먼저 확인하세요.");
            var library = new Mesh { name = scene.name + " surface library" };
            AssetDatabase.CreateAsset(library, libraryPath);
            int changed = 0, removed = 0;
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove overlapping architectural faces");
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
                bool modified = false;
                for (int axis = 0; axis < 3; axis++) for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    float plane = sign > 0 ? box.bounds.max[axis] : box.bounds.min[axis];
                    var original = Rect.MinMaxRect(box.bounds.min[u], box.bounds.min[v], box.bounds.max[u], box.bounds.max[v]);
                    var pieces = new List<Rect> { original };
                    for (int j = 0; j < boxes.Length && pieces.Count > 0; j++)
                    {
                        if (j == i) continue;
                        var other = boxes[j].bounds;
                        if (other.min[axis] > plane + SurfaceTolerance || other.max[axis] < plane - SurfaceTolerance) continue;
                        bool occludes = sign > 0 ? other.max[axis] > plane + SurfaceTolerance : other.min[axis] < plane - SurfaceTolerance;
                        // Coincident outward faces have one deterministic owner. Opposing contact faces vanish on both sides.
                        bool samePlane = Mathf.Abs((sign > 0 ? other.max[axis] : other.min[axis]) - plane) <= SurfaceTolerance;
                        if (!occludes && !(samePlane && j < i)) continue;
                        var cut = Rect.MinMaxRect(other.min[u], other.min[v], other.max[u], other.max[v]);
                        var next = new List<Rect>();
                        foreach (var piece in pieces) SubtractSurface(piece, cut, next);
                        if (!next.SequenceEqual(pieces)) modified = true;
                        pieces = next;
                    }
                    Vector3 World(float x, float y) { var point = Vector3.zero; point[axis] = plane; point[u] = x; point[v] = y; return point; }
                    var normal = Vector3.zero; normal[axis] = sign;
                    foreach (var rect in pieces)
                    {
                        var points = new[] { World(rect.xMin, rect.yMin), World(rect.xMax, rect.yMin), World(rect.xMax, rect.yMax), World(rect.xMin, rect.yMax) };
                        if (sign < 0) Array.Reverse(points);
                        int n = vertices.Count;
                        foreach (var point in points)
                        {
                            vertices.Add(box.filter.transform.InverseTransformPoint(point));
                            uvs.Add(SurfaceUv(box, axis, plane, normal, point));
                        }
                        triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                    }
                    if (pieces.Count == 0) removed++;
                }
                if (!modified) continue;
                var mesh = new Mesh { name = "polished-" + i.ToString("D4") + "-" + box.filter.name };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                AssetDatabase.AddObjectToAsset(mesh, library);
                Undo.RecordObject(box.filter, "Preserve collision, trim duplicate render faces");
                box.filter.sharedMesh = mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(box.filter);
                changed++;
            }
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            VerticalChild(background.transform, "20_SurfacePolish");
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
            return "Boxes=" + boxes.Length + "; trimmed meshes=" + changed + "; fully hidden faces=" + removed + "; colliders and walk heights unchanged.";
        }

        static bool IsPolishBox(MeshFilter f)
        {
            var mesh = f.sharedMesh; var renderer = f.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || !renderer.enabled || mesh.subMeshCount != 1 ||
                !(mesh.name == "Cube" || mesh.name.StartsWith("block-") || mesh.name.StartsWith("ConcreteBlock_")) ||
                renderer.sharedMaterial == null || renderer.sharedMaterial.renderQueue > 2500) return false;
            if (!mesh.isReadable || mesh.triangles.Length != 36 || mesh.uv.Length != mesh.vertexCount) return false;
            if (f.transform.localToWorldMatrix.determinant <= 0) return false;
            // A similarly named imported mesh is not necessarily a cuboid. Never replace its silhouette.
            var local = mesh.bounds;
            foreach (var vertex in mesh.vertices)
                for (int axis = 0; axis < 3; axis++)
                    if (Mathf.Min(Mathf.Abs(vertex[axis] - local.min[axis]), Mathf.Abs(vertex[axis] - local.max[axis])) > .0001f) return false;
            if (Mathf.Min(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z) < .04f) return false;
            foreach (var direction in new[] { f.transform.right, f.transform.up, f.transform.forward })
                if (Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)) < .99999f) return false;
            return true;
        }

        static void SubtractSurface(Rect source, Rect cut, List<Rect> result)
        {
            float left = Mathf.Max(source.xMin, cut.xMin), right = Mathf.Min(source.xMax, cut.xMax);
            float bottom = Mathf.Max(source.yMin, cut.yMin), top = Mathf.Min(source.yMax, cut.yMax);
            if (right - left <= SurfaceTolerance || top - bottom <= SurfaceTolerance) { result.Add(source); return; }
            void Add(float x0, float y0, float x1, float y1) { if (x1 - x0 > SurfaceTolerance && y1 - y0 > SurfaceTolerance) result.Add(Rect.MinMaxRect(x0, y0, x1, y1)); }
            Add(source.xMin, source.yMin, left, source.yMax);
            Add(right, source.yMin, source.xMax, source.yMax);
            Add(left, source.yMin, right, bottom);
            Add(left, top, right, source.yMax);
        }

        static Vector2 SurfaceUv(PolishBox box, int axis, float plane, Vector3 normal, Vector3 point)
        {
            for (int k = 0; k < box.triangles.Length; k += 3)
            {
                int i0 = box.triangles[k], i1 = box.triangles[k + 1], i2 = box.triangles[k + 2];
                var a = box.world[i0]; var b = box.world[i1]; var c = box.world[i2];
                if (Mathf.Abs(a[axis] - plane) > .003f || Mathf.Abs(b[axis] - plane) > .003f || Mathf.Abs(c[axis] - plane) > .003f) continue;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) <= 0) continue;
                var ab = b - a; var ac = c - a; var ap = point - a;
                float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cross = Vector3.Dot(ab, ac);
                float denominator = aa * bb - cross * cross;
                if (Mathf.Abs(denominator) < .0000001f) continue;
                float s = (Vector3.Dot(ap, ab) * bb - Vector3.Dot(ap, ac) * cross) / denominator;
                float t = (Vector3.Dot(ap, ac) * aa - Vector3.Dot(ap, ab) * cross) / denominator;
                return box.uv[i0] + (box.uv[i1] - box.uv[i0]) * s + (box.uv[i2] - box.uv[i0]) * t;
            }
            throw new InvalidOperationException("직육면체 표면 UV를 찾지 못했습니다: " + box.filter.name);
        }
    }
}
