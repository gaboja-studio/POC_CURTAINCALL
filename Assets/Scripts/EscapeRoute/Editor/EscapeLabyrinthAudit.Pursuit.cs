using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeLabyrinthAudit
    {
        static readonly string[] EncounterAnchors = { "ingress", "staging", "approach", "exit", "landing" };
        const float PursuerRadius = 1.4f; // EscapePursuerNavigation.Clear's authored body envelope.
        const float ContactTolerance = .01f;

        static void AuditPursuit(Transform additions, Transform frame, Report report)
        {
            var actor = frame.GetComponentInChildren<EscapeUnderstageChase>(true);
            var walls = frame.GetComponentInChildren<EscapePursuerWalls>(true);
            var route = frame.GetComponentInChildren<EscapePursuerRoute>(true);
            if (!actor || !walls || !route)
                report.failures.Add("Pursuit audit requires the scene's chase actor, walls, and route.");
            else
            {
                AuditEncounterClearance(actor, walls, frame, report);
                AuditAddedServiceObstacles(actor, route, additions, frame, report);
            }
            if (route) AuditFinaleWorkshopArena(additions, frame, route, report);
            AuditCannonClearance(additions, frame, report);
            AuditRopeBodyClearance(additions, frame, report);
        }

        static void AuditFinaleWorkshopArena(Transform additions, Transform frame, EscapePursuerRoute route, Report report)
        {
            // Use the boss attached to this scene's route, never the runtime Current singleton.
            var boss = route.GetComponentInChildren<EscapePursuerBoss>(true);
            if (!boss)
            { report.failures.Add("Finale workshop audit requires the boss on " + Path(route.transform)); return; }
            var names = new[] { "Labyrinth finale South cable workshop aisle", "Labyrinth finale North cable workshop aisle" };
            var floors = additions.GetComponentsInChildren<BoxCollider>(true);
            var samples = new[] { Vector2.zero, new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) };
            foreach (string name in names)
            {
                // The production approach floors before X366 are deliberately outside this contract.
                var matches = floors.Where(floor => floor.name == name && ActiveSolid(floor)).ToArray();
                if (matches.Length != 1)
                {
                    report.failures.Add("Finale workshop audit requires one active solid floor named " + name + "; found " + matches.Length + ".");
                    continue;
                }
                var floor = matches[0];
                float insetX = .15f / floor.transform.TransformVector(Vector3.right).magnitude;
                float insetZ = .15f / floor.transform.TransformVector(Vector3.forward).magnitude;
                float halfX = Mathf.Max(0, floor.size.x * .5f - insetX);
                float halfZ = Mathf.Max(0, floor.size.z * .5f - insetZ);
                for (int sample = 0; sample < samples.Length; sample++)
                {
                    var feet = floor.transform.TransformPoint(floor.center
                        + new Vector3(samples[sample].x * halfX, floor.size.y * .5f, samples[sample].y * halfZ));
                    // InsideArena itself tests feet + world up; do not add the height twice.
                    if (boss.InsideArena(feet)) continue;
                    report.failures.Add("Finale workshop floor is outside boss target arena: " + Path(floor.transform)
                        + "; " + (sample == 0 ? "center" : "near-corner " + sample)
                        + "; feet frame " + FramePoint(frame, feet)
                        + "; tested frame " + FramePoint(frame, feet + Vector3.up)
                        + "; boss " + Path(boss.transform));
                }
            }
        }

        static void AuditEncounterClearance(EscapeUnderstageChase actor, EscapePursuerWalls walls, Transform frame, Report report)
        {
            var encounters = new SerializedObject(actor).FindProperty("encounters");
            if (encounters == null)
            { report.failures.Add("Cannot inspect pursuit encounters on " + Path(actor.transform)); return; }
            var entrance = walls.At(0)?.barrier;
            bool entranceEnabled = entrance && entrance.enabled;
            try
            {
                if (entrance) entrance.enabled = false;
                Physics.SyncTransforms();
                for (int index = 0; index < encounters.arraySize; index++)
                {
                    var row = encounters.GetArrayElementAtIndex(index);
                    string label = "Pursuer encounter " + index + " (" + row.FindPropertyRelative("label").stringValue + ")";
                    var anchors = EncounterAnchors.Select(name => row.FindPropertyRelative(name)?.objectReferenceValue as Transform).ToArray();
                    bool invalid = false;
                    for (int i = 0; i < anchors.Length; i++)
                        if (!anchors[i] || anchors[i].gameObject.scene != frame.gameObject.scene)
                        { report.failures.Add(label + " has missing or foreign-scene " + EncounterAnchors[i] + " anchor."); invalid = true; }
                    int gateIndex = row.FindPropertyRelative("gate").intValue;
                    var barrier = walls.At(gateIndex)?.barrier;
                    if (!barrier || barrier.gameObject.scene != frame.gameObject.scene)
                    { report.failures.Add(label + " has missing or foreign-scene barrier for gate " + gateIndex + "."); invalid = true; }
                    if (invalid) continue;
                    bool enabled = barrier.enabled;
                    try
                    {
                        barrier.enabled = false;
                        Physics.SyncTransforms();
                        for (int leg = 1; leg < 4; leg++)
                        {
                            Vector3 from = anchors[leg - 1].position, to = anchors[leg].position;
                            string segment = label + " " + EncounterAnchors[leg - 1] + " -> " + EncounterAnchors[leg];
                            if (!EscapePursuerNavigation.Clear(actor.transform, from, to))
                            {
                                string hits = string.Join(" | ", PursuerObstacles(actor.transform, from, to)
                                    .Select(c => Path(c.transform)).Distinct());
                                report.failures.Add(segment + " is blocked" + (hits.Length > 0 ? ": " + hits : "."));
                            }
                            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .25f));
                            for (int sample = 0; sample <= steps; sample++)
                            {
                                var feet = Vector3.Lerp(from, to, sample / (float)steps);
                                if (EscapePursuerNavigation.Supported(feet)) continue;
                                report.failures.Add(segment + " lacks shared body support at frame " + FramePoint(frame, feet) + ".");
                                break;
                            }
                        }
                        CheckEncounterJump(actor.transform, anchors[3].position, anchors[4].position,
                            label + " exit -> landing", frame, report);
                        CheckEncounterJump(actor.transform, anchors[4].position, anchors[3].position,
                            label + " landing -> exit", frame, report);
                    }
                    finally
                    {
                        if (barrier) barrier.enabled = enabled;
                        Physics.SyncTransforms();
                    }
                }
            }
            finally
            {
                if (entrance) entrance.enabled = entranceEnabled;
                Physics.SyncTransforms();
            }
        }

        static void CheckEncounterJump(Transform actor, Vector3 from, Vector3 to, string label, Transform frame, Report report)
        {
            if (EscapePursuerNavigation.ClearJump(actor, from, to)) return;
            bool takeoff = EscapePursuerNavigation.Supported(from), landing = EscapePursuerNavigation.Supported(to);
            string hits = string.Join(" | ", PursuerJumpObstacles(actor, from, to).Select(c => Path(c.transform)).Distinct());
            report.failures.Add(label + " fails shared ClearJump; takeoffSupport=" + takeoff + ", landingSupport=" + landing
                + "; frame " + FramePoint(frame, from) + " -> " + FramePoint(frame, to)
                + (hits.Length > 0 ? "; blockers: " + hits : "."));
        }

        static void AuditAddedServiceObstacles(EscapeUnderstageChase actor, EscapePursuerRoute route,
            Transform additions, Transform frame, Report report)
        {
            for (int index = 1; index < route.Points.Count; index++)
            {
                var previous = route.Points[index - 1];
                var next = route.Points[index];
                if (previous?.feet == null || next?.feet == null)
                { report.failures.Add("Service edge " + (index - 1) + " -> " + index + " has a missing feet anchor."); continue; }
                var obstacles = next.jump
                    ? PursuerJumpObstacles(actor.transform, previous.feet.position, next.feet.position)
                    : PursuerObstacles(actor.transform, previous.feet.position, next.feet.position);
                // Retained edges can contain closed gates or intentionally bypassed legacy geometry.
                // Report only new scenery entering their existing walking/jumping envelope.
                foreach (var collider in obstacles.Where(c => c.transform.IsChildOf(additions)).Distinct())
                    report.failures.Add("New scenery blocks authored service " + (next.jump ? "jump " : "edge ")
                        + (index - 1) + " -> " + index + ": " + Path(collider.transform)
                        + "; frame " + FramePoint(frame, previous.feet.position) + " -> " + FramePoint(frame, next.feet.position));
            }
        }

        static IEnumerable<Collider> PursuerJumpObstacles(Transform actor, Vector3 from, Vector3 to)
        {
            float arc = EscapeUnderstageChase.ServiceJumpArc(from, to);
            int steps = Mathf.Max(8, Mathf.CeilToInt((Vector3.Distance(from, to) + arc) / .5f));
            Vector3 previous = from;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                var next = Vector3.Lerp(from, to, t) + Vector3.up * (arc * t * (1 - t));
                foreach (var collider in PursuerObstacles(actor, previous, next)) yield return collider;
                previous = next;
            }
        }

        static IEnumerable<Collider> PursuerObstacles(Transform actor, Vector3 from, Vector3 to)
        {
            bool Obstacle(Collider c) => c && !c.transform.IsChildOf(actor)
                && !c.GetComponentInParent<NetworkPlayer>() && !c.GetComponentInParent<EscapePursuerDebris>();
            foreach (var feet in new[] { from, to })
                foreach (var collider in Physics.OverlapCapsule(feet + Vector3.up * 1.5f, feet + Vector3.up * 3.5f,
                    PursuerRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (Obstacle(collider)) yield return collider;
            var delta = to - from;
            if (delta.sqrMagnitude < .0001f) yield break;
            foreach (var hit in Physics.CapsuleCastAll(from + Vector3.up * 1.5f, from + Vector3.up * 3.5f,
                PursuerRadius, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (Obstacle(hit.collider)) yield return hit.collider;
        }

        static void AuditCannonClearance(Transform additions, Transform frame, Report report)
        {
            foreach (var station in additions.GetComponentsInChildren<HumanCannonStation>(true).Where(s => s.isActiveAndEnabled))
            {
                var landing = station.transform.Find("AuthoredLanding");
                if (!landing)
                { report.failures.Add("New cannon lacks AuthoredLanding: " + Path(station.transform)); continue; }
                Vector3 start = station.Muzzle.position, velocity = station.LaunchVelocity;
                float speed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
                float duration = Vector3.ProjectOnPlane(landing.position - start, Vector3.up).magnitude / speed;
                if (!float.IsFinite(duration) || duration <= 0 || duration > 30)
                { report.failures.Add("New cannon has invalid authored flight duration: " + Path(station.transform)); continue; }
                Vector3 Position(float t) => start + velocity * t - Vector3.up * (.5f * GameSettings.Base.Character.Gravity * t * t);
                var end = Position(duration);
                if (!float.IsFinite(end.sqrMagnitude) || Vector3.Distance(end, landing.position) > .1f)
                {
                    report.failures.Add("New cannon trajectory misses AuthoredLanding: " + Path(station.transform)
                        + "; calculated frame " + FramePoint(frame, end) + "; authored frame " + FramePoint(frame, landing.position));
                    if (!float.IsFinite(end.sqrMagnitude)) continue;
                }
                var seen = new HashSet<Collider>();
                // Sweep through the calculated endpoint. The shared body dimensions and the
                // small floor-contact tolerance avoid leaving an unchecked final approach.
                int steps = Mathf.Max(1, Mathf.CeilToInt(duration / .025f));
                Vector3 previous = start;
                for (int i = 0; i <= steps; i++)
                {
                    float t = duration * i / steps;
                    Vector3 feet = Position(t);
                    foreach (var collider in PlayerBodyObstacles(previous, feet, frame, station.transform))
                        if (seen.Add(collider)) report.failures.Add("New cannon ballistic body obstruction: " + Path(station.transform)
                            + "; t=" + t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                            + "; frame " + FramePoint(frame, feet) + "; " + Path(collider.transform));
                    previous = feet;
                }
                foreach (var collider in PlayerBodyObstacles(end, end, frame, station.transform))
                    if (seen.Add(collider)) report.failures.Add("New cannon endpoint body obstruction: " + Path(station.transform)
                        + "; frame " + FramePoint(frame, end) + "; " + Path(collider.transform));
                bool supported = Physics.RaycastAll(end + Vector3.up * .2f, Vector3.down, .5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    .Any(h => h.collider.gameObject.scene == frame.gameObject.scene && !h.collider.transform.IsChildOf(station.transform)
                        && h.normal.y >= .8f && Mathf.Abs(h.point.y - end.y) <= .1f);
                if (!supported) report.failures.Add("New cannon endpoint lacks floor support: " + Path(station.transform)
                    + "; frame " + FramePoint(frame, end));
            }
        }

        static void AuditRopeBodyClearance(Transform additions, Transform frame, Report report)
        {
            var checkedRopes = new HashSet<BoxCollider>();
            foreach (var course in frame.GetComponentsInChildren<TightropeCourse>(true).Where(c => c.isActiveAndEnabled && c.UsesSceneGeometry))
            {
                var ropes = new SerializedObject(course).FindProperty("sceneRopes");
                if (ropes == null) continue; // AuditRopes already reports an unreadable registry.
                for (int index = 0; index < ropes.arraySize; index++)
                {
                    var rope = ropes.GetArrayElementAtIndex(index).objectReferenceValue as BoxCollider;
                    if (!rope || !ActiveSolid(rope) || !rope.transform.IsChildOf(additions) || !checkedRopes.Add(rope)) continue;
                    Vector3 from = rope.transform.TransformPoint(rope.center + new Vector3(0, rope.size.y * .5f, -rope.size.z * .5f));
                    Vector3 to = rope.transform.TransformPoint(rope.center + new Vector3(0, rope.size.y * .5f, rope.size.z * .5f));
                    var seen = new HashSet<Collider>();
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .25f));
                    var previous = from;
                    for (int sample = 0; sample <= steps; sample++)
                    {
                        var feet = Vector3.Lerp(from, to, sample / (float)steps);
                        foreach (var collider in PlayerBodyObstacles(previous, feet, frame, null))
                            if (collider != rope && seen.Add(collider)) report.failures.Add("New rope body obstruction: " + Path(rope.transform)
                                + "; frame " + FramePoint(frame, feet) + "; " + Path(collider.transform));
                        previous = feet;
                    }
                }
            }
        }

        static IEnumerable<Collider> PlayerBodyObstacles(Vector3 from, Vector3 to, Transform frame, Transform ignored)
        {
            float radius = GameSettings.Base.Character.CapsuleRadius, height = GameSettings.Base.Character.CapsuleHeight;
            // PlayerMover.ApplyBodySize keeps its center at half height above the feet.
            // Lift only the contact tolerance so a flush supporting floor is not an obstruction.
            Vector3 lower = Vector3.up * (radius + ContactTolerance), upper = Vector3.up * (height - radius + ContactTolerance);
            bool Obstacle(Collider c) => c && c.gameObject.scene == frame.gameObject.scene
                && (!ignored || !c.transform.IsChildOf(ignored)) && !c.GetComponentInParent<NetworkPlayer>();
            foreach (var collider in Physics.OverlapCapsule(to + lower, to + upper, radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (Obstacle(collider)) yield return collider;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) yield break;
            foreach (var hit in Physics.CapsuleCastAll(from + lower, from + upper, radius, delta.normalized, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (Obstacle(hit.collider)) yield return hit.collider;
        }

        static string FramePoint(Transform frame, Vector3 world) => Values(frame.InverseTransformPoint(world));
    }
}
