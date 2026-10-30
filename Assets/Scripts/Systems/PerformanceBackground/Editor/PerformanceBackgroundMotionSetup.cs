using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.PerformanceBackground.Editor
{
    /// <summary>One-time authoring of separate moving parts from the baked background mesh.</summary>
    public static class PerformanceBackgroundMotionSetup
    {
        const string DynamicPath = "Assets/_ThirdParty/curtaincall/PerformanceBackground/Dynamic";

        public static string Build()
        {
            var venue = GameObject.Find("PerformanceBackground/CircularVenue");
            if (venue == null) throw new InvalidOperationException("Open performance_background first.");
            if (venue.GetComponentInChildren<PerformanceBackgroundMotion>(true) != null)
                throw new InvalidOperationException("Motion is already installed; edit the existing controller.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add crane and show light motion");
            var group = NewObject("28_DynamicShow", venue.transform);
            var controller = Undo.AddComponent<PerformanceBackgroundMotion>(group);
            var pivots = new[]
            {
                Pivot("Crane_01", new Vector3(284.31f, -39f, 395.90f), group.transform),
                Pivot("Crane_02", new Vector3(-260.65f, -87f, 415.69f), group.transform),
                Pivot("Crane_03", new Vector3(50.92f, -175f, 501.94f), group.transform)
            };
            var city = venue.transform.Find("25_Undercity");
            int movingTriangles = 0;
            foreach (var filter in city.GetComponentsInChildren<MeshFilter>().ToArray())
            {
                if (!filter.name.StartsWith("HeroSawGantry_", StringComparison.Ordinal)) continue;
                var original = filter.sharedMesh;
                if (original.subMeshCount != 1) throw new InvalidOperationException("Expected single-material gantry meshes.");
                var triangles = original.triangles;
                var vertices = original.vertices;
                var pieces = ConnectedPieces(vertices, triangles);
                var buckets = Enumerable.Range(0, 4).Select(_ => new List<int>()).ToArray();
                foreach (var piece in pieces)
                {
                    var bounds = new Bounds(filter.transform.TransformPoint(vertices[triangles[piece[0]]]), Vector3.zero);
                    foreach (int i in piece) bounds.Encapsulate(filter.transform.TransformPoint(vertices[triangles[i]]));
                    int nearest = 0;
                    for (int i = 1; i < pivots.Length; i++)
                        if (HorizontalDistance(bounds.center, pivots[i].position) < HorizontalDistance(bounds.center, pivots[nearest].position)) nearest = i;
                    // Keep the long support columns and foundations fixed. Move whole connected pieces only.
                    bool upperPart = bounds.size.y < 70f && bounds.min.y >= pivots[nearest].position.y - 65f;
                    var destination = buckets[upperPart ? nearest + 1 : 0];
                    foreach (int i in piece) destination.Add(triangles[i]);
                }
                Undo.RecordObject(filter, "Separate fixed gantry supports");
                filter.sharedMesh = SaveSubset(original, buckets[0], filter.name + "_Fixed");
                for (int i = 0; i < pivots.Length; i++)
                {
                    if (buckets[i + 1].Count == 0) continue;
                    movingTriangles += buckets[i + 1].Count / 3;
                    var go = NewObject(filter.name, filter.transform.parent);
                    go.transform.localPosition = filter.transform.localPosition;
                    go.transform.localRotation = filter.transform.localRotation;
                    go.transform.localScale = filter.transform.localScale;
                    go.AddComponent<MeshFilter>().sharedMesh = SaveSubset(original, buckets[i + 1], filter.name + "_Arm" + (i + 1));
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    go.transform.SetParent(pivots[i], true);
                    go.isStatic = false;
                }
            }
            if (pivots.Any(p => p.childCount == 0)) throw new InvalidOperationException("A gantry has no separated moving parts.");
            controller.cranes = pivots.Select((t, i) => Track(t, new Vector3(0f, 7f + i * 1.5f, 0f), 85f + i * 13f, i * .17f)).ToArray();

            var lights = new List<PerformanceBackgroundMotion.Motion>();
            int spotIndex = 0;
            foreach (var light in venue.GetComponentsInChildren<Light>().ToArray())
            {
                if (!light.gameObject.activeInHierarchy || light.type != LightType.Spot || light.name == "EntryWarmPractical") continue;
                lights.Add(Track(light.transform, new Vector3(6f, 28f, 0f), 48f + spotIndex * 3f, spotIndex * .055f));
                spotIndex++;
            }
            int beamIndex = 0;
            foreach (var filter in city.GetComponentsInChildren<MeshFilter>().ToArray())
            {
                if (!filter.name.StartsWith("DustBeam", StringComparison.Ordinal)) continue;
                var mesh = filter.sharedMesh;
                float top = mesh.bounds.max.y;
                var tipVertices = mesh.vertices.Where(v => v.y >= top - .05f).ToArray();
                if (tipVertices.Length == 0) throw new InvalidOperationException("Beam apex not found.");
                var tip = tipVertices.Aggregate(Vector3.zero, (sum, v) => sum + v) / tipVertices.Length;
                var pivot = Pivot(filter.name + "_Sweep", filter.transform.TransformPoint(tip), group.transform);
                Undo.SetTransformParent(filter.transform, pivot, "Set light beam pivot");
                var renderer = filter.GetComponent<Renderer>();
                Undo.RecordObject(filter.gameObject, "Make beam movable");
                GameObjectUtility.SetStaticEditorFlags(filter.gameObject, 0);
                if (renderer != null) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
                lights.Add(Track(pivot, new Vector3(7f, 0f, 10f), 52f + (beamIndex % 6) * 3f, (beamIndex % 6) * .055f));
                beamIndex++;
            }
            controller.showLights = lights.ToArray();
            AddBearings();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(venue.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(venue.scene);
            Selection.activeGameObject = group;
            return $"Installed {pivots.Length} crane arms ({movingTriangles} triangles), {spotIndex} spotlights and {beamIndex} beam sweeps.";
        }

        public static void AddBearings()
        {
            var motion = UnityEngine.Object.FindFirstObjectByType<PerformanceBackgroundMotion>();
            if (motion == null) throw new InvalidOperationException("Motion is not installed.");
            var city = GameObject.Find("PerformanceBackground/CircularVenue/25_Undercity").transform;
            var iron = city.Find("HeroSawGantry_Iron").GetComponent<MeshRenderer>().sharedMaterial;
            var bronze = city.Find("HeroSawGantry_Bronze").GetComponent<MeshRenderer>().sharedMaterial;
            for (int i = 0; i < motion.cranes.Length; i++)
            {
                var pivot = motion.cranes[i].target;
                string name = "CraneSupport_" + (i + 1).ToString("00");
                if (motion.transform.Find(name) != null) continue;
                var support = Pivot(name, pivot.position, motion.transform);
                BearingPart("FixedCrosshead", PrimitiveType.Cube, support, new Vector3(0f, -5f, 0f), new Vector3(86f, 3f, 4f), iron);
                BearingPart("SlewBearing", PrimitiveType.Cylinder, support, new Vector3(0f, -3f, 0f), new Vector3(6f, 1.5f, 6f), bronze);
                BearingPart("RotatingCollar", PrimitiveType.Cylinder, pivot, Vector3.zero, new Vector3(8f, .6f, 8f), bronze);
            }
        }

        static void BearingPart(string name, PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Add crane bearing");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude;

        static PerformanceBackgroundMotion.Motion Track(Transform target, Vector3 degrees, float period, float phase)
        {
            var track = new PerformanceBackgroundMotion.Motion { target = target, rotationDegrees = degrees, periodSeconds = period, phase = phase };
            track.CaptureRestPose();
            return track;
        }

        static GameObject NewObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create moving background part");
            go.transform.SetParent(parent, false);
            return go;
        }

        static Transform Pivot(string name, Vector3 position, Transform parent)
        {
            var t = NewObject(name, parent).transform;
            t.position = position;
            return t;
        }

        internal static Mesh SaveSubset(Mesh original, List<int> indices, string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            var lookup = new Dictionary<int, int>();
            var source = original.vertices;
            var normals = original.normals;
            var uv = original.uv;
            var colors = original.colors;
            var tangents = original.tangents;
            var used = new List<int>();
            var triangles = new int[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                int old = indices[i];
                if (!lookup.TryGetValue(old, out int next)) { next = used.Count; lookup.Add(old, next); used.Add(old); }
                triangles[i] = next;
            }
            mesh.vertices = used.Select(i => source[i]).ToArray();
            if (normals.Length == source.Length) mesh.normals = used.Select(i => normals[i]).ToArray();
            if (uv.Length == source.Length) mesh.uv = used.Select(i => uv[i]).ToArray();
            if (colors.Length == source.Length) mesh.colors = used.Select(i => colors[i]).ToArray();
            if (tangents.Length == source.Length) mesh.tangents = used.Select(i => tangents[i]).ToArray();
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            if (normals.Length != source.Length) mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, $"{DynamicPath}/{name}.asset");
            return mesh;
        }

        // Weld coincident face vertices before finding connected pieces; never cut triangles through a member.
        internal static List<List<int>> ConnectedPieces(Vector3[] vertices, int[] triangles)
        {
            var parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            void Join(int a, int b) { parent[Find(a)] = Find(b); }
            var welded = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i] * 1000f;
                var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (welded.TryGetValue(key, out int other)) Join(i, other); else welded.Add(key, i);
            }
            for (int i = 0; i < triangles.Length; i += 3) { Join(triangles[i], triangles[i + 1]); Join(triangles[i], triangles[i + 2]); }
            var pieces = new Dictionary<int, List<int>>();
            for (int i = 0; i < triangles.Length; i++)
            {
                int key = Find(triangles[i]);
                if (!pieces.TryGetValue(key, out var piece)) { piece = new List<int>(); pieces.Add(key, piece); }
                piece.Add(i);
            }
            return pieces.Values.ToList();
        }
    }
}
