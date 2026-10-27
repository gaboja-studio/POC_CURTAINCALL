using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string Factory = "Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Prefabs/";
        const string Industrial = "Assets/_ThirdParty/LeartesStudios/HDRP/Art/Prefabs/";
        const float Depth = 60, HallHeight = 28;
        static Material concrete, wetFloor, iron, rust;

        // Reference plan: X 0..192, Z 0..76; Y0 walking level, local landing Y+2.
        // The 60m foundation and 28m upper shell express the concept's scale, not stunt tuning.
        [MenuItem("Tools/CurtainCall/탈출로/구조도 기준 배경 재배치")]
        public static void BuildReference()
        {
            PrepareReference();
            BuildReferenceStart(); BuildReferenceMiddle(); BuildReferenceEnd(); DressReferenceDepth(); FinishReference(); ApplyWideLayout(); ConfigurePlayerReference(); ApplyConcreteLook();
        }

        public static void PrepareReference()
        {
            if(SceneManager.GetActiveScene().path!=ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("escape_route 편집 모드가 필요합니다.");
            // Only this task's generated root is replaced; PlaySetup and original assets survive.
            var previous=GameObject.Find(RootName);if(previous!=null)Undo.DestroyObjectImmediate(previous);
            root=new GameObject(RootName).transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Reference escape background");
            InitReference();
        }
        static void InitReference()
        {
            root=GameObject.Find(RootName).transform;font=GetFont();
            concrete=Surface("V4_Wall_DamagedConcrete",.67f);wetFloor=Surface("V4_Floor_WetConcrete",.63f);
            iron=Existing("V4_Iron_Dark");rust=Existing("V4_RustyMetal");
            grey=Mat("placeholder",new Color(.3f,.34f,.37f));metal=iron;
            dark=Mat("void",new Color(.012f,.018f,.025f));amber=Mat("amber",new Color(.75f,.37f,.08f));
        }
        static Material Existing(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_ThirdParty/Materials/WaitingArea/"+name+".mat");
        static Material Surface(string name,float brightness)
        {
            var path=Materials+"escape-"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Existing(name)){name="escape-"+name,color=new Color(brightness,brightness,brightness)};AssetDatabase.CreateAsset(m,path);}return m;
        }

        public static void BuildReferenceStart()
        {
            InitReference();
            var a=Group("01_WaitingUnderstage_24x18");
            Platform(a,0,24,0,18,0);ShellX(a,0,0,24,12);ShellZ(a,0,0,18,12);ShellZ(a,24,0,18,12);
            ShellX(a,18,0,6,12);ShellX(a,18,18,24,12);Portal(a,12,18,12,8,0);
            for(int x=3;x<24;x+=8){Prop(a,"SM_Prop_BarrelStack_01",new Vector3(x,0,3),new Vector3(3,1.5f,2));Lamp(a,new Vector3(x,4,1),true);}
            Placeholder(a,"무대 리프트 하부",new Vector3(12,10,8),new Vector3(10,1,9));
            Sign(a,"01 · 공연장 하부 / 숨겨진 길",new Vector3(12,4,12));

            var b=Group("02_ConcreteApproach_12x48");
            Platform(b,6,18,18,66,0);ShellZ(b,6,18,66,HallHeight);ShellZ(b,18,18,50,HallHeight);
            // Open right side at the end reveals the entire first crossing.
            for(int z=22;z<=62;z+=10)ServiceBay(b,7,z,HallHeight,1);
            Portal(b,12,47,12,9,0);Placeholder(b,"버튼 문 위치",new Vector3(7,1.4f,45),new Vector3(.5f,1,.4f));
            Sign(b,"02 · 긴 진입 / 첫 공동의 드러남",new Vector3(12,4,25));

            var c=Group("03_CannonRopeVoid_40x20");
            Platform(c,18,27,50,70,0);Platform(c,35,41,55,65,2);Platform(c,49,58,50,70,2);
            ShellX(c,70,18,58,HallHeight);ShellX(c,50,27,46,HallHeight);ShellZ(c,58,50,70,HallHeight);
            Abyss(c,27,49,50,70);RailEdge(c,18,50,27,50,0);RailEdge(c,18,70,27,70,0);
            RailEdge(c,35,55,41,55,2);RailEdge(c,35,65,41,65,2);
            // Clear edge openings stay empty for the later gameplay implementation.
            for(int x=22;x<=56;x+=11)ServiceBay(c,x,69,HallHeight,0);
            IndustrialBeam(c,new Vector3(38,22,60),new Vector3(39,1.5f,1.2f));
            Placeholder(c,"대포 설치 자리",new Vector3(24,.3f,60),new Vector3(3,.6f,3));
            Sign(c,"03 · 고립 중간 발판  +2m",new Vector3(38,4,60));
            Sign(c,"외줄 연결 예정 · 기능 없음",new Vector3(45,4,60));
            // Physical cable is a visual marker, not an interactable tightrope.
            Cable(c,new Vector3(40,3,60),new Vector3(50,3,60),.055f,"외줄 연결 시각 표시");
            FoundationPipe(c,new Vector3(36,-27,55),22);
        }

        public static void BuildReferenceMiddle()
        {
            InitReference();
            var d=Group("04_BendAndShutters_16x36");
            Platform(d,46,62,14,42,0);Platform(d,46,62,48,50,2);
            // Six-metre stair flight descends from the +2m arrival to the Y0 gallery.
            for(int i=0;i<10;i++)FloorRect(d,46,62,42+i*.6f,42+(i+1)*.6f,(i+1)*.2f);
            ShellZ(d,46,14,50,HallHeight);ShellZ(d,62,36,50,HallHeight);ShellX(d,14,46,62,HallHeight);
            for(int z=18;z<48;z+=10)ServiceBay(d,47,z,HallHeight,1);
            Portal(d,54,40,16,9,0);Portal(d,54,26,16,9,0);
            Placeholder(d,"진행 레버 위치",new Vector3(48,1.5f,28),new Vector3(.7f,1.4f,.7f));
            Sign(d,"04 · 굽이 / 진행 문과 지연 셔터",new Vector3(54,4,38),180);

            var e=Group("05_SwingVoid_40x30");
            Platform(e,62,72,6,36,0);Platform(e,92,102,6,36,0);Abyss(e,72,92,6,36);
            ShellX(e,6,62,102,HallHeight);ShellX(e,36,62,92,HallHeight);ShellZ(e,102,6,36,HallHeight);
            for(int x=66;x<=100;x+=11)ServiceBay(e,x,7,HallHeight,0);
            RailEdge(e,62,6,72,6,0);RailEdge(e,92,6,102,6,0);
            IndustrialBeam(e,new Vector3(82,14,20),new Vector3(38,1.5f,1.2f));
            Placeholder(e,"트램폴린 설치 자리",new Vector3(69,.25f,20),new Vector3(4,.5f,4));
            for(int i=0;i<3;i++)
            {
                float x=76+i*7;
                Cable(e,new Vector3(x,14,19),new Vector3(x,5,19),.06f,"그네 지지선");
                Cable(e,new Vector3(x,14,21),new Vector3(x,5,21),.06f,"그네 지지선");
                var grip=Box(e,"그네 설치 가이드",new Vector3(x,5,20),new Vector3(.16f,.16f,2),grey);
                Sign(grip.transform,"그네 설치 가이드",new Vector3(x,6,20),90,4);
            }
            Sign(e,"05 · 20m 단절 공동 / 복합 횡단 공간",new Vector3(68,4,25));
            FoundationPipe(e,new Vector3(78,-35,10),26);

            var f=Group("06_Regroup_20x34");
            Platform(f,92,112,36,70,0);ShellZ(f,92,36,70,HallHeight);ShellZ(f,112,36,54,HallHeight);ShellX(f,70,92,112,HallHeight);
            for(int z=42;z<68;z+=11)ServiceBay(f,93,z,HallHeight,1);
            Prop(f,"SM_Prop_Generator_01",new Vector3(96,0,54),new Vector3(4,3,5));
            Sign(f,"06 · 넓은 착지 / 짧은 안도",new Vector3(102,4,43));
        }

        public static void BuildReferenceEnd()
        {
            InitReference();
            var g=Group("07A_FinalRun_LBend");
            Platform(g,112,156,54,70,0);Platform(g,140,156,30,54,0);
            ShellX(g,70,112,156,18);ShellX(g,54,112,140,18);ShellZ(g,156,30,70,18);ShellZ(g,140,30,54,18);
            for(int x=117;x<154;x+=10)ServiceBay(g,x,69,18,0);
            for(int z=35;z<64;z+=11)ServiceBay(g,155,z,18,1);
            Prop(g,"SM_Prop_Generator_01",new Vector3(138,0,57),new Vector3(4,4,5));
            Prop(g,"SM_Prop_BarrelStack_01",new Vector3(123,0,56),new Vector3(3,1.5f,2));
            Prop(g,"SM_Prop_Pallet_01",new Vector3(151,0,44),new Vector3(3,.6f,3));
            Sign(g,"07A · 반출동 / 마지막 질주",new Vector3(123,4,62),90);

            var h=Group("07B_ExitHall_28x30");
            Platform(h,140,168,0,30,0);ShellX(h,0,140,168,20);ShellZ(h,140,0,30,20);
            ShellX(h,30,156,168,20);ShellZ(h,168,0,10,20);ShellZ(h,168,20,30,20);
            Portal(h,148,29,16,10,0);Portal(h,168,15,10,10,90);
            for(int x=145;x<166;x+=10)ServiceBay(h,x,1,20,0);
            Placeholder(h,"출구 장치 작업 공간",new Vector3(155,.15f,15),new Vector3(7,.3f,7));
            Placeholder(h,"출구 잠금 장치",new Vector3(167,6,15),new Vector3(1,2,3));
            Sign(h,"07B · 최종 작업 공간 / 전원 이탈",new Vector3(153,4,22),90);

            var outside=Group("08_Reeds_24x30");
            FloorRect(outside,168,192,0,30,0);
            var ground=Box(outside,"갈대밭 주변 배경 지형",new Vector3(240,-1,15),new Vector3(144,1.8f,160),Mat("earth",new Color(.20f,.24f,.18f)));
            Sign(outside,"08 · 갈대밭 / 해방감과 안정감",new Vector3(179,3,15),90);
            var random=new System.Random(73);
            for(int i=0;i<260;i++)
            {
                float x=170+(float)random.NextDouble()*95,z=-35+(float)random.NextDouble()*100;
                if(x<194 && Mathf.Abs(z-15)<3.5f)continue;
                var grass=Real(outside,Factory+"SM_Grass5.prefab",new Vector3(x,0,z),new Vector3(3.5f,2.4f,3.5f));
                grass.transform.Rotate(0,(float)random.NextDouble()*360,0,Space.World);
            }
            for(int i=0;i<8;i++)
            {
                float x=172+i*3,z=i%2==0?9:21;var reeds=new GameObject("갈대 군락 (그레이박스)").transform;reeds.SetParent(outside,false);
                for(int j=0;j<10;j++){float y=2+(j%3)*.25f;Cable(reeds,new Vector3(x+j*.18f,0,z+(j%2)*.3f),new Vector3(x+j*.18f,y,z+(j%2)*.3f),.04f,"갈대 줄기");Box(reeds,"갈대 이삭",new Vector3(x+j*.18f,y,z+(j%2)*.3f),new Vector3(.13f,.5f,.13f),grey);}
                Sign(reeds,reeds.name,new Vector3(x+.7f,3,z),90,4);
            }
            BuildCeilingShell();
        }

        static void BuildCeilingShell()
        {
            var ceiling=Group("10_Ceilings_ToggleForPlanView");
            Ceiling(ceiling,0,24,0,18,12);Ceiling(ceiling,0,58,50,76,HallHeight);
            Ceiling(ceiling,0,18,18,50,HallHeight);Ceiling(ceiling,46,62,14,50,HallHeight);
            Ceiling(ceiling,62,102,6,42,HallHeight);Ceiling(ceiling,86,112,42,76,HallHeight);
            Ceiling(ceiling,112,156,54,76,18);Ceiling(ceiling,140,156,30,54,18);Ceiling(ceiling,140,168,0,30,20);
        }

        public static void FinishReference()
        {
            InitReference();var scene=SceneManager.GetActiveScene();
            var player=GameObject.Find("PlaySetup/Player");Undo.RecordObject(player.transform,"Place at reference entrance");player.transform.position=new Vector3(12,.05f,8);
            var temp=GameObject.Find("PlaySetup")?.transform.Find("TempFloor");if(temp!=null)temp.gameObject.SetActive(false);
            var sun=GameObject.Find("Directional Light").GetComponent<Light>();sun.intensity=1.15f;sun.color=new Color(1,.79f,.54f);sun.transform.rotation=Quaternion.Euler(23,-80,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.20f,.23f,.28f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.16f,.19f,.23f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=95;RenderSettings.fogEndDistance=330;
            AssetDatabase.SaveAssetIfDirty(font);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root.Find("03_CannonRopeVoid_40x20").gameObject;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(85,-10,38),Quaternion.Euler(52,-25,0),130,false,true);
        }
        public static string DressReferenceDepth()
        {
            InitReference();if(root.Find("11_DepthAndLandmarks")!=null)return "Already dressed";
            var p=Group("11_DepthAndLandmarks");
            foreach(var m in new[]{concrete,wetFloor}){m.SetFloat("_BumpScale",.35f);m.enableInstancing=true;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);}
            foreach(var l in root.GetComponentsInChildren<Light>())
                if(l.name=="Warm service light"){l.intensity=22;l.range=18;}
            foreach(var at in new[]{new Vector3(24,3,57),new Vector3(38,5,57),new Vector3(53,5,62),new Vector3(68,3,15),new Vector3(97,3,25),new Vector3(100,3,45),new Vector3(150,4,15)})
            {
                Lamp(p,at,true);var l=p.GetChild(p.childCount-1).GetComponent<Light>();l.intensity=32;l.range=15;
                var bulb=GameObject.CreatePrimitive(PrimitiveType.Sphere);bulb.name="작업등 전구";bulb.transform.SetParent(p,true);bulb.transform.position=at;bulb.transform.localScale=Vector3.one*.18f;
                bulb.GetComponent<Renderer>().sharedMaterial=Existing("V4_Bulb_Warm");UnityEngine.Object.DestroyImmediate(bulb.GetComponent<Collider>());
            }
            foreach(float z in new[]{56f,60f,64f})FoundationPipe(p,new Vector3(34,-55,z),56);
            foreach(float x in new[]{64f,66f,94f,96f})FoundationPipe(p,new Vector3(x,-54,6.6f),54);
            foreach(int y in new[]{-16,-35,12})
            {
                Real(p,Industrial+"SM_Scaffolding_Flooring.prefab",new Vector3(38,y,68),new Vector3(20,.35f,2),iron);
                RailEdge(p,28,67,48,67,y);
                Real(p,Industrial+"SM_Scaffolding_Flooring.prefab",new Vector3(82,y,8),new Vector3(20,.35f,2),iron);
                RailEdge(p,72,9,92,9,y);
                foreach(float x in new[]{30f,46f,74f,90f}){var l=new GameObject("Level depth light",typeof(Light));l.transform.SetParent(p,true);l.transform.position=new Vector3(x,y+2,x<50?68:8);l.GetComponent<Light>().range=12;l.GetComponent<Light>().intensity=9;l.GetComponent<Light>().color=new Color(1,.53f,.23f);}
            }
            Real(p,Industrial+"SM_Pipe_Big_01.prefab",new Vector3(38,-23,50),new Vector3(25,1.5f,1.5f));
            Real(p,Industrial+"SM_Pipe_Big_01.prefab",new Vector3(82,-25,6),new Vector3(32,1.8f,1.8f));
            for(int z=23;z<46;z+=8)Real(p,Industrial+"SM_Pipe_Medium_01.prefab",new Vector3(7.6f,9,z),new Vector3(.5f,.5f,8),null,Quaternion.Euler(0,90,0));
            var showcase=new GameObject("ScaleReference_1.8m",typeof(CapsuleCollider)).transform;showcase.SetParent(p,true);showcase.position=new Vector3(53,2,59);UnityEngine.Object.DestroyImmediate(showcase.GetComponent<Collider>());
            var person=GameObject.CreatePrimitive(PrimitiveType.Capsule);person.name="사람 크기 비교 1.8m";person.transform.SetParent(showcase,false);person.transform.localPosition=Vector3.up*.9f;person.transform.localScale=new Vector3(.5f,.9f,.5f);person.GetComponent<Renderer>().sharedMaterial=grey;UnityEngine.Object.DestroyImmediate(person.GetComponent<Collider>());Sign(showcase,person.name,new Vector3(53,4.4f,59),90,5);
            foreach(var label in root.GetComponentsInChildren<TMPro.TextMeshPro>())
            {
                var t=label.transform;t.localScale=Vector3.one;
                t.localScale=new Vector3(1/t.TransformVector(Vector3.right).magnitude,1/t.TransformVector(Vector3.up).magnitude,1/t.TransformVector(Vector3.forward).magnitude);
            }
            AssetDatabase.SaveAssetIfDirty(font);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return "Depth landmarks and practical lights placed";
        }

        static GameObject Prop(Transform p,string n,Vector3 at,Vector3 size)=>Asset(p,n,at,size);
        static GameObject Real(Transform p,string path,Vector3 at,Vector3 size,Material material=null,Quaternion? rotation=null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new InvalidOperationException(path);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,p);g.transform.SetPositionAndRotation(Vector3.zero,rotation??Quaternion.identity);
            FixInstanceLods(g);
            var rs=g.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            var worldScale=new Vector3(size.x/Mathf.Max(.01f,b.size.x),size.y/Mathf.Max(.01f,b.size.y),size.z/Mathf.Max(.01f,b.size.z));
            var localScale=Quaternion.Inverse(g.transform.rotation)*worldScale;localScale=new Vector3(Mathf.Abs(localScale.x),Mathf.Abs(localScale.y),Mathf.Abs(localScale.z));
            g.transform.localScale=Vector3.Scale(g.transform.localScale,localScale);
            b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);g.transform.position+=at-new Vector3(b.center.x,b.min.y,b.center.z);
            foreach(var r in rs){r.sharedMaterials=r.sharedMaterials.Select(m=>material!=null?material:Compatible(m)).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            return g;
        }
        static void FixInstanceLods(GameObject g)
        {
            // Imported LOD thresholds are 0,0,0.01. Keep the source untouched and use LOD0
            // in this review map so close views cannot cull the whole structural module.
            foreach(var lod in g.GetComponentsInChildren<LODGroup>())
            {
                var levels=lod.GetLODs();if(levels.Length==0)continue;
                var first=levels[0].renderers;
                foreach(var level in levels)foreach(var r in level.renderers)if(r!=null){r.enabled=first.Contains(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
                // A disabled LODGroup can retain per-camera culling. Remove the instance
                // component entirely, retaining the prefab source and its original LODs.
                Undo.DestroyObjectImmediate(lod);
                foreach(var r in first)if(r!=null){r.enabled=false;r.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            }
        }
        public static string PersistVisibleInstances()
        {
            InitReference();int count=0;
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
            {
                FixInstanceLods(t.gameObject);PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                foreach(var renderer in t.GetComponentsInChildren<Renderer>())PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return count+" prefab overrides saved";
        }
        public static string RepairImportedInstances()
        {
            InitReference();int count=0;
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
            {
                var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;
                var target=rs[0].bounds;foreach(var r in rs)target.Encapsulate(r.bounds);
                FixInstanceLods(t.gameObject);var active=rs.Where(r=>r.enabled).ToArray();if(active.Length==0)continue;
                var actual=active[0].bounds;foreach(var r in active)actual.Encapsulate(r.bounds);
                Vector3 factors=new Vector3(target.size.x/Mathf.Max(.001f,actual.size.x),target.size.y/Mathf.Max(.001f,actual.size.y),target.size.z/Mathf.Max(.001f,actual.size.z));
                if(Mathf.Abs(t.eulerAngles.y%90)<.01f){var local=Quaternion.Inverse(t.rotation)*factors;t.localScale=Vector3.Scale(t.localScale,new Vector3(Mathf.Abs(local.x),Mathf.Abs(local.y),Mathf.Abs(local.z)));}
                actual=active[0].bounds;foreach(var r in active)actual.Encapsulate(r.bounds);
                t.position+=new Vector3(target.center.x-actual.center.x,target.min.y-actual.min.y,target.center.z-actual.center.z);
                foreach(var r in rs)r.sharedMaterials=r.sharedMaterials.Select(m=>m==Existing("V4_Wall_DamagedConcrete")?concrete:m==Existing("V4_Floor_WetConcrete")?wetFloor:m).ToArray();
                count++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return count+" instance LODs repaired";
        }
        static Material Compatible(Material source)
        {
            if(source==null)return grey;
            if(source.shader.name.StartsWith("Universal Render Pipeline")||source.shader.name.StartsWith("Synty/"))return source;
            string path=Materials+"ref-"+source.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ref-"+source.name};
            foreach(var key in new[]{"_BaseMap","_Base_Map","_BaseColorMap"})if(source.HasProperty(key)&&source.GetTexture(key)!=null){m.SetTexture("_BaseMap",source.GetTexture(key));m.SetTextureScale("_BaseMap",source.GetTextureScale(key));break;}
            foreach(var key in new[]{"_Normal","_Normal_Map","_NormalMap"})if(source.HasProperty(key)&&source.GetTexture(key)!=null){m.SetTexture("_BumpMap",source.GetTexture(key));m.EnableKeyword("_NORMALMAP");break;}
            m.SetFloat("_Smoothness",.35f);
            if(source.name.Contains("Grass")){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.4f);m.SetFloat("_Cull",0);m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;m.SetOverrideTag("RenderType","TransparentCutout");}
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static void FloorRect(Transform p,float x1,float x2,float z1,float z2,float y)
        {for(float x=x1;x<x2;x+=4)for(float z=z1;z<z2;z+=4){float w=Mathf.Min(4,x2-x),d=Mathf.Min(4,z2-z);var g=Real(p,Factory+"SM_ConcreteFloor.prefab",new Vector3(x+w/2,y-.3f,z+d/2),new Vector3(w,.3f,d),wetFloor);if(g.GetComponentsInChildren<Collider>().Length==0)g.AddComponent<BoxCollider>();}}
        static void Platform(Transform p,float x1,float x2,float z1,float z2,float y)
        {
            FloorRect(p,x1,x2,z1,z2,y);
            Facade(p,x1,z1,x2,z1,-Depth,y);Facade(p,x2,z1,x2,z2,-Depth,y);Facade(p,x2,z2,x1,z2,-Depth,y);Facade(p,x1,z2,x1,z1,-Depth,y);
        }
        static void Facade(Transform p,float x1,float z1,float x2,float z2,float bottom,float top)
        {
            float length=Vector2.Distance(new Vector2(x1,z1),new Vector2(x2,z2));bool alongX=Mathf.Abs(x2-x1)>.1f;
            for(float d=0;d<length;d+=6)for(float y=bottom;y<top;y+=8)
            {
                float w=Mathf.Min(6,length-d),h=Mathf.Min(8,top-y),t=(d+w/2)/length;
                Vector3 at=new Vector3(Mathf.Lerp(x1,x2,t),y,Mathf.Lerp(z1,z2,t));
                Real(p,Factory+"SM_ConcreteFloor.prefab",at,alongX?new Vector3(w,h,.6f):new Vector3(.6f,h,w),concrete,alongX?Quaternion.Euler(90,0,0):Quaternion.Euler(0,0,90));
            }
        }
        static void ShellX(Transform p,float z,float x1,float x2,float h)=>Facade(p,x1,z,x2,z,0,h);
        static void ShellZ(Transform p,float x,float z1,float z2,float h)=>Facade(p,x,z1,x,z2,0,h);
        static void Ceiling(Transform p,float x1,float x2,float z1,float z2,float y)
        {for(float x=x1;x<x2;x+=8)for(float z=z1;z<z2;z+=8){float w=Mathf.Min(8,x2-x),d=Mathf.Min(8,z2-z);Real(p,Factory+"SM_ConcreteFloor.prefab",new Vector3(x+w/2,y,z+d/2),new Vector3(w,.5f,d),concrete);}}
        static void Abyss(Transform p,float x1,float x2,float z1,float z2)
        {Box(p,"깊은 공동 암부",new Vector3((x1+x2)/2,-Depth-2,(z1+z2)/2),new Vector3(x2-x1,1,z2-z1),dark);}
        static void RailEdge(Transform p,float x1,float z1,float x2,float z2,float y)
        {float length=Vector2.Distance(new Vector2(x1,z1),new Vector2(x2,z2));bool alongX=Mathf.Abs(x2-x1)>.1f;for(float d=0;d<length;d+=3){float w=Mathf.Min(3,length-d),t=(d+w/2)/length;Real(p,Industrial+"SM_Scaffolding_Railing_200m.prefab",new Vector3(Mathf.Lerp(x1,x2,t),y,Mathf.Lerp(z1,z2,t)),alongX?new Vector3(w,1.2f,.12f):new Vector3(.12f,1.2f,w),iron,alongX?Quaternion.Euler(0,90,0):Quaternion.identity);}}
        static void ServiceBay(Transform p,float x,float z,float height,int alongZ)
        {
            for(float y=-Depth;y<height;y+=7)Real(p,Factory+"SM_ConcreteColumn.prefab",new Vector3(x,y,z),new Vector3(2.1f,Mathf.Min(7,height-y),2.1f),concrete);
            for(int i=0;i<3;i++)
            {var at=new Vector3(x+(alongZ==1?1.5f:0),-22,z+(alongZ==0?1.5f:0));at+=new Vector3(alongZ==0?i*.55f:0,0,alongZ==1?i*.55f:0);Real(p,Industrial+"SM_Pipe_Medium_01.prefab",at,new Vector3(.4f,42,.4f),null,Quaternion.Euler(0,0,90));}
            Lamp(p,new Vector3(x+(alongZ==1?1.5f:0),3.5f,z+(alongZ==0?1.5f:0)),alongZ==0);
            Prop(p,"SM_Prop_BarrelStack_01",new Vector3(x+(alongZ==1?2:0),0,z+(alongZ==0?2:0)),new Vector3(2,1.2f,1.3f));
            for(int level=-24;level<=14;level+=19)
            {
                float cx=x+(alongZ==1?2:0),cz=z+(alongZ==0?2:0);
                Real(p,Industrial+"SM_Scaffolding_Flooring.prefab",new Vector3(cx,level,cz),new Vector3(3,.2f,3),iron);
                RailEdge(p,cx-1.5f,cz+1.5f,cx+1.5f,cz+1.5f,level);
                if(level<0){var l=new GameObject("Deep service light",typeof(Light));l.transform.SetParent(p,false);l.transform.position=new Vector3(cx,level+2,cz);l.GetComponent<Light>().type=LightType.Point;l.GetComponent<Light>().range=10;l.GetComponent<Light>().intensity=2;l.GetComponent<Light>().color=new Color(1,.43f,.15f);}
            }
        }
        static void IndustrialBeam(Transform p,Vector3 c,Vector3 size)=>Real(p,Industrial+"SM_Crane_Beam.prefab",c,size);
        static void FoundationPipe(Transform p,Vector3 c,float length)=>Real(p,Industrial+"SM_Pipe_Big_01.prefab",c,new Vector3(2.4f,length,2.4f),null,Quaternion.Euler(0,0,90));
        static void Portal(Transform p,float x,float z,float width,float opening,float yaw)
        {
            bool alongX=yaw==0;var r=Quaternion.Euler(0,yaw,0);
            foreach(float side in new[]{-1f,1f})Real(p,Factory+"SM_ConcreteColumn.prefab",new Vector3(x,0,z)+r*new Vector3(side*width/2,0,0),new Vector3(1.4f,opening+3,1.4f),concrete);
            IndustrialBeam(p,new Vector3(x,opening,z),alongX?new Vector3(width+1,2,1.4f):new Vector3(1.4f,2,width+1));
        }
        static void Cable(Transform p,Vector3 a,Vector3 b,float diameter,string name)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(p,true);g.transform.position=(a+b)/2;g.transform.up=(b-a).normalized;g.transform.localScale=new Vector3(diameter,Vector3.Distance(a,b)/2,diameter);g.GetComponent<Renderer>().sharedMaterial=iron;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
    }
}
