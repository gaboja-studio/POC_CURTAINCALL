using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        // Frame the mannequin only, excluding its potentially distant patrol point children.
        static void FrameInScene(GameObject target, GameObject model)
        {
            if (target == null) return;
            var bounds = new Bounds(target.transform.position + Vector3.up, new Vector3(1, 2, 1));
            bool found = false;
            if (model != null) foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
            var sceneView = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            sceneView.in2DMode = false;
            float size = Mathf.Max(.8f, bounds.extents.magnitude);
            var rotation = Quaternion.Euler(15, target.transform.eulerAngles.y + 135, 0);
            float best = -1;
            Physics.SyncTransforms();
            for (int i = 0; i < 8; i++) {
                var candidate = Quaternion.Euler(15, target.transform.eulerAngles.y + 135 + i * 45, 0);
                var direction = candidate * Vector3.back;
                float distance = size * 2.2f;
                float clear = Physics.SphereCast(bounds.center, .15f, direction, out var hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.distance : distance;
                if (clear <= best) continue;
                best = clear; rotation = candidate;
            }
            sceneView.sceneLighting = false; // Editor inspection light; scene lighting data is untouched.
            sceneView.LookAt(bounds.center, rotation, size, false, true);
            sceneView.Focus();
            sceneView.Repaint();
        }
    }
}
