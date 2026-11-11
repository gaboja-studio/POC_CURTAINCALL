using System;
using System.Collections.Generic;
using System.Globalization;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    // Local QA scene only. The form calls the same public presenter as gameplay.
    public sealed class HudRolePlayground : MonoBehaviour
    {
        public HudRole initialRole=HudRole.Interaction;
        public Camera sceneCamera;
        public Font panelFont;
        TMP_FontAsset runtimeFont;
        public Transform actor, wall;
        public Transform[] targets;
        public LineRenderer ring;
        public TMP_Dropdown role, target, statusPosition,eventKind;
        public TMP_InputField content, heading, condition, seconds, key, firstChoice, secondChoice;
        public Button output, clear, toggle, inside, outside, block, targetLeft, targetRight, interact;
        public TMP_Text diagnostic, message;
        public GameObject form, simulation;
        static readonly string[] Bodies = { "모든 단원이 무대 입구에 모이세요.", "무대 입구로 복귀하세요.", "W/S로 이동하고 A/D로 균형을 잡으세요.", "상자 열기", "은색 열쇠를 획득했습니다.", "은색 열쇠 1", "과반이 동의하면 다음 구간으로 이동합니다.", "완주 2명 · 사망 1명","연습실" };
        static readonly string[] Heads = { "무대 입구 집결", "소집 경보", "외줄 건너기", "", "아이템 획득", "소지 기물", "다음 구간으로 갈까요?", "공연 정산","" };
        readonly List<HudRoleCard> emitted = new();
        HudRoleCard prompt;
        Transform watchedTarget;
        bool armed, wallOn, eligible;
        int interactions;
        string actionText, keyText;
        Key actionKey;
        Vector3[] circle=new Vector3[65];
        HudRole Kind => (HudRole)role.value;
        Transform Selected => targets[target.value];
        float Range => GameSettings.Base.Interaction.Distance;
        public bool PromptVisible => prompt && !prompt.Closing && prompt.motion.surface.gameObject.activeInHierarchy;
        public bool WithinReach => eligible;

        void Start()
        {
            if(panelFont)
            {
                runtimeFont=TMP_FontAsset.CreateFontAsset(panelFont);
                runtimeFont.isMultiAtlasTexturesEnabled=true;
                var controls=form.transform.parent;
                foreach(var label in controls.GetComponentsInChildren<TMP_Text>(true)) label.font=runtimeFont;
                foreach(var field in controls.GetComponentsInChildren<TMP_InputField>(true)) field.fontAsset=runtimeFont;
            }
            role.onValueChanged.AddListener(_=>SelectRole());
            eventKind.onValueChanged.AddListener(_=>EventSample(false));
            target.onValueChanged.AddListener(_=>{ DropPrompt(); RefreshWorld(); });
            output.onClick.AddListener(Emit); clear.onClick.AddListener(Clear);
            toggle.onClick.AddListener(()=>{form.SetActive(!form.activeSelf); toggle.GetComponentInChildren<TMP_Text>().text=form.activeSelf?"패널 접기":"테스트 패널 열기";});
            inside.onClick.AddListener(()=>PlaceActor(true)); outside.onClick.AddListener(()=>PlaceActor(false));
            block.onClick.AddListener(()=>{wallOn=!wallOn; RefreshWorld();});
            targetLeft.onClick.AddListener(()=>MoveTarget(-.75f)); targetRight.onClick.AddListener(()=>MoveTarget(.75f));
            interact.onClick.AddListener(Act);
            Shade(actor.GetComponentInChildren<Renderer>(),new Color(.18f,.77f,.82f));
            Shade(targets[0].GetComponent<Renderer>(),new Color(.96f,.62f,.18f));
            Shade(targets[1].GetComponent<Renderer>(),new Color(.80f,.42f,.92f));
            Shade(wall.GetComponent<Renderer>(),new Color(.54f,.30f,.27f));
            foreach(var t in targets) t.position=new Vector3(t.position.x,PlayerInteraction.Center(actor).y,t.position.z);
            ring.positionCount=circle.Length;
            role.SetValueWithoutNotify((int)initialRole);
            content.lineType=TMP_InputField.LineType.MultiLineNewline;
            var contentRow=content.transform.parent.GetComponent<LayoutElement>();
            contentRow.minHeight=contentRow.preferredHeight=47;
            PlaceActor(true); SelectRole();
        }
        static void Shade(Renderer r,Color color)
        {
            var data=new MaterialPropertyBlock(); data.SetColor("_BaseColor",color); data.SetColor("_Color",color); r.SetPropertyBlock(data);
        }
        static void Row(Component field,bool visible)=>field.transform.parent.gameObject.SetActive(visible);
        static readonly string[] EventHeads={"방송","소집 경보","공연 보상"};
        static readonly string[] EventBodies={"리허설을 준비하세요.","무대 입구로 복귀하세요.","공동 자금 +200"};
        void EventSample(bool force)
        {
            if(Kind!=HudRole.SystemEvent)return;
            int i=eventKind.value;
            if(force || Array.IndexOf(EventHeads,heading.text)>=0)heading.text=EventHeads[i];
            if(force || Array.IndexOf(EventBodies,content.text)>=0)content.text=EventBodies[i];
        }
        void SelectRole()
        {
            armed=false; DropPrompt();
            var r=Kind;
            heading.text=Heads[(int)r]; content.text=Bodies[(int)r];EventSample(true);
            condition.text=r==HudRole.Vote ? "버튼을 눌러 투표하세요." : "집결 1/3";
            seconds.text="";
            Row(eventKind,r==HudRole.SystemEvent); Row(heading,r!=HudRole.Interaction && r!=HudRole.ZoneName); Row(condition,r==HudRole.Objective || r==HudRole.Vote);
            Row(seconds,r==HudRole.SystemEvent || r==HudRole.Guide || r==HudRole.Feedback || r==HudRole.Outcome);
            if(r==HudRole.Guide) key.text=""; else if(r==HudRole.Interaction)key.text="F";
            Row(key,r==HudRole.Interaction || r==HudRole.Guide); Row(target,r==HudRole.Interaction); Row(statusPosition,r==HudRole.Status);
            Row(firstChoice,r==HudRole.Vote); Row(secondChoice,r==HudRole.Vote);
            simulation.SetActive(r==HudRole.Interaction);
            message.text=r==HudRole.Interaction ? "출력을 누르면 접근·이탈에 따라 UI가 켜지고 꺼집니다." : "입력 후 출력을 누르세요. 여러 역할을 함께 띄울 수 있습니다.";
            RefreshWorld();
        }
        public void Emit()
        {
            string text=content.text.Trim();
            if(text.Length==0){message.text="본문을 입력하세요.";return;}
            float? duration=null;
            if(seconds.transform.parent.gameObject.activeSelf && !string.IsNullOrWhiteSpace(seconds.text))
            {
                if(!float.TryParse(seconds.text,NumberStyles.Float,CultureInfo.InvariantCulture,out var val)||!float.IsFinite(val)||val<0){message.text="노출 시간은 0 이상의 숫자입니다. 비워 두면 프리팹 기본값입니다.";return;}
                duration=val;
            }
            string title=heading.text;
            var hud=HudUIManager.Current;
            HudRoleCard card=null;
            switch(Kind)
            {
                case HudRole.Objective: card=hud.ShowObjective(text,transform,title,condition.text); break;
                case HudRole.SystemEvent: card=hud.ShowSystemEvent(text,transform,title,duration,(HudEventKind)eventKind.value); break;
                case HudRole.ZoneName: card=hud.ShowZoneName(text,transform);break;
                case HudRole.Guide: card=hud.ShowGuide(text,transform,title,duration,key.text); break;
                case HudRole.Interaction:
                    if(string.IsNullOrWhiteSpace(key.text)){message.text="표시할 키를 입력하세요.";return;}
                    DropPrompt(); actionText=text; keyText=key.text.Trim();
                    actionKey=Enum.TryParse<Key>(keyText,true,out var parsed) && Enum.IsDefined(typeof(Key),parsed) ? parsed : Key.None;
                    armed=true; RefreshWorld(); break;
                case HudRole.Feedback: card=hud.ShowFeedback(text,transform,title,duration); break;
                case HudRole.Status: card=hud.ShowStatus(text,transform,title,statusPosition.value==1); break;
                case HudRole.Vote:
                    var choices=new List<HudVoteChoice>();
                    foreach(var input in new[]{firstChoice,secondChoice})
                    {
                        var label=input.text.Trim(); if(label.Length!=0) choices.Add(new HudVoteChoice(label,true,()=>message.text="선택: "+label));
                    }
                    if(choices.Count==0){message.text="선택지를 하나 이상 입력하세요.";return;}
                    card=hud.ShowVote(text,transform,title,condition.text,choices.ToArray()); break;
                case HudRole.Outcome: card=hud.ShowOutcome(text,transform,title,duration); break;
            }
            emitted.RemoveAll(c=>!c || c.Closing);
            if(card) emitted.Add(card);
            message.text=armed ? "거리 감시 중 · 대상 좌우 이동 또는 범위 안/밖을 눌러 보세요." : "출력: "+role.options[role.value].text;
        }
        void MoveTarget(float dx)
        {
            var t=Selected; var p=t.position; p.x=Mathf.Clamp(p.x+dx,-5f,5f); t.position=p; RefreshWorld();
        }
        public void PlaceActor(bool inRange)
        {
            var t=Selected; var p=t.position; p.y=0;
            actor.position=p+Vector3.back*(inRange ? Range*.65f : Range+.5f); RefreshWorld();
        }
        void Update()
        {
            if(Kind!=HudRole.Interaction) return;
            var keyboard=Keyboard.current;
            var focused=EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool acceptsKeys=keyboard!=null && !role.IsExpanded && !target.IsExpanded && !(focused && focused.GetComponentInParent<TMP_InputField>());
            if(acceptsKeys)
            {
                float x=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0);
                float z=(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0);
                var p=actor.position+Vector3.ClampMagnitude(new Vector3(x,0,z),1)*3f*Time.unscaledDeltaTime;
                actor.position=new Vector3(Mathf.Clamp(p.x,-7,7),0,Mathf.Clamp(p.z,-6,6));
            }
            RefreshWorld();
            if(acceptsKeys && armed && actionKey!=Key.None && keyboard[actionKey].wasPressedThisFrame) Act();
        }
        void RefreshWorld()
        {
            var t=Selected;
            bool testing=Kind==HudRole.Interaction;
            wall.gameObject.SetActive(testing && wallOn); ring.gameObject.SetActive(testing);
            if(!testing) return;
            var center=PlayerInteraction.Center(actor);
            if(wallOn)
            {
                wall.position=(center+t.position)*.5f;
                var delta=t.position-center; delta.y=0;
                if(delta.sqrMagnitude>.001f) wall.rotation=Quaternion.LookRotation(delta);
            }
            Physics.SyncTransforms();
            float distance=Vector3.Distance(center,t.position);
            eligible=ExplorationReach.From(t,t.position,actor.position,Range,actor:actor)>=0f;
            var vp=sceneCamera.WorldToViewportPoint(t.position);
            bool onScreen=vp.z>=sceneCamera.nearClipPlane && vp.z<=sceneCamera.farClipPlane && vp.x>=0 && vp.x<=1 && vp.y>=0 && vp.y<=1;
            if(!armed || !eligible || !onScreen) DropPrompt();
            else if(!prompt || prompt.Closing || watchedTarget!=t)
            {
                DropPrompt(); watchedTarget=t; prompt=HudUIManager.Current.ShowInteraction(actionText,t,keyText,sceneCamera);
            }
            float dy=center.y-t.position.y;
            float radius=Mathf.Sqrt(Mathf.Max(0,Range*Range-dy*dy));
            for(int i=0;i<circle.Length;i++){float a=i*2*Mathf.PI/(circle.Length-1);circle[i]=new Vector3(t.position.x+radius*Mathf.Cos(a),.035f,t.position.z+radius*Mathf.Sin(a));}
            ring.SetPositions(circle);
            diagnostic.text=$"{target.options[target.value].text}  ·  {distance:F2} / {Range:F2} m  ·  "+(!eligible ? (distance>Range?"범위 밖":"경로 막힘") : !onScreen?"화면 밖":"도달 가능");
            interact.interactable=armed && eligible && onScreen;
        }
        void Act(){ RefreshWorld(); if(armed && eligible && PromptVisible) message.text=$"{actionText} · 테스트 입력 {++interactions}회"; }
        void DropPrompt(){ if(prompt) prompt.Close(true); prompt=null; watchedTarget=null; }
        public void Clear(){ armed=false; DropPrompt(); foreach(var c in emitted) if(c) c.Close(true); emitted.Clear(); interactions=0; message.text="표시한 UI를 지웠습니다."; }
        void OnDestroy(){if(runtimeFont) Destroy(runtimeFont);}
        void OnDisable(){armed=false; DropPrompt(); foreach(var c in emitted) if(c) c.Close(true); emitted.Clear();}
    }
}
