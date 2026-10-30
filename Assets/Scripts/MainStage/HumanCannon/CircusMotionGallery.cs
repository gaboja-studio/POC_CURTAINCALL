using System;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.HumanCannon
{
    /// <summary>Independent inspection of the actual controller states on all four character avatars.</summary>
    public sealed class CircusMotionGallery : MonoBehaviour
    {
        public static readonly string[] Motions={"CannonLoaded","CannonRise","FlightApex","PanicFall","TrampolineRise","FlightLand","TrampolineCarry","CarryJump","RopeHang","RopePull","RopeLift"};
        public static readonly string[] Characters={"Rabbit","Bear","BluePierrot","PinkGirl"};
        public static readonly string[] Stages={"Intact","Left leg lost","Both legs lost","Right arm only"};
        public HumanoidModel[] Models;
        public GameObject[] CharacterPrefabs;
        public Camera ViewCamera;
        public int MotionIndex {get;private set;}
        public float NormalizedTime {get;private set;}
        public bool Paused {get;private set;}
        public bool CompareAll {get;private set;}=true;
        public int SelectedCharacter {get;private set;}
        public int SelectedStage {get;private set;}
        public bool AutoCycle;
        public bool Repeat=true;
        public float Speed=1;
        float elapsed,cycleAge,yaw=0,pitch=48,zoom=8;
        Text status,pauseLabel,compareLabel,autoLabel,repeatLabel,selection;
        Slider timeline;
        bool settingTimeline;
        Font font;

        public string StateName(int stage)=>Motions[MotionIndex]+(stage==0?"":"_Cut"+stage);
        public void Initialize()
        {
            if(Models==null||Models.Length==0)SpawnActors();
            if(Models==null||Models.Length!=16)throw new InvalidOperationException("Gallery requires four avatars x four stages.");
            for(int i=0;i<Models.Length;i++)
            {
                var m=Models[i];int stage=i%4;var mask=PlayerCondition.PartsForLostCount(stage);
                m.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                m.Animator.applyRootMotion=false;m.Animator.Rebind();m.Animator.Update(0);
                m.Apply(mask,mask,false,null);m.Animator.speed=0;m.Animator.SetLayerWeight(1,0);
            }
            SelectMotion(5);
        }
        void SpawnActors()
        {
            if(CharacterPrefabs==null||CharacterPrefabs.Length!=4)throw new InvalidOperationException("Four character prefab references required.");
            Models=new HumanoidModel[16];
            for(int c=0;c<4;c++)for(int stage=0;stage<4;stage++)
            {
                var anchor=new GameObject(Characters[c]+" - "+Stages[stage]);anchor.transform.SetParent(transform,false);
                anchor.transform.localPosition=new Vector3(stage*3,0,c*3.4f);
                var actor=Instantiate(CharacterPrefabs[c],anchor.transform);actor.transform.localRotation=Quaternion.Euler(0,180,0);
                Models[c*4+stage]=actor.GetComponent<HumanoidModel>();
                var text=new GameObject("Label").AddComponent<TextMesh>();text.transform.SetParent(anchor.transform,false);
                text.transform.localPosition=new Vector3(0,.025f,-.8f);text.transform.localRotation=Quaternion.Euler(70,0,0);
                text.text=Characters[c]+"\n"+Stages[stage];text.fontSize=40;text.characterSize=.035f;
                text.anchor=TextAnchor.MiddleCenter;text.color=new Color(1,.84f,.48f);
            }
        }
        void Start(){Initialize();BuildControls();}
        public void SelectMotion(int index)
        {
            MotionIndex=Mathf.Clamp(index,0,Motions.Length-1);elapsed=cycleAge=0;Sample(0);
        }
        public void SetPaused(bool value){Paused=value;}
        public void Seek(float normalized)
        {
            elapsed=Mathf.Clamp01(normalized)*Duration();Sample(Mathf.Clamp01(normalized));
        }
        float Duration()
        {
            foreach(var c in Models[0].Animator.runtimeAnimatorController.animationClips)
                if(c.name==Motions[MotionIndex])return c.length;
            return 1;
        }
        void Sample(float time)
        {
            NormalizedTime=time;
            for(int i=0;i<Models.Length;i++)
            {
                var a=Models[i].Animator;
                if(!a.gameObject.activeInHierarchy)continue;
                a.Play(StateName(i%4),0,Mathf.Min(time,.99999f));a.Update(0);
                Models[i].GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>()?.CorrectPose();
            }
        }
        public void Focus(int character,int stage,bool compare)
        {
            SelectedCharacter=Mathf.Clamp(character,0,3);SelectedStage=Mathf.Clamp(stage,0,3);CompareAll=compare;
            for(int i=0;i<Models.Length;i++)Models[i].transform.parent.gameObject.SetActive(compare||i==SelectedCharacter*4+SelectedStage);
            Sample(NormalizedTime);
            zoom=compare?8:2.1f;UpdateCamera();
        }
        void Update()
        {
            if(!Paused)
            {
                float dt=Time.unscaledDeltaTime*Speed;elapsed+=dt;cycleAge+=dt;
                if(AutoCycle&&cycleAge>=Mathf.Max(3,Duration()*2))SelectMotion((MotionIndex+1)%Motions.Length);
                float t=elapsed/Duration();
                Sample(Repeat?t-Mathf.Floor(t):Mathf.Min(1,t));
                if(!Repeat&&!AutoCycle&&t>=1)Paused=true;
            }
            UpdateCamera();
            if(status==null)return;
            string support=MotionIndex==5||MotionIndex==6||MotionIndex==9?"Ground contact":MotionIndex==0?"Seated in cannon":MotionIndex==8||MotionIndex==10?"Rope-supported":"Airborne";
            status.text=$"{MotionIndex+1:00} / 11   {Motions[MotionIndex]}\n{NormalizedTime*Duration():0.00} / {Duration():0.00} s   |   {support}";
            pauseLabel.text=Paused?"Play":"Pause";autoLabel.text="Auto cycle: "+(AutoCycle?"ON":"OFF");
            repeatLabel.text="Repeat: "+(Repeat?"ON":"OFF");compareLabel.text=CompareAll?"Focus selected actor":"Show all 16 actors";
            selection.text=Characters[SelectedCharacter]+" / "+Stages[SelectedStage]+"\n"+StateName(SelectedStage);
            settingTimeline=true;timeline.value=NormalizedTime;settingTimeline=false;
        }
        void UpdateCamera()
        {
            if(ViewCamera==null)return;
            float panel=Mathf.Min(.43f,326f*Screen.height/(1000f*Screen.width));
            ViewCamera.rect=new Rect(panel,0,1-panel,1);
            var center=CompareAll?new Vector3(4.5f,.7f,5.1f):Models[SelectedCharacter*4+SelectedStage].transform.parent.position+Vector3.up*.8f;
            ViewCamera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            ViewCamera.transform.position=center-ViewCamera.transform.forward*32;
            ViewCamera.orthographicSize=zoom;
        }
        RectTransform UI(string name,Transform parent,float height)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
            go.AddComponent<LayoutElement>().preferredHeight=height;return rt;
        }
        Text Label(Transform parent,string value,int size=15,float height=26)
        {
            var r=UI(value,parent,height);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;
            t.color=new Color(.92f,.94f,1);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;
        }
        Text Button(Transform parent,string text,Action action,float height=27)
        {
            var r=UI(text,parent,height);r.gameObject.AddComponent<Image>().color=new Color(.18f,.23f,.31f);
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.onClick.AddListener(()=>action());
            var t=Label(r,text,14,height);t.alignment=TextAnchor.MiddleCenter;
            var tr=(RectTransform)t.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
            return t;
        }
        Slider Slider(Transform parent,float min,float max,float initial,Action<float> change)
        {
            var r=UI("Slider",parent,22);var slider=r.gameObject.AddComponent<UnityEngine.UI.Slider>();
            r.gameObject.AddComponent<Image>().color=new Color(.22f,.27f,.33f);
            var h=UI("Handle",r,0);h.anchorMin=new Vector2(0,0);h.anchorMax=new Vector2(0,1);h.sizeDelta=new Vector2(12,0);
            var image=h.gameObject.AddComponent<Image>();image.color=new Color(.95f,.69f,.3f);
            slider.handleRect=h;slider.targetGraphic=image;slider.minValue=min;slider.maxValue=max;slider.value=initial;
            slider.onValueChanged.AddListener(v=>change(v));return slider;
        }
        void BuildControls()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas=new GameObject("Gallery Controls",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1440,1000);scale.matchWidthOrHeight=1;
            var panel=UI("Controls",canvas.transform,0);panel.anchorMin=Vector2.zero;panel.anchorMax=new Vector2(0,1);
            panel.pivot=new Vector2(0,.5f);panel.sizeDelta=new Vector2(326,0);panel.anchoredPosition=Vector2.zero;
            panel.gameObject.AddComponent<Image>().color=new Color(.055f,.075f,.11f,.98f);
            var layout=panel.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(14,14,10,10);
            layout.spacing=4;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            Label(panel,"CIRCUS MOTION LAB",22,34);
            Label(panel,"Rows: characters / Columns: injury stages",12,22);
            status=Label(panel,"",15,48);
            for(int i=0;i<Motions.Length;i++){int n=i;Button(panel,$"{i+1:00}  {Motions[i]}",()=>SelectMotion(n));}
            pauseLabel=Button(panel,"Pause",()=>SetPaused(!Paused));
            Button(panel,"Restart",()=>Seek(0));
            timeline=Slider(panel,0,1,0,v=>{if(!settingTimeline){SetPaused(true);Seek(v);}});
            repeatLabel=Button(panel,"Repeat",()=>Repeat=!Repeat);
            autoLabel=Button(panel,"Auto cycle",()=>{AutoCycle=!AutoCycle;Paused=false;cycleAge=0;});
            Label(panel,"Speed (0.1 - 2x)",13,22);Slider(panel,.1f,2,1,v=>Speed=v);
            selection=Label(panel,"",14,44);
            Button(panel,"Next character",()=>Focus((SelectedCharacter+1)%4,SelectedStage,CompareAll));
            Button(panel,"Next injury stage",()=>Focus(SelectedCharacter,(SelectedStage+1)%4,CompareAll));
            compareLabel=Button(panel,"Focus selected actor",()=>Focus(SelectedCharacter,SelectedStage,!CompareAll));
            Label(panel,"Camera: turn / elevation / zoom",13,22);
            Slider(panel,-180,180,0,v=>yaw=v);Slider(panel,5,80,48,v=>pitch=v);Slider(panel,.8f,15,8,v=>zoom=v);
            Label(panel,"Pose inspection. Gameplay contact / flight\nare tested in the actual course scenes.",12,36);
            var events=new GameObject("Gallery EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
