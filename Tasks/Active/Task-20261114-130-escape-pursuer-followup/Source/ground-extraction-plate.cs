using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using CurtainCall.EscapeRoute.EditorTools;using Newtonsoft.Json;
public static class GroundExtractionPlate
{
 const string Folder=".harness/local/pursuer-space-20261116/";
 static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","");}
 sealed class Vec:JsonConverter{public override bool CanConvert(Type t)=>t==typeof(Vector3);public override void WriteJson(JsonWriter w,object value,JsonSerializer serializer){var v=(Vector3)value;w.WriteStartArray();w.WriteValue(v.x);w.WriteValue(v.y);w.WriteValue(v.z);w.WriteEndArray();}public override object ReadJson(JsonReader r,Type t,object existing,JsonSerializer serializer)=>throw new NotSupportedException();}
 public static object Apply()
 {
  var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty||scene.path!=EscapeBackgroundBuilder.LabyrinthScenePath)throw new Exception("Clean stopped Prototype required");
  string[] protectedFiles={"Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodel.unity","Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodelNavigation.asset","Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAChaseNavigation.asset","Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototypeNavigation.asset"};var hashes=protectedFiles.ToDictionary(p=>p,Hash);
  if(!File.Exists(Folder+"ExtractionPlate-before.unity"))File.Copy(scene.path,Folder+"ExtractionPlate-before.unity");
  Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();try
  {
   var change=EscapeBackgroundBuilder.GroundLabyrinthExtractionPlate();var second=EscapeBackgroundBuilder.GroundLabyrinthExtractionPlate();if(!second.alreadyApplied)throw new Exception("Plate fit is not idempotent");
   var frame=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
   var navBounds=new Bounds(new Vector3(30,-2,38),new Vector3(100,45,110));var plateBounds=new Bounds();plateBounds.SetMinMax(change.afterMin,change.afterMax);
   if(navBounds.Intersects(plateBounds))throw new Exception("Plate unexpectedly inside authored navigation bake bounds; reassess rebake");
   var audit=EscapeLabyrinthAudit.Audit(frame.Find("EscapeBackground/70_LabyrinthRemake"),frame);if(audit.failures.Count>0)throw new Exception(string.Join("\n",audit.failures));
   foreach(var pair in hashes)if(Hash(pair.Key)!=pair.Value)throw new Exception("Protected asset changed: "+pair.Key);
   EditorSceneManager.SaveScene(scene);
   var report=new{change,secondRunAlreadyApplied=second.alreadyApplied,navigation=new{rebaked=false,reason="Plate X139.536..141.6 is outside UnderstageChaseNavigation local bake bounds X[-20,80]; top and footprint are unchanged. Prototype and shared navigation data hashes preserved.",bakeMin=navBounds.min,bakeMax=navBounds.max},protectedFilesUnchanged=hashes,sourceSceneSha256=Hash(scene.path),auditFailures=audit.failures.Count,auditWarnings=audit.warnings.Count,audit.colliders,audit.ropes,scope="Prototype scene-instance transform only. Existing Cube mesh and renderer/collider footprint remain identical; bottom meets the existing foundation. Top surface and standing/crouched body contacts unchanged. No runtime/gameplay-rule change and no new continuous-play claim."};
   File.WriteAllText(Folder+"extraction-plate-grounding.json",JsonConvert.SerializeObject(report,Formatting.Indented,new JsonSerializerSettings{Converters=new List<JsonConverter>{new Vec()}}));File.WriteAllText(Folder+"return-audit.json",JsonUtility.ToJson(audit,true));return new{saved=true,change.alreadyApplied,secondRunAlreadyApplied=second.alreadyApplied,change.supportedSamples,change.unchangedBodySamples,change.maxTopDifference,change.topAndFootprintPreserved,change.meshMatchesCollider,navRebaked=false,auditFailures=audit.failures.Count,auditWarnings=audit.warnings.Count};
  }catch{Undo.RevertAllDownToGroup(undo);throw;}
 }
}
