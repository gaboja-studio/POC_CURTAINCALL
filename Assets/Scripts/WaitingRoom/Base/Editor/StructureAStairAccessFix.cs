using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>침대 밑 발견품과 계단 출입구만 수정한다. 기존 다른 탐색 배치는 보존한다.</summary>
    public static class StructureAStairAccessFix
    {
        [MenuItem("CurtainCall/Waiting Room/Fix Bed Pickup And Stair Access")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || !scene.path.EndsWith("/WaitingRoomBase.unity"))
                throw new InvalidOperationException("Saved WaitingRoomBase EditMode required.");
            var layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Fix bed pickup and stair access");
            try
            {
                var bed = layout.Find("S02_Dormitory/Dormitory/Beds/Bed_B").GetComponent<Collider>().bounds;
                var hairpin = layout.Find("S02_Dormitory/Exploration_A/bed-hairpin");
                Undo.RecordObject(hairpin, "Expose bed hairpin edge");
                hairpin.position = new Vector3(bed.min.x - .08f, .04f, bed.center.z);

                // Two wall ledgers cross the descending flight. Keep only the portion outside its east guard.
                EnsureSideLedger(layout.Find("ST_Stairwell/B1Level/Shell/WallStructure"), new Vector3(.35f, -2.8f, 101.93f), 1.1f);
                EnsureSideLedger(layout.Find("B1_MachineRoom/DeepPlant/UpperShell/Shell/WallStructure"), new Vector3(-1.45f, -2.8f, 101.77f), 4.7f);
                var ledgers = layout.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.name == "SteelLedger"
                    && (r.transform.IsChildOf(layout.Find("ST_Stairwell/B1Level")) || r.transform.IsChildOf(layout.Find("B1_MachineRoom/DeepPlant/UpperShell")))
                    && Mathf.Abs(r.bounds.center.y + 2.8f) < .02f && Mathf.Abs(r.bounds.center.z - 101.85f) < .2f).ToArray();
                foreach (var ledger in ledgers)
                {
                    var b = ledger.bounds; const float clearToX = 12.5f;
                    if (b.max.x <= 3.5f || b.min.x >= clearToX - .001f) continue;
                    if (b.max.x <= clearToX) { Undo.DestroyObjectImmediate(ledger.gameObject); continue; }
                    var t = ledger.transform; Undo.RecordObject(t, "Remove cross-stair steel span");
                    var localX = t.InverseTransformDirection(Vector3.right);
                    var scale = t.localScale; float ratio = (b.max.x - clearToX) / b.size.x;
                    if (Mathf.Abs(localX.x) > .9f) scale.x *= ratio;
                    else if (Mathf.Abs(localX.z) > .9f) scale.z *= ratio;
                    else throw new InvalidOperationException("Unexpected ledger orientation.");
                    t.localScale = scale; t.position += Vector3.right * ((clearToX + b.max.x) * .5f - b.center.x);
                }

                var assembly = layout.Find("ST_Stairwell/StairwellDoorway");
                foreach (var name in new[] { "LeafHingeL", "LeafHingeR" })
                {
                    var hinge = assembly.Find(name); Undo.RecordObject(hinge, "Close locked stair door"); hinge.localRotation = Quaternion.identity;
                }
                Physics.SyncTransforms();
                var leaves = assembly.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "LockedDoorLeafL" || r.name == "LockedDoorLeafR").ToArray();
                var bounds = leaves[0].bounds; foreach (var leaf in leaves.Skip(1)) bounds.Encapsulate(leaf.bounds);
                var gate = layout.Find("ST_Stairwell/Exploration_A/stairsDoor");
                Undo.RecordObject(gate, "Align stair gate to closed leaves");
                gate.SetPositionAndRotation(new Vector3(bounds.center.x, .0f, bounds.center.z), Quaternion.identity);
                var blocker = gate.GetComponent<BoxCollider>(); Undo.RecordObject(blocker, "Fit closed door collision");
                blocker.center = gate.InverseTransformPoint(bounds.center); blocker.size = bounds.size; blocker.enabled = true;
                var visual = gate.Find("Visual"); Undo.RecordObject(visual, "Fit hidden door panel");
                visual.position = bounds.center; visual.rotation = Quaternion.identity; visual.localScale = Vector3.Max(bounds.size - Vector3.one * .02f, Vector3.one * .01f);
                foreach (var name in new[] { "OutlineProxy_Front", "OutlineProxy_Back" })
                {
                    var proxy = gate.Find(name); Undo.RecordObject(proxy, "Align outline to closed door");
                    proxy.position = new Vector3(bounds.center.x, bounds.center.y, name.EndsWith("Front") ? bounds.max.z + .008f : bounds.min.z - .008f);
                    proxy.rotation = Quaternion.identity; proxy.localScale = new Vector3(bounds.size.x + .02f, bounds.size.y + .02f, .004f);
                }
                var anchor = gate.Find("InteractionPoint");
                if (anchor == null)
                {
                    var go = new GameObject("InteractionPoint"); Undo.RegisterCreatedObjectUndo(go, "Add stair lock interaction point");
                    go.transform.SetParent(gate, false); anchor = go.transform;
                }
                Undo.RecordObject(anchor, "Place interaction on lock face");
                anchor.position = new Vector3(bounds.center.x, 1.1f, bounds.min.z - .08f);
                var so = new SerializedObject(gate.GetComponent<ExplorationGate>());
                so.FindProperty("interactionPoint").objectReferenceValue = anchor; so.ApplyModifiedProperties();
                var hardware = assembly.Find("DoorLockPoint"); Undo.RecordObject(hardware, "Place lock on closed seam");
                hardware.position = anchor.position;
                Physics.SyncTransforms(); Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"Bed pickup exposed at {hairpin.position}; two crossing ledgers trimmed; stair leaves/collision/outline/interaction aligned at {bounds.center}.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        // Preserve the original short west-wall ledgers; only spans crossing x3.5–12.5 are removed.
        static void EnsureSideLedger(Transform parent, Vector3 center, float width)
        {
            if (parent.GetComponentsInChildren<Renderer>(true).Any(r => r.name == "SteelLedger" && Vector3.Distance(r.bounds.center, center) < .02f)) return;
            var sample = parent.GetComponentsInChildren<Renderer>(true).First(r => r.name == "SteelLedger");
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "SteelLedger";
            Undo.RegisterCreatedObjectUndo(go, "Preserve west-wall ledger"); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.identity); go.transform.localScale = new Vector3(width, .24f, .16f);
            go.GetComponent<Renderer>().sharedMaterial = sample.sharedMaterial; Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
        }
    }
}
