using System;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateMidRouteEncounters()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("현재 A 편집 씬이 필요합니다.");
            Physics.SyncTransforms();
            var root = GameObject.Find(RootName).transform.Find(MidEncounterGroup);
            if (root == null) throw new InvalidOperationException("중반 기믹이 없습니다.");
            var hazards = root.GetComponentsInChildren<HumanCannonHazard>();
            var course = UnityEngine.Object.FindAnyObjectByType<HumanCannonHazardCourse>();
            var character = GameSettings.Base.Character;
            float radius = character.CapsuleRadius, height = character.CapsuleHeight;
            var report = new StringBuilder(); int passed = 0, failed = 0;
            void Check(string label, bool ok) { if (ok) passed++; else { failed++; report.AppendLine("FAIL " + label); } }
            Check("one host hazard course", UnityEngine.Object.FindObjectsByType<HumanCannonHazardCourse>(FindObjectsSortMode.None).Length == 1);
            Check("8 mid-route hazards", hazards.Length == 8);
            Check("2 saws / 2 presses / 4 vents", hazards.Count(h => h.Kind == CannonHazardKind.GroundSaw) == 2
                && hazards.Count(h => h.Kind == CannonHazardKind.FallingLoad) == 2 && hazards.Count(h => h.Kind == CannonHazardKind.FireJet) == 4);
            Check("21 original host registrations", course.HazardCount == 21);
            var registered = Enumerable.Range(0, course.HazardCount).Select(course.Hazard).ToArray();
            Check("no duplicate or missing registrations", registered.All(h => h != null) && registered.Distinct().Count() == 21);
            Check("original finale indices preserved", registered.Take(13).All(h => h.transform.IsChildOf(GameObject.Find(RootName + "/25_FinaleHazards_A").transform)));
            Check("new indices appended", registered.Skip(13).SequenceEqual(hazards));
            foreach (var hazard in hazards)
            {
                var cue = hazard.GetComponent<HumanCannonHazardCue>(); string label = hazard.transform.parent.name;
                Check(label + " cue/visual/warning", cue != null && cue.visual != null && cue.warning != null);
                if (cue == null) continue;
                double period = cue.warningSeconds + cue.activeSeconds + cue.restSeconds;
                double At(double cycle) => (cycle - cue.phase + period) % period;
                double live = At(cue.warningSeconds + cue.activeSeconds * .98), warning = At(cue.warningSeconds * .5), rest = At(cue.warningSeconds + cue.activeSeconds + cue.restSeconds * .5);
                var centre = hazard.Position(live, 1); float floor = hazard.Kind == CannonHazardKind.FireJet ? -8 : -22;
                var feet = new Vector3(centre.x, floor, centre.z);
                Check(label + " visible physical threat reaches route", hazard.Touches(feet, height, radius, live, 1));
                Check(label + " warning harmless", !hazard.Touches(feet, height, radius, warning, 1));
                Check(label + " rest harmless", !hazard.Touches(feet, height, radius, rest, 1));
                Check(label + " readable warning >=1.6s", cue.warningSeconds >= 1.6f);
                Check(label + " no permanent visual collider", cue.visual.GetComponentsInChildren<Collider>(true).Length == 0);
                if (hazard.Kind != CannonHazardKind.FireJet)
                {
                    // Gate depth plus body diameter at normal walking speed; excludes warning time from the safe allowance.
                    float depth = hazard.Kind == CannonHazardKind.GroundSaw ? .28f : new SerializedObject(hazard).FindProperty("boxSize").vector3Value.x;
                    float crossing = (depth + 2 * radius) / character.FreeMoveSpeed;
                    Check(label + " rest exceeds crossing+reaction", cue.restSeconds > crossing + .75f);
                    report.AppendLine(label + ": harmless rest " + cue.restSeconds.ToString("F1") + "s; nominal crossing " + crossing.ToString("F2") + "s.");
                }
            }
            foreach (var safe in root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Safe_")))
            {
                Vector3 at = safe.position;
                Check(safe.name + " floor", Physics.Raycast(at + Vector3.up * .2f, Vector3.down, out var hit, .4f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - at.y) < .03f);
                Check(safe.name + " standing room", !Physics.CheckCapsule(at + Vector3.up * (radius + .06f), at + Vector3.up * (height - radius + .06f), radius, ~0, QueryTriggerInteraction.Ignore));
                bool safeAll = true;
                for (int tick = 0; tick < 800 && safeAll; tick++)
                    safeAll = hazards.All(h => !h.Touches(at, height, radius, tick * .05, 1));
                Check(safe.name + " safe for full 40s joint cycle", safeAll);
            }
            // The pocket opening must be walkable without jumping over a retained rail/toe.
            for (float x = 63; x <= 68; x += .5f)
            {
                var at = new Vector3(x, -22, -24);
                Check("pocket access " + x, Physics.Raycast(at + Vector3.up * .2f, Vector3.down, .4f, ~0, QueryTriggerInteraction.Ignore)
                    && !Physics.CheckCapsule(at + Vector3.up * (radius + .06f), at + Vector3.up * (height - radius + .06f), radius, ~0, QueryTriggerInteraction.Ignore));
            }
            var crouchAt = new Vector3(110, -22, -32.5f);
            float crouchHeight = height * character.CrouchHeightRatio;
            Check("low duct requires crouch", Physics.CheckCapsule(crouchAt + Vector3.up * (radius + .06f), crouchAt + Vector3.up * (height - radius + .06f), radius, ~0, QueryTriggerInteraction.Ignore));
            for (float z = -37; z <= -28; z += .5f) foreach (float x in new[] { 109f, 110f, 111f })
            {
                var at = new Vector3(x, -22, z);
                Check("crouch clearance " + at, !Physics.CheckCapsule(at + Vector3.up * (radius + .06f), at + Vector3.up * (crouchHeight - radius + .06f), radius, ~0, QueryTriggerInteraction.Ignore));
            }
            // ParticleSystemRenderer reserves a null trail slot even when trails are disabled.
            Check("used new materials", root.GetComponentsInChildren<Renderer>().All(r =>
                (r is ParticleSystemRenderer && !r.GetComponent<ParticleSystem>().trails.enabled ? new[] { r.sharedMaterial } : r.sharedMaterials)
                .All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")));
            Check("no missing scripts", root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            report.AppendLine("Passed=" + passed + "; Failed=" + failed + ". Static geometry/contact/timing only; no player inputs or movement.");
            return report.ToString();
        }
    }
}
