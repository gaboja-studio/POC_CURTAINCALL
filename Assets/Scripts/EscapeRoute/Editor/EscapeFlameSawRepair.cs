using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapeFlameSawRepair
    {
        [MenuItem("Tools/CurtainCall/탈출로/A 화염 예고·인원별 절단기 복구")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.isDirty || scene.path != "Assets/Scenes/Levels/escape_route.unity")
                throw new InvalidOperationException("Saved A edit scene required");
            var frame = scene.GetRootGameObjects().Single().transform;
            var background = frame.Find("EscapeBackground");
            var baked = background.GetComponentsInChildren<MeshRenderer>(true).Where(r =>
                r.name.StartsWith("Factory static ", StringComparison.Ordinal)
                && r.sharedMaterial != null && r.sharedMaterial.name == "solo-danger-red").ToArray();
            if (baked.Length != 3) throw new InvalidOperationException("Review legacy red batches before modifying them");
            var paint = baked[0].sharedMaterial;
            foreach (var renderer in baked) { Undo.RecordObject(renderer, "Remove baked warning paint"); renderer.enabled = false; }
            Physics.SyncTransforms();
            foreach (var cue in frame.GetComponentsInChildren<HumanCannonHazardCue>(true))
            {
                if (cue.warning == null) continue;
                if (cue.warning.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    var hazard = cue.GetComponent<HumanCannonHazard>();
                    if (hazard == null) throw new InvalidOperationException("Warning owner missing");
                    var size = new SerializedObject(hazard).FindProperty("boxSize").vector3Value;
                    Vector3 a = frame.InverseTransformPoint(cue.origin), b = frame.InverseTransformPoint(cue.destination);
                    Vector3 center = (a + b) * .5f;
                    Vector3 floor = GroundAt(frame.TransformPoint(center));
                    center.y = frame.InverseTransformPoint(floor).y + .04f;
                    float width = hazard.Kind == CannonHazardKind.FireJet ? 2.15f : Mathf.Abs(a.x - b.x) + size.x;
                    float depth = hazard.Kind == CannonHazardKind.FireJet ? size.z : Mathf.Abs(a.z - b.z) + Mathf.Max(1.2f, size.z);
                    Lines(frame, cue.warning, center, width, depth, paint);
                }
                Undo.RecordObject(cue.warning.gameObject, "Warning starts hidden");
                cue.warning.gameObject.SetActive(false);
            }
            var existing = frame.GetComponentsInChildren<EscapeExitSaw>(true);
            var template = existing.Single(s => new SerializedObject(s).FindProperty("deviceId").intValue == 1);
            for (int id = 2; id < 4; id++)
            {
                if (existing.Any(s => new SerializedObject(s).FindProperty("deviceId").intValue == id)) continue;
                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                Undo.RegisterCreatedObjectUndo(clone, "Additional crew launcher");
                clone.name = "인원별 휴대 절단기 시스템 " + (id + 1);
                var saw = clone.GetComponent<EscapeExitSaw>(); var data = new SerializedObject(saw);
                data.FindProperty("deviceId").intValue = id;
                saw.CarryRoot.name = "휴대 절단기 " + (id + 1);
                var at = frame.TransformPoint(new Vector3(364, 0, id == 2 ? 75.2f : 80.8f));
                saw.CarryRoot.position = at;
                ((Transform)data.FindProperty("recoveryAnchor").objectReferenceValue).position = at;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var saw in frame.GetComponentsInChildren<EscapeExitSaw>(true))
            {
                var data = new SerializedObject(saw); data.FindProperty("scaleWithCrew").boolValue = true;
                data.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects(); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        static Vector3 GroundAt(Vector3 center)
        {
            var hits = Physics.RaycastAll(center + Vector3.up * 3, Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore);
            var support = hits.Where(h => h.normal.y > .75f).OrderBy(h => h.distance).ToArray();
            if (support.Length == 0) throw new InvalidOperationException("Warning has no physical floor");
            return support[0].point;
        }

        static void Lines(Transform frame, Transform owner, Vector3 at, float width, float depth, Material paint)
        {
            void Strip(string name, Vector3 center, Vector3 size)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(go, "Restore controlled warning");
                go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetPositionAndRotation(frame.TransformPoint(center), frame.rotation);
                go.transform.localScale = size; go.transform.SetParent(owner, true);
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = paint;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Strip("점멸 측선", at + Vector3.right * side * width * .5f, new Vector3(.08f, .02f, depth));
                Strip("점멸 진입선", at + Vector3.forward * side * depth * .5f, new Vector3(width - .08f, .02f, .08f));
            }
            for (int i = -1; i <= 1; i++) Strip("점멸 내부 표식", at + Vector3.right * i * width * .25f,
                new Vector3(.06f, .02f, Mathf.Min(.65f, depth * .4f)));
        }
    }
}
