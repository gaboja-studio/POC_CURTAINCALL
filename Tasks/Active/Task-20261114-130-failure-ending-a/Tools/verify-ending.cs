using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EndingVerification
{
    static async Task Wait(Func<bool> ready,string label,int seconds=90)
    {
        var until=DateTime.UtcNow.AddSeconds(seconds);
        while(!ready()) { if(DateTime.UtcNow>until)throw new Exception("Timed out: "+label);await Task.Delay(100); }
    }
    public static object Prepare()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/InGame/Start.unity");
        return new{scene=SceneManager.GetActiveScene().name};
    }
    public static async Task<object> Launch(int day,int phase)
    {
        typeof(DevMode).GetField("_enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        Application.runInBackground=true;Time.timeScale=1;
        var launch=DevStartLauncher.EnsureExists();
        if(!launch.TryLaunch(new RunStartPoint(day,(RunStartPhase)phase,0,CampaignSnapshot.TightropeId)))throw new Exception(launch.Status);
        await Wait(()=>!launch.IsBusy,"F8 scene launch",150);
        if(!launch.LastLaunchSucceeded)throw new Exception(launch.Status);
        await Wait(()=>NetworkPlayer.Local!=null&&NetworkPlayer.Local.IsSpawned,"player spawn");
        return new{day=CampaignRun.Instance.State.Day,phase=CampaignRun.Instance.State.Phase.ToString(),scene=SceneManager.GetActiveScene().name,launch.Status,frame=Time.frameCount};
    }
    public static object Cheat(int reason)
    {
        var before=CampaignRun.Instance.State;
        var panel=DevCheatPanel.EnsureExists();
        bool accepted=panel.TryFailureEnding((RunEndReason)reason);
        if(!accepted)throw new Exception(panel.Status);
        var after=CampaignRun.Instance.State;
        if(before.Day!=after.Day||before.Generation!=after.Generation)throw new Exception("Cheat erased run date");
        return new{accepted,day=after.Day,phase=after.Phase.ToString(),reason=after.LastEnd.ToString()};
    }
    public static async Task<object> FailureReady()
    {
        await Wait(()=>FailureEndingPresentation.Current!=null&&FailureEndingPresentation.Current.Ready,"Failure result",25);
        var ending=FailureEndingPresentation.Current;
        var result=ending.GetComponent<EscapeEndingResults>();
        var stage=ending.GetComponentInChildren<FailureEndingStage>(true);
        int firstFrame=Time.frameCount;float min=float.MaxValue,max=0,maxAngle=0;
        var camera=stage.GetComponentInChildren<Camera>(true);var initial=camera.transform.position;
        for(int i=0;i<75;i++)
        {
            await Task.Delay(100);min=Mathf.Min(min,stage.CurrentIntensity);max=Mathf.Max(max,stage.CurrentIntensity);maxAngle=Mathf.Max(maxAngle,Mathf.Abs(stage.SwayAngle));
            if(Vector3.Distance(initial,camera.transform.position)>.001f)throw new Exception("Camera moved during static shot");
        }
        if(Time.frameCount-firstFrame<20)throw new Exception("Play frames are stalled");
        var title=result.View.EndingTitle;
        foreach(char c in title.text)if(!char.IsWhiteSpace(c)&&!title.font.HasCharacter(c))throw new Exception("Missing glyph: "+c);
        return new {title=title.text,summary=result.View.Summary.text,participants=result.People.Count,
            fade=ending.FadeAlpha,minIntensity=min,maxIntensity=max,maxAngle,cameraStationary=true,frames=Time.frameCount-firstFrame,
            phase=CampaignRun.Instance.State.Phase.ToString(),day=CampaignRun.Instance.State.Day};
    }
    public static async Task<object> FadeProof()
    {
        Cheat(1);
        var ending=FailureEndingPresentation.Current;
        var stage=ending.GetComponentInChildren<FailureEndingStage>(true);
        bool up=false,black=false,down=false,stageAppearedUnderBlack=false;
        var deadline=DateTime.UtcNow.AddSeconds(25);
        bool stageSeen=false;
        while(!ending.Ready)
        {
            float alpha=ending.FadeAlpha;
            if(!stage.Playing&&alpha>0&&alpha<1)up=true;
            if(alpha>=.999f)black=true;
            if(!stageSeen&&stage.Playing){stageSeen=true;stageAppearedUnderBlack=alpha>=.999f;}
            if(stage.Playing&&alpha>0&&alpha<1)down=true;
            if(DateTime.UtcNow>deadline)throw new Exception("Fade sequence timed out");
            await Task.Delay(20);
        }
        if(!up||!black||!down||!stageAppearedUnderBlack)throw new Exception("Fade/camera switch order was not correct");
        return new{fadeOut=up,fullyCovered=black,cameraSwitchUnderBlack=stageAppearedUnderBlack,fadeIn=down,title=ending.GetComponent<EscapeEndingResults>().View.EndingTitle.text};
    }
    public static object Statistics(bool show)
    {
        var ending=FailureEndingPresentation.Current;
        var result=ending.IsPresenting?ending.GetComponent<EscapeEndingResults>():EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>();
        result.View.SelectTab(show);return new{selected=result.View.StatisticsSelected};
    }
    public static async Task<object> Return()
    {
        var ending=FailureEndingPresentation.Current;
        var success=EscapeEndingPresentation.Current;
        bool failure=ending!=null&&ending.IsPresenting;
        Component owner=failure?ending:success;
        owner.GetComponent<EscapeEndingResults>().View.ReturnButton.onClick.Invoke();
        await Wait(()=>SpaceLinkDirector.Instance.Current==SpaceId.Hub&&!SpaceLinkDirector.Instance.IsTransitioning,"Lobby return",100);
        await Wait(()=>failure?!ending.IsPresenting:success==null||!success.IsPresenting,"Presentation cleanup",10);
        await Task.Delay(1000);
        var player=NetworkPlayer.Local;var mover=player.GetComponent<PlayerMover>();
        bool leaked=((System.Collections.Generic.HashSet<object>)typeof(InputGate).GetField("blockers",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Contains(owner);
        if(leaked||!mover.ControlEnabled||player.State!=PlayerState.Normal)throw new Exception("Lobby control was not restored");
        return new{scene=SceneManager.GetActiveScene().name,control=mover.ControlEnabled,state=player.State.ToString(),inputBlockLeaked=leaked};
    }
    public static object Expire()
    {
        var run=CampaignRun.Instance;
        var host=(RunState)typeof(CampaignRun).GetField("host",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(run);
        if(!run.State.IsFinalMorning)throw new Exception("Not final morning");
        host.DebugSetRemaining(.01f);host.SetRestTimerPaused(false);
        // Use the actual timer transition, then let CampaignRun publish the resulting snapshot.
        host.Tick(1);
        typeof(CampaignRun).GetMethod("Apply",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(run,new object[]{host.Snapshot});
        return new{day=run.State.Day,reason=run.State.LastEnd.ToString(),phase=run.State.Phase.ToString()};
    }
    public static object Retire()
    {
        DevCheats.HoldCheckpointWipe=false;
        foreach(var player in NetworkPlayer.All)if(player.IsSpawned)player.ServerSetState(PlayerState.Retired);
        return new{phase=CampaignRun.Instance.State.Phase.ToString()};
    }
    public static async Task<object> Success()
    {
        var content=EscapeCampaignContent.Current;
        if(!content.DevOpenExit(out string why))throw new Exception(why);
        var area=(BoxCollider)typeof(EscapeCampaignContent).GetField("arrivalArea",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(content);
        NetworkPlayer.Local.GetComponent<PlayerMover>().Relocate(area.transform.TransformPoint(area.center),Quaternion.identity);
        await Wait(()=>CampaignRun.Instance.State.LastEnd==RunEndReason.EscapeSucceeded,"Arrival success",20);
        var result=EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>();
        await Wait(()=>result.View!=null&&result.View.Visible,"Success result",20);
        return new{title=result.View.EndingTitle.text,day=CampaignRun.Instance.State.Day,phase=CampaignRun.Instance.State.Phase.ToString(),reason=CampaignRun.Instance.State.LastEnd.ToString()};
    }
}
