using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Network.Session.Editor
{
    /// <summary>
    /// 플레이 중 한 프레임이 오래 멈추면(기본 2초 이상) 그 프레임에서 시간을 쓴 작업을 Logs/Performance/long-frame-*.json으로 남긴다.
    /// Relay는 메인 스레드가 약 10초 멈추면 연결을 끊으므로(relay-stall-disconnect 함정), 씬 전환 중 어떤 작업이 멈춤을 만드는지 찾는 용도다.
    /// 실제 접속·캠페인 흐름 그대로 플레이하면서 켜 두면 된다. 동작은 바꾸지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class LongFrameRecorder
    {
        const string EnabledKey = "CurtainCall.LongFrameRecorder.Enabled";
        const string Menu = "Tools/Curtain Call/Performance/Record Long Frames In Play";
        public const float ThresholdMs = 2000, PathMinMs = 50;
        public const int MaxReports = 20;
        static int nextFrame = -1;
        public static string LastReportPath { get; private set; }
        public static int ReportCount { get; private set; }

        static LongFrameRecorder()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && Enabled) Begin();
                if (state == PlayModeStateChange.ExitingPlayMode) Stop();
            };
            if (EditorApplication.isPlaying && Enabled) Begin();
        }

        public static bool Enabled
        {
            get => SessionState.GetBool(EnabledKey, false);
            set
            {
                SessionState.SetBool(EnabledKey, value);
                if (value && EditorApplication.isPlaying) Begin();
                if (!value) Stop();
            }
        }

        [MenuItem(Menu)] static void Toggle() => Enabled = !Enabled;
        [MenuItem(Menu, true)] static bool ToggleValidate() { UnityEditor.Menu.SetChecked(Menu, Enabled); return true; }

        static void Begin()
        {
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
            nextFrame = -1;
            ReportCount = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Debug.Log($"[LongFrame] 기록 시작: {ThresholdMs:0}ms 이상 멈춘 프레임을 Logs/Performance에 남깁니다.");
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
            if (nextFrame != -2) ProfilerDriver.enabled = false;
            nextFrame = -2;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || !Enabled) { Stop(); return; }
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            if (last < 0) return;
            // 시작 전·도메인 리로드 전 버퍼에 남은 프레임은 보지 않는다(2026-11-05 버퍼의 옛 멈춤을 연달아 기록한 문제).
            // 프로파일러 프레임을 지우면 번호가 다시 작아진다. 그때도 새 프레임부터 본다.
            if (nextFrame < 0 || nextFrame > last + 1) nextFrame = last;
            if (nextFrame < first) nextFrame = first;
            for (; nextFrame <= last; nextFrame++)
            {
                float ms;
                using (var raw = ProfilerDriver.GetRawFrameDataView(nextFrame, 0))
                {
                    if (!raw.valid) continue;
                    ms = raw.frameTimeMs;
                }
                // 한 번에 하나만 기록하고 다음 업데이트로 넘긴다. 그사이 끄기 요청이 반영된다.
                if (ms >= ThresholdMs) { Write(nextFrame, ms); nextFrame++; return; }
            }
        }

        sealed class Item { public string path; public int depth; public float totalMs, selfMs; public int calls; }

        static void Write(int frame, float frameMs)
        {
            var paths = new List<Item>();
            var self = new Dictionary<string, float>();
            using (var h = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                HierarchyFrameDataView.columnTotalTime, false))
            {
                var children = new List<int>();
                var stack = new Stack<(int id, string path, int depth)>();
                stack.Push((h.GetRootItemID(), "", 0));
                while (stack.Count > 0)
                {
                    var (id, parent, depth) = stack.Pop();
                    h.GetItemChildren(id, children);
                    foreach (int c in children.ToArray())
                    {
                        string name = h.GetItemName(c);
                        float total = h.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
                        float own = h.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
                        self[name] = (self.TryGetValue(name, out float s) ? s : 0) + own;
                        if (total < PathMinMs) continue;
                        string path = parent.Length == 0 ? name : parent + " > " + name;
                        paths.Add(new Item { path = path, depth = depth, totalMs = total, selfMs = own,
                            calls = (int)h.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnCalls) });
                        stack.Push((c, path, depth + 1));
                    }
                }
            }

            var scenes = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++) scenes.Add(SceneManager.GetSceneAt(i).name);
            var report = new
            {
                measuredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                unity = Application.unityVersion,
                note = "에디터 플레이 측정(빌드보다 느릴 수 있음). paths는 총 시간 " + PathMinMs + "ms 이상 항목만, 깊이 우선 순서.",
                frame, frameMs,
                activeScene = SceneManager.GetActiveScene().name,
                loadedScenes = scenes,
                topSelf = self.OrderByDescending(p => p.Value).Take(25).Select(p => new { name = p.Key, selfMs = p.Value }),
                paths = paths.OrderBy(p => p.path, StringComparer.Ordinal),
            };
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Performance");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            LastReportPath = Path.Combine(dir, $"long-frame-{stamp}-f{frame}.json");
            File.WriteAllText(LastReportPath, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            // 전체 프로파일(.data)은 저장하지 않는다. 버퍼 전체라 장시간 플레이에서 1개 3GB에 달했고,
            // 저장 자체가 2초 넘는 멈춤을 만들어 다시 기록되는 반복이 생겼다(2026-11-05, 247GB).
            ReportCount++;
            if (ReportCount >= MaxReports) { Enabled = false; Debug.LogWarning($"[LongFrame] {MaxReports}개를 기록해 자동으로 껐습니다."); }
            Debug.LogWarning($"[LongFrame] {frameMs:0}ms 멈춤 기록 scene={report.activeScene}: {LastReportPath}");
        }
    }
}
