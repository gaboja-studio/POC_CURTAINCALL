using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.EscapeRoute.EditorTools;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prototype-only restoration of the second, reversible static-batching pass.
// Factory static meshes belong to the older destructive art pass and are untouched.
// No production scene, batch record, shared mesh, prefab asset or static flag is changed.
// Three reviewed pieces are copied to new Prototype-only meshes before unbatching.
public static class PrototypeStaticRestore
{
    const string Prototype = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototype.unity";
    const string PrototypeGuid = "a568647cfce3f80448fdb86d875c39d6";
    const string Production = "Assets/Scenes/Levels/escape_route.unity";
    const string ProductionGuid = "c9bc27ef66a57414e8bd36141ad746b2";
    const string Record = "Assets/Scenes/Levels/EscapeFactoryArt/static-batch-record.asset";
    const string MeshFolder = "Assets/_ThirdParty/Task073/StaticOptimized/";
    const string Prefix = "A optimized chunk ";
    const string PrivateMeshes = "Assets/Scenes/Tests/EscapeRoute/Pursuer/LabyrinthStaticRestoreMeshes";
    // Task100 trimmed overlapping render faces after the reversible bake. The changed
    // mesh identities are explicit: two are now ghosts; one has three surviving cuts.
    static readonly (string name, ulong filterId, string guid)[] Repairs = {
        ("A optimized chunk 55", 1433137604, "4195d70d0f5e4f6469f964d15d984879"),
        ("A optimized chunk 66", 1722472661, "91b970a4adfde16479271acbc0423e24"),
        ("A optimized chunk 56", 2019335420, "f8bb3e9f0bdf8ef4799f06fdccc3bb76")
    };
    // Production YAML: 55 = old flame connector steps 0..8, 56 = steps 9..39.
    // All forty recorded renderers were intentionally removed from this Prototype.
    static readonly ulong[] RemovedStairSources = {
        1462009367UL, 63239711UL, 680308183UL, 52902723UL, 1658244190UL, 1862835558UL, 313992936UL, 164436926UL, 1084862280UL,
        23040961UL, 1518878000UL, 1587732992UL, 195646411UL, 1678238059UL, 749011311UL, 1299383745UL, 930813777UL,
        2077648785UL, 1767971544UL, 1937849963UL, 1301294646UL, 1888245026UL, 1270491169UL, 1373498955UL, 658721859UL,
        402940195UL, 611832158UL, 522010288UL, 1883891256UL, 26721034UL, 2109263166UL, 1272221182UL, 297538160UL,
        1807933995UL, 210197419UL, 231407372UL, 206494040UL, 1978812940UL, 449016309UL, 290589417UL
    };
    static readonly ulong[] ReviewedSourceIds = { 1559058404UL, 935945337UL, 1288486255UL };
    sealed class Replacement
    {
        public MeshFilter filter;
        public Mesh mesh;
        public string path;
        public int originalTriangles, reviewedTriangles;
        public float originalArea, reviewedArea;
    }

    sealed class Plan
    {
        public Scene scene;
        public MeshRenderer[] sources;
        public MeshRenderer[] preservedSources;
        public GameObject[] chunks, preservedChunks;
        public string[] missing, meshes;
        public Replacement[] replacements = Array.Empty<Replacement>();
        public int recorded;
        public string Summary(string status) => JsonConvert.SerializeObject(new {
            status, scene = scene.path, sceneGuid = PrototypeGuid, recordedSources = recorded,
            survivingSources = sources.Length, currentlyDisabled = sources.Count(r => !r.enabled),
            missingSources = missing.Length, missingSourceIds = missing,
            chunkObjects = chunks.Length, protectedMeshAssets = meshes.Length,
            preservedReviewedChunks = preservedChunks.Select(g => g.name).ToArray(),
            preservedDisabledSources = preservedSources.Select(r => GlobalObjectId.GetGlobalObjectIdSlow(r).ToString()).ToArray(),
            olderFactoryStaticMeshes = "Unchanged; this pass has no reversible source record for them.",
            staticFlags = "Preserved exactly; no static batching flags are added."
            , reviewedMeshCopies = replacements.Select(r => new { r.path, r.originalTriangles, r.reviewedTriangles, r.originalArea, r.reviewedArea }).ToArray()
        });
        public void DisposeTransient()
        {
            foreach (var replacement in replacements)
                if (replacement.mesh && !EditorUtility.IsPersistent(replacement.mesh)) Object.DestroyImmediate(replacement.mesh);
        }
    }

    public static string DryRun()
    {
        var plan = Inspect();
        try { return plan.Summary("DRY_RUN_NO_CHANGES"); }
        finally { plan.DisposeTransient(); }
    }

    public static string Apply()
    {
        var plan = Inspect();
        if (plan.scene.isDirty)
        {
            plan.DisposeTransient();
            throw new InvalidOperationException("Save the current Prototype edits first. This restore neither discards nor silently saves unrelated unsaved work.");
        }
        if (plan.chunks.Length == 0)
        {
            if (plan.sources.Any(r => !r.enabled))
                throw new InvalidOperationException("Chunks are absent but recorded source renderers remain disabled; inspect this partial state manually.");
            return plan.Summary("ALREADY_RESTORED_NO_CHANGES");
        }

        // Hash protected files before any mutation. Saving uses only the Prototype path;
        // there is deliberately no SaveAssets or SavePrefabAsset call. A failed operation
        // may delete only new private assets it created during that same operation.
        var protectedFiles = new[] { Production, Production + ".meta", Record, Record + ".meta" }
            .Concat(plan.meshes.SelectMany(p => new[] { p, p + ".meta" }))
            .Concat(plan.replacements.Select(r => AssetDatabase.GetAssetPath(r.filter.sharedMesh))
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)).SelectMany(p => new[] { p, p + ".meta" }))
            .Distinct().ToDictionary(p => p, Hash);
        var flags = plan.sources.Concat(plan.preservedSources).ToDictionary(r => r.gameObject, r => GameObjectUtility.GetStaticEditorFlags(r.gameObject));
        int restored = plan.sources.Count(r => !r.enabled), removed = plan.chunks.Length;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore Prototype editable static geometry only");
        var createdAssets = new List<string>();
        try
        {
            if (plan.replacements.Length > 0 && !AssetDatabase.IsValidFolder(PrivateMeshes))
                AssetDatabase.CreateFolder("Assets/Scenes/Tests/EscapeRoute/Pursuer", "LabyrinthStaticRestoreMeshes");
            foreach (var replacement in plan.replacements)
            {
                if (AssetDatabase.LoadMainAssetAtPath(replacement.path))
                    throw new InvalidOperationException("Refuse to overwrite an existing private mesh: " + replacement.path);
                AssetDatabase.CreateAsset(replacement.mesh, replacement.path);
                createdAssets.Add(replacement.path);
                Undo.RecordObject(replacement.filter, "Preserve reviewed surface in Prototype source");
                replacement.filter.sharedMesh = replacement.mesh;
                EditorUtility.SetDirty(replacement.filter);
                if (PrefabUtility.IsPartOfPrefabInstance(replacement.filter))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.filter);
            }
            foreach (var renderer in plan.sources)
            {
                if (renderer.enabled) continue;
                Undo.RecordObject(renderer, "Restore Prototype source renderer");
                renderer.enabled = true;
                EditorUtility.SetDirty(renderer);
                if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (var chunk in plan.chunks) Undo.DestroyObjectImmediate(chunk);
            foreach (var pair in flags)
                if (GameObjectUtility.GetStaticEditorFlags(pair.Key) != pair.Value)
                    throw new InvalidOperationException("Static flags changed: " + pair.Key.name);
            var after = Inspect();
            if (after.chunks.Length != 0 || after.sources.Any(r => !r.enabled))
                throw new InvalidOperationException("Prototype restoration postcondition failed.");
            AssertProtected(protectedFiles);
            EditorSceneManager.MarkSceneDirty(plan.scene);
            if (!EditorSceneManager.SaveScene(plan.scene, Prototype))
                throw new InvalidOperationException("Could not save restored Prototype.");
            AssertProtected(protectedFiles);
            Undo.CollapseUndoOperations(group);
            return JsonConvert.SerializeObject(new {
                status = "RESTORED_PROTOTYPE_ONLY", scene = Prototype, restoredRenderers = restored,
                removedSceneChunks = removed, absentOriginalsNotResurrected = plan.missing.Length,
                protectedFilesUnchanged = protectedFiles.Count, protectedMeshAssets = plan.meshes.Length,
                preservedReviewedChunks = plan.preservedChunks.Select(g => g.name).ToArray(),
                preservedDisabledSources = plan.preservedSources.Length,
                privateReviewedMeshes = createdAssets.ToArray(),
                staticFlagsPreserved = true, rebatchingPerformed = false
            });
        }
        catch
        {
            // Revert only this operation's scene Undo group. Protected assets are never
            // rewritten as a rollback, so an external concurrent edit cannot be discarded.
            Undo.RevertAllDownToGroup(group);
            foreach (string path in createdAssets)
                if (path.StartsWith(PrivateMeshes + "/", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            throw;
        }
        finally { plan.DisposeTransient(); }
    }

    static Plan Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || !scene.IsValid() || !scene.isLoaded || scene.path != Prototype)
            throw new InvalidOperationException("Open the Prototype as the active scene in Edit Mode.");
        if (AssetDatabase.AssetPathToGUID(Prototype) != PrototypeGuid || AssetDatabase.AssetPathToGUID(Production) != ProductionGuid)
            throw new InvalidOperationException("Scene GUID mismatch: refuse to remap source file IDs.");
        var record = AssetDatabase.LoadAssetAtPath<EscapeStaticBatchRecord>(Record);
        if (!record || record.sources == null || record.meshes == null || record.sources.Length == 0 || record.meshes.Length == 0)
            throw new InvalidOperationException("The original static-batch record is missing or empty.");
        if (record.sources.Distinct().Count() != record.sources.Length || record.meshes.Distinct().Count() != record.meshes.Length)
            throw new InvalidOperationException("The original static-batch record contains duplicate entries.");
        var meshes = new HashSet<string>(record.meshes, StringComparer.Ordinal);
        foreach (string mesh in meshes)
            if (!mesh.StartsWith(MeshFolder, StringComparison.Ordinal) || !AssetDatabase.LoadAssetAtPath<Mesh>(mesh))
                throw new InvalidOperationException("Unexpected or missing recorded shared mesh: " + mesh);

        var renderers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var byLocalId = new Dictionary<string, MeshRenderer>();
        foreach (var renderer in renderers)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(renderer);
            if (id.identifierType != 2 || id.assetGUID.ToString() != PrototypeGuid) continue;
            if (!byLocalId.TryAdd(Key(id), renderer))
                throw new InvalidOperationException("Duplicate Prototype renderer identity: " + id);
        }
        var sources = new List<MeshRenderer>();
        var missing = new List<string>();
        foreach (string source in record.sources)
        {
            if (!GlobalObjectId.TryParse(source, out var original) || original.identifierType != 2
                || original.assetGUID.ToString() != ProductionGuid)
                throw new InvalidOperationException("Record source does not belong to the production scene: " + source);
            // Never resolve the production GlobalObjectId. Resolve only an explicitly
            // remapped Prototype GUID and verify it against the active scene enumeration.
            string mappedText = "GlobalObjectId_V1-2-" + PrototypeGuid + "-" + original.targetObjectId + "-" + original.targetPrefabId;
            if (!GlobalObjectId.TryParse(mappedText, out var mapped))
                throw new InvalidOperationException("Invalid Prototype identity: " + mappedText);
            var resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(mapped);
            if (!byLocalId.TryGetValue(Key(original), out var renderer))
            {
                if (resolved)
                    throw new InvalidOperationException("A deleted source ID now resolves to a different component: " + mappedText);
                missing.Add(source);
                continue;
            }
            if (resolved != renderer || renderer.gameObject.scene != scene || EditorUtility.IsPersistent(renderer)
                || !renderer.TryGetComponent<MeshFilter>(out var sourceFilter) || !sourceFilter.sharedMesh)
                throw new InvalidOperationException("Unsafe Prototype source mapping: " + mappedText);
            sources.Add(renderer);
        }
        var chunks = new List<GameObject>();
        var preservedChunks = new List<GameObject>();
        var preservedSources = new HashSet<MeshRenderer>();
        var replacements = new List<Replacement>();
        int reviewedChunks = 0;
        var canonicalMeshes = new HashSet<string>(StringComparer.Ordinal);
        var protectedMeshes = new HashSet<string>(meshes, StringComparer.Ordinal);
        try
        {
        foreach (var renderer in renderers.Where(r => r.name.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            var go = renderer.gameObject;
            if (!go.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh
                || go.transform.childCount != 0 || go.GetComponents<Component>().Any(c => c == null
                    || !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)))
                throw new InvalidOperationException("Chunk is not a recorded leaf visual; refuse to delete: " + go.name);
            string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
            var repair = Repairs.FirstOrDefault(r => r.name == go.name);
            if (repair.name != null)
            {
                var id = GlobalObjectId.GetGlobalObjectIdSlow(filter);
                if (id.identifierType != 2 || id.assetGUID.ToString() != PrototypeGuid || id.targetObjectId != repair.filterId
                    || id.targetPrefabId != 0 || AssetDatabase.AssetPathToGUID(meshPath) != repair.guid
                    || !go.activeInHierarchy || !renderer.enabled)
                    throw new InvalidOperationException("Reviewed surface identity/visibility changed: " + go.name);
                string original = MeshFolder + "chunk-" + go.name.Substring(Prefix.Length) + ".asset";
                if (!meshes.Contains(original) || !canonicalMeshes.Add(original))
                    throw new InvalidOperationException("Reviewed chunk has no unique original mesh record: " + go.name);
                if (go.name == "A optimized chunk 66")
                {
                    var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(original);
                    foreach (ulong sourceId in ReviewedSourceIds)
                    {
                        var member = sources.SingleOrDefault(r => GlobalObjectId.GetGlobalObjectIdSlow(r).targetObjectId == sourceId);
                        if (!member || member.enabled) throw new InvalidOperationException("Expected disabled reviewed source is missing: " + sourceId);
                        replacements.Add(ExtractReviewedSource(renderer, oldMesh, member, sourceId));
                    }
                }
                else
                    foreach (ulong sourceId in RemovedStairSources)
                        if (!missing.Any(s => s == "GlobalObjectId_V1-2-" + ProductionGuid + "-" + sourceId + "-0"))
                            throw new InvalidOperationException("A supposedly removed old stair source is present or no longer recorded: " + sourceId);
                protectedMeshes.Add(meshPath);
                reviewedChunks++;
                chunks.Add(go);
                continue;
            }
            if (!meshes.Contains(meshPath) || !canonicalMeshes.Add(meshPath))
                throw new InvalidOperationException("Unexpected or duplicate chunk mesh: " + go.name + " -> " + meshPath);
            chunks.Add(go);
        }
        if (chunks.Count > 0 && reviewedChunks != Repairs.Length)
            throw new InvalidOperationException("A reviewed chunk is missing; refuse to restore its untrimmed sources.");
        if (chunks.Count > 0 && (chunks.Count != meshes.Count || canonicalMeshes.Count != meshes.Count))
            throw new InvalidOperationException("Prototype chunks do not have a one-to-one mapping to the recorded shared meshes.");
        return new Plan { scene = scene, sources = sources.Where(r => !preservedSources.Contains(r)).ToArray(), chunks = chunks.ToArray(),
            preservedSources = preservedSources.ToArray(), preservedChunks = preservedChunks.ToArray(),
            missing = missing.ToArray(), meshes = protectedMeshes.ToArray(), recorded = record.sources.Length,
            replacements = replacements.ToArray() };
        }
        catch
        {
            foreach (var replacement in replacements)
                if (replacement.mesh && !EditorUtility.IsPersistent(replacement.mesh)) Object.DestroyImmediate(replacement.mesh);
            throw;
        }
    }

    static Replacement ExtractReviewedSource(MeshRenderer chunk, Mesh original, MeshRenderer source, ulong sourceId)
    {
        const float tolerance = .00025f;
        var reviewed = chunk.GetComponent<MeshFilter>().sharedMesh;
        var filter = source.GetComponent<MeshFilter>();
        var sourceMesh = filter.sharedMesh;
        if (!original || !reviewed || !sourceMesh || !original.isReadable || !reviewed.isReadable || !sourceMesh.isReadable
            || original.subMeshCount != 1 || reviewed.subMeshCount != 1 || sourceMesh.subMeshCount != 1
            || source.sharedMaterials.Length != 1 || source.sharedMaterial != chunk.sharedMaterial)
            throw new InvalidOperationException("Reviewed source has incompatible mesh/material: " + sourceId);
        var oldPoints = original.vertices; var points = reviewed.vertices;
        if (points.Length < oldPoints.Length || oldPoints.Where((v, i) => Vector3.Distance(v, points[i]) > .00001f).Any())
            throw new InvalidOperationException("Reviewed mesh lost the original vertex prefix.");
        Matrix4x4 sourceToChunk = chunk.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
        var sourcePoints = sourceMesh.vertices.Select(sourceToChunk.MultiplyPoint3x4).ToArray();
        var sourceTriangles = sourceMesh.triangles; var originalTriangles = original.triangles;
        if (sourceTriangles.Length != 36) throw new InvalidOperationException("Expected twelve original frame triangles: " + sourceId);
        bool Near(Vector3 x, Vector3 y) => (x - y).sqrMagnitude <= tolerance * tolerance;
        bool Same(Vector3 a, Vector3 b, Vector3 c, Vector3 x, Vector3 y, Vector3 z) =>
            Near(a, x) && Near(b, y) && Near(c, z) || Near(a, y) && Near(b, z) && Near(c, x) || Near(a, z) && Near(b, x) && Near(c, y);
        var owners = new List<(Vector3 a, Vector3 b, Vector3 c)>();
        for (int i = 0; i < sourceTriangles.Length; i += 3)
        {
            var a = sourcePoints[sourceTriangles[i]]; var b = sourcePoints[sourceTriangles[i + 1]]; var c = sourcePoints[sourceTriangles[i + 2]];
            int matches = 0;
            (Vector3 a, Vector3 b, Vector3 c) matched = default;
            for (int j = 0; j < originalTriangles.Length; j += 3)
                if (Same(a, b, c, oldPoints[originalTriangles[j]], oldPoints[originalTriangles[j + 1]], oldPoints[originalTriangles[j + 2]]))
                { matches++; matched = (oldPoints[originalTriangles[j]], oldPoints[originalTriangles[j + 1]], oldPoints[originalTriangles[j + 2]]); }
            if (matches != 1) throw new InvalidOperationException("Source triangle has no unique original owner: " + sourceId + "/" + i / 3 + " matches=" + matches);
            owners.Add(matched);
        }
        bool Inside(Vector3 p, (Vector3 a, Vector3 b, Vector3 c) t)
        {
            Vector3 normal = Vector3.Cross(t.b - t.a, t.c - t.a).normalized;
            return Mathf.Abs(Vector3.Dot(normal, p - t.a)) <= tolerance
                && Vector3.Dot(normal, Vector3.Cross(t.b - t.a, p - t.a)) >= -tolerance * (t.b - t.a).magnitude
                && Vector3.Dot(normal, Vector3.Cross(t.c - t.b, p - t.b)) >= -tolerance * (t.c - t.b).magnitude
                && Vector3.Dot(normal, Vector3.Cross(t.a - t.c, p - t.c)) >= -tolerance * (t.a - t.c).magnitude;
        }
        var selected = new List<int>(); var faceAreas = new float[owners.Count];
        var later = reviewed.triangles;
        for (int i = 0; i < later.Length; i += 3)
        {
            Vector3 a = points[later[i]], b = points[later[i + 1]], c = points[later[i + 2]];
            Vector3 cross = Vector3.Cross(b - a, c - a); float area = cross.magnitude * .5f;
            if (area < 1e-9f) continue;
            int owner = -1;
            for (int j = 0; j < owners.Count; j++)
            {
                var t = owners[j];
                if (Vector3.Dot(cross.normalized, Vector3.Cross(t.b - t.a, t.c - t.a).normalized) < .99999f
                    || !Inside(a, t) || !Inside(b, t) || !Inside(c, t)) continue;
                if (owner >= 0) throw new InvalidOperationException("Reviewed fragment belongs to multiple source faces: " + sourceId);
                owner = j;
            }
            if (owner < 0) continue;
            faceAreas[owner] += area;
            selected.Add(later[i]); selected.Add(later[i + 1]); selected.Add(later[i + 2]);
        }
        int cuts = 0; float oldArea = 0, newArea = faceAreas.Sum();
        for (int i = 0; i < owners.Count; i++)
        {
            var t = owners[i]; float area = Vector3.Cross(t.b - t.a, t.c - t.a).magnitude * .5f;
            oldArea += area;
            if (faceAreas[i] > area + Mathf.Max(.001f, area * .0001f))
                throw new InvalidOperationException("Reviewed fragment area exceeds its source face: " + sourceId + "/" + i);
            if (area - faceAreas[i] > .000001f) cuts++;
        }
        int expectedCuts = sourceId == 1559058404UL ? 4 : 2;
        if (selected.Count == 0 || cuts != expectedCuts || newArea >= oldArea)
            throw new InvalidOperationException("Reviewed cut coverage mismatch: " + sourceId + " cuts=" + cuts + " expected=" + expectedCuts);
        string path = PrivateMeshes + "/chunk66-source-" + sourceId + ".asset";
        if (AssetDatabase.LoadMainAssetAtPath(path)) throw new InvalidOperationException("Private reviewed mesh already exists: " + path);
        var indices = selected.Distinct().ToArray();
        var remap = indices.Select((index, ordinal) => (index, ordinal)).ToDictionary(p => p.index, p => p.ordinal);
        Matrix4x4 toSource = source.transform.worldToLocalMatrix * chunk.transform.localToWorldMatrix;
        var result = new Mesh { name = "Prototype reviewed source " + sourceId, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        result.vertices = indices.Select(i => toSource.MultiplyPoint3x4(points[i])).ToArray();
        var normals = reviewed.normals;
        if (normals.Length == points.Length)
        { var normalMatrix = toSource.inverse.transpose; result.normals = indices.Select(i => normalMatrix.MultiplyVector(normals[i]).normalized).ToArray(); }
        var tangents = reviewed.tangents;
        if (tangents.Length == points.Length)
            result.tangents = indices.Select(i => { var t = tangents[i]; Vector3 v = toSource.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                return new Vector4(v.x, v.y, v.z, t.w * Mathf.Sign(toSource.determinant)); }).ToArray();
        var colors = reviewed.colors;
        if (colors.Length == points.Length) result.colors = indices.Select(i => colors[i]).ToArray();
        for (int channel = 0; channel < 8; channel++)
        {
            var uv = new List<Vector4>(); reviewed.GetUVs(channel, uv);
            if (uv.Count == points.Length) result.SetUVs(channel, indices.Select(i => uv[i]).ToList());
        }
        result.SetTriangles(selected.Select(i => remap[i]).ToArray(), 0, false); result.RecalculateBounds();
        return new Replacement { filter = filter, mesh = result, path = path, originalTriangles = owners.Count,
            reviewedTriangles = selected.Count / 3, originalArea = oldArea, reviewedArea = newArea };
    }

    static string Key(GlobalObjectId id) => id.identifierType + ":" + id.targetObjectId + ":" + id.targetPrefabId;
    static string Hash(string assetPath)
    {
        string absolute = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), assetPath);
        using var stream = File.OpenRead(absolute);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream));
    }
    static void AssertProtected(Dictionary<string, string> protectedFiles)
    {
        foreach (var pair in protectedFiles)
            if (Hash(pair.Key) != pair.Value)
                throw new InvalidOperationException("Protected file changed outside this scene-only operation: " + pair.Key);
    }
}
