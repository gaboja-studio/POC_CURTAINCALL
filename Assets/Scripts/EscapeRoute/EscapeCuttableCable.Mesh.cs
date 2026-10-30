using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeCuttableCable
    {
        void BuildMesh(float progress)
        {
            if (mesh == null) return;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var shell = new List<int>(); var copper = new List<int>();
            if (!cut) Tube(path, vertices, normals, uv, shell, copper);
            else if (path.Length >= 2)
            {
                var left = new List<Vector3>(); var right = new List<Vector3>();
                float ease = progress * progress * (3 - 2 * progress);
                float wobble = Mathf.Sin(progress * Mathf.PI * 4) * Mathf.Exp(-progress * 4) * .16f;
                Vector3 tangent = (path[cutSegment + 1] - path[cutSegment]).normalized;
                Vector3 down = transform.InverseTransformVector(Vector3.down).normalized;
                Vector3 side = Vector3.Cross(tangent, down).normalized;
                if (side.sqrMagnitude < .1f) side = Vector3.right;
                Vector3 leftOffset = -tangent * (.035f + breakGap * .5f * ease) + down * (droopDistance * ease) + side * wobble;
                Vector3 rightOffset = tangent * (.035f + breakGap * .5f * ease) + down * (droopDistance * ease) - side * wobble;
                float leftBend = Mathf.Max(.001f, Mathf.Min(1.8f, cutDistance));
                float rightBend = Mathf.Max(.001f, Mathf.Min(1.8f, lengths[lengths.Length - 1] - cutDistance));
                for (int i = 0; i <= cutSegment; i++)
                {
                    float weight = Mathf.Pow(Mathf.Clamp01(1 - (cutDistance - lengths[i]) / leftBend), 2);
                    AddPoint(left, path[i] + leftOffset * (i == 0 ? 0 : weight));
                }
                AddPoint(left, cutLocal + leftOffset);
                right.Add(cutLocal + rightOffset);
                for (int i = cutSegment + 1; i < path.Length; i++)
                {
                    float weight = Mathf.Pow(Mathf.Clamp01(1 - (lengths[i] - cutDistance) / rightBend), 2);
                    AddPoint(right, path[i] + rightOffset * (i == path.Length - 1 ? 0 : weight));
                }
                Tube(left, vertices, normals, uv, shell, copper);
                Tube(right, vertices, normals, uv, shell, copper);
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2; mesh.SetTriangles(shell, 0); mesh.SetTriangles(copper, 1); mesh.RecalculateBounds();
        }

        static void AddPoint(List<Vector3> points, Vector3 value)
        {
            if (points.Count == 0 || (points[points.Count - 1] - value).sqrMagnitude > .0000001f) points.Add(value);
        }

        void Tube(IReadOnlyList<Vector3> points, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv,
            List<int> shell, List<int> copper)
        {
            if (points.Count < 2) return;
            int sides = Mathf.Clamp(radialSegments, 6, 12), first = vertices.Count;
            Vector3 previousTangent = Vector3.forward, normal = Vector3.right;
            float distance = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 tangent = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)];
                if (tangent.sqrMagnitude < .0000001f) tangent = previousTangent;
                tangent.Normalize();
                normal = i == 0 ? Vector3.Cross(tangent, Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > .95f ? Vector3.right : Vector3.up).normalized
                    : Vector3.ProjectOnPlane(Quaternion.FromToRotation(previousTangent, tangent) * normal, tangent).normalized;
                Vector3 across = Vector3.Cross(tangent, normal).normalized;
                if (i > 0) distance += Vector3.Distance(points[i - 1], points[i]);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    Vector3 radial = normal * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                    vertices.Add(points[i] + radial * radius); normals.Add(radial); uv.Add(new Vector2((float)side / sides, distance));
                    if (i == 0) continue;
                    int a = first + (i - 1) * sides + side, b = first + (i - 1) * sides + (side + 1) % sides;
                    int c = first + i * sides + side, d = first + i * sides + (side + 1) % sides;
                    shell.Add(a); shell.Add(b); shell.Add(c); shell.Add(b); shell.Add(d); shell.Add(c);
                }
                previousTangent = tangent;
            }
            Cap(0, -(points[1] - points[0]).normalized, true);
            Cap(points.Count - 1, (points[points.Count - 1] - points[points.Count - 2]).normalized, false);
            void Cap(int ring, Vector3 outward, bool reverse)
            {
                int center = vertices.Count; vertices.Add(points[ring]); normals.Add(outward); uv.Add(Vector2.one * .5f);
                for (int side = 0; side < sides; side++)
                { vertices.Add(vertices[first + ring * sides + side]); normals.Add(outward); uv.Add(Vector2.zero); }
                for (int side = 0; side < sides; side++)
                {
                    int a = center + 1 + side, b = center + 1 + (side + 1) % sides;
                    copper.Add(center); copper.Add(reverse ? b : a); copper.Add(reverse ? a : b);
                }
            }
        }
    }
}
