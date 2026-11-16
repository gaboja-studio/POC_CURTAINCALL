using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

public static class ThermalWestAccessProbe
{
    static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    // Analytic capsule/old guard test is historical comparison only. Current scene
    // movement below is measured with real Physics casts and support raycasts.
    static bool HistoricalGuardOverlap(Vector3 floor, float height, float radius)
    {
        var min = new Vector3(119.5f, -8, 49);
        var max = new Vector3(120, -6.5f, 51);
        float low = floor.y + .08f + radius, high = floor.y + .08f + height - radius;
        float dx = Mathf.Max(min.x - floor.x, 0, floor.x - max.x);
        float dz = Mathf.Max(min.z - floor.z, 0, floor.z - max.z);
        float dy = Mathf.Max(min.y - high, 0, low - max.y);
        return dx * dx + dy * dy + dz * dz < radius * radius;
    }

    public static object Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototype.unity")
            throw new InvalidOperationException("Stopped Prototype required; this probe never saves or edits it.");
        var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Single(t => t.name == "EscapeCampaignFrame");
        bool dirtyBefore = scene.isDirty;
        var paths = new[]
        {
            new { name = "public lower landing to old A05 west pocket", points = new[]
                { new Vector3(122.5f, -8, 50), new(122, -8, 48.9f), new(117, -8, 48.9f) } },
            new { name = "old A05 west pocket to private gallery interior", points = new[]
                { new Vector3(117, -8, 48.9f), new(108, -8, 51) } }
        };
        var outcomes = new List<object>(); var summaries = new List<object>();
        Physics.SyncTransforms();
        float radius = GameSettings.Base.Character.CapsuleRadius;
        foreach (var path in paths)
        foreach (float height in new[] { GameSettings.Base.Character.CapsuleHeight,
            GameSettings.Base.Character.CapsuleHeight * GameSettings.Base.Character.CrouchHeightRatio })
        {
            var samples = new List<object>(); var segments = new List<object>();
            bool supported = true, clear = true, oldGuardBlocks = false;
            for (int edge = 1; edge < path.points.Length; edge++)
            {
                Vector3 a = path.points[edge - 1], b = path.points[edge];
                Vector3 world = frame.TransformPoint(a) + Vector3.up * .08f;
                Vector3 delta = frame.TransformPoint(b) - frame.TransformPoint(a);
                var castHits = Physics.CapsuleCastAll(world + Vector3.up * radius,
                    world + Vector3.up * (height - radius), radius, delta.normalized,
                    delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.gameObject.scene == scene).OrderBy(h => h.distance).ToArray();
                if (castHits.Length != 0) clear = false;
                segments.Add(new { edge, from = V(a), to = V(b),
                    hits = castHits.Select(h => new { path = Path(h.collider.transform), distance = h.distance,
                        at = V(frame.InverseTransformPoint(h.point)), normal = V(h.normal) }).ToArray() });
                int count = Mathf.CeilToInt(delta.magnitude / .1f);
                for (int i = 0; i <= count; i++)
                {
                    var point = Vector3.Lerp(a, b, (float)i / count);
                    var feet = frame.TransformPoint(point);
                    var floor = Physics.RaycastAll(feet + Vector3.up * .2f, Vector3.down, .4f,
                        ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.gameObject.scene == scene
                        && h.normal.y > .7f && Mathf.Abs(h.point.y - feet.y) < .12f).ToArray();
                    var body = Physics.OverlapCapsule(feet + Vector3.up * (.08f + radius),
                        feet + Vector3.up * (.08f + height - radius), radius, ~0, QueryTriggerInteraction.Ignore)
                        .Where(c => c.gameObject.scene == scene).ToArray();
                    bool historical = HistoricalGuardOverlap(point, height, radius);
                    supported &= floor.Length > 0; clear &= body.Length == 0; oldGuardBlocks |= historical;
                    samples.Add(new { point = V(point), support = floor.Select(h => Path(h.collider.transform)).Distinct().ToArray(),
                        bodyHits = body.Select(c => Path(c.transform)).Distinct().ToArray(), oldGuardWouldBlock = historical });
                }
            }
            outcomes.Add(new { path = path.name, posture = height == GameSettings.Base.Character.CapsuleHeight ? "standing" : "crouched",
                height, radius, currentScenePassable = supported && clear, supported, clear,
                originalDeletedGuardWouldBlockSampledPath = oldGuardBlocks, segments, samples });
            summaries.Add(new { path = path.name, posture = height == GameSettings.Base.Character.CapsuleHeight ? "standing" : "crouched",
                currentScenePassable = supported && clear, supported, clear, originalDeletedGuardWouldBlockSampledPath = oldGuardBlocks,
                sampledPositions = samples.Count });
        }
        if (scene.isDirty != dirtyBefore) throw new InvalidOperationException("Read-only probe changed scene dirty state.");
        var report = new { scene = scene.path, dirtyBefore, dirtyAfter = scene.isDirty,
            scope = "Current-scene Physics casts/support samples; historical original guard uses analytic capsule/AABB comparison only. No setup, gameplay execution or scene mutation.",
            originalGuard = new { name = "Stair exit west jump guard", min = new[] {119.5f,-8f,49f}, max = new[] {120f,-6.5f,51f} },
            outcomes };
        File.WriteAllText(".harness/local/pursuer-polish-20261116/thermal-west-access.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return summaries;
    }
}
