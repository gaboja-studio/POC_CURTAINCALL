using System;
using System.Collections;
using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeDevelopmentSession
    {
        static EscapeDevelopmentSession entranceSession;
        public static bool AwaitingEntrance => entranceSession != null && !entranceSession.Ready;
        ShopChannel entranceChannel;

        // Each owner resolves its slot against this copy of A, while all movement is held.
        // A fresh challenge keeps acknowledgements from a previous load out of this barrier.
        IEnumerator PrepareEntrance()
        {
            string challenge = session.IsHost ? Guid.NewGuid().ToString("N") : null;
            var placed = new HashSet<ulong>();
            bool released = false;
            bool localPlaced = false;
            var slots = GetComponent<PlayerSpawnPoints>();
            Status = "탈출로 로딩 · 전원 시작 발판 확인 중";
            entranceChannel = new ShopChannel("CurtainCall.Escape.StartHall",
                (id, request) =>
                {
                    if (request == "sync") entranceChannel.SendState(challenge, id);
                    else if (request == challenge)
                    {
                        placed.Add(id);
                        if (released) entranceChannel.SendState("go/" + challenge, id);
                    }
                },
                state =>
                {
                    if (state.StartsWith("go/", StringComparison.Ordinal))
                        released = localPlaced && challenge != null && state == "go/" + challenge;
                    else if (Guid.TryParseExact(state, "N", out _)) challenge = state;
                },
                _ => { }, () => { }, () => entranceChannel.Request("sync"));

            float retry = 0f;
            var pollingInterval = new WaitForSecondsRealtime(.1f);
            while (!released)
            {
                entranceChannel.Update();
                var local = NetworkPlayer.Local;
                if (!localPlaced && local != null && local.IsSpawned && gameObject.scene.isLoaded
                    && slots != null && slots.TryGetSlotPose(local.Slot, out var position, out var rotation)
                    && EscapeCampaignContent.Current != null && EscapeCampaignContent.Current.IsActive && EscapeCampaignContent.Current.IsSceneryReady
                    && HasArrivalFloor(gameObject.scene, position) && local.TryGetComponent(out PlayerMover mover))
                {
                    mover.SetStartPose(position, rotation);
                    CameraViewDirector.Current?.SetLookRotation(rotation);
                    localPlaced = true;
                }
                if (localPlaced && entranceChannel.IsHost)
                {
                    placed.Add(local.OwnerClientId);
                    released = true;
                    foreach (ulong id in session.NetworkManager.ConnectedClientsIds)
                        if (!placed.Contains(id)) { released = false; break; }
                    if (released) entranceChannel.SendState("go/" + challenge);
                }
                else if (localPlaced && challenge != null && Time.unscaledTime >= retry)
                {
                    entranceChannel.Request(challenge);
                    retry = Time.unscaledTime + .5f;
                }
                if (!released) yield return pollingInterval;
            }
            Ready = true;
            InputGate.SetBlocking(this, false);
        }

        internal static bool HasArrivalFloor(Scene destination, Vector3 feet)
        {
            Physics.SyncTransforms();
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * .15f, Vector3.down, 5f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.gameObject.scene == destination && hit.normal.y >= .7f
                    && hit.collider.GetComponentInParent<NetworkPlayer>() == null) return true;
            return false;
        }
    }
}
