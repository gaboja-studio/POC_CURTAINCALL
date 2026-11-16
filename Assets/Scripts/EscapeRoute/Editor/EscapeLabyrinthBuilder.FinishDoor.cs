using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string FinishedDoorMarker = "85_FittedExitDoorSeals";

        // The original closed leaves leave a 20cm centre slit and 5–10cm perimeter
        // gaps. Fit conventional overlapping steel rebates to the existing doors;
        // the centre seal travels with one leaf when the shared saw puzzle opens it.
        public static object FinishLabyrinthExitDoor()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Labyrinth Prototype edit scene required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var background = frame.Find("EscapeBackground");
            var root = background.Find("70_LabyrinthRemake");
            var door = background.Find("40_AExitCableSaw/거대 양개 탈출문");
            if (!root || !door) throw new InvalidOperationException("Reviewed labyrinth and original twin door required.");
            if (root.Find(FinishedDoorMarker)) return new { applied = false, reason = "Door already fitted" };
            var leaves = door.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name == "28m 철제 문짝").OrderBy(r => LocalBounds(frame, r).center.z).ToArray();
            if (leaves.Length != 2 || leaves.Any(r => !r.enabled || !r.gameObject.activeInHierarchy))
                throw new InvalidOperationException("Two active original door leaves required.");
            var south = LocalBounds(frame, leaves[0]);
            var north = LocalBounds(frame, leaves[1]);
            void Near(float actual, float expected)
            {
                if (Mathf.Abs(actual - expected) > .015f)
                    throw new InvalidOperationException("Exit door has changed; inspect its fitting before applying.");
            }
            foreach (var bounds in new[] { south, north })
            {
                Near(bounds.min.x, 399.6f); Near(bounds.max.x, 400.4f);
                Near(bounds.min.y, .1f); Near(bounds.max.y, 27.9f);
            }
            Near(south.min.z, 66.05f); Near(south.max.z, 77.9f);
            Near(north.min.z, 78.1f); Near(north.max.z, 89.95f);
            var material = leaves[0].sharedMaterial;
            if (!material || leaves[1].sharedMaterial != material)
                throw new InvalidOperationException("Original matched door material required.");
            foreach (var leaf in leaves)
                if (Quaternion.Angle(leaf.transform.parent.rotation, frame.rotation) > .01f)
                    throw new InvalidOperationException("Door must be in its authored closed orientation.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fit labyrinth exit door rebates");
            try
            {
                var marker = new GameObject(FinishedDoorMarker);
                Undo.RegisterCreatedObjectUndo(marker, "Door fitting marker");
                marker.transform.SetParent(root, false);
                int count = 0;
                void Strip(Transform parent, string name, Vector3 min, Vector3 max)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Undo.RegisterCreatedObjectUndo(go, "Fit exit door rebate");
                    go.name = "Fitted exit " + name;
                    go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation(frame.TransformPoint((min + max) * .5f), frame.rotation);
                    Vector3 size = max - min;
                    Vector3 inherited = parent.lossyScale;
                    go.transform.localScale = new Vector3(size.x / Mathf.Abs(inherited.x),
                        size.y / Mathf.Abs(inherited.y), size.z / Mathf.Abs(inherited.z));
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    // Existing leaves and the concrete surround own the collision.
                    // Cosmetic rebates must not change the shared door swing or passage.
                    UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                    count++;
                }
                Strip(leaves[1].transform.parent, "moving centre astragal",
                    new Vector3(399.36f, -.08f, 77.8f), new Vector3(399.66f, 28.08f, 78.2f));
                for (int i = 0; i < leaves.Length; i++)
                {
                    // Butt against the vertical rebates instead of giving two
                    // differently tiled cubes coincident front faces at each corner.
                    float minZ = i == 0 ? 66.12f : 78.2f, maxZ = i == 0 ? 77.8f : 89.88f;
                    Strip(leaves[i].transform.parent, "moving bottom rebate " + i,
                        new Vector3(399.36f, -.08f, minZ), new Vector3(399.66f, .18f, maxZ));
                    Strip(leaves[i].transform.parent, "moving top rebate " + i,
                        new Vector3(399.36f, 27.82f, minZ), new Vector3(399.66f, 28.08f, maxZ));
                }
                Strip(door, "south fixed jamb rebate",
                    new Vector3(399.36f, -.08f, 65.92f), new Vector3(399.66f, 28.08f, 66.12f));
                Strip(door, "north fixed jamb rebate",
                    new Vector3(399.36f, -.08f, 89.88f), new Vector3(399.66f, 28.08f, 90.08f));
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                return new { applied = true, strips = count, centreGapBefore = north.min.z - south.max.z,
                    sharedDoorMotionUnchanged = true, addedColliders = 0 };
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        public static object FitLabyrinthExitDoorSealJoints()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Stopped Labyrinth Prototype required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var background = frame.Find("EscapeBackground");
            var door = background.Find("40_AExitCableSaw/거대 양개 탈출문");
            if (!door || !background.Find("70_LabyrinthRemake/" + FinishedDoorMarker))
                throw new InvalidOperationException("Existing fitted original exit door required.");
            var leaves = door.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "28m 철제 문짝")
                .OrderBy(r => LocalBounds(frame, r).center.z).ToArray();
            if (leaves.Length != 2 || leaves.Any(r => Quaternion.Angle(r.transform.parent.rotation, frame.rotation) > .01f))
                throw new InvalidOperationException("Original twin door must be closed.");
            var seals = door.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name.StartsWith("Fitted exit ", StringComparison.Ordinal)).ToArray();
            if (seals.Length != 7) throw new InvalidOperationException("Expected exactly seven fitted exit seals.");
            MeshRenderer Seal(string name, Transform parent, Vector3 min, Vector3 max)
            {
                var r = seals.SingleOrDefault(s => s.name == "Fitted exit " + name);
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || r.transform.parent != parent
                    || r.transform.childCount != 0 || Quaternion.Angle(r.transform.rotation, frame.rotation) > .01f
                    || r.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer))
                    || r.sharedMaterials.Length != 1 || r.sharedMaterial != leaves[0].sharedMaterial)
                    throw new InvalidOperationException("Unexpected door seal: " + name);
                var b = LocalBounds(frame, r); var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh || mesh.triangles.Length != 36 || Vector3.Distance(b.min, min) > .002f || Vector3.Distance(b.max, max) > .002f)
                    throw new InvalidOperationException("Door seal geometry changed: " + name);
                return r;
            }
            Seal("moving centre astragal", leaves[1].transform.parent,
                new Vector3(399.36f, -.08f, 77.8f), new Vector3(399.66f, 28.08f, 78.2f));
            Seal("south fixed jamb rebate", door,
                new Vector3(399.36f, -.08f, 65.92f), new Vector3(399.66f, 28.08f, 66.12f));
            Seal("north fixed jamb rebate", door,
                new Vector3(399.36f, -.08f, 89.88f), new Vector3(399.66f, 28.08f, 90.08f));
            var changes = new System.Collections.Generic.List<(Transform transform, Vector3 min, Vector3 max)>();
            for (int side = 0; side < 2; side++)
            foreach (bool top in new[] { false, true })
            {
                float minY = top ? 27.82f : -.08f, maxY = top ? 28.08f : .18f;
                float oldMinZ = side == 0 ? 66 : 78, oldMaxZ = side == 0 ? 78 : 90;
                float minZ = side == 0 ? 66.12f : 78.2f, maxZ = side == 0 ? 77.8f : 89.88f;
                string name = "moving " + (top ? "top" : "bottom") + " rebate " + side;
                var existing = seals.SingleOrDefault(s => s.name == "Fitted exit " + name);
                if (!existing) throw new InvalidOperationException("Missing horizontal door seal: " + name);
                var actual = LocalBounds(frame, existing);
                var min = new Vector3(399.36f, minY, minZ); var max = new Vector3(399.66f, maxY, maxZ);
                bool fitted = Vector3.Distance(actual.min, min) < .002f && Vector3.Distance(actual.max, max) < .002f;
                Seal(name, leaves[side].transform.parent, fitted ? min : new Vector3(399.36f, minY, oldMinZ),
                    fitted ? max : new Vector3(399.66f, maxY, oldMaxZ));
                if (!fitted) changes.Add((existing.transform, min, max));
            }
            int CoplanarOverlapCount()
            {
                int count = 0;
                for (int i = 0; i < seals.Length; i++) for (int j = i + 1; j < seals.Length; j++)
                {
                    var a = LocalBounds(frame, seals[i]); var b = LocalBounds(frame, seals[j]);
                    if (Mathf.Abs(a.min.x - b.min.x) < .0005f
                        && Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y) > .0001f
                        && Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z) > .0001f) count++;
                }
                return count;
            }
            int before = CoplanarOverlapCount();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fit non-overlapping exit door seal joints");
            try
            {
                foreach (var change in changes)
                {
                    Undo.RecordObject(change.transform, "Fit door rebate ends");
                    change.transform.position = frame.TransformPoint((change.min + change.max) * .5f);
                    var size = change.max - change.min; var inherited = change.transform.parent.lossyScale;
                    change.transform.localScale = new Vector3(size.x / Mathf.Abs(inherited.x), size.y / Mathf.Abs(inherited.y), size.z / Mathf.Abs(inherited.z));
                }
                int after = CoplanarOverlapCount();
                if (after != 0) throw new InvalidOperationException("Door seal fronts must meet without positive-area overlap.");
                if (changes.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
                return new { applied = changes.Count > 0, resized = changes.Count, coplanarOverlapsBefore = before,
                    coplanarOverlapsAfter = after, doorMotionAndCollisionUnchanged = true };
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Bounds LocalBounds(Transform frame, Renderer renderer)
        {
            var world = renderer.bounds;
            Bounds local = new Bounds(frame.InverseTransformPoint(world.min), Vector3.zero);
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                local.Encapsulate(frame.InverseTransformPoint(new Vector3(x == 0 ? world.min.x : world.max.x,
                    y == 0 ? world.min.y : world.max.y, z == 0 ? world.min.z : world.max.z)));
            return local;
        }
    }
}
