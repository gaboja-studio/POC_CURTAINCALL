using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        [Serializable]
        public sealed class ThermalSoffitReport
        {
            public bool applied;
            public int removed, created, eyes, hiddenTargets, sightlines, visibleBefore, visibleAfter;
            public int encounters, jumpDirections, clearJumpsBefore, clearJumpsAfter, newPrivateViews, jumpRegressions;
            public float[] gate8Exit, gate8Landing;
            public string rejection;
        }

        public static object FinishLabyrinthThermalSoffit()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Stopped Prototype required.");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var root = frame.Find("EscapeBackground/70_LabyrinthRemake");
            const string markerName = "87_FittedThermalSoffit";
            if (!root) throw new InvalidOperationException("Labyrinth root required.");
            if (root.Find(markerName)) return new { applied = false, reason = "Soffit already fitted" };
            var route = frame.GetComponentInChildren<EscapePursuerRoute>(true);
            var chase = route.GetComponentInChildren<EscapeUnderstageChase>(true);
            var walls = route.GetComponent<EscapePursuerWalls>();
            if (!chase || !walls) throw new InvalidOperationException("Existing physical pursuit required.");
            var opening = new Bounds(); opening.SetMinMax(new Vector3(139, 6, 70.4f), new Vector3(149, 11, 75.6f));
            Vector3 centre = frame.TransformPoint(opening.center);
            var report = new ThermalSoffitReport();
            var serialized = new SerializedObject(chase).FindProperty("encounters");
            report.encounters = serialized.arraySize;
            if (report.encounters != 16) throw new InvalidOperationException("Inspect changed encounter registry before soffit fitting.");
            var jumps = new List<(Vector3 from, Vector3 to)>();
            var hidden = new List<Vector3>();
            int gate8 = 0;
            void Hidden(Vector3 feet)
            {
                foreach (float height in new[] { 1f, 3f })
                {
                    Vector3 p = feet + Vector3.up * height;
                    if (Vector3.Distance(p, centre) < 130 && !hidden.Any(v => (v - p).sqrMagnitude < .01f)) hidden.Add(p);
                }
            }
            for (int i = 0; i < report.encounters; i++)
            {
                var row = serialized.GetArrayElementAtIndex(i);
                Transform Anchor(string key) => row.FindPropertyRelative(key).objectReferenceValue as Transform;
                var exit = Anchor("exit"); var landing = Anchor("landing");
                if (!exit || !landing) throw new InvalidOperationException("Missing physical encounter anchors: " + i);
                jumps.Add((exit.position, landing.position)); jumps.Add((landing.position, exit.position));
                foreach (string key in new[] { "ingress", "staging", "approach" })
                {
                    var anchor = Anchor(key);
                    if (!anchor) throw new InvalidOperationException("Missing private encounter anchor: " + key);
                    Hidden(anchor.position);
                }
                if (row.FindPropertyRelative("gate").intValue == 8)
                {
                    gate8++;
                    var a = frame.InverseTransformPoint(exit.position); var b = frame.InverseTransformPoint(landing.position);
                    report.gate8Exit = new[] { a.x, a.y, a.z };
                    report.gate8Landing = new[] { b.x, b.y, b.z };
                }
            }
            if (gate8 != 1) throw new InvalidOperationException("Expected one actual gate8 encounter.");
            for (int i = 0; i < route.Points.Count; i++)
            {
                var p = route.Points[i];
                if (!p.feet || p.exposed) continue;
                Hidden(p.feet.position);
                if (i == 0 || !route.Points[i - 1].feet || route.Points[i - 1].exposed) continue;
                Vector3 a = route.Points[i - 1].feet.position, b = p.feet.position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 3));
                float arc = p.jump ? EscapeUnderstageChase.ServiceJumpArc(a, b) : 0;
                for (int j = 1; j < steps; j++)
                {
                    float t = j / (float)steps;
                    Hidden(Vector3.Lerp(a, b, t) + Vector3.up * (arc * t * (1 - t)));
                }
            }

            var eyes = new List<Vector3>();
            var character = GameSettings.Base.Character;
            void Eyes(Vector3 feet)
            {
                foreach (float height in new[] { character.CapsuleHeight * .9f, character.CapsuleHeight * character.CrouchHeightRatio * .9f })
                {
                    Vector3 p = feet + Vector3.up * height;
                    if (Vector3.Distance(p, centre) <= 65 && !eyes.Any(v => (v - p).sqrMagnitude < .0025f)) eyes.Add(p);
                }
            }
            void Segment(Vector3 a, Vector3 b)
            {
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 3));
                for (int i = 0; i <= steps; i++) Eyes(Vector3.Lerp(a, b, i / (float)steps));
            }
            foreach (var guide in route.PlayerGuides)
                if (guide.from && guide.to) Segment(guide.from.position, guide.to.position);
            foreach (var point in route.Points) if (point.playerPath) Eyes(point.playerPath.position);
            foreach (var course in frame.GetComponentsInChildren<TightropeCourse>(true))
            {
                var ropes = new SerializedObject(course).FindProperty("sceneRopes");
                for (int i = 0; i < ropes.arraySize; i++)
                {
                    var rope = ropes.GetArrayElementAtIndex(i).objectReferenceValue as BoxCollider;
                    if (!rope || !rope.enabled || !rope.gameObject.activeInHierarchy) continue;
                    Vector3 top = rope.center + Vector3.up * (rope.size.y * .5f);
                    Segment(rope.transform.TransformPoint(top - Vector3.forward * (rope.size.z * .5f)),
                        rope.transform.TransformPoint(top + Vector3.forward * (rope.size.z * .5f)));
                }
            }
            foreach (var pole in frame.GetComponentsInChildren<HumanCannonPole>(true))
            {
                if (!pole.isActiveAndEnabled || Vector3.Distance(pole.Grip.position, centre) > 75) continue;
                foreach (float fraction in new[] { -1f, -.5f, 0f, .5f, 1f })
                for (int seat = 0; seat < pole.RiderCapacity; seat++)
                    Eyes(pole.HangingPosition(GameSettings.Poles.MaxSwingAngle * fraction, seat));
            }
            report.eyes = eyes.Count; report.hiddenTargets = hidden.Count;
            if (eyes.Count < 20 || hidden.Count < 20) throw new InvalidOperationException("Insufficient authored visibility samples.");

            bool Visible(Vector3 eye, Vector3 target)
            {
                Vector3 delta = target - eye;
                if (delta.sqrMagnitude < .01f) return true;
                return !Physics.RaycastAll(eye, delta.normalized, delta.magnitude - .02f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(h =>
                    !h.collider.transform.IsChildOf(chase.transform) && !h.collider.GetComponentInParent<NetworkPlayer>()
                    && !h.collider.GetComponentInParent<EscapePursuerDebris>());
            }
            T WithOpenGates<T>(Func<T> inspect)
            {
                var barriers = Enumerable.Range(0, walls.GateCount).Select(i => walls.At(i)?.barrier).Where(c => c).Distinct().ToArray();
                var states = barriers.Select(c => c.enabled).ToArray();
                try
                {
                    foreach (var b in barriers) b.enabled = false;
                    Physics.SyncTransforms();
                    return inspect();
                }
                finally
                {
                    for (int i = 0; i < barriers.Length; i++) if (barriers[i]) barriers[i].enabled = states[i];
                    Physics.SyncTransforms();
                }
            }
            bool[] CheckJumps() => WithOpenGates(() => jumps.Select(j =>
                EscapePursuerNavigation.ClearJump(chase.transform, j.from, j.to)).ToArray());
            Physics.SyncTransforms();
            var beforeJumps = CheckJumps(); report.jumpDirections = beforeJumps.Length;
            report.clearJumpsBefore = beforeJumps.Count(v => v);
            var rays = (from eye in eyes from target in hidden where Vector3.Distance(eye, target) <= 65
                select (eye, target)).ToArray();
            var visibleClosed = rays.Select(r => Visible(r.eye, r.target)).ToArray();
            var visibleOpen = WithOpenGates(() => rays.Select(r => Visible(r.eye, r.target)).ToArray());
            report.sightlines = rays.Length * 2;
            report.visibleBefore = visibleClosed.Count(v => v) + visibleOpen.Count(v => v);

            var referenced = new HashSet<Object>();
            foreach (var behavior in frame.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behavior) continue;
                var it = new SerializedObject(behavior).GetIterator();
                while (it.Next(true)) if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue)
                    referenced.Add(it.objectReferenceValue);
            }
            var candidates = new List<(MeshRenderer renderer, Bounds bounds)>();
            foreach (var r in frame.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !r.name.StartsWith("Concrete remainder", StringComparison.Ordinal)) continue;
                var b = LocalBounds(frame, r); if (!FacadeOverlaps(b, opening)) continue;
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh; var colliders = r.GetComponents<Collider>();
                var components = r.GetComponents<Component>();
                if (!mesh || mesh.triangles.Length != 36 || r.transform.childCount != 0 || r.sharedMaterials.Length != 1 || !r.sharedMaterial
                    || colliders.Length != 1 || !colliders[0].enabled || colliders[0].isTrigger
                    || Vector3.Distance(colliders[0].bounds.center, r.bounds.center) > .0005f
                    || Vector3.Distance(colliders[0].bounds.size, r.bounds.size) > .0005f
                    || components.Any(c => !(c is Transform || c is MeshRenderer || c is MeshFilter || c is BoxCollider || c is MeshCollider))
                    || referenced.Contains(r.gameObject) || components.Any(referenced.Contains)
                    || mesh.vertices.Any(v =>
                    {
                        var p = frame.InverseTransformPoint(r.transform.TransformPoint(v));
                        return Enumerable.Range(0, 3).Any(a => Mathf.Min(Mathf.Abs(p[a] - b.min[a]), Mathf.Abs(p[a] - b.max[a])) > .0005f);
                    })) throw new InvalidOperationException("Soffit intersects unverified structural object: " + r.name);
                candidates.Add((r, b));
            }
            if (candidates.Count == 0) throw new InvalidOperationException("Expected downward soffit fragments missing.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fit thermal soffit without exposing pursuit galleries");
            try
            {
                foreach (var row in candidates)
                {
                    foreach (var b in FacadeSubtract(row.bounds, opening))
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Concrete remainder";
                        Undo.RegisterCreatedObjectUndo(go, "Retain fitted soffit structure");
                        go.transform.SetParent(row.renderer.transform.parent, false);
                        go.transform.SetPositionAndRotation(frame.TransformPoint(b.center), frame.rotation);
                        var scale = go.transform.parent.lossyScale;
                        go.transform.localScale = new Vector3(b.size.x / Mathf.Abs(scale.x), b.size.y / Mathf.Abs(scale.y), b.size.z / Mathf.Abs(scale.z));
                        go.GetComponent<MeshRenderer>().sharedMaterial = row.renderer.sharedMaterial;
                        go.layer = row.renderer.gameObject.layer; GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(row.renderer.gameObject));
                        report.created++;
                    }
                    Undo.DestroyObjectImmediate(row.renderer.gameObject); report.removed++;
                }
                Physics.SyncTransforms();
                var afterJumps = CheckJumps(); report.clearJumpsAfter = afterJumps.Count(v => v);
                report.jumpRegressions = beforeJumps.Where((v, i) => v && !afterJumps[i]).Count();
                void CompareSightlines(bool[] baseline)
                {
                    for (int i = 0; i < rays.Length; i++)
                    {
                        bool visible = Visible(rays[i].eye, rays[i].target);
                        if (visible) report.visibleAfter++;
                        if (visible && !baseline[i]) report.newPrivateViews++;
                    }
                }
                CompareSightlines(visibleClosed);
                WithOpenGates(() => { CompareSightlines(visibleOpen); return true; });
                if (report.newPrivateViews > 0 || report.jumpRegressions > 0)
                    throw new InvalidOperationException($"Soffit rejected: {report.newPrivateViews} new private views, {report.jumpRegressions} jump regressions.");
                var marker = new GameObject(markerName); Undo.RegisterCreatedObjectUndo(marker, "Fitted soffit marker");
                marker.transform.SetParent(root, false);
                report.applied = true; EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
                File.WriteAllText(".harness/local/pursuer-polish-20261116/soffit-apply.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
                return report;
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(undo); Physics.SyncTransforms(); report.applied = false; report.rejection = ex.Message;
                File.WriteAllText(".harness/local/pursuer-polish-20261116/soffit-rejected.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
                throw;
            }
        }
    }
}
