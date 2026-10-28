using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        const string FacilityName = "FacilityConnections";
        const float ShowStart = 27f, ShowEnd = 83f, ShowCurtain = 92f, ShowWidth = 14f;

        // User-approved scope: logistics and stage route, compact lobby lift,
        // ladder collision repairs and the concealed lower plant approach.
        public static string RefineFacility()
        {
            var (scene, target) = Begin();
            var original = Snapshot(source);
            int undo = StartUndo("Layout A: retained logistics and enclosed stage route");
            try
            {
                ApplyFacilityRevision(scene, target);
                CurtainCall.WaitingRoom.EditorTools.WaitingRoomLayoutFitter.FitMouseLookZone(scene, target);
                Physics.SyncTransforms();
                AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                return "Scoped facility details revised; stage tunnel=56m x 14m; compact lift and concealed plant connected";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void ApplyFacilityRevision(Scene scene, Transform target)
        {
            var g = target.Find(FacilityName);
            if (g == null) g = Group(target, FacilityName, Origin);
            LevelY = 0;
            // Existing logistics frontages are user-preserved. Detail work must never rebuild them.
            if(g.Find("LogisticsFrontages")==null)
                RetainedLogisticsAndLobby(target, Group(g, "LogisticsFrontages", Origin));
            var route=g.Find("StageProcessionalRoute");
            if(route!=null)Undo.DestroyObjectImmediate(route.gameObject);
            SourceFaithfulStageRoute(target, Group(g, "StageProcessionalRoute", Origin));
            CompactLobbyElevator(target);
            RepairPracticeCollision(target);
            ConcealedMachinePlant(target);
            ApplyVisualSupportRepairs(target);

            Physics.SyncTransforms();
        }

        static void MuteFacility(Transform t)
        {
            if (t == null || !t.gameObject.activeSelf) return;
            Undo.RecordObject(t.gameObject, "Replace closed-room geometry");
            t.gameObject.SetActive(false);
        }

        static void MoveFacility(Transform t, Vector3 p)
        {
            Undo.RecordObject(t, "Move facility fitting");
            t.position = p;
        }

        static void MuteShellWalls(Transform root, bool ceiling = false)
        {
            if (root == null) return;
            MuteFacility(root.Find("Shell/WallSurfaces"));
            MuteFacility(root.Find("Shell/WallStructure"));
            if (ceiling) MuteFacility(root.Find("Shell/Ceiling"));
        }

        static Transform FacilityWall(Transform g, string name, Vector2 a, Vector2 b, float height, float y = 0)
        {
            var m = (a + b) * .5f;
            return Box(g, name, W(m.x, m.y, y + height * .5f),
                Size(new Vector3(b.x-a.x,0,b.y-a.y).normalized, Vector2.Distance(a,b), height,.28f), Mat("LA_WallConcrete"), true);
        }

        static void OpenSpan(Transform g, Vector2 a, Vector2 b, float openingHeight)
        {
            foreach (var p in new[] { a,b })
            {
                Box(g,"FacilityColumn",W(p.x,p.y,WallHeight*.5f),new Vector3(.3f,WallHeight,.3f),Mat("V4_Iron_Dark"),true);
                Box(g,"ColumnFoot",W(p.x,p.y,.15f),new Vector3(.5f,.3f,.5f),Mat("V4_RustyMetal"));
            }
            var mid=(a+b)*.5f; var dir=new Vector3(b.x-a.x,0,b.y-a.y).normalized;
            Box(g,"ContinuousLedger",W(mid.x,mid.y,openingHeight),Size(dir,Vector2.Distance(a,b),.38f,.35f),Mat("V4_Iron_Dark"));
        }

        static void FacilityLabel(Transform parent,string text,Vector3 at,Quaternion facing,float size=.08f)
        {
            var sign=Copy(Required("S01_Lobby/Sign_② 숙소"),parent,"FacilitySign",false);
            sign.gameObject.SetActive(true); sign.SetPositionAndRotation(at,facing);
            var t=sign.GetComponent<TextMesh>(); t.text=text;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
            t.fontSize=64;t.characterSize=size;t.color=new Color(.88f,.79f,.58f);
        }





        static void RetainedLogisticsAndLobby(Transform target, Transform g)
        {
            var lobby=target.Find("S01_Lobby"); if(lobby==null)return;
            var surface=lobby.Find("Shell/WallSurfaces").GetComponent<MeshFilter>();
            Undo.RecordObject(surface,"Keep only logistics and stage openings");
            surface.sharedMesh=RetainedLobbyMesh();
            // Only the two retained frontages change. All other lobby/room objects were restored by ID.
            foreach(Transform t in lobby.Find("Shell/WallStructure"))
            {
                bool east=Mathf.Abs(t.position.x-LobbyW)<.8f&&t.position.z>=W(0,-4.6f).z&&t.position.z<=W(0,14.6f).z;
                bool south=Mathf.Abs(t.position.z-W(0,LobbyS).z)<.8f&&t.position.x>=-15.2f&&t.position.x<=7f;
                if((east||south)&&(t.name=="WallCollider"||t.name=="DoorHeaderCollider"||t.name=="SteelLedger"))MuteFacility(t);
                if(south&&new[]{"DoorJamb","DoorLintel","Threshold","Rivet","IBeamWeb","IBeamFlange","BasePlate"}.Contains(t.name))MuteFacility(t);
                if(east&&t.position.z<W(0,5.2f).z&&new[]{"DoorJamb","DoorLintel","Threshold","Rivet","IBeamWeb","IBeamFlange","BasePlate"}.Contains(t.name))MuteFacility(t);
            }
            foreach(var id in new[]{"06","11","12"})MuteFacility(lobby.Find("FutureConnections/Portal_"+id));
            // Solid infill above and between the large portals: the building is enclosed.
            foreach(var run in new[]{(5f,7.5f),(11.1f,14f)})FacilityWall(g,"LobbyEastInfill",new(14.95f,run.Item1),new(14.95f,run.Item2),13);
            Box(g,"StagePortalHeader",W(14.95f,.5f,11.1f),new Vector3(.5f,3.8f,9),Mat("LA_WallRust"),true);
            Box(g,"ExpertOriginalHeader",W(14.95f,9.3f,8.6f),new Vector3(.5f,8.8f,3.6f),Mat("LA_WallRust"),true);
            foreach(var run in new[]{(-14.7f,-13.2f),(-4.1f,-1.5f)})FacilityWall(g,"LobbySouthInfill",new(run.Item1,-14.25f),new(run.Item2,-14.25f),13);
            foreach(var span in new[]{(-13.2f,-4.1f),(-1.5f,6.4f)})
            {
                var mid=(span.Item1+span.Item2)*.5f;float width=span.Item2-span.Item1;
                Box(g,"LogisticsPortalHeader",W(mid,-14.25f,11f),new Vector3(width,4,.5f),Mat("LA_WallRust"),true);
                Box(g,"LoadingApron",W(mid,-14.3f,-.1f),new Vector3(width,.2f,1.5f),Mat("LA_FloorConcrete"),true);
                OpenSpan(g,new(span.Item1,-14),new(span.Item2,-14),9);
            }
            FacilityLabel(g,"11  무대 장치",W(-8.5f,-13.9f,6.3f),Quaternion.Euler(0,180,0));
            FacilityLabel(g,"12  화물 적재",W(2.7f,-13.9f,6.3f),Quaternion.Euler(0,180,0));
            var hall=target.Find("S11_StageGear/LogisticsHall");if(hall==null)return;
            MuteShellWalls(hall);
            FacilityWall(g,"LogisticsWest",new(WhX0,WhZ0),new(WhX0,WhZ1),WhHeight);
            FacilityWall(g,"LogisticsSouth",new(WhX0,WhZ0),new(WhX1,WhZ0),WhHeight);
            FacilityWall(g,"LogisticsEast",new(WhX1,WhZ0),new(WhX1,WhZ1),WhHeight);
            foreach(var run in new[]{(WhX0,-13.2f),(-4.1f,-1.5f),(6.4f,WhX1)})FacilityWall(g,"LogisticsNorthInfill",new(run.Item1,WhZ1),new(run.Item2,WhZ1),WhHeight);
            foreach(var span in new[]{(-13.2f,-4.1f),(-1.5f,6.4f)})
                Box(g,"LoadingBayHeader",W((span.Item1+span.Item2)*.5f,WhZ1,10.5f),new Vector3(span.Item2-span.Item1,3,.3f),Mat("LA_WallRust"),true);
            foreach(var run in new[]{(-46f,-35.5f),(-28.7f,-22f)})
            {
                Box(g,"LowLogisticsDivider",W(3f,(run.Item1+run.Item2)*.5f,.55f),new Vector3(.12f,1.1f,run.Item2-run.Item1),Mat("LA_WallRust"),true);
                Box(g,"DividerCoping",W(3f,(run.Item1+run.Item2)*.5f,1.15f),new Vector3(.2f,.1f,run.Item2-run.Item1),Mat("V4_Iron_Dark"));
            }
            FacilityLabel(g,"11  SCENERY / RIGGING",W(-10,-20,6.4f),Quaternion.Euler(0,180,0),.07f);
            FacilityLabel(g,"12  CARGO / DISPATCH",W(13,-20,6.4f),Quaternion.Euler(0,180,0),.07f);
            foreach(var x in new[]{-8.5f,2.7f})Box(g,"TransportLane",W(x,-17,.013f),new Vector3(.13f,.008f,9),Mat("LA_HazardPaint"));
        }

        static Mesh RetainedLobbyMesh()
        {
            var b=new MeshBuilder("LA_RetainedLobbyWalls",2);
            foreach(var w in Walls)
            {
                var dir=(w.b-w.a).normalized;float length=Vector2.Distance(w.a,w.b),cursor=0;
                Vector3 P(float d,float y)=>new Vector3(w.a.x+dir.x*d,y,w.a.y+dir.y*d);
                var normal=new Vector3(w.inward.x,0,w.inward.y);
                void Face(float a,float z,float lo,float hi,int material,float tile){if(z-a>.01f&&hi-lo>.01f)b.Quad(material,P(a,lo),P(a,hi),P(z,hi),P(z,lo),normal,tile);}
                foreach(var original in w.doors.Append(new Door("","",length+100,0)))
                {
                    var d=original;float h=DoorHeight;
                    if(d.Id=="06"){d.Width=9;h=9.2f;}
                    if(d.Id=="11"){d.At=NotchX+8.65f;d.Width=9.1f;h=9;}
                    if(d.Id=="12"){d.At=NotchX-2.45f;d.Width=7.9f;h=9;}
                    float end=Mathf.Min(length,d.At-d.Width*.5f);
                    Face(cursor,end,0,LedgerY,0,4);Face(cursor,end,LedgerY,WallHeight,1,3);
                    if(d.At>length)break;
                    Face(d.At-d.Width*.5f,d.At+d.Width*.5f,h,LedgerY,0,4);
                    Face(d.At-d.Width*.5f,d.At+d.Width*.5f,Mathf.Max(h,LedgerY),WallHeight,1,3);
                    cursor=d.At+d.Width*.5f;
                }
            }
            return SaveMesh(b.Build());
        }

    }
}
