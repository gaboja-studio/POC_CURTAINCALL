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
            HumanCannonSystem.Current?.ServerReleaseForInspection(OwnerClientId);
            HumanCannonCarry.Held(OwnerClientId)?.ServerReleaseForInspection(OwnerClientId);
            TightropeHangSystem.Current?.ServerReleaseForInspection(OwnerClientId);
            PiggybackSystem.Current?.ServerReleaseForInspection(OwnerClientId);
            checkpointRecoveryPending = feet;
            checkpointRecoveryRestart = RestartVersion;
            ServerRestoreAll();
            RecoverAtDevCheckpointRpc(feet, facing, scene, RestartVersion, reviveCount.Value, ++checkpointRequest);
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
                    if (CurrentFallEpoch(restart, revive) && State == PlayerState.Normal)
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
