using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeLabyrinthAudit
    {
        static void AuditStoryShell(Transform additions, Report report)
        {
            var displays = additions.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Human conversion evidence" && t.gameObject.activeInHierarchy).ToArray();
            if (displays.Length != 5)
                report.failures.Add("Story shell audit requires five active machinery displays; found " + displays.Length + ".");
            var character = GameSettings.Base.Character;
            float standing = character.CapsuleHeight;
            float crouched = standing * character.CrouchHeightRatio;
            var levels = new List<float>();
            // PiggybackFollow.HeadOf uses the supporting body's height. These are upright
            // stack and hop representatives, not a simulation of every coordinated launch.
            for (int tier = 0; tier < Mathf.Max(1, GameSettings.Base.Session.MaxPlayers); tier++)
            {
                float feet = tier * standing;
                levels.Add(feet);
                levels.Add(feet + character.JumpHeight * (tier == 0 ? 1 : GameSettings.Tightrope.Piggyback.JumpHeightMultiplier));
            }
            int probes = 0, renderedBarriers = 0;
            foreach (var display in displays)
            {
                var chamber = display.parent;
                if (!chamber || chamber == additions)
                { report.failures.Add("Story display lacks an individual chamber parent: " + Path(display)); continue; }
                var shell = new HashSet<Collider>(chamber.GetComponentsInChildren<Collider>(true)
                    .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && !c.transform.IsChildOf(display)));
                if (shell.Count == 0)
                { report.failures.Add("Story chamber has no solid access barriers: " + Path(chamber)); continue; }
                float shellTop = 0;
                foreach (var collider in shell)
                {
                    if (StoryBarrierVisible(collider)) renderedBarriers++;
                    else report.failures.Add("Story shell collider has no matching visible surface: " + Path(collider.transform));
                    if (collider is not BoxCollider box)
                    {
                        report.failures.Add("Story shell envelope cannot be checked as an authored box: " + Path(collider.transform));
                        continue;
                    }
                    var bounds = StoryLocalBoxBounds(chamber, box);
                    shellTop = Mathf.Max(shellTop, bounds.max.y);
                    // Positive overlap only: the supporting shelf may touch y=0.
                    if (bounds.max.x > -6.99f && bounds.min.x < 6.99f
                        && bounds.max.y > .01f && bounds.min.y < 5.99f
                        && bounds.max.z > .81f && bounds.min.z < 4.79f)
                        report.failures.Add("Story shell box bounds intrude into the evidence envelope: " + Path(collider.transform)
                            + "; chamber min=" + Values(bounds.min) + " max=" + Values(bounds.max));
                }
                foreach (float height in new[] { standing, crouched })
                {
                    float radius = Mathf.Min(character.CapsuleRadius, height * .5f);
                    string posture = Mathf.Approximately(height, standing) ? "standing" : "crouched";
                    float spacing = Mathf.Min(.25f, radius);
                    foreach (float feet in levels.Distinct())
                    {
                        CheckStoryFace(chamber, shell, "front", posture, feet, height, radius,
                            -7.45f, 7.45f, spacing, true, -.8f, 1.2f, report, ref probes);
                        CheckStoryFace(chamber, shell, "rear", posture, feet, height, radius,
                            -7.45f, 7.45f, spacing, true, 6.7f, 4.6f, report, ref probes);
                        CheckStoryFace(chamber, shell, "left end", posture, feet, height, radius,
                            .05f, 5.55f, spacing, false, -8.3f, -6.8f, report, ref probes);
                        CheckStoryFace(chamber, shell, "right end", posture, feet, height, radius,
                            .05f, 5.55f, spacing, false, 8.3f, 6.8f, report, ref probes);
                    }
                }
                // Independent descent probes prevent a high grille from being called a sealed
                // chamber merely because ordinary ground/stack entry samples were blocked.
                foreach (float x in new[] { -7.1f, 0, 7.1f })
                    foreach (float z in new[] { .8f, 2.8f, 5.1f })
                    {
                        var from = chamber.TransformPoint(new Vector3(x, shellTop + standing + .5f, z));
                        var to = chamber.TransformPoint(new Vector3(x, 6.1f, z));
                        probes++;
                        if (!StoryEntryBlocked(shell, from, to, standing, character.CapsuleRadius, out bool initialOverlap))
                            report.failures.Add("Story roof leaves a sampled capsule descent open: " + Path(chamber)
                                + "; chamber x/z=" + Form(x) + "," + Form(z));
                        else if (initialOverlap)
                            report.failures.Add("Story roof descent probe starts inside its shell: " + Path(chamber));
                    }
            }
            report.warnings.Add("Story shell audit coverage: " + displays.Length + " displays, " + probes
                + " standing/crouched/stack entry and roof descent probes, " + renderedBarriers
                + " visible collider envelopes checked. These are sampled local access/presentation checks, not proof of every reachable launch or of in-game visual legibility.");
        }

        static void CheckStoryFace(Transform chamber, HashSet<Collider> shell, string face, string posture,
            float feetHeight, float height, float radius, float start, float end, float spacing,
            bool acrossZ, float outside, float inside, Report report, ref int probes)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((end - start) / spacing));
            for (int i = 0; i <= steps; i++)
            {
                float along = Mathf.Lerp(start, end, i / (float)steps);
                Vector3 Local(float cross) => acrossZ ? new Vector3(along, 0, cross) : new Vector3(cross, 0, along);
                var from = chamber.TransformPoint(Local(outside)) + Vector3.up * feetHeight;
                var to = chamber.TransformPoint(Local(inside)) + Vector3.up * feetHeight;
                probes++;
                bool blocked = StoryEntryBlocked(shell, from, to, height, radius, out bool initialOverlap);
                if (blocked && !initialOverlap) continue;
                report.failures.Add("Story shell " + face + " " + posture + " entry "
                    + (initialOverlap ? "cannot be verified because the exterior probe overlaps its shell" : "is open to the shared player capsule")
                    + ": " + Path(chamber) + "; feetHeight=" + feetHeight.ToString("F3", CultureInfo.InvariantCulture)
                    + "; chamber crossing=" + Values(Local(outside)) + " -> " + Values(Local(inside)));
                break; // One precise failing crossing per face/height/posture is sufficient.
            }
        }

        static bool StoryEntryBlocked(HashSet<Collider> shell, Vector3 from, Vector3 to,
            float height, float radius, out bool initialOverlap)
        {
            var lower = Vector3.up * (radius + .01f);
            var upper = Vector3.up * (Mathf.Max(radius, height - radius) + .01f);
            initialOverlap = Physics.OverlapCapsule(from + lower, from + upper, radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(shell.Contains);
            if (initialOverlap) return true;
            // Only the chamber's own shell proves enclosure. An unrelated old wall must not
            // hide an omitted guard panel, and noncolliding evidence cannot serve as a barrier.
            if (Physics.OverlapCapsule(to + lower, to + upper, radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(shell.Contains)) return true;
            Vector3 delta = to - from;
            return delta.sqrMagnitude > .000001f && Physics.CapsuleCastAll(from + lower, from + upper, radius,
                delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Any(hit => shell.Contains(hit.collider));
        }

        static bool StoryBarrierVisible(Collider collider)
        {
            var renderer = collider.GetComponent<Renderer>();
            var mesh = collider.GetComponent<MeshFilter>();
            if (!renderer || !renderer.enabled || renderer.forceRenderingOff
                || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || !mesh || !mesh.sharedMesh) return false;
            bool VisibleMaterial(Material material)
            {
                if (!material || !material.shader) return false;
                if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor").a > .05f;
                if (material.HasProperty("_Color")) return material.GetColor("_Color").a > .05f;
                return true;
            }
            if (!renderer.sharedMaterials.Any(VisibleMaterial)) return false;
            var visible = renderer.bounds;
            visible.Expand(.04f);
            return visible.Contains(collider.bounds.min) && visible.Contains(collider.bounds.max);
        }

        static Bounds StoryLocalBoxBounds(Transform chamber, BoxCollider box)
        {
            var first = chamber.InverseTransformPoint(box.transform.TransformPoint(box.center - box.size * .5f));
            var bounds = new Bounds(first, Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = box.center + Vector3.Scale(box.size * .5f,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                bounds.Encapsulate(chamber.InverseTransformPoint(box.transform.TransformPoint(corner)));
            }
            return bounds;
        }
    }
}
