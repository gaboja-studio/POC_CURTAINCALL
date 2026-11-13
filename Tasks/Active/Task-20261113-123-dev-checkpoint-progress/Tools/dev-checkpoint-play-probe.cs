using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DevCheckpointPlayProbe
{
    const string Early = "탈출로 · 후반 그네 앞";
    const string Late = "탈출로 · 마지막 문 앞";
    const string Skip = "탈출로 · 추격 / 마지막 접이교 우회 뒤편";
    const string PerformanceEarly = "대포 1 탑승 위치";
    const string PerformanceLate = "대포 4 탑승 위치";
    static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    static string Last(NetworkPlayer player) => (string)typeof(DevCheats).GetMethod("LastCheckpointLabel", Flags).Invoke(null, new object[] { player });

    static List<DevCheats.TeleportPoint> Points()
    {
        var points = new List<DevCheats.TeleportPoint>();
        DevCheats.CollectTeleportPoints(NetworkPlayer.Local, points);
        return points;
    }

    static DevCheats.TeleportPoint Point(string label) => Points().Single(p => p.Label == label);

    public static async Task<object> Launch(string phase, int count)
    {
        var session = NetworkSessionManager.Instance;
        using (session.RequestMinimumStartPlayers(count))
        {
            await Wait(() => Actors().Length == count, "Player slots", 15);
            var start = phase == "Escape"
                ? new RunStartPoint(1, RunStartPhase.Escape, 0, "")
                : new RunStartPoint(1, RunStartPhase.Performance, 0, CampaignSnapshot.HumanCannonId, 2);
            var launcher = DevStartLauncher.EnsureExists();
            if (phase != "Escape") launcher.PreserveInjuries = false;
            void Isolate(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != "HumanCannon") return;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var hazard in root.GetComponentsInChildren<HumanCannonHazard>(true)) hazard.enabled = false;
                    foreach (var wind in root.GetComponentsInChildren<HumanCannonWindZone>(true)) wind.enabled = false;
                    foreach (var fire in root.GetComponentsInChildren<HumanCannonFireWall>(true))
                        typeof(HumanCannonFireWall).GetField("available", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fire, false);
                }
            }
            SceneManager.sceneLoaded += Isolate;
            try
            {
                if (!launcher.TryLaunch(start)) throw new Exception(launcher.Status);
                await Wait(() => !launcher.IsBusy && !EscapeDevelopmentSession.AwaitingEntrance
                    && Points().Any(p => p.Label == (phase == "Escape" ? Early : PerformanceEarly)), "Route", 80);
                if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
                if (phase == "Escape") await Wait(() => Actors().All(p => Last(p) != null), "Start passage on both peers", 15);
                if (phase != "Escape") await Wait(() => PerformanceRewards.Instance?.State?.IsOpen == true, "Performance roster open", 8);
                return new { scene = SceneManager.GetActiveScene().name, phase, points = Points().Count, starts = Actors().Select(Last).ToArray() };
            }
            finally { SceneManager.sceneLoaded -= Isolate; }
        }
    }

    public static async Task<object> RestartEscapeWithoutHazards()
    {
        foreach (var chase in UnityEngine.Object.FindObjectsByType<EscapeUnderstageChase>(FindObjectsSortMode.None)) chase.enabled = false;
        foreach (var boss in UnityEngine.Object.FindObjectsByType<EscapePursuerBoss>(FindObjectsSortMode.None)) boss.enabled = false;
        foreach (var fire in UnityEngine.Object.FindObjectsByType<EscapePursuitFire>(FindObjectsSortMode.None)) fire.enabled = false;
        foreach (var p in Actors()) p.ServerRestoreAll();
        NetworkPlayer.ServerRestartAll();
        await Wait(() => Actors().All(p => p.State == PlayerState.Normal), "Reset after disabling hazards");
        return Report(Actors());
    }

    public static object Move(string label, float dx = 0)
    {
        var point = Point(label);
        if (!DevCheats.Teleport(NetworkPlayer.Local, point.Position + Vector3.right * dx, point.Rotation, out var why)) throw new Exception(why);
        return point.Label;
    }

    public static async Task<object> CheckEscapeHostBeforeClient()
    {
        var local = NetworkPlayer.Local;
        Move(Early, -2.8f);
        await Task.Delay(300);
        if (Last(local) == Early) throw new Exception("Outside of radius was accepted");
        Move(Early, -.6f);
        await Wait(() => Last(local) == Early, "Inside radius");
        Move(Skip);
        await Task.Delay(250);
        if (Last(local) != Early) throw new Exception("Teleport-only point was accepted: " + Last(local));
        Move("씬 출발점");
        await Task.Delay(300);
        if (Last(local) != Early) throw new Exception("Backtracking lost progress: " + Last(local));
        return new { outsideRejected = true, insideAccepted = true, teleportOnlyIgnored = true, backtrackPreserved = true };
    }

    public static async Task<object> CheckEscapeRecovery()
    {
        var a = Actors();
        if (a.Length != 2) throw new Exception("Expected two players");
        await Wait(() => Last(a[0]) == Early && Last(a[1]) == Late, "Host saw separate passages");
        // Move the host to a known earlier spot so we can detect if the first revive moves the survivor.
        var hostPosition = a[0].transform.position;
        Kill(a[1]);
        Revive();
        await CheckPosition(a[1], Late);
        if (Vector3.Distance(a[0].transform.position, hostPosition) > .7f || a[0].State != PlayerState.Normal)
            throw new Exception("Survivor was moved");
        Kill(a[0]);
        Kill(a[1]);
        await Task.Delay(1450);
        if (a.Any(p => p.State != PlayerState.Retired)) throw new Exception("Dev wipe did not wait for F9");
        string result = Revive();
        await CheckPosition(a[0], Early);
        await CheckPosition(a[1], Late);
        if (Points().Any(p => p.Label.Contains("갈대밭 · 엔딩 도착"))) throw new Exception("Closed exit leaked its checkpoint");
        if (!DevCheatPanel.Instance.TryOpenEscapeExit() || !Points().Any(p => p.Label.Contains("갈대밭 · 엔딩 도착")))
            throw new Exception("Open exit did not expose its checkpoint");
        return new { survivorUntouched = true, wipeWaited = true, result, players = Report(a), exitGuard = true };
    }

    public static async Task<object> CheckPerformanceRecovery()
    {
        var a = Actors();
        var local = a.Single(p => p.IsLocal);
        Move(PerformanceLate, -.35f);
        await Wait(() => Last(local) == PerformanceLate, "Cannon passage");
        Kill(local, PlayerState.Fallen);
        await Task.Delay(250);
        if (local.State != PlayerState.Fallen) throw new Exception("Wipe already transitioned");
        string result = Revive();
        await CheckPosition(local, PerformanceLate);
        var reward = PerformanceRewards.Instance.State;
        if (reward?.IsOpen != true || reward.Segments.Single(p => p.ClientId == local.OwnerClientId).Dead)
            throw new Exception("Performance still considered the revived participant dead");
        return new { result, activePerformance = true, players = Report(a) };
    }

    public static object DiagnosePerformance()
    {
        var reward = PerformanceRewards.Instance;
        return new {phase = CampaignRun.Instance.State.Phase.ToString(), session = NetworkSessionManager.Instance.GameState.ToString(), elapsed = Time.timeAsDouble, actors = Report(Actors()), rewards = reward?.State == null ? null : new {reward.State.IsOpen, reward.State.Revision, result = reward.State.Latest?.EndReason, roster = reward.State.Segments.Select(p=>new{p.ClientId,p.Dead}).ToArray()}};
    }

    public static object CheckDisabled()
    {
        var cache = typeof(DevMode).GetField("_enabled", Flags);
        object original = cache.GetValue(null);
        cache.SetValue(null, false);
        try
        {
            var points = Points();
            DevCheats.HoldCheckpointWipe = true;
            if (DevMode.Enabled || points.Count != 0 || DevCheats.HoldCheckpointWipe
                || DevCheats.TryRecoverTeamAtLastCheckpoint(out _) || Last(NetworkPlayer.Local) != null)
                throw new Exception("Checkpoint state leaked outside Dev mode");
            return new { listEmpty = true, reviveBlocked = true, trackingHidden = true, wipeHoldBlocked = true };
        }
        finally { cache.SetValue(null, original); }
    }

    static NetworkPlayer[] Actors() => NetworkPlayer.All.Where(p => p != null && p.IsSpawned && p.Slot != NetworkPlayer.NoSlot).OrderBy(p => p.Slot).ToArray();
    static object Report(NetworkPlayer[] actors) => actors.Select(p => new { p.Slot, p.IsLocal, state = p.State.ToString(), injury = p.LostParts.ToString(), position = p.transform.position.ToString("F2"), last = Last(p) }).ToArray();

    static void Kill(NetworkPlayer player, PlayerState state = PlayerState.Retired)
    {
        player.ServerCut(1);
        player.ServerSetState(state);
        if (player.State != state) throw new Exception("Could not mark dead");
    }

    static string Revive()
    {
        if (!DevCheats.TryRecoverTeamAtLastCheckpoint(out var why)) throw new Exception(why);
        return why;
    }

    static Task CheckPosition(NetworkPlayer player, string label) => Wait(() => player.State == PlayerState.Normal
        && player.LostParts == BodyPart.None && Vector3.Distance(player.transform.position, Point(label).Position) < 1.2f, "Revived at " + label);

    static async Task Wait(Func<bool> ready, string stage, int seconds = 8)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        while (!ready())
        {
            if (Time.realtimeSinceStartupAsDouble >= end)
                throw new Exception(stage + " / " + DevStartLauncher.Instance?.Status + " / " + string.Join(", ", Actors().Select(p => p.Slot + ":" + p.State + ":" + Last(p) + ":" + p.transform.position)));
            await Task.Delay(65);
        }
    }
}
