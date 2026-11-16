using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;using CurtainCall.EscapeRoute;using CurtainCall.EscapeRoute.EditorTools;
public static class PolishStairEdge {
 public static object Exit()=>RunMethod("FixFinishedThermalExitEdge", "stair-exit-apply.json");
 public static object Run(){
  return RunMethod("FixFinishedThermalDescentEdge", "stair-edge-apply.json");
 }
 static object RunMethod(string methodName, string reportName){
  var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlayingOrWillChangePlaymode||scene.isDirty||scene.path!=EscapeBackgroundBuilder.LabyrinthScenePath)throw new Exception("Clean stopped prototype required");
  var f=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
  var route=f.GetComponentInChildren<EscapePursuerRoute>(true);var chase=route.GetComponentInChildren<EscapeUnderstageChase>(true);var walls=route.GetComponent<EscapePursuerWalls>();
  var rows=new SerializedObject(chase).FindProperty("encounters");if(rows.arraySize!=16)throw new Exception("Registry changed");
  var jumps=new List<(Vector3 a,Vector3 b)>();for(int i=0;i<rows.arraySize;i++){var row=rows.GetArrayElementAtIndex(i);var a=(row.FindPropertyRelative("exit").objectReferenceValue as Transform).position;var b=(row.FindPropertyRelative("landing").objectReferenceValue as Transform).position;jumps.Add((a,b));jumps.Add((b,a));}
  bool[] Check(){var barriers=Enumerable.Range(0,walls.GateCount).Select(i=>walls.At(i)?.barrier).Where(b=>b).Distinct().ToArray();var states=barriers.Select(b=>b.enabled).ToArray();try{foreach(var b in barriers)b.enabled=false;Physics.SyncTransforms();return jumps.Select(j=>EscapePursuerNavigation.ClearJump(chase.transform,j.a,j.b)).ToArray();}finally{for(int i=0;i<barriers.Length;i++)barriers[i].enabled=states[i];Physics.SyncTransforms();}}
  var before=Check();if(before.Any(x=>!x))throw new Exception("Existing jump baseline is not clear");
  Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();
  try{var method=typeof(EscapeBackgroundBuilder).GetMethod(methodName);if(method==null)throw new Exception("Fix method not compiled");object fix=method.Invoke(null,null);var after=Check();if(after.Any(x=>!x))throw new Exception("Guard blocks actual encounter jump: "+string.Join(",",after.Select((v,i)=>(v,i)).Where(t=>!t.v).Select(t=>t.i)));var result=new{success=true,clearBefore=before.Count(x=>x),clearAfter=after.Count(x=>x),fix};EditorSceneManager.SaveScene(scene);File.WriteAllText(".harness/local/pursuer-polish-20261116/"+reportName,Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;}
  catch{Undo.RevertAllDownToGroup(undo);Physics.SyncTransforms();throw;}
 }
}
