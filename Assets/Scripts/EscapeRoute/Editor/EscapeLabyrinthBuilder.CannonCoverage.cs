using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string BoardingBayRoot = "84_ExtractionBoardingPursuit";
        const string BoardingBayLabel = "추출 대포 승차장 · 기존 상부 합류대 추격";

        [Serializable] public sealed class BoardingCoverageReport
        {
            public bool alreadyApplied;
            public int removedCuboids, retainedCuboids, encounters, gates;
        }

        // Reuse the gate and the existing public floor. The heat-route encounter remains
        // registered separately; neither branch loses its original physical approach.
        public static BoardingCoverageReport AddLabyrinthBoardingEncounter()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Stopped Prototype required");
            var previousFrame = labyrinthFrame;
            var previousRoot = labyrinthRoot;
            int undo = -1;
            try
            {
                labyrinthFrame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Single(t => t.name == "EscapeCampaignFrame");
                var additions = labyrinthFrame.Find("EscapeBackground/70_LabyrinthRemake");
                var route = labyrinthFrame.GetComponentInChildren<EscapePursuerRoute>(true);
                var chase = route.GetComponentInChildren<EscapeUnderstageChase>(true);
                var data = new SerializedObject(chase);
                var rows = data.FindProperty("encounters");
                int source = -1, existing = -1;
                for (int i = 0; i < rows.arraySize; i++)
                {
                    var row = rows.GetArrayElementAtIndex(i);
                    if (row.FindPropertyRelative("label").stringValue == BoardingBayLabel) existing = i;
                    if (row.FindPropertyRelative("platform").objectReferenceValue is BoxCollider p && p.name == "Extraction public pursuit area") source = i;
                }
                if (source < 0) throw new InvalidOperationException("Existing Extraction encounter missing");
                var report = new BoardingCoverageReport { alreadyApplied = existing >= 0 };
                labyrinthRoot = additions.Find(BoardingBayRoot);
                if ((existing >= 0) != (labyrinthRoot != null)) throw new InvalidOperationException("Partial boarding encounter authoring");
                Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Connect existing extraction boarding pursuit floor");
                if (!labyrinthRoot)
                {
                    labyrinthRoot = new GameObject(BoardingBayRoot).transform;
                    Undo.RegisterCreatedObjectUndo(labyrinthRoot.gameObject, "Boarding pursuit root");
                    labyrinthRoot.SetParent(additions, false);
                }
                // Keep the full west retaining wall, floor and lower 2.5m of the chamber
                // facade. Only the upper diagonal takeoff and its ceiling clearance change.
                CutBoardingCuboids(new Bounds(new(116.45f, 1.25f, 46.3f), new(4.3f, 13.5f, 4.6f)), report);
                CutBoardingCuboids(new Bounds(new(116.45f, 10.4f, 40), new(4.3f, .8f, 10)), report);
                if (existing < 0)
                {
                    Undo.RecordObject(chase, "Append boarding encounter");
                    var row = rows.GetArrayElementAtIndex(source);
                    Transform Anchor(string key) => (Transform)row.FindPropertyRelative(key).objectReferenceValue;
                    Vector3 P(string key) => labyrinthFrame.InverseTransformPoint(Anchor(key).position);
                    LBranchEncounter(route, BoardingBayLabel, row.FindPropertyRelative("gate").intValue,
                        33, 48, Anchor("ingress"), P("staging"), P("approach"), P("exit"),
                        new(118.5f, 0, 32), new(120.5f, 0, 32), new(9, 5, 6), "Boarding");
                }
                Physics.SyncTransforms();
                report.encounters = new SerializedObject(chase).FindProperty("encounters").arraySize;
                report.gates = new SerializedObject(route.GetComponent<EscapePursuerWalls>()).FindProperty("gates").arraySize;
                if (!report.alreadyApplied || report.removedCuboids > 0) EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
                return report;
            }
            catch { if (undo >= 0) Undo.RevertAllDownToGroup(undo); throw; }
            finally { labyrinthFrame = previousFrame; labyrinthRoot = previousRoot; }
        }

        static void CutBoardingCuboids(Bounds opening, BoardingCoverageReport report)
        {
            Physics.SyncTransforms();
            bool Named(string n) => n == "Concrete remainder" || n == "Concrete remainder fitted return"
                || n == "Opaque breach facade right 6" || n == "Opaque breach facade right 6 fitted return";
            var candidates = labyrinthFrame.GetComponentsInChildren<Collider>(true)
                .Where(c => c.enabled && !c.isTrigger && (c is BoxCollider || c is MeshCollider) && Named(c.name)).ToArray();
            foreach (var collider in candidates)
            {
                if (!collider) continue; // An authored box can carry both collider types.
                var bounds = new Bounds(labyrinthFrame.InverseTransformPoint(collider.bounds.center), collider.bounds.size);
                var lo = Vector3.Max(bounds.min, opening.min);
                var hi = Vector3.Min(bounds.max, opening.max);
                if ((hi - lo).x < .001f || (hi - lo).y < .001f || (hi - lo).z < .001f) continue;
                if (collider.transform.childCount != 0 || collider.GetComponents<Component>().Any(c =>
                    !(c is Transform || c is MeshFilter || c is MeshRenderer || c is BoxCollider || c is MeshCollider)))
                    throw new InvalidOperationException("Boarding cut contains attached gameplay or presentation objects: " + collider.name);
                var renderer = collider.GetComponent<MeshRenderer>();
                var filter = collider.GetComponent<MeshFilter>();
                var mesh = filter ? filter.sharedMesh : null;
                if (!renderer || !mesh || mesh.triangles.Length != 36
                    || Vector3.Distance(renderer.bounds.center, collider.bounds.center) > .005f
                    || Vector3.Distance(renderer.bounds.size, collider.bounds.size) > .005f)
                    throw new InvalidOperationException("Boarding aperture requires a matching structural cuboid: " + collider.name);
                foreach (var vertex in mesh.vertices)
                {
                    var p = labyrinthFrame.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (Mathf.Min(Mathf.Abs(p.x - bounds.min.x), Mathf.Abs(p.x - bounds.max.x)) > .005f
                        || Mathf.Min(Mathf.Abs(p.y - bounds.min.y), Mathf.Abs(p.y - bounds.max.y)) > .005f
                        || Mathf.Min(Mathf.Abs(p.z - bounds.min.z), Mathf.Abs(p.z - bounds.max.z)) > .005f)
                        throw new InvalidOperationException("Non-cuboid structural mesh: " + collider.name);
                }
                var material = renderer.sharedMaterial;
                int layer = collider.gameObject.layer;
                var flags = GameObjectUtility.GetStaticEditorFlags(collider.gameObject);
                string name = collider.name.EndsWith(" fitted return") ? collider.name : collider.name + " fitted return";
                void Piece(Vector3 a, Vector3 b)
                {
                    var size = b - a;
                    if (size.x < .001f || size.y < .001f || size.z < .001f) return;
                    var box = LBox(name, (a + b) * .5f, size, material);
                    Undo.RegisterCreatedObjectUndo(box.gameObject, "Retain boarding chamber structure");
                    box.gameObject.layer = layer;
                    GameObjectUtility.SetStaticEditorFlags(box.gameObject, flags);
                    report.retainedCuboids++;
                }
                Piece(bounds.min, new(lo.x, bounds.max.y, bounds.max.z));
                Piece(new(hi.x, bounds.min.y, bounds.min.z), bounds.max);
                Piece(new(lo.x, bounds.min.y, bounds.min.z), new(hi.x, lo.y, bounds.max.z));
                Piece(new(lo.x, hi.y, bounds.min.z), new(hi.x, bounds.max.y, bounds.max.z));
                Piece(new(lo.x, lo.y, bounds.min.z), new(hi.x, hi.y, lo.z));
                Piece(new(lo.x, lo.y, hi.z), new(hi.x, hi.y, bounds.max.z));
                Undo.DestroyObjectImmediate(collider.gameObject);
                report.removedCuboids++;
            }
        }
    }
}
