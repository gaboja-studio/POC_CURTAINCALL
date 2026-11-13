using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Dev.EditorTools;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WaitingRoomCheckpointProbe
{
    public static object Seed(bool save)
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            return new[] { WaitingRoomScenes.IntegratedPath, WaitingRoomScenes.PursuitPath, WaitingRoomScenes.BasePath }.Select(path =>
            {
                var scene = EditorSceneManager.OpenScene(path);
                try
                {
                    var count = DevCheckpointWindow.SeedRoute(scene);
                    var route = DevCheckpointRoute.In(scene);
                    if (count != (scene.name == WaitingRoomScenes.IntegratedName ? 28 : 22)) throw new Exception("Missing entrance");
                    if (DevCheckpointWindow.SeedRoute(scene) != count) throw new Exception("Duplicated entrance");
                    if (save && !EditorSceneManager.SaveScene(scene)) throw new Exception("Save failed");
                    return new { scene = scene.name, count, error = "", points = route.Points.Select(p => new {p.label, feet = route.PoseFor(p).position.ToString("F2"), p.radius}).ToArray() };
                }
                catch (Exception e) { return new { scene = scene.name, count = 0, error = e.Message, points = new[] { new { label = "", feet = "", radius = 0f } }.Take(0).ToArray() }; }
            }).ToArray();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    public static async Task<object> CheckRestTime()
    {
        var session = NetworkSessionManager.Instance;
        var launcher = DevStartLauncher.EnsureExists();
        if (!WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene())) using (session.RequestMinimumStartPlayers(1))
        {
            if (!launcher.TryLaunch(new RunStartPoint(2, RunStartPhase.Morning, 0, CampaignSnapshot.TightropeId))) throw new Exception(launcher.Status);
            await Wait(() => !launcher.IsBusy && WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene()), "Waiting-room scene", 60);
        }
        if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
        var player = NetworkPlayer.Local;
        if (player.State != PlayerState.Normal) player.ServerRestoreAll();
        DevCheats.SuppressFalls = true;
        var director = PursuitDirector.Current;
        director?.DebugSetInvulnerable(true);
        var points = new List<DevCheats.TeleportPoint>();
        DevCheats.CollectTeleportPoints(player, points);
        if (points.Count != 28 || points.Any(p => p.Order < 0)) throw new Exception("Default waiting-room list missing");
        var last = typeof(DevCheats).GetMethod("LastCheckpointLabel", BindingFlags.Static | BindingFlags.NonPublic);
        string Last() => (string)last.Invoke(null, new object[] {player});

        async Task Go(string label)
        {
            var target = points.Single(p => p.Label == label);
            if (!DevCheats.Teleport(player, target.Position, target.Rotation, out var why)) throw new Exception(why);
            await Wait(() => Last() == label && Vector3.Distance(player.transform.position, target.Position) < .8f, "Visit " + label);
        }

        await Go("이전 세대 숙소 입구");
        await Go("숙소 입구");
        // Exercise the production pursuit presentation cleanup with a retired victim.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var victimType = typeof(PursuitDirector).GetNestedType("Victim", BindingFlags.NonPublic);
        var victim = Activator.CreateInstance(victimType, true);
        var pose = player.gameObject.AddComponent<PursuitVictimPose>();
        victimType.GetField("Object").SetValue(victim, player.gameObject);
        victimType.GetField("Pose").SetValue(victim, pose);
        victimType.GetField("Retired").SetValue(victim, true);
        ((System.Collections.IDictionary)typeof(PursuitDirector).GetField("victims", flags).GetValue(director))[player.OwnerClientId] = victim;
        player.ServerRetireFromPursuit();
        if (!PursuitDirector.IsCaptured(player.gameObject)) throw new Exception("Victim setup failed");
        await Task.Delay(100);
        if (!DevCheats.TryRecoverTeamAtLastCheckpoint(out var result)) throw new Exception(result);
        var expected = points.Single(p => p.Label == "숙소 입구");
        await Wait(() => player.State == PlayerState.Normal && !PursuitDirector.IsCaptured(player.gameObject)
            && player.GetComponent<PursuitVictimPose>() == null && player.GetComponent<CurtainCall.Player.PlayerMover>().ControlEnabled
            && Vector3.Distance(player.transform.position, expected.Position) < .8f, "Recovered control and latest entrance");
        if (CampaignRun.Instance.State.Phase != CampaignPhase.RestTime) throw new Exception("Left rest time");
        var flag = typeof(DevMode).GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic);
        object old = flag.GetValue(null);
        try
        {
            flag.SetValue(null, false);
            DevCheats.CollectTeleportPoints(player, points);
            if (points.Count != 0 || DevCheats.TryRecoverTeamAtLastCheckpoint(out _)) throw new Exception("Dev OFF leaked");
        }
        finally { flag.SetValue(null, old); }
        return new { scene = SceneManager.GetActiveScene().name, last = expected.Label, result, controlRestored = true, devOff = true };
    }

    public static object Diagnose()
    {
        var p = NetworkPlayer.Local;
        return new { phase = CampaignRun.Instance.State.Phase.ToString(), scene = SceneManager.GetActiveScene().name, state = p.State.ToString(), death = p.LastDeathOrigin.ToString(), position = p.transform.position.ToString(), caught = PursuitDirector.IsCaptured(p.gameObject) };
    }

    static async Task Wait(Func<bool> predicate, string stage, double seconds = 6)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate())
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new Exception(stage + " / " + DevStartLauncher.Instance?.Status);
            await Task.Delay(65);
        }
    }
}
