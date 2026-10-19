#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Player.Validation.Editor
{
    /// <summary>원본 바인드 포즈에서 지정한 팔다리 영역을 분리하는 검증 전용 빌더.</summary>
    public static class FixedLegMeshBuilder
    {
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Uv;
            public BoneWeight Weight;
        }

        private sealed class Triangle
        {
            public readonly int[] Indices;
            public readonly int Submesh;
            public bool Selected;
            public Triangle(int a, int b, int c, int submesh)
            {
                Indices = new[] { a, b, c };
                Submesh = submesh;
            }
        }

        private readonly struct Edge : IEquatable<Edge>
        {
            public readonly int A;
            public readonly int B;
            public Edge(int a, int b) { A = Math.Min(a, b); B = Math.Max(a, b); }
            public bool Equals(Edge other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is Edge other && Equals(other);
            public override int GetHashCode() => unchecked(A * 397 ^ B);
        }

        // 인접 공간 셀까지 검사하여 셀 경계에서도 같은 위치를 용접한다.
        private sealed class Weld
        {
            private readonly float epsilon;
            private readonly Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
            public readonly List<Vector3> Positions = new List<Vector3>();
            public Weld(float epsilon) { this.epsilon = epsilon; }
            public int Add(Vector3 p)
            {
                var cell = new Vector3Int(Mathf.FloorToInt(p.x / epsilon), Mathf.FloorToInt(p.y / epsilon), Mathf.FloorToInt(p.z / epsilon));
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!cells.TryGetValue(cell + new Vector3Int(x, y, z), out var ids)) continue;
                    foreach (int id in ids)
                        if ((Positions[id] - p).sqrMagnitude <= epsilon * epsilon) return id;
                }
                int next = Positions.Count;
                Positions.Add(p);
                if (!cells.TryGetValue(cell, out var bucket)) cells.Add(cell, bucket = new List<int>());
                bucket.Add(next);
                return next;
            }
        }

        private sealed class Output
        {
            public readonly List<Vertex> Vertices = new List<Vertex>();
            public readonly List<int>[] Submeshes;
            public Output(int count)
            {
                Submeshes = new List<int>[count];
                for (int i = 0; i < count; i++) Submeshes[i] = new List<int>();
            }
            public void Polygon(List<Vertex> polygon, int submesh, float areaTolerance)
            {
                for (int i = 1; i + 1 < polygon.Count; i++)
                    AddTriangle(polygon[0], polygon[i], polygon[i + 1], submesh, areaTolerance);
            }
            public void AddTriangle(Vertex a, Vertex b, Vertex c, int submesh, float areaTolerance)
            {
                if (Vector3.Cross(b.Position - a.Position, c.Position - a.Position).sqrMagnitude <= areaTolerance)
                    throw new InvalidOperationException("퇴화 삼각형이 발생했습니다. 절단 위치 또는 원본 메시를 확인하세요.");
                int start = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                Submeshes[submesh].Add(start); Submeshes[submesh].Add(start + 1); Submeshes[submesh].Add(start + 2);
            }
            public Mesh Create(string name, Matrix4x4[] bindposes, int boneCount)
            {
                if (Vertices.Count == 0) throw new InvalidOperationException("절단 결과의 한쪽 메시가 비어 있습니다.");
                var positions = new Vector3[Vertices.Count];
                var normals = new Vector3[Vertices.Count];
                var uvs = new Vector2[Vertices.Count];
                var weights = new BoneWeight[Vertices.Count];
                for (int i = 0; i < Vertices.Count; i++)
                {
                    Vertex v = Vertices[i];
                    Validate(v, boneCount);
                    positions[i] = v.Position; normals[i] = v.Normal; uvs[i] = v.Uv; weights[i] = v.Weight;
                }
                var mesh = new Mesh { name = name, indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                try
                {
                    mesh.vertices = positions; mesh.normals = normals; mesh.uv = uvs;
                    mesh.boneWeights = weights; mesh.bindposes = bindposes;
                    mesh.subMeshCount = Submeshes.Length;
                    for (int s = 0; s < Submeshes.Length; s++)
                    {
                        foreach (int index in Submeshes[s])
                            if (index < 0 || index >= Vertices.Count) throw new InvalidOperationException("결과 인덱스 범위를 벗어났습니다.");
                        mesh.SetTriangles(Submeshes[s], s, false);
                    }
                    mesh.RecalculateBounds();
                    return mesh;
                }
                catch { UnityEngine.Object.DestroyImmediate(mesh); throw; }
            }
        }

        public static (Mesh body, Mesh leg, string report) Build(SkinnedMeshRenderer skin, Transform upperLeg, Transform lowerLeg, float fraction = 0.45f)
        {
            if (skin == null || upperLeg == null || lowerLeg == null)
                throw new ArgumentNullException("렌더러와 상부/하부 관절 참조가 필요합니다.");
            if (!Finite(fraction) || fraction <= 0f || fraction >= 1f)
                throw new ArgumentOutOfRangeException(nameof(fraction), "절단 비율은 0과 1 사이여야 합니다.");
            Mesh source = skin.sharedMesh;
            if (source == null || !source.isReadable) throw new InvalidOperationException("읽기 가능한 원본 메시가 필요합니다.");
            Transform[] bones = skin.bones;
            Matrix4x4[] bindposes = source.bindposes;
            int upperIndex = Array.IndexOf(bones, upperLeg);
            int lowerIndex = Array.IndexOf(bones, lowerLeg);
            if (upperIndex < 0 || lowerIndex < 0 || bindposes.Length != bones.Length || !lowerLeg.IsChildOf(upperLeg))
                throw new InvalidOperationException("대상 본 계층 또는 바인드 포즈 배열이 올바르지 않습니다.");
            foreach (Matrix4x4 pose in bindposes)
            {
                for (int i = 0; i < 16; i++) if (!Finite(pose[i])) throw new InvalidOperationException("유효하지 않은 바인드 포즈입니다.");
                if (Mathf.Abs(pose.determinant) < 1e-12f) throw new InvalidOperationException("역행렬이 없는 바인드 포즈입니다.");
            }
            // 호출자는 휴지 포즈를 제공한다. 메시의 수직 축을 추정하지 않고 월드 관절을 로컬로 변환한다.
            Vector3 upper = skin.transform.InverseTransformPoint(upperLeg.position);
            Vector3 lower = skin.transform.InverseTransformPoint(lowerLeg.position);
            float thighLength = Vector3.Distance(upper, lower);
            if (!Finite(thighLength) || thighLength < 1e-5f) throw new InvalidOperationException("대상 관절 간 길이가 유효하지 않습니다.");
            Vector3 restUpper = bindposes[upperIndex].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 restLower = bindposes[lowerIndex].inverse.MultiplyPoint3x4(Vector3.zero);
            float restTolerance = Mathf.Max(thighLength * 0.001f, 1e-5f);
            if ((upper - restUpper).sqrMagnitude > restTolerance * restTolerance || (lower - restLower).sqrMagnitude > restTolerance * restTolerance)
                throw new InvalidOperationException("현재 관절이 메시의 휴지 포즈와 다릅니다. 휴지 포즈에서 빌드하세요.");
            Vector3 normal = (lower - upper).normalized;
            Vector3 origin = Vector3.Lerp(upper, lower, fraction);
            float epsilon = Mathf.Max(thighLength * 1e-5f, 1e-7f);
            float areaTolerance = epsilon * epsilon * epsilon * epsilon;
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            BoneWeight[] weights = source.boneWeights;
            if (positions.Length == 0 || normals.Length != positions.Length || uvs.Length != positions.Length || weights.Length != positions.Length)
                throw new InvalidOperationException("원본 정점/노멀/UV/본 가중치 배열이 필요합니다.");
            var descendant = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++) descendant[i] = bones[i] != null && (bones[i] == upperLeg || bones[i].IsChildOf(upperLeg));
            var vertices = new Vertex[positions.Length];
            var distance = new float[positions.Length];
            var weld = new Weld(epsilon);
            var weldedIds = new int[positions.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vertex { Position = positions[i], Normal = normals[i], Uv = uvs[i], Weight = weights[i] };
                Validate(vertices[i], bones.Length);
                vertices[i].Weight = Blend(weights[i], weights[i], 0f);
                vertices[i].Normal = normals[i].normalized;
                distance[i] = Vector3.Dot(positions[i] - origin, normal);
                // 정점 위를 지나는 절단은 접촉/분기 모호성을 만들므로 추측하지 않는다.
                if (Mathf.Abs(distance[i]) <= epsilon) throw new InvalidOperationException("절단면이 원본 정점에 너무 가깝습니다. 엄격한 경계 검증을 중단합니다.");
                weldedIds[i] = weld.Add(positions[i]);
            }
            var triangles = new List<Triangle>();
            var edgeOwners = new Dictionary<Edge, List<int>>();
            int seed = -1;
            float seedScore = float.PositiveInfinity;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                if (source.GetTopology(s) != MeshTopology.Triangles) throw new InvalidOperationException("삼각형 서브메시만 지원합니다.");
                int[] indices = source.GetTriangles(s);
                if (indices.Length % 3 != 0) throw new InvalidOperationException("삼각형 인덱스 수가 올바르지 않습니다.");
                for (int j = 0; j < indices.Length; j += 3)
                {
                    for (int k = 0; k < 3; k++)
                        if (indices[j + k] < 0 || indices[j + k] >= vertices.Length) throw new InvalidOperationException("원본 인덱스 범위를 벗어났습니다.");
                    var triangle = new Triangle(indices[j], indices[j + 1], indices[j + 2], s);
                    int id = triangles.Count;
                    triangles.Add(triangle);
                    int a = triangle.Indices[0], b = triangle.Indices[1], c = triangle.Indices[2];
                    if (Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).sqrMagnitude <= areaTolerance)
                        throw new InvalidOperationException("원본에 퇴화 삼각형이 있습니다.");
                    if (distance[a] < 0 && distance[b] < 0 && distance[c] < 0) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int p = triangle.Indices[k], q = triangle.Indices[(k + 1) % 3];
                        if (distance[p] < 0 && distance[q] < 0) continue;
                        var edge = new Edge(weldedIds[p], weldedIds[q]);
                        if (!edgeOwners.TryGetValue(edge, out var owners)) edgeOwners.Add(edge, owners = new List<int>());
                        owners.Add(id);
                    }
                    float influence = (Influence(weights[a], descendant) + Influence(weights[b], descendant) + Influence(weights[c], descendant)) / 3f;
                    if (influence < 0.5f) continue;
                    float score = (((positions[a] + positions[b] + positions[c]) / 3f) - lower).sqrMagnitude;
                    if (score < seedScore) { seed = id; seedScore = score; }
                }
            }
            if (seed < 0) throw new InvalidOperationException("분리할 대상의 가중치 영역을 찾지 못했습니다.");
            // 절단면 양의 방향의 연결 성분만 추적하므로 다른 영역에 무한 평면 절단을 적용하지 않는다.
            var neighbours = new List<int>[triangles.Count];
            for (int i = 0; i < neighbours.Length; i++) neighbours[i] = new List<int>();
            foreach (var pair in edgeOwners)
            {
                var owners = pair.Value;
                if (owners.Count > 2) throw new InvalidOperationException("절단면 양의 방향에 비다양체 모서리가 있습니다.");
                if (owners.Count == 2) { neighbours[owners[0]].Add(owners[1]); neighbours[owners[1]].Add(owners[0]); }
            }
            var queue = new Queue<int>();
            queue.Enqueue(seed); triangles[seed].Selected = true;
            while (queue.Count > 0)
            {
                int id = queue.Dequeue();
                foreach (int next in neighbours[id])
                    if (!triangles[next].Selected) { triangles[next].Selected = true; queue.Enqueue(next); }
            }
            foreach (Triangle triangle in triangles)
            {
                int a = triangle.Indices[0], b = triangle.Indices[1], c = triangle.Indices[2];
                if (distance[a] < 0 && distance[b] < 0 && distance[c] < 0) continue;
                float influence = (Influence(weights[a], descendant) + Influence(weights[b], descendant) + Influence(weights[c], descendant)) / 3f;
                if (!triangle.Selected && influence >= 0.5f)
                    throw new InvalidOperationException("대상 가중치 영역에 분리된 연결 성분이 있습니다. 여러 조각 분리는 지원하지 않습니다.");
                if (triangle.Selected && distance[a] > 0 && distance[b] > 0 && distance[c] > 0 && influence < 0.01f)
                    throw new InvalidOperationException("분리 영역이 대상 밖으로 연결됩니다. 다른 영역 보존을 위해 중단합니다.");
            }
            var body = new Output(source.subMeshCount + 1);
            var leg = new Output(source.subMeshCount + 1);
            var boundary = new Weld(epsilon);
            var boundaryVertices = new List<Vertex>();
            var segments = new List<Edge>();
            var directedSegments = new List<(int a, int b)>();
            foreach (Triangle triangle in triangles)
            {
                var polygon = new List<Vertex>(3);
                foreach (int index in triangle.Indices) polygon.Add(vertices[index]);
                if (!triangle.Selected) { body.Polygon(polygon, triangle.Submesh, areaTolerance); continue; }
                var upperPolygon = Clip(polygon, origin, normal, false);
                var lowerPolygon = Clip(polygon, origin, normal, true);
                body.Polygon(upperPolygon, triangle.Submesh, areaTolerance);
                leg.Polygon(lowerPolygon, triangle.Submesh, areaTolerance);
                var cut = new List<Vertex>(2);
                for (int k = 0; k < 3; k++)
                {
                    int a = triangle.Indices[k], b = triangle.Indices[(k + 1) % 3];
                    if ((distance[a] > 0) == (distance[b] > 0)) continue;
                    cut.Add(Interpolate(vertices[a], vertices[b], distance[a] / (distance[a] - distance[b])));
                }
                if (cut.Count == 0) continue;
                if (cut.Count != 2) throw new InvalidOperationException("절단 삼각형의 경계 교점 수가 올바르지 않습니다.");
                int first = BoundaryId(cut[0], boundary, boundaryVertices, epsilon);
                int second = BoundaryId(cut[1], boundary, boundaryVertices, epsilon);
                if (first == second) throw new InvalidOperationException("절단 경계의 길이가 너무 짧습니다.");
                segments.Add(new Edge(first, second));
                // 몸통 측면의 면 방향을 기준으로 캡과 경계 방향이 반대인지 검사한다.
                for (int k = 0; k < upperPolygon.Count; k++)
                {
                    Vector3 a = upperPolygon[k].Position, b = upperPolygon[(k + 1) % upperPolygon.Count].Position;
                    if (Mathf.Abs(Vector3.Dot(a - origin, normal)) <= epsilon && Mathf.Abs(Vector3.Dot(b - origin, normal)) <= epsilon)
                    {
                        int pa = (a - cut[0].Position).sqrMagnitude <= epsilon * epsilon ? first : second;
                        directedSegments.Add((pa, pa == first ? second : first));
                    }
                }
            }
            List<int> loop = SingleLoop(segments, boundary.Positions.Count);
            if (directedSegments.Count != segments.Count) throw new InvalidOperationException("절단 경계 방향이 모호합니다.");
            Vector3 axisU = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 axisV = Vector3.Cross(normal, axisU);
            var flat = new Vector2[boundary.Positions.Count];
            for (int i = 0; i < flat.Length; i++) { Vector3 p = boundary.Positions[i] - origin; flat[i] = new Vector2(Vector3.Dot(p, axisU), Vector3.Dot(p, axisV)); }
            float area = SignedArea(loop, flat);
            if (Mathf.Abs(area) <= epsilon * epsilon) throw new InvalidOperationException("절단 경계의 면적이 없습니다.");
            if (area < 0) loop.Reverse();
            var loopDirections = new HashSet<(int, int)>();
            for (int i = 0; i < loop.Count; i++) loopDirections.Add((loop[i], loop[(i + 1) % loop.Count]));
            foreach (var segment in directedSegments)
                if (!loopDirections.Contains((segment.b, segment.a))) throw new InvalidOperationException("원본 면 방향과 캡의 바깥 방향이 일치하지 않습니다.");
            ValidateSimple(loop, flat, epsilon);
            List<int> cap = Triangulate(loop, flat, epsilon);
            for (int i = 0; i < cap.Count; i += 3)
            {
                Vertex a = CapVertex(cap[i], normal, flat, boundaryVertices);
                Vertex b = CapVertex(cap[i + 1], normal, flat, boundaryVertices);
                Vertex c = CapVertex(cap[i + 2], normal, flat, boundaryVertices);
                body.AddTriangle(a, b, c, source.subMeshCount, areaTolerance);
                a.Normal = b.Normal = c.Normal = -normal;
                leg.AddTriangle(a, c, b, source.subMeshCount, areaTolerance);
            }
            // 캡 이외의 틈도 검사하되 원래 존재하던 경계는 변경 없이 허용한다.
            ValidateClosure(body, vertices, triangles, epsilon);
            ValidateClosure(leg, vertices, triangles, epsilon);
            Mesh bodyMesh = null, legMesh = null;
            try
            {
                bodyMesh = body.Create(source.name + "_FixedBody", bindposes, bones.Length);
                legMesh = leg.Create(source.name + "_FixedLimb", bindposes, bones.Length);
                return (bodyMesh, legMesh, $"Rest pose segment {fraction:P0}; boundary={loop.Count}, cap triangles={cap.Count / 3}; body vertices={bodyMesh.vertexCount}, limb vertices={legMesh.vertexCount}; original submeshes={source.subMeshCount}, cap submesh={source.subMeshCount}. Source unchanged; meshes are transient.");
            }
            catch
            {
                if (bodyMesh != null) UnityEngine.Object.DestroyImmediate(bodyMesh);
                if (legMesh != null) UnityEngine.Object.DestroyImmediate(legMesh);
                throw;
            }
        }

        private static List<Vertex> Clip(List<Vertex> input, Vector3 origin, Vector3 normal, bool positive)
        {
            var result = new List<Vertex>(4);
            for (int i = 0; i < input.Count; i++)
            {
                Vertex a = input[i], b = input[(i + 1) % input.Count];
                float da = Vector3.Dot(a.Position - origin, normal), db = Vector3.Dot(b.Position - origin, normal);
                bool insideA = positive ? da > 0 : da < 0, insideB = positive ? db > 0 : db < 0;
                if (insideA) result.Add(a);
                if (insideA != insideB) result.Add(Interpolate(a, b, da / (da - db)));
            }
            return result;
        }

        private static Vertex Interpolate(Vertex a, Vertex b, float t)
        {
            Vector3 normal = Vector3.Lerp(a.Normal, b.Normal, t);
            if (normal.sqrMagnitude < 1e-12f) throw new InvalidOperationException("교점의 보간 노멀이 상쇄되었습니다.");
            return new Vertex { Position = Vector3.Lerp(a.Position, b.Position, t), Normal = normal.normalized, Uv = Vector2.Lerp(a.Uv, b.Uv, t), Weight = Blend(a.Weight, b.Weight, t) };
        }

        private static BoneWeight Blend(BoneWeight a, BoneWeight b, float t)
        {
            var sums = new Dictionary<int, float>();
            AddWeights(sums, a, 1f - t); AddWeights(sums, b, t);
            var sorted = new List<KeyValuePair<int, float>>(sums);
            sorted.Sort((x, y) => { int order = y.Value.CompareTo(x.Value); return order != 0 ? order : x.Key.CompareTo(y.Key); });
            var ids = new int[4]; var values = new float[4];
            float total = 0;
            for (int i = 0; i < Math.Min(4, sorted.Count); i++) { ids[i] = sorted[i].Key; values[i] = sorted[i].Value; total += values[i]; }
            if (total <= 0 || !Finite(total)) throw new InvalidOperationException("정규화할 본 가중치가 없습니다.");
            return new BoneWeight { boneIndex0 = ids[0], boneIndex1 = ids[1], boneIndex2 = ids[2], boneIndex3 = ids[3], weight0 = values[0] / total, weight1 = values[1] / total, weight2 = values[2] / total, weight3 = values[3] / total };
        }

        private static void AddWeights(Dictionary<int, float> sums, BoneWeight w, float scale)
        {
            AddWeight(sums, w.boneIndex0, w.weight0 * scale); AddWeight(sums, w.boneIndex1, w.weight1 * scale);
            AddWeight(sums, w.boneIndex2, w.weight2 * scale); AddWeight(sums, w.boneIndex3, w.weight3 * scale);
        }
        private static void AddWeight(Dictionary<int, float> sums, int index, float weight)
        {
            if (weight <= 0) return;
            sums.TryGetValue(index, out float existing); sums[index] = existing + weight;
        }
        private static float Influence(BoneWeight w, bool[] descendants) =>
            (descendants[w.boneIndex0] ? w.weight0 : 0) + (descendants[w.boneIndex1] ? w.weight1 : 0) +
            (descendants[w.boneIndex2] ? w.weight2 : 0) + (descendants[w.boneIndex3] ? w.weight3 : 0);

        private static int BoundaryId(Vertex vertex, Weld weld, List<Vertex> vertices, float epsilon)
        {
            int id = weld.Add(vertex.Position);
            if (id == vertices.Count) vertices.Add(vertex);
            else
            {
                // UV/노멀 이음매는 허용하지만 위치가 같은 캡의 스키닝 가중치는 같아야 한다.
                var delta = new Dictionary<int, float>();
                AddWeights(delta, vertices[id].Weight, 1f);
                var other = new Dictionary<int, float>(); AddWeights(other, vertex.Weight, 1f);
                foreach (var pair in other) { delta.TryGetValue(pair.Key, out float value); delta[pair.Key] = value - pair.Value; }
                foreach (float value in delta.Values)
                    if (Mathf.Abs(value) > 1e-4f) throw new InvalidOperationException("용접 경계의 본 가중치가 서로 다릅니다.");
            }
            return id;
        }

        private static List<int> SingleLoop(List<Edge> segments, int count)
        {
            if (count < 3) throw new InvalidOperationException("절단 경계가 없습니다.");
            var adjacency = new List<int>[count];
            for (int i = 0; i < count; i++) adjacency[i] = new List<int>();
            var unique = new HashSet<Edge>();
            foreach (Edge edge in segments)
            {
                if (!unique.Add(edge)) throw new InvalidOperationException("절단 경계에 중복 모서리가 있습니다.");
                adjacency[edge.A].Add(edge.B); adjacency[edge.B].Add(edge.A);
            }
            foreach (var neighbours in adjacency)
                if (neighbours.Count != 2) throw new InvalidOperationException("절단 경계의 차수가 2가 아닙니다.");
            var loop = new List<int>(); var visited = new HashSet<int>();
            int previous = -1, current = 0;
            do
            {
                if (!visited.Add(current)) throw new InvalidOperationException("절단 경계가 단순 루프가 아닙니다.");
                loop.Add(current);
                int next = adjacency[current][0] == previous ? adjacency[current][1] : adjacency[current][0];
                previous = current; current = next;
            } while (current != 0);
            if (loop.Count != count) throw new InvalidOperationException("여러 절단 루프는 지원하지 않습니다.");
            return loop;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static float SignedArea(List<int> loop, Vector2[] points)
        {
            float sum = 0;
            for (int i = 0; i < loop.Count; i++) sum += Cross(points[loop[i]], points[loop[(i + 1) % loop.Count]]);
            return sum * 0.5f;
        }
        private static void ValidateSimple(List<int> loop, Vector2[] points, float epsilon)
        {
            for (int i = 0; i < loop.Count; i++)
            for (int j = i + 1; j < loop.Count; j++)
            {
                if (j == i + 1 || (i == 0 && j == loop.Count - 1)) continue;
                Vector2 a = points[loop[i]], b = points[loop[(i + 1) % loop.Count]], c = points[loop[j]], d = points[loop[(j + 1) % loop.Count]];
                float tolerance = epsilon * epsilon;
                float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a), cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
                if (Mathf.Max(a.x, b.x) + epsilon < Mathf.Min(c.x, d.x) || Mathf.Max(c.x, d.x) + epsilon < Mathf.Min(a.x, b.x) ||
                    Mathf.Max(a.y, b.y) + epsilon < Mathf.Min(c.y, d.y) || Mathf.Max(c.y, d.y) + epsilon < Mathf.Min(a.y, b.y)) continue;
                if ((abC >= -tolerance && abD <= tolerance || abD >= -tolerance && abC <= tolerance) &&
                    (cdA >= -tolerance && cdB <= tolerance || cdB >= -tolerance && cdA <= tolerance))
                    throw new InvalidOperationException("절단 루프가 교차하거나 접촉합니다.");
            }
        }
        private static List<int> Triangulate(List<int> loop, Vector2[] points, float epsilon)
        {
            var remaining = new List<int>(loop); var result = new List<int>();
            float tolerance = epsilon * epsilon;
            while (remaining.Count > 3)
            {
                bool removed = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[b] - points[a], points[c] - points[a]) <= tolerance) continue;
                    bool contains = false;
                    foreach (int p in remaining)
                    {
                        if (p == a || p == b || p == c) continue;
                        if (Cross(points[b] - points[a], points[p] - points[a]) >= -tolerance &&
                            Cross(points[c] - points[b], points[p] - points[b]) >= -tolerance &&
                            Cross(points[a] - points[c], points[p] - points[c]) >= -tolerance) { contains = true; break; }
                    }
                    if (contains) continue;
                    result.Add(a); result.Add(b); result.Add(c); remaining.RemoveAt(i); removed = true; break;
                }
                if (!removed) throw new InvalidOperationException("단순 캡 삼각분할에 실패했습니다.");
            }
            if (Cross(points[remaining[1]] - points[remaining[0]], points[remaining[2]] - points[remaining[0]]) <= tolerance)
                throw new InvalidOperationException("마지막 캡 삼각형이 퇴화했습니다.");
            result.AddRange(remaining);
            return result;
        }
        private static Vertex CapVertex(int id, Vector3 normal, Vector2[] flat, List<Vertex> vertices)
        {
            Vertex vertex = vertices[id]; vertex.Normal = normal; vertex.Uv = flat[id]; return vertex;
        }
        private static void ValidateClosure(Output output, Vertex[] original, List<Triangle> triangles, float epsilon)
        {
            var weld = new Weld(epsilon);
            var originalCounts = new Dictionary<Edge, int>();
            foreach (Triangle triangle in triangles)
            for (int i = 0; i < 3; i++)
            {
                var edge = new Edge(weld.Add(original[triangle.Indices[i]].Position), weld.Add(original[triangle.Indices[(i + 1) % 3]].Position));
                originalCounts.TryGetValue(edge, out int n); originalCounts[edge] = n + 1;
            }
            var counts = new Dictionary<Edge, int>(); var directions = new Dictionary<Edge, int>();
            foreach (var indices in output.Submeshes)
            for (int i = 0; i < indices.Count; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = weld.Add(output.Vertices[indices[i + k]].Position), b = weld.Add(output.Vertices[indices[i + (k + 1) % 3]].Position);
                if (a == b) throw new InvalidOperationException("용접 후 퇴화 모서리가 있습니다.");
                var edge = new Edge(a, b);
                counts.TryGetValue(edge, out int n); counts[edge] = n + 1;
                directions.TryGetValue(edge, out int direction); directions[edge] = direction + (a == edge.A ? 1 : -1);
            }
            foreach (var pair in counts)
            {
                if (pair.Value == 2 && directions[pair.Key] == 0) continue;
                if (pair.Value == 1 && originalCounts.TryGetValue(pair.Key, out int originalCount) && originalCount == 1) continue;
                throw new InvalidOperationException("절단 결과에 새 틈, 비다양체 모서리 또는 뒤집힌 면이 있습니다.");
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(Vertex vertex, int boneCount)
        {
            if (!Finite(vertex.Position.x) || !Finite(vertex.Position.y) || !Finite(vertex.Position.z) ||
                !Finite(vertex.Normal.x) || !Finite(vertex.Normal.y) || !Finite(vertex.Normal.z) || vertex.Normal.sqrMagnitude < 1e-12f ||
                !Finite(vertex.Uv.x) || !Finite(vertex.Uv.y)) throw new InvalidOperationException("유효하지 않은 정점 속성입니다.");
            BoneWeight w = vertex.Weight;
            int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
            float sum = 0;
            for (int i = 0; i < 4; i++)
            {
                if (ids[i] < 0 || ids[i] >= boneCount || !Finite(values[i]) || values[i] < 0) throw new InvalidOperationException("본 인덱스 또는 가중치가 유효하지 않습니다.");
                sum += values[i];
            }
            if (!Finite(sum) || Mathf.Abs(sum - 1f) > 0.01f) throw new InvalidOperationException("원본 본 가중치 합이 1이 아닙니다.");
        }
    }
}
#endif
