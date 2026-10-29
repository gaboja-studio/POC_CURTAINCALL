using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.StartLobby;
using CurtainCall.Campaign;
using CurtainCall.SpaceLink;
using CurtainCall.Player;
using CurtainCall.UI.BackstageHudV1;
using CurtainCall.UI.GuidesV1;
using CurtainCall.UI.NotificationsV1;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Lobby-only presentation; ready/start authority remains with the physical board and lever.</summary>
    public sealed class LobbyHudBinding : MonoBehaviour
    {
        public LobbyRoom room;
        public LobbyReadyBoard board;
        public LobbyCurtainLever lever;
        public LobbyRoomInfoView info;
        public GuideView guide;
        public SystemBannerView banner;
        public ToastView toast;
        NetworkSessionManager session;
        CampaignRun campaign;
        string previousBanner;
        string previousGuide;
        bool wasVisible;
        GameObject appearancePanel;
        TMP_Text appearanceLabel;

        void Start()
        {
            session=NetworkSessionManager.EnsureExists();
            campaign=CampaignRun.EnsureExists();
            info.CopyRequested+=Copy;
            CreateAppearanceControls();
        }
        void OnDestroy() { if(info!=null) info.CopyRequested-=Copy; }
        void OnDisable()
        {
            if(info!=null) info.SetContext(false);
            if(guide!=null) guide.HideImmediate();
            if(banner!=null) banner.HideImmediate();
            if(toast!=null) toast.HideImmediate();
            previousBanner=previousGuide=null;wasVisible=false;
        }
        void Update()
        {
            bool connected=session!=null && (session.ConnectionState==SessionConnectionState.Host || session.ConnectionState==SessionConnectionState.Client);
            bool visible=connected && !SpaceLinkHub.HasLoadedSpace(gameObject.scene)
                && (SpaceLinkDirector.Instance==null || !SpaceLinkDirector.Instance.IsTransitioning);
            info.SetContext(visible);
            if (appearancePanel != null) appearancePanel.SetActive(visible && session.GameState == GameSessionState.Waiting && NetworkPlayer.Local != null);
            if(!visible) { if(wasVisible) OnDisable(); return; }
            wasVisible=true;
            var host=NetworkPlayer.All.FirstOrDefault(p=>p!=null && p.OwnerClientId==NetworkManager.ServerClientId);
            info.SetRoomInfo(session.JoinKey,host==null?null:$"단원 {host.Slot+1}",session.PlayerCount,session.RequiredPlayers);
            info.SetControlsEnabled(!InputGate.IsBlocked);
            if (appearanceLabel != null && NetworkPlayer.Local != null)
            {
                var p=NetworkPlayer.Local; var catalog=CurtainCall.CharacterArt.PlayerAppearanceCatalog.Current;
                if(catalog != null && catalog.Valid(p.AppearanceModel,p.AppearanceOutfit)) appearanceLabel.text=catalog.models[p.AppearanceModel].name+" · "+catalog.outfits[p.AppearanceOutfit];
            }
            string title,sub;
            var state=campaign.State;
            if(state.Phase==CampaignPhase.Intro)
            {title=state.IsGenerationChangeIntro?"새 세대가 무대에 섭니다":"막이 오릅니다";sub="첫 공연을 준비하세요";}
            else if(state.Phase==CampaignPhase.Ending)
            {title="커튼 콜!";sub=session.IsHost?"커튼 레버로 다시 준비하세요":"방장이 다시 준비할 때까지 기다려주세요";}
            else
            {
                title=$"출연 준비 · 서명 {board.ReadyCount} / {session.PlayerCount}";
                sub=lever.Prompt(out _).Replace("[F] ","");
            }
            bool blocked=InputGate.IsBlocked;
            var key=title+"|"+sub;
            if(blocked){banner.HideImmediate();previousBanner=null;}
            else if(key!=previousBanner){banner.Show(title,sub,BannerState.Normal,3600);previousBanner=key;}
            string action=board.LocalReady?"서명 완료 · 방장이 커튼 레버로 시작합니다":"출연 명단에 다가가 서명하세요";
            if(blocked || room.FocusedInteraction!=null || session.GameState!=GameSessionState.Waiting)
            {guide.HideImmediate();previousGuide=null;}
            else if(previousGuide!=action){guide.ShowKey("F",action,GuideKeyWidth.Normal);previousGuide=action;}
        }
        void Copy(string value)
        {
            if(session==null || value!=session.JoinKey || string.IsNullOrEmpty(value) || InputGate.IsBlocked) return;
            GUIUtility.systemCopyBuffer=value;
            toast.Show("접속 정보를 복사했습니다",ToastState.Success,2);
        }

        void CreateAppearanceControls()
        {
            var canvasObject=new GameObject("Appearance Selection",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=34;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            appearancePanel=new GameObject("Appearance Panel",typeof(RectTransform),typeof(UnityEngine.UI.Image));appearancePanel.transform.SetParent(canvasObject.transform,false);
            var rect=(RectTransform)appearancePanel.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-32,-120);rect.sizeDelta=new Vector2(350,128);
            appearancePanel.GetComponent<UnityEngine.UI.Image>().color=new Color(.08f,.07f,.06f,.9f);
            var font=info.GetComponentInChildren<TMP_Text>(true)?.font;
            TMP_Text Label(string name,string text,Vector2 pos,Vector2 size,int fontsize)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(appearancePanel.transform,false);
                var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;
                var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.fontSize=fontsize;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.text=text;t.raycastTarget=false;return t;
            }
            appearanceLabel=Label("Selected", "의상 선택",new Vector2(0,32),new Vector2(320,34),23);
            void ButtonAt(string name,string text,float x,System.Action action)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(appearancePanel.transform,false);
                var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,-23);r.sizeDelta=new Vector2(150,48);
                go.GetComponent<UnityEngine.UI.Image>().color=new Color(.34f,.25f,.16f,1);go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>{if(!InputGate.IsBlocked)action();});
                var child=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));child.transform.SetParent(go.transform,false);var cr=(RectTransform)child.transform;cr.anchorMin=Vector2.zero;cr.anchorMax=Vector2.one;cr.offsetMin=cr.offsetMax=Vector2.zero;
                var t=child.GetComponent<TextMeshProUGUI>();t.font=font;t.fontSize=21;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.text=text;t.raycastTarget=false;
            }
            ButtonAt("Model","모델 변경",-82,()=>ChangeAppearance(true));
            ButtonAt("Outfit","의상 변경",82,()=>ChangeAppearance(false));
            appearancePanel.SetActive(false);
        }

        static void ChangeAppearance(bool model)
        {
            var p=NetworkPlayer.Local;var catalog=CurtainCall.CharacterArt.PlayerAppearanceCatalog.Current;
            if(p==null||catalog==null||!catalog.Valid(p.AppearanceModel,p.AppearanceOutfit))return;
            if(model)
            {
                for(int i=1;i<=catalog.models.Length;i++){int next=(p.AppearanceModel+i)%catalog.models.Length;if(catalog.Valid(next,p.AppearanceOutfit)){p.RequestAppearance(next,p.AppearanceOutfit);return;}}
            }
            else
            {
                int count=Mathf.Min(4,catalog.outfits.Length);
                for(int i=1;i<=count;i++){int next=(p.AppearanceOutfit+i)%count;if(!NetworkPlayer.All.Any(other=>other!=null&&other!=p&&other.AppearanceOutfit==next)){p.RequestAppearance(p.AppearanceModel,next);return;}}
            }
        }
    }
}
