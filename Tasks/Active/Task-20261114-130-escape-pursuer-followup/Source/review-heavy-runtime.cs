using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;

public static class HeavyRuntimeReview
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Folder = ".harness/local/pursuer-weight-20261116/";
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static object Call(object o, string f, params object[] a) => o.GetType().GetMethod(f, Flags).Invoke(o,a);
    public static object Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "Already entering/playing";
        SessionState.SetInt("HeavyReview.PreviousPlayers", EscapeMppmReviewConfig.PursuerPlayers);
        EscapeMppmReviewConfig.PursuerPlayers = 1;
        EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect();
        return EditorApplication.ExecuteMenuItem("Tools/CurtainCall/탈출로/추격자 모드 실행");
    }
    public static object Observe() => Begin(false);
    public static object Fast() => Begin(true);
    static object Begin(bool fast)
    {
        var show = UnityEngine.Object.FindFirstObjectByType<UnderstagePursuerShowcase>();
        if (!show || !(bool)Call(show,"CanReview")) return "WAIT for local host and escape timer";
        Call(show,"Open");
        var kind = show.GetType().GetNestedType("Motion", BindingFlags.NonPublic);
        Call(show,"Select",Enum.Parse(kind,"Run"));
        var chase = (EscapeUnderstageChase)Get(show,"chase");
        var source = (AudioSource)Get(chase,"footstep");
        if (fast)
        {
            show.enabled = false; // Close() restores chase; isolate it again for this probe.
            chase.enabled = false;
            ((Behaviour)Get(show,"breach")).enabled = false;
            Call(chase,"RestPose");
            chase.GetType().GetProperty("InShowcase",Flags).SetValue(chase,true);
        }
        var frames = new List<object>(); var heard = new List<double>();
        double started = EditorApplication.timeSinceStartup;
        int lastFrame = -1; uint lastSound = (uint)Get(chase,"audibleFootfalls");
        double previousTime = Time.timeAsDouble;
        bool impactPlayed = false;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!EditorApplication.isPlaying || !show) { EditorApplication.update -= tick; return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (fast)
            {
                float dt = Mathf.Min(.2f,(float)(Time.timeAsDouble-previousTime));
                previousTime = Time.timeAsDouble;
                Call(chase,"AdvanceGait",20f*dt,0f,1f,false,dt);
                if (!impactPlayed && EditorApplication.timeSinceStartup-started > 2)
                { Call(chase,"PlayStep",.6f); impactPlayed = true; }
            }
            uint sound = (uint)Get(chase,"audibleFootfalls");
            if (sound > lastSound) heard.Add(Time.timeAsDouble);
            lastSound = sound;
            frames.Add(new { time=Time.timeAsDouble, phase=Get(chase,"stride"), contacts=Get(chase,"stepNumber"),
                audio=source.isPlaying, pitch=source.pitch, clip=source.clip.name, audible=sound });
            if (EditorApplication.timeSinceStartup - started < 8) return;
            EditorApplication.update -= tick;
            File.WriteAllText(Folder+(fast ? "runtime-fast" : "runtime-run")+".json", Newtonsoft.Json.JsonConvert.SerializeObject(new { samples=frames, footfalls=heard }, Newtonsoft.Json.Formatting.Indented));
        };
        EditorApplication.update += tick;
        return fast ? "Sampling the runtime 20m/s gait/audio path and separate landing voice for 8 seconds" : "Observing real showcase Run for 8 seconds";
    }
    public static object Stop()
    {
        int previous = SessionState.GetInt("HeavyReview.PreviousPlayers", -1);
        if (previous >= 0) { EscapeMppmReviewConfig.PursuerPlayers = previous; SessionState.EraseInt("HeavyReview.PreviousPlayers"); }
        EditorApplication.ExitPlaymode();
        return "Play review ended; original player-count preference restored";
    }
}
