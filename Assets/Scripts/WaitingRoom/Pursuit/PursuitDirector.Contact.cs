using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitDirector
    {
        const string ContactMessage = "CurtainCall.WaitingRoom.Pursuit.Contact.v4";
        readonly List<PursuitContactHistory> contactHistory = new();
        readonly Dictionary<ulong, uint> contactSequences = new();
        uint contactSequence;
        float contactSendAt;
        public PursuitContactRejection LastContactRejection { get; private set; }
        public int AcceptedContacts { get; private set; }

        public bool RequiresOwnerContact(GameObject player)
        {
            var network = player != null ? player.GetComponent<NetworkPlayer>() : null;
            return network != null && network.IsSpawned && network.OwnerClientId != NetworkManager.ServerClientId;
        }
        void ResetContacts()
        {
            foreach (var history in contactHistory) history.Clear();
            contactSequences.Clear(); contactSequence = 0; contactSendAt = 0;
            LastContactRejection = PursuitContactRejection.None; AcceptedContacts = 0;
        }
        void RecordContactHistory(double stamp)
        {
            while (contactHistory.Count < actors.Count) contactHistory.Add(new PursuitContactHistory());
            for (int i = 0; i < actors.Count; i++) {
                var actor = actors[i];
                // 추격 행동 중이면 막힌 길 끝에서 노려보며 서 있어도(달리기 아님) 닿은 플레이어를 잡는다.
                contactHistory[i].Record(stamp, actor.transform.position, ContactEligible(actor));
            }
        }
        /// <summary>닿은 플레이어를 잡을 수 있는 추격자 상태. 쫓는 대상이 아니어도 추격 중 닿으면 잡는다(2026-11-12 결정).</summary>
        static bool ContactEligible(PursuitActor actor)
            => actor != null && actor.IsActivated && actor.Behaviour == PursuitBehaviour.Chase && actor.Victim == null
                // 오르내리는 동안은 잡지 않는다(플레이어가 반응할 시간). 복제본도 동작 상태로 같은 판정을 한다.
                && !PursuitActor.IsTraversalMotion(actor.State);
        /// <summary>호스트·오프라인 플레이어 포획. 쫓는 대상을 먼저 보고, 포획 범위에 든 다른 플레이어도 잡는다.
        /// 원격 플레이어는 자기 화면의 접촉 보고로만 잡힌다.</summary>
        internal void CaptureInReach(PursuitActor actor, GameObject preferred)
        {
            if (!ContactEligible(actor)) return;
            if (TryHostCapture(actor, preferred)) return;
            foreach (var player in candidates)
                if (player != preferred && TryHostCapture(actor, player)) return;
        }
        bool TryHostCapture(PursuitActor actor, GameObject player)
        {
            if (player == null || !IsAvailable(player) || RequiresOwnerContact(player)
                || !actor.WithinCaptureReach(player.transform.position)
                || !ClearLine(actor.transform.position + Vector3.up, player.transform.position + Vector3.up, player.transform)) return false;
            BeginCapture(actor, player);
            return actor.Victim != null;
        }
        /// <summary>Called after this replica has applied its displayed pose, never from host AI for a remote victim.</summary>
        internal void ReportLocalContact(PursuitActor actor)
        {
            if (run == null || run.HasAuthority || !IsInitialized || !active || messaging == null || manager == null
                || !receivedSnapshot || CaptureSuppressed || !PlayerRecognitionEnabled || Time.unscaledTime < contactSendAt) return;
            var campaign = run.State;
            if (!IsPursuitPhase(campaign.Phase) || campaign.Timer != RestTimerState.Pursuit) return;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsSpawned || !player.IsOwner || !IsAvailable(player.gameObject)
                || !ContactEligible(actor) || !actor.HasFreshContactPose) return;
            if (!PursuitContactRules.Touching(actor.transform.position, player.transform.position, GameSettings.WaitingRoom.Pursuit)
                || !ClearLine(actor.transform.position + Vector3.up, player.transform.position + Vector3.up, player.transform)) return;
            int index = actors.IndexOf(actor);
            if (index < 0) return;
            var contact = new PursuitContact { Generation = generation, Day = day, Performance = performance, Epoch = poseEpoch,
                Actor = index, Sequence = ++contactSequence, PoseTime = actor.ContactPoseTime,
                ActorPosition = actor.transform.position, PlayerPosition = player.transform.position };
            contactSendAt = Time.unscaledTime + .15f;
            using var writer = new FastBufferWriter(PursuitContact.Bytes, Allocator.Temp);
            contact.Write(writer);
            messaging.SendNamedMessage(ContactMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            // No input lock, injury or capture presentation before the authoritative snapshot confirms it.
        }
        void ReceiveContact(ulong sender, FastBufferReader reader)
        {
            if (manager == null || !manager.IsServer || !manager.ConnectedClients.ContainsKey(sender)
                || !readyClients.Contains(sender) || !PursuitContact.Read(reader, out var contact)) return;
            TryCommitContact(sender, contact, Time.realtimeSinceStartupAsDouble);
        }
        bool TryCommitContact(ulong sender, PursuitContact contact, double now)
        {
            LastContactRejection = ValidateContact(sender, contact, now, out var actor, out var player);
            if (LastContactRejection != PursuitContactRejection.None) return false;
            contactSequences[sender] = contact.Sequence;
            CommitCapture(actor, player, contact.PlayerPosition);
            AcceptedContacts++;
            Broadcast();
            return true;
        }
        PursuitContactRejection ValidateContact(ulong sender, PursuitContact contact, double now, out PursuitActor actor, out GameObject player)
        {
            actor = null; player = null;
            if (run == null || !run.HasAuthority || !IsInitialized || !active || !IsPursuitPhase(run.State.Phase)
                || run.State.Timer != RestTimerState.Pursuit || CaptureSuppressed || !PlayerRecognitionEnabled) return PursuitContactRejection.State;
            if (contact.Generation != generation || contact.Day != day || contact.Performance != performance || contact.Epoch != poseEpoch)
                return PursuitContactRejection.Epoch;
            var campaign = run.State;
            if (contact.Generation != campaign.Generation || contact.Day != campaign.Day || contact.Performance != campaign.PerformanceIndex)
                return PursuitContactRejection.Epoch;
            foreach (var candidate in NetworkPlayer.All)
                if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == sender) { player = candidate.gameObject; break; }
            if (player == null || !IsAvailable(player)) return PursuitContactRejection.Owner;
            if (contactSequences.TryGetValue(sender, out uint previous) && !PursuitContactRules.Newer(contact.Sequence, previous))
                return PursuitContactRejection.Sequence;
            if (contact.Actor < 0 || contact.Actor >= actors.Count || contact.Actor >= contactHistory.Count) return PursuitContactRejection.State;
            actor = actors[contact.Actor];
            if (!ContactEligible(actor)) return PursuitContactRejection.State;
            var rules = GameSettings.WaitingRoom.Pursuit;
            var result = PursuitContactRules.Validate(contact, contactHistory[contact.Actor], now,
                player.transform.position, rules.CaptureRadius, rules.CaptureBelow, rules.CaptureAbove);
            if (result != PursuitContactRejection.None) return result;
            if (!ClearLine(contact.ActorPosition + Vector3.up, contact.PlayerPosition + Vector3.up, player.transform))
                return PursuitContactRejection.Occluded;
            return PursuitContactRejection.None;
        }
    }
}
