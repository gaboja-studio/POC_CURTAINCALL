using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static object FinishLabyrinthThermalFacade()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Stopped Prototype required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var root = frame.Find("EscapeBackground/70_LabyrinthRemake");
            const string markerName = "86_FlushThermalFacade";
            if (!root) throw new InvalidOperationException("Labyrinth root missing.");
            if (root.Find(markerName)) return new { applied = false, reason = "Thermal facade already fitted" };
            Physics.SyncTransforms();

            // The former upper/lower cut leaves a metre-thick skin projecting from
            // the backing wall and three different front edges at Z50/51/51.5.
            // Remove those projections. The existing 3m backing stays solid.
            Bounds Volume(Vector3 min, Vector3 max) { var b = new Bounds(); b.SetMinMax(min, max); return b; }
            var cuts = new[]
            {
                Volume(new Vector3(130, 0, 50), new Vector3(131, 32, 102)),
                Volume(new Vector3(126, 4.2f, 50), new Vector3(130, 32, 51.5f))
            };
            var referenced = new HashSet<Object>();
            foreach (var behavior in frame.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behavior) continue;
                var it = new SerializedObject(behavior).GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue)
                        referenced.Add(it.objectReferenceValue);
            }
            var candidates = new List<(MeshRenderer renderer, Bounds bounds)>();
            foreach (var renderer in frame.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || !renderer.name.StartsWith("Concrete remainder", StringComparison.Ordinal)) continue;
                var bounds = LocalBounds(frame, renderer);
                if (!cuts.Any(c => FacadeOverlaps(bounds, c))) continue;
                var components = renderer.GetComponents<Component>();
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                var colliders = renderer.GetComponents<Collider>();
                bool aligned = mesh && mesh.triangles.Length == 36 && mesh.vertices.All(v =>
                {
                    var p = frame.InverseTransformPoint(renderer.transform.TransformPoint(v));
                    return Enumerable.Range(0, 3).All(a => Mathf.Min(Mathf.Abs(p[a] - bounds.min[a]),
                        Mathf.Abs(p[a] - bounds.max[a])) < .0005f);
                });
                if (renderer.transform.childCount != 0 || renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterial
                    || !aligned || colliders.Length != 1 || !colliders[0].enabled || colliders[0].isTrigger
                    || Vector3.Distance(colliders[0].bounds.center, renderer.bounds.center) > .0005f
                    || Vector3.Distance(colliders[0].bounds.size, renderer.bounds.size) > .0005f
                    || components.Any(c => !(c is Transform || c is MeshRenderer || c is MeshFilter || c is BoxCollider || c is MeshCollider))
                    || referenced.Contains(renderer.gameObject) || components.Any(referenced.Contains))
                    throw new InvalidOperationException("Facade cut intersects a non-structural or referenced object: " + renderer.name);
                candidates.Add((renderer, bounds));
            }
            foreach (var p in new[] { new Vector3(130.5f, 8, 80), new Vector3(130.5f, 24, 80) })
                if (!candidates.Any(c => c.bounds.Contains(p)))
                    throw new InvalidOperationException("Expected thermal facade changed; inspect before fitting.");

            // Close the old 2m wall/roof gap only after checking real support and roof
            // colliders across the complete cap, well above all public movements.
            Material capMaterial = null;
            foreach (float x in new[] { 126.05f, 128f, 129.95f })
            foreach (float z in new[] { 51.55f, 75f, 101.95f })
            {
                Vector3 origin = frame.TransformPoint(new Vector3(x, 33, z));
                if (!Physics.Raycast(origin, frame.up, out var ceiling, 1.02f, ~0, QueryTriggerInteraction.Ignore)
                    || !Physics.Raycast(origin, -frame.up, out var wall, 1.02f, ~0, QueryTriggerInteraction.Ignore)
                    || Mathf.Abs(frame.InverseTransformPoint(ceiling.point).y - 34) > .01f
                    || Mathf.Abs(frame.InverseTransformPoint(wall.point).y - 32) > .01f
                    || ceiling.collider.name != "북측 설비동 덮개"
                    || !wall.collider.name.StartsWith("Concrete remainder", StringComparison.Ordinal))
                    throw new InvalidOperationException("Thermal coping must touch the existing wall and roof at every sample.");
                capMaterial = wall.collider.GetComponent<MeshRenderer>()?.sharedMaterial;
            }
            if (!capMaterial) throw new InvalidOperationException("Wall cap material missing.");
            int capLayer = candidates[0].renderer.gameObject.layer;
            var capFlags = GameObjectUtility.GetStaticEditorFlags(candidates[0].renderer.gameObject);
            var lamp = root.Find("Extraction corner worklight");
            var mount = root.Find("Extraction corner worklight fixed wall mount");
            var upright = root.Find("Extraction corner worklight fixed steel upright");
            var fin = root.Find("Extraction lamp concrete fin");
            if (!lamp || !lamp.GetComponent<Light>() || !mount || !upright || !fin)
                throw new InvalidOperationException("Original extraction corner lamp and support required.");
            void ExpectedBounds(Transform node, Vector3 min, Vector3 max)
            {
                var renderer = node.GetComponent<MeshRenderer>();
                if (!renderer || node.childCount != 0 || node.GetComponents<MonoBehaviour>().Length != 0
                    || referenced.Contains(node.gameObject) || node.GetComponents<Component>().Any(referenced.Contains))
                    throw new InvalidOperationException("Unexpected extraction lamp support: " + node.name);
                var bounds = LocalBounds(frame, renderer);
                if (Vector3.Distance(bounds.min, min) > .002f || Vector3.Distance(bounds.max, max) > .002f)
                    throw new InvalidOperationException("Extraction lamp support moved: " + node.name);
            }
            ExpectedBounds(fin, new Vector3(143.5f, -30, 43), new Vector3(144.5f, 6, 45));
            ExpectedBounds(mount, new Vector3(143.725f, 5, 43.8f), new Vector3(144.275f, 5.5f, 44.2f));
            ExpectedBounds(upright, new Vector3(143.93f, 0, 43.93f), new Vector3(144.07f, 5, 44.07f));
            if (lamp.childCount != 0 || lamp.GetComponents<Component>().Any(c =>
                    !(c is Transform || c is Light || c is UnityEngine.Rendering.Universal.UniversalAdditionalLightData))
                || Vector3.Distance(frame.InverseTransformPoint(lamp.position), new Vector3(144, 5, 44)) > .002f)
                throw new InvalidOperationException("Extraction corner worklight changed; inspect its attachment.");

            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fit continuous thermal facade");
            int removed = 0, created = 0;
            try
            {
                var marker = new GameObject(markerName);
                Undo.RegisterCreatedObjectUndo(marker, "Thermal facade marker"); marker.transform.SetParent(root, false);
                GameObject Make(string name, Transform parent, Bounds b, Material material, int layer, StaticEditorFlags flags)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Undo.RegisterCreatedObjectUndo(go, "Fit thermal facade"); go.name = name;
                    go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation(frame.TransformPoint(b.center), frame.rotation);
                    var scale = parent.lossyScale;
                    go.transform.localScale = new Vector3(b.size.x / Mathf.Abs(scale.x), b.size.y / Mathf.Abs(scale.y), b.size.z / Mathf.Abs(scale.z));
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    go.layer = layer; GameObjectUtility.SetStaticEditorFlags(go, flags); created++; return go;
                }
                foreach (var row in candidates)
                {
                    var pieces = new List<Bounds> { row.bounds };
                    foreach (var cut in cuts) pieces = pieces.SelectMany(p => FacadeSubtract(p, cut)).ToList();
                    foreach (var piece in pieces)
                        Make("Concrete remainder", row.renderer.transform.parent, piece, row.renderer.sharedMaterial,
                            row.renderer.gameObject.layer, GameObjectUtility.GetStaticEditorFlags(row.renderer.gameObject));
                    Undo.DestroyObjectImmediate(row.renderer.gameObject); removed++;
                }
                // The cap belongs to the same architectural root; it creates no low
                // ledge, shortcut or collision beneath the nearby extraction poles.
                Make("Thermal facade continuous roof coping", root,
                    Volume(new Vector3(126, 32, 51.5f), new Vector3(130, 34, 102)), capMaterial, capLayer, capFlags);
                // The old 36m concrete fin ended in mid-air at Y-30. Reuse its
                // actual lamp on the existing wall, with the bracket's back at X130.
                // Preserve light settings and the intended landing target.
                Undo.RecordObject(lamp, "Mount extraction lamp on continuous wall");
                Undo.RecordObject(mount, "Fit extraction lamp bracket");
                lamp.SetPositionAndRotation(frame.TransformPoint(new Vector3(130.45f, 5, 52)),
                    Quaternion.LookRotation(frame.TransformPoint(new Vector3(140, 0, 47))
                        - frame.TransformPoint(new Vector3(130.45f, 5, 52))));
                mount.SetPositionAndRotation(frame.TransformPoint(new Vector3(130.2f, 5, 52)),
                    frame.rotation * Quaternion.Euler(0, 90, 0));
                Undo.DestroyObjectImmediate(upright.gameObject);
                Undo.DestroyObjectImmediate(fin.gameObject);
                Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
                return new { applied = true, removed, created, backingFaceX = 130, frontEdgeZ = 51.5f,
                    capSupportSamples = 9, minimumNewStructuralGeometryHeight = 32,
                    floatingLampFinRemoved = true, lampSettingsPreserved = true };
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static bool FacadeOverlaps(Bounds a, Bounds b)
        {
            Vector3 s = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
            return s.x > .0005f && s.y > .0005f && s.z > .0005f;
        }

        static IEnumerable<Bounds> FacadeSubtract(Bounds source, Bounds cut)
        {
            if (!FacadeOverlaps(source, cut)) { yield return source; yield break; }
            Vector3 lo = Vector3.Max(source.min, cut.min), hi = Vector3.Min(source.max, cut.max);
            var boxes = new[]
            {
                (source.min, new Vector3(lo.x,source.max.y,source.max.z)),
                (new Vector3(hi.x,source.min.y,source.min.z), source.max),
                (new Vector3(lo.x,source.min.y,source.min.z), new Vector3(hi.x,lo.y,source.max.z)),
                (new Vector3(lo.x,hi.y,source.min.z), new Vector3(hi.x,source.max.y,source.max.z)),
                (new Vector3(lo.x,lo.y,source.min.z), new Vector3(hi.x,hi.y,lo.z)),
                (new Vector3(lo.x,lo.y,hi.z), new Vector3(hi.x,hi.y,source.max.z))
            };
            foreach (var (min, max) in boxes)
            {
                Vector3 size = max - min;
                if (size.x > .0005f && size.y > .0005f && size.z > .0005f)
                    yield return new Bounds((min + max) * .5f, size);
            }
        }
    }
}
