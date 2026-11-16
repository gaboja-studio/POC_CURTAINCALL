using System;
using System.IO;
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
using CurtainCall.WaitingRoom;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FailureRestartVerification
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static async Task Until(Func<bool> ready,string label,int seconds=100)
    {var deadline=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>deadline)throw new Exception(label);await Task.Delay(100);}}
    public static async Task<object> Fail(bool deadline,bool mastered)
    {
        typeof(DevMode).GetField("_enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        Application.runInBackground=true;Time.timeScale=1;
        var launch=DevStartLauncher.EnsureExists();
        var point=new RunStartPoint(deadline?4:3,deadline?RunStartPhase.Morning:RunStartPhase.Escape,0,CampaignSnapshot.TightropeId);
        if(!launch.TryLaunch(point))throw new Exception(launch.Status);
        await Until(()=>!launch.IsBusy,"Launch",150);if(!launch.LastLaunchSucceeded)throw new Exception(launch.Status);
        await Until(()=>NetworkPlayer.Local!=null&&NetworkPlayer.Local.IsSpawned,"Spawn");
        // Test fixture: previously learned rope/one guide, optionally cannon. No PlayerPrefs writes.
        WaitingRoomTutorial.MemoryOnly=true;
        var book=typeof(WaitingRoomTutorial).GetProperty("Book",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        book.GetType().GetMethod("MarkDone").Invoke(book,new object[]{"G11"});
        typeof(WaitingRoomRehearsal).GetField("savedStage",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,100);
        var cannon=UnityEngine.Object.FindFirstObjectByType<WaitingRoomCannonRehearsal>();
        var shared=typeof(WaitingRoomCannonRehearsal).GetField("shared",Fields).GetValue(cannon);
        shared.GetType().GetField("mastered").SetValue(shared,mastered);
        if(deadline)
        {
            var run=CampaignRun.Instance;var host=(RunState)typeof(CampaignRun).GetField("host",Fields).GetValue(run);
            host.DebugSetRemaining(.01f);host.SetRestTimerPaused(false);host.Tick(1);
            typeof(CampaignRun).GetMethod("Apply",Fields).Invoke(run,new object[]{host.Snapshot});
        }
        else
        {
            DevCheats.HoldCheckpointWipe=false;
            foreach(var player in NetworkPlayer.All)if(player.IsSpawned)player.ServerSetState(PlayerState.Retired);
        }
        await Until(()=>FailureEndingPresentation.Current!=null&&FailureEndingPresentation.Current.Ready,"Failure ready",30);
        typeof(DevMode).GetField("_enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
        var view=FailureEndingPresentation.Current.GetComponent<EscapeEndingResults>().View;
        if(view.ReturnLabel.text!="다음 런 시작")throw new Exception("Wrong failure action label");
        return new{day=CampaignRun.Instance.State.Day,generation=CampaignRun.Instance.State.Generation,reason=CampaignRun.Instance.State.LastEnd.ToString(),button=view.ReturnLabel.text,session=NetworkSessionManager.Instance.GameState.ToString(),mastered};
    }
    public static async Task<object> Restart(bool mastered,string capture)
    {
        var ending=FailureEndingPresentation.Current;var previous=CampaignRun.Instance.State;
        ending.GetComponent<EscapeEndingResults>().View.ReturnButton.onClick.Invoke();
        await Until(()=>CampaignRun.Instance.State.Generation==previous.Generation+1&&SpaceLinkDirector.Instance.Current==SpaceId.WaitingRoom&&!SpaceLinkDirector.Instance.IsTransitioning&&!ending.IsPresenting,"New first morning",100);
        await Until(()=>!SceneCurtainTransition.IsActive,"Curtain clears",20);
        await Until(()=>UnityEngine.Object.FindObjectsByType<HudEventAppearance>(FindObjectsSortMode.None).Any(a=>a.Kind==HudEventKind.Warning&&a.GetComponent<HudRoleCard>().Title=="탈출 실패"),"Red failure system UI",15);
        await Task.Delay(350);
        string path=Path.GetFullPath("Tasks/Active/Task-20261114-131-escape-success-art/Previews/"+capture+".png");Directory.CreateDirectory(Path.GetDirectoryName(path));ScreenCapture.CaptureScreenshot(path);
        await Task.Delay(700);
        var state=CampaignRun.Instance.State;var player=NetworkPlayer.Local;
        bool blocked=((System.Collections.Generic.HashSet<object>)typeof(InputGate).GetField("blockers",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Contains(ending);
        var book=typeof(WaitingRoomTutorial).GetProperty("Book",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        bool seen=(bool)book.GetType().GetMethod("IsDone").Invoke(book,new object[]{"G11"});
        bool unseen=(bool)book.GetType().GetMethod("IsDone").Invoke(book,new object[]{"N02"});
        if(state.Day!=1||state.Phase!=CampaignPhase.RestTime||state.Rest!=RestKind.Morning||state.CannonUnlocked||blocked
            ||player.State!=PlayerState.Normal||!player.GetComponent<PlayerMover>().ControlEnabled||!seen||unseen
            ||!WaitingRoomRehearsal.HasMastered||WaitingRoomCannonRehearsal.HasMastered!=mastered
            ||NetworkSessionManager.Instance.GameState!=GameSessionState.Playing)throw new Exception("Failure restart invariant failed");
        return new{scene=SceneManager.GetActiveScene().name,state.Generation,state.Day,phase=state.Phase.ToString(),rest=state.Rest.ToString(),state.CannonUnlocked,
            player=player.State.ToString(),control=player.GetComponent<PlayerMover>().ControlEnabled,seenGuideRetained=seen,unseenGuideStillAvailable=!unseen,
            ropeLearned=WaitingRoomRehearsal.HasMastered,cannonLearned=WaitingRoomCannonRehearsal.HasMastered,session=NetworkSessionManager.Instance.GameState.ToString(),capture=path};
    }
}
