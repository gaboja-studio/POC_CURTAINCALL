using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using CurtainCall.EscapeRoute;
using CurtainCall.EscapeRoute.EditorTools;
using Newtonsoft.Json;
public static class CloseTransferFront
{
    const string Folder=".harness/local/pursuer-space-20261116/";
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","");}
    static object Sample(Transform frame,Vector3 local)
    {
        bool found=NavMesh.SamplePosition(frame.TransformPoint(local),out var hit,.2f,NavMesh.AllAreas);
        return new{local,found,hitLocal=found?frame.InverseTransformPoint(hit.position):Vector3.zero};
    }
    sealed class Vec : JsonConverter
    {
        public override bool CanConvert(Type t)=>t==typeof(Vector3);
        public override void WriteJson(JsonWriter w,object value,JsonSerializer serializer){var v=(Vector3)value;w.WriteStartObject();w.WritePropertyName("x");w.WriteValue(v.x);w.WritePropertyName("y");w.WriteValue(v.y);w.WritePropertyName("z");w.WriteValue(v.z);w.WriteEndObject();}
        public override object ReadJson(JsonReader r,Type t,object existing,JsonSerializer serializer)=>throw new NotSupportedException();
    }
    public static object Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(Application.isPlaying||scene.isDirty||scene.path!=EscapeBackgroundBuilder.LabyrinthScenePath)throw new Exception("Clean stopped Prototype required");
        string[] protectedFiles={"Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodel.unity","Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodelNavigation.asset","Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAChaseNavigation.asset"};
        var hashes=protectedFiles.Where(File.Exists).ToDictionary(p=>p,Hash);
        var frame=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
        var module=frame.GetComponentInChildren<EscapePursuerRoute>(true);var surface=module.GetComponent<NavMeshSurface>();
        if(!surface)throw new Exception("Prototype navigation surface missing");
        const string prototypeNav="Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototypeNavigation.asset";
        if(AssetDatabase.GetAssetPath(surface.navMeshData)!=prototypeNav)throw new Exception("Expected existing dedicated Prototype navigation asset");
        var sources=new List<NavMeshBuildSource>();
        UnityEngine.AI.NavMeshBuilder.CollectSources(module.transform.root,surface.layerMask,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
        sources.RemoveAll(s=>s.component is Collider c&&(c.isTrigger||c.GetComponentInParent<EscapeUnderstageChase>()||c.name=="Personnel light curtain"||c.name=="Counterweighted shutter"));
        sources.RemoveAll(s=>s.component&&s.component.TryGetComponent<NavMeshModifier>(out var m)&&m.isActiveAndEnabled&&m.ignoreFromBuild&&m.AffectsAgentType(surface.agentTypeID));
        var removedSources=sources.Where(s=>s.component is BoxCollider b&&b.name=="Concrete remainder"&&Mathf.Abs(frame.InverseTransformPoint(b.bounds.center).x-74.4f)<1&&Mathf.Abs(frame.InverseTransformPoint(b.bounds.max).y+23)<.02f&&b.bounds.size.y>60&&((b.transform.parent==frame.Find("Pole feedback architecture")&&Mathf.Abs(b.bounds.size.z-57.4f)<.02f)||(b.transform.parent==frame.Find("EscapeBackground/70_LabyrinthRemake/82_IndependentTransferRoutes")&&Mathf.Abs(b.bounds.size.z-6.4f)<.02f))).Select(s=>s.component).Distinct().ToArray();
        if(!frame.Find("EscapeBackground/70_LabyrinthRemake/83_TransferFrontClosure")&&removedSources.Length!=2)throw new Exception("Expected exactly two collected old support sources, got "+removedSources.Length);
        var eyes=new[]{new Vector3(67,-19.5f,2),new Vector3(80.714f,-20.7f,4.3f),new Vector3(80.714f,-22.7f,0)};
        var serviceTargets=new List<Vector3>();
        for(int i=0;i<module.Points.Count-1;i++)
        {
            if(module.Points[i].exposed||module.Points[i+1].exposed)continue;
            var a=module.Points[i].feet.position;var b=module.Points[i+1].feet.position;int count=Mathf.CeilToInt(Vector3.Distance(a,b));
            for(int j=0;j<=count;j++){var at=Vector3.Lerp(a,b,(float)j/Mathf.Max(count,1));if(Vector3.Distance(frame.InverseTransformPoint(at),new Vector3(75,-23,0))<60)serviceTargets.Add(at+Vector3.up*1.5f);}
        }
        var rays=eyes.SelectMany(e=>serviceTargets.Select(t=>(from:frame.TransformPoint(e),to:t))).ToArray();
        bool Blocked((Vector3 from,Vector3 to) r)=>Physics.RaycastAll(r.from,(r.to-r.from).normalized,Vector3.Distance(r.from,r.to)-.05f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject.scene==scene);
        var blockedBefore=rays.Select(Blocked).ToArray();
        var samplePoints=new[]{new Vector3(75,-23,0),new Vector3(72.2814f,-23,2.560004f)};
        var beforeSamples=samplePoints.Select(p=>Sample(frame,p)).ToArray();
        if(!File.Exists(Folder+"TransferFront-before.unity"))File.Copy(scene.path,Folder+"TransferFront-before.unity");
        if(!File.Exists(Folder+"TransferFront-before-navigation.asset"))File.Copy(prototypeNav,Folder+"TransferFront-before-navigation.asset");
        var collectedSources=removedSources.Select(c=>new{name=c.name,id=GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()}).ToArray();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();
        try
        {
            var change=EscapeBackgroundBuilder.RefineLabyrinthTransferFront();Physics.SyncTransforms();
            int newlyVisible=rays.Select((r,i)=>blockedBefore[i]&&!Blocked(r)).Count(v=>v);
            if(newlyVisible>0)throw new Exception("Removal newly exposes service-route views: "+newlyVisible);
            var audit=EscapeLabyrinthAudit.Audit(frame.Find("EscapeBackground/70_LabyrinthRemake"),frame);
            if(audit.failures.Count>0)throw new Exception(string.Join("\n",audit.failures));
            bool rebaked=removedSources.Length>0;
            if(rebaked)UnderstageChaseNavigation.Bake();
            var afterSamples=samplePoints.Select(p=>Sample(frame,p)).ToArray();
            if(rebaked&&samplePoints.Any(p=>NavMesh.SamplePosition(frame.TransformPoint(p),out var hit,.2f,NavMesh.AllAreas)))throw new Exception("Deleted front shelf still has a navigation surface");
            foreach(var pair in hashes)if(Hash(pair.Key)!=pair.Value)throw new Exception("Protected source asset changed: "+pair.Key);
            if(AssetDatabase.GetAssetPath(surface.navMeshData)!=prototypeNav)throw new Exception("Navigation data rebound outside Prototype");
            EditorSceneManager.SaveScene(scene);
            var report=new{change,visibility=new{eyes,serviceTargets=serviceTargets.Count,rayPairs=rays.Length,newlyVisible},navigation=new{collectedSources,rebaked,asset=prototypeNav,bakeLocalBounds=new{min=new Vector3(-20,-24.5f,-17),max=new Vector3(80,20.5f,93)},beforeSamples,afterSamples},auditFailures=audit.failures,auditWarnings=audit.warnings.Count,audit.colliders,audit.ropes,protectedFilesUnchanged=hashes,sourceSceneSha256=Hash(scene.path),scope="Removed only the redundant/unintended front shelf and its deep volume at local Z<4. Retained the exact old rear support subset at Z>=4. No added solid volume, high wall never applied. Geometry validation is not a new continuous gameplay pass; earlier standalone runs used the previous shelf."};
            File.WriteAllText(Folder+"transfer-front-closure.json",JsonConvert.SerializeObject(report,Formatting.Indented,new JsonSerializerSettings{Converters=new List<JsonConverter>{new Vec()}}));
            File.WriteAllText(Folder+"return-audit.json",JsonUtility.ToJson(audit,true));return new{saved=true,change.removedBoxes,change.createdBoxes,change.launchSamples,change.checkedCannons,change.supportedFloorSamples,change.removedShelfSamples,navRebaked=rebaked,collectedSources=collectedSources.Length,visibilityRayPairs=rays.Length,newlyVisible,auditFailures=audit.failures.Count,auditWarnings=audit.warnings.Count,audit.colliders,audit.ropes};
        }
        catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}
