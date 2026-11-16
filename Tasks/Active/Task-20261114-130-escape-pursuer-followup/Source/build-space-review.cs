using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
public static class SpaceBuildPlayer {
 public static object Run()=>Queue("build","space-build-result.json");
 public static object RunFinal()=>Queue("build-final","space-final-build-result.json");
 public static object RunPressure()=>Queue("build-pressure","space-pressure-build-result.json");
 public static object RunCoverage()=>Queue("build-coverage","space-coverage-build-result.json");
 static object Queue(string directory,string resultFile){
  if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty||SceneManager.GetActiveScene().path!="Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototype.unity")throw new Exception("Saved prototype edit scene required");
  foreach(var pending in EditorApplication.delayCall?.GetInvocationList()??Array.Empty<Delegate>()) if(pending.Method.DeclaringType.FullName.StartsWith("SpaceBuildPlayer")) EditorApplication.delayCall-=(EditorApplication.CallbackFunction)pending; Action build=()=>{
   const string root="C:/Unity/POC_CURTAINCALL/.harness/local/pursuer-space-20261116";
   Directory.CreateDirectory(root+"/"+directory);
   try{var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototype.unity","Assets/Scenes/UI/CommonPerformanceUI.unity","Assets/Scenes/UI/CommonInteractionUI.unity","Assets/Scenes/UI/CommonMenuUI.unity","Assets/Scenes/InGame/Lobby.unity"},locationPathName=root+"/"+directory+"/PursuerCampaign.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
    File.WriteAllText(root+"/"+resultFile,Newtonsoft.Json.JsonConvert.SerializeObject(new{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,seconds=report.summary.totalTime.TotalSeconds,steps=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.SelectMany(report.steps,s=>System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(s.messages,m=>m.type==LogType.Error||m.type==LogType.Exception),m=>m.content)))}));
   }catch(Exception e){File.WriteAllText(root+"/"+resultFile,Newtonsoft.Json.JsonConvert.SerializeObject(new{error=e.ToString()}));}
  }; double after=EditorApplication.timeSinceStartup+5; EditorApplication.CallbackFunction callback=null; callback=()=>{if(EditorApplication.timeSinceStartup<after)return;EditorApplication.update-=callback;build();};EditorApplication.update+=callback;return "Full prototype Development build queued";
 }
}
