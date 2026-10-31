using System;
using System.Collections.Generic;
using CurtainCall.Player;
using CurtainCall.WaitingRoom;
using CurtainCall.CharacterArt;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.ItemArt
{
    [DefaultExecutionOrder(950)]
    public sealed class ItemInteractionGallery : MonoBehaviour
    {
        public GameObject[] CharacterPrefabs;
        public Camera ViewCamera;
        public HumanoidModel[] Models { get; private set; }
        public GameObject[] HandProps { get; private set; }
        public float TargetHeight = 1.1f;
        public bool CompareAll = true, Paused;
        public int Character, Stage, MotionIndex = 4;
        public float NormalizedTime { get; private set; }
        readonly ItemCarryBook book = new(32);
        readonly Dictionary<int, GameObject> floor = new();
        readonly string[] motionLabels = { "휘두르기", "손으로 조작", "잡아당기기", "정면 조준", "한손 들기", "꺼내기", "넣기", "오른손 기어가기" };
        readonly string[] stageLabels = { "정상", "왼다리 없음", "양다리 없음", "오른팔만 남음" };
        readonly string[] characterLabels = { "토끼", "곰", "파랑 삐에로", "분홍 소녀" };
        Transform[] targets;
        string shownId, message = "기물을 선택해 내려놓고 다시 주워 보세요. 장비는 선택해도 손에 들지 않습니다.";
        int uid = 100, selected = -1;
        float elapsed;
        bool preview, uv;
        string previewItem;
        Font font;
        public IReadOnlyList<PropItem> Inventory => book.Held(0);
        public int FloorCount => book.Floor.Count;
        public void Initialize()
        {
            if(Models != null)return;
            Models=new HumanoidModel[16];HandProps=new GameObject[16];targets=new Transform[16];
            for(int c=0;c<4;c++)for(int stage=0;stage<4;stage++)
            {
                int i=c*4+stage;
                var anchor=new GameObject(characterLabels[c]+" / "+stageLabels[stage]);anchor.transform.SetParent(transform,false);anchor.transform.localPosition=new Vector3(stage*3,0,c*3.2f);
                var actor=Instantiate(CharacterPrefabs[c],anchor.transform);actor.transform.localRotation=Quaternion.Euler(0,180,0);
                var m=actor.GetComponent<HumanoidModel>();Models[i]=m;
                m.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;m.Animator.applyRootMotion=false;m.Animator.Rebind();m.Animator.Update(0);
                var mask=PlayerCondition.PartsForLostCount(stage);m.Apply(mask,mask,false,null);m.Animator.speed=0;
                for(int l=1;l<m.Animator.layerCount;l++)m.Animator.SetLayerWeight(l,0);
                var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Interaction target";target.transform.SetParent(anchor.transform,false);target.transform.localPosition=new Vector3(-.2f,TargetHeight,-.8f);target.transform.localScale=new Vector3(.32f,.28f,.08f);Destroy(target.GetComponent<Collider>());targets[i]=target.transform;
            }
            Add("stake-puller");Add("uv-lamp");selected=book.Held(0)[0].Uid;
            font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",16);
        }
        void Start()=>Initialize();
        public void Add(string id)
        {
            foreach(var existing in book.Held(0))if(existing.ItemId==id){selected=existing.Uid;preview=false;return;}
            if(book.Held(0).Count>=2){if(selected<0)selected=book.Held(0)[0].Uid;Drop();}
            var item=ItemCatalog.NewProduct(++uid,id,id);
            var result=book.Take(0,2,item);
            message=result==CarryRefusal.None?ItemCatalog.DisplayName(item)+" 지급": "두 슬롯이 찼습니다. 먼저 내려놓으세요.";
            if(result==CarryRefusal.None)selected=item.Uid;
            preview=false;
        }
        public void SelectSlot(int slot)
        {
            var items=book.Held(0);if(slot<0||slot>=items.Count){selected=-1;return;}
            selected=selected==items[slot].Uid?-1:items[slot].Uid;
            preview=false;Play(selected<0?6:5,false);
        }
        PropItem Selected(){foreach(var item in book.Held(0))if(item.Uid==selected)return item;return default;}
        public void Drop()
        {
            var item=Selected();if(item.Uid==0)return;
            int index=book.Floor.Count;var point=new Vector3(-2.4f,0,index*.55f);
            if(book.Drop(0,point.x,point.y,point.z,out var dropped,item.Uid)!=CarryRefusal.None)return;
            var visual=ItemVisualAssets.Create(item.ItemId);if(visual!=null){visual.transform.position=point;floor[item.Uid]=visual;}
            selected=-1;preview=false;Play(6,false);message="내려놓음: "+item.Name+" / F 또는 줍기 버튼";
        }
        public void PickUp()
        {
            if(book.Floor.Count==0)return;var dropped=book.Floor[0];
            var result=book.PickUp(dropped.Item.Uid,0,2,out var item);
            if(result!=CarryRefusal.None){message="두 슬롯이 찼습니다.";return;}
            if(floor.TryGetValue(item.Uid,out var visual)){Destroy(visual);floor.Remove(item.Uid);}
            selected=item.Uid;preview=false;Play(5,false);message="주움: "+item.Name;
        }
        public void Play(int motion,bool directPreview=true)
        {MotionIndex=Mathf.Clamp(motion,0,7);elapsed=0;preview=directPreview;previewItem=null;Paused=false;}
        public void Interact()
        {
            string id=Selected().ItemId;
            Play(id=="stake-puller"?0:1,false);
            message=id=="stake-puller"?"판자 타격 → 회복":id=="magician-hairpin"?"자물쇠 접근 → 손목 조작 → 회복":"상자 / 문 손잡이 조작";
        }
        float Duration()=>MotionIndex==0?1.2f:MotionIndex==1?1.4f:MotionIndex==2||MotionIndex==7?1.6f:MotionIndex==5||MotionIndex==6?.6f:2;
        void Update()
        {
            if(Models==null)return;
            var kb=Keyboard.current;
            if(kb!=null)
            {
                if(kb.digit1Key.wasPressedThisFrame)SelectSlot(0);if(kb.digit2Key.wasPressedThisFrame)SelectSlot(1);
                if(kb.gKey.wasPressedThisFrame)Drop();if(kb.fKey.wasPressedThisFrame)PickUp();if(kb.eKey.wasPressedThisFrame)Interact();
            }
            uv=false;if(Mouse.current!=null&&Mouse.current.rightButton.isPressed)foreach(var item in book.Held(0))if(item.ItemId=="uv-lamp")uv=true;
            if(!Paused)elapsed+=Time.deltaTime;
            if(!preview&&elapsed>Duration()&&MotionIndex!=4){MotionIndex=4;elapsed=0;}
            NormalizedTime=(elapsed/Duration())%1f;
        }
        void LateUpdate(){if(Models!=null)Sample(NormalizedTime);}
        public void Sample(float time)
        {
            string item=Selected().ItemId;
            string display=uv?"uv-lamp":ItemVisualAssets.IsTool(item)?item:null;
            if(preview)display=MotionIndex==3?"uv-lamp":MotionIndex==1?"magician-hairpin":MotionIndex==2?null:"stake-puller";
            if(preview&&previewItem!=null)display=previewItem;
            if(display!=shownId)
            {
                shownId=display;
                for(int i=0;i<16;i++){if(HandProps[i]!=null)Destroy(HandProps[i]);HandProps[i]=ItemVisualAssets.Create(display);}
            }
            for(int i=0;i<16;i++)
            {
                var m=Models[i];bool visible=CompareAll||i==Character*4+Stage;m.transform.parent.gameObject.SetActive(visible);
                if(HandProps[i]!=null)HandProps[i].SetActive(visible&&MotionIndex!=6);
                if(!visible)continue;
                int stage=i%4;string motion=uv?"ItemAim":ItemVisualAssets.Motions[MotionIndex];
                if(!preview&&display==null&&MotionIndex==4)motion=stage>=2?"ItemCrawl":"Idle";
                string state=motion=="Idle"?motion:ItemVisualAssets.State(motion,stage);
                m.Animator.Play(state,0,Mathf.Min(time,.9999f));m.Animator.Update(0);
                targets[i].localPosition=new Vector3(-.2f,TargetHeight,-.8f);
                targets[i].gameObject.SetActive(MotionIndex<3||MotionIndex==3||uv);
                if(MotionIndex==1||MotionIndex==2||uv)ItemVisualAssets.Bend(m.Animator,targets[i].position,uv?1:Mathf.Sin(time*Mathf.PI));
                m.GetComponent<RabbitTrialGrounding>()?.CorrectPose();
                if(display=="uv-lamp")ItemVisualAssets.AimLamp(m.Animator,m.transform.forward,MotionIndex==7);
                ItemVisualAssets.Attach(HandProps[i],m.Animator,display);
                targets[i].localRotation=Quaternion.Euler(MotionIndex==0&&time>.55f?65:MotionIndex==1?Mathf.Sin(time*Mathf.PI)*55:0,0,0);
            }
            if(ViewCamera!=null)
            {
                Vector3 center=CompareAll?new Vector3(4.5f,0,4.5f):Models[Character*4+Stage].transform.parent.position+Vector3.up*.55f;
                ViewCamera.orthographicSize=CompareAll?8.4f:2.0f;ViewCamera.transform.position=center+new Vector3(CompareAll?0:2,CompareAll?16:2.2f,CompareAll?-17:-4.5f);
                ViewCamera.transform.LookAt(center);
            }
        }
        void OnGUI()
        {
            if(Models==null)return;
            GUI.skin.font=font;GUI.skin.button.fontSize=13;GUI.skin.label.fontSize=14;
            GUILayout.BeginArea(new Rect(14,12,Mathf.Min(Screen.width-28,1130),255),GUI.skin.box);
            GUILayout.Label("CURTAIN CALL  |  기물 · 오른손 상호작용 검증실");
            GUILayout.BeginHorizontal();
            for(int i=0;i<motionLabels.Length;i++)if(GUILayout.Button(motionLabels[i],GUILayout.Height(28)))Play(i);
            GUILayout.EndHorizontal();GUILayout.BeginHorizontal();
            if(GUILayout.Button(CompareAll?"16종 비교 → 개별 확대":"개별 확대 → 16종 비교"))CompareAll=!CompareAll;
            Character=GUILayout.SelectionGrid(Character,characterLabels,4);Stage=GUILayout.SelectionGrid(Stage,stageLabels,4);
            if(GUILayout.Button(Paused?"재생":"일시정지",GUILayout.Width(70)))Paused=!Paused;
            GUILayout.EndHorizontal();GUILayout.BeginHorizontal();GUILayout.Label("대상 높이 "+TargetHeight.ToString("F2")+"m",GUILayout.Width(150));TargetHeight=GUILayout.HorizontalSlider(TargetHeight,.2f,2.1f);
            GUILayout.Label("재생 위치",GUILayout.Width(70));float seek=GUILayout.HorizontalSlider(NormalizedTime,0,.999f);if(Mathf.Abs(seek-NormalizedTime)>.01f){elapsed=seek*Duration();NormalizedTime=seek;Paused=true;}
            GUILayout.EndHorizontal();GUILayout.Label(message);GUILayout.Label("1·2 선택/넣기  |  G 내려놓기  |  F 줍기  |  E 상호작용  |  우클릭 홀드: 소지한 UV 등 조준");
            GUILayout.BeginHorizontal();for(int i=0;i<2;i++)
            {var items=book.Held(0);var it=i<items.Count?items[i]:default;if(it.Uid!=0)GUILayout.Box(ItemVisualAssets.Icon(it.ItemId)?.texture,GUILayout.Width(44),GUILayout.Height(44));if(GUILayout.Button((selected==it.Uid&&it.Uid!=0?"● ":"")+(i+1)+" "+(it.Uid==0?"빈 슬롯":it.Name),GUILayout.Width(180),GUILayout.Height(45)))SelectSlot(i);}
            if(GUILayout.Button("내려놓기",GUILayout.Height(45)))Drop();if(GUILayout.Button("바닥 줍기 ("+FloorCount+")",GUILayout.Height(45)))PickUp();if(GUILayout.Button("상호작용",GUILayout.Height(45)))Interact();GUILayout.EndHorizontal();GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(14,Screen.height-125,Mathf.Min(Screen.width-28,1130),113),GUI.skin.box);
            for(int row=0;row<2;row++){GUILayout.BeginHorizontal();for(int column=0;column<4;column++){string id=ItemVisualAssets.Ids[row*4+column];var icon=ItemVisualAssets.Icon(id,true);if(icon!=null)GUILayout.Box(icon.texture,GUILayout.Width(38),GUILayout.Height(38));if(GUILayout.Button(ItemVisualAssets.DisplayName(id),GUILayout.Height(38)))Add(id);}GUILayout.EndHorizontal();}GUILayout.EndArea();
        }
        void OnDestroy(){if(HandProps!=null)foreach(var prop in HandProps)if(prop!=null)Destroy(prop);foreach(var prop in floor.Values)if(prop!=null)Destroy(prop);if(font!=null)Destroy(font);}
    }
}
