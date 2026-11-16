using System.Collections;
using CurtainCall.Cameras;
using CurtainCall.Dev;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        uint checkpointRequest, newestCheckpoint;
        Vector3? checkpointRecoveryPending;
        int checkpointRecoveryRestart;

        internal bool TryGetDevCheckpointFeet(out Vector3 feet)
        {
            // Use the newest replicated owner position on the host, without interpolating across gaps.
            feet = IsOwner || networkTransform == null
                ? transform.position : networkTransform.GetSpaceRelativePosition(true);
            if (!IsOwner && networkTransform != null && networkTransform.InLocalSpace && transform.parent != null)
                feet = transform.parent.TransformPoint(feet);
            if (checkpointRecoveryPending is not { } target) return true;
            if (checkpointRecoveryRestart == RestartVersion && (feet - target).sqrMagnitude > 1f) return false;
            checkpointRecoveryPending = null;
            return true;
        }

        internal void ServerRecoverAtDevCheckpoint(Vector3 feet, Quaternion facing, string scene)
        {
            if (!IsServer || !IsSpawned || !DevMode.Enabled) return;
            ReleaseHoldsForDev();
            checkpointRecoveryPending = feet;
            checkpointRecoveryRestart = RestartVersion;
            ServerRestoreAll();
            RecoverAtDevCheckpointRpc(feet, facing, scene, RestartVersion, reviveCount.Value, ++checkpointRequest);
        }

        void ReleaseHoldsForDev()
        {
            HumanCannonSystem.Current?.ServerReleaseForInspection(OwnerClientId);
            HumanCannonCarry.Held(OwnerClientId)?.ServerReleaseForInspection(OwnerClientId);
            TightropeHangSystem.Current?.ServerReleaseForInspection(OwnerClientId);
            PiggybackSystem.Current?.ServerReleaseForInspection(OwnerClientId);
        }

        /// <summary>호스트: Dev F9 대상 이동. 살아 있는 플레이어를 붙잡힌 장치에서 풀고 소유자 화면에서 옮긴다.</summary>
        internal bool ServerDevTeleport(Vector3 feet, Quaternion facing)
        {
            if (!IsServer || !IsSpawned || !DevMode.Enabled || State != PlayerState.Normal) return false;
            ReleaseHoldsForDev();
            if (IsOwner) return DevCheats.Teleport(this, feet, facing, out _);
            DevTeleportRpc(feet, facing, gameObject.scene.name, RestartVersion, reviveCount.Value);
            return true;
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void DevTeleportRpc(Vector3 feet, Quaternion facing, string scene, int restart, int revive)
        {
            if (!IsOwner || !CurrentFallEpoch(restart, revive) || !SceneManager.GetSceneByName(scene).isLoaded) return;
            DevCheats.Teleport(this, feet, facing, out _);
        }

        /// <summary>호스트: Dev F9 대상의 균형을 소유자 화면에서 중앙으로 되돌린다.</summary>
        internal void ServerDevResetBalance()
        {
            if (!IsServer || !IsSpawned || !DevMode.Enabled) return;
            if (IsOwner) ResetDevBalance();
            else DevResetBalanceRpc();
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void DevResetBalanceRpc() { if (IsOwner) ResetDevBalance(); }

        void ResetDevBalance()
        {
            if (State == PlayerState.Normal && balance != null && balance.IsActive && !balance.HasFallen) balance.ResetBalance();
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void RecoverAtDevCheckpointRpc(Vector3 feet, Quaternion facing, string scene, int restart, int revive, uint request)
        {
            if (!IsOwner || !IsSpawned || request <= newestCheckpoint) return;
            newestCheckpoint = request;
            StartCoroutine(PlaceAfterRevive());

            IEnumerator PlaceAfterRevive()
            {
                // The targeted RPC can arrive before replicated state and recovery counters.
                double deadline = Time.unscaledTimeAsDouble + 3;
                while (IsSpawned && IsOwner && request == newestCheckpoint && Time.unscaledTimeAsDouble < deadline
                    && SceneManager.GetSceneByName(scene).isLoaded && RestartVersion <= restart && reviveCount.Value <= revive)
                {
                    // A waiting-room capture pose owns the mover until the pursuit snapshot removes it.
                    if (CurrentFallEpoch(restart, revive) && State == PlayerState.Normal
                        && (CurtainCall.WaitingRoom.Pursuit.PursuitDirector.Current == null || !CurtainCall.WaitingRoom.Pursuit.PursuitDirector.IsCaptured(gameObject))
                        && GetComponent<CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose>() == null)
                    {
                        GetComponent<HumanCannonPilot>()?.ClearMotionForInspection();
                        ResetFallTracking();
                        if (balance != null) { balance.ResetBalance(); balance.SetBalanceActive(false); }
                        ragdoll?.Restore();
                        mover.Simulated = true;
                        mover.SetControlEnabled(true);
                        mover.Relocate(feet, facing);
                        CameraViewDirector.Current?.SetLookRotation(facing);
                        yield break;
                    }
                    yield return null;
                }
            }
        }
    }
}
