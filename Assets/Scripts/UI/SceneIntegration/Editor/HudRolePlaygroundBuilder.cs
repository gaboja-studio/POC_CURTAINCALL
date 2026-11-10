using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class HudRolePlaygroundBuilder
    {
        public const string Path="Assets/Scenes/Tests/UI/HudRolePlayground.unity";
        static TMP_FontAsset font;
        static TMP_DefaultControls.Resources skin;
        static RectTransform Node(string name,Transform parent)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform; r.SetParent(parent,false); return r;
        }
        static void Height(Component c,float h){var e=c.gameObject.AddComponent<LayoutElement>();e.minHeight=e.preferredHeight=h;}
        static TMP_Text Label(Transform parent,string name,string text,int size)
        {
            var t=Node(name,parent).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=text;t.color=Color.white;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;return t;
        }
        static void Vertical(RectTransform r,float spacing)
        {var g=r.gameObject.AddComponent<VerticalLayoutGroup>();g.spacing=spacing;g.childControlWidth=g.childControlHeight=g.childForceExpandWidth=true;g.childForceExpandHeight=false;}
        static RectTransform Line(Transform parent,string label="")
        {
            var r=Node(label==""?"Actions":label,parent);Height(r,30);
            var g=r.gameObject.AddComponent<HorizontalLayoutGroup>();g.spacing=5;g.childAlignment=TextAnchor.MiddleLeft;g.childControlHeight=g.childControlWidth=g.childForceExpandHeight=true;g.childForceExpandWidth=false;
            if(label!=""){var l=Label(r,"Label",label,15);var el=l.gameObject.AddComponent<LayoutElement>();el.minWidth=el.preferredWidth=87;}
            return r;
        }
        static T Control<T>(GameObject go,Transform parent,string name) where T:Component
        {
            go.name=name;go.transform.SetParent(parent,false);
            var e=go.AddComponent<LayoutElement>();e.flexibleWidth=1;e.preferredWidth=10;
            foreach(var t in go.GetComponentsInChildren<TMP_Text>(true)){t.font=font;t.fontSize=16;t.color=new Color(.08f,.12f,.15f);t.richText=false;}
            return go.GetComponent<T>();
        }
        static Button Btn(Transform row,string caption)
        {
            var b=Control<Button>(TMP_DefaultControls.CreateButton(skin),row,caption);
            b.GetComponentInChildren<TMP_Text>().text=caption;return b;
        }
        static TMP_InputField Input(Transform parent,string name,string value,string hint="")
        {
            var t=Control<TMP_InputField>(TMP_DefaultControls.CreateInputField(skin),Line(parent,name),name);
            t.fontAsset=font;t.text=value;
            if(t.placeholder is TMP_Text ph) ph.text=hint;
            return t;
        }
        static TMP_Dropdown Dropdown(Transform parent,string name,params string[] options)
        {
            var d=Control<TMP_Dropdown>(TMP_DefaultControls.CreateDropdown(skin),Line(parent,name),name);
            d.ClearOptions();d.AddOptions(new List<string>(options));d.RefreshShownValue();
            // The standard template only shows about four entries without scrolling.
            var rect=d.template; rect.sizeDelta=new Vector2(rect.sizeDelta.x,Mathf.Min(220,options.Length*25+12));return d;
        }
        static GameObject Primitive(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Transform parent=null)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;return go;}
        [MenuItem("Curtain Call/UI/Open role HUD playground")]
        public static void Open()
        {
            if(EditorApplication.isPlaying) return;
            if(!File.Exists(Path)) Build();
            UpgradeEventControls();
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty){EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(Path));return;}
            EditorSceneManager.OpenScene(Path,OpenSceneMode.Single);
        }
        [MenuItem("Curtain Call/UI/Upgrade HUD artwork and event controls")]
        public static void UpgradePresentation()
        {
            HudRolePrefabBuilder.Build();
            if(!File.Exists(Path))Build();
            UpgradeEventControls();
            Debug.Log("HUD event controls and artwork ready");
        }
        static void UpgradeEventControls()
        {
            var scene=SceneManager.GetSceneByPath(Path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(Path,OpenSceneMode.Additive);
            try
            {
                foreach(var root in scene.GetRootGameObjects())
                {
                    var demo=root.GetComponentInChildren<HudRolePlayground>(true);
                    if(!demo || demo.eventKind)continue;
                    var row=UnityEngine.Object.Instantiate(demo.statusPosition.transform.parent.gameObject,demo.form.transform,false);
                    row.name="사건 종류";row.transform.SetSiblingIndex(demo.role.transform.parent.GetSiblingIndex()+1);
                    row.transform.Find("Label").GetComponent<TMP_Text>().text="사건 종류";
                    demo.eventKind=row.GetComponentInChildren<TMP_Dropdown>(true);
                    demo.eventKind.ClearOptions();demo.eventKind.AddOptions(new List<string>{"알림","경고","보상"});demo.eventKind.RefreshShownValue();
                    if(demo.role.options.Count==8)demo.role.AddOptions(new List<string>{"⑨ 대기실 구역명"});
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
            }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        public static void Build()
        {
            if(File.Exists(Path))return;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); AssetDatabase.Refresh();
            var prev=SceneManager.GetActiveScene();
            bool emptySession=SceneManager.sceneCount==1 && string.IsNullOrEmpty(prev.path) && !prev.isDirty;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptySession?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                font=Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
                Sprite S(string name)=>AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/"+name+".psd");
                skin=new TMP_DefaultControls.Resources{standard=S("UISprite"),background=S("Background"),inputField=S("InputFieldBackground"),knob=S("Knob"),checkmark=S("Checkmark"),dropdown=S("DropdownArrow"),mask=S("UIMask")};
                var root=new GameObject("HUD Role Playground");var demo=root.AddComponent<HudRolePlayground>();
                demo.panelFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-Medium.ttf");
                var cam=new GameObject("Preview Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=new Vector3(0,7.5f,-11);cam.transform.LookAt(new Vector3(0,0,1));cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.09f,.15f,.19f);demo.sceneCamera=cam;
                var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(52,-32,0);
                var floor=Primitive("Floor",PrimitiveType.Cube,new Vector3(0,-.13f,0),new Vector3(18,.25f,13));
                demo.actor=new GameObject("Player · feet").transform;
                Primitive("Player capsule",PrimitiveType.Capsule,new Vector3(0,.8f,0),new Vector3(.64f,.8f,.64f),demo.actor);
                demo.targets=new[]{Primitive("Target A",PrimitiveType.Cube,new Vector3(1.4f,1,1),Vector3.one).transform,Primitive("Target B",PrimitiveType.Sphere,new Vector3(4,1,2.6f),Vector3.one).transform};
                demo.wall=Primitive("Occluder",PrimitiveType.Cube,new Vector3(0,1,0),new Vector3(1.8f,2,.14f)).transform;demo.wall.gameObject.SetActive(false);
                demo.ring=new GameObject("Interaction radius").AddComponent<LineRenderer>();demo.ring.useWorldSpace=true;demo.ring.widthMultiplier=.045f;
                demo.ring.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                demo.ring.startColor=demo.ring.endColor=new Color(1,.72f,.22f);demo.ring.gameObject.SetActive(false);
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                var canvas=new GameObject("Playground controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=600;
                var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
                var dock=Node("Console",canvas.transform);dock.anchorMin=dock.anchorMax=dock.pivot=Vector2.zero;dock.anchoredPosition=new Vector2(16,16);dock.sizeDelta=new Vector2(420,0);
                dock.gameObject.AddComponent<Image>().color=new Color(.055f,.12f,.15f,.97f);Vertical(dock,5);dock.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(12,12,10,10);
                dock.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                var header=Line(dock);var title=Label(header,"Title","HUD  /  역할 테스트",19);title.gameObject.AddComponent<LayoutElement>().preferredWidth=250;
                demo.toggle=Btn(header,"패널 접기");
                var form=Node("Form",dock);Vertical(form,4);demo.form=form.gameObject;
                demo.role=Dropdown(form,"역할","① 팀 목표","② 시스템 사건","③ 학습 가이드","④ 상호작용","⑤ 피드백","⑥ 상태","⑦ 투표","⑧ 결과");
                demo.heading=Input(form,"제목","");demo.content=Input(form,"본문","");demo.condition=Input(form,"조건 / 힌트","");
                demo.seconds=Input(form,"노출 시간","","초 · 빈칸: 기본값 / 0: 유지");
                demo.key=Input(form,"표시 키","F");demo.target=Dropdown(form,"대상","A · 주황 상자","B · 보라 구체");
                demo.statusPosition=Dropdown(form,"상태 위치","기본 상태","공연 현재 구간");
                demo.firstChoice=Input(form,"선택지 1","동의");demo.secondChoice=Input(form,"선택지 2","취소");
                var actions=Line(form);demo.output=Btn(actions,"출력");demo.clear=Btn(actions,"모두 지우기");
                var sim=Node("World controls",form);Vertical(sim,4);demo.simulation=sim.gameObject;
                Height(Label(sim,"Legend","청록: 플레이어 · 바닥 원: 도달 범위 · WASD: 이동",15),23);
                var moves=Line(sim);demo.inside=Btn(moves,"범위 안");demo.outside=Btn(moves,"범위 밖");demo.block=Btn(moves,"가림벽 전환");
                var shifts=Line(sim);demo.targetLeft=Btn(shifts,"대상 ←");demo.targetRight=Btn(shifts,"대상 →");demo.interact=Btn(shifts,"행동 실행");
                demo.diagnostic=Label(sim,"Reach","",16);Height(demo.diagnostic,24);
                demo.message=Label(form,"Message","",14);Height(demo.message,36);
                if(!EditorSceneManager.SaveScene(scene,Path))throw new IOException(Path);
            }
            finally{if(!emptySession){SceneManager.SetActiveScene(prev);EditorSceneManager.CloseScene(scene,true);}}
        }
    }
}
