using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Scene-only union of proven closed cuboids. Collision, objects and logic keep their identities.</summary>
    public static class EscapeStaticSurfaceOptimization
    {
        public const string ScenePath = "Assets/Scenes/Levels/escape_route.unity";
        public const string LibraryPath = "Assets/Scenes/Levels/EscapeFactoryArt/optimized-solid-surfaces.asset";
        const float Epsilon = .0005f;
        static readonly string[] Scopes = { "EscapeBackground", "EscapePursuer_A", "EscapeArrivalStairs",
            "Pole feedback architecture", "60_VisualFacilityStory", "61_공장 가동 연출", "Industrial processing vignettes", "FacilityContent" };
        sealed class Box
        {
            public MeshRenderer renderer;
            public MeshFilter filter;
            public Mesh mesh;
            public Vector3[] points;
            public int[] indices;
            public Bounds bounds;
            public string id;
        }
        [Serializable] public sealed class Change
        {
            public string id, path;
            public int beforeTriangles, afterTriangles;
            public float removedArea;
            public bool hidden;
        }
        [Serializable] public sealed class Report
        {
            public int activeRenderersBefore, activeRenderersAfter, closedBoxes, modified, hidden;
            public int trianglesBefore, trianglesAfter;
            public float removedArea;
            public string invariantsBefore, invariantsAfter;
            public List<Change> changes = new();
        }
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
        static Scene Scene()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved production escape scene in EditMode required.");
            return scene;
        }
        static HashSet<Object> References(Transform root)
        {
            var result = new HashSet<Object>();
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!script) continue;
                var p = new SerializedObject(script).GetIterator();
                while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue)
                    result.Add(p.objectReferenceValue);
            }
            return result;
        }
        static bool Pure(MeshRenderer r, Transform scope, HashSet<Object> refs)
        {
            // Access audit uses each machinery guard bar's rendered envelope to
            // verify its collider. Keep those authored envelopes individually intact.
            if (r.name.Contains("machine guard bar ") && r.GetComponent<Collider>() || r.name.Contains("wall bracket")) return false;
            var filter = r.GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(filter.sharedMesh)) is ModelImporter)
                return false; // Imported commercial geometry stays in its private source package.
            if (!r.enabled || r.forceRenderingOff || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly
                || !r.gameObject.activeInHierarchy || r.sharedMaterials.Length != 1 || !r.sharedMaterial
                || r.sharedMaterial.renderQueue >= 2450 || r.HasPropertyBlock()
                || r.sharedMaterial.HasProperty("_AlphaClip") && r.sharedMaterial.GetFloat("_AlphaClip") > .5f
                || r.lightmapIndex >= 0 && r.lightmapIndex < 65534
                || refs.Contains(r) || refs.Contains(r.GetComponent<MeshFilter>())) return false;
            for (var t = r.transform; t && t != scope; t = t.parent)
                if (refs.Contains(t) || refs.Contains(t.gameObject) || t.GetComponent<LODGroup>() || t.GetComponent<Rigidbody>()
                    || t.GetComponent<Animation>() || t.GetComponent<Animator>() || t.GetComponents<MonoBehaviour>().Length > 0) return false;
            return true;
        }

        // A bounds box alone is not an occluder: incomplete walls and door openings must never pass this proof.
        public static bool IsClosedBox(Vector3[] points, int[] indices, out Bounds bounds)
        {
            bounds = default;
            if (points.Length == 0 || indices.Length != 36 || points.Any(p => !float.IsFinite(p.sqrMagnitude))) return false;
            bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p);
            if (bounds.size.x < .002f || bounds.size.y < .002f || bounds.size.z < .002f) return false;
            var area = new double[6]; var counts = new int[6];
            var corners = Enumerable.Range(0, 6).Select(_ => new List<HashSet<int>>()).ToArray();
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = points[indices[i]]; var b = points[indices[i + 1]]; var c = points[indices[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                int axis = Mathf.Abs(normal.x) > Mathf.Abs(normal.y) ? 0 : 1;
                if (Mathf.Abs(normal.z) > Mathf.Abs(normal[axis])) axis = 2;
                int side = normal[axis] > 0 ? 1 : 0;
                float plane = side == 1 ? bounds.max[axis] : bounds.min[axis];
                if (normal.sqrMagnitude < 1e-12f || Mathf.Abs(normal.normalized[axis]) < .99999f
                    || Mathf.Abs(a[axis] - plane) > Epsilon || Mathf.Abs(b[axis] - plane) > Epsilon || Mathf.Abs(c[axis] - plane) > Epsilon) return false;
                var codes = new HashSet<int>();
                foreach (var point in new[] { a, b, c })
                {
                    int code = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        if (Mathf.Abs(point[k] - bounds.max[k]) <= Epsilon) code |= 1 << k;
                        else if (Mathf.Abs(point[k] - bounds.min[k]) > Epsilon) return false;
                    }
                    codes.Add(code);
                }
                corners[axis * 2 + side].Add(codes);
                area[axis * 2 + side] += normal.magnitude * .5; counts[axis * 2 + side]++;
            }
            for (int axis = 0; axis < 3; axis++) for (int side = 0; side < 2; side++)
            {
                double expected = bounds.size[(axis + 1) % 3] * bounds.size[(axis + 2) % 3];
                if (counts[axis * 2 + side] != 2 || Math.Abs(area[axis * 2 + side] - expected) > Math.Max(.00001, expected * .0001)) return false;
                var shared = corners[axis * 2 + side][0].Intersect(corners[axis * 2 + side][1]).ToArray();
                // Equal triangle area is not enough: two overlapping half-faces can
                // have the right sum while leaving a hole. They must share the diagonal.
                if (shared.Length != 2 || (shared[0] ^ shared[1]) != (7 ^ (1 << axis))) return false;
            }
            return true;
        }
        static List<Box> Boxes(Transform frame)
        {
            var refs = References(frame); var boxes = new List<Box>();
            foreach (string name in Scopes)
            {
                var scope = frame.Find(name); if (!scope) continue;
                foreach (var r in scope.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!Pure(r, scope, refs)) continue;
                    var f = r.GetComponent<MeshFilter>(); var mesh = f ? f.sharedMesh : null;
                    if (!mesh || !mesh.isReadable || mesh.subMeshCount != 1 || mesh.uv.Length != mesh.vertexCount) continue;
                    var points = mesh.vertices.Select(f.transform.TransformPoint).ToArray(); var indices = mesh.triangles;
                    if (!IsClosedBox(points, indices, out var bounds)) continue;
                    boxes.Add(new Box { renderer = r, filter = f, mesh = mesh, points = points, indices = indices,
                        bounds = bounds, id = GlobalObjectId.GetGlobalObjectIdSlow(r).ToString() });
                }
            }
            return boxes.OrderBy(b => b.id, StringComparer.Ordinal).ToList();
        }

        public static List<Rect> Subtract(Rect source, Rect cut)
        {
            float x0 = Mathf.Max(source.xMin, cut.xMin), x1 = Mathf.Min(source.xMax, cut.xMax);
            float y0 = Mathf.Max(source.yMin, cut.yMin), y1 = Mathf.Min(source.yMax, cut.yMax);
            if (x1 <= x0 || y1 <= y0) return new List<Rect> { source };
            var output = new List<Rect>();
            void Add(float a, float b, float c, float d) { if (c - a > .000001f && d - b > .000001f) output.Add(Rect.MinMaxRect(a, b, c, d)); }
            Add(source.xMin, source.yMin, x0, source.yMax); Add(x1, source.yMin, source.xMax, source.yMax);
            Add(x0, source.yMin, x1, y0); Add(x0, y1, x1, source.yMax); return output;
        }
        public static bool Occludes(Bounds other, int axis, int sign, float plane, bool ownsCoplanar)
        {
            if (other.min[axis] > plane + Epsilon || other.max[axis] < plane - Epsilon) return false;
            float outside = sign > 0 ? other.max[axis] - plane : plane - other.min[axis];
            return outside > Epsilon || ownsCoplanar && Mathf.Abs(outside) <= Epsilon;
        }
        public static List<Rect> MergeAdjacent(List<Rect> pieces)
        {
            // Subtraction can split a single remaining strip several times. Join only
            // exact shared edges, never bridge a hole or a physical gap.
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < pieces.Count && !changed; i++)
                    for (int j = i + 1; j < pieces.Count; j++)
                    {
                        var a = pieces[i]; var b = pieces[j];
                        bool horizontal = a.yMin == b.yMin && a.yMax == b.yMax && (a.xMax == b.xMin || b.xMax == a.xMin);
                        bool vertical = a.xMin == b.xMin && a.xMax == b.xMax && (a.yMax == b.yMin || b.yMax == a.yMin);
                        if (!horizontal && !vertical) continue;
                        pieces[i] = Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));
                        pieces.RemoveAt(j); changed = true; break;
                    }
            } while (changed);
            return pieces;
        }
        static List<Rect>[] Faces(Box source, List<Box> neighbors, List<Box> all)
        {
            var faces = new List<Rect>[6]; int index = all.IndexOf(source);
            for (int axis = 0; axis < 3; axis++) for (int side = 0; side < 2; side++)
            {
                int sign = side == 1 ? 1 : -1, u = (axis + 1) % 3, v = (axis + 2) % 3;
                float plane = side == 1 ? source.bounds.max[axis] : source.bounds.min[axis];
                var pieces = new List<Rect> { Rect.MinMaxRect(source.bounds.min[u], source.bounds.min[v], source.bounds.max[u], source.bounds.max[v]) };
                var exterior = new List<Rect>(pieces);
                foreach (var other in neighbors)
                {
                    bool owns = all.IndexOf(other) < index && other.renderer.sharedMaterial == source.renderer.sharedMaterial;
                    if (!Occludes(other.bounds, axis, sign, plane, owns)) continue;
                    var cut = Rect.MinMaxRect(other.bounds.min[u], other.bounds.min[v], other.bounds.max[u], other.bounds.max[v]);
                    pieces = pieces.SelectMany(p => Subtract(p, cut)).ToList();
                    float otherPlane = side == 1 ? other.bounds.max[axis] : other.bounds.min[axis];
                    if (owns && Mathf.Abs(otherPlane - plane) <= Epsilon)
                        exterior = exterior.SelectMany(p => Subtract(p, cut)).ToList();
                }
                pieces = MergeAdjacent(pieces); exterior = MergeAdjacent(exterior);
                // Tiny buried cuts in beams can multiply triangles without saving a
                // draw call. Keep that original interior instead; always fix shared
                // exterior planes and always remove completely buried faces.
                faces[axis * 2 + side] = pieces.Count <= Math.Max(1, exterior.Count) ? pieces : exterior;
            }
            return faces;
        }
        internal static bool HasRenderableArea(Vector3 a, Vector3 b, Vector3 c, Matrix4x4 toWorld)
        {
            Vector3 ab = b - a, ac = c - a;
            if (Vector3.Cross(ab, ac).sqrMagnitude > 1e-16f) return true;
            // A thin local triangle can still have area on a highly scaled wall.
            // Verify its world-space altitude before discarding a clipping sliver.
            ab = toWorld.MultiplyVector(ab);
            ac = toWorld.MultiplyVector(ac);
            return Vector3.Cross(ab, ac).magnitude > .00001f * Mathf.Max(ab.magnitude, ac.magnitude, (ab - ac).magnitude);
        }

        static Mesh Build(Box box, List<Rect>[] faces)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var uv = Enumerable.Range(0, 8).Select(_ => new List<Vector4>()).ToArray();
            var originalUv = Enumerable.Range(0, 8).Select(_ => new List<Vector4>()).ToArray();
            for (int ch = 0; ch < 8; ch++) box.mesh.GetUVs(ch, originalUv[ch]);
            var colors = box.mesh.colors; var outputColors = new List<Color>();
            for (int axis = 0; axis < 3; axis++) for (int side = 0; side < 2; side++)
            {
                int sign = side == 1 ? 1 : -1, u = (axis + 1) % 3, v = (axis + 2) % 3;
                float plane = side == 1 ? box.bounds.max[axis] : box.bounds.min[axis];
                int a = -1, b = 0, c = 0;
                for (int t = 0; t < box.indices.Length; t += 3)
                {
                    int x = box.indices[t], y = box.indices[t + 1], z = box.indices[t + 2];
                    var n = Vector3.Cross(box.points[y] - box.points[x], box.points[z] - box.points[x]).normalized;
                    if (n[axis] * sign > .9999f) { a = x; b = y; c = z; break; }
                }
                if (a < 0) throw new InvalidOperationException("Missing proven cube face");
                var e = box.points[b] - box.points[a]; var f = box.points[c] - box.points[a];
                double divisor = (double)e[u] * f[v] - (double)e[v] * f[u];
                foreach (var rect in faces[axis * 2 + side])
                {
                    Vector3 Point(float x, float y) { var p = Vector3.zero; p[axis] = plane; p[u] = x; p[v] = y; return p; }
                    var points = new[] { Point(rect.xMin, rect.yMin), Point(rect.xMax, rect.yMin), Point(rect.xMax, rect.yMax), Point(rect.xMin, rect.yMax) };
                    if (sign < 0) Array.Reverse(points);
                    var local = points.Select(box.filter.transform.InverseTransformPoint).ToArray();
                    var world = box.filter.transform.localToWorldMatrix;
                    if (!HasRenderableArea(local[0], local[1], local[2], world)
                        || !HasRenderableArea(local[0], local[2], local[3], world)) continue;
                    int first = vertices.Count;
                    for (int vertex = 0; vertex < points.Length; vertex++)
                    {
                        var d = points[vertex] - box.points[a];
                        float beta = (float)(((double)d[u] * f[v] - (double)d[v] * f[u]) / divisor);
                        float gamma = (float)(((double)e[u] * d[v] - (double)e[v] * d[u]) / divisor), alpha = 1 - beta - gamma;
                        vertices.Add(local[vertex]);
                        for (int ch = 0; ch < 8; ch++) if (originalUv[ch].Count == box.mesh.vertexCount)
                            uv[ch].Add(originalUv[ch][a] * alpha + originalUv[ch][b] * beta + originalUv[ch][c] * gamma);
                        if (colors.Length == box.mesh.vertexCount) outputColors.Add(colors[a] * alpha + colors[b] * beta + colors[c] * gamma);
                    }
                    triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            }
            if (triangles.Count == 0) return null;
            var mesh = new Mesh { name = "Optimized solid " + box.renderer.name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            for (int ch = 0; ch < 8; ch++) if (uv[ch].Count == vertices.Count) mesh.SetUVs(ch, uv[ch]);
            if (outputColors.Count == vertices.Count) mesh.SetColors(outputColors);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
        public static string Invariants(Transform root)
        {
            var values = new List<string>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (!c || !(c is Transform || c is Collider || c is MonoBehaviour || c is Light)) continue;
                string serialized = EditorJsonUtility.ToJson(c);
                serialized = Regex.Replace(serialized, "\"instanceID\":\\s*(-?\\d+)", match => {
                    var target = EditorUtility.InstanceIDToObject(int.Parse(match.Groups[1].Value));
                    return "\"object\":\"" + (target ? GlobalObjectId.GetGlobalObjectIdSlow(target).ToString() : "null") + "\""; });
                values.Add(GlobalObjectId.GetGlobalObjectIdSlow(c) + ":" + serialized);
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", values)))).Replace("-", "");
        }
        public static Report Run(bool apply, string reportPath)
        {
            var scene = Scene(); var frame = scene.GetRootGameObjects().Single().transform;
            if (apply && AssetDatabase.LoadMainAssetAtPath(LibraryPath)) throw new InvalidOperationException("Optimization library already exists; inspect before repeating.");
            var boxes = Boxes(frame); var report = new Report { closedBoxes = boxes.Count, invariantsBefore = Invariants(frame),
                activeRenderersBefore = frame.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy) };
            Mesh library = null;
            foreach (var source in boxes)
            {
                var extent = source.bounds; extent.Expand(Epsilon * 2);
                var neighbors = boxes.Where(b => b != source && extent.Intersects(b.bounds)).ToList();
                if (neighbors.Count == 0) continue;
                var faces = Faces(source, neighbors, boxes);
                float before = 2 * (source.bounds.size.x * source.bounds.size.y + source.bounds.size.y * source.bounds.size.z + source.bounds.size.z * source.bounds.size.x);
                float after = faces.Sum(face => face.Sum(r => r.width * r.height));
                if (before - after < .00001f) continue;
                bool hidden = faces.All(face => face.Count == 0); int count = faces.Sum(face => face.Count) * 2;
                // Story-shell access auditing requires each authored barrier to keep
                // its own visible envelope, even where an end bar is buried in a jamb.
                if (hidden && source.renderer.GetComponent<Collider>() && source.renderer.transform.parent
                    && source.renderer.transform.parent.Find("Human conversion evidence")) continue;
                report.changes.Add(new Change { id = source.id, path = PathOf(source.renderer.transform), beforeTriangles = 12,
                    afterTriangles = count, removedArea = before - after, hidden = hidden });
                report.modified++; if (hidden) report.hidden++;
                report.trianglesBefore += 12; report.trianglesAfter += count; report.removedArea += before - after;
                if (!apply) continue;
                if (hidden)
                {
                    Undo.RecordObject(source.renderer, "Remove fully covered escape rendering"); source.renderer.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(source.renderer);
                }
                else
                {
                    var mesh = Build(source, faces);
                    if (!mesh) continue; // Keep the source when no remaining face survived conversion.
                    if (!library) { library = new Mesh { name = "Escape optimized solid surfaces" }; AssetDatabase.CreateAsset(library, LibraryPath); }
                    AssetDatabase.AddObjectToAsset(mesh, library);
                    Undo.RecordObject(source.filter, "Trim internal and duplicate escape faces"); source.filter.sharedMesh = mesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(source.filter);
                }
            }
            report.invariantsAfter = Invariants(frame);
            report.activeRenderersAfter = frame.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy);
            if (report.invariantsBefore != report.invariantsAfter) throw new InvalidOperationException("Geometry edit changed collision, transforms, lights or gameplay references; do not save.");
            if (apply && report.modified > 0)
            {
                if (library) { EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library); }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); return report;
        }
    }
}
