using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.WaitingRoom;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Local screen-space targeting aid. Never spawned or replicated as a NetworkObject.</summary>
    public sealed class RescueTargetIndicator : MonoBehaviour
    {
        NetworkPlayer viewer;
        RescueTargetMarkerView view;
        public NetworkPlayer VisibleTarget { get; private set; }
        void Awake() => viewer=GetComponent<NetworkPlayer>();
        void LateUpdate()
        {
            VisibleTarget=viewer && viewer.IsOwner && viewer.IsSpawned ? viewer.RescuePreviewTarget : null;
            var camera=Camera.main;
            if(!VisibleTarget || !camera) {if(view)view.Hide();return;}
            if(!view)
            {
                var prefab=GameSettings.PerformanceEquipment.rescueTargetVisual;
                if(!prefab || !prefab.GetComponent<RescueTargetMarkerView>())return;
                view=Instantiate(prefab,transform,false).GetComponent<RescueTargetMarkerView>();
            }
            var body=VisibleTarget.GetComponent<CharacterController>();
            var point=VisibleTarget.transform.position+Vector3.up*((body ? body.height : 1.5f)+.18f);
            var screen=camera.WorldToScreenPoint(point);
            var held=PropCarry.Instance.Held(viewer.OwnerClientId);
            int slot=held.Count>0 && held[0].ItemId==ItemUses.RescueGloves ? 0 : 1;
            var reader=viewer.GetComponent<PlayerInputReader>();
            string key=reader ? reader.BindingLabel(slot==0 ? "UseSlotOne" : "UseSlotTwo",slot==0 ? "Q" : "E") : slot==0 ? "Q" : "E";
            view.Show(VisibleTarget,screen,key,GameSettings.PerformanceEquipment.rescueTargetOpacity);
        }
        void OnDisable() {VisibleTarget=null;if(view)view.Hide();}
        void OnDestroy() {if(view)Destroy(view.gameObject);}
    }
}
