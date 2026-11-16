using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.EscapeRoute.EditorTools;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only scene diagnostic. Does not open production or alter scene/mesh/record data.
public static class PrototypeStaticDiagnostic
{
    public static string Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototype.unity")
            throw new InvalidOperationException("Prototype edit scene required.");
        string guid = AssetDatabase.AssetPathToGUID(scene.path);
        var record = AssetDatabase.LoadAssetAtPath<EscapeStaticBatchRecord>("Assets/Scenes/Levels/EscapeFactoryArt/static-batch-record.asset");
        var ids = new HashSet<string>();
        foreach (string text in record.sources)
        { if (!GlobalObjectId.TryParse(text, out var id)) throw new InvalidOperationException(text); ids.Add(Key(id)); }
        var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        bool Recorded(MeshRenderer r)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(r);
            return id.assetGUID.ToString() == guid && ids.Contains(Key(id));
        }
        var surviving = renderers.Where(Recorded).ToArray();
        var rows = new List<object>();
        foreach (int number in new[] { 55, 56, 66 })
        {
            var chunk = renderers.Single(r => r.name == "A optimized chunk " + number);
            var original = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_ThirdParty/Task073/StaticOptimized/chunk-" + number + ".asset");
            var reviewed = chunk.GetComponent<MeshFilter>().sharedMesh;
            int[] trimmedOriginal = TrimmedOriginalTriangles(original, reviewed);
            var nearbyBounds = chunk.bounds; nearbyBounds.Expand(1f);
            var nearby = renderers.Where(r => r != chunk && nearbyBounds.Intersects(r.bounds)).ToArray();
            var candidates = new List<object>();
            foreach (var renderer in nearby)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter ? filter.sharedMesh : null;
                if (!mesh) continue;
                int exact = 0, loose = 0, trimmed = 0;
                if (mesh.isReadable && original.isReadable)
                {
                    var matrix = chunk.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    exact = Matches(original, mesh, matrix, .00025f);
                    loose = Matches(original, mesh, matrix, .01f);
                    trimmed = Matches(original, mesh, matrix, .00025f, trimmedOriginal);
                }
                var id = GlobalObjectId.GetGlobalObjectIdSlow(renderer);
                candidates.Add(new {
                    renderer.name, id = id.ToString(), recorded = Recorded(renderer), renderer.enabled,
                    active = renderer.gameObject.activeInHierarchy,
                    sameParentTree = renderer.transform.IsChildOf(chunk.transform.parent),
                    sameMaterial = renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial == chunk.sharedMaterial,
                    path = Path(renderer.transform), mesh = AssetDatabase.GetAssetPath(mesh), mesh.isReadable,
                    originalTrianglesMatched = exact, originalTrianglesMatchedAt1cm = loose,
                    trimmedOriginalTrianglesStillPresent = trimmed,
                    sourceTriangles = mesh.isReadable ? mesh.triangles.Length / 3 : -1,
                    center = V(renderer.bounds.center), extents = V(renderer.bounds.extents)
                });
            }
            rows.Add(new { chunk.name, path = Path(chunk.transform),
                mesh = AssetDatabase.GetAssetPath(chunk.GetComponent<MeshFilter>().sharedMesh),
                originalTriangles = original.triangles.Length / 3,
                reviewedTrimmedOriginalTriangles = trimmedOriginal.Length / 3,
                center = V(chunk.bounds.center), extents = V(chunk.bounds.extents), candidates });
        }
        var survivingKeys = new HashSet<string>(surviving.Select(r => Key(GlobalObjectId.GetGlobalObjectIdSlow(r))));
        string result = JsonConvert.SerializeObject(new {
            scope = "Read-only current scene: candidates have no parent/material exclusion; triangle positions and winding are measured independently.",
            scene = scene.path, sceneGuid = guid, recorded = ids.Count, surviving = surviving.Length,
            disabled = surviving.Count(r => !r.enabled), missingLocalIds = ids.Where(k => !survivingKeys.Contains(k)).ToArray(), chunks = rows
        });
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath),
            ".harness/local/pursuer-space-20261116/static-diagnostic.json"), result);
        return result;
    }

    static int Matches(Mesh original, Mesh source, Matrix4x4 matrix, float tolerance, int[] selection = null)
    {
        var unique = new List<Vector3>();
        int Point(Vector3 p, bool add)
        {
            for (int i = 0; i < unique.Count; i++) if ((unique[i] - p).sqrMagnitude <= tolerance * tolerance) return i;
            if (!add) return -1; unique.Add(p); return unique.Count - 1;
        }
        string Tri(int a, int b, int c) => a <= b && a <= c ? a + ":" + b + ":" + c
            : b <= a && b <= c ? b + ":" + c + ":" + a : c + ":" + a + ":" + b;
        int[] oldPoints = original.vertices.Select(v => Point(v, true)).ToArray();
        var expected = new HashSet<string>(); var old = selection ?? original.triangles;
        for (int i = 0; i < old.Length; i += 3) expected.Add(Tri(oldPoints[old[i]], oldPoints[old[i + 1]], oldPoints[old[i + 2]]));
        int[] points = source.vertices.Select(v => Point(matrix.MultiplyPoint3x4(v), false)).ToArray();
        int count = 0; var triangles = source.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            if (a >= 0 && b >= 0 && c >= 0 && expected.Contains(Tri(a, b, c))) count++;
        }
        return count;
    }
    static int[] TrimmedOriginalTriangles(Mesh original, Mesh reviewed)
    {
        // EscapeCoplanarPolish keeps the old vertex array as an unchanged prefix,
        // appending intersections and replacing only triangles cut by a reviewed rule.
        var a = original.vertices; var b = reviewed.vertices;
        if (b.Length < a.Length || a.Where((p, i) => Vector3.Distance(p, b[i]) > .00001f).Any())
            throw new InvalidOperationException("Reviewed mesh no longer has the original vertex prefix.");
        string Tri(int x, int y, int z) => x <= y && x <= z ? x + ":" + y + ":" + z
            : y <= x && y <= z ? y + ":" + z + ":" + x : z + ":" + x + ":" + y;
        var kept = new HashSet<string>(); var later = reviewed.triangles;
        for (int i = 0; i < later.Length; i += 3) kept.Add(Tri(later[i], later[i + 1], later[i + 2]));
        var result = new List<int>(); var before = original.triangles;
        for (int i = 0; i < before.Length; i += 3)
            if (!kept.Contains(Tri(before[i], before[i + 1], before[i + 2])))
                result.AddRange(new[] { before[i], before[i + 1], before[i + 2] });
        return result.ToArray();
    }
    static string Key(GlobalObjectId id) => id.identifierType + ":" + id.targetObjectId + ":" + id.targetPrefabId;
    static object V(Vector3 p) => new { x = p.x, y = p.y, z = p.z };
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
}
