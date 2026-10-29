using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        static Rect[] Frame8GalleryParts()=>new[]{Rect.MinMaxRect(.5f,85.5f,14.7f,101.85f),Rect.MinMaxRect(-3.5f,84.3f,.5f,95.3f),Rect.MinMaxRect(.5f,84.3f,11.1f,85.5f)};
        static bool Frame8InGallery(Vector2 p)=>Frame8GalleryParts().Any(r=>r.Contains(p));

        static void ResizeFrame8Lobby150(Transform target)
        {
            void Shift(string p,Vector3 delta){var t=target.Find(p);MoveFacility(t,t.position+delta);}
            foreach(var p in new[]{"S10_Workshop","S04_PropRoom"})Shift(p,new Vector3(-.5f,0,5));
            Shift("ST_Stairwell",new Vector3(0,0,5));
            foreach(var t in target.Cast<Transform>().Where(t=>t.name.StartsWith("B1_",StringComparison.Ordinal)).ToArray())MoveFacility(t,t.position+new Vector3(0,0,5));
            foreach(var p in new[]{"S02_Dormitory","P05_PipeZone","S05_JunkStorage"})Shift(p,new Vector3(4.75f,0,1.5f));
            foreach(var p in new[]{"S11_StageGear","S12_CargoBay"})Shift(p,new Vector3(-4.75f,0,-3.25f));
            foreach(var p in new[]{"S06_StageCorridor","S07_StageEntrance"})Shift(p,new Vector3(0,0,-4.75f));
            Shift("S09_PracticeRoom",new Vector3(-.5f,0,1));
            Shift("S03_Shop",new Vector3(4.2f,0,5));Shift("S08_ElevatorHall/FreightLift",new Vector3(4.2f,0,5));
            Shift("S01_Lobby/Frame8RetainedFactory/SteelStair",new Vector3(-4.5f,0,0));
            Group(target,"Frame8Lobby150",Origin);Physics.SyncTransforms();
        }

        static void CompactFrame8Lobby(Transform target)
        {
            void Shift(string path,Vector3 delta){var t=target.Find(path);MoveFacility(t,t.position+delta);}
            foreach(var p in new[]{"S10_Workshop","S04_PropRoom","ST_Stairwell"})Shift(p,new Vector3(1,0,-10));
            foreach(var t in target.Cast<Transform>().Where(t=>t.name.StartsWith("B1_",StringComparison.Ordinal)).ToArray())MoveFacility(t,t.position+new Vector3(1,0,-10));
            foreach(var p in new[]{"S02_Dormitory","P05_PipeZone","S05_JunkStorage"})Shift(p,new Vector3(-9.5f,0,-3));
            foreach(var p in new[]{"S11_StageGear","S12_CargoBay"})Shift(p,new Vector3(9.5f,0,6.5f));
            foreach(var p in new[]{"S06_StageCorridor","S07_StageEntrance"})Shift(p,new Vector3(0,0,9.5f));
            Shift("S09_PracticeRoom",new Vector3(1,0,-2));
            var shop=target.Find("S03_Shop");Undo.RecordObject(shop,"Put shop against gallery wall");shop.rotation=Quaternion.identity;shop.position+=new Vector3(7.3f,5,85.8f)-shop.Find("Pillar").position;
            var lift=target.Find("S08_ElevatorHall/FreightLift");Undo.RecordObject(lift,"Clear stair approach with wall-side expert service");lift.SetPositionAndRotation(new Vector3(7.3f,0,92.4f),Quaternion.identity);
            MoveFacility(target.Find("S01_Lobby/Gameplay/SpawnPoints"),new Vector3(-4.5f,.1f,80));
            var lobby=target.Find("S01_Lobby");var retained=Group(lobby,"Frame8RetainedFactory",Origin);
            var stair=lobby.Find("Structure/Mezzanine/SteelStair");
            if(stair!=null)
            {
                Undo.SetTransformParent(stair,retained,"Restore original factory steel stair");
                MoveFacility(stair,new Vector3(-13,0,72.9f));Undo.RecordObject(stair.gameObject,"Restore stair");stair.gameObject.SetActive(true);
            }
            Group(target,"Frame8CompactLobby",Origin);Physics.SyncTransforms();
        }

        static void Frame8FactoryDetail(Transform target,Transform lobbyDetail,System.Collections.Generic.List<Passage> ps)
        {
            var g=Group(lobbyDetail,"AbandonedFactory",Origin);
            // A walkable landing for the preserved two-flight stair, including steel supports and guarded edges.
            MezzDeck(g,"RestoredFactoryDeck",-18.15f,-12f,11.8f,12.95f);
            MezzRail(g,new(-18.15f,12.95f),new(-12f,12.95f));MezzRail(g,new(-12f,11.8f),new(-12f,12.95f));
            MezzRail(g,new(-16.85f,11.8f),new(-12f,11.8f));
            foreach(var x in new[]{-17.9f,-12.3f})
            {
                Box(g,"LoftSteelColumn",new Vector3(x,2.65f,82.7f),new Vector3(.18f,5.3f,.22f),Mat("V4_Iron_Dark"),true);
                Box(g,"LoftFooting",new Vector3(x,.08f,82.7f),new Vector3(.4f,.16f,.4f),Mat("V4_RustyMetal"));
            }
            // Cement wall patches, I-section supports and exposed pipes are placed only on solid wall arcs.
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12;var normal=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var p=DomeC+normal*Frame8LobbyR;
                bool opening=ps.Any(passage=>passage.Cuts(DomeC,p,.75f));
                if(!opening&&i%2==0)
                {
                    var at=DomeC+normal*(Frame8LobbyR-.16f);var q=Quaternion.LookRotation(new Vector3(normal.x,0,normal.y));
                    var c=Box(g,"CementRepairPanel",new Vector3(at.x,1.8f,at.y),new Vector3(1.2f,2.7f,.05f),RoughConcrete());c.rotation=q;
                    foreach(var y in new[]{.5f,3.2f})Box(g,"PanelBolt",new Vector3(at.x, y,at.y)-new Vector3(normal.x,0,normal.y)*.04f,new Vector3(.07f,.07f,.07f),Mat("V4_Iron_Dark"));
                    var col=DomeC+normal*(Frame8LobbyR-.32f);
                    foreach(var off in new[]{-.12f,.12f})Box(g,"FactoryIFlange",new Vector3(col.x,5,col.y)+new Vector3(normal.x,0,normal.y)*off,new Vector3(.28f,10,.025f),Mat("LA_SteelPlate")).rotation=q;
                    Box(g,"FactoryIWeb",new Vector3(col.x,5,col.y),new Vector3(.025f,10,.24f),Mat("LA_SteelPlate")).rotation=q;
                    Pipe(g,new Vector3(at.x,4.2f,at.y)-new Vector3(normal.x,0,normal.y)*.14f,Vector3.up,6.2f,.06f).name="WallRiser";
                    var wheel=Prim(g,PrimitiveType.Cylinder,"ValveWheel",new Vector3(at.x,1.5f,at.y)-new Vector3(normal.x,0,normal.y)*.32f,new Vector3(.28f,.025f,.28f),Mat("V4_RustyMetal"));wheel.rotation=Quaternion.FromToRotation(Vector3.up,new Vector3(normal.x,0,normal.y));
                }
                float b=(i+1)*Mathf.PI/12;var end=DomeC+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*(Frame8LobbyR-.38f);var begin=DomeC+normal*(Frame8LobbyR-.38f);
                if(ps.Any(passage=>passage.H>7&&passage.Cuts(DomeC,p,.4f)))continue;
                var va=new Vector3(begin.x,7.8f,begin.y);var vb=new Vector3(end.x,7.8f,end.y);
                Pipe(g,(va+vb)*.5f,vb-va,Vector3.Distance(va,vb),.15f).name="MainWallPipe";
                Box(g,"PipeWallClamp",new Vector3(p.x,7.8f,p.y)-new Vector3(normal.x,0,normal.y)*.2f,new Vector3(.12f,.12f,.5f),Mat("V4_Iron_Dark")).rotation=Quaternion.LookRotation(new Vector3(normal.x,0,normal.y));
            }
            PipeRun(g,new(-12,7.8f),new(1,7.8f),8.4f,.18f);PipeRun(g,new(-12,8.3f),new(1,8.3f),8.8f,.09f);
            foreach(var x in new[]{-11f,-7f,-3f,0f})
            {
                float roof=DomeHeightAt(Vector2.Distance(new(x,77.8f),DomeC),Frame8LobbyR,DomeWallH,Frame8TopH)-.1f;
                Pipe(g,new Vector3(x,(roof+8.4f)*.5f,77.8f),Vector3.up,roof-8.4f,.025f).name="CeilingPipeHanger";
                Box(g,"PipeCeilingAnchor",new Vector3(x,roof,77.8f),new Vector3(.24f,.08f,.24f),Mat("LA_SteelPlate"));
            }
            Box(g,"ElectricalCabinet",new Vector3(7.55f,1.5f,78.5f),new Vector3(.45f,1.7f,1.2f),Mat("V4_Iron_Dark"),true);
            foreach(var y in new[]{1.05f,1.45f,1.85f})Box(g,"CabinetVent",new Vector3(7.31f,y,78.5f),new Vector3(.03f,.05f,.7f),Mat("V4_Black"));
            PlaceFactory("SM_Barrel_1",g,new Vector3(-17.6f,0,79.3f),0);
            PlaceProp(LeartesProps+"SM_Machinery_03",g,new Vector3(5.2f,0,79.5f),0);
            foreach(var v in new[]{(-15f,80f),(-10f,77f),(-5f,86.5f),(1f,77f)})TrimCord(CagedLamp(g,new Vector3(v.Item1,7.2f,v.Item2),1.4f,WhiteLight),DomeHeightAt(Vector2.Distance(new(v.Item1,v.Item2),DomeC),Frame8LobbyR,DomeWallH,Frame8TopH));
            // Keep old dressing only where it still stands on the compact lobby floor, leaving every route mouth clear.
            foreach(var name in new[]{"Dressing","FloorWear","Lighting"})
            {
                var group=target.Find("S01_Lobby/"+name);if(group==null)continue;
                foreach(Transform t in group){var bb=Frame8VisibleBounds(t);var p=new Vector2(bb.center.x,bb.center.z);if((p-DomeC).magnitude>Frame8LobbyR-.4f||ps.Where(passage=>passage.StartC!=null).Any(passage=>InPassageMouth(passage,p)))MuteFacility(t);}
            }
            Frame8WorkshopBranch(g);
            Frame8CorridorPipes(g,ps);
            Frame8ServiceWallPipes(target,g);
        }

        static void Frame8ServiceWallPipes(Transform target,Transform g)
        {
            foreach(var y in new[]{5.5f,6.2f})Pipe(g,new Vector3(14.45f,y,93.6f),Vector3.forward,15.6f,.07f).name="ServiceWallPipe";
            foreach(var z in new[]{86.3f,94.3f,100.8f})
            {
                Pipe(g,new Vector3(14.4f,3.6f,z),Vector3.up,5.2f,.045f).name="ServiceRiser";
                for(float y=1.3f;y<6.5f;y+=1.5f)Box(g,"ServicePipeClamp",new Vector3(14.6f,y,z),new Vector3(.3f,.08f,.1f),Mat("V4_Iron_Dark"));
            }
            foreach(var r in target.Find("S03_Shop").GetComponentsInChildren<Renderer>().Where(r=>r.name=="Rope"&&r.bounds.max.y>8f&&r.bounds.max.y<13.95f))
            {
                var b=r.bounds;float hi=Frame8GalleryH-.06f;Pipe(g,new Vector3(b.center.x,(hi+b.max.y)*.5f,b.center.z),Vector3.up,hi-b.max.y,.025f).name="ShopLampRoofTie";
                Box(g,"ShopLampAnchor",new Vector3(b.center.x,hi,b.center.z),new Vector3(.18f,.08f,.18f),Mat("LA_SteelPlate"));
            }
        }

        static void Frame8WorkshopBranch(Transform g)
        {
            var path=new[]{new Vector2(-9.8f,93.4f),new(-13.38f,93.4f),new(-13.38f,99.95f)};const float hw=1.7f,h=4.2f;
            var left=new Vector2[3];var right=new Vector2[3];
            for(int i=0;i<3;i++)
            {
                var a=(path[Mathf.Min(i+1,2)]-path[Mathf.Max(i-1,0)]).normalized;
                Vector2 n;float scale=hw;
                if(i==1){var d0=(path[1]-path[0]).normalized;var d1=(path[2]-path[1]).normalized;var n0=new Vector2(-d0.y,d0.x);var n1=new Vector2(-d1.y,d1.x);n=(n0+n1).normalized;scale=hw/Vector2.Dot(n,n0);}
                else n=new Vector2(-a.y,a.x);
                left[i]=path[i]+n*scale;right[i]=path[i]-n*scale;
            }
            var branch=Group(g,"WorkshopBranch",Origin);var mesh=new MeshBuilder("LA_Frame8WorkshopBranch",1);
            Vector3 P(Vector2 p,float y)=>new(p.x,y,p.y);
            for(int i=0;i<2;i++)
            {
                mesh.Quad(0,P(left[i],.006f),P(right[i],.006f),P(right[i+1],.006f),P(left[i+1],.006f),Vector3.up,3.5f);
                mesh.Quad(0,P(left[i],h),P(left[i+1],h),P(right[i+1],h),P(right[i],h),Vector3.down,3.5f);
                foreach(var edge in new[]{(left[i],left[i+1]),(right[i],right[i+1])})
                {var m=(edge.Item1+edge.Item2)*.5f;Box(branch,"BranchWall",new Vector3(m.x,h*.5f,m.y),new Vector3(.2f,h,Vector2.Distance(edge.Item1,edge.Item2)),Mat("LA_WallConcrete"),true).rotation=Quaternion.LookRotation(P(edge.Item2-edge.Item1,0));}
            }
            var floor=MeshObject(branch,"BranchFloorCeiling",Vector3.zero,SaveMesh(mesh.Build()),Mat("LA_FloorConcrete"));floor.gameObject.AddComponent<MeshCollider>().sharedMesh=floor.GetComponent<MeshFilter>().sharedMesh;floor.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.TwoSided;
            TrimCord(CagedLamp(branch,new Vector3(-13.38f,3.1f,96),.7f,WhiteLight),h);
            FacilityLabel(branch,"10 작업실",new Vector3(-10.2f,3.2f,93.4f),Quaternion.Euler(0,90,0),.045f);
        }

        static void Frame8CorridorPipes(Transform g,System.Collections.Generic.List<Passage> ps)
        {
            foreach(var p in ps.Where(p=>p.StartC!=null&&p.Name!="F8_Stage"))
            {
                float lo=Mathf.Max(p.T0(-p.W*.5f),p.T0(p.W*.5f))+.2f,hi=Mathf.Min(p.T1(-p.W*.5f),p.T1(p.W*.5f))-.2f;
                if(hi-lo<1)continue;var group=Group(g,"FactoryPipes_"+p.Name,Origin);
                foreach(var side in new[]{-1f,1f})
                {
                    var a=p.At(side*(p.W*.5f-.2f),lo);var b=p.At(side*(p.W*.5f-.2f),hi);var va=new Vector3(a.x,p.H-.6f,a.y);var vb=new Vector3(b.x,p.H-.6f,b.y);
                    Pipe(group,(va+vb)*.5f,vb-va,Vector3.Distance(va,vb),side<0?.09f:.05f).name="CorridorMainPipe";
                    for(float t=lo+.4f;t<hi;t+=1.8f){var at=p.At(side*(p.W*.5f-.1f),t);Box(group,"PipeBracket",new Vector3(at.x,p.H-.6f,at.y),new Vector3(.14f,.14f,.14f),Mat("V4_Iron_Dark"));}
                }
            }
        }
    }
}
