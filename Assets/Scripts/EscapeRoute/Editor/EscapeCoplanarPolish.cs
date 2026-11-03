using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Remove only the shared render area. Authored transforms, collision and remaining UVs stay in place.</summary>
    public static class EscapeCoplanarPolish
    {
        [Serializable] public sealed class Rule
        {
            public string target, occluder;
            public int axis, sign;
            public float plane, otherPlane;
            public bool self;
        }
        [Serializable] public sealed class Plan { public Rule[] rules; }
        sealed class Source
        {
            public MeshFilter filter;
            public Mesh mesh;
            public Vector3[] points;
            public int[][] triangles;
            public Matrix4x4 fromFrame;
        }
        sealed class CutTriangle { public Vector3[] points; public int ordinal; }
        sealed class Cut { public Rule rule; public CutTriangle[] triangles; }

        public static double Area(IReadOnlyList<Vector3> polygon, int axis)
        {
            double area = 0; int u = (axis + 1) % 3, v = (axis + 2) % 3;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                area += (double)a[u] * b[v] - (double)a[v] * b[u];
            }
            return Math.Abs(area) * .5;
        }

        public static List<List<Vector3>> Subtract(List<Vector3> subject, Vector3[] triangle, int axis)
        {
            int u = (axis + 1) % 3, v = (axis + 2) % 3;
            double winding = ((double)triangle[1][u] - triangle[0][u]) * (triangle[2][v] - triangle[0][v])
                - ((double)triangle[1][v] - triangle[0][v]) * (triangle[2][u] - triangle[0][u]);
            if (Math.Abs(winding) < 1e-10) return new List<List<Vector3>> { subject };
            double sign = Math.Sign(winding); var pieces = new List<List<Vector3>>(); var remaining = subject;
            for (int edge = 0; edge < 3 && remaining.Count >= 3; edge++)
            {
                var a = triangle[edge]; var b = triangle[(edge + 1) % 3];
                double Distance(Vector3 p) => sign * (((double)b[u] - a[u]) * (p[v] - a[v])
                    - ((double)b[v] - a[v]) * (p[u] - a[u]));
                List<Vector3> Clip(bool inside)
                {
                    var output = new List<Vector3>(); var previous = remaining[remaining.Count - 1]; double d = Distance(previous);
                    foreach (var point in remaining)
                    {
                        double next = Distance(point);
                        bool before = inside ? d >= 0 : d <= 0, after = inside ? next >= 0 : next <= 0;
                        if (before != after) output.Add(Vector3.LerpUnclamped(previous, point, (float)(d / (d - next))));
                        if (after) output.Add(point);
                        previous = point; d = next;
                    }
                    return output;
                }
                var outside = Clip(false); if (outside.Count >= 3 && Area(outside, axis) > 1e-8) pieces.Add(outside);
                remaining = Clip(true);
            }
            return pieces;
        }

        static bool Matches(Vector3 a, Vector3 b, Vector3 c, Rule rule, float plane)
        {
            var normal = Vector3.Cross(b - a, c - a);
            return normal.sqrMagnitude > 1e-12f && normal.normalized[rule.axis] * rule.sign > .99999f
                && Mathf.Abs(a[rule.axis] - plane) < .0011f && Mathf.Abs(b[rule.axis] - plane) < .0011f
                && Mathf.Abs(c[rule.axis] - plane) < .0011f;
        }
        static bool Overlap(IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b, int axis)
        {
            int u = (axis + 1) % 3, v = (axis + 2) % 3;
            return Math.Min(a.Max(p => p[u]), b.Max(p => p[u])) - Math.Max(a.Min(p => p[u]), b.Min(p => p[u])) > .00001f
                && Math.Min(a.Max(p => p[v]), b.Max(p => p[v])) - Math.Max(a.Min(p => p[v]), b.Min(p => p[v])) > .00001f;
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/'); Folder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }

        public static string Apply(string planPath)
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.isDirty || scene.path != "Assets/Scenes/Levels/escape_route.unity")
                throw new InvalidOperationException("Saved A edit scene required");
            var frame = scene.GetRootGameObjects().Single().transform;
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(planPath));
            const string folder = "Assets/_ThirdParty/Task073/SurfaceReview"; Folder(folder);
            var sources = new Dictionary<string, Source>();
            foreach (string id in plan.rules.SelectMany(r => new[] { r.target, r.occluder }).Distinct())
            {
                if (!GlobalObjectId.TryParse(id, out var global)
                    || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global) is not MeshFilter filter || filter.gameObject.scene != scene)
                    throw new InvalidOperationException("Surface owner no longer matches the reviewed scene");
                var mesh = filter.sharedMesh;
                sources[id] = new Source { filter = filter, mesh = mesh,
                    points = mesh.vertices.Select(p => frame.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray(),
                    triangles = Enumerable.Range(0, mesh.subMeshCount).Select(mesh.GetTriangles).ToArray(),
                    fromFrame = filter.transform.worldToLocalMatrix * frame.localToWorldMatrix };
            }
            int meshes = 0, trimmed = 0;
            foreach (var group in plan.rules.GroupBy(r => r.target))
            {
                var source = sources[group.Key]; int count = source.mesh.vertexCount;
                var cuts = group.Select(rule => { var other = sources[rule.occluder]; var indices = other.triangles.SelectMany(t => t).ToArray();
                    var triangles = new List<CutTriangle>();
                    for (int t = 0; t < indices.Length; t += 3)
                    {
                        var a = other.points[indices[t]]; var b = other.points[indices[t + 1]]; var c = other.points[indices[t + 2]];
                        if (Matches(a, b, c, rule, rule.otherPlane)) triangles.Add(new CutTriangle { points = new[] { a, b, c }, ordinal = t / 3 });
                    }
                    return new Cut { rule = rule, triangles = triangles.ToArray() }; }).ToArray();
                var vertices = source.mesh.vertices.ToList(); var normals = source.mesh.normals.ToList();
                var tangents = source.mesh.tangents.ToList(); var colors = source.mesh.colors.ToList();
                var uv = Enumerable.Range(0, 8).Select(_ => new List<Vector4>()).ToArray();
                for (int channel = 0; channel < 8; channel++) source.mesh.GetUVs(channel, uv[channel]);
                bool hasNormals = normals.Count == count, hasTangents = tangents.Count == count, hasColors = colors.Count == count;
                var validUv = uv.Select(c => c.Count == count).ToArray();
                int Append(Vector3 point, int a, int b, int c, int axis)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3; var x = source.points[a];
                    var e = source.points[b] - x; var f = source.points[c] - x; var p = point - x;
                    double denominator = (double)e[u] * f[v] - (double)e[v] * f[u];
                    float beta = (float)(((double)p[u] * f[v] - (double)p[v] * f[u]) / denominator);
                    float gamma = (float)(((double)e[u] * p[v] - (double)e[v] * p[u]) / denominator), alpha = 1 - beta - gamma;
                    int index = vertices.Count; vertices.Add(source.fromFrame.MultiplyPoint3x4(point));
                    if (hasNormals) normals.Add((normals[a] * alpha + normals[b] * beta + normals[c] * gamma).normalized);
                    if (hasTangents) tangents.Add(tangents[a] * alpha + tangents[b] * beta + tangents[c] * gamma);
                    if (hasColors) colors.Add(colors[a] * alpha + colors[b] * beta + colors[c] * gamma);
                    for (int ch = 0; ch < 8; ch++) if (validUv[ch]) uv[ch].Add(uv[ch][a] * alpha + uv[ch][b] * beta + uv[ch][c] * gamma);
                    return index;
                }
                var submeshes = new List<int>[source.triangles.Length]; int changed = 0;
                for (int sub = 0; sub < submeshes.Length; sub++)
                {
                    var output = submeshes[sub] = new List<int>(); var indices = source.triangles[sub];
                    int ordinalOffset = source.triangles.Take(sub).Sum(t => t.Length / 3);
                    for (int t = 0; t < indices.Length; t += 3)
                    {
                        int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                        var triangle = new List<Vector3> { source.points[a], source.points[b], source.points[c] };
                        var applicable = cuts.Where(cut => Matches(triangle[0], triangle[1], triangle[2], cut.rule, cut.rule.plane)).ToArray();
                        if (applicable.Length == 0) { output.AddRange(new[] { a, b, c }); continue; }
                        int axis = applicable[0].rule.axis; var pieces = new List<List<Vector3>> { triangle };
                        foreach (var cut in applicable) foreach (var cutter in cut.triangles)
                        {
                            if (cut.rule.self && cutter.ordinal >= ordinalOffset + t / 3) continue;
                            var next = new List<List<Vector3>>();
                            foreach (var piece in pieces)
                                if (Overlap(piece, cutter.points, axis)) next.AddRange(Subtract(piece, cutter.points, axis)); else next.Add(piece);
                            pieces = next;
                        }
                        if (Area(triangle, axis) - pieces.Sum(p => Area(p, axis)) < 1e-6) { output.AddRange(new[] { a, b, c }); continue; }
                        changed++;
                        foreach (var piece in pieces)
                        {
                            int first = Append(piece[0], a, b, c, axis);
                            for (int i = 1; i + 1 < piece.Count; i++)
                                if (Area(new[] { piece[0], piece[i], piece[i + 1] }, axis) > 1e-8)
                                    output.AddRange(new[] { first, Append(piece[i], a, b, c, axis), Append(piece[i + 1], a, b, c, axis) });
                        }
                    }
                }
                if (changed == 0) continue;
                string path = folder + "/" + Path.GetFileNameWithoutExtension(planPath) + "-" + source.filter.GetInstanceID() + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) throw new InvalidOperationException("Surface asset already exists");
                var result = new Mesh { name = "Reviewed surface " + source.filter.name,
                    indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : source.mesh.indexFormat };
                result.SetVertices(vertices); if (hasNormals) result.SetNormals(normals); if (hasTangents) result.SetTangents(tangents);
                if (hasColors) result.SetColors(colors);
                for (int ch = 0; ch < 8; ch++) if (validUv[ch]) result.SetUVs(ch, uv[ch]);
                result.subMeshCount = submeshes.Length;
                for (int sub = 0; sub < submeshes.Length; sub++) result.SetTriangles(submeshes[sub], sub, false);
                result.RecalculateBounds(); if (!hasNormals) result.RecalculateNormals();
                AssetDatabase.CreateAsset(result, path); Undo.RecordObject(source.filter, "Remove shared coplanar render area");
                source.filter.sharedMesh = result; PrefabUtility.RecordPrefabInstancePropertyModifications(source.filter);
                meshes++; trimmed += changed;
            }
            Undo.FlushUndoRecordObjects(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); return "Reviewed meshes=" + meshes + "; trimmed triangles=" + trimmed;
        }
    }
}
