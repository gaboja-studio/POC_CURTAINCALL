using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    // Edit-only spatial proposal. No AI, damage, gameplay colliders or build-scene registration.
    public static class EscapePursuerLayoutReview
    {
        public const string SourceScene = "Assets/Scenes/Levels/escape_route.unity";
        public const string ReviewScene = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapePursuerLayout.unity";
        const string Folder = "Assets/Scenes/Tests/EscapeRoute/Pursuer";
        const string PlaytestScene = EscapeMppmReviewConfig.PursuerScene;
        internal const string PrototypePrefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/Pursuer5mGreybox.prefab";
        const string ReportFolder = "Tasks/Active/Task-20261103-100-exit-route-pursuer/Review";
        static Transform frame;
        static Material grey, human, proposed, cut, route;
        const string ShowcaseMenu = "Tools/CurtainCall/탈출로/추격자 모드 실행";
        const string PlayerMenu = "Tools/CurtainCall/탈출로/추격자 테스트 인원/";
        const string PreviousStartScene = "CurtainCall.PursuerShowcase.PreviousStartScene";
        static readonly Vector3 ConveyorBaseline = new(51, -17, -32);
        static readonly Vector3 CoopBaseline = new(118.5f, 0, 25.5f);

        [MenuItem(PlayerMenu + "1인")] static void Solo() => EscapeMppmReviewConfig.PursuerPlayers = 1;
        [MenuItem(PlayerMenu + "2인")] static void Pair() => EscapeMppmReviewConfig.PursuerPlayers = 2;
        [MenuItem(PlayerMenu + "3인")] static void Trio() => EscapeMppmReviewConfig.PursuerPlayers = 3;
        [MenuItem(PlayerMenu + "4인")] static void Four() => EscapeMppmReviewConfig.PursuerPlayers = 4;

        [MenuItem(PlayerMenu + "1인", true)]
        [MenuItem(PlayerMenu + "2인", true)]
        [MenuItem(PlayerMenu + "3인", true)]
        [MenuItem(PlayerMenu + "4인", true)]
        static bool ValidatePlayerCount()
        {
            for (int i = 1; i <= 4; i++) Menu.SetChecked(PlayerMenu + i + "인", EscapeMppmReviewConfig.PursuerPlayers == i);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(ShowcaseMenu, true)]
        static bool CanRunShowcase() => EscapeMppmReviewConfig.PursuerPlayers == 1 &&
            (!EditorApplication.isPlayingOrWillChangePlaymode ||
                (EditorApplication.isPlaying && SceneManager.GetActiveScene().path == PlaytestScene));

        [MenuItem(ShowcaseMenu)]
        static void RunShowcase()
        {
            if (EditorApplication.isPlaying)
            {
                UnityEngine.Object.FindFirstObjectByType<UnderstagePursuerShowcase>()?.RequestOpen();
                return;
            }
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlaytestScene);
            if (!scene) throw new FileNotFoundException("추격자 모드 씬을 찾을 수 없습니다.");
            var previous = EditorSceneManager.playModeStartScene;
            SessionState.SetString(PreviousStartScene, previous ? AssetDatabase.GetAssetPath(previous) : "");
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void RegisterShowcaseRestore() => EditorApplication.playModeStateChanged += RestoreStartScene;

        static void RestoreStartScene(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode && state != PlayModeStateChange.EnteredEditMode) return;
            string path = SessionState.GetString(PreviousStartScene, "-");
            if (path == "-") return;
            EditorSceneManager.playModeStartScene = path.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.EraseString(PreviousStartScene);
            if (state == PlayModeStateChange.EnteredPlayMode)
                UnityEngine.Object.FindFirstObjectByType<UnderstagePursuerShowcase>()?.RequestOpen();
        }

        [MenuItem("Tools/CurtainCall/탈출로/추격자 첫 등장 테스트 씬 열기")]
        public static void OpenPlaytest()
        {
            RequireStoppedAndSaved();
            EditorSceneManager.OpenScene(PlaytestScene, OpenSceneMode.Single);
        }

        [MenuItem("Tools/CurtainCall/탈출로/추격자 배치 검토 열기")]
        public static void Open()
        {
            RequireStoppedAndSaved();
            OpenSource();
            if (!File.Exists(ReviewScene)) { Create(); return; }
            var review = SceneManager.GetSceneByPath(ReviewScene);
            if (!review.isLoaded) review = EditorSceneManager.OpenScene(ReviewScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(SourceScene));
            Selection.activeGameObject = review.GetRootGameObjects().FirstOrDefault();
        }

        // Rebind module translations to the current source landmarks. Geometry must be surveyed again.
        [MenuItem("Tools/CurtainCall/탈출로/추격자 검토 앵커 위치 갱신")]
        public static void RefreshAnchors()
        {
            RequireStoppedAndSaved(); OpenSource();
            var review = SceneManager.GetSceneByPath(ReviewScene);
            if (!review.isLoaded) review = EditorSceneManager.OpenScene(ReviewScene, OpenSceneMode.Additive);
            var root = review.GetRootGameObjects().Single(x => x.name == "Task098_PursuerLayout_Proposal").transform;
            foreach (Transform module in root)
            {
                if (module.name.StartsWith("00_"))
                {
                    Undo.RecordObject(module, "Rebind pursuer scale comparison");
                    module.SetPositionAndRotation(frame.TransformPoint(new Vector3(18, 0, 10)), frame.rotation);
                    continue;
                }
                if (!module.name.Contains("__")) continue;
                Undo.RecordObject(module, "Rebind pursuer layout landmark");
                module.SetPositionAndRotation(Anchor(module.name), frame.rotation);
            }
            EditorSceneManager.MarkSceneDirty(review);
            EditorSceneManager.SaveScene(review);
            Debug.LogWarning("Pursuer proposal anchors moved. Recheck floors, walls, swing envelopes and sightlines before using this as a playable route.");
        }

        public static object Create()
        {
            RequireStoppedAndSaved(); OpenSource();
            if (File.Exists(ReviewScene)) throw new InvalidOperationException("Review already exists; open it instead of overwriting manual edits.");
            EnsureFolder(Folder); Directory.CreateDirectory(ReportFolder);
            string sourceHash = Hash(SourceScene);
            grey = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePrefab).GetComponentInChildren<MeshRenderer>().sharedMaterial;
            human = MakeMaterial("human-reference", new Color(.12f, .65f, .88f));
            proposed = MakeMaterial("proposed-floor", new Color(.28f, .34f, .39f));
            cut = MakeMaterial("required-opening", new Color(.94f, .25f, .16f));
            route = MakeMaterial("proposed-route", new Color(.95f, .68f, .14f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Task098_PursuerLayout_Proposal"); root.tag = "EditorOnly";
            SceneManager.MoveGameObjectToScene(root, scene);

            var scale = Module(root.transform, "00_Scale_1p73_3p2_5p0_6p0", new Vector3(18, 0, 10));
            var bounds = new List<object>();
            foreach (var d in new[] { (x:-8f,h:1.73f,label:"Human 1.73m"), (x:-4f,h:3.2f,label:"3.2m previous"), (x:1f,h:5f,label:"5m prototype"), (x:7f,h:6f,label:"6m comparison") })
            {
                var person = Proxy(scale, new Vector3(d.x, 0, 0), d.h, d.h < 2 ? human : grey, d.label);
                if (d.h < 2) person.localScale = new Vector3(.65f, 1, 1);
                var b = BoundsOf(person); bounds.Add(new { d.label, requestedHeight=d.h, measuredHeight=b.size.y, measuredWidth=b.size.x, depth=b.size.z });
            }

            var first = Module(root.transform, "01_FirstBreach__Understage", Vector3.zero);
            Label(first, "P1 / Understage breach in A", Vector3.up * 6.5f, .13f);

            var conveyor=Module(root.transform,"02_OuterLane__Conveyor",ConveyorBaseline);
            Box(conveyor,"Proposed north deck",new Vector3(26,-18.65f,-27.75f)-ConveyorBaseline,new Vector3(4.5f,.3f,25.5f),proposed);
            Box(conveyor,"Proposed south landing",new Vector3(26,-19.4f,-54.5f)-ConveyorBaseline,new Vector3(4.5f,.3f,21),proposed);
            Path(conveyor,"P2 outer walk",Offset(ConveyorBaseline,new Vector3(26,-18.5f,-15),new Vector3(26,-18.5f,-40.5f)),false);
            Path(conveyor,"P2 3.5m gap, 0.75m lower landing",Offset(ConveyorBaseline,new Vector3(26,-18.5f,-40.5f),new Vector3(26,-17.5f,-42.25f),new Vector3(26,-19.25f,-44)),true);
            Path(conveyor,"P2 landing walk",Offset(ConveyorBaseline,new Vector3(26,-19.25f,-44),new Vector3(26,-19.25f,-65)),false);
            Proxy(conveyor,new Vector3(26,-18.5f,-28)-ConveyorBaseline,5f,grey,"P2 outer service lane / 5m prototype");
            Label(conveyor,"New independent support + enclosure required",new Vector3(26,-14,-38)-ConveyorBaseline,.17f);

            var coop=Module(root.transform,"03_CoopPressure__UpperLanding",CoopBaseline);
            Box(coop,"Proposed north service deck",new Vector3(99,-.15f,41)-CoopBaseline,new Vector3(30,.3f,4.5f),proposed);
            Path(coop,"P3 north approach",Offset(CoopBaseline,new Vector3(84,0,41),new Vector3(98,0,41),new Vector3(112.5f,0,41)),false);
            Path(coop,"P3 exit connector - new floor required",Offset(CoopBaseline,new Vector3(112.5f,0,41),new Vector3(112.5f,0,30),new Vector3(118,0,27)),true);
            Opening(coop,new Vector3(98,0,41)-CoopBaseline,Vector3.right,"P3 5m-thick pier passage + breakable gate");
            Proxy(coop,new Vector3(106,0,41)-CoopBaseline,5f,grey,"P3 gate pressure / 5m prototype");

            var early = Module(root.transform,"04_FinalEntry__BridgeOne",new Vector3(179,0,78));
            Proxy(early,new Vector3(153,0,78)-new Vector3(179,0,78),5f,grey,"P4 warning before crew bridge / 5m");
            Path(early,"P4 warning approach",Offset(new Vector3(179,0,78),new Vector3(153,0,78),new Vector3(165,0,78)),false);
            var bypass = Module(root.transform,"05_BypassReserve__BridgeTwo",new Vector3(292,0,78));
            Path(bypass,"RESERVED underfloor connector - no floor authored",Offset(new Vector3(292,0,78),new Vector3(153,-10,84),new Vector3(335,-10,84)),true);
            foreach(float x in new[]{179f,292f,315f})
            {
                Box(bypass,"Proposed tunnel cross-section bay",new Vector3(x,-10.15f,84)-new Vector3(292,0,78),new Vector3(6,.3f,4.5f),proposed);
                Label(bypass,"RESERVE: 4.5m width / 6m headroom",new Vector3(x,-3.5f,84)-new Vector3(292,0,78),.13f);
            }
            var finale=Module(root.transform,"06_LastChase__BridgeThree",new Vector3(315,0,78));
            Path(finale,"P5 reentry shaft RESERVED",Offset(new Vector3(315,0,78),new Vector3(335,-10,84),new Vector3(335,0,84),new Vector3(336,0,78)),true);
            Path(finale,"P5 final direct chase",Offset(new Vector3(315,0,78),new Vector3(336,0,78),new Vector3(394,0,78)),false);
            Proxy(finale,new Vector3(336,0,78)-new Vector3(315,0,78),5f,grey,"P5 final chase / 5m prototype");
            Opening(finale,new Vector3(335,0,84)-new Vector3(315,0,78),Vector3.back,"P5 service hatch; shaft motion unresolved");
            Label(root.transform,"PROPOSAL / NOT PLAYABLE",frame.TransformPoint(new Vector3(18,7,10)),.08f);
            foreach (var material in new[] { grey, human, proposed, cut, route }) AssetDatabase.SaveAssetIfDirty(material);
            EditorSceneManager.SaveScene(scene,ReviewScene);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(SourceScene));
            var result=new{sourceScene=SourceScene,sourceHash,reviewScene=ReviewScene,coordinateSpace="EscapeCampaignFrame local metres; position is feet; module origins bind to current landmarks",models=bounds,modules=root.transform.Cast<Transform>().Select(t=>new{t.name,world=Vector(t.position),children=t.childCount}).ToArray(),colliders=root.GetComponentsInChildren<Collider>(true).Length,missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),sourceUnchanged=Hash(SourceScene)==sourceHash,sourceDirty=SceneManager.GetSceneByPath(SourceScene).isDirty};
            File.WriteAllText(ReportFolder+"/layout-validation.json",JsonConvert.SerializeObject(result,Formatting.Indented));
            Selection.activeGameObject=first.gameObject;
            return result;
        }

        static void RequireStoppedAndSaved()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before layout review.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save the current scene first: "+SceneManager.GetSceneAt(i).path);
        }
        static void OpenSource()
        {
            var scene=SceneManager.GetSceneByPath(SourceScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(SourceScene,OpenSceneMode.Single);
            frame=scene.GetRootGameObjects().Single(x=>x.name=="EscapeCampaignFrame").transform;
        }
        static Vector3 Anchor(string name)
        {
            if (name.StartsWith("01_"))
            {
                // The four Understage walls share a name. This is the corridor's right-hand wall in A.
                const string wallId = "GlobalObjectId_V1-2-c9bc27ef66a57414e8bd36141ad746b2-1914776641-0";
                GlobalObjectId.TryParse(wallId, out var id);
                var wall = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Transform;
                if (!wall) throw new InvalidOperationException("Understage 복도 오른쪽 벽이 교체되었습니다. P1 검토 앵커를 다시 지정하세요.");
                return wall.TransformPoint(new Vector3(.175f, -.5f, 0f));
            }
            if(name.StartsWith("02_"))return frame.Find("EscapeBackground/34_AFusedRoute/04B_ConveyorCatch/LoopTrack_0").position;
            if(name.StartsWith("03_"))
            {var b=frame.Find("EscapeBackground/32_A_CooperativeRopes/상부 도착 정비대").GetComponent<Collider>().bounds;return new Vector3(b.center.x,b.max.y,b.center.z);}
            string section=name.StartsWith("04_")?"07A_ShoulderDrawbridge/목마 반출교":name.StartsWith("05_")?"07B_CrewSwingHoist/두 사람 하중 권상교":"07C_BuddyReturnBridge/동료 유지 반출교";
            var span=frame.Find("EscapeBackground/38_ACooperativeWorks/"+section);
            var so=new SerializedObject(span.GetComponent<EscapeCooperativeSpan>());
            var a=so.FindProperty("leftLeaf").objectReferenceValue as Transform;
            var bLeaf=so.FindProperty("rightLeaf").objectReferenceValue as Transform;
            return(a.position+bLeaf.position)*.5f;
        }
        static Transform Module(Transform root,string name,Vector3 baseline)
        {
            var g=new GameObject(name);g.transform.SetParent(root,false);
            g.transform.SetPositionAndRotation(name.StartsWith("00_")?frame.TransformPoint(baseline):Anchor(name),frame.rotation);
            return g.transform;
        }
        static Vector3[] Offset(Vector3 origin,params Vector3[] points)=>points.Select(p=>p-origin).ToArray();
        static GameObject Box(Transform p,string name,Vector3 center,Vector3 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=center;g.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;return g;
        }
        static Transform Proxy(Transform p,Vector3 feet,float h,Material material,string label)
        {
            if (h == 5f)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePrefab);
                if (!prefab) throw new FileNotFoundException("Missing five-metre pursuer greybox", PrototypePrefab);
                var body = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, p)).transform;
                body.name = label;
                body.localPosition = feet;
                Label(body, label, Vector3.up * (h + .4f), .16f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                PrefabUtility.RecordPrefabInstancePropertyModifications(body.gameObject);
                return body;
            }
            var r=new GameObject(label).transform;r.SetParent(p,false);r.localPosition=feet;
            Box(r,"Torso",Vector3.up*h*.635f,new Vector3(h*.46875f,h*.31f,h*.22f),material);
            Box(r,"Pelvis",Vector3.up*h*.44f,new Vector3(h*.29f,h*.15f,h*.2f),material);
            Sphere(r,"Head",Vector3.up*h*.915f,h*.17f,material);
            Box(r,"Neck",Vector3.up*h*.815f,new Vector3(h*.12f,h*.13f,h*.12f),material);
            foreach(float side in new[]{-1f,1f})
            {
                Limb(r,"Upper arm",new Vector3(side*h*.24f,h*.74f,0),new Vector3(side*h*.24f,h*.52f,0),h*.04f,material);
                Limb(r,"Forearm",new Vector3(side*h*.24f,h*.52f,0),new Vector3(side*h*.23f,h*.32f,.05f*h),h*.035f,material);
                Limb(r,"Thigh",new Vector3(side*h*.095f,h*.41f,0),new Vector3(side*h*.105f,h*.23f,0),h*.055f,material);
                Limb(r,"Shin",new Vector3(side*h*.105f,h*.23f,0),new Vector3(side*h*.11f,h*.07f,0),h*.045f,material);
                Box(r,"Foot",new Vector3(side*h*.11f,h*.025f,h*.035f),new Vector3(h*.11f,h*.05f,h*.18f),material);
            }
            Label(r,label,new Vector3(0,h+.4f,0),.16f);return r;
        }
        static void Sphere(Transform p,string name,Vector3 center,float diameter,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=center;g.transform.localScale=Vector3.one*diameter;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;
        }
        static void Limb(Transform p,string name,Vector3 a,Vector3 b,float radius,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Capsule);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=(a+b)*.5f;g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);g.transform.localScale=new Vector3(radius*2,Vector3.Distance(a,b)*.5f+radius,radius*2);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;
        }
        static void Opening(Transform p,Vector3 feet,Vector3 normal,string title)
        {
            var r=new GameObject(title).transform;r.SetParent(p,false);r.localPosition=feet;r.localRotation=Quaternion.LookRotation(normal);
            foreach(float x in new[]{-2.25f,2.25f})Box(r,"Opening jamb",new Vector3(x,3,0),new Vector3(.08f,6,.08f),cut);
            Box(r,"Opening head",new Vector3(0,6,0),new Vector3(4.58f,.08f,.08f),cut);
            Label(r,title,new Vector3(0,6.5f,0),.13f);
        }
        static void Path(Transform p,string name,Vector3[] points,bool dashed)
        {
            var g=new GameObject(name);g.transform.SetParent(p,false);
            for(int i=1;i<points.Length;i++)
            {
                var a=points[i-1]+Vector3.up*.08f;var b=points[i]+Vector3.up*.08f;float length=Vector3.Distance(a,b);int count=dashed?Mathf.CeilToInt(length/2):1;
                for(int n=0;n<count;n++)
                {
                    var start=Vector3.Lerp(a,b,(float)n/count);var end=Vector3.Lerp(a,b,Mathf.Min(1,(n+(dashed?.55f:1))/count));
                    var line=Box(g.transform,"Route",(start+end)*.5f,new Vector3(.09f,.09f,Vector3.Distance(start,end)),route);line.transform.localRotation=Quaternion.LookRotation(end-start);
                }
            }
        }
        static void Label(Transform p,string text,Vector3 position,float size)
        {
            var g=new GameObject("Label");g.transform.SetParent(p,false);g.transform.localPosition=position;
            var t=g.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size*.25f;t.anchor=TextAnchor.MiddleCenter;t.color=Color.white;
            g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
        }
        static Bounds BoundsOf(Transform root)
        {
            var rs=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TextMesh>()==null).ToArray();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static Material MakeMaterial(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old!=null)return old;
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,path);return m;
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));
        }
        static string Hash(string path)=>BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        static object Vector(Vector3 p)=>new{x=p.x,y=p.y,z=p.z};
    }
}
