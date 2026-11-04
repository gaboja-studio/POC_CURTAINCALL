using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>Explicit A/B only. Uses a temporary pipeline copy and restores cameras/settings on every exit.</summary>
    [InitializeOnLoad]
    public static class WaitingRoomRenderComparison
    {
        static readonly string[] Rooms = {"S02_Dormitory", "Lobby", "S09_PracticeRoom"};
        [Serializable] public struct Frame { public string room; public float ms; public int batches, setPass, triangles, shadows; }
        [Serializable] public class Report { public string label, scene; public bool gpuResident, gpuResidentActive, complete; public Frame[] frames; public long allocatedMemory; }
        static readonly List<Frame> frames = new();
        static readonly List<(Camera camera, bool enabled)> cameras = new();
        static UniversalRenderPipelineAsset temporary;
        static RenderPipelineAsset previousQuality;
        static BatchRendererGroupStrippingMode previousStripping;
        static Camera capture;
        static string label;
        static bool enabled;
        static bool previousBackground;
        static int previousFrameRate;
        static int room, lastFrame;
        static double warmUntil, sampleUntil;
        public static bool IsRunning => temporary != null;
        public static string LastReportPath { get; private set; }

        static WaitingRoomRenderComparison()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) Restore(); };
        }

        public static bool Begin(string name, bool gpuResident)
        {
            try { return BeginCore(name, gpuResident); }
            catch { Restore(); throw; }
        }

        static bool BeginCore(string name, bool gpuResident)
        {
            if (!EditorApplication.isPlaying || IsRunning || string.IsNullOrEmpty(name)
                || name.Any(c => !char.IsLetterOrDigit(c) && c != '-') || Camera.main == null
                || GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset original
                || PursuitDirector.Current == null || !PursuitDirector.Current.IsInitialized) return false;
            label = name; enabled = gpuResident; frames.Clear(); cameras.Clear(); lastFrame = -1; room = 0;
            previousQuality = QualitySettings.renderPipeline;
            previousStripping = EditorGraphicsSettings.batchRendererGroupShaderStrippingMode;
            previousBackground = Application.runInBackground; previousFrameRate = Application.targetFrameRate;
            Application.runInBackground = true; Application.targetFrameRate = -1;
            temporary = UnityEngine.Object.Instantiate(original); temporary.hideFlags = HideFlags.DontSave;
            // The drawer silently falls back without this prerequisite. Use the same
            // prerequisite in A and B, and restore the project setting on every exit.
            SetStripping(BatchRendererGroupStrippingMode.KeepAll);
            temporary.gpuResidentDrawerMode = gpuResident ? GPUResidentDrawerMode.InstancedDrawing : GPUResidentDrawerMode.Disabled;
            capture = new GameObject("Waiting room render comparison", typeof(Camera)).GetComponent<Camera>();
            capture.gameObject.hideFlags = HideFlags.DontSave; capture.CopyFrom(Camera.main);
            if (Camera.main.TryGetComponent(out UniversalAdditionalCameraData data))
                EditorUtility.CopySerialized(data, capture.GetUniversalAdditionalCameraData());
            foreach (var camera in Camera.allCameras) {
                if (camera == capture) continue;
                cameras.Add((camera, camera.enabled)); camera.enabled = false;
            }
            QualitySettings.renderPipeline = temporary;
            if (!PlaceRoom()) { Restore(); return false; }
            warmUntil = Time.realtimeSinceStartupAsDouble + 5; sampleUntil = 0;
            EditorApplication.update += Tick;
            return true;
        }

        static bool PlaceRoom()
        {
            var target = UnityEngine.Object.FindObjectsByType<PursuitRoom>(FindObjectsSortMode.None)
                .FirstOrDefault(r => r.name == Rooms[room]);
            if (target == null || target.Area == null) return false;
            var b = target.Area.bounds;
            float floor = Physics.Raycast(b.center, Vector3.down, out var hit, 30) ? hit.point.y : b.min.y;
            capture.transform.position = new Vector3(b.center.x - b.extents.x * .6f, floor + 1.6f, b.center.z - b.extents.z * .6f);
            capture.transform.LookAt(new Vector3(b.center.x, floor + 1.2f, b.center.z + b.extents.z * .5f));
            return true;
        }

        static void Tick()
        {
            try { Sample(); }
            catch (Exception exception) { Restore(); Debug.LogException(exception); }
        }

        static void Sample()
        {
            if (!EditorApplication.isPlaying || capture == null) { Restore(); return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < warmUntil) return;
            // Shader preparation may outlast the wall-clock warmup. Start the sample
            // window after a rendered frame resumes instead of silently skipping a room.
            if (sampleUntil == 0) { sampleUntil = now + 3; return; }
            if (now < sampleUntil) {
                frames.Add(new Frame {room = Rooms[room], ms = Time.unscaledDeltaTime * 1000,
                    batches = UnityStats.batches, setPass = UnityStats.setPassCalls,
                    triangles = UnityStats.triangles, shadows = UnityStats.shadowCasters});
                return;
            }
            if (++room < Rooms.Length && PlaceRoom()) { warmUntil = now + 2; sampleUntil = 0; return; }
            var report = new Report {label = label, scene = PursuitDirector.Current.gameObject.scene.name,
                gpuResident = enabled, frames = frames.ToArray(), allocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()};
            report.gpuResidentActive = typeof(GPUResidentDrawer).GetProperty("instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) != null;
            report.complete = Rooms.All(name => frames.Any(frame => frame.room == name)) && (!enabled || report.gpuResidentActive);
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Performance");
            Directory.CreateDirectory(directory);
            LastReportPath = Path.Combine(directory, "render-" + label + ".json");
            File.WriteAllText(LastReportPath, JsonUtility.ToJson(report));
            Restore();
        }

        static void Restore()
        {
            EditorApplication.update -= Tick;
            if (temporary != null) {
                QualitySettings.renderPipeline = previousQuality;
                SetStripping(previousStripping);
                Application.runInBackground = previousBackground; Application.targetFrameRate = previousFrameRate;
            }
            foreach (var item in cameras) if (item.camera != null) item.camera.enabled = item.enabled;
            cameras.Clear();
            if (capture != null) UnityEngine.Object.DestroyImmediate(capture.gameObject);
            if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
            temporary = null; capture = null;
        }

        static void SetStripping(BatchRendererGroupStrippingMode mode)
        {
            using var settings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var property = settings.FindProperty("m_BrgStripping");
            if (property == null) throw new InvalidOperationException("BatchRendererGroup setting is unavailable.");
            property.intValue = (int)mode;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
