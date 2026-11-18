using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Host-owned first-claim collection. Reuses reward replication and a reliable late-join snapshot.</summary>
    public sealed class PerformanceCheerRuntime : MonoBehaviour
    {
        [Serializable] sealed class Snapshot { public string attempt; public string[] taken; public int style; }
        readonly HashSet<string> taken = new();
        readonly Dictionary<ulong,(Vector3 position,int restart)> previous = new();
        readonly Dictionary<ulong,double> suppressedUntil = new();
        static PerformanceCheerRuntime current;
        public static void InvalidateTraversal(ulong player)
        {
            if(!current) return;
            current.previous.Remove(player);
            current.suppressedUntil[player]=Time.unscaledTimeAsDouble+.3;
        }
        ShopChannel channel;
        string attempt;
        CheerPointStyle style;
        PerformanceRewards Rewards => PerformanceRewards.Instance;
        bool Active => Rewards != null && Rewards.State?.IsOpen == true;
        void Awake() { current=this; channel = new ShopChannel("CurtainCall.CheerPoints", (sender,_) => Publish(sender), Read, _=>{}, Reset, ()=>channel.Request("sync")); }
        void Reset() { attempt=null; taken.Clear(); previous.Clear(); }
        void OnDestroy() { channel?.Dispose(); if(current==this) current=null; }
        void Update()
        {
            channel.Update();
            if (Rewards != null && Rewards.HasAuthority)
            {
                string next = Active ? Rewards.State.Performance.Id : null;
                var nextStyle=GameSettings.PerformanceEquipment.cheerStyle;
                if (attempt!=next || style!=nextStyle)
                {
                    if(attempt!=next) { taken.Clear(); previous.Clear(); }
                    attempt=next; style=nextStyle; Publish();
                }
                if(Active) Collect(); else previous.Clear();
            }
            foreach(var point in PerformanceCheerPoint.All)
                if(point) point.Present(Active && point.trickId==Rewards.State.Performance.TrickId && attempt==Rewards.State.Performance.Id,
                    taken.Contains(point.pointId),style);
        }
        void Collect()
        {
            foreach(var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned) continue;
                if(suppressedUntil.TryGetValue(player.OwnerClientId,out var until) && Time.unscaledTimeAsDouble<until) continue;
                var participant=Rewards.State.Segments.FirstOrDefault(p=>p.ClientId==player.OwnerClientId);
                if (player.State!=PlayerState.Normal || participant==null || participant.Dead || !participant.Connected || PerformanceRewards.IsFinishedSpectator(player))
                { previous.Remove(player.OwnerClientId); continue; }
                var body=player.GetComponent<CharacterController>();
                Vector3 center=body ? player.transform.TransformPoint(body.center) : player.transform.position+Vector3.up*.75f;
                if(previous.TryGetValue(player.OwnerClientId,out var last) && last.restart==player.RestartVersion && Vector3.Distance(last.position,center)<30f)
                    foreach(var point in PerformanceCheerPoint.All)
                    {
                        if(!point || point.trickId!=Rewards.State.Performance.TrickId || string.IsNullOrWhiteSpace(point.pointId) || taken.Contains(point.pointId) || !point.Crossed(last.position,center)) continue;
                        // First claimant owns this point even if their personal applause is already capped.
                        taken.Add(point.pointId);
                        Rewards.AddAudienceReaction(participant.ParticipantId,GameSettings.PerformanceEquipment.cheerAmount);
                        Publish();
                    }
                previous[player.OwnerClientId]=(center,player.RestartVersion);
            }
        }
        void Publish(ulong? target=null)
        {
            if(Rewards==null || !Rewards.HasAuthority) return;
            channel.SendState(JsonUtility.ToJson(new Snapshot { attempt=attempt, taken=taken.ToArray(),style=(int)style }),target);
        }
        void Read(string json)
        {
            if(Rewards != null && Rewards.HasAuthority) return;
            var data=JsonUtility.FromJson<Snapshot>(json);
            if(data==null || data.taken==null || data.taken.Length>512 || data.style<0 || data.style>2) return;
            attempt=data.attempt; style=(CheerPointStyle)data.style; taken.Clear();
            foreach(var id in data.taken) if(!string.IsNullOrEmpty(id)) taken.Add(id);
        }
    }
}
