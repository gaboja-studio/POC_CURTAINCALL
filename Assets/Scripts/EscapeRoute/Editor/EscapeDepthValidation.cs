using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateDepthExpansion()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            Physics.SyncTransforms();
            int passed = 0, failed = 0; var report = new StringBuilder();
            void Check(string label, bool ok) { if (ok) passed++; else { failed++; if (failed <= 30) report.AppendLine("FAIL " + label); } }
            var root = GameObject.Find(RootName).transform;
            Check("new route active", root.Find(DepthGroup).gameObject.activeInHierarchy);
            var extension = root.Find(DepthGroup);
            bool hasEncounters = root.Find(MidEncounterGroup) != null;
            bool hasSwings = root.Find(CompletionGroup) != null;
            Check("new materials", extension.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")));
            Check("persistent new meshes", extension.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMPro.TMP_Text>() == null).All(f => f.sharedMesh != null && AssetDatabase.Contains(f.sharedMesh)));
            Check("new missing scripts", extension.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            foreach (string old in new[] { "04_DeepServiceCut", "06_AscendingWorks" })
                Check("old route preserved " + old, !root.Find("17_BrutalistRoute/" + old).gameObject.activeSelf);
            float radius = GameSettings.Base.Character.CapsuleRadius, height = GameSettings.Base.Character.CapsuleHeight;
            var paths = new[] {
                new[] { new Vector3(68,2,61.5f), new Vector3(68,2,60.5f), new Vector3(68,-6,46.5f),
                    new Vector3(68,-6,41.5f), new Vector3(68,-14,27.5f), new Vector3(68,-14,22.5f),
                    new Vector3(68,-22,8.5f), new Vector3(68,-22,-58), new Vector3(110,-22,-58),
                    new Vector3(110,-22,-4), new Vector3(90,-22,-4) },
                new[] { new Vector3(122.5f,-8,47), new Vector3(122.5f,-8,112), new Vector3(135,-8,112),
                    new Vector3(135,-8,110), new Vector3(135,0,96), new Vector3(135,0,78), new Vector3(140,0,78) }
            };
            if (hasSwings)
                paths[0] = new[] {new Vector3(68,-22,1.5f),new Vector3(68,-22,-58),new Vector3(110,-22,-58),new Vector3(110,-22,-4),new Vector3(90,-22,-4)};
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                var path = paths[pathIndex]; float length = 0;
                for (int segment = 1; segment < path.Length; segment++)
                {
                    Vector3 from = path[segment - 1], to = path[segment], delta = to - from;
                    length += delta.magnitude;
                    int steps = Mathf.CeilToInt(delta.magnitude / .5f);
                    Vector3 side = Vector3.Cross(Vector3.up, delta.normalized).normalized;
                    for (int i = 0; i <= steps; i++) foreach (float offset in new[] { -1f, 0f, 1f })
                    {
                        var point = Vector3.Lerp(from, to, i / (float)steps) + side * offset;
                        bool floor = Physics.Raycast(point + Vector3.up * .3f, Vector3.down, out var hit, .65f, ~0, QueryTriggerInteraction.Ignore);
                        string label = "route" + pathIndex + " " + point;
                        Check(label + " floor", floor && Mathf.Abs(hit.point.y - point.y) <= .3f);
                        if (!floor) continue;
                        point.y = hit.point.y;
                        bool crouch = hasEncounters && pathIndex == 0 && point.x > 106 && point.z >= -36.5f && point.z <= -28.5f;
                        float sampleHeight = crouch ? height * GameSettings.Base.Character.CrouchHeightRatio : height;
                        float lift = crouch ? .06f : .26f;
                        var hits = Physics.OverlapCapsule(point + Vector3.up * (radius + lift), point + Vector3.up * (sampleHeight - radius + lift), radius, ~0, QueryTriggerInteraction.Ignore)
                            .Where(c => c.bounds.max.y > point.y + lift).ToArray();
                        Check(label + " body " + string.Join(",", hits.Select(c => c.name)), hits.Length == 0);
                    }
                }
                report.AppendLine("Route " + pathIndex + " centreline " + length.ToString("F1") + "m (walk/stairs; no live movement).");
            }
            var system = UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>();
            Check("two original stations", system != null && system.StationCount == 2);
            Check("deep flight boundary", system != null && Mathf.Approximately(system.FlightFloorHeight, -80));
            if (system != null && system.StationCount == 2)
            {
                var station = system.Station(1);
                Check("prepared original station", PrefabUtility.GetCorrespondingObjectFromSource(station) != null);
                Check("deep launch above flight boundary", station.Muzzle.position.y > system.FlightFloorHeight);
                Vector3 position = station.Muzzle.position, velocity = station.LaunchVelocity;
                Collider first = null; Vector3 contact = default; float travel = 0;
                for (int i = 0; i < 600; i++)
                {
                    Vector3 step = BallisticMotion.Step(ref velocity, Vector3.zero, GameSettings.Base.Character.Gravity, 1f / 120);
                    if (Physics.CapsuleCast(position + Vector3.up * radius, position + Vector3.up * (height - radius), radius,
                        step.normalized, out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    { first = hit.collider; contact = hit.point; break; }
                    position += step; travel += step.magnitude;
                }
                var destination = root.Find("17_BrutalistRoute/05_TransferChasm/트램폴린 진입 대").GetComponent<BoxCollider>();
                Check("return flight reaches A05 first (" + (first != null ? first.name : "none") + ")", first == destination);
                Check("A05 original safe landing", destination.GetComponent<HumanCannonLandingZone>() != null);
                report.AppendLine("Return first contact " + contact + "; arc " + travel.ToString("F1") + "m.");
                var board = station.BoardingPoint.position;
                Check("return cannon boarding support", Physics.Raycast(board + Vector3.up * .25f, Vector3.down, .6f, ~0, QueryTriggerInteraction.Ignore));
            }
            // Static signatures include active states and every transform under the preserved finale groups.
            foreach (string name in new[] { "19_RunnerEscape", "25_FinaleHazards_A" })
            {
                if (hasSwings) { report.AppendLine("SUPERSEDED " + name + " whole-tree signature: user requested original swing art, saw direction and fire replacement; ValidateCompletedEscape and ValidateRunnerEscape check the resulting geometry."); continue; }
                string snapshot = ".harness/local/runner-escape/before-mid-expansion-" + name + ".txt";
                if (!System.IO.File.Exists(snapshot)) { report.AppendLine("SKIP " + name + " preservation comparison: local pre-edit snapshot unavailable."); continue; }
                var expected = System.IO.File.ReadAllLines(snapshot);
                var actual = root.Find(name).GetComponentsInChildren<Transform>(true).Select(x => GlobalObjectId.GetGlobalObjectIdSlow(x) + "|" + x.name + "|" + x.gameObject.activeSelf + "|" + x.position.ToString("R") + "|" + x.rotation.ToString("R") + "|" + x.lossyScale.ToString("R")).ToArray();
                Check(name + " identical", expected.SequenceEqual(actual));
            }
            Check("old A04 shortcut sealed", Physics.Raycast(new Vector3(70, 1, 28), Vector3.right, 10, ~0, QueryTriggerInteraction.Ignore));
            Check("no bridge across pump void", !Physics.Raycast(new Vector3(76, -21.8f, -4), Vector3.down, 2, ~0, QueryTriggerInteraction.Ignore));
            Check("no A06 direct exit below core", Physics.Raycast(new Vector3(122, -6, 78), Vector3.right, 12, ~0, QueryTriggerInteraction.Ignore));
            report.AppendLine("Passed=" + passed + "; Failed=" + failed + ". Static queries only. No player movement or input.");
            return report.ToString();
        }

        [MenuItem("Tools/CurtainCall/탈출로/A 확장 펌프동 보기")]
        public static void ViewDepthPumpHall() => ViewAFusion(new Vector3(68, -20.3f, 2), new Vector3(86, -15, -47));

        [MenuItem("Tools/CurtainCall/탈출로/A 확장 북측 회랑 보기")]
        public static void ViewDepthNorthHall() => ViewAFusion(new Vector3(122.5f, -6.3f, 52), new Vector3(124, -3, 107));
    }
}
