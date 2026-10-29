using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // Stage 9 performance finish: hand-sized fittings, ties and bearers cast shadows nobody can see but each one
        // is drawn again in every shadow pass. Only the shadow flag changes; placement and look stay as they are.
        public static string FinishShadowCasters()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Stage 9: drop shadows from tiny fittings");
            try
            {
                var supports = target.Find("SupportFinishing");
                var tiny = target.GetComponentsInChildren<MeshRenderer>()
                    .Where(r => r.shadowCastingMode != ShadowCastingMode.Off && (r.bounds.size.magnitude < .35f || (supports != null && r.transform.IsChildOf(supports))))
                    .ToArray();
                foreach (var r in tiny) { Undo.RecordObject(r, "Drop tiny shadow caster"); r.shadowCastingMode = ShadowCastingMode.Off; }
                AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
                return "Shadow casting turned off on " + tiny.Length + " small fittings";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
    }
}
