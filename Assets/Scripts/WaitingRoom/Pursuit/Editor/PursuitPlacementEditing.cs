using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    // The Transform inspector and Scene handles bypass PursuitAuthoring.Move.
    [InitializeOnLoad]
    public static class PursuitPlacementEditing
    {
        static bool handling;
        static PursuitPlacementEditing() => Undo.postprocessModifications += PreserveRoutes;

        public static void Move(PursuitPlacement item, Vector3 position, Quaternion rotation)
        {
            Undo.FlushUndoRecordObjects();
            handling = true;
            try {
                var points = (item.PatrolPoints ?? Array.Empty<Transform>()).Where(p => p != null && p.IsChildOf(item.transform)).Distinct().ToArray();
                var positions = points.Select(p => p.position).ToArray();
                bool detach = item.transform.position != position && IsDistant(item, position);
                Undo.RecordObject(item.transform, "마네킹 이동/회전");
                if (!detach) Undo.RecordObjects(points, "순찰 위치 유지");
                item.transform.SetPositionAndRotation(position, rotation);
                if (detach) ClearRoute(item);
                else for (int i = 0; i < points.Length; i++) points[i].position = positions[i];
                PursuitAuthoring.Dirty(item);
                Undo.FlushUndoRecordObjects();
            } finally { handling = false; }
        }
        static UndoPropertyModification[] PreserveRoutes(UndoPropertyModification[] changes)
        {
            if (handling || EditorApplication.isPlayingOrWillChangePlaymode) return changes;
            handling = true;
            try {
                var targets = changes.Select(c => c.currentValue.target).OfType<Transform>()
                    .Select(t => t.GetComponent<PursuitPlacement>()).Where(p => p != null).Distinct();
                foreach (var item in targets) {
                    var t = item.transform;
                    Vector3 oldPosition = t.localPosition, oldScale = t.localScale;
                    Quaternion oldRotation = t.localRotation;
                    bool changed = false, translated = false;
                    foreach (var change in changes) {
                        var before = change.previousValue;
                        if (before.target != t || !float.TryParse(before.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) continue;
                        string path = before.propertyPath;
                        int axis = path.EndsWith(".x") ? 0 : path.EndsWith(".y") ? 1 : path.EndsWith(".z") ? 2 : path.EndsWith(".w") ? 3 : -1;
                        if (axis < 0) continue;
                        if (path.StartsWith("m_LocalPosition.") && axis < 3) { oldPosition[axis] = value; changed = translated = true; }
                        else if (path.StartsWith("m_LocalRotation.")) { oldRotation[axis] = value; changed = true; }
                        else if (path.StartsWith("m_LocalScale.") && axis < 3) { oldScale[axis] = value; changed = true; }
                    }
                    if (!changed) continue;
                    var oldWorld = (t.parent != null ? t.parent.localToWorldMatrix : Matrix4x4.identity) * Matrix4x4.TRS(oldPosition, oldRotation, oldScale);
                    var correction = oldWorld * t.worldToLocalMatrix;
                    var points = (item.PatrolPoints ?? Array.Empty<Transform>()).Where(p => p != null && p.IsChildOf(t)).Distinct().ToArray();
                    var locals = points.Select(p => p.localPosition).ToArray();
                    var worlds = points.Select(p => correction.MultiplyPoint3x4(p.position)).ToArray();
                    // Evaluate against the old world route, then restore the unmodified local poses
                    // before recording undo or deleting objects. Deleted transforms need no pose edit.
                    for (int i = 0; i < points.Length; i++) points[i].position = worlds[i];
                    bool detach = translated && IsDistant(item, t.position);
                    for (int i = 0; i < points.Length; i++) points[i].localPosition = locals[i];
                    if (detach) {
                        // DestroyObjectImmediate flushes the pending Transform record inside this
                        // callback. Preserve its original pose explicitly before deleting children.
                        var position = t.localPosition; var rotation = t.localRotation; var scale = t.localScale;
                        t.localPosition = oldPosition; t.localRotation = oldRotation; t.localScale = oldScale;
                        Undo.RegisterCompleteObjectUndo(t, "마네킹 이동 및 먼 순찰 경로 해제");
                        t.localPosition = position; t.localRotation = rotation; t.localScale = scale;
                        ClearRoute(item);
                    }
                    else foreach (var point in points) Undo.RegisterCompleteObjectUndo(point, "마네킹 이동 시 순찰 위치 유지");
                    if (!detach) for (int i = 0; i < points.Length; i++) {
                        points[i].position = worlds[i]; PursuitAuthoring.Dirty(points[i]);
                    }
                }
            } finally { handling = false; }
            return changes;
        }
        static bool IsDistant(PursuitPlacement item, Vector3 position)
        {
            var route = item.Route(item.OwnerRoom());
            if (item.RouteDetachDistance <= 0 || route == null || !route.Any(p => p != null)) return false;
            var closest = PursuitActor.ClosestPatrolPosition(position, route, position);
            return Vector3.Distance(position, closest) > item.RouteDetachDistance;
        }
        public static bool ClearDistantRoute(PursuitPlacement item)
        {
            if (!IsDistant(item, item.transform.position)) return false;
            ClearRoute(item); return true;
        }
        static void ClearRoute(PursuitPlacement item)
        {
            Undo.RegisterCompleteObjectUndo(item, "먼 순찰 경로 해제");
            var owned = (item.PatrolPoints ?? Array.Empty<Transform>()).Where(p => p != null && p.IsChildOf(item.transform)).Distinct().ToArray();
            item.PatrolPoints = Array.Empty<Transform>(); item.UseRoomPatrol = false;
            foreach (var point in owned) if (point != null) Undo.DestroyObjectImmediate(point.gameObject);
            PursuitAuthoring.Dirty(item);
            PursuitDesignerWindow.NotifyRouteRemoved(item);
        }
    }
}