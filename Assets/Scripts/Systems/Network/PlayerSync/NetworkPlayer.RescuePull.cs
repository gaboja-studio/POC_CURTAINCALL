using CurtainCall.MainStage.CheerPoints;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        readonly NetworkVariable<ulong> rescueCaster=new(ulong.MaxValue);
        double rescueDeadline;
        uint rescueSequence;
        int rescueEpoch,rescueRevive;
        bool localRescuePull,previousRescueController;
        double localRescueBegan;
        float localRescueSeconds;
        Vector3 rescueFrom,rescueDestination;
        Quaternion rescueRotation;
        PlayerController rescueController;
        public bool IsBeingRescued => rescueCaster.Value!=ulong.MaxValue || localRescuePull;

        void StartServerRescuePull(NetworkPlayer caster,Vector3 destination)
        {
            rescueCaster.Value=caster.NetworkObjectId;
            float seconds=Mathf.Max(.2f,GameSettings.PerformanceEquipment.rescuePullSeconds);
            rescueDeadline=EquipmentNow+seconds+2;
            rescueEpoch=RestartVersion;rescueRevive=reviveCount.Value;
            balloonUntil.Value=0;stickyUntil.Value=0;
            ReleaseHoldsForDev();
            ResetFallTracking();PerformanceCheerRuntime.InvalidateTraversal(OwnerClientId);
            BeginRescuePullRpc(destination,caster.transform.rotation,seconds,++rescueSequence,rescueEpoch,rescueRevive);
            caster.RescuePullVisualRpc(NetworkObjectId,destination,seconds);
        }

        [Rpc(SendTo.Owner,InvokePermission=RpcInvokePermission.Server)]
        void BeginRescuePullRpc(Vector3 destination,Quaternion rotation,float seconds,uint sequence,int restart,int revive)
        {
            if(!CurrentFallEpoch(restart,revive) || State!=PlayerState.Normal) return;
            EndLocalRescuePull();
            EquipmentPilot?.ClearMotionForInspection();TightropeHangSystem.Current?.ReleaseLocalIntoFall(this);
            rescueSequence=sequence;rescueEpoch=restart;rescueRevive=revive;
            rescueFrom=transform.position;rescueDestination=destination;rescueRotation=rotation;
            localRescueSeconds=seconds;localRescueBegan=Time.unscaledTimeAsDouble;
            rescueController=GetComponent<PlayerController>();previousRescueController=rescueController && rescueController.enabled;
            if(rescueController)rescueController.enabled=false;
            mover.ClearPendingInput();mover.ResetExternalMotion(true);mover.Simulated=false;mover.SetControlEnabled(false);
            if(balance)balance.IsSuspended=true;
            localRescuePull=true;ResetFallTracking();
        }

        void UpdateRescuePull()
        {
            if(IsServer && rescueCaster.Value!=ulong.MaxValue)
            {
                bool casterValid=NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(rescueCaster.Value,out var net)
                    && net.TryGetComponent<NetworkPlayer>(out var caster) && caster.State==PlayerState.Normal;
                if(!casterValid || State!=PlayerState.Normal || !CurrentFallEpoch(rescueEpoch,rescueRevive) || EquipmentNow>rescueDeadline)
                { rescueCaster.Value=ulong.MaxValue;CancelRescuePullRpc(rescueSequence); }
            }
            if(!IsOwner || !localRescuePull)return;
            if(!CurrentFallEpoch(rescueEpoch,rescueRevive) || State!=PlayerState.Normal || !mover.Body.enabled)
            { EndLocalRescuePull();return; }
            float t=Mathf.Clamp01((float)(Time.unscaledTimeAsDouble-localRescueBegan)/localRescueSeconds);
            Vector3 position=Vector3.Lerp(rescueFrom,rescueDestination,Mathf.SmoothStep(0,1,t));
            // Continuous owner-authoritative movement: NetworkTransform sends intermediate poses to every observer.
            // Never Relocate/Teleport here; CharacterController keeps newly appearing obstacles solid.
            mover.Body.Move(position-transform.position);
            transform.rotation=Quaternion.Slerp(transform.rotation,rescueRotation,Time.unscaledDeltaTime*8);
            if(Vector3.Distance(position,transform.position)>.3f || t>=1)
            {
                Vector3 feet=transform.position;uint sequence=rescueSequence;
                EndLocalRescuePull();FinishRescuePullRpc(sequence,rescueEpoch,rescueRevive,feet);
            }
        }

        void EndLocalRescuePull()
        {
            if(!localRescuePull)return;
            localRescuePull=false;
            if(rescueController)rescueController.enabled=previousRescueController;
            mover.Simulated=State!=PlayerState.EscapeCaptured;
            if(State==PlayerState.Normal)
            {
                // Releasing a hang/flight hands control back to ordinary movement.
                mover.SetControlEnabled(true);
                bool grounded=WalkableSupport.IsSafeLanding(mover,transform.position);
                mover.ResetExternalMotion(!grounded);ResetFallTracking();
                if(balance){balance.IsSuspended=false;balance.ResetBalance();}
                if(grounded)ResetEquipmentAirCycle();
            }
            else if(balance && State!=PlayerState.EscapeCaptured)balance.IsSuspended=false;
        }

        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        void FinishRescuePullRpc(uint sequence,int restart,int revive,Vector3 feet)
        {
            if(sequence!=rescueSequence || !CurrentFallEpoch(restart,revive) || !FiniteFallPoint(feet))return;
            rescueCaster.Value=ulong.MaxValue;WaitForFallTeleport(feet);PerformanceCheerRuntime.InvalidateTraversal(OwnerClientId);
        }
        [Rpc(SendTo.Owner,InvokePermission=RpcInvokePermission.Server)]
        void CancelRescuePullRpc(uint sequence) {if(sequence==rescueSequence)EndLocalRescuePull();}

        [Rpc(SendTo.Everyone,InvokePermission=RpcInvokePermission.Server)]
        void RescuePullVisualRpc(ulong targetId,Vector3 destination,float seconds)
        {
            if(NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId,out var target))
                EquipmentUseVisual.ShowRescue(this,target.transform,destination,seconds);
        }
    }
}
