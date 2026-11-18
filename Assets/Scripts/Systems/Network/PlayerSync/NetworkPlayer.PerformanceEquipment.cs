using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        uint equipmentRequest, equipmentLastRequest, equipmentPending;
        int equipmentPendingUid, equipmentEpoch=-1, balloonSlot=-1;
        string equipmentPendingKind;
        double equipmentPendingUntil;
        bool extraJumpUsed, balloonUsed;
        object stickyHold;
        readonly NetworkVariable<double> balloonUntil = new();
        readonly NetworkVariable<double> stickyStart = new(), stickyUntil = new();
        readonly NetworkVariable<bool> stickySpent = new();
        double EquipmentNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.unscaledTimeAsDouble;
        public bool BalloonEquipmentActive => State==PlayerState.Normal && EquipmentNow < balloonUntil.Value;
        public int ActiveBalloonSlot => BalloonEquipmentActive ? balloonSlot : -1;
        public bool StickyEquipmentActive => State==PlayerState.Normal && EquipmentNow < stickyUntil.Value;
        public void ResetEquipmentAirCycle()
        {
            extraJumpUsed=balloonUsed=false;
            if(IsOwner && IsSpawned && BalloonEquipmentActive) StopBalloonEquipmentRpc();
        }
        public bool CanUseEquipmentSlot(int slot)
        {
            if(!IsOwner || !IsSpawned || IsBeingRescued || State!=PlayerState.Normal || InputGate.IsBlocked || equipmentPending!=0) return false;
            if(BalloonEquipmentActive && slot==balloonSlot) return true;
            var carry=PropCarry.Instance; var held=carry?.Held(OwnerClientId);
            if(held==null || slot<0 || slot>=held.Count) return false;
            var item=held[slot];
            if(EquipmentPlaceBlocked(item.ItemId) || !carry.EquipmentReady(OwnerClientId,item.ItemId) || !CanApplyEquipment(item.ItemId)) return false;
            if(item.ItemId==ItemUses.StickyGloves && IsServer) return CurrentStickyHold() is {} hold && !Equals(hold,stickyHold);
            if(item.ItemId==ItemUses.RescueGloves) return FindEquipmentRescue(out _) != null;
            return true;
        }
        public bool IsEquipmentReserved(int uid) => IsServer && equipmentPendingUid==uid && EquipmentNow<equipmentPendingUntil;
        public double HoldPauseDelta(double elapsed) => State!=PlayerState.Normal ? 0 : Math.Max(0,Math.Min(EquipmentNow,stickyUntil.Value)-Math.Max(EquipmentNow-elapsed,stickyStart.Value));
        bool HoldingEquipmentSurface => TightropeHangSystem.Current?.IsHanging(this)==true || HumanCannonPoleSystem.Current?.HeldBy(OwnerClientId)>=0;
        HumanCannonPilot EquipmentPilot => GetComponent<HumanCannonPilot>();
        bool FreeEquipmentAir => !HoldingEquipmentSurface && (EquipmentPilot != null && EquipmentPilot.IsFlying || mover.IsAirborne && mover.Simulated && mover.ControlEnabled);

        void UpdatePerformanceEquipment()
        {
            if (equipmentEpoch!=RestartVersion || State!=PlayerState.Normal)
            {
                equipmentEpoch=RestartVersion; extraJumpUsed=balloonUsed=false; stickyHold=null;
                equipmentPending=0; equipmentPendingUid=0; balloonSlot=-1;
                if(IsServer) { balloonUntil.Value=0; stickyUntil.Value=0; stickySpent.Value=false; }
            }
            if(IsServer && equipmentPendingUid!=0 && EquipmentNow>=equipmentPendingUntil) equipmentPendingUid=0;
            if(IsServer && stickyHold!=null && !Equals(CurrentStickyHold(),stickyHold)) { stickyUntil.Value=0; stickySpent.Value=false; stickyHold=null; }
            if(!IsOwner) return;
            var pilot=EquipmentPilot;
            bool grounded=!HoldingEquipmentSurface && (pilot==null || !pilot.IsDriving) && !mover.IsAirborne && mover.Body.isGrounded;
            if(grounded) { extraJumpUsed=balloonUsed=false; }
            if(BalloonEquipmentActive && (!FreeEquipmentAir || grounded || State!=PlayerState.Normal)) StopBalloonEquipmentRpc();
            mover.EquipmentGlide=BalloonEquipmentActive;
            if(equipmentPending!=0 && EquipmentNow>=equipmentPendingUntil) equipmentPending=0;
            if(State!=PlayerState.Normal || IsBeingRescued || InputGate.IsBlocked || !mover.enabled || equipmentPending!=0) return;
            var input=GetComponent<PlayerInputReader>();
            if(!input || GetComponent<HumanoidPlayerPresentation>()?.InteractionActionLocked==true) return;
            if(input.Current.UseSlotOnePressed) TryUseEquipmentSlot(0);
            else if(input.Current.UseSlotTwoPressed) TryUseEquipmentSlot(1);
        }

        public bool TryUseEquipmentSlot(int slot)
        {
            if(!IsOwner || !IsSpawned || IsBeingRescued || State!=PlayerState.Normal || InputGate.IsBlocked || slot<0 || slot>1 || equipmentPending!=0) return false;
            if(BalloonEquipmentActive && slot==balloonSlot) { StopBalloonEquipmentRpc(); return true; }
            var carry=PropCarry.Instance;
            var items=carry?.Held(OwnerClientId);
            if(items==null || slot>=items.Count) return false;
            var item=items[slot];
            // 대기실 연습실 밖에서 쓰려 하면 이 플레이어에게만 쓸 수 있는 곳을 안내한다(2026-11-18 사용자).
            if(EquipmentPlaceBlocked(item.ItemId)) { PropCarry.ShowEquipmentPlaceGuide(transform); return false; }
            if(!carry.EquipmentReady(OwnerClientId,item.ItemId) || !CanApplyEquipment(item.ItemId)) return false;
            var rescueTarget=item.ItemId==ItemUses.RescueGloves ? FindEquipmentRescue(out _) : null;
            if(item.ItemId==ItemUses.RescueGloves && !rescueTarget) return false;
            equipmentPending=++equipmentRequest; equipmentPendingUntil=EquipmentNow+5;
            RequestEquipmentRpc(slot,item.Uid,equipmentPending,RestartVersion,reviveCount.Value,rescueTarget ? rescueTarget.NetworkObjectId : ulong.MaxValue);
            return true;
        }

        // 대기실에서는 연습실 구역에서만 공연 기물을 쓴다(연습실은 내구도 소모 없음, PropCarry.UseEquipmentUid). 2026-11-18 사용자.
        bool EquipmentPlaceBlocked(string id) => ItemCatalog.Get(id)?.use==ItemUse.Performance
            && PropCarry.PlaceAt(transform.position)==PropCarry.EquipmentPlace.Blocked;

        bool CanApplyEquipment(string id) => id switch
        {
            ItemUses.SpringShoes => FreeEquipmentAir && !extraJumpUsed,
            ItemUses.BalloonPants => FreeEquipmentAir && !balloonUsed,
            ItemUses.StickyGloves => HoldingEquipmentSurface && !StickyEquipmentActive && !stickySpent.Value,
            ItemUses.RescueGloves => !IsAirborne && !HoldingEquipmentSurface && !HasOutgoingRescue,
            _ => false
        };

        [Rpc(SendTo.Server, InvokePermission=RpcInvokePermission.Owner)]
        void RequestEquipmentRpc(int slot,int uid,uint sequence,int restart,int revive,ulong rescueTargetId)
        {
            bool valid=sequence>equipmentLastRequest && CurrentFallEpoch(restart,revive) && State==PlayerState.Normal && !IsBeingRescued && equipmentPendingUid==0;
            equipmentLastRequest=Math.Max(equipmentLastRequest,sequence);
            var carry=PropCarry.Instance; var items=carry?.Held(OwnerClientId);
            if(!valid || items==null || slot<0 || slot>1 || slot>=items.Count || items[slot].Uid!=uid || !carry.EquipmentReady(OwnerClientId,items[slot].ItemId)
                || EquipmentPlaceBlocked(items[slot].ItemId))
            { EquipmentResultRpc(sequence,false); return; }
            string id=items[slot].ItemId;
            if(id==ItemUses.StickyGloves)
            {
                object hold=CurrentStickyHold();
                if(hold==null || Equals(hold,stickyHold) || !carry.UseEquipmentUid(OwnerClientId,uid)) { EquipmentResultRpc(sequence,false); return; }
                stickyHold=hold; stickySpent.Value=true; stickyStart.Value=EquipmentNow; stickyUntil.Value=EquipmentNow+GameSettings.PerformanceEquipment.stickySeconds;
                EquipmentVisualRpc(id,(float)(stickyUntil.Value-EquipmentNow)); EquipmentResultRpc(sequence,true); return;
            }
            if(id==ItemUses.RescueGloves)
            {
                if(rescueTargetId==ulong.MaxValue) {EquipmentResultRpc(sequence,false);return;}
                var target=FindRescueCandidate(out Vector3 landing,rescueTargetId,false);
                if(!target || !carry.UseEquipmentUid(OwnerClientId,uid)) { EquipmentResultRpc(sequence,false); return; }
                target.StartServerRescuePull(this,landing);
                EquipmentResultRpc(sequence,true); return;
            }
            if(id!=ItemUses.SpringShoes && id!=ItemUses.BalloonPants) { EquipmentResultRpc(sequence,false); return; }
            equipmentPendingUid=uid; equipmentPendingKind=id; equipmentPendingUntil=EquipmentNow+5;
            ApplyAirEquipmentRpc(id,uid,slot,sequence,restart,revive);
        }

        object CurrentStickyHold()
        {
            var hang=TightropeHangSystem.Current?.Hangs.FirstOrDefault(h=>h.Id==OwnerClientId);
            if(hang!=null && !hang.IsPulling && hang.Remaining>0) return hang;
            return HumanCannonPoleSystem.Current?.EquipmentHoldToken(OwnerClientId);
        }

        [Rpc(SendTo.Owner, InvokePermission=RpcInvokePermission.Server)]
        void ApplyAirEquipmentRpc(string id,int uid,int slot,uint sequence,int restart,int revive)
        {
            bool applied=false;
            if(equipmentPending==sequence && CurrentFallEpoch(restart,revive) && State==PlayerState.Normal
                && PropCarry.Instance?.Held(OwnerClientId).Any(i=>i.Uid==uid && i.ItemId==id && i.Uses>0)==true && CanApplyEquipment(id))
            {
                if(id==ItemUses.SpringShoes)
                {
                    var pilot=EquipmentPilot;
                    applied=pilot!=null && pilot.IsFlying ? pilot.TryEquipmentJump(GameSettings.PerformanceEquipment.extraJumpHeight)
                        : mover.TryEquipmentJump(GameSettings.PerformanceEquipment.extraJumpHeight);
                    if(applied) extraJumpUsed=true;
                }
                else { applied=true; balloonUsed=true; balloonSlot=slot; }
            }
            ConfirmAirEquipmentRpc(sequence,restart,revive,applied);
        }

        [Rpc(SendTo.Server, InvokePermission=RpcInvokePermission.Owner)]
        void ConfirmAirEquipmentRpc(uint sequence,int restart,int revive,bool applied)
        {
            if(sequence!=equipmentLastRequest || equipmentPendingUid==0) return;
            string id=equipmentPendingKind; int uid=equipmentPendingUid; equipmentPendingUid=0;
            bool consumed=applied && CurrentFallEpoch(restart,revive) && State==PlayerState.Normal && PropCarry.Instance?.UseEquipmentUid(OwnerClientId,uid)==true;
            if(consumed)
            {
                if(id==ItemUses.BalloonPants) balloonUntil.Value=EquipmentNow+GameSettings.PerformanceEquipment.balloonSeconds;
                EquipmentVisualRpc(id,id==ItemUses.BalloonPants ? GameSettings.PerformanceEquipment.balloonSeconds : .45f);
            }
            EquipmentResultRpc(sequence,consumed);
        }

        [Rpc(SendTo.Owner, InvokePermission=RpcInvokePermission.Server)]
        void EquipmentResultRpc(uint sequence,bool succeeded) { if(equipmentPending==sequence) equipmentPending=0; }
        [Rpc(SendTo.Server, InvokePermission=RpcInvokePermission.Owner)]
        void StopBalloonEquipmentRpc() => balloonUntil.Value=0;
        [Rpc(SendTo.Everyone, InvokePermission=RpcInvokePermission.Server)]
        void EquipmentVisualRpc(string item,float seconds) => CurtainCall.MainStage.CheerPoints.EquipmentUseVisual.Show(this,item,seconds);

    }
}
