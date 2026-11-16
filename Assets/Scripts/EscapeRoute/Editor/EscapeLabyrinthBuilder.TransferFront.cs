using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        [Serializable] public sealed class TransferFrontReport
        {
            public bool alreadyApplied,noAddedSolidVolume,retainedRearSupport,primitiveMeshMatchesCollider;
            public int removedBoxes,createdBoxes,launchSamples,supportedFloorSamples,removedShelfSamples;
            public float originalUnionVolume,retainedVolume,removedUnionVolume;
            public string[] checkedCannons;
            public Vector3 retainedMin,retainedMax;
            public string rejectedDesign="High wall NOT applied: it obscured 239-241 of 659 A-cannon viewing phases per loop.";
        }
        public static TransferFrontReport RefineLabyrinthTransferFront()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=LabyrinthScenePath)throw new InvalidOperationException("Stopped Prototype required");
            var frame=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            var root=frame.Find("EscapeBackground/70_LabyrinthRemake");
            const string groupName="83_TransferFrontClosure",pieceName="Transfer retained rear deep support";
            Bounds BoundsOf(BoxCollider b)=>new(frame.InverseTransformPoint(b.transform.TransformPoint(b.center)),Vector3.Scale(b.size,b.transform.lossyScale));
            var existing=root.Find(groupName);var report=new TransferFrontReport();BoxCollider retained;int undo=-1;
            try
            {
                if(existing)
                {
                    var parts=existing.GetComponentsInChildren<BoxCollider>(true);
                    if(parts.Length!=1||parts[0].name!=pieceName)throw new InvalidOperationException("Unexpected transfer-front revision");
                    retained=parts[0];report.alreadyApplied=true;
                }
                else
                {
                    BoxCollider Find(Transform parent,Vector3 min,Vector3 max)=>parent.GetComponentsInChildren<BoxCollider>(true).Single(b=>b.transform.parent==parent&&b.name=="Concrete remainder"&&Vector3.Distance(BoundsOf(b).min,min)<.02f&&Vector3.Distance(BoundsOf(b).max,max)<.02f);
                    var a=Find(frame.Find("Pole feedback architecture"),new(71.6f,-90,-3.2f),new(77.2f,-23,54.2f));
                    var b=Find(root.Find("82_IndependentTransferRoutes"),new(71.6f,-90,-3.2f),new(78.414f,-23,3.2f));
                    foreach(var box in new[]{a,b})CheckTransferPrimitive(box,frame);
                    var ba=BoundsOf(a);var bb=BoundsOf(b);var rear=new Bounds();rear.SetMinMax(new(ba.min.x,ba.min.y,4),ba.max);
                    var intersection=new Bounds();intersection.SetMinMax(Vector3.Max(ba.min,bb.min),Vector3.Min(ba.max,bb.max));
                    float Volume(Bounds x)=>x.size.x*x.size.y*x.size.z;
                    report.originalUnionVolume=Volume(ba)+Volume(bb)-Volume(intersection);report.retainedVolume=Volume(rear);report.removedUnionVolume=report.originalUnionVolume-report.retainedVolume;
                    // Exact retained old union at Z>=4; no newly introduced collision or occlusion.
                    if(rear.min.x<ba.min.x||rear.max.x>ba.max.x||rear.min.y<ba.min.y||rear.max.y>ba.max.y||rear.min.z<ba.min.z||rear.max.z>ba.max.z||bb.max.z>=4)throw new InvalidOperationException("Rear support is not a subset of the old union");
                    Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Remove unused Prototype transfer shelf");
                    var group=new GameObject(groupName);Undo.RegisterCreatedObjectUndo(group,"Transfer cleanup root");group.transform.SetParent(root,false);
                    var go=Object.Instantiate(a.gameObject,group.transform);Undo.RegisterCreatedObjectUndo(go,"Retain rear foundation");go.name=pieceName;
                    go.transform.SetPositionAndRotation(frame.TransformPoint(rear.center),frame.rotation);go.transform.localScale=rear.size;
                    GameObjectUtility.SetStaticEditorFlags(go,GameObjectUtility.GetStaticEditorFlags(a.gameObject));retained=go.GetComponent<BoxCollider>();
                    Undo.DestroyObjectImmediate(a.gameObject);Undo.DestroyObjectImmediate(b.gameObject);report.removedBoxes=2;report.createdBoxes=1;Physics.SyncTransforms();
                }
                CheckTransferPrimitive(retained,frame);var finalBounds=BoundsOf(retained);report.retainedMin=finalBounds.min;report.retainedMax=finalBounds.max;
                if(Vector3.Distance(finalBounds.min,new(71.6f,-90,4))>.02f||Vector3.Distance(finalBounds.max,new(77.2f,-23,54.2f))>.02f)throw new InvalidOperationException("Retained support dimensions changed");
                report.noAddedSolidVolume=report.retainedRearSupport=report.primitiveMeshMatchesCollider=true;
                for(float x=71.91f;x<78.114f;x+=.4f)for(float z=-2.89f;z<3.7f;z+=.4f)
                {
                    var p=frame.TransformPoint(new Vector3(x,-23,z));
                    if(Physics.RaycastAll(p+Vector3.up*.2f,Vector3.down,.4f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject.scene==scene&&h.normal.y>.9f))throw new InvalidOperationException("Walkable shelf remains at "+frame.InverseTransformPoint(p));
                    report.removedShelfSamples++;
                }
                var floorSamples=new[]{new Vector3(69.6f,-22,2),new Vector3(74,-22,7.5f),new Vector3(80.714f,-22,4.3f),new Vector3(80.714f,-24,-7.35f),new Vector3(52,-19.2f,-60.5f),new Vector3(62,-19.2f,-60.5f),new Vector3(77,-19.2f,-43)};
                foreach(var p in floorSamples)
                {
                    var world=frame.TransformPoint(p);
                    if(!Physics.RaycastAll(world+Vector3.up*.15f,Vector3.down,.3f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject.scene==scene&&h.normal.y>.9f&&Mathf.Abs(h.point.y-world.y)<.04f))throw new InvalidOperationException("Choice floor height/support changed: "+p);
                    report.supportedFloorSamples++;
                }
                var stations=frame.GetComponentsInChildren<HumanCannonStation>(true);
                var cannons=new[]{"A04B_ConveyorCannon","Labyrinth_03_ProcessingLaunch"}.Select(n=>stations.Single(c=>c.name==n&&c.isActiveAndEnabled)).ToArray();
                report.checkedCannons=cannons.Select(c=>c.name).ToArray();var rules=GameSettings.Base.Character;
                foreach(var cannon in cannons)
                {
                    var start=cannon.Muzzle.position;var velocity=cannon.LaunchVelocity;var previous=start;
                    for(int i=0;i<=300;i++)
                    {
                        float t=i*.02f;var p=start+velocity*t-Vector3.up*(.5f*rules.Gravity*t*t);var delta=p-previous;
                        var lower=Vector3.up*(rules.CapsuleRadius+.01f);var upper=Vector3.up*(rules.CapsuleHeight-rules.CapsuleRadius+.01f);
                        if(Physics.OverlapCapsule(p+lower,p+upper,rules.CapsuleRadius,~0,QueryTriggerInteraction.Ignore).Contains(retained)||delta.sqrMagnitude>.000001f&&Physics.CapsuleCastAll(previous+lower,previous+upper,rules.CapsuleRadius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider==retained))throw new InvalidOperationException("Retained support intersects launch: "+cannon.name);
                        previous=p;report.launchSamples++;
                    }
                }
                if(undo>=0){EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(undo);}return report;
            }
            catch{if(undo>=0)Undo.RevertAllDownToGroup(undo);throw;}
        }
        [Serializable] public sealed class ExtractionPlateReport
        {
            public bool alreadyApplied,topAndFootprintPreserved,meshMatchesCollider;
            public int supportedSamples,unchangedBodySamples;
            public Vector3 beforeMin,beforeMax,afterMin,afterMax;
            public float foundationTop,maxTopDifference;
        }
        public static ExtractionPlateReport GroundLabyrinthExtractionPlate()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=LabyrinthScenePath)throw new InvalidOperationException("Stopped Prototype required");
            var frame=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            Bounds Local(BoxCollider b)=>new(frame.InverseTransformPoint(b.transform.TransformPoint(b.center)),Vector3.Scale(b.size,b.transform.lossyScale));
            var plate=frame.Find("EscapePursuer_A/Concealed pursuit structure").GetComponentsInChildren<BoxCollider>(true).Single(b=>b.name=="Concrete remainder"&&Vector3.Distance(Local(b).max,new(141.6f,.2f,85))<.01f&&Mathf.Abs(Local(b).min.x-139.536224f)<.01f&&Mathf.Abs(Local(b).min.z-83.11615f)<.01f);
            var foundation=frame.Find("EscapeBackground/70_LabyrinthRemake").GetComponentsInChildren<BoxCollider>(true).Single(b=>b.name=="타설 콘크리트 벽 fitted return"&&Vector3.Distance(Local(b).max,new(142.5f,-.05f,85))<.01f&&Vector3.Distance(Local(b).min,new(139.5f,-32.1f,70.5f))<.01f);
            CheckTransferPrimitive(plate,frame);CheckTransferPrimitive(foundation,frame);
            var before=Local(plate);float bottom=Local(foundation).max.y;
            var report=new ExtractionPlateReport{beforeMin=before.min,beforeMax=before.max,foundationTop=bottom,alreadyApplied=Mathf.Abs(before.min.y-bottom)<.0001f};
            if(!report.alreadyApplied&&Mathf.Abs(before.min.y-.05f)>.001f)throw new InvalidOperationException("Unexpected extraction plate lower face");
            var samples=Enumerable.Range(0,9).Select(i=>frame.TransformPoint(new Vector3(Mathf.Lerp(before.min.x,before.max.x,.1f+.4f*(i%3)),before.max.y,Mathf.Lerp(before.min.z,before.max.z,.1f+.4f*(i/3))))).ToArray();
            int[] Body(Vector3 p,float height)=>Physics.OverlapCapsule(p+Vector3.up*(GameSettings.Base.Character.CapsuleRadius+.01f),p+Vector3.up*(height-GameSettings.Base.Character.CapsuleRadius+.01f),GameSettings.Base.Character.CapsuleRadius,~0,QueryTriggerInteraction.Ignore).Select(c=>c.GetInstanceID()).OrderBy(i=>i).ToArray();
            var heights=new[]{GameSettings.Base.Character.CapsuleHeight,GameSettings.Base.Character.CapsuleHeight*GameSettings.Base.Character.CrouchHeightRatio};
            var bodies=samples.SelectMany(p=>heights.Select(h=>Body(p,h))).ToArray();
            var renderer=plate.GetComponent<MeshRenderer>();var sharedMesh=plate.GetComponent<MeshFilter>().sharedMesh;
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();
            try
            {
                if(!report.alreadyApplied)
                {
                    Undo.RecordObject(plate.transform,"Ground Prototype extraction plate");
                    var after=new Bounds();after.SetMinMax(new(before.min.x,bottom,before.min.z),before.max);
                    plate.transform.position=frame.TransformPoint(after.center);plate.transform.localScale=after.size;Physics.SyncTransforms();
                }
                var final=Local(plate);report.afterMin=final.min;report.afterMax=final.max;
                if(Vector3.Distance(before.max,final.max)>.0001f||Mathf.Abs(before.min.x-final.min.x)>.0001f||Mathf.Abs(before.min.z-final.min.z)>.0001f||Mathf.Abs(final.min.y-bottom)>.0001f)throw new InvalidOperationException("Extraction plate top/footprint or base changed unexpectedly");
                if(plate.GetComponent<MeshFilter>().sharedMesh!=sharedMesh||Vector3.Distance(renderer.bounds.center,plate.bounds.center)>.0001f||Vector3.Distance(renderer.bounds.size,plate.bounds.size)>.0001f)throw new InvalidOperationException("Extraction plate mesh/collider mismatch");
                report.topAndFootprintPreserved=report.meshMatchesCollider=true;
                for(int i=0;i<samples.Length;i++)
                {
                    var p=samples[i];
                    if(!plate.Raycast(new Ray(p+Vector3.up*.1f,Vector3.down),out var hit,.2f))throw new InvalidOperationException("Extraction plate top support missing");
                    report.maxTopDifference=Mathf.Max(report.maxTopDifference,Mathf.Abs(hit.point.y-p.y));
                    var basePoint=p; basePoint.y=frame.TransformPoint(new Vector3(0,bottom,0)).y;
                    if(!Physics.RaycastAll(basePoint+Vector3.up*.01f,Vector3.down,.02f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider==foundation&&h.normal.y>.9f))throw new InvalidOperationException("Extraction plate foundation contact missing");
                    report.supportedSamples++;
                    for(int j=0;j<heights.Length;j++)
                    {
                        if(!bodies[i*heights.Length+j].SequenceEqual(Body(p,heights[j])))throw new InvalidOperationException("Extraction plate standing/crouched clearance changed");
                        report.unchangedBodySamples++;
                    }
                }
                if(report.maxTopDifference>.0001f)throw new InvalidOperationException("Extraction plate walking top moved");
                if(!report.alreadyApplied)EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(undo);return report;
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        static void CheckTransferPrimitive(BoxCollider b,Transform frame)
        {
            var mesh=b.GetComponent<MeshFilter>()?.sharedMesh;
            if(!b.enabled||b.isTrigger||!b.gameObject.activeInHierarchy||b.transform.childCount!=0||b.center.sqrMagnitude>.000001f||Vector3.Distance(b.size,Vector3.one)>.0001f||Vector3.Dot(b.transform.right,frame.right)<.99999f||Vector3.Dot(b.transform.up,frame.up)<.99999f||!mesh||mesh.name!="Cube"||mesh.vertexCount!=24||!b.GetComponent<MeshRenderer>()||mesh.vertices.Any(v=>Mathf.Abs(Mathf.Abs(v.x)-.5f)>.0001f||Mathf.Abs(Mathf.Abs(v.y)-.5f)>.0001f||Mathf.Abs(Mathf.Abs(v.z)-.5f)>.0001f)||b.GetComponents<Component>().Any(c=>!(c is Transform||c is MeshFilter||c is MeshRenderer||c is BoxCollider)))throw new InvalidOperationException("Expected plain aligned transfer support primitive");
        }
    }
}
