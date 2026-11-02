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
        public const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Pursuit/WaitingRoomPursuit.unity";
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
            go.transform.SetParent(room.transform, false); go.transform.position = position;
            var item = Undo.AddComponent<PursuitPlacement>(go); item.Prefab = prefab;
            Undo.RecordObject(room, "마네킹 배치"); room.UsePlacements = true;
            room.Placements = (room.Placements ?? Array.Empty<PursuitPlacement>()).Where(p => p != null).Append(item).ToArray();
            Dirty(room); return item;
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
        {
            // Start marker movement must not move its patrol route with it.
            var points = item.PatrolPoints ?? Array.Empty<Transform>();
            var valid = points.Where(p => p != null && p.IsChildOf(item.transform)).ToArray();
            var positions = valid.Select(p => p.position).ToArray();
            Undo.RecordObjects(valid.Cast<UnityEngine.Object>().Append(item.transform).ToArray(), "마네킹 이동/회전");
            item.transform.SetPositionAndRotation(position, rotation);
            for (int i = 0; i < valid.Length; i++) valid[i].position = positions[i];
            Dirty(item);
        }
        public static void Remove(PursuitRoom room, PursuitPlacement item)
        {
            Undo.RecordObject(room, "마네킹 제거"); room.Placements = room.Placements.Where(p => p != item).ToArray();
            Undo.DestroyObjectImmediate(item.gameObject); Dirty(room);
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
        }
    }
}
