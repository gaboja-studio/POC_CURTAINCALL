using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

public static class AscentCannonObstructionProbe
{
    static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    static string Ref(UnityEngine.Object o) => o is Component c ? Path(c.transform) + " [" + c.GetType().Name + "]"
        : o is GameObject g ? Path(g.transform) : o ? o.name + " [" + o.GetType().Name + "]" : null;
    static object[] References(MonoBehaviour m)
    {
        var refs = new List<object>();
        var serialized = new SerializedObject(m);
        var field = serialized.GetIterator();
        while (field.NextVisible(true))
            if (field.propertyType == SerializedPropertyType.ObjectReference && field.objectReferenceValue)
                refs.Add(new { field = field.propertyPath, target = Ref(field.objectReferenceValue) });
        return refs.ToArray();
    }
    public static object Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototype.unity")
            throw new InvalidOperationException("Stopped Prototype required. Read-only collider investigation; no lock or collider changes.");
        var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var frame = transforms.Single(t => t.name == "EscapeCampaignFrame");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).ToArray();
        var behaviors = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m).ToArray();
        bool dirtyBefore = scene.isDirty;
        Physics.SyncTransforms();
        var links = new Dictionary<Transform, List<string>>();
        var controls = new List<object>();
        foreach (var m in behaviors.Where(m => m.GetType().Name == "EscapeCooperativeLocks" || m.GetType().Name == "HumanCannonSawGate"))
        {
            var refs = References(m);
            controls.Add(new { owner = Path(m.transform), type = m.GetType().FullName, enabled = m.enabled,
                active = m.gameObject.activeInHierarchy, references = refs });
            var so = new SerializedObject(m); var p = so.GetIterator();
            while (p.NextVisible(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue)
                {
                    Transform t = p.objectReferenceValue is GameObject go ? go.transform : p.objectReferenceValue is Component co ? co.transform : null;
                    if (!t) continue;
                    if (!links.TryGetValue(t, out var row)) links[t] = row = new List<string>();
                    row.Add(Path(m.transform) + " :: " + p.propertyPath);
                }
        }
        string[] LinkedControls(Transform t)
        {
            var rows = new List<string>();
            for (var p = t; p; p = p.parent) if (links.TryGetValue(p, out var r)) rows.AddRange(r);
            return rows.Distinct().ToArray();
        }
        object ColliderRow(Collider c) => new
        {
            path = Path(c.transform), type = c.GetType().Name, instanceId = c.GetInstanceID(),
            enabled = c.enabled, active = c.gameObject.activeInHierarchy, trigger = c.isTrigger,
            layer = c.gameObject.layer, layerName = LayerMask.LayerToName(c.gameObject.layer),
            worldMin = V(c.bounds.min), worldMax = V(c.bounds.max),
            localCenter = V(frame.InverseTransformPoint(c.bounds.center)),
            rootBody = c.attachedRigidbody ? Path(c.attachedRigidbody.transform) : null,
            material = c.sharedMaterial ? c.sharedMaterial.name : null,
            owners = c.GetComponentsInParent<MonoBehaviour>(true).Where(m => m).Select(m => new
                { path = Path(m.transform), type = m.GetType().FullName, enabled = m.enabled }).ToArray(),
            editorControlReferences = LinkedControls(c.transform)
        };
        object HitRow(RaycastHit h) => new { collider = ColliderRow(h.collider), distance = h.distance,
            pointWorld = V(h.point), pointLocal = V(frame.InverseTransformPoint(h.point)), normal = V(h.normal) };
        var stalled = new Vector3(-324.58f, 17.08f, 15.60f);
        var segments = new[]
        {
            new { name = "actual previous waypoint to target", a = new Vector3(-325,17.08f,13.75f), b = new Vector3(-324,17.08f,16.4f) },
            new { name = "stalled to target", a = stalled, b = new Vector3(-324,17.08f,16.4f) },
            new { name = "alternate across south of cannon", a = new Vector3(-325,17.08f,13.75f), b = new Vector3(-321.25f,17.08f,13.75f) },
            new { name = "alternate south to seat approach", a = new Vector3(-321.25f,17.08f,13.75f), b = new Vector3(-321.25f,17.08f,16.5f) },
            new { name = "alternate north edge from pole side", a = new Vector3(-325,17.08f,13.75f), b = new Vector3(-324,17.08f,17.5f) },
            new { name = "alternate north edge to seat", a = new Vector3(-324,17.08f,17.5f), b = new Vector3(-321.25f,17.08f,17.5f) }
        };
        float radius = GameSettings.Base.Character.CapsuleRadius, height = GameSettings.Base.Character.CapsuleHeight;
        var paths = new List<object>();
        foreach (var segment in segments)
        {
            var delta = segment.b - segment.a;
            var casts = Physics.CapsuleCastAll(segment.a + Vector3.up * radius,
                segment.a + Vector3.up * (height - radius), radius, delta.normalized, delta.magnitude,
                ~0, QueryTriggerInteraction.Collide).Where(h => h.collider.gameObject.scene == scene).OrderBy(h => h.distance).ToArray();
            var samples = new List<object>();
            int count = Mathf.CeilToInt(delta.magnitude / .1f);
            for (int i = 0; i <= count; i++)
            {
                var feet = Vector3.Lerp(segment.a, segment.b, (float)i / count);
                var body = Physics.OverlapCapsule(feet + Vector3.up * radius, feet + Vector3.up * (height - radius),
                    radius, ~0, QueryTriggerInteraction.Collide).Where(c => c.gameObject.scene == scene).ToArray();
                var floor = Physics.RaycastAll(feet + Vector3.up * .12f, Vector3.down, .5f, ~0,
                    QueryTriggerInteraction.Ignore).Where(h => h.collider.gameObject.scene == scene).OrderBy(h => h.distance).ToArray();
                samples.Add(new { feetWorld = V(feet), feetLocal = V(frame.InverseTransformPoint(feet)),
                    overlaps = body.Select(ColliderRow).ToArray(), support = floor.Select(HitRow).ToArray() });
            }
            paths.Add(new { segment.name, start = V(segment.a), end = V(segment.b),
                solidCastHits = casts.Count(h => !h.collider.isTrigger), casts = casts.Select(HitRow).ToArray(), samples });
        }
        var region = new Bounds(new Vector3(-323.5f,18,15.5f), new Vector3(12,8,12));
        var nearby = all.Where(c => c.enabled && c.gameObject.activeInHierarchy && region.Intersects(c.bounds)).ToArray();
        if (scene.isDirty != dirtyBefore) throw new InvalidOperationException("Read-only probe changed scene dirty state.");
        var report = new { scene = scene.path, dirtyBefore, dirtyAfter = scene.isDirty,
            scope = "Stopped-editor Physics. Includes triggers and controller ownership. Editor lock shutters may be closed; runtime continuous01 showed lock1 open/latched. No controller mutation and no inference that every editor hit existed during runtime.",
            radius, height, stalledWorld = V(stalled), stalledLocal = V(frame.InverseTransformPoint(stalled)),
            paths, nearby = nearby.Select(ColliderRow).ToArray(), controls };
        File.WriteAllText(".harness/local/pursuer-polish-20261116/ascent-cannon-obstruction.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return new { paths = paths.Count, nearbyColliders = nearby.Length,
            report = ".harness/local/pursuer-polish-20261116/ascent-cannon-obstruction.json", dirtyUnchanged = scene.isDirty == dirtyBefore };
    }
}
