using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // This entry point intentionally excludes logistics, the elevator and basement.
        public static string RestoreSourceStageAndRaisedRing()
        {
            var (scene,target)=Begin();var original=Snapshot(source);
            int undo=StartUndo("Restore source stage composition and raised ring");
            try
            {
                var facility=target.Find(FacilityName)??Group(target,FacilityName,Origin);
                var old=facility.Find("StageProcessionalRoute");if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
                LevelY=0;
                SourceFaithfulStageRoute(target,Group(facility,"StageProcessionalRoute",Origin));
                RepairPracticeCollision(target);
                ApplyVisualSupportRepairs(target);
                CurtainCall.WaitingRoom.EditorTools.WaitingRoomLayoutFitter.FitMouseLookZone(scene,target);
                Physics.SyncTransforms();AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                return "Original stage composition scaled as a whole; original materials/curtains/pipes retained; ring=0.6m with two stairs";
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }

        static void SourceFaithfulStageRoute(Transform target,Transform g)
        {
            var route=target.Find("S06_StageCorridor");var stage=target.Find("S07_StageEntrance");
            if(route==null||stage==null)return;
            foreach(var n in new[]{"Shell","TunnelArt","FloorCollider","Zone_Corridor"})MuteFacility(route.Find(n));
            foreach(var n in new[]{"Shell","StageArt","Zone_StageEntrance"})MuteFacility(stage.Find(n));
            // 새 통로가 이전 통로 구역을 대체하므로 카메라 시점 구역도 실제 통로에 맞춘다.
            var approachView = Copy(Required("CameraZones/Zone_Corridor"), g, "Zone_StageApproach", false);
            approachView.SetPositionAndRotation(W((14.7f + ShowStart) * .5f, .5f, 4.6f), Quaternion.identity);
            approachView.localScale = Vector3.one;
            approachView.GetComponent<BoxCollider>().size = new Vector3(ShowStart - 14.7f, 9.2f, 9f);
            approachView.gameObject.SetActive(true);
            var stageView = Copy(Required("CameraZones/Zone_Corridor"), g, "Zone_StagePassage", false);
            stageView.SetPositionAndRotation(W((ShowStart + ShowCurtain) * .5f, .5f, 6.5f), Quaternion.identity);
            stageView.localScale = Vector3.one;
            stageView.GetComponent<BoxCollider>().size = new Vector3(ShowCurtain - ShowStart, 13f, ShowWidth);
            stageView.gameObject.SetActive(true);
            // Every source transform shares one affine frame. Relative spacing, pipe joints, curtain geometry,
            // surface materials and the original lighting rhythm remain intact instead of being redesigned.
            const float sx=(ShowCurtain-ShowStart)/42f,sy=13f/7.6f,sz=14f/8f;
            var composition=Group(g,"GreyboxComposition",new Vector3(10,0,70));
            foreach(var path in new[]{"S06_StageCorridor_Temp/Floor","S06_StageCorridor_Temp/CurtainRooms","S07_StageEntrance_Temp","Mood/Tunnel","Mood/Furnishings/Corridor"})
                Copy(Required(path),composition,Required(path).name,false);
            foreach(Transform curtain in Required("Mood/Curtains"))
                if(curtain.name.StartsWith("Curtain_CurtainRoom")||curtain.name=="Curtain_StageEntrance")Copy(curtain,composition,curtain.name,false);
            foreach(var path in new[]{"Mood/VisibilityLighting/Corridor_Near","Mood/VisibilityLighting/Corridor_Middle","Mood/VisibilityLighting/Corridor_Far","Mood/VisibilityLighting/StageEntrance","Mood/Lights/StageEntrance_RedSpill","Mood/LightingPass/Spot_StageCurtain"})
                Copy(Required(path),composition,Required(path).name,false);
            foreach(var path in new[]{"Walls/W_06_North_08_South","Walls/W_06_South","Walls/W_06_East"})
            {
                var wall=Copy(Required(path),composition,Required(path).name,false);
                // The source's unseen walls extend above its vault. Fit that hidden backing inside the 13m envelope.
                foreach(Transform seg in wall){seg.position=new Vector3(seg.position.x,6.5f/sy,seg.position.z);seg.localScale=new Vector3(seg.localScale.x,13f/sy,seg.localScale.z);}
            }
            composition.localScale=new Vector3(sx,sy,sz);composition.position=W(ShowStart,.5f);
            foreach(var light in composition.GetComponentsInChildren<Light>(true)){light.range*=sz;light.intensity*=sz*sz;}
            var vault=composition.Find("Tunnel/Vault");
            if(vault.GetComponent<MeshCollider>()==null)vault.gameObject.AddComponent<MeshCollider>().sharedMesh=vault.GetComponent<MeshFilter>().sharedMesh;

            // Same original vault/wall/floor materials on the shorter approach. It is a transition, not a new theatre facade.
            var floor=Box(g,"ApproachFloor",W((14.7f+ShowStart)*.5f,.5f,-.1f),new Vector3(ShowStart-14.7f,.2f,9),Required("S06_StageCorridor_Temp/Floor").GetComponent<Renderer>().sharedMaterial,true);
            var wallMaterial=Required("Walls/W_06_South/Seg0").GetComponent<Renderer>().sharedMaterial;
            foreach(var z in new[]{-4f,5f})Box(g,"ApproachWall",W((14.7f+ShowStart)*.5f,z,4.6f),new Vector3(ShowStart-14.7f,9.2f,.35f),wallMaterial,true);
            var approach=Group(g,"OriginalVaultApproach",new Vector3(31,0,70));
            var av=Copy(Required("Mood/Tunnel/Vault"),approach,"Vault",false);
            approach.localScale=new Vector3((ShowStart-14.7f)/42f,9.2f/7.6f,9f/8f);approach.position=W((14.7f+ShowStart)*.5f,.5f);
            av.gameObject.AddComponent<MeshCollider>().sharedMesh=av.GetComponent<MeshFilter>().sharedMesh;
            // Close the difference in section at the seam, using the original vault surface.
            var cap=new MeshBuilder("LA_SourceVaultTransition",1);
            var edges=Enumerable.Range(0,57).Select(i=>-6.5f+i*.25f).Concat(new[]{-4f,5f}).Distinct().OrderBy(z=>z).ToArray();
            float Roof(float z,float half,float spring,float rise)=>spring+rise*Mathf.Sqrt(Mathf.Max(0,1-(z-.5f)*(z-.5f)/(half*half)));
            for(int i=0;i<edges.Length-1;i++)
            {
                float a=edges[i],b=edges[i+1],m=(a+b)*.5f;
                float loA=m>-4&&m<5?Roof(a,4.5f,3.6f*9.2f/7.6f,4*9.2f/7.6f):0;
                float loB=m>-4&&m<5?Roof(b,4.5f,3.6f*9.2f/7.6f,4*9.2f/7.6f):0;
                cap.Quad(0,W(ShowStart,a,loA),W(ShowStart,a,Roof(a,7,3.6f*sy,4*sy)),W(ShowStart,b,Roof(b,7,3.6f*sy,4*sy)),W(ShowStart,b,loB),Vector3.left,3);
            }
            var cover=MeshObject(g,"ClosedSourceSectionTransition",Vector3.zero,SaveMesh(cap.Build()),Required("Mood/Tunnel/Vault").GetComponent<Renderer>().sharedMaterial);
            cover.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            cover.gameObject.AddComponent<MeshCollider>().sharedMesh=cover.GetComponent<MeshFilter>().sharedMesh;
            // Lighting on the approach borrows the source's near light; no new broad fill lights or marquee are introduced.
            var near=Copy(Required("Mood/VisibilityLighting/Corridor_Near"),g,"ApproachNearLight",false);near.position=W(21,.5f,4);
            foreach(var light in near.GetComponentsInChildren<Light>(true)){light.range*=1.2f;}

            var gather=stage.GetComponentInChildren<StageEntranceGather>(true);
            if(gather!=null)
            {
                MoveFacility(gather.transform,W(88,.5f));var so=new SerializedObject(gather);
                so.FindProperty("areaCenter").vector3Value=new Vector3(0,1.5f,0);so.FindProperty("areaSize").vector3Value=new Vector3(6,3,12);so.ApplyModifiedProperties();gather.ApplyArea();
            }
            var night=route.GetComponentInChildren<NightDoorLock>(true);
            if(night!=null)
            {
                var shutter=(Transform)new SerializedObject(night).FindProperty("shutter").objectReferenceValue;var door=shutter.parent;
                MoveFacility(door,door.position+new Vector3(ShowStart-shutter.position.x,0,W(0,.5f).z-shutter.position.z));
                Undo.RecordObject(door,"Fit source route night shutter");door.localScale=new Vector3(1,9.2f/3f,9f/3.33f);
                Undo.RecordObject(shutter,"Open route in edit mode");shutter.localScale=new Vector3(shutter.localScale.x,.06f,shutter.localScale.z);
            }
        }

        static void RaisedPracticeRing(Transform arena)
        {
            const float top=.6f;var ring=arena.Find("PracticeRing");if(ring==null)return;
            Undo.RecordObject(ring,"Raise original ring");ring.position=W(PracRing.x,PracRing.y,top-PracRingDeck);
            var deck=ring.Find("DeckCollider");Undo.RecordObject(deck,"Fit raised deck collision");deck.position=W(PracRing.x,PracRing.y,top*.5f);deck.localScale=new Vector3(PracRingRadius*2,top*.5f,PracRingRadius*2);
            foreach(var skirt in ring.Cast<Transform>().Where(t=>t.name=="HarlequinSkirt"))
            {
                Undo.RecordObject(skirt,"Extend existing skirt");var at=skirt.position;at.y=top*.5f;skirt.position=at;var scale=skirt.localScale;scale.y=top;skirt.localScale=scale;
            }
            var lanes=arena.Find("PracticeLanes");
            if(lanes.Find("FittingsAtRaisedDeck")==null)
            {
                var names=new[]{"CannonPlatform","CannonRail","WheelChock","LandingMat","NetPost","NetStrand","NetTopBar","FloorTape","CannonLaunch","CannonLanding"};
                foreach(Transform t in lanes)if(names.Contains(t.name))
                {
                    var at=t.position+Vector3.up*(top-PracRingDeck);if(t.name=="CannonLanding")at.y=top;MoveFacility(t,at);
                }
                Group(lanes,"FittingsAtRaisedDeck",W(PracRing.x,PracRing.y,top));
            }
            var platform=lanes.Find("CannonPlatform");
            if(platform!=null&&platform.GetComponent<BoxCollider>()==null)Undo.AddComponent<BoxCollider>(platform.gameObject);
            Remove(arena,"RaisedRingAccess");var g=Group(arena,"RaisedRingAccess",arena.position);
            foreach(var west in new[]{false,true})
            {
                Vector3 start=west?W(PracRing.x-PracRingRadius-1.35f,PracRing.y):W(PracRing.x,PracRing.y-PracRingRadius-1.35f);
                Vector3 direction=west?Vector3.right:Vector3.forward;
                for(int i=0;i<3;i++)Box(g,"RingAccessStep",start+direction*(.45f*(i+.5f))+Vector3.up*(.1f*(i+1)),west?new Vector3(.45f,.2f*(i+1),2.2f):new Vector3(2.2f,.2f*(i+1),.45f),Mat("V4_Wood_Old"));
                var ramp=Group(g,"RingStairCollision",start+direction*.675f+Vector3.up*.25f);
                ramp.rotation=Quaternion.LookRotation(direction*1.35f+Vector3.up*top,Vector3.up);
                var collider=ramp.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(2.2f,.1f,Mathf.Sqrt(1.35f*1.35f+top*top));
            }
        }
    }
}
