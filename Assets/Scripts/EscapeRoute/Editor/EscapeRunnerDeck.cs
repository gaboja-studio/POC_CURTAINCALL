using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // Authored planar walking surface with chipped outer edges. MeshCollider matches the visible geometry.
        static void RunnerDeck(Transform p, string name, int start, int end)
        {
            string path = BrutalMeshes + "/runner-deck-" + start + "-" + end + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
                void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    int n = vertices.Count;
                    vertices.AddRange(new[] { a, b, c, d });
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    foreach (var at in new[] { a, b, c, d })
                        uv.Add(Mathf.Abs(normal.y) > .5f ? new Vector2(at.x, at.z) / 3 : Mathf.Abs(normal.z) > .5f ? new Vector2(at.x, at.y) / 3 : new Vector2(at.z, at.y) / 3);
                    triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                }
                var south = new List<Vector3>(); var north = new List<Vector3>();
                int count = Mathf.CeilToInt((end - start) / 1.5f);
                for (int i = 0; i <= count; i++)
                {
                    float x = Mathf.Lerp(start, end, (float)i / count);
                    float bite = i == 0 || i == count ? 0 : i % 9 < 4 ? new[] { .15f, .8f, 1.45f, .35f }[i % 9] : 0;
                    float northBite = i == 0 || i == count ? 0 : i % 11 > 6 ? .3f * (i % 5) : 0;
                    south.Add(new Vector3(x, 0, 71 + bite)); north.Add(new Vector3(x, 0, 85 - northBite));
                }
                var down = Vector3.down * 2.5f;
                for (int i = 0; i < count; i++)
                {
                    var a = south[i]; var b = north[i]; var c = north[i + 1]; var d = south[i + 1];
                    Face(a, b, c, d); Face(d + down, c + down, b + down, a + down);
                    Face(a + down, a, d, d + down); Face(c + down, c, b, b + down);
                }
                Face(south[0], south[0] + down, north[0] + down, north[0]);
                Face(north[count], north[count] + down, south[count] + down, south[count]);
                mesh = new Mesh { name = "RunnerDeck_" + start + "_" + end };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            Undo.RegisterCreatedObjectUndo(go, "Chipped running slab"); go.transform.SetParent(p, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = bConcrete;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
