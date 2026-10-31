using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.SpaceLink;
using CurtainCall.WaitingRoom;
using TMPro;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Waiting-room assembly only. The stairs and camera switch belong to the escape scene.</summary>
    public sealed class EscapeEntrance : MonoBehaviour
    {
        [SerializeField] BoxCollider assemblyArea;
        [SerializeField] GameObject forwardBarrier, returnBarrier;
        [SerializeField] TMP_Text worldLabel, hudLabel;
        [SerializeField] GameObject hudRoot;
        public static EscapeEntrance Current { get; private set; }
        public IReadOnlyList<ulong> Participants => EscapeSceneTransfer.EnsureExists().Participants;
        public bool Departed => snapshot.stage == (int)EscapeAssemblyState.Stage.Departed;
        public BoxCollider AssemblyArea => assemblyArea;
        // The destination owns this trigger after the loading transition.
        public BoxCollider FirstPersonArea => EscapeSceneArrival.Current != null ? EscapeSceneArrival.Current.FirstPersonArea : null;
        readonly List<ulong> candidates = new();
        readonly EscapeAssemblyState assembly = new();
        ShopChannel channel;
        CampaignRun run;
        float nextSend;
        int generation = -1, day, performance;
        [Serializable] sealed class Snapshot
        {
            public int generation, day, performance, stage, occupants, eligible, required;
        }
        Snapshot snapshot = new();

        void Awake()
        {
            Current = this;
            EscapeSceneTransfer.EnsureExists().SetSource(assemblyArea != null ? assemblyArea.transform : transform);
            channel = new ShopChannel("CurtainCall.Escape.Assembly", (_, _) => Publish(), Receive, _ => { },
                () => { assembly.Reset(); snapshot = new(); }, () => channel.Request("sync"));
        }
        void Update()
        {
            channel.Update();
            var campaign = CampaignRun.Instance;
            if (run != campaign)
            {
                if (run != null) run.StepChanged -= OnStep;
                run = campaign;
                if (run != null) { run.StepChanged += OnStep; OnStep(run.State); }
            }
            if (run == null) return;
            if (run.HasAuthority && run.State.Phase == CampaignPhase.RestTime)
            {
                bool doorOpen = ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(StructureAKeyRoute.EscapeDoor);
                int occupants = 0;
                candidates.Clear();
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || !channel.IsConnected(player.OwnerClientId)
                        || !Contains(assemblyArea, player.transform.position)) continue;
                    occupants++;
                    if (player.State == PlayerState.Normal) candidates.Add(player.OwnerClientId);
                }
                assembly.Tick(occupants, doorOpen ? candidates.Count : 0, channel.Session.PlayerCount);
                if (doorOpen && assembly.CanDepart)
                {
                    var transfer = EscapeSceneTransfer.EnsureExists();
                    if (transfer.Begin(candidates, run.State.Generation))
                    {
                        // Seal the rear exit before StartEscape notifies the scene-transition coordinator.
                        if (returnBarrier != null) returnBarrier.SetActive(true);
                        if (run.StartEscape()) assembly.Depart();
                        else transfer.Cancel();
                    }
                }
                snapshot = new Snapshot { generation = run.State.Generation, day = run.State.Day,
                    performance = run.State.PerformanceIndex, stage = (int)assembly.Status,
                    occupants = assembly.Occupants, eligible = assembly.Eligible, required = channel.Session.PlayerCount };
                if (Time.unscaledTime >= nextSend || Departed) { Publish(); nextSend = Time.unscaledTime + .25f; }
            }
            RefreshPresentation();
        }
        void OnStep(CampaignSnapshot state)
        {
            if (state.Phase != CampaignPhase.Escape && (state.Phase != CampaignPhase.RestTime
                || state.Generation != generation || state.Day != day || state.PerformanceIndex != performance))
            {
                assembly.Reset(); snapshot = new();
                generation = state.Generation; day = state.Day; performance = state.PerformanceIndex;
            }
            RefreshPresentation();
        }
        void Publish() { if (channel.IsHost) channel.SendState(JsonUtility.ToJson(snapshot)); }
        void Receive(string json)
        {
            var value = JsonUtility.FromJson<Snapshot>(json);
            if (value == null || run == null || value.generation != run.State.Generation || value.day != run.State.Day
                || value.performance != run.State.PerformanceIndex || value.stage < 0
                || value.stage > (int)EscapeAssemblyState.Stage.Departed) return;
            snapshot = value;
        }
        void RefreshPresentation()
        {
            if (run == null) return;
            bool escaping = run.State.Phase == CampaignPhase.Escape;
            // This wall never opens in W. The matching A room opens toward its own staircase.
            if (forwardBarrier != null) forwardBarrier.SetActive(true);
            if (returnBarrier != null) returnBarrier.SetActive(escaping);
            var transfer = EscapeSceneTransfer.Current;
            string message = escaping ? (string.IsNullOrEmpty(transfer?.Failure) ? "모두 합류했습니다\n탈출로로 이동 중" : "이동을 다시 준비하고 있습니다")
                : $"탈출 준비 {snapshot.eligible}/{snapshot.required}\n플레이 중인 전원이 모이면 출발";
            if (worldLabel != null) worldLabel.text = message;
            if (hudLabel != null) hudLabel.text = message;
            if (hudRoot != null) hudRoot.SetActive(run.State.Phase == CampaignPhase.RestTime && snapshot.occupants > 0);
        }
        public static bool Contains(BoxCollider area, Vector3 feet)
        {
            if (area == null || !area.gameObject.activeInHierarchy) return false;
            var local = area.transform.InverseTransformPoint(feet + Vector3.up * .35f) - area.center;
            var half = area.size * .5f;
            return float.IsFinite(local.x) && float.IsFinite(local.y) && float.IsFinite(local.z)
                && Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
        void OnDestroy()
        {
            channel?.Dispose();
            if (run != null) run.StepChanged -= OnStep;
            if (Current == this) Current = null;
        }
    }
}
