using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>
    /// 추격 대기실 부하 측정(반복 가능). 플레이에서 씬을 불러오며 최장 멈춤 프레임·상위 비용·씬 규모·렌더 통계·스냅샷 크기를
    /// Logs/Performance/에 JSON으로 남기고 기준값과 비교한다. 오브젝트·작업물이 추가된 뒤 같은 메뉴로 다시 실행하면 된다.
    /// 에디터 수치는 빌드보다 느리게 나오는 경향이 있으므로 기준값은 여유 있게 잡고 추세 비교에 쓴다.
    /// </summary>
    [InitializeOnLoad]
    public static class PursuitPerformanceProbe
    {
        const string PendingKey = "CurtainCall.PursuitProbe.Pending";
        // 기준값: Relay는 메인 스레드 약 10초 정지 시 연결을 끊는다. 에디터 기준 절반 이하를 목표로 한다.
        public const float StallBudgetMs = 5000, SnapshotBudgetBytes = PursuitSnapshotCodec.UnreliableLimit;
        public const int BatchesBudget = 3000, ShadowCastersBudget = 2000;
        static readonly string[] Views = { "S02_Dormitory", "Lobby", "S09_PracticeRoom" };

        enum Step { Settle, Load, AfterLoad, Pursuit, Views, Done }
        static Step step;
        static double until;
        static AsyncOperation load;
        static double loadStart, loadEnd;
        static int view;
        static GameObject camera;
        sealed class SnapshotInfo { public int actors, legacyJsonBytes, binaryBytes; public float legacyKBps, binaryKBps; }
        sealed class ViewInfo { public string room; public bool missing; public int batches, setPassCalls, triangles, shadowCasters; public float renderMs, frameMs; }
        static readonly List<ViewInfo> viewStats = new();
        static SnapshotInfo snapshot;
        public static string LastReportPath { get; private set; }

        static PursuitPerformanceProbe()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false)) Begin();
            };
        }

        [MenuItem("Tools/Curtain Call/Performance/Measure Pursuit Waiting Room")]
        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            if (EditorApplication.isPlaying) Begin(); else EditorApplication.isPlaying = true;
        }

        static void Begin()
        {
            SessionState.SetBool(PendingKey, false);
            step = Step.Settle; until = EditorApplication.timeSinceStartup + 3; view = 0;
            viewStats.Clear(); snapshot = null;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Debug.Log("[PerfProbe] 추격 대기실 측정 시작 (약 30초)");
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            double now = EditorApplication.timeSinceStartup;
            if (now < until) return;
            switch (step) {
                case Step.Settle:
                    ProfilerDriver.ClearAllFrames(); ProfilerDriver.profileEditor = false; ProfilerDriver.enabled = true;
                    loadStart = now;
                    load = SceneManager.LoadSceneAsync(WaitingRoomScenes.PursuitName, LoadSceneMode.Single);
                    step = Step.Load; break;
                case Step.Load:
                    if (load != null && !load.isDone) return;
                    loadEnd = now; step = Step.AfterLoad; until = now + 3; break; // 첫 렌더·셰이더 컴파일 포함
                case Step.AfterLoad: {
                    ProfilerDriver.enabled = false;
                    var director = UnityEngine.Object.FindFirstObjectByType<PursuitDirector>();
                    if (director != null) { director.DebugStart(); director.DebugExpireTimer(); }
                    step = Step.Pursuit; until = now + 3; break;
                }
                case Step.Pursuit: {
                    var director = UnityEngine.Object.FindFirstObjectByType<PursuitDirector>();
                    if (director != null) {
                        var (legacy, binary) = director.MeasureSnapshot();
                        snapshot = new SnapshotInfo { actors = director.Actors.Count, legacyJsonBytes = legacy, binaryBytes = binary,
                            legacyKBps = legacy * 10 / 1024f, binaryKBps = binary * 10 / 1024f };
                    }
                    var old = Camera.main; if (old != null) { old.enabled = false; old.tag = "Untagged"; }
                    camera = new GameObject("PerfProbeCamera", typeof(Camera)) { tag = "MainCamera" };
                    camera.GetComponent<Camera>().depth = 50;
                    step = Step.Views; view = -1; until = now; break;
                }
                case Step.Views: {
                    if (view >= 0) viewStats.Add(ReadView(Views[view]));
                    view++;
                    if (view >= Views.Length) { step = Step.Done; until = now; break; }
                    if (!PlaceCamera(Views[view])) { viewStats.Add(new ViewInfo { room = Views[view], missing = true }); view++; if (view >= Views.Length) step = Step.Done; break; }
                    until = now + 1.5; break;
                }
                case Step.Done:
                    EditorApplication.update -= Tick;
                    Write();
                    EditorApplication.isPlaying = false;
                    break;
            }
        }

        static bool PlaceCamera(string roomName)
        {
            var director = UnityEngine.Object.FindFirstObjectByType<PursuitDirector>();
            var room = director != null ? director.Rooms.FirstOrDefault(r => r != null && r.name == roomName) : null;
            if (room == null || room.Area == null || camera == null) return false;
            var b = room.Area.bounds;
            float floor = Physics.Raycast(b.center, Vector3.down, out var hit, 30) ? hit.point.y : b.min.y;
            var p = new Vector3(b.center.x - b.extents.x * .5f, floor + 1.6f, b.min.z + 1.5f);
            camera.transform.position = p;
            camera.transform.LookAt(new Vector3(b.center.x, floor + 1.2f, b.center.z + b.extents.z * .5f));
            return true;
        }
        static ViewInfo ReadView(string room) => new ViewInfo {
            room = room, batches = UnityStats.batches, setPassCalls = UnityStats.setPassCalls, triangles = UnityStats.triangles,
            shadowCasters = UnityStats.shadowCasters, renderMs = UnityStats.renderTime * 1000, frameMs = UnityStats.frameTime * 1000 };

        static void Write()
        {
            // 최장 프레임과 그 프레임의 자체 시간 상위 항목.
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex, worst = -1;
            float worstMs = 0; var stalls = new List<float>();
            for (int i = first; i >= 0 && i <= last; i++) {
                using var raw = ProfilerDriver.GetRawFrameDataView(i, 0);
                if (!raw.valid) continue;
                if (raw.frameTimeMs > worstMs) { worstMs = raw.frameTimeMs; worst = i; }
                if (raw.frameTimeMs > 1000) stalls.Add(raw.frameTimeMs);
            }
            var top = new List<object>();
            if (worst >= 0) {
                using var h = ProfilerDriver.GetHierarchyFrameDataView(worst, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);
                var items = new List<(string name, float self, float total)>();
                var stack = new Stack<int>(); stack.Push(h.GetRootItemID()); var children = new List<int>();
                while (stack.Count > 0) {
                    int id = stack.Pop();
                    h.GetItemChildren(id, children);
                    foreach (var c in children) {
                        items.Add((h.GetItemName(c), h.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime), h.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime)));
                        stack.Push(c);
                    }
                }
                top.AddRange(items.GroupBy(i => i.name).Select(g => new { name = g.Key, selfMs = g.Sum(x => x.self), totalMs = g.Max(x => x.total) })
                    .OrderByDescending(x => x.selfMs).Take(15));
            }
            var scene = SceneStats();
            var budget = new List<string>();
            if (worstMs > StallBudgetMs) budget.Add($"WARN 최장 멈춤 {worstMs:0}ms > {StallBudgetMs:0}ms");
            if (snapshot != null && snapshot.binaryBytes > SnapshotBudgetBytes) budget.Add($"WARN 스냅샷 {snapshot.binaryBytes}B > {SnapshotBudgetBytes}B");
            foreach (var v in viewStats) {
                if (v.missing) continue;
                if (v.batches > BatchesBudget) budget.Add($"WARN {v.room} batches {v.batches} > {BatchesBudget}");
                if (v.shadowCasters > ShadowCastersBudget) budget.Add($"WARN {v.room} shadowCasters {v.shadowCasters} > {ShadowCastersBudget}");
            }
            var report = new {
                measuredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), unity = Application.unityVersion, note = "에디터 플레이 측정(빌드보다 느림). 추세 비교용.",
                load = new { totalMs = (loadEnd - loadStart) * 1000, worstFrameMs = worstMs, framesOver1s = stalls.Count, stallsMs = stalls, topSelfInWorstFrame = top },
                scene, snapshot, views = viewStats, budget = budget.Count == 0 ? new List<string> { "PASS" } : budget };
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Performance");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            LastReportPath = Path.Combine(dir, $"pursuit-waitingroom-{stamp}.json");
            File.WriteAllText(LastReportPath, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            ProfilerDriver.SaveProfile(Path.Combine(dir, $"pursuit-waitingroom-{stamp}.data"));
            Debug.Log($"[PerfProbe] 완료: {LastReportPath} · {string.Join(" / ", report.budget)}");
        }

        static object SceneStats()
        {
            var scene = SceneManager.GetSceneByName(WaitingRoomScenes.PursuitName);
            var roots = scene.IsValid() ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
            T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            var lights = All<Light>();
            var renderers = All<MeshRenderer>();
            return new {
                gameObjects = All<Transform>().Length, meshRenderers = renderers.Length, activeRenderers = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy),
                staticBatched = renderers.Count(r => r.isPartOfStaticBatch), lodGroups = All<LODGroup>().Length, colliders = All<Collider>().Length,
                monoBehaviours = All<MonoBehaviour>().Length, lights = lights.Length, realtimeLights = lights.Count(l => l.lightmapBakeType != LightmapBakeType.Baked),
                shadowLights = lights.Count(l => l.shadows != LightShadows.None && l.isActiveAndEnabled), pursuers = All<PursuitActor>().Length };
        }
    }
}
