using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        static readonly Vector2 Frame8Practice = new(-48.5f, 112f);
        static readonly Rect Frame8Gallery = Rect.MinMaxRect(-3.5f, 84.3f, 14.7f, 101.85f);
        const float Frame8LobbyR = 14.25f, Frame8TopH = 12f;
        const float Frame8GalleryH = 14f;

        static List<Passage> Frame8Passages() => new()
        {
            new() { Name="F8_Practice", Label="09 연습실", O=DomeC, U=(Frame8Practice-DomeC).normalized, W=6f, H=5.5f, StartC=DomeC, StartR=Frame8LobbyR, EndC=Frame8Practice, EndR=PracR },
            new() { Name="F8_PropRoom", Label="04 소품실 · 10 작업실", O=new(-8.2f,77.4f), U=Vector2.up, W=3.4f, H=4.2f, StartC=DomeC, StartR=Frame8LobbyR, EndT=18f, WallGapSide=1, WallGapA=14.3f, WallGapB=17.7f },
            new() { Name="F8_Cargo", Label="12 화물 · 11 무대 장치", O=new(-6f,68.65f), U=Vector2.left, W=3.6f, H=4.2f, StartC=DomeC, StartR=Frame8LobbyR, EndT=20.85f },
            new() { Name="F8_Dorm", Label="02 숙소 · 화장실", O=new(-6f,72.55f), U=Vector2.right, W=3.8f, H=4.2f, StartC=DomeC, StartR=Frame8LobbyR, EndT=17.15f },
            new() { Name="F8_Stage", Label="06 무대 입구 통로", O=DomeC, U=Vector2.down, W=9f, H=9.2f, StartC=DomeC, StartR=Frame8LobbyR, EndT=14.35f, Lamp=false },
            // This is a mask for the shared lobby gallery, not a tunnel with interior side walls.
            new() { Name="F8_GalleryMask", Label="", O=new(5.6f,84.3f), U=Vector2.up, W=18.2f, H=10f, StartT=0, EndT=17.55f, Lamp=false, OpeningMask=Frame8InGallery },
            new() { Name="F8_PipeCrawl", Label="", O=new(16.05f,62.65f), U=Vector2.down, W=1.9f, H=2.4f, StartT=0, EndT=3.1f, Lamp=false },
        };

        public static string BuildFrame8Layout()
        {
            var (scene,target)=Begin(true); var original=Snapshot(source); int undo=StartUndo("Frame 8: relocate preserved room assemblies");
            try
            {
                if(target.Find("Frame8Layout")==null)
                {
                    AttachFrame8RoomDependents(target);
                    MoveFrame8Assemblies(target);
                    Group(target,"Frame8Layout",Origin);
                }
                if(target.Find("Frame8CompactLobby")==null) CompactFrame8Lobby(target);
                if(target.Find("Frame8Lobby150")==null) ResizeFrame8Lobby150(target);
                Physics.SyncTransforms();
                BuildFrame8Connections(target);
                Frame8WaitingAreas(target);
                CurtainCall.WaitingRoom.EditorTools.WaitingRoomLayoutFitter.FitMouseLookZone(scene,target);
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                return "Frame8: room interiors retained; relocated rooms, connected lobby/corridors, stage south; original Greybox unchanged";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void AttachFrame8RoomDependents(Transform target)
        {
            void Attach(Transform t,Transform owner) { if(t!=null&&owner!=null&&t.parent!=owner)Undo.SetTransformParent(t,owner,"Attach room-dependent finish before relocation"); }
            var dorm=target.Find("S02_Dormitory");var route=target.Find("S06_StageCorridor");var lift=target.Find("S08_ElevatorHall/FreightLift");
            foreach(var n in new[]{"Skin_Dormitory","Skin_Restroom"})Attach(target.Find("S01_Lobby/Dome/"+n),dorm);
            foreach(var n in new[]{"ScheduleBoard","ScheduleFrame","ScheduleCloseUp"})Attach(target.Find("S01_Lobby/Gameplay/"+n),dorm);
            foreach(var t in target.Find("FactoryFrame").Cast<Transform>().ToArray())
            {
                var owner=target.Cast<Transform>().FirstOrDefault(r=>t.name.StartsWith(r.name+"_",StringComparison.Ordinal));Attach(t,owner);
            }
            foreach(var t in target.Find("CircusProps").Cast<Transform>().ToArray())Attach(t,target.Find(t.name));
            // All environment-detail groups are below ground or in the stairwell and share the stair assembly transform.
            Attach(target.Find("EnvironmentDetail"),target.Find("ST_Stairwell"));
            Attach(target.Find("FacilityConnections/StageProcessionalRoute"),route);
            Attach(target.Find("FacilityConnections/LogisticsFrontages/StagePortalHeader"),route);
            Attach(target.Find("FacilityConnections/LogisticsFrontages"),target.Find("S11_StageGear"));
            var enclosure=target.Find("ExteriorEnclosure");
            foreach(var t in enclosure.Cast<Transform>().ToArray())
                if(t.name.StartsWith("Stage",StringComparison.Ordinal))Attach(t,route);
                else if(t.name.StartsWith("LiftShaft",StringComparison.Ordinal))Attach(t,lift);
                else MuteFacility(t);
            // Existing suspension pieces must move with the room they support, not remain at the old world coordinates.
            var rooms=target.Cast<Transform>().Where(r=>r.name!="S01_Lobby"&&(r.name.StartsWith("S0")||r.name.StartsWith("S1")||r.name is "P05_PipeZone" or "LG_Lounge")).ToArray();
            var bounds=rooms.ToDictionary(t=>t,Frame8VisibleBounds);
            foreach(var t in target.Find("SupportFinishing").Cast<Transform>().ToArray())
            {
                var b=Frame8VisibleBounds(t);var p=b.center;Transform owner=null;
                if(p.y<-.05f)owner=target.Find("B1_MachineRoom");
                else if(p.x>=14.4f&&p.x<=94&&p.z>=62.8f&&p.z<=78.2f)owner=route;
                else
                {
                    var nearest=rooms.Select(r=>(room:r,d:bounds[r].SqrDistance(p))).OrderBy(x=>x.d).First();
                    if(nearest.d<9f)owner=nearest.room;
                }
                Attach(t,owner);
            }
        }

        static Bounds Frame8VisibleBounds(Transform t)
        {
            var rs=t.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!(r is ParticleSystemRenderer)&&r.bounds.size.sqrMagnitude>.0001f).ToArray();
            if(rs.Length==0)return new Bounds(t.position,Vector3.zero);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }

        static void Frame8Rigid(Transform t,Vector3 from,Vector3 to,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);Undo.RecordObject(t,"Move preserved room assembly");t.SetPositionAndRotation(to+q*(t.position-from),q*t.rotation);
        }

        static void MoveFrame8Assemblies(Transform target)
        {
            void Move(string path,Vector3 from,Vector3 to,float yaw)=>Frame8Rigid(target.Find(path),from,to,yaw);
            Move("S09_PracticeRoom",new(-20,0,128.5f),new(-49,0,113),0);
            Move("S10_Workshop",new(-43.18f,0,69.38f),new(-15.5f,0,113.5f),90);
            Move("S04_PropRoom",new(18.5f,0,60.7f),new(-6,0,106.5f),-90);
            Move("S02_Dormitory",new(-8.9f,0,86.4f),new(21.5f,0,78),90);
            var shop=target.Find("S03_Shop");var pillar=shop.Find("Pillar").position;pillar.y=0;
            Move("S03_Shop",pillar,new(5,0,91),-90);
            Move("S08_ElevatorHall/FreightLift",new(8,0,78.1f),new(11,0,91),-90);
            Move("LG_Lounge",target.Find("LG_Lounge").position,new(-6,0,73),0);
            foreach(var path in new[]{"S06_StageCorridor","S07_StageEntrance"})Move(path,new(14.7f,0,70.5f),new(-6,0,58.4f),90);
            foreach(var path in new[]{"S11_StageGear","S12_CargoBay"})Move(path,new(2,0,38.7f),new(-48.3f,0,51),-90);
            foreach(var t in target.Cast<Transform>().Where(t=>t.name=="ST_Stairwell"||t.name.StartsWith("B1_",StringComparison.Ordinal)).ToArray())
                Frame8Rigid(t,new(-30.45f,0,80.9f),new(5.2f,0,109.5f),90);
            Move("P05_PipeZone",new(18.3f,0,88.9f),new(20.8f,0,61.25f),90);
            Move("S05_JunkStorage",new(18.3f,0,88.9f),new(27.8f,0,56.25f),90);
            MoveFacility(target.Find("S01_Lobby/Gameplay/SpawnPoints"),new Vector3(-6,.1f,82));
            Physics.SyncTransforms();
            // Keep the exterior schedule board fully on the new dorm frontage rather than straddling the partition.
            var dorm=target.Find("S02_Dormitory");var board=dorm.Find("ScheduleBoard");var delta=new Vector3(15.87f,1.9f,79.2f)-Frame8VisibleBounds(board).center;
            foreach(var n in new[]{"ScheduleBoard","ScheduleFrame","ScheduleCloseUp"}){var t=dorm.Find(n);MoveFacility(t,t.position+delta);}
            // The old pipe collider covered both rooms. Fit each floor to its unchanged visible room envelope after separation.
            var pipes=target.Find("P05_PipeZone");var b=pipes.Find("Shell/WallSurfaces").GetComponent<Renderer>().bounds;
            var floor=pipes.Find("FloorCollider");Undo.RecordObject(floor,"Fit separated pipe floor");floor.SetPositionAndRotation(new Vector3(b.center.x,-.1f,b.center.z),Quaternion.identity);
            var box=floor.GetComponent<BoxCollider>();Undo.RecordObject(box,"Fit separated pipe floor");box.center=Vector3.zero;box.size=new Vector3(b.size.x+.2f,.2f,b.size.z+.2f);
            var junk=target.Find("S05_JunkStorage");b=junk.Find("Shell/WallSurfaces").GetComponent<Renderer>().bounds;
            var jf=Group(junk,"Frame8FloorCollider",new Vector3(b.center.x,-.1f,b.center.z));jf.rotation=Quaternion.identity;
            Undo.AddComponent<BoxCollider>(jf.gameObject).size=new Vector3(b.size.x+.2f,.2f,b.size.z+.2f);
        }

        static void BuildFrame8Connections(Transform target)
        {
            var lobby=target.Find("S01_Lobby");MuteFacility(lobby.Find("Dome"));
            Remove(lobby,"Frame8Lobby");var g=Group(lobby,"Frame8Lobby",Origin);var ps=Frame8Passages();
            BuildRoundHall(g,"LA_Frame8Lobby",DomeC,Frame8LobbyR,DomeWallH,Frame8TopH,ps,16,true);
            g.Find("DrumWall").GetComponent<Renderer>().sharedMaterials=new[]{RoughConcrete(),Mat("LA_WallConcrete")};
            foreach(var p in ps.Where(p=>p.Name!="F8_GalleryMask"))BuildPassage(g,p);
            BuildFrame8Gallery(g);
            RepairFrame8PracticeMouth(target,ps.Single(p=>p.Name=="F8_Practice"));
            Frame8LogisticsDoor(target,g);
            Frame8PipeLink(g);
            Frame8FactoryDetail(target,g,ps);
            foreach(var path in new[]{"S04_PropRoom","S10_Workshop","P05_PipeZone","S05_JunkStorage"})SkinRoomExterior(target.Find(path),Group(g,"OuterSkin_"+path,Origin));
            Point(g,"PlazaKey",new Vector3(-6,9.5f,77.4f),WhiteLight,2.7f,18,LightShadows.Soft);
            DarkOuterShell(target,g);
        }

        static void RepairFrame8PracticeMouth(Transform target,Passage passage)
        {
            var round=target.Find("S09_PracticeRoom/RoundArena");MuteFacility(round.Find("DrumWall"));MuteFacility(round.Find("WallColliders"));
            foreach(Transform t in round)if(t.name is "LedgerBand" or "DrumPilaster")
            {var p=new Vector2(t.position.x,t.position.z);if(passage.Cuts(Frame8Practice,p,.6f))MuteFacility(t);}
            Remove(round,"Frame8Boundary");var g=Group(round,"Frame8Boundary",round.position);
            BuildRoundHall(g,"LA_Frame8Practice",Frame8Practice,PracR,PracWallH,PracTopH,new List<Passage>{passage},20,true,true);
        }

        static void Frame8LogisticsDoor(Transform target,Transform g)
        {
            var fronts=target.Find("S11_StageGear/LogisticsFrontages");MuteFacility(fronts.Find("LogisticsSouth"));
            var wall=Group(g,"CargoDockConnection",Origin);var mat=Mat("LA_WallConcrete");
            foreach(var span in new[]{(28.25f,66.75f),(70.55f,80.25f)})Box(wall,"CargoOuterReturn",new Vector3(-26.85f,6,(span.Item1+span.Item2)*.5f),new Vector3(.28f,12,span.Item2-span.Item1),mat,true);
            Box(wall,"CargoDockHeader",new Vector3(-26.85f,8.3f,68.65f),new Vector3(.28f,7.4f,3.8f),mat,true);
            foreach(var span in new[]{(43.6f,9.1f),(54.7f,7.9f)})Box(wall,"FormerEntryClosure",new Vector3(-60.25f,4.5f,span.Item1),new Vector3(.3f,9,span.Item2),mat,true);
            // Open one existing loading dock on the new lobby side; all racks, shelves and stored goods stay put.
            foreach(var t in target.Find("S12_CargoBay/Zone12").Cast<Transform>())
                if(t.name is "DockShutter" or "ShutterRib")if(Mathf.Abs(t.position.z-68.65f)<2f)MuteFacility(t);
        }

        static void Frame8PipeLink(Transform g)
        {
            var link=Group(g,"PipeJunkLink",Origin);
            RoomShell(link,"LA_Frame8PipeLink",13.85f,35.75f,45.85f-70,50.85f-70,5.2f,2.4f,Mat("LA_FloorConcrete"),Mat("LA_WallConcrete"),Mat("LA_WallConcrete"),
                new[]{(1,16.45f,4f,.2f),(1,26.15f,4f,.2f),(3,23.45f,4f,.2f),(3,33.15f,4f,.2f)});
            FloorCollider(link,13.85f,35.75f,45.85f-70,50.85f-70);
            Point(link,"PipeLinkLight",new Vector3(24.75f,4.2f,48.35f),WhiteLight,1.2f,13);
        }

        static void BuildFrame8Gallery(Transform g)
        {
            var hall=Group(g,"ServiceGallery",Origin);var r=Frame8Gallery;var concrete=Mat("LA_WallConcrete");
            var mesh=new MeshBuilder("LA_Frame8GalleryFloor",1);
            foreach(var part in Frame8GalleryParts())
            {
                mesh.Quad(0,new Vector3(part.xMin,.004f,part.yMin),new Vector3(part.xMin,.004f,part.yMax),new Vector3(part.xMax,.004f,part.yMax),new Vector3(part.xMax,.004f,part.yMin),Vector3.up,3.5f);
                var fc=Group(hall,"FloorCollider",new Vector3(part.center.x,-.1f,part.center.y));Undo.AddComponent<BoxCollider>(fc.gameObject).size=new Vector3(part.width,.2f,part.height);
                Box(hall,"GalleryRoof",new Vector3(part.center.x,Frame8GalleryH+.15f,part.center.y),new Vector3(part.width,.3f,part.height),concrete);
            }
            MeshObject(hall,"GalleryFloor",Vector3.zero,SaveMesh(mesh.Build()),Mat("LA_FloorConcrete"));
            void Wall(Vector2 a,Vector2 b,float y0=0,float y1=Frame8GalleryH)
            {
                if(Vector2.Distance(a,b)<.02f)return;var m=(a+b)*.5f;
                Box(hall,"GalleryWall",new Vector3(m.x,(y0+y1)*.5f,m.y),new Vector3(.3f,y1-y0,Vector2.Distance(a,b)+.02f),concrete,true).rotation=Quaternion.LookRotation(new Vector3(b.x-a.x,0,b.y-a.y));
            }
            void Clipped(Vector2 a,Vector2 b)
            {
                var d=b-a;var p=a-DomeC;float aa=d.sqrMagnitude,bb=2*Vector2.Dot(p,d),cc=p.sqrMagnitude-Frame8LobbyR*Frame8LobbyR,disc=bb*bb-4*aa*cc;
                var ts=new List<float>{0,1};if(disc>0)foreach(var t in new[]{(-bb-Mathf.Sqrt(disc))/(2*aa),(-bb+Mathf.Sqrt(disc))/(2*aa)})if(t>0&&t<1)ts.Add(t);
                ts.Sort();for(int i=0;i<ts.Count-1;i++)if((Vector2.Lerp(a,b,(ts[i]+ts[i+1])*.5f)-DomeC).magnitude>Frame8LobbyR-.01f)Wall(Vector2.Lerp(a,b,ts[i]),Vector2.Lerp(a,b,ts[i+1]));
            }
            var polygon=new[]{new Vector2(-3.5f,84.3f),new(11.1f,84.3f),new(11.1f,85.5f),new(14.7f,85.5f),new(14.7f,101.85f),new(.5f,101.85f),new(.5f,95.3f),new(-3.5f,95.3f)};
            for(int i=0;i<polygon.Length;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];
                if(i==4){Wall(new(.5f,101.85f),new(3.45f,101.85f));Wall(new(7.05f,101.85f),new(14.7f,101.85f));Wall(new(3.45f,101.85f),new(7.05f,101.85f),4.2f,Frame8GalleryH);}
                else Clipped(a,b);
            }
            // Close the raised gallery roof above the dome spring line along their shared curved opening.
            for(int i=0;i<160;i++)
            {
                float a=i*Mathf.PI*2/160,b=(i+1)*Mathf.PI*2/160;var p=DomeC+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*Frame8LobbyR;var q=DomeC+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*Frame8LobbyR;
                if(Frame8InGallery((p+q)*.5f))Wall(p,q,DomeWallH,Frame8GalleryH);
            }
            foreach(var z in new[]{89f,98f})TrimCord(CagedLamp(hall,new Vector3(3.5f,7,z),1.4f,WhiteLight),Frame8GalleryH);
            FacilityLabel(hall,"계단 · B1",new Vector3(5.25f,4.8f,101.6f),Quaternion.identity,.055f);
        }

        static void Frame8WaitingAreas(Transform target)
        {
            var timer=target.Find("ExplorationTimer");MuteFacility(timer.Find("WaitingRoomArea"));Remove(timer,"Frame8Areas");var g=Group(timer,"Frame8Areas",Origin);
            void Area(string n,Bounds b)
            {
                var t=Group(g,n,b.center);var c=Undo.AddComponent<BoxCollider>(t.gameObject);c.isTrigger=true;c.size=b.size;
            }
            Area("Lobby",new Bounds(new Vector3(-6,3,77.4f),new Vector3(28.5f,12,28.5f)));
            Area("ServiceGallery",new Bounds(new Vector3(Frame8Gallery.center.x,6,Frame8Gallery.center.y),new Vector3(Frame8Gallery.width,14,Frame8Gallery.height)));
            foreach(var p in Frame8Passages().Where(p=>p.Name!="F8_Stage"&&p.Name!="F8_GalleryMask"))
            {
                float a=Mathf.Min(p.T0(-p.W*.5f),p.T0(p.W*.5f)),b=Mathf.Max(p.T1(-p.W*.5f),p.T1(p.W*.5f));var m=p.At(0,(a+b)*.5f);
                var t=Group(g,p.Name,new Vector3(m.x,3,m.y));t.rotation=Quaternion.LookRotation(new Vector3(p.U.x,0,p.U.y));var c=Undo.AddComponent<BoxCollider>(t.gameObject);c.isTrigger=true;c.size=new Vector3(p.W+.6f,8,b-a+.6f);
            }
            foreach(var room in target.Cast<Transform>().Where(t=>t.name!="S01_Lobby"&&(t.name.StartsWith("S0")||t.name.StartsWith("S1")||t.name.StartsWith("B1_")||t.name is "ST_Stairwell" or "P05_PipeZone")))
            {
                if(room.name is "S06_StageCorridor" or "S07_StageEntrance")continue;var b=Frame8VisibleBounds(room);b.Expand(new Vector3(1,3,1));Area("Area_"+room.name,b);
            }
            Area("PipeLink",new Bounds(new Vector3(24.8f,2.5f,48.35f),new Vector3(22,7,5.2f)));
            Area("WorkshopBranch",new Bounds(new Vector3(-12f,3,95.8f),new Vector3(7,8,9)));
        }

        static string ValidateFrame8(Transform target)
        {
            if(source.gameObject.activeSelf)throw new InvalidOperationException("Original Greybox active");
            if(target.GetComponentsInChildren<Component>(true).Any(c=>c==null))throw new InvalidOperationException("Missing script");
            ValidateShop(target.gameObject.scene,target.Find("S03_Shop"));
            foreach(Transform p in target.Find("S01_Lobby/Gameplay/SpawnPoints"))CheckStanding(p.position,target,"Frame8 spawn "+p.name);
            foreach(var p in Frame8Passages().Where(p=>p.StartC!=null))
            {
                float a=Mathf.Min(p.T0(-p.W*.5f),p.T0(p.W*.5f)),b=Mathf.Max(p.T0(-p.W*.5f),p.T0(p.W*.5f));
                foreach(var t in new[]{a-.8f,b+.8f}){var q=p.At(0,t);CheckStanding(new Vector3(q.x,.1f,q.y),target,"Frame8 "+p.Name);}
            }
            return "Frame8 structure PASS; shop/spawns/passage centres; original inactive";
        }
    }
}
