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
        [Serializable] public sealed class ForkFloorSeamReport
        {
            public string scene;
            public bool applied;
            public int trimmedSources, trimmedThinCaps, preservedDeepSupports, createdFragments, remainingOverlaps, floorCollisionSamples;
            public List<ForkFloorSeamCut> cuts = new();
        }

        [Serializable] public sealed class ForkFloorSeamCut
        {
            public string source;
            public Vector3 center, size;
            public bool deepSupport;
            public Bounds removed;
            public List<Bounds> retained = new();
        }

        sealed class ForkFloorSeamPlan
        {
            public Transform frame;
            public BoxCollider floor;
            public ForkFloorSeamReport report;
            public readonly List<BoxCollider> sources = new();
        }

        // Cut only the volume already occupied by the new floor. Deep support cores,
        // outside fragments, renderer settings and all shared mesh assets are retained.
        public static ForkFloorSeamReport InspectLabyrinthForkFloorSeams() => PlanForkFloorSeams().report;

        public static ForkFloorSeamReport TrimLabyrinthForkFloorSeams()
        {
            var plan = PlanForkFloorSeams();
            if (plan.sources.Count == 0) return plan.report;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Trim Prototype fork floor caps");
            try
            {
                for (int i = 0; i < plan.sources.Count; i++)
                {
                    var source = plan.sources[i];
                    var cut = plan.report.cuts[i];
                    var flags = GameObjectUtility.GetStaticEditorFlags(source.gameObject);
                    foreach (var bounds in cut.retained)
                    {
                        var piece = Object.Instantiate(source.gameObject, source.transform.parent);
                        Undo.RegisterCreatedObjectUndo(piece, "Retain fork cap outside new floor");
                        piece.name = source.name;
                        // All planned boxes are aligned with the unit campaign frame.
                        piece.transform.position = plan.frame.TransformPoint(bounds.center);
                        piece.transform.localScale = Vector3.Scale(source.transform.localScale,
                            new Vector3(bounds.size.x / cut.size.x, bounds.size.y / cut.size.y, bounds.size.z / cut.size.z));
                        GameObjectUtility.SetStaticEditorFlags(piece, flags);
                        plan.report.createdFragments++;
                    }
                    Undo.DestroyObjectImmediate(source.gameObject);
                    plan.report.trimmedSources++;
                }
                var after = PlanForkFloorSeams();
                plan.report.remainingOverlaps = after.sources.Count;
                if (after.sources.Count != 0) throw new InvalidOperationException("Fork cap trim did not reach an idempotent result.");
                Physics.SyncTransforms();
                foreach (var cut in plan.report.cuts)
                    foreach (var offset in new[] { Vector2.zero, new Vector2(-.8f, -.8f), new Vector2(-.8f, .8f), new Vector2(.8f, -.8f), new Vector2(.8f, .8f) })
                    {
                        var point = cut.removed.center;
                        point.x += offset.x * cut.removed.extents.x;
                        point.z += offset.y * cut.removed.extents.z;
                        point.y = ForkFrameBox(plan.floor, plan.frame).max.y;
                        if (!Physics.RaycastAll(plan.frame.TransformPoint(point) + Vector3.up * .05f, Vector3.down, .1f,
                            ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider == plan.floor && h.normal.y > .99f))
                            throw new InvalidOperationException("Fork floor collision support is absent after trim.");
                        plan.report.floorCollisionSamples++;
                    }
                plan.report.applied = true;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(group);
                return plan.report;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        static ForkFloorSeamPlan PlanForkFloorSeams()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Open the stopped Prototype scene; no scene is opened automatically.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            if (Vector3.Distance(frame.lossyScale, Vector3.one) > .0001f || Vector3.Dot(frame.up, Vector3.up) < .99999f)
                throw new InvalidOperationException("Expected an upright, unit-scale campaign frame.");
            var additions = frame.Find("EscapeBackground/70_LabyrinthRemake");
            if (!additions) throw new InvalidOperationException("Prototype additions are absent.");
            var floor = additions.GetComponentsInChildren<BoxCollider>(true).Single(b => b.name == "Fork concealed floor");
            Bounds opening = ForkFrameBox(floor, frame);
            if (!floor.enabled || floor.isTrigger || !floor.gameObject.activeInHierarchy
                || Vector3.Distance(opening.center, new Vector3(58.7f, -6.45f, 57.65f)) > .01f
                || Vector3.Distance(opening.size, new Vector3(7.6f, .9f, 7.7f)) > .01f)
                throw new InvalidOperationException("Fork floor changed; review its trim envelope before proceeding.");
            var plan = new ForkFloorSeamPlan { frame = frame, floor = floor, report = new ForkFloorSeamReport { scene = scene.path } };
            foreach (var box in additions.GetComponentsInChildren<BoxCollider>(true)
                .Where(b => b.name == "Concrete remainder" && b.enabled && !b.isTrigger && b.gameObject.activeInHierarchy))
            {
                // Do not select other parents, fitted route fragments or any gameplay object.
                if (box.transform.parent != floor.transform.parent) continue;
                Bounds bounds = ForkFrameBox(box, frame);
                Vector3 lo = Vector3.Max(bounds.min, opening.min), hi = Vector3.Min(bounds.max, opening.max);
                if (hi.x - lo.x <= .0005f || hi.z - lo.z <= .0005f || Mathf.Abs(bounds.max.y - opening.max.y) > .001f) continue;
                ValidateForkCap(box);
                var cut = new ForkFloorSeamCut { source = GlobalObjectId.GetGlobalObjectIdSlow(box.gameObject).ToString(),
                    center = bounds.center, size = bounds.size, deepSupport = bounds.size.y > .2f,
                    removed = new Bounds((lo + hi) * .5f, hi - lo) };
                void Retain(Vector3 min, Vector3 max)
                {
                    var size = max - min;
                    if (size.x > .0005f && size.y > .0005f && size.z > .0005f)
                        cut.retained.Add(new Bounds((min + max) * .5f, size));
                }
                // A full-footprint core reaches exactly to the new floor's underside.
                // Up to four upper XZ strips retain the original volume outside it.
                Retain(bounds.min, new Vector3(bounds.max.x, lo.y, bounds.max.z));
                Retain(new Vector3(bounds.min.x, lo.y, bounds.min.z), new Vector3(lo.x, bounds.max.y, bounds.max.z));
                Retain(new Vector3(hi.x, lo.y, bounds.min.z), bounds.max);
                Retain(new Vector3(lo.x, lo.y, bounds.min.z), new Vector3(hi.x, bounds.max.y, lo.z));
                Retain(new Vector3(lo.x, lo.y, hi.z), new Vector3(hi.x, bounds.max.y, bounds.max.z));
                float Volume(Vector3 v) => v.x * v.y * v.z;
                float retained = cut.retained.Sum(b => Volume(b.size));
                if (Mathf.Abs(retained + Volume(cut.removed.size) - Volume(bounds.size)) > Mathf.Max(.0002f, Volume(bounds.size) * .00001f))
                    throw new InvalidOperationException("Fork trim would remove volume outside the existing floor.");
                if (cut.deepSupport)
                {
                    if (!cut.retained.Any(b => Mathf.Abs(b.max.y - opening.min.y) < .0001f
                        && Mathf.Abs(b.min.y - bounds.min.y) < .0001f && b.min.x <= lo.x + .0001f && b.max.x >= hi.x - .0001f
                        && b.min.z <= lo.z + .0001f && b.max.z >= hi.z - .0001f))
                        throw new InvalidOperationException("Fork trim would lose the deep support-to-floor seam.");
                    plan.report.preservedDeepSupports++;
                }
                else plan.report.trimmedThinCaps++;
                plan.sources.Add(box); plan.report.cuts.Add(cut);
            }
            plan.report.remainingOverlaps = plan.sources.Count;
            return plan;
        }

        static Bounds ForkFrameBox(BoxCollider box, Transform frame)
        {
            var t = box.transform;
            if (Vector3.Dot(t.right, frame.right) < .99999f || Vector3.Dot(t.up, frame.up) < .99999f
                || Vector3.Dot(t.forward, frame.forward) < .99999f)
                throw new InvalidOperationException("Rotated fork cap requires review: " + box.name);
            return new Bounds(frame.InverseTransformPoint(t.TransformPoint(box.center)), Vector3.Scale(box.size, t.lossyScale));
        }

        static void ValidateForkCap(BoxCollider box)
        {
            var go = box.gameObject;
            var filter = go.GetComponent<MeshFilter>();
            var mesh = filter ? filter.sharedMesh : null;
            if (go.transform.childCount != 0 || box.center.sqrMagnitude > .000001f || Vector3.Distance(box.size, Vector3.one) > .0001f
                || !go.GetComponent<MeshRenderer>() || !mesh || mesh.name != "Cube" || mesh.vertexCount != 24
                || mesh.vertices.Any(v => Mathf.Abs(Mathf.Abs(v.x) - .5f) > .0001f || Mathf.Abs(Mathf.Abs(v.y) - .5f) > .0001f || Mathf.Abs(Mathf.Abs(v.z) - .5f) > .0001f)
                || go.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer || c is BoxCollider)))
                throw new InvalidOperationException("Fork cap is no longer a plain primitive: " + go.name);
        }
    }
}
