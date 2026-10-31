using System.Collections;
using CurtainCall.Campaign;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.ItemArt
{
    /// <summary>Uses the real Player, PropCarry, exploration targets and UV implementation in an offline run.</summary>
    public sealed class ItemGameplayPlayground : MonoBehaviour
    {
        public GameObject Player;
        public GameObject[] CharacterPrefabs;
        public Camera ViewCamera;
        public Transform[] Stations;
        public Transform KeyPickup,KeyDoor;
        int character,stage,uid=880000;
        Font font;
        bool ready;
        string toolMessage="시험할 도구를 아래에서 선택하세요. 두 칸이 차면 선택 기물을 내려놓고 교체합니다.";
        IEnumerator Start()
        {
            font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",16);
            var campaign=CampaignRun.EnsureExists();
            if(!campaign.StartOfflineRun())yield break;
            var rewards=PerformanceRewards.EnsureExists();
            if(PropCarry.Instance==null)new GameObject("Playground PropCarry").AddComponent<PropCarry>();
            if(ExplorationState.Instance==null)new GameObject("Playground ExplorationState").AddComponent<ExplorationState>();
            while(!rewards.HasAuthority)yield return null;
            Player.GetComponent<PlayerBalance>().SetBalanceActive(false);
            Player.GetComponent<PlayerController>().SetScheme(ScriptableObject.CreateInstance<PlatformControlScheme>());
            Player.GetComponent<PlayerMover>().SetStartPose(new Vector3(0,.05f,-1.2f),Quaternion.identity);
            SetAppearance();ready=true;Give("stake-puller");Give("uv-lamp");PropCarry.Instance.RequestSelectSlot(0);
        }
        public void Give(string id)
        {
            if(!ready)return;
            var carry=PropCarry.Instance;
            var held=carry.Held(0);
            for(int i=0;i<held.Count;i++)if(held[i].ItemId==id){if(carry.Selected(0).Uid!=held[i].Uid)carry.RequestSelectSlot(i);toolMessage=ItemVisualAssets.DisplayName(id)+" 선택";return;}
            if(held.Count>=2){if(carry.Selected(0).Uid==0)carry.RequestSelectSlot(0);carry.RequestDrop();}
            carry.Receive(0,ItemCatalog.NewProduct(++uid,id,id));
            held=carry.Held(0);for(int i=0;i<held.Count;i++)if(held[i].ItemId==id&&carry.Selected(0).Uid!=held[i].Uid){carry.RequestSelectSlot(i);break;}
            toolMessage=ItemVisualAssets.DisplayName(id)+" 지급 / "+carry.LastMessage;
        }
        public void SetAppearance()
        {
            Player.GetComponent<PlayerCondition>().SetLostParts(PlayerCondition.PartsForLostCount(stage));
            Player.GetComponent<PlayerModelSlot>().SetModel(CharacterPrefabs[character]);
        }
        void Update()
        {
            if(!ready||Keyboard.current==null)return;
            if(Keyboard.current.digit1Key.wasPressedThisFrame)PropCarry.Instance.RequestSelectSlot(0);
            if(Keyboard.current.digit2Key.wasPressedThisFrame)PropCarry.Instance.RequestSelectSlot(1);
            if(Keyboard.current.gKey.wasPressedThisFrame)PropCarry.Instance.RequestDrop();
            ShopReach.Pump();
        }
        void LateUpdate()
        {
            if(Player==null||ViewCamera==null)return;
            Vector3 target=Player.transform.position+Vector3.up*.7f;
            ViewCamera.transform.position=target+new Vector3(3,3.2f,-5);ViewCamera.transform.LookAt(target);
        }
        void OnGUI()
        {
            GUI.skin.font=font;
            GUILayout.BeginArea(new Rect(12,12,Mathf.Min(Screen.width-24,1120),270),GUI.skin.box);
            GUILayout.Label("실제 PLAYER 검증 · 오프라인 테스트 런");
            GUILayout.Label("WASD 이동 · F 상호작용/줍기 · F 길게 또는 G 내려놓기 · 1/2 선택/넣기 · 우클릭 UV");
            GUILayout.BeginHorizontal();int c=GUILayout.SelectionGrid(character,new[]{"토끼","곰","파랑 삐에로","분홍 소녀"},4);int s=GUILayout.SelectionGrid(stage,new[]{"정상","외발","양다리 없음","오른팔만"},4);
            if(c!=character||s!=stage){character=c;stage=s;SetAppearance();}
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();for(int i=0;i<Stations.Length;i++)if(GUILayout.Button(new[]{"타격 벽 앞으로","잠긴 상자 앞으로","일반 문 앞으로"}[i]))Player.GetComponent<PlayerMover>().SetStartPose(Stations[i].position+(i==1?new Vector3(.35f,0,-.5f):Vector3.back*1.1f),Quaternion.identity);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(KeyPickup!=null&&GUILayout.Button("열쇠 줍기 시험대"))Player.GetComponent<PlayerMover>().SetStartPose(KeyPickup.position+Vector3.back*.8f,Quaternion.identity);
            if(KeyDoor!=null&&GUILayout.Button("열쇠 문 시험대"))Player.GetComponent<PlayerMover>().SetStartPose(KeyDoor.position+Vector3.back*.9f,Quaternion.identity);
            GUILayout.EndHorizontal();
            if(PropCarry.Instance!=null)
            {
                var items=PropCarry.Instance.Held(0);var selected=PropCarry.Instance.Selected(0);GUILayout.BeginHorizontal();
                for(int i=0;i<2;i++)if(i<items.Count)
                {var icon=ItemVisualAssets.Icon(items[i].ItemId);if(icon!=null)GUILayout.Box(icon.texture,GUILayout.Width(46),GUILayout.Height(46));if(GUILayout.Button((selected.Uid==items[i].Uid?"● ":"")+(i+1)+" "+items[i].Name,GUILayout.Width(170),GUILayout.Height(46)))PropCarry.Instance.RequestSelectSlot(i);}
                else GUILayout.Label((i+1)+" 빈 슬롯",GUILayout.Width(170));
                if(GUILayout.Button("선택 기물 내려놓기",GUILayout.Height(46)))PropCarry.Instance.RequestDrop();GUILayout.EndHorizontal();
            }
            GUILayout.Label(ShopReach.Focused is IShopPresentation p?p.ActionLabel:"대상 앞으로 이동해 F를 누르세요.");
            GUILayout.Label(ExplorationState.Instance?.LastMessage??"");GUILayout.Label(toolMessage);GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(12,Screen.height-130,Mathf.Min(Screen.width-24,1120),120),GUI.skin.box);
            for(int row=0;row<2;row++)
            {
                GUILayout.BeginHorizontal();
                for(int column=0;column<4;column++)
                {string id=ItemVisualAssets.Ids[row*4+column];var icon=ItemVisualAssets.Icon(id,true);if(icon!=null)GUILayout.Box(icon.texture,GUILayout.Width(38),GUILayout.Height(38));if(GUILayout.Button(ItemVisualAssets.DisplayName(id),GUILayout.Height(38)))Give(id);}
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("머리핀: 선택 후 잠긴 상자에서 F  |  열쇠: 버튼으로 손동작 / 열쇠 시험대에서 획득·문 열기");GUILayout.EndArea();
        }
        void OnDestroy(){if(font!=null)Destroy(font);}
    }
}
