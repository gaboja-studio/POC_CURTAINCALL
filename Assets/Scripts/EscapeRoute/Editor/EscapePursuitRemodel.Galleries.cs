using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        const string GalleryAsset = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodelGeometry.asset";

        static void BuildGallerySurfaces(EscapePursuerRoute route)
        {
            var run = new List<Vector3>();
            var supported = new HashSet<int>();
            int runId = 0;
            for (int i = 2; i <= 70; i++)
            {
                if (route.Points[i].jump)
                {
                    if (run.Count > 1) Gallery(run, runId++);
                    run.Clear();
                    continue;
                }
                if (run.Count == 0) run.Add(frame.InverseTransformPoint(route.Points[i - 1].feet.position));
                run.Add(frame.InverseTransformPoint(route.Points[i].feet.position));
                supported.Add(i - 1); supported.Add(i);
            }
            if (run.Count > 1) Gallery(run, runId);
            for (int i = 1; i <= 70; i++)
                if (!supported.Contains(i))
                    Box("Isolated jump abutment " + i, frame.InverseTransformPoint(route.Points[i].feet.position) - Vector3.up * .45f,
                        new Vector3(4.8f, .9f, 4.8f));
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(GalleryAsset));
        }

        static void Gallery(List<Vector3> path, int id)
        {
            // Shared vertices at the bends avoid coplanar overlapping cube tops and their shimmer.
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            float length = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var incoming = Vector3.ProjectOnPlane(path[i] - path[Mathf.Max(0, i - 1)], Vector3.up).normalized;
                var outgoing = Vector3.ProjectOnPlane(path[Mathf.Min(path.Count - 1, i + 1)] - path[i], Vector3.up).normalized;
                if (i == 0) incoming = outgoing;
                if (i == path.Count - 1) outgoing = incoming;
                Vector3 side = Vector3.Cross(Vector3.up, (incoming + outgoing).normalized);
                float miter = Mathf.Min(3.4f, 2.4f / Mathf.Max(.5f, Vector3.Dot(side, Vector3.Cross(Vector3.up, outgoing))));
                var center = path[i];
                if (i == 0) center -= incoming * 2.4f;
                if (i == path.Count - 1) center += outgoing * 2.4f;
                if (i > 0) length += Vector3.Distance(path[i - 1], path[i]);
                vertices.Add(center - side * miter);
                vertices.Add(center + side * miter);
                vertices.Add(center - side * miter - Vector3.up * .9f);
                vertices.Add(center + side * miter - Vector3.up * .9f);
                uv.Add(new Vector2(0, length * .2f)); uv.Add(new Vector2(1, length * .2f));
                uv.Add(new Vector2(0, length * .2f)); uv.Add(new Vector2(1, length * .2f));
                if (i == 0) continue;
                int a = (i - 1) * 4, b = i * 4;
                Quad(a, b, b + 1, a + 1); // top
                Quad(a + 3, b + 3, b + 2, a + 2);
                Quad(a + 2, b + 2, b, a);
                Quad(a + 1, b + 1, b + 3, a + 3);
            }
            Quad(0, 1, 3, 2);
            int last = vertices.Count - 4;
            Quad(last + 2, last + 3, last + 1, last);
            var mesh = new Mesh { name = "Continuous gallery " + id };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var saved = System.Linq.Enumerable.FirstOrDefault(AssetDatabase.LoadAllAssetsAtPath(GalleryAsset), a => a is Mesh && a.name == mesh.name) as Mesh;
            if (saved)
            {
                // Update the live mesh through its API so the renderer uploads the new geometry too.
                saved.Clear();
                saved.indexFormat = mesh.indexFormat;
                saved.SetVertices(vertices);
                saved.SetUVs(0, uv);
                saved.SetTriangles(triangles, 0);
                saved.RecalculateNormals();
                saved.RecalculateBounds();
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = saved;
                EditorUtility.SetDirty(mesh);
            }
            else if (AssetDatabase.LoadMainAssetAtPath(GalleryAsset)) AssetDatabase.AddObjectToAsset(mesh, GalleryAsset);
            else AssetDatabase.CreateAsset(mesh, GalleryAsset);
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(structure, false);
            go.transform.SetPositionAndRotation(frame.position, frame.rotation);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = concrete;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            void Quad(int a, int b, int c, int d)
            { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(a); triangles.Add(c); triangles.Add(d); }
        }
    }
}
