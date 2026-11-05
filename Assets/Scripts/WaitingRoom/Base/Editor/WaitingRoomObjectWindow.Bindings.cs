using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    public sealed partial class WaitingRoomObjectWindow
    {
        static Transform PlacementVisual(Transform target) => target != null
            ? target.GetComponents<MonoBehaviour>().OfType<IShopPresentation>().FirstOrDefault()?.VisualRoot ?? target : null;
        internal static Vector3 PlacementPoint(Transform target) => PlacementVisual(target).position;
        static bool ConditionalContent(Transform target) => target != null && (!target.gameObject.activeSelf
            || target.TryGetComponent<ExplorationGate>(out var gate) && (gate.RevealPending || !string.IsNullOrEmpty(gate.RevealTarget))
            || target.TryGetComponent<FoundItemSpot>(out var spot) && !string.IsNullOrEmpty(SpotCondition(spot)));
        static string ContentCondition(Transform target)
        {
            string id = target.TryGetComponent<ExplorationGate>(out var gate) ? gate.RevealTarget : target.TryGetComponent<FoundItemSpot>(out var spot) ? SpotCondition(spot) : "";
            var owner = string.IsNullOrEmpty(id) ? null : target.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).OfType<IExplorationTarget>().FirstOrDefault(t => t.TargetId == id);
            return owner != null ? "개방 후 발견 · " + owner.Label : !target.gameObject.activeSelf ? "발견 전 숨김" : "조건부 발견";
        }
        internal static void EnsureOwnedDropAnchors(Transform root)
        {
            foreach (var component in root.GetComponents<MonoBehaviour>().Where(c => c != null)) {
                var so = new SerializedObject(component); var p = so.FindProperty("rewardPoint");
                if (p?.objectReferenceValue is Transform point && point != root && !point.IsChildOf(root)) {
                    var anchor = NewDropAnchor(root, point.position);
                    p.objectReferenceValue = anchor; so.ApplyModifiedProperties();
                }
            }
            var drops = root.GetComponent<ExplorationDrops>();
            if (drops == null) return;
            foreach (var entry in drops.entries ?? System.Array.Empty<ExplorationDrops.Entry>())
                if (entry?.point != null && entry.point != root && !entry.point.IsChildOf(root)) {
                    Undo.RecordObject(drops, "드롭 위치를 기물에 연결");
                    entry.point = NewDropAnchor(root, entry.point.position); EditorUtility.SetDirty(drops);
                }
        }
        static Transform NewDropAnchor(Transform root, Vector3 position)
        {
            var go = new GameObject("DropPoint");
            go.transform.SetParent(root, false); go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, "드롭 위치 연결"); return go.transform;
        }
        internal IEnumerable<Transform> OwnedCompanions(Transform root)
        {
            var seen = new HashSet<Transform> { root }; var queue = new Queue<Transform>(); queue.Enqueue(root);
            while (queue.Count > 0)
                foreach (var target in DirectCompanions(queue.Dequeue())) {
                    if (target == null || target.gameObject.scene != root.gameObject.scene || !seen.Add(target) || root.IsChildOf(target)) continue;
                    yield return target;
                    if (IsInteractionObject(target)) queue.Enqueue(target);
                }
        }
        IEnumerable<Transform> DirectCompanions(Transform root)
        {
            EnsureOwnedDropAnchors(root);
            if (root.TryGetComponent<ScheduleBoardView>(out var board)) {
                if (board.CloseUpPose != null) yield return board.CloseUpPose;
                var frame = root.parent != null ? root.parent.Find("ScheduleFrame") : null;
                if (frame != null) yield return frame;
            }
            var owner = root.GetComponents<MonoBehaviour>().OfType<IExplorationTarget>().FirstOrDefault();
            bool container = root.GetComponent<ToolTarget>() != null || root.GetComponent<BreakableWall>() != null
                || root.TryGetComponent<ExplorationGate>(out var g) && !g.IsKeyPickup;
            if (container && owner != null)
                foreach (var child in root.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationGate>(true)))
                    if (child.RevealTarget == owner.TargetId && IsInteractionObject(child.transform) && CurrentLayoutTarget(child.transform)) yield return child.transform;
            if (container && owner != null)
                foreach (var spot in root.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FoundItemSpot>(true)))
                    if (SpotCondition(spot) == owner.TargetId && CurrentLayoutTarget(spot.transform)) yield return spot.transform;
            foreach (var component in root.GetComponents<MonoBehaviour>().Where(c => c != null)) {
                var so = new SerializedObject(component);
                var reward = so.FindProperty("rewardPoint")?.objectReferenceValue as Transform;
                if (IsPositionAnchor(reward)) yield return reward;
            }
            var dropList = root.GetComponent<ExplorationDrops>();
            if (dropList != null) foreach (var entry in dropList.entries ?? System.Array.Empty<ExplorationDrops.Entry>())
                if (entry != null && IsPositionAnchor(entry.point)) yield return entry.point;
            if (root.TryGetComponent<ToolTarget>(out var tool)) {
                var list = new SerializedObject(tool).FindProperty("showOnOpen");
                for (int i = 0; list != null && i < list.arraySize; i++) {
                    var go = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (go != null && IsInteractionObject(go.transform)) yield return go.transform;
                }
            }
        }
        void ApplyPlacement(Vector3 point, Quaternion rotation)
        {
            if (selected == null) return;
            var delta = rotation * Quaternion.Inverse(selected.rotation);
            Apply(point + delta * (selected.position - PlacementPoint(selected)), rotation);
        }
        internal static void MovePlacementGroup(Transform root, IEnumerable<Transform> companions, Vector3 position, Quaternion rotation)
        {
            var roots = MoveRoots(root, companions); var origin = root.position;
            var delta = rotation * Quaternion.Inverse(root.rotation);
            Undo.RecordObjects(roots, "대기실 오브젝트·연결 위치 이동");
            foreach (var t in roots) {
                t.SetPositionAndRotation(position + delta * (t.position - origin), delta * t.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
        }
    }
}
