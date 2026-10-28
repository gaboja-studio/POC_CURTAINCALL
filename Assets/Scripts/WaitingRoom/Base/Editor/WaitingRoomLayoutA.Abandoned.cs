using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // Local room coordinates: Frame8 can move/rotate the room without invalidating this pass.
        static Transform AbGroup(Transform p, string name, Vector3 at, float yaw = 0)
        {
            var t = Group(p, name, p.TransformPoint(at));
            t.localRotation = Quaternion.Euler(0, yaw, 0); return t;
        }
        static Transform AbBox(Transform p, string n, Vector3 at, Vector3 size, Material m, bool solid = false)
            => BoxLocal(p, n, at, size, m, solid);
        static Transform AbShape(Transform p, string n, PrimitiveType type, Vector3 at, Vector3 size, Material m)
        {
            var t = Prim(p, type, n, Vector3.zero, size, m); t.localPosition = at; t.localRotation = Quaternion.identity; return t;
        }
        static Transform AbRod(Transform p, string n, Vector3 a, Vector3 b, float radius, Material m)
        {
            var t = AbShape(p,n,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius*2,(b-a).magnitude*.5f,radius*2),m);
            t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t;
        }
        static Material AbMetal()=>Lit("LA_AbandonedOxide",Hex("65594B"),.17f,FactoryTex("TileRust/T_TileRust_BC.TGA"),metallic:.45f);
        static Material AbPaint()=>Lit("LA_AbandonedInstitutionGreen",Hex("55615B"),.14f,FactoryTex("Concrete/T_Concrete_BC.TGA"));
        static Material AbDark()=>Mat("V4_Iron_Dark");
        static Material AbGlow()
        {
            var m=Lit("LA_AbandonedPhosphor",Hex("38605C"),.3f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Hex("446F62")*.75f);return m;
        }
        static void AbText(Transform p,string text,Vector3 at,float size,Color color)
        {
            var g=AbGroup(p,"Label_"+text,at);g.localRotation=Quaternion.Euler(0,180,0);var t=g.gameObject.AddComponent<TextMesh>();
            t.text=text;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.color=color;
            g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static void AbLight(Transform p,Vector3 at,Color color,float intensity,float range)
        {
            var t=AbGroup(p,"LocalLight",at);var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;
        }
        static Transform AbPrefab(Transform p,string path,Vector3 at,float yaw,float maxSize)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path)??throw new InvalidOperationException("Missing dressing prefab: "+path);
            var t=((GameObject)PrefabUtility.InstantiatePrefab(asset,p)).transform;t.localPosition=Vector3.zero;t.localRotation=Quaternion.Euler(0,yaw,0);
            var b=Frame8VisibleBounds(t);float scale=maxSize/Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z));t.localScale*=scale;
            b=Frame8VisibleBounds(t);t.position+=p.TransformPoint(at)-new Vector3(b.center.x,b.min.y,b.center.z);
            foreach(var c in t.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
            return t;
        }

        public static string DetailAbandonedStorage()
        {
            var (scene,target)=Begin(true);var original=Snapshot(source);int undo=StartUndo("Abandoned storage: research lockers and retired troupe");
            try
            {
                var room=target.Find("S05_JunkStorage")??throw new InvalidOperationException("Storage room missing");
                foreach(var n in new[]{"Archive","UnderSheets","S05_JunkStorage"})
                {var t=room.Find(n);if(t!=null){Undo.RecordObject(t.gameObject,"Preserve previous dressing inactive");t.gameObject.SetActive(false);}}
                Remove(room,"AbandonedResearchStorage");var g=AbGroup(room,"AbandonedResearchStorage",Vector3.zero);
                // Two transverse partitions split the room into three bays. A 2.6 m spine and both west doors stay open.
                foreach(var z in new[]{-2.1f,3.1f})
                {
                    AbPartition(g,new Vector3(3.25f,0,z),6.5f);
                    AbPartition(g,new Vector3(-6.1f,0,z),2.1f);
                }
                var records=AbGroup(g,"01_MonitoringAndRecords",new Vector3(2.3f,0,-8.7f));
                AbDesk(records,Vector3.zero,4.4f);
                AbComputer(records,new Vector3(-1.4f,1.02f,0),false);
                AbComputer(records,new Vector3(.15f,1.02f,0),true);
                AbBooks(records,new Vector3(1.3f,1.02f,.1f),5);
                AbChair(records,new Vector3(-1.35f,0,1.2f),-14);
                AbChair(records,new Vector3(.4f,0,1.6f),23);
                AbMonitor(g,new Vector3(2.3f,3.5f,-9.8f),5.2f,2.35f,true);
                AbText(g,"ARCHIVE / 04",new Vector3(2.3f,5.1f,-9.72f),.07f,Hex("A99E7C"));
                AbCabinet(g,new Vector3(-5.8f,0,-9.1f),0,3,true);
                AbBrokenFrame(g,new Vector3(-3.35f,2.75f,-9.89f),7);
                AbBrokenFrame(g,new Vector3(-5.3f,3.2f,-9.89f),-12);
                AbSparks(g,new Vector3(4.7f,3.3f,-9.58f),1.2f);
                AbSparks(g,new Vector3(-5.8f,1.9f,-8.72f),3.1f);
                // Locker bank in the middle bay, facing the central spine.
                AbCabinet(g,new Vector3(6.4f,0,.45f),-90,5,false);
                AbDesk(AbGroup(g,"SortingBench",new Vector3(3.1f,0,1.9f)),Vector3.zero,2.8f);
                AbBooks(g,new Vector3(3.2f,1.02f,1.9f),3);
                AbShelf(g,new Vector3(-6.65f,0,.45f),90,3.8f,3.2f,12);
                // North bay: deep shelving against all three walls, upper and lower tiers densely loaded.
                AbShelf(g,new Vector3(3.8f,0,9.25f),180,5.3f,4f,23);
                AbShelf(g,new Vector3(-3.45f,0,9.25f),180,5.3f,4f,41);
                AbShelf(g,new Vector3(6.6f,0,6.35f),-90,3.6f,3.5f,62);
                AbRetiredEquipment(g,new Vector3(2.6f,0,5.15f));
                AbSparks(g,new Vector3(6.95f,4.8f,5.4f),2.4f);
                // Four distinct silhouettes and missing-part configurations. No physics or gameplay scripts are added.
                AbBody(g,"01_Bear_Seated_MissingLeftArm",new Vector3(4.9f,0,-5.25f),-45,0);
                AbBody(g,"02_Rabbit_Prone_MissingRightLeg",new Vector3(-3.3f,0,-7.2f),22,1);
                AbBody(g,"03_Ringmaster_Side_MissingHead",new Vector3(2.7f,0,-.45f),75,2);
                AbBody(g,"04_Clown_Slumped_MissingLowerLeg",new Vector3(-3.9f,0,7.65f),160,3);
                for(int i=0;i<18;i++)
                {
                    var p=new Vector3(-5.9f+(i%6)*2.1f,.012f,-8.9f+(i/6)*7.8f);
                    var paper=AbBox(g,"AbandonedPaper",p,new Vector3(.23f,.007f,.32f),Mat("LA_Paper"));paper.localRotation=Quaternion.Euler(0,i*47,0);
                }
                foreach(var p in new[]{new Vector3(1,5.9f,-5.8f),new Vector3(-1.4f,5.9f,.3f),new Vector3(.3f,5.9f,6.5f)})AbCeilingLamp(g,p,7f);
                foreach(var x in new[]{-6.8f,6.8f})
                {
                    AbRod(g,"CableConduit",new Vector3(x,5.7f,-9.8f),new Vector3(x,5.7f,9.8f),.035f,AbDark());
                    for(float z=-9;z<10;z+=3)AbBox(g,"WallBracket",new Vector3(x,5.7f,z),new Vector3(.5f,.1f,.12f),AbMetal());
                }
                AbLight(g,new Vector3(2.3f,2.6f,-8),new Color(.55f,.7f,.65f),.7f,5);
                ArrangeAbandonedDisplay(room);
                AbFinish(g);Physics.SyncTransforms();AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                return $"Storage complete: 4 retired troupe figures; 3 bays; {g.GetComponentsInChildren<Renderer>().Length} renderers; 3 spark emitters; original preserved.";
            }
            catch {Undo.RevertAllDownToGroup(undo);throw;}
        }

        public static string ArrangeRetiredTroupeDisplay()
        {
            var (scene,target)=Begin(true);int undo=StartUndo("Group retired troupe as one mannequin exhibit");
            try{ArrangeAbandonedDisplay(target.Find("S05_JunkStorage"));Physics.SyncTransforms();Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();return "Four original poses lined up on the east wall display beside the lockers";}
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        static void ArrangeAbandonedDisplay(Transform room)
        {
            var g=room.Find("AbandonedResearchStorage");
            var bench=g.Find("SortingBench");if(bench!=null){Undo.RecordObject(bench,"Clear display bay");bench.localPosition=new Vector3(-5.7f,0,-7.3f);bench.localRotation=Quaternion.Euler(0,90,0);}
            var books=g.Find("FormerTroupeJournals");if(books!=null){Undo.RecordObject(books,"Move books with sorting bench");books.localPosition=new Vector3(-5.7f,1.02f,-7.3f);}
            var previousDisplay=g.Find("RetiredTroupeDisplay");
            if(previousDisplay!=null)foreach(var body in previousDisplay.Cast<Transform>().Where(t=>t.name.Contains("_Missing")).ToArray())Undo.SetTransformParent(body,g,"Retain existing exhibit poses on rebuild");
            // Empty east wall of the records bay, beside the locker bank across the partition. Wall face is local X=7.5.
            const float length=7.45f,depth=2f,top=.23f;
            Remove(g,"RetiredTroupeDisplay");var display=AbGroup(g,"RetiredTroupeDisplay",new Vector3(7.5f-depth*.5f,0,-9.75f+length*.5f));
            AbBox(display,"SharedMuseumPlinth",new Vector3(0,top*.5f,0),new Vector3(depth,top,length),AbPaint(),true);
            AbBox(display,"PlinthFrontTrim",new Vector3(-depth*.5f-.02f,.14f,0),new Vector3(.04f,.08f,length),AbMetal());
            var names=new[]{"01_Bear_Seated_MissingLeftArm","02_Rabbit_Prone_MissingRightLeg","03_Ringmaster_Side_MissingHead","04_Clown_Slumped_MissingLowerLeg"};
            // Joint poses are untouched. Seated figures face the room; the lying ones rest lengthwise so they stay on the plinth.
            var yaws=new[]{-90f,0f,-90f,-90f};
            float cursor=-length*.5f+.1f;
            for(int i=0;i<names.Length;i++)
            {
                var body=g.Find(names[i])??g.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==names[i]);
                if(body==null)throw new InvalidOperationException("Missing exhibit figure "+names[i]);
                Undo.SetTransformParent(body,display,"Line up four costume figures along the wall");Undo.RecordObject(body,"Place figure without changing its pose");
                body.localPosition=new Vector3(0,top,0);body.localRotation=Quaternion.Euler(0,yaws[i],0);
                var b=AbLocalBounds(display,body);
                body.localPosition+=new Vector3(depth*.5f-.1f-b.max.x,0,cursor-b.min.z);
                float centre=cursor+b.size.z*.5f;cursor+=b.size.z+.2f;
                // Plates hang on the plinth face so reaching limbs never cover them.
                var plate=AbGroup(display,"ExhibitNumber_"+(i+1),new Vector3(-depth*.5f-.05f,top*.5f,centre),90);
                AbBox(plate,"BrassInventoryPlate",Vector3.zero,new Vector3(.5f,.15f,.02f),Mat("LA_Bronze"));
                var label=AbGroup(plate,"Number",new Vector3(0,0,-.012f));var text=label.gameObject.AddComponent<TextMesh>();text.text="0"+(i+1)+" / RETIRED";text.fontSize=48;text.characterSize=.016f;text.anchor=TextAnchor.MiddleCenter;text.color=Hex("D0BD8C");
            }
            if(cursor-.2f>length*.5f)throw new InvalidOperationException("Exhibit row overruns the wall plinth");
            var frontLabel=AbGroup(display,"ExhibitWallLabel",new Vector3(depth*.5f-.03f,2.4f,0),90);
            AbBox(frontLabel,"LabelBackboard",new Vector3(0,0,.005f),new Vector3(2.6f,.36f,.02f),AbDark());
            var wallText=AbGroup(frontLabel,"Text",new Vector3(0,0,-.012f));
            var frontText=wallText.gameObject.AddComponent<TextMesh>();frontText.text="RETIRED TROUPE / ARCHIVE";frontText.fontSize=64;frontText.characterSize=.025f;frontText.anchor=TextAnchor.MiddleCenter;frontText.color=Hex("B5A37C");
            float rail=-depth*.5f-.12f,end=length*.5f-.05f;
            foreach(float z in new[]{-end,end})
            {AbRod(display,"DisplayBoundaryPost",new Vector3(rail,0,z),new Vector3(rail,.75f,z),.025f,AbMetal());}
            AbRod(display,"DisplayBoundaryRail",new Vector3(rail,.75f,-end),new Vector3(rail,.75f,end),.018f,AbMetal());
            AbLight(display,new Vector3(-.3f,3.4f,0),new Color(1,.82f,.61f),3.5f,6);
        }
        static Bounds AbLocalBounds(Transform frame,Transform t)
        {
            Bounds? result=null;
            foreach(var r in t.GetComponentsInChildren<Renderer>())
            {
                var w=r.bounds;
                for(int i=0;i<8;i++)
                {
                    var c=frame.InverseTransformPoint(w.center+Vector3.Scale(w.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(result==null)result=new Bounds(c,Vector3.zero);else{var b=result.Value;b.Encapsulate(c);result=b;}
                }
            }
            return result??new Bounds(t.position,Vector3.zero);
        }

        static void AbPartition(Transform p,Vector3 at,float width)
        {
            var g=AbGroup(p,"SteelMeshPartition",at);
            AbBox(g,"OpaqueLowerPanel",new Vector3(0,.7f,0),new Vector3(width,1.4f,.12f),AbPaint(),true);
            AbBox(g,"UpperCollision",new Vector3(0,2.15f,0),new Vector3(width,1.5f,.08f),AbDark(),true).GetComponent<Renderer>().enabled=false;
            foreach(float x in new[]{-width*.5f,width*.5f})AbBox(g,"PartitionPost",new Vector3(x,1.5f,0),new Vector3(.09f,3,.09f),AbMetal());
            foreach(float y in new[]{1.42f,2.95f})AbBox(g,"PartitionRail",new Vector3(0,y,0),new Vector3(width,.08f,.1f),AbMetal());
            for(float x=-width*.5f+.15f;x<width*.5f;x+=.23f)AbRod(g,"WireMesh",new Vector3(x,1.46f,0),new Vector3(x,2.91f,0),.008f,AbDark());
            for(float y=1.65f;y<2.9f;y+=.23f)AbRod(g,"WireMesh",new Vector3(-width*.5f,y,0),new Vector3(width*.5f,y,0),.008f,AbDark());
            for(float x=-width*.5f+.2f;x<width*.5f;x+=.85f)AbBox(g,"RustedPanelPatch",new Vector3(x,.65f,.065f),new Vector3(.13f,1.1f,.01f),AbMetal());
        }
        static void AbDesk(Transform p,Vector3 at,float width)
        {
            var g=AbGroup(p,"RecordsDesk",at);AbBox(g,"ScratchedWorktop",new Vector3(0,.96f,0),new Vector3(width,.1f,.95f),Mat("V4_Wood_Old"),true);
            foreach(float x in new[]{-width*.5f+.12f,width*.5f-.12f})foreach(float z in new[]{-.32f,.32f})AbBox(g,"SteelLeg",new Vector3(x,.45f,z),new Vector3(.065f,.9f,.065f),AbMetal());
            AbBox(g,"DrawerPedestal",new Vector3(width*.5f-.4f,.48f,0),new Vector3(.56f,.85f,.7f),AbPaint(),true);
            foreach(float y in new[]{.28f,.57f,.82f})AbBox(g,"DrawerPull",new Vector3(width*.5f-.4f,y,.37f),new Vector3(.23f,.025f,.04f),AbDark());
        }
        static void AbComputer(Transform p,Vector3 at,bool broken)
        {
            var g=AbGroup(p,broken?"DeadTerminal":"DormantComputer",at);
            AbBox(g,"CRTCase",new Vector3(0,.3f,-.13f),new Vector3(.72f,.58f,.61f),AbPaint());
            AbBox(g,"CRTGlass",new Vector3(0,.31f,.185f),new Vector3(.56f,.41f,.015f),Mat("V4_Black"));
            AbBox(g,"Keyboard",new Vector3(0,.025f,.48f),new Vector3(.72f,.05f,.24f),AbPaint());
            for(int r=0;r<4;r++)for(int c=0;c<12;c++)AbBox(g,"Key",new Vector3(-.31f+c*.055f,.059f,.4f+r*.047f),new Vector3(.038f,.018f,.031f),AbDark());
            AbBox(g,"ComputerTower",new Vector3(.66f,.33f,-.1f),new Vector3(.35f,.66f,.6f),AbPaint());
            for(int i=0;i<7;i++)AbBox(g,"Vent",new Vector3(.66f,.16f+i*.045f,.207f),new Vector3(.24f,.015f,.014f),AbDark());
            if(broken)for(int i=0;i<3;i++)AbRod(g,"CrackedScreen",new Vector3(-.2f+i*.11f,.14f,.2f),new Vector3(.15f,.48f-i*.08f,.2f),.005f,Mat("LA_Wrap"));
        }
        static void AbMonitor(Transform p,Vector3 at,float w,float h,bool wall)
        {
            var g=AbGroup(p,"LargeMonitoringScreen",at);
            AbBox(g,"SteelHousing",Vector3.zero,new Vector3(w+.22f,h+.22f,.22f),AbDark());
            AbBox(g,"DeadGlass",new Vector3(0,0,.12f),new Vector3(w,h,.018f),Mat("V4_Black"));
            for(int r=0;r<2;r++)for(int c=0;c<3;c++)
            {
                var v=new Vector3((c-1)*(w/3),(.5f-r)*(h/2),.135f);
                AbBox(g,"OfflineCameraFeed",v,new Vector3(w/3-.07f,h/2-.07f,.008f),c==0&&r==0?AbGlow():AbPaint());
                AbText(g,$"CAM {r*3+c+1:00}\nNO SIGNAL",v+new Vector3(0,0,.015f),.035f,Hex("8D9C87"));
                for(int j=0;j<5;j++)AbBox(g,"Scanline",v+new Vector3(0,-.35f+j*.15f,.012f),new Vector3(w/3-.1f,.012f,.006f),AbDark());
            }
            AbRod(g,"ScreenPowerCable",new Vector3(w*.5f,-h*.5f,0),new Vector3(w*.5f,-at.y+.2f,0),.025f,AbDark());
        }
        static void AbCabinet(Transform p,Vector3 at,float yaw,int count,bool electrical)
        {
            var g=AbGroup(p,electrical?"ElectricalCabinetBank":"TroupeLockerBank",at,yaw);
            for(int i=0;i<count;i++)
            {
                float x=(i-(count-1)*.5f)*.72f;var locker=AbGroup(g,"Locker_"+(i+1),new Vector3(x,0,0));
                AbBox(locker,"CabinetBack",new Vector3(0,1.35f,-.38f),new Vector3(.68f,2.7f,.08f),AbMetal(),true);
                foreach(float side in new[]{-.34f,.34f})AbBox(locker,"CabinetSide",new Vector3(side,1.35f,0),new Vector3(.04f,2.7f,.8f),AbPaint(),true);
                foreach(float y in new[]{.1f,1.9f,2.7f})AbBox(locker,"CabinetShelf",new Vector3(0,y,0),new Vector3(.7f,.045f,.8f),AbMetal());
                var hinge=AbGroup(locker,"DoorHinge",new Vector3(-.33f,0,.4f),i%3==1?-48:0);
                AbBox(hinge,"DentedDoor",new Vector3(.33f,1.36f,0),new Vector3(.66f,2.62f,.035f),AbPaint(),true);
                AbBox(hinge,"Handle",new Vector3(.55f,1.35f,.04f),new Vector3(.025f,.17f,.05f),AbMetal());
                for(int v=0;v<5;v++)AbBox(hinge,"VentSlit",new Vector3(.33f,2.1f+v*.07f,.025f),new Vector3(.42f,.025f,.012f),AbDark());
                AbText(hinge,$"{i+17:00}",new Vector3(.33f,1.82f,.03f),.035f,Hex("BDB5A1"));
                AbBox(locker,"ForgottenBox",new Vector3(0,2.95f,-.02f),new Vector3(.55f,.44f,.6f),Mat("LA_Cardboard"));
                if(i%3==1)AbBox(locker,"HangingRetiredCoat",new Vector3(0,1.25f,0),new Vector3(.38f,1.1f,.21f),Mat("LA_TentRed"));
            }
        }
        static void AbShelf(Transform p,Vector3 at,float yaw,float width,float height,int seed)
        {
            var g=AbGroup(p,"LoadedSalvageShelving",at,yaw);var rng=new System.Random(seed);
            foreach(float x in new[]{-width*.5f,width*.5f})foreach(float z in new[]{-.43f,.43f})AbBox(g,"AngleIronUpright",new Vector3(x,height*.5f,z),new Vector3(.07f,height,.07f),AbMetal());
            int levels=4;
            for(int level=0;level<levels;level++)
            {
                float y=.16f+level*(height-.35f)/3;
                AbBox(g,"DustyShelf",new Vector3(0,y,0),new Vector3(width,.07f,.98f),AbMetal());
                int count=Mathf.FloorToInt(width/.66f);
                for(int j=0;j<count;j++)
                {
                    float x=-width*.5f+.34f+j*(width-.65f)/Mathf.Max(1,count-1);float h=.22f+(float)rng.NextDouble()*.4f;
                    if((j+level+seed)%4==0)
                    {
                        AbShape(g,"UnusedCanister",PrimitiveType.Cylinder,new Vector3(x,y+.27f,0),new Vector3(.36f,.22f,.36f),AbMetal());
                        AbBox(g,"CanisterCap",new Vector3(x,y+.51f,0),new Vector3(.12f,.06f,.12f),AbDark());
                    }
                    else if((j+level)%5==1)
                    {
                        AbBox(g,"DiscardedInstrument",new Vector3(x,y+.25f,0),new Vector3(.52f,.43f,.58f),AbPaint());
                        AbBox(g,"DeadInstrumentGlass",new Vector3(x,y+.29f,.3f),new Vector3(.35f,.22f,.012f),Mat("V4_Black"));
                        foreach(float dx in new[]{-.15f,.15f})AbShape(g,"InstrumentKnob",PrimitiveType.Sphere,new Vector3(x+dx,y+.12f,.32f),Vector3.one*.045f,Mat("LA_Bronze"));
                    }
                    else
                    {
                        AbBox(g,"RetiredEquipmentCase",new Vector3(x,y+.04f+h*.5f,0),new Vector3(.43f+(float)rng.NextDouble()*.14f,h,.64f),j%3==0?AbPaint():Mat("LA_Cardboard"));
                        AbBox(g,"PaperInventoryLabel",new Vector3(x,y+.06f+h*.5f,.326f),new Vector3(.19f,.09f,.008f),Mat("LA_Paper"));
                        AbBox(g,"PackingStrap",new Vector3(x+.13f,y+.04f+h*.5f,.328f),new Vector3(.025f,h,.01f),Mat("LA_TentCream"));
                    }
                }
            }
            AbBox(g,"ShelfCollision",new Vector3(0,height*.5f,0),new Vector3(width,height,.98f),AbDark(),true).GetComponent<Renderer>().enabled=false;
        }
        static void AbBooks(Transform p,Vector3 at,int count)
        {
            var g=AbGroup(p,"FormerTroupeJournals",at);
            AbPrefab(g,"Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Item/SM_Item_Book_Open_01.prefab",Vector3.zero,10,.54f);
            for(int i=0;i<count;i++)AbPrefab(g,"Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Item/SM_Item_Book_01.prefab",new Vector3(.55f,.055f*i,-.02f),-7+i*4,.33f);
            AbBox(g,"HandwrittenPage",new Vector3(-.43f,.01f,.13f),new Vector3(.24f,.005f,.32f),Mat("LA_Paper"));
            for(int i=0;i<8;i++)AbBox(g,"FadedHandwriting",new Vector3(-.43f,.014f,.02f+i*.028f),new Vector3(.15f-(i%3)*.02f,.002f,.003f),AbDark());
        }
        static void AbChair(Transform p,Vector3 at,float yaw)
        {
            var g=AbGroup(p,"AbandonedChair",at,yaw);AbBox(g,"Seat",new Vector3(0,.48f,0),new Vector3(.5f,.08f,.5f),AbPaint());
            AbBox(g,"Back",new Vector3(0,.89f,-.23f),new Vector3(.5f,.6f,.06f),AbPaint());
            foreach(float x in new[]{-.19f,.19f})foreach(float z in new[]{-.19f,.19f})AbRod(g,"Leg",new Vector3(x,0,z),new Vector3(x,.46f,z),.023f,AbMetal());
        }
        static void AbBrokenFrame(Transform p,Vector3 at,float tilt)
        {
            var g=AbGroup(p,"BrokenTroupePortrait",at);g.localRotation=Quaternion.Euler(0,0,tilt);
            AbBox(g,"FadedPhotograph",Vector3.zero,new Vector3(1.2f,.85f,.018f),Mat("LA_Paper"));
            foreach(float x in new[]{-.63f,.63f})AbBox(g,"FrameSide",new Vector3(x,0,.02f),new Vector3(.06f,.98f,.07f),Mat("LA_Bronze"));
            AbBox(g,"FrameTop",new Vector3(0,.46f,.02f),new Vector3(1.3f,.06f,.07f),Mat("LA_Bronze"));
            AbBox(g,"BrokenBottomLeft",new Vector3(-.38f,-.46f,.02f),new Vector3(.5f,.06f,.07f),Mat("LA_Bronze"));
            for(int i=0;i<4;i++){AbShape(g,"PortraitHead",PrimitiveType.Sphere,new Vector3(-.4f+i*.27f,.14f,.015f),new Vector3(.15f,.18f,.015f),AbPaint());AbBox(g,"PortraitCoat",new Vector3(-.4f+i*.27f,-.13f,.018f),new Vector3(.18f,.35f,.015f),AbPaint());}
            foreach(var v in new[]{new Vector3(-.5f,.35f,.04f),new Vector3(.55f,.25f,.04f),new Vector3(.42f,-.35f,.04f)})AbRod(g,"FracturedGlass",new Vector3(-.04f,-.1f,.04f),v,.006f,Mat("LA_Wrap"));
        }
        static void AbRetiredEquipment(Transform p,Vector3 at)
        {
            var g=AbGroup(p,"RetiredEquipmentPallet",at);
            AbPrefab(g,FactoryPrefabs+"SM_Woodpallet_1.prefab",Vector3.zero,0,2.4f);
            AbPrefab(g,FactoryPrefabs+"SM_Cablespool.prefab",new Vector3(.65f,.2f,.3f),0,1.1f);
            for(int i=0;i<3;i++)AbBox(g,"DiscardedTrunk",new Vector3(-.6f,.4f+i*.36f,0),new Vector3(.9f,.34f,.8f),i%2==0?AbPaint():Mat("V4_Wood_Old"));
            AbBox(g,"PalletCollider",new Vector3(0,.65f,0),new Vector3(2.5f,1.3f,1.5f),AbDark(),true).GetComponent<Renderer>().enabled=false;
        }
        static void AbBody(Transform p,string name,Vector3 at,float yaw,int pose)
        {
            var g=AbGroup(p,name,at,yaw);var cloth=Lit("LA_RetiredCostume_"+pose,Hex(new[]{"6E5944","817760","592828","485754"}[pose]),.04f,FactoryTex("Concrete/T_Concrete_BC.TGA"));
            var seam=Mat("LA_TentCream");var flesh=Lit("LA_DesiccatedSkin",Hex("767268"),.02f);
            // Anatomical joint positions are authored per pose, not rigidly rotated standing figures.
            Vector3 hip,chest,neck,leftKnee,leftFoot,rightKnee,rightFoot,leftElbow,leftHand,rightElbow,rightHand;
            if(pose==0){hip=new(0,.28f,0);chest=new(0,.85f,-.13f);neck=new(.05f,1.15f,-.08f);leftKnee=new(-.3f,.15f,.55f);leftFoot=new(-.42f,.12f,1);rightKnee=new(.4f,.38f,.45f);rightFoot=new(.65f,.1f,.7f);leftElbow=leftHand=Vector3.zero;rightElbow=new(.48f,.52f,.1f);rightHand=new(.4f,.2f,.47f);}
            else if(pose==1){hip=new(0,.26f,0);chest=new(.04f,.23f,-.58f);neck=new(.1f,.2f,-.93f);leftKnee=new(-.23f,.18f,.48f);leftFoot=new(-.42f,.15f,.94f);rightKnee=rightFoot=Vector3.zero;leftElbow=new(-.55f,.16f,-.45f);leftHand=new(-.73f,.13f,-.8f);rightElbow=new(.43f,.14f,-.78f);rightHand=new(.3f,.12f,-1.1f);}
            else if(pose==2){hip=new(0,.29f,0);chest=new(.12f,.36f,-.55f);neck=new(.16f,.34f,-.9f);leftKnee=new(-.23f,.18f,.43f);leftFoot=new(-.5f,.13f,.72f);rightKnee=new(.37f,.24f,.3f);rightFoot=new(.12f,.17f,.72f);leftElbow=new(-.35f,.15f,-.6f);leftHand=new(-.58f,.12f,-.92f);rightElbow=new(.4f,.55f,-.3f);rightHand=new(.08f,.5f,-.49f);}
            else{hip=new(0,.3f,0);chest=new(.2f,.72f,.25f);neck=new(.25f,.95f,.39f);leftKnee=new(-.35f,.18f,.45f);leftFoot=new(-.75f,.12f,.63f);rightKnee=new(.38f,.16f,.5f);rightFoot=Vector3.zero;leftElbow=new(-.25f,.45f,.47f);leftHand=new(-.42f,.12f,.66f);rightElbow=new(.56f,.34f,.43f);rightHand=new(.67f,.1f,.6f);}
            void Limb(string n,Vector3 a,Vector3 b,float radius,Material m){var t=AbShape(g,n,PrimitiveType.Capsule,(a+b)*.5f,new Vector3(radius*2,Vector3.Distance(a,b)*.5f+radius*.35f,radius*2),m);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
            Limb("CostumeTorso",hip,chest,.26f,cloth);AbShape(g,"PaddedPelvis",PrimitiveType.Sphere,hip,new Vector3(.51f,.32f,.35f),cloth);
            Vector3 shoulderL=chest+new Vector3(-.28f,0,0),shoulderR=chest+new Vector3(.28f,0,0);
            if(pose!=0){Limb("LeftSleeve",shoulderL,leftElbow,.105f,cloth);Limb("LeftForearm",leftElbow,leftHand,.085f,cloth);AbShape(g,"LeftHand",PrimitiveType.Sphere,leftHand,new Vector3(.14f,.08f,.19f),flesh);}
            else AbTornEnd(g,shoulderL,Vector3.left,.115f,cloth);
            Limb("RightSleeve",shoulderR,rightElbow,.105f,cloth);Limb("RightForearm",rightElbow,rightHand,.085f,cloth);AbShape(g,"RightHand",PrimitiveType.Sphere,rightHand,new Vector3(.14f,.08f,.19f),flesh);
            Limb("LeftTrouser",hip+Vector3.left*.15f,leftKnee,.13f,cloth);Limb("LeftLowerLeg",leftKnee,leftFoot,.1f,cloth);AbShape(g,"LeftBoot",PrimitiveType.Sphere,leftFoot,new Vector3(.21f,.18f,.32f),AbDark());
            if(pose!=1){Limb("RightTrouser",hip+Vector3.right*.15f,rightKnee,.13f,cloth);if(pose!=3){Limb("RightLowerLeg",rightKnee,rightFoot,.1f,cloth);AbShape(g,"RightBoot",PrimitiveType.Sphere,rightFoot,new Vector3(.21f,.18f,.32f),AbDark());}else AbTornEnd(g,rightKnee,Vector3.forward,.105f,cloth);}
            else AbTornEnd(g,hip+Vector3.right*.18f,Vector3.forward,.13f,cloth);
            if(pose!=2)
            {
                Limb("ExposedNeck",chest,neck,.09f,flesh);
                string src="Mood/InteriorArt/Stage03_PropRoom/StoredMascotHead_"+(pose==0?2:pose==1?1:0);
                var head=Object.Instantiate(Required(src).gameObject,g).transform;head.name="WornMascotHead";head.gameObject.SetActive(true);head.localRotation=Quaternion.Euler(pose==1?95:pose==3?35:12,pose==0?-18:15,pose==3?-15:8);
                foreach(var c in head.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                var bounds=Frame8VisibleBounds(head);head.localScale*=.58f/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                bounds=Frame8VisibleBounds(head);head.position+=g.TransformPoint(neck+new Vector3(0,pose==1?.12f:.08f,pose==1?-.14f:0))-bounds.center;
            }
            else AbTornEnd(g,neck,(neck-chest).normalized,.1f,cloth);
            // Faded costume trim, buttons, repair patches and a crooked troupe number.
            var front=pose==1?Vector3.up:pose==0?Vector3.forward:new Vector3(0,.8f,.4f);
            for(int i=0;i<4;i++)AbShape(g,"TarnishedCostumeButton",PrimitiveType.Sphere,Vector3.Lerp(hip,chest,.3f+i*.17f)+front*.24f,Vector3.one*.035f,Mat("LA_Bronze"));
            for(int i=0;i<5;i++)AbRod(g,"ExposedCostumeStitch",Vector3.Lerp(hip,chest,i*.18f)+front*.25f+Vector3.left*.12f,Vector3.Lerp(hip,chest,i*.18f)+front*.26f+Vector3.left*.07f,.006f,seam);
            if(pose==2){var flap=AbBox(g,"RingmasterCoatTail",hip+new Vector3(.2f,.04f,.15f),new Vector3(.46f,.04f,.6f),cloth);flap.localRotation=Quaternion.Euler(0,24,0);}
        }
        static void AbTornEnd(Transform p,Vector3 at,Vector3 normal,float radius,Material cloth)
        {
            var t=AbShape(p,"MissingPart_DarkCavity",PrimitiveType.Cylinder,at,new Vector3(radius*1.8f,.012f,radius*1.8f),AbDark());t.localRotation=Quaternion.FromToRotation(Vector3.up,normal);
            var q=Quaternion.FromToRotation(Vector3.up,normal);
            for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;var v=q*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);AbRod(p,"FrayedCostumeEdge",at+v,at+v+normal*(.025f+i%3*.018f),.009f,cloth);}
        }
        static void AbSparks(Transform p,Vector3 at,float delay)
        {
            var g=AbGroup(p,"FaultyJunction_Sparks",at);
            AbBox(g,"JunctionBox",Vector3.zero,new Vector3(.24f,.35f,.12f),AbMetal());
            AbRod(g,"BrokenConductor",new Vector3(0,-.1f,.1f),new Vector3(.16f,-.28f,.2f),.014f,Mat("LA_Bronze"));
            var e=AbGroup(g,"IntermittentSparks",new Vector3(.1f,-.15f,.16f));var ps=e.gameObject.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=4.8f;main.loop=true;main.startDelay=delay;main.startLifetime=new ParticleSystem.MinMaxCurve(.16f,.48f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.2f,3.7f);main.startSize=new ParticleSystem.MinMaxCurve(.015f,.035f);main.startColor=new Color(1,.67f,.2f);main.gravityModifier=1.1f;main.maxParticles=28;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(.2f,10),new ParticleSystem.Burst(.34f,5),new ParticleSystem.Burst(3.4f,7)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.035f;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2.5f;renderer.velocityScale=.07f;
            var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDir+"/LA_AbandonedSpark.mat");
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.SetColor("_BaseColor",new Color(5,2.6f,.7f));AssetDatabase.CreateAsset(mat,AssetDir+"/LA_AbandonedSpark.mat");}renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;ps.Play();
        }
        static void AbCeilingLamp(Transform p,Vector3 at,float ceiling)
        {
            AbRod(p,"LampSuspension",at+Vector3.up*.15f,new Vector3(at.x,ceiling,at.z),.02f,AbDark());
            AbBox(p,"LampHousing",at,new Vector3(1.3f,.1f,.22f),AbMetal());
            AbBox(p,"AgedTube",at-Vector3.up*.07f,new Vector3(1.15f,.05f,.08f),Mat("LA_Bulb"));
            AbLight(p,at-Vector3.up*.25f,new Color(1,.88f,.67f),7f,13);
        }
        static void AbFinish(Transform g)
        {
            foreach(var r in g.GetComponentsInChildren<Renderer>())if(r.bounds.size.magnitude<.65f)r.shadowCastingMode=ShadowCastingMode.Off;
        }

        public static string DetailAbandonedLobby()
        {
            var (scene,target)=Begin(true);var original=Snapshot(source);int undo=StartUndo("Lobby abandoned factory dressing");
            try
            {
                var lobby=target.Find("S01_Lobby");Remove(lobby,"AbandonedFactoryDressing");var g=AbGroup(lobby,"AbandonedFactoryDressing",Vector3.zero);
                var passages=Frame8Passages();int bays=0;
                // Work only along solid wall arcs; retain generous clear space at every mouth and the stair.
                for(int i=0;i<24;i++)
                {
                    float angle=i*15*Mathf.Deg2Rad;var normal=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var wall=DomeC+normal*(Frame8LobbyR-.65f);
                    if(passages.Any(p=>p.Cuts(DomeC,wall,1.5f))||wall.x< -16&&wall.y>79||Frame8InGallery(wall))continue;
                    var bay=AbGroup(g,"DerelictWallBay_"+i,lobby.InverseTransformPoint(new Vector3(wall.x,0,wall.y)),Mathf.Atan2(-normal.x,-normal.y)*Mathf.Rad2Deg);bays++;
                    AbBox(bay,"PeelingLowerPanel",new Vector3(0,1.35f,0),new Vector3(1.6f,2.7f,.04f),AbPaint());
                    for(int j=0;j<6;j++)AbBox(bay,"OxideStreak",new Vector3(-.65f+j*.24f,2.2f-(j%3)*.35f,.03f),new Vector3(.045f,.7f+(j%3)*.3f,.008f),AbMetal());
                    AbRod(bay,"ExposedRiser",new Vector3(-.65f,.2f,.2f),new Vector3(-.65f,7.65f,.2f),.085f,AbMetal());
                    foreach(float y in new[]{.5f,2.6f,5.3f,7.3f})AbBox(bay,"PipeBracket",new Vector3(-.65f,y,.12f),new Vector3(.29f,.07f,.32f),AbDark());
                    if(i%3==0){AbBox(bay,"DisusedSwitchgear",new Vector3(.25f,1.3f,.28f),new Vector3(.8f,1.8f,.46f),AbPaint(),true);for(int j=0;j<5;j++)AbBox(bay,"SwitchVent",new Vector3(.25f,1.65f-j*.09f,.52f),new Vector3(.5f,.035f,.02f),AbDark());AbText(bay,"OFFLINE",new Vector3(.25f,1.15f,.53f),.025f,Hex("AD9E81"));}
                    else{AbPrefab(bay,FactoryPrefabs+"SM_Barrel_1.prefab",new Vector3(.25f,0,.5f),i*13,1.1f);AbBox(bay,"DiscardedPackingCase",new Vector3(-.25f,.23f,1f),new Vector3(.6f,.46f,.55f),Mat("V4_Wood_Old"),true);}
                    for(int j=0;j<4;j++){var plank=AbBox(bay,"DiscardedBoard",new Vector3(.4f,.04f+j*.035f,.85f),new Vector3(1.1f,.035f,.13f),Mat("V4_Wood_Old"));plank.localRotation=Quaternion.Euler(0,j*13-20,0);}
                    // Rust pools and cracked flooring are non-colliding and sit directly on the floor.
                    var stain=AbShape(bay,"OldOilPool",PrimitiveType.Quad,new Vector3(0,.012f,.6f),new Vector3(2,1.5f,1),Mat("LA_PuddleDecal"));stain.localRotation=Quaternion.Euler(90,0,0);
                }
                // Weather the upper gallery with structural cross braces and suspended cable trays.
                foreach(float z in new[]{74.8f,80.2f})
                {
                    var a=lobby.InverseTransformPoint(new Vector3(-15.2f,8.9f,z));var b=lobby.InverseTransformPoint(new Vector3(2.5f,8.9f,z));
                    AbRod(g,"OverheadTrunkPipe",a,b,.14f,AbMetal());
                    for(int j=0;j<5;j++){var v=Vector3.Lerp(a,b,j/4f);float roof=DomeHeightAt(Vector2.Distance(new Vector2(v.x,v.z+70),DomeC),Frame8LobbyR,DomeWallH,Frame8TopH);if(roof>9)AbRod(g,"AnchoredPipeHanger",v,new Vector3(v.x,roof-.1f,v.z),.02f,AbDark());}
                }
                // Preserve a readable, warm circus focal point while the factory perimeter remains desaturated.
                foreach(var l in lobby.GetComponentsInChildren<Light>())
                {
                    if(l.transform.IsChildOf(g))continue;Undo.RecordObject(l,"Warm lobby working lights");
                    Color.RGBToHSV(l.color,out _,out float saturation,out _);if(saturation<.5f)l.color=new Color(1,.87f,.7f);
                }
                AbFinish(g);Physics.SyncTransforms();AssertUnchanged(original);Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                return $"Lobby: {bays} abandoned wall bays; retained dome, stair, services and circulation; original preserved.";
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
    }
}
