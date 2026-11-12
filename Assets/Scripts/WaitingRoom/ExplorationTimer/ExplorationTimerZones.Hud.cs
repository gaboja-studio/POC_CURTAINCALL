using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public sealed partial class ExplorationTimerZones
    {
        static readonly Dictionary<string,string> Labels=new()
        {
            {"Lobby","로비"},{"Area_Lobby","로비"},{"Area_LobbyEast","로비 동쪽"},{"Area_Main","대기실"},{"Area_EastRooms","동쪽 통로"},
            {"Area_Expert","전문가실"},{"ServiceGallery","서비스 통로"},{"WorkshopBranch","작업실 연결 통로"},{"Area_S10_Workshop","작업실"},
            {"Area_S02_Dormitory","숙소"},{"F8_Dorm","숙소"},{"Area_Dormitory","숙소"},{"Area_S03_Shop","상점"},
            {"Area_S04_PropRoom","소품실"},{"Area_PropRoom","소품실"},{"F8_PropRoom","소품실"},{"Area_S05_JunkStorage","폐기물 보관소"},
            {"Area_S08_ElevatorHall","승강기 홀"},{"Area_S09_PracticeRoom","연습실"},{"F8_Practice","연습실"},{"Area_RoundPractice","연습실"},
            {"Area_S11_StageGear","무대 장치 보관실"},{"Area_S12_CargoBay","화물 적재 구역"},{"F8_Cargo","화물 적재 구역"},
            {"Area_P05_PipeZone","배관 구역"},{"PipeLink","배관 연결 통로"},{"F8_PipeCrawl","배관 통로"},{"Area_ST_Stairwell","계단실"},
            {"Area_B1_MachineRoom","기계실"},{"Area_B1_Office","관리실"},{"Area_B1_OfficeLink","관리실 연결 통로"},
            {"Area_B1_ChangingRoom","탈의실"},{"Area_B1_EscapeCorridor","탈출로 통로"},{"Area_B1_EscapeEntrance","탈출로 입구"},
            {"Area_DomePassage_Stage","무대 입구 통로"},{"Area_DomePassage_Practice","연습실 연결 통로"},
            {"Area_DomePassage_Logistics11","무대 장치 보관실 연결 통로"},{"Area_DomePassage_Logistics12","화물 적재 구역 연결 통로"},
            {"Area_DomePassage_PropRoom","소품실 연결 통로"},{"Area_DomePassage_Stairs","계단 연결 통로"},
            {"Area_DomePassage_Workshop","작업실 연결 통로"},{"Area_DomePassage_HiddenPipeCrawl","배관 통로"},
            {"Area_N1_BackCorridor","뒤쪽 복도"},{"Area_N2_OldQuarters","이전 세대 생활 장소"},{"Area_N3_Storage","창고"},{"Area_N4_MachineEntry","기계실 입구"}
        };
        readonly List<BoxCollider> hudZones=new();
        float nextZoneCheck;
        HudRoleCard zoneCard;
        string zoneKey;
        void CollectHudZones()
        {
            hudZones.Clear();
            void Add(Transform root)
            {
                if(!root)return;
                foreach(var box in root.GetComponentsInChildren<BoxCollider>(true))
                    if(Labels.ContainsKey(box.name) && !hudZones.Contains(box))hudZones.Add(box);
            }
            Add(areas ? areas : transform);Add(transform.Find("WaitingRoomArea"));
        }
        public string ZoneAt(Vector3 feet)
        {
            var sample=feet+Vector3.up*.35f;BoxCollider best=null;float smallest=float.PositiveInfinity;
            foreach(var box in hudZones)
            {
                if(!box || !box.enabled || !box.gameObject.activeInHierarchy)continue;
                var local=box.transform.InverseTransformPoint(sample)-box.center;var half=box.size*.5f;
                if(Mathf.Abs(local.x)>half.x || Mathf.Abs(local.y)>half.y || Mathf.Abs(local.z)>half.z)continue;
                var size=Vector3.Scale(box.size,box.transform.lossyScale);
                float volume=Mathf.Abs(size.x*size.y*size.z);
                if(volume>=smallest)continue;
                smallest=volume;best=box;
            }
            return best?best.name:"Area_Main";
        }
        void UpdateZoneHud()
        {
            if(Time.unscaledTime<nextZoneCheck)return;
            nextZoneCheck=Time.unscaledTime+.12f;
            var room=CommonUiSceneLoader.CurrentWaitingRoom;
            var player=NetworkPlayer.Local;
            Transform actor=player && player.IsSpawned && player.State!=PlayerState.Fallen && player.State!=PlayerState.Retired ? player.transform:null;
            if(!actor && (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening))
                foreach(var mover in offlinePlayers)if(mover && mover.isActiveAndEnabled){actor=mover.transform;break;}
            if(!room || room.gameObject.scene!=gameObject.scene || !actor){HideZone();return;}
            var key=ZoneAt(actor.position);
            if(key==zoneKey)return;
            zoneKey=key;
            string text=WaitingRoomText.Get("hud.zone."+key,Labels[key]);
            zoneCard=HudUIManager.Current.ShowZoneName(text,transform);
        }
        void HideZone(){if(zoneKey!=null)HudUIManager.DismissOwned(transform,HudRole.ZoneName);zoneCard=null;zoneKey=null;}
        void OnDisable()=>HideZone();
    }
}
