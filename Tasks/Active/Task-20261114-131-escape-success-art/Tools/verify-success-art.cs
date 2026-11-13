using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.SpaceLink;

public static class SuccessArtVerification
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static async Task Until(Func<bool> ready,string label,int seconds=100)
    {var deadline=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>deadline)throw new Exception(label);await Task.Delay(100);}}
    public static object Prepare()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/InGame/Start.unity");
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
        return new{scene=SceneManager.GetActiveScene().name};
    }
    public static async Task<object> Launch(int model=0,int outfit=2)
    {
        typeof(DevMode).GetField("_enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        Application.runInBackground=true;Time.timeScale=1;
        var session=CurtainCall.Network.Session.NetworkSessionManager.Instance;
        if(session!=null&&session.GameState==CurtainCall.Network.Session.GameSessionState.Ended
            &&SpaceLinkDirector.Instance.Current==SpaceId.Hub)session.ReturnToLobby();
        if(session!=null&&session.GameState==CurtainCall.Network.Session.GameSessionState.Waiting&&NetworkPlayer.Local!=null)
        {
            NetworkPlayer.Local.RequestAppearance(model,outfit);
            await Until(()=>NetworkPlayer.Local.AppearanceModel==model&&NetworkPlayer.Local.AppearanceOutfit==outfit,"Lobby appearance selection",10);
        }
        var launch=DevStartLauncher.EnsureExists();
        if(!launch.TryLaunch(new RunStartPoint(3,RunStartPhase.Escape,0,CampaignSnapshot.TightropeId)))throw new Exception(launch.Status);
        await Until(()=>!launch.IsBusy,"Escape load",150);
        if(!launch.LastLaunchSucceeded)throw new Exception(launch.Status);
        await Until(()=>NetworkPlayer.Local!=null&&NetworkPlayer.Local.IsSpawned,"Player spawn");
        await Task.Delay(1500);
        if(NetworkPlayer.Local.AppearanceModel!=model||NetworkPlayer.Local.AppearanceOutfit!=outfit)throw new Exception("Select the requested variation in the lobby before launching.");
        return new {scene=SceneManager.GetActiveScene().name,day=CampaignRun.Instance.State.Day,model,outfit};
    }
    public static async Task<object> Escape()
    {
        var content=EscapeCampaignContent.Current;
        if(!content.DevOpenExit(out string why))throw new Exception(why);
        var area=(BoxCollider)typeof(EscapeCampaignContent).GetField("arrivalArea",Fields).GetValue(content);
        NetworkPlayer.Local.GetComponent<PlayerMover>().Relocate(area.transform.TransformPoint(area.center),Quaternion.identity);
        await Until(()=>CampaignRun.Instance.State.LastEnd==RunEndReason.EscapeSucceeded,"Actual exit arrival",25);
        var result=EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>();
        await Until(()=>result.View!=null&&result.View.Visible,"Results",25);
        typeof(DevMode).GetField("_enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
        await Task.Delay(1000);
        var art=UnityEngine.Object.FindFirstObjectByType<EscapeEndingArt>();
        if(art==null)throw new Exception("Ending art not instantiated");
        var shaders=NetworkPlayer.Local.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.shader.name).Distinct().ToArray();
        if(!shaders.Any(n=>n.StartsWith("CurtainCall/Endings/Survivor")))throw new Exception("Costume wear not applied");
        return new{title=result.View.EndingTitle.text,people=result.People.Count,fog=RenderSettings.fogMode.ToString(),fogColor=RenderSettings.fogColor.ToString(),shaders};
    }
    public static async Task<object> LaunchAndEscape(int model=0,int outfit=2)
    {
        await Launch(model,outfit);
        return await Escape();
    }
    public static object View(bool visible,bool statistics=false)
    {
        var view=EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>().View;
        view.SelectTab(statistics);view.Group.alpha=visible?1:0;
        return new{visible,statistics};
    }
    public static async Task<object> Running(int seconds=12)
    {
        var player=NetworkPlayer.Local;var start=player.transform.position;int frame=Time.frameCount;
        var rig=UnityEngine.Object.FindFirstObjectByType<EscapeEndingCinematic>();
        var tileRoots=(Transform[])typeof(EscapeEndingCinematic).GetField("meadowTiles",Fields).GetValue(rig);
        var initial=tileRoots.Select(t=>t.position).ToArray();
        await Task.Delay(seconds*1000);
        float moved=Vector3.Distance(start,player.transform.position);
        if(moved<seconds*.5f||Time.frameCount-frame<seconds*5)throw new Exception("Running stalled");
        if(player.transform.position.y<start.y-1)throw new Exception("Actor fell through recycled field");
        return new{seconds,distance=moved,frames=Time.frameCount-frame,recycled=tileRoots.Where((t,i)=>Vector3.Distance(t.position,initial[i])>1).Count(),actorY=player.transform.position.y,details=tileRoots.Select(t=>t.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)).ToArray()};
    }
    public static async Task<object> Return()
    {
        var owner=EscapeEndingPresentation.Current;
        owner.GetComponent<EscapeEndingResults>().View.ReturnButton.onClick.Invoke();
        await Until(()=>SpaceLinkDirector.Instance.Current==SpaceId.Hub&&!SpaceLinkDirector.Instance.IsTransitioning,"Lobby return");await Task.Delay(1000);
        var player=NetworkPlayer.Local;var motor=player.GetComponent<PlayerMover>();
        bool leaked=((System.Collections.Generic.HashSet<object>)typeof(InputGate).GetField("blockers",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Contains(owner);
        var shaders=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.shader.name).Distinct().ToArray();
        if(leaked||!motor.ControlEnabled||player.State!=PlayerState.Normal||shaders.Any(n=>n.StartsWith("CurtainCall/Endings/Survivor")))throw new Exception("Ending state leaked into lobby");
        return new{scene=SceneManager.GetActiveScene().name,control=motor.ControlEnabled,state=player.State.ToString(),leaked,shaders,fog=RenderSettings.fogMode.ToString()};
    }
    public static object Assets()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Endings/SuccessEndingArt.prefab");
        if(prefab==null)throw new Exception("Missing prefab");
        var art=new SerializedObject(prefab.GetComponent<EscapeEndingArt>());
        foreach(string name in new[]{"sourceShaders","wornShaders"})
        {
            var array=art.FindProperty(name);if(array.arraySize!=4)throw new Exception("Four character mappings required");
            for(int i=0;i<4;i++)if(array.GetArrayElementAtIndex(i).objectReferenceValue==null)throw new Exception(name+" missing shader "+i);
        }
        string[] shaderPaths=AssetDatabase.FindAssets("t:Shader",new[]{"Assets/Scripts/EscapeRoute/Shaders"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("Success")||p.Contains("Survivor")).ToArray();
        var errors=shaderPaths.SelectMany(p=>ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>(p)).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>p+": "+m.message)).ToArray();
        if(errors.Length>0)throw new Exception(string.Join("\n",errors));
        return new{prefab=AssetDatabase.GetAssetPath(prefab),mappedModels=4,shaders=shaderPaths.Length,shaderErrors=errors.Length};
    }
}
