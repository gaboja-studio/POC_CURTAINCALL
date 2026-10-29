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

        void Start()
        {
            session=NetworkSessionManager.EnsureExists();
            campaign=CampaignRun.EnsureExists();
            info.CopyRequested+=Copy;
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
            if(!visible) { if(wasVisible) OnDisable(); return; }
            wasVisible=true;
            var host=NetworkPlayer.All.FirstOrDefault(p=>p!=null && p.OwnerClientId==NetworkManager.ServerClientId);
            info.SetRoomInfo(session.JoinKey,host==null?null:$"단원 {host.Slot+1}",session.PlayerCount,session.RequiredPlayers);
            info.SetControlsEnabled(!InputGate.IsBlocked);
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
    }
}
