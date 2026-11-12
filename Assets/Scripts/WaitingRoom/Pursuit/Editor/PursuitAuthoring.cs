using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitAuthoring
    {
        public static string ScenePath {
            get {
                var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (CurtainCall.Settings.WaitingRoomScenes.HasPursuit(active.name)) return active.path;
                var selected = CurtainCall.Settings.GameSettings.Base.SpaceTransition.WaitingRoomVariant;
                return CurtainCall.Settings.WaitingRoomScenes.Path(selected == CurtainCall.Settings.WaitingRoomSceneVariant.Base
                    ? CurtainCall.Settings.WaitingRoomSceneVariant.Integrated : selected);
            }
        }
        public static Bounds Bounds(PursuitRoom room) => room.Area != null ? room.Area.bounds : new Bounds(room.transform.position, new Vector3(12, 4, 12));
        public static float FloorReference(PursuitRoom room)
        {
            // Placement Y may be on a bed/table. It must never redefine the room's storey,
            // otherwise moving the first mannequin removes its whole room from the floor filter.
            var heights = (room.PatrolPoints ?? Array.Empty<Transform>()).Where(p => p != null).Select(p => p.position.y).OrderBy(y => y).ToArray();
            if (heights.Length == 0) heights = (room.SpawnPoints ?? Array.Empty<Transform>()).Where(p => p != null).Select(p => p.position.y).OrderBy(y => y).ToArray();
            return heights.Length > 0 ? heights[heights.Length / 2] : Bounds(room).min.y;
        }
        public static bool Ground(PursuitRoom room, Vector3 input, out Vector3 point)
        {
            Physics.SyncTransforms();
            var bounds = Bounds(room);
            // Detection volumes extend below floors (Lobby min=-3, floor=0).
            // Probe this storey, not the bottom of the detection volume or a lower floor.
            float floor = FloorReference(room);
            float top = Mathf.Min(bounds.max.y - .1f, floor + 2.4f);
            var hits = Physics.RaycastAll(new Vector3(input.x, top, input.z), Vector3.down,
                Mathf.Max(.1f, top - floor + .75f), ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits.OrderBy(h => h.distance)) {
                if (hit.normal.y < .65f || hit.collider.GetComponentInParent<PursuitActor>() != null ||
                    hit.collider.GetComponentInParent<PursuitPlacement>() != null) continue;
                point = new Vector3(input.x, hit.point.y + .015f, input.z); return true;
            }
            // Navigation is a fallback only; preserve the XZ selected by the designer.
            var probe = new Vector3(input.x, floor + .25f, input.z);
            if (NavMesh.SamplePosition(probe, out var nav, 2, NavMesh.AllAreas) &&
                Mathf.Abs(nav.position.y - floor) <= .75f &&
                Vector2.Distance(new Vector2(nav.position.x, nav.position.z), new Vector2(input.x, input.z)) < .25f) {
                point = new Vector3(input.x, nav.position.y + .015f, input.z); return true;
            }
            point = input; return false;
        }
        public static int Migrate(PursuitDirector director)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before editing placements.");
            int variation = 0, count = 0;
            Undo.SetCurrentGroupName("추격 배치 전환");
            foreach (var room in director.Rooms) {
                if (room == null) continue;
                if (room.UsePlacements) { variation += room.Placements.Length; continue; }
                Undo.RecordObject(room, "추격 배치 전환");
                room.UsePlacements = true;
                if (room.SpawnPoints == null || room.SpawnPoints.Length == 0 || director.Mannequins == null || director.Mannequins.Length == 0) continue;
                for (int n = 0; n < Mathf.Clamp(room.Count, 0, 32); n++) {
                    var prefab = director.Mannequins[variation++ % director.Mannequins.Length];
                    var spawn = room.SpawnPoints[n % room.SpawnPoints.Length];
                    if (prefab == null || spawn == null) continue;
                    var placement = Add(room, prefab, spawn.position, false);
                    placement.transform.rotation = spawn.rotation;
                    foreach (var old in room.PatrolPoints ?? Array.Empty<Transform>())
                        if (old != null) AddPoint(placement, old.position);
                    count++;
                }
                Dirty(room);
            }
            return count;
        }
        public static PursuitPlacement Add(PursuitRoom room, GameObject prefab, Vector3 position, bool snap = true)
        {
            if (EditorApplication.isPlaying || room == null || prefab == null || prefab.GetComponent<PursuitActor>() == null) return null;
            if (snap && !Ground(room, position, out position)) return null;
            var go = new GameObject("Mannequin_" + prefab.name.Replace("Pursuit_", ""));
            Undo.RegisterCreatedObjectUndo(go, "마네킹 배치");
            go.transform.SetParent(PlacementParent(room), false); go.transform.position = position;
            var item = Undo.AddComponent<PursuitPlacement>(go); item.Prefab = prefab;
            Undo.RecordObject(room, "마네킹 배치"); room.UsePlacements = true;
            room.Placements = (room.Placements ?? Array.Empty<PursuitPlacement>()).Where(p => p != null).Append(item).ToArray();
            Dirty(room); QueuePrefabApply(go.transform); return item;
        }
        public static Transform AddPoint(PursuitPlacement item, Vector3 position)
        {
            var go = new GameObject("WatchPoint"); Undo.RegisterCreatedObjectUndo(go, "감시 지점 추가");
            go.transform.SetParent(item.transform, false); go.transform.position = position;
            Undo.RecordObject(item, "감시 지점 추가"); item.UseRoomPatrol = false;
            item.PatrolPoints = (item.PatrolPoints ?? Array.Empty<Transform>()).Where(p => p != null).Append(go.transform).ToArray();
            Dirty(item); return go.transform;
        }
        public static void Move(PursuitPlacement item, Vector3 position, Quaternion rotation)
            => PursuitPlacementEditing.Move(item, position, rotation);
        public static void Remove(PursuitRoom room, PursuitPlacement item)
        {
            Undo.RecordObject(room, "마네킹 제거"); room.Placements = room.Placements.Where(p => p != item).ToArray();
            var parent = item.transform.parent;
            Undo.DestroyObjectImmediate(item.gameObject); Dirty(room); QueuePrefabApply(parent);
        }
        public static void Reorder(PursuitPlacement item, int from, int to)
        {
            if (from < 0 || to < 0 || from >= item.PatrolPoints.Length || to >= item.PatrolPoints.Length) return;
            Undo.RecordObject(item, "순찰 순서 변경");
            var list = item.PatrolPoints.ToList(); var point = list[from]; list.RemoveAt(from); list.Insert(to, point);
            item.PatrolPoints = list.ToArray(); Dirty(item);
        }
        public static void RemovePoint(PursuitPlacement item, int index)
        {
            Undo.RecordObject(item, "감시 지점 제거"); var point = item.PatrolPoints[index];
            item.PatrolPoints = item.PatrolPoints.Where((p, i) => i != index).ToArray();
            if (point != null && point.IsChildOf(item.transform)) Undo.DestroyObjectImmediate(point.gameObject);
            Dirty(item);
        }
        public static void Dirty(Component target)
        {
            EditorUtility.SetDirty(target); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            QueuePrefabApply(target.transform);
        }

        // 대기실은 마네킹 배치를 방 공간 프리팹 안에 둔다. 방 프리팹이 없는 통로·로비 영역은 공용 연결 프리팹을 쓴다.
        public const string PlacementGroup = "PursuitPlacements";
        public static Transform PlacementParent(PursuitRoom room)
        {
            var layout = room.gameObject.scene.GetRootGameObjects().FirstOrDefault(g => g.name == "WaitingRoom_LayoutA");
            if (layout == null) return room.transform;
            var space = layout.transform.Find(room.name == "Lobby" ? "S01_Lobby" : room.name);
            if (space == null || !PrefabUtility.IsAnyPrefabInstanceRoot(space.gameObject)) space = layout.transform.Find("FacilityConnections");
            if (space == null || !PrefabUtility.IsAnyPrefabInstanceRoot(space.gameObject)) return room.transform;
            var group = space.Find(PlacementGroup);
            if (group != null) return group;
            group = new GameObject(PlacementGroup).transform;
            group.SetParent(space, false);
            ApplyToPrefab(group);
            return space.Find(PlacementGroup);
        }

        // 배치 창은 드래그 중에도 Dirty를 부르므로 프리팹 저장은 편집이 멈춘 뒤 한 번에 한다.
        static readonly System.Collections.Generic.HashSet<Transform> pendingApply = new();
        static double applyDue;
        static void QueuePrefabApply(Transform scope)
        {
            if (scope == null || EditorApplication.isPlayingOrWillChangePlaymode || PrefabInstanceRoot(scope) == null) return;
            if (pendingApply.Count == 0) EditorApplication.update += FlushPrefabApply;
            pendingApply.Add(scope); applyDue = EditorApplication.timeSinceStartup + .4;
        }
        static void FlushPrefabApply()
        {
            if (EditorApplication.timeSinceStartup < applyDue || GUIUtility.hotControl != 0) return;
            EditorApplication.update -= FlushPrefabApply;
            var scopes = pendingApply.Where(s => s != null).ToArray(); pendingApply.Clear();
            foreach (var scope in scopes) ApplyToPrefab(scope);
        }
        static GameObject PrefabInstanceRoot(Transform scope)
        {
            for (var t = scope; t != null; t = t.parent)
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) return PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
            return null;
        }
        public static void ApplyToPrefab(Transform scope)
        {
            var root = PrefabInstanceRoot(scope);
            if (root == null) return;
            var path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root));
            bool Inside(UnityEngine.Object o) => o is Component c ? c != null && c.transform.IsChildOf(scope) : o is GameObject g && g != null && g.transform.IsChildOf(scope);
            foreach (var added in PrefabUtility.GetAddedGameObjects(root).Where(a => Inside(a.instanceGameObject))) added.Apply(path, InteractionMode.AutomatedAction);
            foreach (var removed in PrefabUtility.GetRemovedGameObjects(root).Where(r => Inside(r.parentOfRemovedGameObjectInInstance))) removed.Apply(path, InteractionMode.AutomatedAction);
            foreach (var added in PrefabUtility.GetAddedComponents(root).Where(a => Inside(a.instanceComponent))) added.Apply(path, InteractionMode.AutomatedAction);
            foreach (var change in PrefabUtility.GetObjectOverrides(root).Where(o => Inside(o.instanceObject))) change.Apply(path, InteractionMode.AutomatedAction);
        }
    }
}
