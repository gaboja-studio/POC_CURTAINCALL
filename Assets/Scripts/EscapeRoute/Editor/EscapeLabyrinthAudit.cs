using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Read-only checks for the new copy. Caller synchronizes physics after construction.</summary>
    public static partial class EscapeLabyrinthAudit
    {
        [Serializable] public sealed class Report
        {
            public List<string> failures = new();
            public List<string> warnings = new();
            public List<LandingSample> landingSamples = new();
            public int activeObjects, missingScripts, colliders, solidBoxes, floorCandidates, ropes, stories, renderers, isolatedProps;
        }
        [Serializable] public sealed class LandingSample
        {
            public string floor, hit;
            public int sample;
            public Vector3 pointInFrame, hitInFrame;
            public float upwardNormal;
        }

        public static Report Audit(Transform additions, Transform frame)
        {
            var report = new Report();
            if (!additions || !frame || additions.gameObject.scene != frame.gameObject.scene)
            { report.failures.Add("Additions and frame must belong to the same loaded scene."); return report; }
            foreach (var root in frame.gameObject.scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.gameObject.activeInHierarchy))
                {
                    report.activeObjects++;
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    report.missingScripts += missing;
                    if (missing > 0) report.failures.Add("Missing scripts: " + Path(t));
                }
            var colliders = additions.GetComponentsInChildren<Collider>(true);
            report.colliders = colliders.Length;
            foreach (var collider in colliders)
                if (!Positive(collider)) report.failures.Add("Nonpositive or invalid collider size: " + Path(collider.transform));
            var boxes = colliders.OfType<BoxCollider>().Where(ActiveSolid).ToArray();
            report.solidBoxes = boxes.Length;
            var unique = new Dictionary<string, BoxCollider>();
            foreach (var box in boxes)
            {
                var q = box.transform.rotation;
                if (q.w < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                string key = Values(box.transform.TransformPoint(box.center)) + Values(WorldSize(box))
                    + Form(q.x) + "," + Form(q.y) + "," + Form(q.z) + "," + Form(q.w);
                if (unique.TryGetValue(key, out var other)) report.failures.Add("Duplicate solid boxes: " + Path(other.transform) + " | " + Path(box.transform));
                else unique.Add(key, box);
                if (FloorCandidate(box)) SampleFloor(box, frame, report);
            }
            AuditRopes(frame, boxes, report);
            AuditEntrance(additions, frame, report);
            AuditPoleBodyClearance(additions, frame, report);
            AuditPursuit(additions, frame, report);
            AuditStories(additions, report);
            AuditStoryShell(additions, report);
            AuditPropContacts(additions, report);
            return report;
        }

        static void SampleFloor(BoxCollider box, Transform frame, Report report)
        {
            report.floorCandidates++;
            var offsets = new[] { Vector2.zero, new Vector2(-.8f, -.8f), new Vector2(-.8f, .8f), new Vector2(.8f, -.8f), new Vector2(.8f, .8f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                var top = box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(offsets[i].x, 1, offsets[i].y)));
                var hit = Physics.RaycastAll(top + Vector3.up * .3f, Vector3.down, .7f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.gameObject.scene == frame.gameObject.scene).OrderBy(h => h.distance).FirstOrDefault();
                report.landingSamples.Add(new LandingSample { floor = Path(box.transform), sample = i, pointInFrame = frame.InverseTransformPoint(top),
                    hit = hit.collider ? Path(hit.collider.transform) : "NO_HIT", hitInFrame = hit.collider ? frame.InverseTransformPoint(hit.point) : Vector3.zero,
                    upwardNormal = hit.collider ? hit.normal.y : 0 });
                if (!hit.collider) report.failures.Add("No solid floor at top sample " + i + ": " + Path(box.transform));
                else if (hit.collider != box || hit.normal.y < .9f)
                    report.warnings.Add("Floor top sample " + i + " intercepted by " + Path(hit.collider.transform) + " at " + Path(box.transform));
            }
        }

        static void AuditRopes(Transform frame, BoxCollider[] boxes, Report report)
        {
            var registered = new HashSet<BoxCollider>();
            foreach (var root in frame.gameObject.scene.GetRootGameObjects())
                foreach (var course in root.GetComponentsInChildren<TightropeCourse>(true).Where(c => c.isActiveAndEnabled && c.UsesSceneGeometry))
                {
                    var ropes = new SerializedObject(course).FindProperty("sceneRopes");
                    if (ropes == null) { report.warnings.Add("Cannot inspect registered ropes on " + Path(course.transform)); continue; }
                    for (int i = 0; i < ropes.arraySize; i++)
                        if (ropes.GetArrayElementAtIndex(i).objectReferenceValue is BoxCollider rope && ActiveSolid(rope)) registered.Add(rope);
                }
            report.ropes = registered.Count;
            foreach (var rope in registered)
            {
                Vector3 a = rope.transform.TransformPoint(rope.center + new Vector3(0, rope.size.y * .5f, -rope.size.z * .5f));
                Vector3 b = rope.transform.TransformPoint(rope.center + new Vector3(0, rope.size.y * .5f, rope.size.z * .5f));
                float length = Vector3.ProjectOnPlane(b - a, Vector3.up).magnitude;
                if (length <= 3.02f) continue;
                foreach (var box in boxes.Where(FloorCandidate))
                {
                    if (box == rope) continue;
                    int steps = Mathf.CeilToInt((length - 3.02f) / .4f);
                    for (int i = 0; i <= steps; i++)
                    {
                        var p = Vector3.Lerp(a, b, (1.51f + (length - 3.02f) * i / Mathf.Max(1, steps)) / length);
                        float drop = p.y - box.bounds.max.y;
                        if (drop < -.01f || drop > 25) continue;
                        var local = box.transform.InverseTransformPoint(new Vector3(p.x, box.bounds.center.y, p.z)) - box.center;
                        float dx = Mathf.Max(0, Mathf.Abs(local.x) - box.size.x * .5f) * box.transform.TransformVector(Vector3.right).magnitude;
                        float dz = Mathf.Max(0, Mathf.Abs(local.z) - box.size.z * .5f) * box.transform.TransformVector(Vector3.forward).magnitude;
                        if (dx * dx + dz * dz > .8f * .8f) continue;
                        report.failures.Add("New fall-catching floor below inner registered rope: " + Path(box.transform) + " | " + Path(rope.transform)
                            + " | drop=" + drop.ToString("F2", CultureInfo.InvariantCulture) + "m | frame=" + frame.InverseTransformPoint(p));
                        break;
                    }
                }
            }
        }

        static void AuditEntrance(Transform additions, Transform frame, Report report)
        {
            var breach=frame.GetComponentInChildren<EscapeUnderstageBreach>(true);
            if(!breach)return;
            var origin=frame.InverseTransformPoint(breach.transform.position);
            var reported=new HashSet<Collider>();
            // Validate new scenery against the retained entrance sweep, including the
            // offscreen starting pose. A clear public route alone does not prove emergence.
            for(float offset=-9;offset<=13;offset+=.3f)
            {
                var feet=frame.TransformPoint(origin+Vector3.forward*offset);
                foreach(var c in Physics.OverlapCapsule(feet+Vector3.up*1.5f,feet+Vector3.up*3.5f,1.4f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(c.transform.IsChildOf(additions)&&reported.Add(c))
                        report.failures.Add("New scenery blocks retained pursuer entrance: "+Path(c.transform));
            }
        }

        static void AuditStories(Transform additions, Report report)
        {
            foreach (var story in additions.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Human conversion evidence"))
            {
                report.stories++;
                if (story.GetComponentsInChildren<Collider>(true).Length > 0) report.failures.Add("Story contains a collider: " + Path(story));
                foreach (var renderer in story.GetComponentsInChildren<Renderer>(true))
                {
                    var bounds = renderer.localBounds;
                    bool outside = false;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var p = story.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                        outside |= p.x < -7.03f || p.x > 7.03f || p.y < -.03f || p.y > 6.03f || p.z < .77f || p.z > 4.83f;
                    }
                    if (outside) report.failures.Add("Story renderer outside agreed envelope: " + Path(renderer.transform));
                }
            }
        }

        static void AuditPropContacts(Transform additions, Report report)
        {
            var renderers = additions.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            report.renderers = renderers.Length;
            foreach (var renderer in renderers)
            {
                if (renderer.GetComponent<Collider>()) continue;
                var bounds = renderer.bounds; bounds.Expand(.16f);
                if (renderers.Any(other => other != renderer && bounds.Intersects(other.bounds))) continue;
                // A conservative AABB-neighbour hint, not proof of structural support or an old-scene failure.
                report.isolatedProps++;
                report.warnings.Add("Isolated added visual; inspect its support: " + Path(renderer.transform));
            }
        }

        static bool ActiveSolid(BoxCollider b) => b.enabled && b.gameObject.activeInHierarchy && !b.isTrigger;
        static bool FloorCandidate(BoxCollider b) => WorldSize(b).x > 1 && WorldSize(b).z > 1 && Vector3.Dot(b.transform.up, Vector3.up) > .99f;
        static Vector3 WorldSize(BoxCollider b) => new(b.size.x * b.transform.TransformVector(Vector3.right).magnitude,
            b.size.y * b.transform.TransformVector(Vector3.up).magnitude, b.size.z * b.transform.TransformVector(Vector3.forward).magnitude);
        static bool Positive(Collider c)
        {
            Vector3 scale = c.transform.lossyScale;
            if (!float.IsFinite(scale.sqrMagnitude) || Mathf.Abs(scale.x * scale.y * scale.z) <= 0) return false;
            if (c is BoxCollider b) return ValidSize(b.size);
            if (c is SphereCollider s) return float.IsFinite(s.radius) && s.radius > 0;
            if (c is CapsuleCollider p) return float.IsFinite(p.radius) && float.IsFinite(p.height) && p.radius > 0 && p.height > 0;
            if (c is MeshCollider m) return m.sharedMesh && ValidSize(m.sharedMesh.bounds.size);
            return !c.enabled || !c.gameObject.activeInHierarchy || ValidSize(c.bounds.size);
        }
        static bool ValidSize(Vector3 size) => float.IsFinite(size.sqrMagnitude) && size.x > 0 && size.y > 0 && size.z > 0;
        static string Values(Vector3 v) => Form(v.x) + "," + Form(v.y) + "," + Form(v.z) + ";";
        static string Form(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    }
}
