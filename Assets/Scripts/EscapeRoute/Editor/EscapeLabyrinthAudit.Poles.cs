using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeLabyrinthAudit
    {
        static void AuditPoleBodyClearance(Transform additions, Transform frame, Report report)
        {
            var poles = additions.GetComponentsInChildren<HumanCannonPole>(true)
                .Where(p => p.isActiveAndEnabled).ToArray();
            if (poles.Length == 0) return;
            var systems = frame.gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<HumanCannonPoleSystem>(true)).ToArray();
            var registrations = new Dictionary<HumanCannonPole, int>();
            foreach (var system in systems)
            {
                var entries = new SerializedObject(system).FindProperty("poles");
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).objectReferenceValue is HumanCannonPole pole)
                        registrations[pole] = registrations.GetValueOrDefault(pole) + 1;
            }
            if (systems.Length != 1) report.failures.Add("Pole audit requires one shared system; found " + systems.Length + ".");
            int poses = 0;
            var character = GameSettings.Base.Character;
            float maxAngle = GameSettings.Poles.MaxSwingAngle;
            int steps = Mathf.CeilToInt(maxAngle * 2 / 5f);
            foreach (var pole in poles)
            {
                if (registrations.GetValueOrDefault(pole) != 1)
                    report.failures.Add("New pole must be registered exactly once: " + Path(pole.transform));
                if (pole.LinkedGate || pole.Loop)
                    report.failures.Add("New regular static pole unexpectedly references a gate or moving loop: " + Path(pole.transform));
                foreach (int seat in Enumerable.Range(0, Mathf.Max(2, pole.RiderCapacity)))
                    foreach (float height in new[] { character.CapsuleHeight, character.CapsuleHeight * character.CrouchHeightRatio })
                    {
                        string posture = Mathf.Approximately(height, character.CapsuleHeight) ? "standing" : "crouched";
                        var reported = new HashSet<Collider>();
                        Vector3 previous = pole.HangingPosition(-maxAngle, seat);
                        for (int i = 0; i <= steps; i++)
                        {
                            float angle = Mathf.Lerp(-maxAngle, maxAngle, (float)i / steps);
                            Vector3 feet = pole.HangingPosition(angle, seat);
                            poses++;
                            foreach (var collider in LPoleBodyObstacles(previous, feet, height, character.CapsuleRadius, frame, pole.transform))
                                if (reported.Add(collider))
                                    report.failures.Add("Pole body sweep blocked: " + Path(pole.transform) + " | seat=" + seat
                                        + " | " + posture + " | angle=" + Form(angle) + " | feet=" + FramePoint(frame, feet)
                                        + " | collider=" + Path(collider.transform));
                            previous = feet;
                        }
                    }
            }
            report.warnings.Add("Pole clearance coverage: " + poles.Length + " new poles, " + poses
                + " shared standing/crouched seat poses across the full swing, at most 5 degrees apart plus capsule sweeps between poses. "
                + "Own pole hierarchy and triggers are excluded; external supports and other poles are included. "
                + "This is static body clearance and registration evidence, not airborne re-grab feasibility, rider timing, network validation or completed normal-input play.");
        }

        static IEnumerable<Collider> LPoleBodyObstacles(Vector3 from, Vector3 to, float height, float radius,
            Transform frame, Transform pole)
        {
            radius = Mathf.Min(radius, height * .5f);
            Vector3 lower = Vector3.up * (radius + .01f), upper = Vector3.up * (height - radius + .01f);
            bool Obstacle(Collider c) => c && c.gameObject.scene == frame.gameObject.scene
                && !c.transform.IsChildOf(pole) && !c.GetComponentInParent<NetworkPlayer>();
            foreach (var collider in Physics.OverlapCapsule(to + lower, to + upper, radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (Obstacle(collider)) yield return collider;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) yield break;
            foreach (var hit in Physics.CapsuleCastAll(from + lower, from + upper, radius, delta.normalized,
                delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (Obstacle(hit.collider)) yield return hit.collider;
        }
    }
}
