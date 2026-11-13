using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Dev.EditorTools;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.UI.EscapeResultsKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CheckpointSubmissionProbe
{
    const string Start = "Assets/Scenes/InGame/Start.unity";
    static readonly string[] Scenes = {
        "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity",
        "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity",
        "Assets/Scenes/Levels/escape_route.unity"
    };

    public static object Seed()
    {
        var result = new List<object>();
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            int count = DevCheckpointWindow.SeedRoute(scene);
            if (count < 2) throw new Exception("Too few checkpoints in " + path);
            var route = DevCheckpointRoute.In(scene);
            if (scene.name == "escape_route")
            {
                if (!route.Points.Any(p => p.label.Contains("추격 /"))) throw new Exception("Updated chase anchors missing");
                if (UnityEngine.Object.FindFirstObjectByType<EscapePursuerBoss>(FindObjectsInactive.Include) == null
                    || UnityEngine.Object.FindFirstObjectByType<EscapeEndingCinematic>(FindObjectsInactive.Include) == null)
                    throw new Exception("Latest boss or ending rig is missing after scene merge");
            }
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save " + path);
            result.Add(new { scene = scene.name, count, order = route.Points.Select(p => p.label).ToArray() });
        }
        EditorSceneManager.OpenScene(Start);
        return result;
    }

    public static object Prepare()
    {
        SessionState.SetBool("CheckpointSubmissionProbe.OldDev", DevMode.Enabled);
        DevMode.SetEditorEnabled(true);
        EditorSceneManager.OpenScene(Start);
        return "Ready";
    }

    public static object Restore()
    {
        DevMode.SetEditorEnabled(SessionState.GetBool("CheckpointSubmissionProbe.OldDev", false));
        return "Restored";
    }

    const string ScenarioPath = "Assets/Settings/PlayMode/CheckpointSubmissionLocal.asset";
    public static async Task<object> PrepareSecondEditor()
    {
        if (!AssetDatabase.CopyAsset("Assets/Settings/PlayMode/EscapeEntryFourEditors.asset", ScenarioPath))
            throw new Exception("Could not copy temporary scenario");
        var config = AssetDatabase.LoadMainAssetAtPath(ScenarioPath);
        var serialized = new SerializedObject(config);
        serialized.FindProperty("m_EditorInstances").arraySize = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(config);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        config.GetType().GetMethod("OnConfigSelected", flags).Invoke(config, null);
        var start = (Task)config.GetType().GetMethod("ExecuteStartAsync", flags).Invoke(config, new object[] { System.Threading.CancellationToken.None });
        if (await Task.WhenAny(start, Task.Delay(1400)) == start) await start;
        return start.Status.ToString();
    }

    public static object RemoveScenario()
    {
        AssetDatabase.DeleteAsset(ScenarioPath);
        return "Removed";
    }

    static async Task Wait(Func<bool> ready, string stage)
    {
        double end = Time.realtimeSinceStartupAsDouble + 19;
        while (!ready())
        {
            if (Time.realtimeSinceStartupAsDouble >= end)
                throw new Exception(stage + ": " + DevStartLauncher.Instance?.Status);
            await Task.Delay(80);
        }
    }
    public static async Task<object> StartEscape()
    {
        await Host();
        return await LaunchEscape(1);
    }

    public static async Task<object> Host()
    {
        var session = NetworkSessionManager.EnsureExists();
        if (!await session.HostAsync(new LanSessionConnector(session.NetworkManager, 7798))) throw new Exception("Host failed");
        return new { host = session.IsHost };
    }

    public static async Task<object> Join()
    {
        var session = NetworkSessionManager.EnsureExists();
        if (!await session.JoinAsync(new LanSessionConnector(session.NetworkManager, 7798), "127.0.0.1")) throw new Exception("Join failed");
        await Wait(() => NetworkPlayer.Local != null && NetworkPlayer.Local.IsSpawned, "Client spawn");
        return new { slot = NetworkPlayer.Local.Slot };
    }

    public static Task<object> StartTeamEscape() => LaunchEscape(2);
    static async Task<object> LaunchEscape(int count)
    {
        var session = NetworkSessionManager.Instance;
        using (session.RequestMinimumStartPlayers(count))
        {
            await Wait(() => NetworkPlayer.All.Count(p => p != null && p.IsSpawned && p.Slot != NetworkPlayer.NoSlot) == count, "Player spawn");
            var launcher = DevStartLauncher.EnsureExists();
            if (!launcher.TryLaunch(new RunStartPoint(1, RunStartPhase.Escape, 0, ""))) throw new Exception(launcher.Status);
            await Wait(() => !launcher.IsBusy, "Dev escape");
            if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
            return new { scene = SceneManager.GetActiveScene().name, phase = CampaignRun.Instance.State.Phase.ToString() };
        }
    }

    public static async Task<object> RecoverTeam()
    {
        var actors = NetworkPlayer.All.Where(p => p.IsSpawned && p.Slot != NetworkPlayer.NoSlot).OrderBy(p=>p.Slot).ToArray();
        if (actors.Length != 2) throw new Exception("Expected host and client");
        var points = new List<DevCheats.TeleportPoint>();
        DevCheats.CollectCheckpoints(points);
        var target = points.Last(p => p.TeamRecovery);
        foreach (var player in actors) { player.ServerCut(1); player.ServerSetState(PlayerState.Retired); }
        if (!DevCheats.TryRecoverTeamAtLastCheckpoint(out var why)) throw new Exception(why);
        await Wait(() => actors.All(p => p.State == PlayerState.Normal && p.LostParts == CurtainCall.Player.BodyPart.None
            && Vector3.Distance(p.transform.position, target.Position) < 1.4f), "Both owners relocated");
        return new { checkpoint = target.Label, actors = actors.Select(p=>new {p.Slot, state = p.State.ToString(),
            injury = p.LostParts.ToString(), position = p.transform.position.ToString("F2")}).ToArray() };
    }

    public static async Task<object> RecoverAndEnd()
    {
        var actor = NetworkPlayer.Local;
        var points = new List<DevCheats.TeleportPoint>();
        DevCheats.CollectCheckpoints(points);
        if (points.Any(p => p.Label.Contains("엔딩 도착"))) throw new Exception("Closed door exposed arrival shortcut");
        var target = points.Last(p => p.TeamRecovery);
        actor.ServerCut(1);
        actor.ServerSetState(PlayerState.Retired);
        if (!DevCheats.TryRecoverTeamAtLastCheckpoint(out var why)) throw new Exception(why);
        await Wait(() => actor.State == PlayerState.Normal && actor.LostParts == CurtainCall.Player.BodyPart.None
            && Vector3.Distance(actor.transform.position, target.Position) < .8f, "Checkpoint revival");
        if (!DevCheatPanel.EnsureExists().TryOpenEscapeExit()) throw new Exception(DevCheatPanel.Instance.Status);
        DevCheats.CollectCheckpoints(points);
        var meadow = points.Single(p => p.Label.Contains("엔딩 도착"));
        if (meadow.TeamRecovery) throw new Exception("Ending arrival can be used for team recovery");
        if (!DevCheats.Teleport(actor, meadow.Position, meadow.Rotation, out why)) throw new Exception(why);
        await Wait(() => CampaignRun.Instance.State.Phase == CampaignPhase.Ending
            && EscapeEndingPresentation.Current?.GetComponent<EscapeEndingResults>()?.View?.Visible == true, "Real result window");
        var ending = EscapeEndingPresentation.Current;
        return new { checkpoint = target.Label, checkpointCount = points.Count, recovery = true, door = true,
            phase = CampaignRun.Instance.State.Phase.ToString(), result = true, running = ending.GetComponent<EscapeEndingCinematic>().Playing };
    }
}
