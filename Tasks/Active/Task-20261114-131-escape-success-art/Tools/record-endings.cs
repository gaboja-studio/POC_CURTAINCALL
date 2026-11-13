using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.SpaceLink;

// Editor-only, outside Assets. Records actual Game View; never supplies fake result rows/actors.
public static class EndingVideoRecording
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    const string Folder="Tasks/Active/Task-20261114-131-escape-success-art/Videos";
    static RecorderController recorder;
    static double started;
    static string output;
    static void Dev(bool enabled)=>typeof(DevMode).GetField("_enabled",F).SetValue(null,enabled);
    static async Task Until(Func<bool> ready,string label,int seconds=120)
    {var end=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>end)throw new Exception(label);await Task.Delay(75);}}
    public static object Begin(string name)
    {
        Directory.CreateDirectory(Folder);output=Path.GetFullPath(Folder+"/"+name);
        if(File.Exists(output+".mp4"))throw new Exception("Preserve existing recording: "+output);
        var settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();
        recorder=new RecorderController(settings);
        var movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name=name;movie.Enabled=true;movie.CaptureAudio=false;movie.CaptureAlpha=false;
        movie.EncoderSettings=new CoreEncoderSettings{EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.High,Codec=CoreEncoderSettings.OutputCodec.MP4};
        movie.ImageInputSettings=new GameViewInputSettings{OutputWidth=1920,OutputHeight=1080};
        movie.OutputFile=output;settings.AddRecorderSettings(movie);settings.SetRecordModeToManual();
        settings.FrameRate=30;settings.FrameRatePlayback=FrameRatePlayback.Variable;settings.CapFrameRate=true;
        RecorderOptions.VerboseMode=false;recorder.PrepareRecording();
        if(!recorder.StartRecording())throw new Exception("Recorder refused");
        started=Time.realtimeSinceStartupAsDouble;
        return new{output=output+".mp4",players=NetworkSessionManager.Instance.PlayerCount};
    }
    public static object Stop()
    {
        recorder?.StopRecording();recorder=null;
        return new{output=output+".mp4",seconds=Time.realtimeSinceStartupAsDouble-started,bytes=new FileInfo(output+".mp4").Length};
    }
    public static async Task<object> Launch(bool failure=false,bool deadline=false)
    {
        Dev(true);DevCheats.HoldCheckpointWipe=true;Application.runInBackground=true;Time.timeScale=1;
        var session=NetworkSessionManager.Instance;
        if(session!=null&&session.GameState==GameSessionState.Ended&&SpaceLinkDirector.Instance.Current==SpaceId.Hub)session.ReturnToLobby();
        var launch=DevStartLauncher.EnsureExists();
        if(!launch.TryLaunch(new RunStartPoint(deadline?4:3,deadline?RunStartPhase.Morning:RunStartPhase.Escape,0,CampaignSnapshot.TightropeId)))throw new Exception(launch.Status);
        await Until(()=>!launch.IsBusy,"Launch",180);
        if(!launch.LastLaunchSucceeded)throw new Exception(launch.Status);
        await Until(()=>NetworkPlayer.Local!=null&&NetworkPlayer.Local.IsSpawned,"Spawn");
        if(!deadline)
        {
            foreach(var player in NetworkPlayer.All)if(player.IsSpawned){player.ServerRestoreAll();player.ServerSetState(PlayerState.Normal);}
            var points=new System.Collections.Generic.List<DevCheats.TeleportPoint>();
            DevCheats.CollectTeleportPoints(NetworkPlayer.Local,points);
            var point=points.Last(p=>p.Label.Contains("마지막 문")&&!p.Label.Contains("갈대"));
            DevCheats.SelectAllTargets();
            if(!DevCheats.TeleportTargets(p=>(true,new Pose(point.Position+Vector3.right*p.Slot*.8f,point.Rotation)),"Recording start",out var why))throw new Exception(why);
        }
        Dev(false);await Task.Delay(1200);
        return new{players=session.PlayerCount,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name};
    }
    public static async Task<object> TriggerSuccess()
    {
        Dev(true);var content=EscapeCampaignContent.Current;
        var partner=NetworkPlayer.All.FirstOrDefault(p=>p.IsSpawned&&!p.IsLocal);
        if(partner!=null&&partner.LostParts==CurtainCall.Player.BodyPart.None)partner.ServerCut(1);
        if(!content.DevOpenExit(out string why))throw new Exception(why);
        var area=(BoxCollider)typeof(EscapeCampaignContent).GetField("arrivalArea",F).GetValue(content);
        DevCheats.SelectAllTargets();
        if(!DevCheats.TeleportTargets(p=>(true,new Pose(area.transform.TransformPoint(area.center)+Vector3.right*p.Slot*.7f,Quaternion.identity)),"Ending arrival",out var teleport))throw new Exception(teleport);
        Dev(false);
        await Until(()=>CampaignRun.Instance.State.LastEnd==RunEndReason.EscapeSucceeded,"Success arrival",40);
        await Until(()=>EscapeEndingPresentation.Current?.GetComponent<EscapeEndingResults>().View?.Visible==true,"Success results",40);
        return new{people=EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>().People.Count};
    }
    public static async Task<object> TriggerFailure(bool deadline)
    {
        if(deadline)
        {
            var run=CampaignRun.Instance;var host=(RunState)typeof(CampaignRun).GetField("host",F).GetValue(run);
            host.DebugSetRemaining(.01f);host.SetRestTimerPaused(false);host.Tick(1);
            typeof(CampaignRun).GetMethod("Apply",F).Invoke(run,new object[]{host.Snapshot});
        }
        else{DevCheats.HoldCheckpointWipe=false;foreach(var p in NetworkPlayer.All)if(p.IsSpawned)p.ServerSetState(PlayerState.Retired);}
        await Until(()=>FailureEndingPresentation.Current?.Ready==true,"Failure results",40);
        return new{reason=CampaignRun.Instance.State.LastEnd.ToString(),players=NetworkSessionManager.Instance.PlayerCount};
    }
    public static async Task<object> Finish(bool failure)
    {
        var ending=failure?(Component)FailureEndingPresentation.Current:EscapeEndingPresentation.Current;
        var view=ending.GetComponent<EscapeEndingResults>().View;
        await Task.Delay(4500);view.SelectTab(true);await Task.Delay(4500);
        view.SelectTab(false);await Task.Delay(1500);
        var previous=CampaignRun.Instance.State.Generation;
        view.ReturnButton.onClick.Invoke();
        await Until(()=>!SpaceLinkDirector.Instance.IsTransitioning&&(failure
            ? CampaignRun.Instance.State.Generation==previous+1&&SpaceLinkDirector.Instance.Current==SpaceId.WaitingRoom&&!FailureEndingPresentation.Current.IsPresenting
            : SpaceLinkDirector.Instance.Current==SpaceId.Hub),"Post-ending transition");
        await Task.Delay(5000);
        var actor=NetworkPlayer.Local;
        if(!actor.GetComponent<PlayerMover>().ControlEnabled||actor.State!=PlayerState.Normal)throw new Exception("Control did not restore");
        if(failure&&(CampaignRun.Instance.State.Day!=1||CampaignRun.Instance.State.Rest!=RestKind.Morning||CampaignRun.Instance.State.CannonUnlocked))throw new Exception("Wrong new run");
        return new{video=Stop(),scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,generation=CampaignRun.Instance.State.Generation,day=CampaignRun.Instance.State.Day,players=NetworkSessionManager.Instance.PlayerCount,control=true};
    }
    public static async Task<object> Single(string name,bool failure,bool deadline=false)
    {
        await Launch(failure,deadline);Begin(name);
        try{await Task.Delay(2000);if(failure)await TriggerFailure(deadline);else await TriggerSuccess();return await Finish(failure);}
        finally{if(recorder!=null)Stop();}
    }
    public static async Task<object> ClientEscape()
    {
        Application.runInBackground=true;
        await Until(()=>CampaignRun.Instance?.State.Phase==CampaignPhase.Escape&&EscapeCampaignContent.Current?.ExitOpen==true&&!SpaceLinkDirector.Instance.IsTransitioning,"Host exit open",180);
        var area=(BoxCollider)typeof(EscapeCampaignContent).GetField("arrivalArea",F).GetValue(EscapeCampaignContent.Current);
        var actor=NetworkPlayer.Local;
        actor.GetComponent<PlayerMover>().Relocate(area.transform.TransformPoint(area.center)+Vector3.right*.7f,Quaternion.identity);
        Dev(false);
        await Until(()=>EscapeEndingPresentation.Current?.GetComponent<EscapeEndingResults>().View?.Visible==true,"Client result",45);
        return new{client=actor.OwnerClientId,actor.AppearanceModel,actor.AppearanceOutfit,lost=actor.LostParts.ToString(),people=EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>().People.Count};
    }
}
