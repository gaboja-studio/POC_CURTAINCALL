using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>현재 충돌체로 만든 임시 경로망. 문 개방은 소스 제외로 모의하고 씬/상태는 바꾸지 않는다.</summary>
    public static class StructureARouteAudit
    {
        sealed class Point { public float x, y, z; public Point(float[] a) { x = a[0]; y = a[1]; z = a[2]; } }
        sealed class Row
        {
            public string id, path;
            public float x, y, z;
            public bool closed, opened;
            public float[] partialEnd, nearbyFloor;
            public float[][] closedCorners, openedCorners;
            public Point[] closedWalk, openedWalk;
            public float[] closedApproach, openedApproach;
        }
        static readonly List<Row> rows = new();
        static List<NavMeshBuildSource> closedSources;
        static HashSet<Component> removable;
        static NavMeshBuildSettings settings;
        static NavMeshData data;
        static NavMeshDataInstance instance;
        static AsyncOperation baking;
        static Transform layout;
        static Vector3 start;
        static int phase;
        static bool originalDirty;
        static readonly Bounds Volume = new(new Vector3(-10, -1, 80), new Vector3(145, 45, 140));
        public static string Status { get; private set; } = "idle";
        public const string ReportPath = ".harness/local/stage8-connectivity.json";

        [MenuItem("CurtainCall/Waiting Room/Audit Structure A Exploration Routes")]
        public static void MenuAudit() => Debug.Log(Begin());
        public static string Begin() => BeginCore(false);
        public static string BeginRuntime() => BeginCore(true);
        static string BeginCore(bool runtime)
        {
            if (baking != null) return Status;
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying != runtime || scene.path != "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity" || (!runtime && scene.isDirty))
                throw new InvalidOperationException("Open saved WaitingRoomBase in Edit Mode.");
            originalDirty = scene.isDirty;
            layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            start = runtime && CurtainCall.Network.PlayerSync.NetworkPlayer.Local != null
                ? CurtainCall.Network.PlayerSync.NetworkPlayer.Local.transform.position
                : scene.GetRootGameObjects().Single(g => g.name == "PlaySetup").GetComponentInChildren<CharacterController>(true).transform.position;
            rows.Clear(); removable = new HashSet<Component>();
            foreach (var component in layout.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is IExplorationTarget target && (target.TargetId.StartsWith("structure-a:") || target.TargetId == "restroom-breach"))
                {
                    var p = component.transform.position;
                    rows.Add(new Row { id = target.TargetId, path = AnimationUtility.CalculateTransformPath(component.transform, layout), x = p.x, y = p.y, z = p.z });
                    var so = new SerializedObject(component);
                    var blocker = so.FindProperty("blocker")?.objectReferenceValue as Collider;
                    if (blocker != null) removable.Add(blocker);
                    var hide = so.FindProperty("hideOnOpen");
                    if (hide != null) for (int i = 0; i < hide.arraySize; i++)
                        if (hide.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go)
                            foreach (var c in go.GetComponentsInChildren<Collider>(true)) removable.Add(c);
                }
            }
            foreach (var spot in layout.GetComponentsInChildren<FoundItemSpot>(true))
            {
                var p = spot.transform.position;
                rows.Add(new Row { id = "spot:" + spot.SpotId, path = AnimationUtility.CalculateTransformPath(spot.transform, layout), x = p.x, y = p.y, z = p.z });
            }
            closedSources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(layout, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), closedSources);
            closedSources.RemoveAll(s => s.component is Collider c && (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy));
            // Authoring shutters are stored closed; normal daytime traversal has those shutters raised.
            var daytimeShutters = new HashSet<Component>();
            foreach (var door in layout.GetComponentsInChildren<NightDoorLock>(true))
            {
                var shutter = new SerializedObject(door).FindProperty("shutter").objectReferenceValue as Transform;
                if (shutter != null) foreach (var collider in shutter.GetComponentsInChildren<Collider>(true)) daytimeShutters.Add(collider);
            }
            closedSources.RemoveAll(s => daytimeShutters.Contains(s.component));
            settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = CurtainCall.Settings.GameSettings.Base.Character.CapsuleRadius;
            settings.agentHeight = CurtainCall.Settings.GameSettings.Base.Character.CapsuleHeight;
            settings.agentClimb = .3f; settings.agentSlope = 45;
            settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            settings.overrideTileSize = true; settings.tileSize = 256;
            phase = 0; StartBake(); EditorApplication.update += Pump;
            return Status;
        }
        static void StartBake()
        {
            data = new NavMeshData(settings.agentTypeID) { hideFlags = HideFlags.HideAndDontSave };
            var sources = phase == 0 ? closedSources : closedSources.Where(s => !removable.Contains(s.component)).ToList();
            baking = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, Volume);
            Status = phase == 0 ? "baking-closed" : "baking-opened";
        }
        static void Pump()
        {
            if (baking == null || !baking.isDone) return;
            try
            {
                instance = NavMesh.AddNavMeshData(data);
                foreach (var row in rows)
                {
                    bool connected = Connected(row, out var point, out var corners);
                    if (phase == 0) { row.closed = connected; row.closedApproach = new[] { point.x, point.y, point.z }; row.closedCorners = corners; row.closedWalk = corners?.Select(c => new Point(c)).ToArray(); }
                    else { row.opened = connected; row.openedApproach = new[] { point.x, point.y, point.z }; row.openedCorners = corners; row.openedWalk = corners?.Select(c => new Point(c)).ToArray(); }
                }
                instance.Remove(); Object.DestroyImmediate(data); data = null; baking = null;
                if (phase++ == 0) { StartBake(); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, JsonConvert.SerializeObject(new
                {
                    status = "completed", note = "Daytime shutters raised by source exclusion. Temporary collider NavMesh connectivity; puzzle-open geometry is simulated by omitted sources, not actual interaction or walked play.",
                    agentRadius = settings.agentRadius, agentHeight = settings.agentHeight, agentClimb = settings.agentClimb,
                    start = new[] { start.x, start.y, start.z },
                    sceneDirtyBefore = originalDirty, sceneDirtyAfter = SceneManager.GetActiveScene().isDirty,
                    total = rows.Count, closedReachable = rows.Count(r => r.closed), openedReachable = rows.Count(r => r.opened), targets = rows
                }, Formatting.Indented));
                Status = "completed"; EditorApplication.update -= Pump;
            }
            catch (Exception error)
            {
                if (instance.valid) instance.Remove(); if (data != null) Object.DestroyImmediate(data);
                baking = null; Status = "failed: " + error.Message; EditorApplication.update -= Pump; Debug.LogException(error);
            }
        }
        static bool Connected(Row row, out Vector3 point, out float[][] corners)
        {
            point = default; corners = null; float best = float.MaxValue;
            if (!NavMesh.SamplePosition(start, out var origin, 2f, NavMesh.AllAreas)) return false;
            var target = new Vector3(row.x, row.y, row.z);
            // Sample around the target on its floor, not at a nearest point on a different storey.
            for (int ring = 1; ring <= 3; ring++) for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                var at = target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (.45f * ring);
                if (!NavMesh.SamplePosition(at, out var hit, 1.6f, NavMesh.AllAreas)) continue;
                if (hit.position.y > target.y + .25f || target.y - hit.position.y > 1.8f || Vector2.Distance(new Vector2(hit.position.x, hit.position.z), new Vector2(target.x, target.z)) > 1.55f) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(origin.position, hit.position, NavMesh.AllAreas, path)) continue;
                if (path.corners.Length > 0 && phase == 1)
                {
                    var last = path.corners[path.corners.Length - 1]; float d = Vector3.Distance(last, target);
                    if (d < best) { best = d; row.partialEnd = new[] { last.x, last.y, last.z }; }
                }
                if (path.status != NavMeshPathStatus.PathComplete) continue;
                point = hit.position; corners = path.corners.Select(c => new[] { c.x, c.y, c.z }).ToArray(); return true;
            }
            if (phase == 1 && NavMesh.SamplePosition(target, out var nearby, 5f, NavMesh.AllAreas))
                row.nearbyFloor = new[] { nearby.position.x, nearby.position.y, nearby.position.z };
            return false;
        }
    }
}
