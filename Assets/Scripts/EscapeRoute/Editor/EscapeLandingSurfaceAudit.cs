using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Read-only geometry check. Gameplay components and serialized references never exclude a landing floor.</summary>
    public static class EscapeLandingSurfaceAudit
    {
        public static void ReadGeometry(Mesh mesh, out Vector3[] vertices, out int[][] triangles)
        {
            // Editor-only access also covers imported meshes with Read/Write disabled.
            // Do not reimport them or change runtime memory settings for an inspection.
            using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
            if (data[0].vertexCount == 0)
            {
                vertices = Array.Empty<Vector3>();
                triangles = Enumerable.Range(0, data[0].subMeshCount).Select(_ => Array.Empty<int>()).ToArray();
                return;
            }
            using var points = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
            data[0].GetVertices(points);
            vertices = points.ToArray();
            triangles = new int[data[0].subMeshCount][];
            for (int sub = 0; sub < triangles.Length; sub++)
            {
                using var indices = new NativeArray<int>(data[0].GetSubMesh(sub).indexCount, Allocator.Temp);
                data[0].GetIndices(indices, sub);
                triangles[sub] = indices.ToArray();
            }
        }

        sealed class Face
        {
            public Vector3[] points;
            public float height;
            public Rect projection;
        }

        static List<Face> Tops(MeshFilter filter, Transform frame)
        {
            var result = new List<Face>();
            var mesh = filter.sharedMesh;
            if (!mesh) return result;
            ReadGeometry(mesh, out var source, out var submeshes);
            var vertices = source.Select(p => frame.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray();
            var renderer = filter.GetComponent<MeshRenderer>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var materials = renderer.sharedMaterials;
                var material = materials.Length > 0 ? materials[Math.Min(sub, materials.Length - 1)] : null;
                if (!material || material.renderQueue >= 2450) continue;
                var indices = submeshes[sub];
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var p = new[] { vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]] };
                    var normal = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                    if (normal.sqrMagnitude < 1e-10f || normal.normalized.y < .99999f
                        || p.Max(v => v.y) - p.Min(v => v.y) > .0005f) continue;
                    result.Add(new Face { points = p, height = p.Average(v => v.y),
                        projection = Rect.MinMaxRect(p.Min(v => v.z), p.Min(v => v.x), p.Max(v => v.z), p.Max(v => v.x)) });
                }
            }
            return result;
        }

        public static string[] FindCoincidentLandingFaces(Transform frame)
        {
            var renderers = frame.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy)
                .Where(r => !r.GetComponentInParent<LODGroup>() || r.GetComponentInParent<LODGroup>().GetLODs()[0].renderers.Contains(r))
                .Where(r => r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var cache = new Dictionary<MeshRenderer, List<Face>>();
            List<Face> Faces(MeshRenderer r)
            {
                if (!cache.TryGetValue(r, out var faces)) cache[r] = faces = Tops(r.GetComponent<MeshFilter>(), frame);
                return faces;
            }
            var findings = new List<string>();
            foreach (var landing in renderers.Where(r => r.GetComponent<HumanCannonLandingZone>()))
            {
                var bounds = landing.bounds; bounds.Expand(.001f);
                foreach (var other in renderers.Where(r => r != landing && bounds.Intersects(r.bounds)))
                {
                    double area = 0;
                    foreach (var a in Faces(landing)) foreach (var b in Faces(other))
                    {
                        if (Mathf.Abs(a.height - b.height) > .0005f || !a.projection.Overlaps(b.projection)) continue;
                        area += EscapeCoplanarPolish.Area(a.points, 1)
                            - EscapeCoplanarPolish.Subtract(a.points.ToList(), b.points, 1).Sum(p => EscapeCoplanarPolish.Area(p, 1));
                    }
                    if (area > .01) findings.Add($"{landing.name} / {other.name}: {area:F4} m²");
                }
            }
            return findings.ToArray();
        }
    }
}
