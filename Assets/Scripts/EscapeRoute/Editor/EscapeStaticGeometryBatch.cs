using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapeStaticGeometryBatch
    {
        const string ScenePath = "Assets/Scenes/Levels/escape_route.unity";
        const string RecordPath = "Assets/Scenes/Levels/EscapeFactoryArt/static-batch-record.asset";
        const string MeshFolder = "Assets/_ThirdParty/Task073/StaticOptimized";
        const string Prefix = "A optimized chunk ";

        static Scene RequireScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Open A in Edit Mode.");
            return scene;
        }

        [MenuItem("CurtainCall/Escape/Optimization/Restore editable geometry")]
        public static void Restore()
        {
            var scene = RequireScene();
            var record = AssetDatabase.LoadAssetAtPath<EscapeStaticBatchRecord>(RecordPath);
            if (record == null) return;
            foreach (string source in record.sources)
                if (GlobalObjectId.TryParse(source, out var id)
                    && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is MeshRenderer renderer)
                {
                    renderer.enabled = true;
                    EditorUtility.SetDirty(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            foreach (var filter in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)))
                if (filter.name.StartsWith(Prefix, StringComparison.Ordinal)
                    && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(MeshFolder + "/", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(filter.gameObject);
            foreach (string mesh in record.meshes)
                if (mesh.StartsWith(MeshFolder + "/", StringComparison.Ordinal))
                    AssetDatabase.DeleteAsset(mesh);
            record.sources = Array.Empty<string>();
            record.meshes = Array.Empty<string>();
            EditorUtility.SetDirty(record);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssetIfDirty(record);
        }

        [MenuItem("CurtainCall/Escape/Optimization/Rebuild static geometry chunks")]
        public static void Rebuild()
        {
            var scene = RequireScene();
            Restore();
            var background = scene.GetRootGameObjects().Single().transform.Find("EscapeBackground");
            var references = new HashSet<UnityEngine.Object>();
            foreach (var behaviour in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (behaviour == null) continue;
                var property = new SerializedObject(behaviour).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                        references.Add(property.objectReferenceValue);
            }
            bool Pure(MeshRenderer renderer)
            {
                if (references.Contains(renderer) || references.Contains(renderer.GetComponent<MeshFilter>())
                    || renderer.HasPropertyBlock() || renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534)
                    return false;
                for (var t = renderer.transform; t != null && t != background; t = t.parent)
                    if (t.GetComponent<LODGroup>() != null || t.GetComponent<Rigidbody>() != null
                        || t.GetComponent<Animator>() != null || t.GetComponents<MonoBehaviour>().Length != 0
                        || references.Contains(t) || references.Contains(t.gameObject))
                        return false;
                return true;
            }
            Transform Module(Transform t)
            {
                while (t.parent != background) t = t.parent;
                return t;
            }
            var candidates = background.GetComponentsInChildren<MeshRenderer>(true).Where(r =>
                r.enabled && r.gameObject.activeInHierarchy && r.sharedMaterials.Length == 1
                && r.sharedMaterial != null && r.sharedMaterial.renderQueue < 2450
                && r.GetComponent<MeshFilter>() is { sharedMesh: { isReadable: true, subMeshCount: 1 } }
                && r.bounds.size.magnitude < 24 && Pure(r)).ToArray();
            var groups = candidates.GroupBy(r => (
                module: Module(r.transform), material: r.sharedMaterial, r.shadowCastingMode,
                r.receiveShadows, r.gameObject.layer, r.renderingLayerMask,
                x: Mathf.FloorToInt(r.bounds.center.x / 16),
                y: Mathf.FloorToInt(r.bounds.center.y / 16),
                z: Mathf.FloorToInt(r.bounds.center.z / 16))).Where(g => g.Count() >= 4).ToArray();
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/_ThirdParty/Task073", "StaticOptimized");
            var record = AssetDatabase.LoadAssetAtPath<EscapeStaticBatchRecord>(RecordPath);
            if (record == null)
            {
                record = ScriptableObject.CreateInstance<EscapeStaticBatchRecord>();
                AssetDatabase.CreateAsset(record, RecordPath);
            }
            var sourceIds = new List<string>();
            var meshPaths = new List<string>();
            var changedSources = new List<MeshRenderer>();
            var createdNodes = new List<GameObject>();
            try
            {
                foreach (var group in groups)
                {
                    var node = new GameObject(Prefix + meshPaths.Count);
                    createdNodes.Add(node);
                    node.transform.SetParent(group.Key.module, false);
                    node.layer = group.Key.layer;
                    var combined = new Mesh { name = node.name, indexFormat = IndexFormat.UInt32 };
                    combined.CombineMeshes(group.Select(r => new CombineInstance {
                        mesh = r.GetComponent<MeshFilter>().sharedMesh,
                        transform = node.transform.worldToLocalMatrix * r.transform.localToWorldMatrix
                    }).ToArray(), true, true, false);
                    combined.RecalculateBounds();
                    string path = MeshFolder + "/chunk-" + meshPaths.Count + ".asset";
                    AssetDatabase.CreateAsset(combined, path);
                    meshPaths.Add(path);
                    node.AddComponent<MeshFilter>().sharedMesh = combined;
                    var renderer = node.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = group.Key.material;
                    renderer.shadowCastingMode = group.Key.shadowCastingMode;
                    renderer.receiveShadows = group.Key.receiveShadows;
                    renderer.renderingLayerMask = group.Key.renderingLayerMask;
                    foreach (var original in group)
                    {
                        sourceIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(original).ToString());
                        changedSources.Add(original);
                        original.enabled = false;
                        EditorUtility.SetDirty(original);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(original);
                    }
                }
                record.sources = sourceIds.ToArray();
                record.meshes = meshPaths.ToArray();
                EditorUtility.SetDirty(record);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"A static rendering: {sourceIds.Count} sources → {meshPaths.Count} chunks. Use Restore editable geometry before editing their visuals.");
            }
            catch
            {
                foreach (var original in changedSources)
                    if (original != null)
                    {
                        original.enabled = true;
                        EditorUtility.SetDirty(original);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(original);
                    }
                foreach (var node in createdNodes)
                    if (node != null) UnityEngine.Object.DestroyImmediate(node);
                foreach (string mesh in meshPaths) AssetDatabase.DeleteAsset(mesh);
                record.sources = Array.Empty<string>();
                record.meshes = Array.Empty<string>();
                EditorUtility.SetDirty(record);
                AssetDatabase.SaveAssetIfDirty(record);
                EditorSceneManager.MarkSceneDirty(scene);
                throw;
            }
        }
    }
}
