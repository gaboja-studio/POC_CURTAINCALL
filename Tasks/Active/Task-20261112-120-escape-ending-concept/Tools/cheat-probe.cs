using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Reward;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CheatProbe
{
    public static object Prepare()
    {
        SessionState.SetBool("CheatProbe.OldDev",DevMode.Enabled);
        SessionState.SetInt("CheatProbe.Next",0);
        DevMode.SetEditorEnabled(true);
        EditorSceneManager.OpenScene("Assets/Scenes/InGame/Start.unity");
        return "Prepared";
    }
    static async Task Until(Func<bool> ready)
    {
        double end = Time.realtimeSinceStartupAsDouble + 100;
        while (!ready())
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new Exception("Timeout: " + DevStartLauncher.Instance?.Status);
            await Task.Delay(100);
        }
    }
    public static async Task<object> NextPerformance()
    {
        int index = SessionState.GetInt("CheatProbe.Next",0);
        if (index >= 6) throw new Exception("All six completed");
        var session = NetworkSessionManager.EnsureExists();
        using (session.RequestMinimumStartPlayers(1))
        {
            if (session.ConnectionState == SessionConnectionState.Offline
                && !await session.HostAsync(new LanSessionConnector(session.NetworkManager,7795))) throw new Exception("Host failed");
            await Until(() => NetworkPlayer.Local != null && NetworkPlayer.Local.IsSpawned);
            string trick = index < 3 ? "Tightrope" : "HumanCannon"; int tier = index % 3 + 1;
            var panel = DevCheatPanel.EnsureExists();
            if (!panel.IsOpen) panel.Toggle();
            if (!panel.TryLaunchPerformance(trick,tier)) throw new Exception(panel.Status);
            var launcher = DevStartLauncher.Instance;
            await Until(() => !launcher.IsBusy);
            if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
            await Task.Delay(400);
            var state = CampaignRun.Instance.State;
            if (state.TrickId != trick || state.Difficulty != tier || PerformanceRewards.Instance.Difficulty != tier)
                throw new Exception("Difficulty not applied: " + state.Difficulty + "/" + PerformanceRewards.Instance.Difficulty);
            SessionState.SetInt("CheatProbe.Next",index+1);
            return new { trick,tier,scene=SceneManager.GetActiveScene().name,rewardTier=PerformanceRewards.Instance.Difficulty,
                cheatClosed=!panel.IsOpen,launcherClosed=!launcher.IsOpen,frames=Time.frameCount };
        }
    }
    public static async Task<object> EscapePoints()
    {
        var launcher = DevStartLauncher.EnsureExists();
        if (!launcher.TryLaunch(new RunStartPoint(1,RunStartPhase.Escape,0,""))) throw new Exception(launcher.Status);
        await Until(() => !launcher.IsBusy);
        if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
        var points = new List<DevCheats.TeleportPoint>();
        DevCheats.CollectTeleportPoints(NetworkPlayer.Local,points);
        var rear = points.Where(p=>p.Label.StartsWith("탈출로 · ")).ToArray();
        if (rear.Length != 6) throw new Exception("Expected six closed-door stops, found " + rear.Length);
        var records = new List<object>();
        foreach (var point in rear)
        {
            if (!DevCheats.Teleport(NetworkPlayer.Local,point.Position,point.Rotation,out var why)) throw new Exception(why);
            await Task.Delay(180);
            var actor = NetworkPlayer.Local;
            float displacement = Vector3.Distance(actor.transform.position,point.Position);
            bool floor = Physics.Raycast(point.Position + Vector3.up*.5f,Vector3.down,out var hit,1f,~0,QueryTriggerInteraction.Ignore);
            records.Add(new {point.Label,position=point.Position.ToString("F2"),displacement,state=actor.State.ToString(),floor,
                support=floor ? hit.collider.name : null});
            if (!floor || displacement > 1 || actor.State != PlayerState.Normal) throw new Exception("Unsafe stop: "+point.Label+" "+displacement);
        }
        return records;
    }
    public static async Task<object> EscapeOnly()
    {
        var session=NetworkSessionManager.EnsureExists();
        using(session.RequestMinimumStartPlayers(1))
        {
            if(!await session.HostAsync(new LanSessionConnector(session.NetworkManager,7795))) throw new Exception("Host failed");
            await Until(()=>NetworkPlayer.Local != null && NetworkPlayer.Local.IsSpawned);
            return await EscapePoints();
        }
    }
    public static async Task<object> KeepClearsAndOpenCheckpoints()
    {
        var campaign = CampaignRun.Instance;
        if (!campaign.RecordClear(CampaignSnapshot.TightropeId, 2)
            || !campaign.RecordClear(CampaignSnapshot.HumanCannonId, 1))
            throw new Exception("Could not seed successful performances via the host clear API");
        var launcher = DevStartLauncher.Instance;
        launcher.PreservePerformanceClears = true;
        var checkpoints = await EscapePoints();
        if (campaign.ClearedTier(CampaignSnapshot.TightropeId) != 2
            || campaign.ClearedTier(CampaignSnapshot.HumanCannonId) != 1
            || campaign.MaxSelectableTier(CampaignSnapshot.TightropeId) != 3
            || campaign.MaxSelectableTier(CampaignSnapshot.HumanCannonId) != 2)
            throw new Exception("Clear tiers did not survive the space jump");
        var panel = DevCheatPanel.EnsureExists();
        if (!panel.IsOpen) panel.Toggle();
        return new { scene=SceneManager.GetActiveScene().name, checkpoints,
            tightrope=campaign.ClearedTier(CampaignSnapshot.TightropeId),
            cannon=campaign.ClearedTier(CampaignSnapshot.HumanCannonId), panel=panel.IsOpen };
    }
    public static async Task<object> ClearHistoryDisabled()
    {
        var launcher = DevStartLauncher.Instance;
        launcher.PreservePerformanceClears = false;
        var panel = DevCheatPanel.Instance;
        if (!panel.TryLaunchPerformance(CampaignSnapshot.TightropeId, 1)) throw new Exception(panel.Status);
        await Until(() => !launcher.IsBusy);
        if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
        var campaign = CampaignRun.Instance;
        if (campaign.ClearedTier(CampaignSnapshot.TightropeId) != 0
            || campaign.ClearedTier(CampaignSnapshot.HumanCannonId) != 0)
            throw new Exception("Opting out did not reset clear history");
        return new { scene=SceneManager.GetActiveScene().name, clearedTiersReset=true };
    }
    public static async Task<object> RetryAfterRetired()
    {
        int generation=CampaignRun.Instance.State.Generation;
        NetworkPlayer.Local.ServerSetState(PlayerState.Retired);
        await Until(()=>NetworkPlayer.Local.State == PlayerState.Normal);
        if(CampaignRun.Instance.State.Phase != CampaignPhase.Escape || CampaignRun.Instance.State.Generation != generation)
            throw new Exception("Direct-start retry changed the run");
        return new { phase=CampaignRun.Instance.State.Phase.ToString(), generation, state=NetworkPlayer.Local.State.ToString() };
    }
    public static async Task<object> OpenDoorAndArrive()
    {
        // Exercise the same public action as the F9 button. Reflection below reads evidence only.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var saw = (EscapeExitSaw)typeof(EscapeCampaignContent).GetField("exitSaw",flags).GetValue(EscapeCampaignContent.Current);
        var panel = DevCheatPanel.EnsureExists();
        if (saw.DoorOpen) throw new Exception("Door must start closed");
        try
        {
            DevMode.SetEditorEnabled(false);
            if (panel.TryOpenEscapeExit() || saw.DoorOpen) throw new Exception("Dev-off guard failed");
        }
        finally { DevMode.SetEditorEnabled(true); }
        if (!panel.TryOpenEscapeExit() || !saw.DoorOpen) throw new Exception(panel.Status);
        var revision = typeof(EscapeExitSaw).GetField("stateRevision",flags);
        uint openedRevision = (uint)revision.GetValue(saw);
        if (!panel.TryOpenEscapeExit() || (uint)revision.GetValue(saw) != openedRevision)
            throw new Exception("Repeated open changed the state");
        var cables = (EscapeCuttableCable[])typeof(EscapeExitSaw).GetField("cables",flags).GetValue(saw);
        if (cables.Any(c=>c != null && c.RequiredForExit && !c.IsCut)) throw new Exception("Required cable visuals not cut");
        NetworkPlayer.Local.RequestRestartAll();
        await Until(()=>!saw.DoorOpen);
        await Until(()=>!EscapeDevelopmentSession.AwaitingEntrance && NetworkPlayer.Local.State == PlayerState.Normal);
        if (!panel.TryOpenEscapeExit() || !saw.DoorOpen) throw new Exception("Reopen after restart: " + panel.Status);
        await Task.Delay(2000);
        float doorAngle = (float)typeof(EscapeExitSaw).GetField("doorAngle",flags).GetValue(saw);
        if (doorAngle <= 0) throw new Exception("Door did not animate");
        var points = new List<DevCheats.TeleportPoint>();
        DevCheats.CollectTeleportPoints(NetworkPlayer.Local,points);
        var meadow = points.Single(p=>p.Label == "탈출로 · 갈대밭 · 엔딩 도착");
        if (DevCheatPanel.Instance.IsOpen) DevCheatPanel.Instance.Toggle();
        if (!DevCheats.Teleport(NetworkPlayer.Local,meadow.Position,meadow.Rotation,out var why)) throw new Exception(why);
        await Until(()=>CampaignRun.Instance.State.Phase == CampaignPhase.Ending);
        await Until(()=>EscapeEndingPresentation.Current != null
            && EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>().View != null
            && EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>().View.Visible);
        return new {point=meadow.Label,state=NetworkPlayer.Local.State.ToString(),phase=CampaignRun.Instance.State.Phase.ToString(),
            doorAngle,requiredCables=saw.RequiredCableCount,cutCables=saw.CutCableCount,
            devOffRejected=true,repeatedOpenStable=true,restartClosed=true,resultsVisible=true,frame=Time.frameCount};
    }
    public static object Restore() { DevMode.SetEditorEnabled(SessionState.GetBool("CheatProbe.OldDev",false)); return "Dev restored"; }
}
