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
        public static object FinishLabyrinthThermalFacadeFoot()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Stopped Prototype required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var root = frame.Find("EscapeBackground/70_LabyrinthRemake");
            const string markerName = "88_FlushThermalFoundation";
            if (!root || !root.Find("86_FlushThermalFacade")) throw new InvalidOperationException("Fit the upper thermal facade first.");
            if (root.Find(markerName)) return new { applied = false, reason = "Thermal foundation already fitted" };
            Physics.SyncTransforms();
            var candidates = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy
                && r.name.StartsWith("Concrete remainder", StringComparison.Ordinal)).Where(r =>
                {
                    var b = LocalBounds(frame, r);
                    return Vector3.Distance(b.min, new Vector3(130, -54, 50)) < .002f
                        && Vector3.Distance(b.max, new Vector3(131, 0, 102)) < .002f;
                }).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("Expected one protruding thermal wall stem.");
            var source = candidates[0]; var mesh = source.GetComponent<MeshFilter>()?.sharedMesh;
            var box = LocalBounds(frame, source); var colliders = source.GetComponents<Collider>();
            var components = source.GetComponents<Component>();
            if (!mesh || mesh.triangles.Length != 36 || source.transform.childCount != 0 || source.sharedMaterials.Length != 1 || !source.sharedMaterial
                || colliders.Length != 1 || !colliders[0].enabled || colliders[0].isTrigger
                || Vector3.Distance(colliders[0].bounds.center, source.bounds.center) > .0005f
                || Vector3.Distance(colliders[0].bounds.size, source.bounds.size) > .0005f
                || components.Any(c => !(c is Transform || c is MeshRenderer || c is MeshFilter || c is BoxCollider || c is MeshCollider))
                || mesh.vertices.Any(v =>
                {
                    var p = frame.InverseTransformPoint(source.transform.TransformPoint(v));
                    return Enumerable.Range(0, 3).Any(a => Mathf.Min(Mathf.Abs(p[a] - box.min[a]), Mathf.Abs(p[a] - box.max[a])) > .0005f);
                })) throw new InvalidOperationException("Thermal foundation must be a matching structural cuboid.");
            var referenced = new HashSet<Object>();
            foreach (var behavior in frame.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behavior) continue;
                var it = new SerializedObject(behavior).GetIterator();
                while (it.Next(true)) if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue)
                    referenced.Add(it.objectReferenceValue);
            }
            if (referenced.Contains(source.gameObject) || components.Any(referenced.Contains))
                throw new InvalidOperationException("Thermal foundation is referenced by gameplay.");
            int supports = 0;
            foreach (float x in new[] { 129.05f, 129.5f, 129.95f })
            foreach (float z in new[] { 51.55f, 75f, 101.95f })
            {
                if (!Physics.Raycast(frame.TransformPoint(new Vector3(x, -54.98f, z)), -frame.up, out var floor,
                        .05f, ~0, QueryTriggerInteraction.Ignore)
                    || floor.collider.name != "북측 55m 하부 집수"
                    || Mathf.Abs(frame.InverseTransformPoint(floor.point).y + 55) > .005f
                    || !Physics.Raycast(frame.TransformPoint(new Vector3(x, -9.02f, z)), frame.up, out var backing,
                        .05f, ~0, QueryTriggerInteraction.Ignore)
                    || !backing.collider.name.StartsWith("Concrete remainder", StringComparison.Ordinal)
                    || Mathf.Abs(frame.InverseTransformPoint(backing.point).y + 9) > .005f)
                    throw new InvalidOperationException("Every foundation sample must join the real deep floor and upper backing wall.");
                supports++;
            }
            var material = source.sharedMaterial; int layer = source.gameObject.layer;
            var flags = GameObjectUtility.GetStaticEditorFlags(source.gameObject);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fit thermal wall foundation flush to its facade");
            try
            {
                var marker = new GameObject(markerName); Undo.RegisterCreatedObjectUndo(marker, "Thermal foundation marker");
                marker.transform.SetParent(root, false);
                var stem = GameObject.CreatePrimitive(PrimitiveType.Cube); stem.name = "Thermal wall flush deep foundation";
                Undo.RegisterCreatedObjectUndo(stem, "Join thermal wall to deep foundation"); stem.transform.SetParent(root, false);
                stem.transform.SetPositionAndRotation(frame.TransformPoint(new Vector3(129.5f, -32, 76.75f)), frame.rotation);
                var inherited = root.lossyScale;
                stem.transform.localScale = new Vector3(1 / Mathf.Abs(inherited.x), 46 / Mathf.Abs(inherited.y), 50.5f / Mathf.Abs(inherited.z));
                stem.GetComponent<MeshRenderer>().sharedMaterial = material; stem.layer = layer;
                GameObjectUtility.SetStaticEditorFlags(stem, flags);
                Undo.DestroyObjectImmediate(source.gameObject); Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
                return new { applied = true, removed = 1, created = 1, supports, facadePlaneX = 130,
                    foundationBottom = -55, foundationTop = -9, newPublicFloor = false };
            }
            catch { Undo.RevertAllDownToGroup(undo); Physics.SyncTransforms(); throw; }
        }
    }
}
