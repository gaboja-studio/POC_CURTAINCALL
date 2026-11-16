using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string FinishedDescentRoot = "85_FinishedThermalDescent";
        const string ThermalWestParapet = "Thermal second-flight west parapet";
        const string ThermalLowerExitWall = "Thermal lower gallery west closure";

        [Serializable] public sealed class FinishedDescentReport
        {
            public bool alreadyApplied, commonRulesUnchanged, sharedEndpointsPreserved;
            public int removedObjects, newSteps, supportSamples, clearanceSamples, addedGuides;
            public float riser, maximumFlightDrop, ordinaryJumpHeight, injuryHeight;
            public string[] removedNames, blockers;
            public string scope = "Edit-mode support and body-clearance samples; not a play test.";
        }

        // Replace the three reversals with two forward flights and one obvious middle turn.
        // The original cooperative return stairs, upper cannon and lower flame course stay put.
        public static FinishedDescentReport FinishLabyrinthThermalDescent()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("A stopped Labyrinth Prototype is required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var remake = frame.Find("EscapeBackground/70_LabyrinthRemake");
            var report = new FinishedDescentReport
            {
                riser = .2f, maximumFlightDrop = 4,
                ordinaryJumpHeight = GameSettings.Base.Character.JumpHeight,
                injuryHeight = GameSettings.Base.BodyDamage.FallInjuryHeight
            };
            if (report.maximumFlightDrop + report.ordinaryJumpHeight >= report.injuryHeight)
                throw new InvalidOperationException("The common jump/fall settings no longer permit a four-metre isolated flight.");
            if (Vector3.Distance(remake.lossyScale, Vector3.one) > .001f)
                throw new InvalidOperationException("Unexpected remake scale; preserve actual world dimensions.");
            int undo = -1;
            try
            {
                var finished = remake.Find(FinishedDescentRoot);
                report.alreadyApplied = finished;
                if (!finished)
                {
                    var old = frame.GetComponentsInChildren<Transform>(true).Where(t => OldThermalDescentObject(t.name)).ToList();
                    // The old working-facility batch is visual-only. The inspection found
                    // disconnected upper pipe mounts and a clip inside the replaced steps.
                    // Remove this scene instance; never modify its shared ThirdParty mesh.
                    var obsoleteFixture = frame.Find("EscapeBackground/18_WorkingFacilityFinish/Factory static 27");
                    if (obsoleteFixture)
                    {
                        var mesh = obsoleteFixture.GetComponent<MeshFilter>();
                        if (!mesh || AssetDatabase.GetAssetPath(mesh.sharedMesh) != "Assets/_ThirdParty/Task073/FactoryMeshes/static-27.asset")
                            throw new InvalidOperationException("Unexpected old thermal fixture mesh.");
                        old.Add(obsoleteFixture);
                    }
                    if (old.Count(t => t.name.Contains(" step ")) != 40)
                        throw new InvalidOperationException("Expected the four original ten-step flights.");
                    foreach (var t in old)
                        if (t.childCount != 0 || t.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter
                            || c is MeshRenderer || c is BoxCollider || c is MeshCollider)))
                            throw new InvalidOperationException("Unexpected gameplay or children on old stair piece: " + t.name);
                    var concrete = old.Single(t => t.name == "Switchback central concrete divider")
                        .GetComponent<MeshRenderer>().sharedMaterial;
                    var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-steel.mat");
                    if (!concrete || !steel) throw new InvalidOperationException("Existing factory materials missing.");
                    Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Finish thermal descent architecture");
                    finished = new GameObject(FinishedDescentRoot).transform;
                    finished.SetParent(remake, false); Undo.RegisterCreatedObjectUndo(finished.gameObject, "Thermal descent");
                    BoxCollider Box(string name, Vector3 min, Vector3 max, Material material)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                        go.transform.SetParent(finished, false);
                        go.transform.SetPositionAndRotation(frame.TransformPoint((min + max) * .5f), frame.rotation);
                        go.transform.localScale = max - min;
                        go.GetComponent<MeshRenderer>().sharedMaterial = material;
                        Undo.RegisterCreatedObjectUndo(go, "Finished thermal structure");
                        return go.GetComponent<BoxCollider>();
                    }
                    void Flight(string name, float left, float from, float to, float top)
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            float a = Mathf.Lerp(from, to, i / 20f), b = Mathf.Lerp(from, to, (i + 1) / 20f);
                            Box(name + " step " + i.ToString("00"), new(left, -9, a),
                                new(left + 4.5f, top - (i + 1) * .2f, b), concrete);
                            report.newSteps++;
                        }
                    }
                    Flight("Thermal first flight", 120, 35, 41, 0);
                    Box("Thermal single middle landing", new(120, -9, 41), new(129.5f, -4, 44), concrete);
                    Flight("Thermal second flight", 125, 44, 49, -4);
                    // The old gallery already occupies X120..125, Z49..89. Meet its east
                    // edge, not its top face, so no coplanar overlap or additional void floor.
                    Box("Thermal final flush landing", new(125, -9, 49), new(129.5f, -8, 51), concrete);
                    Box("Thermal integral central divider", new(124.5f, -9, 35), new(125, 3.2f, 41), concrete);
                    Box("Thermal integral turn wall", new(120, -9, 44), new(125, 3.2f, 44.3f), concrete);
                    // The west side of the second flight borders an unusable pocket,
                    // including a narrow seam above the old lower connection. A running
                    // player turning back must meet a wall, not fall through that pocket.
                    Box(ThermalWestParapet, new(124.5f, -9, 44.3f), new(125, -2.8f, 49), concrete);
                    // Continue the old retaining face across the lower gallery exit.
                    // This closes only the obsolete A05 side pocket, not a playable route.
                    Box(ThermalLowerExitWall, new(119.5f, -9, 48.5f), new(120, -5.5f, 51), concrete);
                    Box("Thermal upper east return wall", new(125, -9, 35), new(129.5f, 3.2f, 35.3f), concrete);
                    // Full-height east closure completes the final landing and sends the
                    // player left into the existing gallery. It is outside the gate-6 jump.
                    Box("Thermal final landing end wall", new(125, -9, 51), new(129.5f, 3.2f, 51.3f), concrete);
                    // Keep the existing west-wall notch at Z45.5..48.5 / topY-3: the
                    // pursuer's gate-6 outward and return arc needs that exact opening.
                    void Lamp(string name, Vector3 at, Vector3 target)
                    {
                        Box(name + " recessed steel fixture", at - new Vector3(.38f, .12f, .09f),
                            at + new Vector3(.38f, .12f, .09f), steel);
                        var go = new GameObject(name); go.transform.SetParent(finished, false);
                        go.transform.SetPositionAndRotation(frame.TransformPoint(at + Vector3.back * .11f),
                            Quaternion.LookRotation(frame.TransformDirection(target - at)));
                        var light = go.AddComponent<Light>(); light.type = LightType.Spot;
                        light.color = new Color(.82f, .86f, .78f); light.intensity = 75;
                        light.range = 13; light.spotAngle = 78; light.innerSpotAngle = 48;
                        light.shadows = LightShadows.None; Undo.RegisterCreatedObjectUndo(go, "Mounted thermal task light");
                    }
                    Lamp("Thermal turn task light", new(122.25f, 1.8f, 43.96f), new(122.25f, -1, 37));
                    Lamp("Thermal gallery entry task light", new(127.25f, -1.8f, 50.96f), new(127.25f, -6, 47));
                    report.removedNames = old.Select(t => t.name).ToArray(); report.removedObjects = old.Count;
                    foreach (var t in old) Undo.DestroyObjectImmediate(t.gameObject);
                    report.addedGuides = FitFinishedThermalGuides(frame, finished);
                }
                Physics.SyncTransforms();
                ValidateFinishedThermalDescent(frame, finished, report);
                report.commonRulesUnchanged = report.sharedEndpointsPreserved = true;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                if (undo >= 0) Undo.CollapseUndoOperations(undo);
                WriteThermalReport(report);
                return report;
            }
            catch
            {
                WriteThermalReport(report);
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        static bool OldThermalDescentObject(string name)
        {
            string[] prefixes = { "Upper south to north step ", "Upper north to south step ",
                "Lower south to north step ", "Flame exit flight step " };
            string[] names = { "Upper turning landing", "Lower turning landing", "Lower north landing",
                "Flame exit flush landing", "Switchback central concrete divider", "Upper landing jump guard",
                "Lower south landing jump guard", "Lower north landing jump guard", "Stairwell south side guard",
                "Entry platform side guard", "Lower landing underside closure", "Flame landing turn guard",
                "Final stair west jump guard", "Stair exit west jump guard" };
            return prefixes.Any(name.StartsWith) || names.Any(n => name == n || name.StartsWith(n + " fitted return", StringComparison.Ordinal));
        }

        [Serializable] public sealed class FinishedDescentEdgeReport
        {
            public bool alreadyApplied, preservedCommonRules, protectedBacktrackingEdge;
            public int supportSamples, clearanceSamples, barrierCastSamples;
            [Newtonsoft.Json.JsonIgnore] public Vector3 min, max;
            public float[] minimum => new[] { min.x, min.y, min.z };
            public float[] maximum => new[] { max.x, max.y, max.z };
            public string scope = "Scene repair and static body/support/edge checks. Requires a new play test and pursuit jump clearance review.";
        }

        // Targeted revision for existing copies made before the backtracking fixture
        // exposed the open west edge. Do not overwrite the original descent report.
        public static FinishedDescentEdgeReport FixFinishedThermalDescentEdge()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("A stopped Labyrinth Prototype is required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var root = frame.Find("EscapeBackground/70_LabyrinthRemake/" + FinishedDescentRoot);
            if (!root || Vector3.Distance(root.lossyScale, Vector3.one) > .001f)
                throw new InvalidOperationException("The finished thermal descent with unit world scale is required.");
            var report = new FinishedDescentEdgeReport { min = new(124.5f, -9, 44.3f), max = new(125, -2.8f, 49) };
            int undo = -1;
            try
            {
                var piece = root.Find(ThermalWestParapet);
                report.alreadyApplied = piece;
                if (!piece)
                {
                    Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Close thermal stair backtracking edge");
                    var material = root.Find("Thermal integral central divider").GetComponent<MeshRenderer>().sharedMaterial;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = ThermalWestParapet;
                    go.transform.SetParent(root, false);
                    go.transform.SetPositionAndRotation(frame.TransformPoint((report.min + report.max) * .5f), frame.rotation);
                    go.transform.localScale = report.max - report.min;
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    Undo.RegisterCreatedObjectUndo(go, "Integral thermal west parapet"); piece = go.transform;
                }
                var box = piece.GetComponent<BoxCollider>();
                if (!box || box.isTrigger || !box.enabled || !piece.gameObject.activeInHierarchy || piece.childCount != 0
                    || piece.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer || c is BoxCollider)))
                    throw new InvalidOperationException("Unexpected components on the thermal west parapet.");
                Physics.SyncTransforms();
                var local = new Bounds(frame.InverseTransformPoint(box.bounds.center), box.bounds.size);
                if (Vector3.Distance(local.min, report.min) > .002f || Vector3.Distance(local.max, report.max) > .002f)
                    throw new InvalidOperationException("Thermal west parapet does not match its reviewed solid volume.");
                var samples = new FinishedDescentReport();
                ValidateFinishedThermalDescent(frame, root, samples);
                report.supportSamples = samples.supportSamples; report.clearanceSamples = samples.clearanceSamples;
                float radius = GameSettings.Base.Character.CapsuleRadius - .015f;
                float height = GameSettings.Base.Character.CapsuleHeight;
                float jump = GameSettings.Base.Character.JumpHeight;
                for (int i = 0; i < 20; i++)
                {
                    float z = 44 + (i + .5f) * .25f;
                    float top = -4 - (i + 1) * .2f;
                    foreach (float lift in new[] { .205f, jump + .08f })
                    {
                        var feet = frame.TransformPoint(new Vector3(125.7f, top + lift, z));
                        var hits = Physics.CapsuleCastAll(feet + Vector3.up * radius,
                            feet + Vector3.up * (height - radius), radius, frame.TransformDirection(Vector3.left),
                            1.2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
                        // The first tread shares this edge with the existing turn wall;
                        // both pieces meet at Z44.3 with the same X125 public face.
                        if (hits.Length == 0 || (hits[0].collider != box && hits[0].collider.transform != root.Find("Thermal integral turn wall")))
                            throw new InvalidOperationException("Thermal west edge is not the first body barrier at step " + i + ", lift " + lift);
                        report.barrierCastSamples++;
                    }
                }
                report.protectedBacktrackingEdge = report.preservedCommonRules = true;
                // The caller validates all actual pursuit arcs before saving this repair.
                EditorSceneManager.MarkSceneDirty(scene);
                if (undo >= 0) Undo.CollapseUndoOperations(undo);
                Directory.CreateDirectory(".harness/local/pursuer-polish-20261116");
                File.WriteAllText(".harness/local/pursuer-polish-20261116/thermal-descent-edge-fix.json",
                    Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
                return report;
            }
            catch
            {
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        public static FinishedDescentEdgeReport FixFinishedThermalExitEdge()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("A stopped Labyrinth Prototype is required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var root = frame.Find("EscapeBackground/70_LabyrinthRemake/" + FinishedDescentRoot);
            if (!root || Vector3.Distance(root.lossyScale, Vector3.one) > .001f)
                throw new InvalidOperationException("The finished thermal descent with unit world scale is required.");
            var report = new FinishedDescentEdgeReport
            {
                min = new(119.5f, -9, 48.5f), max = new(120, -5.5f, 51),
                scope = "Close the obsolete A05 side pocket from the lower public gallery. The prior probe did not establish access through the private interior wall. Static checks require pursuit arc validation and play verification."
            };
            int undo = -1;
            try
            {
                var piece = root.Find(ThermalLowerExitWall);
                report.alreadyApplied = piece;
                if (!piece)
                {
                    Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Restore lower thermal gallery west closure");
                    var material = root.Find("Thermal integral central divider").GetComponent<MeshRenderer>().sharedMaterial;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = ThermalLowerExitWall;
                    go.transform.SetParent(root, false);
                    go.transform.SetPositionAndRotation(frame.TransformPoint((report.min + report.max) * .5f), frame.rotation);
                    go.transform.localScale = report.max - report.min;
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    Undo.RegisterCreatedObjectUndo(go, "Integral lower gallery west closure"); piece = go.transform;
                }
                var box = piece.GetComponent<BoxCollider>();
                if (!box || box.isTrigger || !box.enabled || !piece.gameObject.activeInHierarchy || piece.childCount != 0
                    || piece.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer || c is BoxCollider)))
                    throw new InvalidOperationException("Unexpected components on the thermal lower exit wall.");
                Physics.SyncTransforms();
                var local = new Bounds(frame.InverseTransformPoint(box.bounds.center), box.bounds.size);
                if (Vector3.Distance(local.min, report.min) > .002f || Vector3.Distance(local.max, report.max) > .002f)
                    throw new InvalidOperationException("Thermal lower exit wall dimensions differ from the reviewed volume.");
                var samples = new FinishedDescentReport();
                ValidateFinishedThermalDescent(frame, root, samples);
                report.supportSamples = samples.supportSamples; report.clearanceSamples = samples.clearanceSamples;
                float radius = GameSettings.Base.Character.CapsuleRadius - .015f;
                float fullHeight = GameSettings.Base.Character.CapsuleHeight;
                foreach (var stance in new[]
                {
                    (height: fullHeight, lift: .08f),
                    (height: fullHeight * GameSettings.Base.Character.CrouchHeightRatio, lift: .08f),
                    (height: fullHeight, lift: GameSettings.Base.Character.JumpHeight + .08f)
                })
                for (int i = 0; i < 10; i++)
                {
                    var feet = frame.TransformPoint(new Vector3(120.7f, -8 + stance.lift, 48.5f + (i + .5f) * .25f));
                    var hits = Physics.CapsuleCastAll(feet + Vector3.up * radius,
                        feet + Vector3.up * (stance.height - radius), radius, frame.TransformDirection(Vector3.left),
                        1.2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
                    if (hits.Length == 0 || (hits[0].collider != box
                        && !hits[0].collider.name.StartsWith("Switchback west retaining wall", StringComparison.Ordinal)))
                        throw new InvalidOperationException("Lower gallery west closure is not the first body barrier at sample " + i);
                    report.barrierCastSamples++;
                }
                report.protectedBacktrackingEdge = report.preservedCommonRules = true;
                // Root validates the actual sixteen encounter arcs before saving.
                EditorSceneManager.MarkSceneDirty(scene);
                if (undo >= 0) Undo.CollapseUndoOperations(undo);
                Directory.CreateDirectory(".harness/local/pursuer-polish-20261116");
                File.WriteAllText(".harness/local/pursuer-polish-20261116/thermal-exit-edge-fix.json",
                    Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
                return report;
            }
            catch
            {
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        static int FitFinishedThermalGuides(Transform frame, Transform finished)
        {
            var route = frame.GetComponentInChildren<EscapePursuerRoute>(true);
            var data = new SerializedObject(route); var array = data.FindProperty("playerGuides");
            Undo.RecordObject(route, "Align thermal public path guides");
            int matches = 0;
            for (int i = array.arraySize - 1; i >= 0; i--)
            {
                var row = array.GetArrayElementAtIndex(i);
                var from = row.FindPropertyRelative("from").objectReferenceValue as Transform;
                var to = row.FindPropertyRelative("to").objectReferenceValue as Transform;
                if (from && to && from.name == "Player course 31" && to.name == "Player course 32")
                { array.DeleteArrayElementAtIndex(i); matches++; }
            }
            if (matches != 1) throw new InvalidOperationException("Expected one obsolete diagonal fire-entry guide.");
            var points = new[] { new Vector3(118, 0, 32), new(122.25f, 0, 34.8f), new(122.25f, -4, 42.5f),
                new(127.25f, -4, 42.5f), new(127.25f, -8, 50), new(122.5f, -8, 50), new(122.5f, -8, 52) };
            var progress = new[] { 29f, 29.3f, 30.2f, 30.5f, 31.4f, 31.8f, 32f };
            var anchors = points.Select((p, i) =>
            {
                var t = new GameObject("Thermal public descent " + i).transform;
                t.SetParent(finished, false); t.position = frame.TransformPoint(p);
                Undo.RegisterCreatedObjectUndo(t.gameObject, "Thermal public path anchor"); return t;
            }).ToArray();
            for (int i = 1; i < anchors.Length; i++)
            {
                int n = array.arraySize++; var row = array.GetArrayElementAtIndex(n);
                row.FindPropertyRelative("from").objectReferenceValue = anchors[i - 1];
                row.FindPropertyRelative("to").objectReferenceValue = anchors[i];
                row.FindPropertyRelative("fromProgress").floatValue = progress[i - 1];
                row.FindPropertyRelative("toProgress").floatValue = progress[i];
            }
            data.ApplyModifiedProperties(); return anchors.Length - 1;
        }

        static void ValidateFinishedThermalDescent(Transform frame, Transform finished, FinishedDescentReport report)
        {
            var blocked = new List<string>();
            void Sample(Vector3 local, string label)
            {
                var feet = frame.TransformPoint(local);
                bool support = Physics.RaycastAll(feet + Vector3.up * .08f, Vector3.down, .16f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(h => h.collider.gameObject.scene == frame.gameObject.scene && h.normal.y > .9f && Mathf.Abs(h.point.y - feet.y) < .025f);
                if (!support) blocked.Add(label + " missing flush support " + local);
                report.supportSamples++;
                var character = GameSettings.Base.Character;
                float radius = character.CapsuleRadius - .015f;
                foreach (float height in new[] { character.CapsuleHeight, character.CapsuleHeight * character.CrouchHeightRatio })
                {
                    // Ignore the lowest 200 mm which is the deliberately walkable stair
                    // riser. Test the rest of the standing/crouching body at its real height.
                    var lower = feet + Vector3.up * (.205f + radius);
                    var upper = feet + Vector3.up * Mathf.Max(.205f + radius, height - radius);
                    var hits = Physics.OverlapCapsule(lower, upper, radius, ~0, QueryTriggerInteraction.Ignore)
                        .Where(c => c.gameObject.scene == frame.gameObject.scene && c.enabled && !(c is CharacterController)).ToArray();
                    if (hits.Length > 0) blocked.Add(label + " body obstruction " + string.Join(", ", hits.Select(h => h.name).Distinct()));
                    report.clearanceSamples++;
                }
            }
            foreach (var step in finished.GetComponentsInChildren<BoxCollider>().Where(c => c.name.Contains(" flight step ")))
            {
                var center = frame.InverseTransformPoint(step.bounds.center); float top = step.bounds.max.y - frame.position.y;
                foreach (float dx in new[] { -1.3f, 0f, 1.3f }) Sample(new(center.x + dx, top, center.z), step.name);
            }
            foreach (var p in new[] { new Vector3(122.25f, 0, 34.6f), new(122.25f, -4, 41.5f), new(122.25f, -4, 42.5f),
                new(124.75f, -4, 42.5f), new(127.25f, -4, 42.5f), new(127.25f, -4, 43.5f), new(127.25f, -8, 50),
                new(125.05f, -8, 50), new(124.95f, -8, 50), new(122.5f, -8, 50), new(122.5f, -8, 52) }) Sample(p, "landing/entry seam");
            report.blockers = blocked.Distinct().ToArray();
            if (report.blockers.Length > 0) throw new InvalidOperationException("Thermal descent static check failed: " + string.Join("; ", report.blockers.Take(8)));
        }

        static void WriteThermalReport(FinishedDescentReport report)
        {
            const string directory = ".harness/local/pursuer-polish-20261116";
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/thermal-descent.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
