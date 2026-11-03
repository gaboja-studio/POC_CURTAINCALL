using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeFactoryArtPass
    {
        static Transform Module(Transform item)
        {
            while (item.parent != null && item.parent != background) item = item.parent;
            return item;
        }
        static void BatchStatic()
        {
            var candidates = background.GetComponentsInChildren<MeshRenderer>(true).Where(r =>
                r.enabled && r.gameObject.activeInHierarchy && r.sharedMaterials.Length == 1
                && r.sharedMaterial != null && r.sharedMaterial.renderQueue < 2450 && PureStatic(r)
                && r.GetComponent<MeshFilter>() is { sharedMesh: { isReadable: true } } filter
                && filter.sharedMesh.subMeshCount == 1 && !referenced.Contains(filter)
                && r.GetComponentInParent<Collider>() == null && !Concrete(r.sharedMaterial)
                && r.GetComponentInParent<LODGroup>() == null).ToArray();
            var groups = candidates.GroupBy(r =>
            {
                Vector3 c = r.bounds.center;
                return (module: Module(r.transform), material: r.sharedMaterial,
                    x: Mathf.FloorToInt(c.x / 24), y: Mathf.FloorToInt(c.y / 24), z: Mathf.FloorToInt(c.z / 24));
            }).ToArray();
            int sources = 0;
            foreach (var group in groups)
            {
                if (group.Count() < 3) continue;
                var parent = group.Key.module;
                var node = new GameObject("Factory static " + batches); node.transform.SetParent(parent, false);
                var combines = new List<CombineInstance>();
                foreach (var r in group)
                {
                    var filter = r.GetComponent<MeshFilter>(); var mesh = filter.sharedMesh;
                    combines.Add(new CombineInstance { mesh = mesh, transform = node.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                var combined = new Mesh { name = "A static chunk " + batches, indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(combines.ToArray(), true, true, false); combined.RecalculateBounds();
                AssetDatabase.CreateAsset(combined, MeshOutput + "/static-" + batches + ".asset");
                node.AddComponent<MeshFilter>().sharedMesh = combined;
                var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key.material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(node, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                foreach (var old in group)
                {
                    var go = old.gameObject; Unpack(go);
                    UnityEngine.Object.DestroyImmediate(old.GetComponent<MeshFilter>()); UnityEngine.Object.DestroyImmediate(old);
                    if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1
                        && !referenced.Contains(go) && !referenced.Contains(go.transform)) UnityEngine.Object.DestroyImmediate(go);
                    sources++;
                }
                batches++;
            }
            report.Add($"staticSources={sources}; chunks={batches}; cellSize=24m; worldProjectedConcrete=2.5m");
        }
    }
}
