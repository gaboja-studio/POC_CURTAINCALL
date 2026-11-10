using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public sealed partial class WaitingRoomCannonRehearsal
    {
        enum Request : byte { Start, Vote, Placed, Skip }
        const string RequestName = "CurtainCall.CannonRehearsal.Request";
        ScheduleChannel channel;
        CustomMessagingManager requests;
        bool requestHost;

        void AwakeNetwork() => channel = new ScheduleChannel("CurtainCall.CannonRehearsal", Receive,
            id => channel.SendState(JsonUtility.ToJson(shared), id), ResetConnection);

        void ResetConnection()
        {
            if (Host) return;
            shared = new Shared();
            Cleanup();
            // The next room starts at round 1 again. Old local acknowledgements must not skip its return.
            endingRound = returnedRound = notifiedRound = -1;
            returnPoseRound = -1;
            guideRound = -1;
            actionGuides.Clear(); pendingGuides.Clear();
            returnAt = 0d;
            prompt = pendingUnlockPrompt = returnToSchedule = discoveryPrompt = false;
            promptBlock?.SetOpen(false);
            votePanel?.Hide();
            fader?.Cancel();
            fallen.Clear();
            lastCarrier = CurtainCall.HumanCannon.CannonSeatState.Nobody;
            local = null; previousState = default;
            room = null;
            nextPlacement = nextPublish = 0f;
        }

        void DestroyNetwork()
        {
            channel?.Dispose();
            if (requestHost) requests?.UnregisterNamedMessageHandler(RequestName);
        }

        void TickNetwork()
        {
            channel.Update();
            var next = channel.Session?.NetworkManager is { IsListening: true } net ? net.CustomMessagingManager : null;
            bool host = channel.IsHost;
            if (next == requests && requestHost == host) return;
            if (requestHost) requests?.UnregisterNamedMessageHandler(RequestName);
            requests = next; requestHost = host;
            if (requests != null && host) requests.RegisterNamedMessageHandler(RequestName, ReceiveRequest);
        }

        void Publish()
        {
            if (!Host) return;
            shared.version++;
            channel.SendState(JsonUtility.ToJson(shared));
        }

        void Receive(string json)
        {
            var next = JsonUtility.FromJson<Shared>(json);
            if (next == null || next.version < shared.version || next.round < 0 || next.participants == null
                || next.placed == null || next.middle == null || next.bounced == null || next.arrived == null || next.votes == null || next.skipVotes == null) return;
            shared = next;
        }

        /// <summary>All owners may interact with the unlocked prop; the host validates position before starting.</summary>
        public static void RequestPractice()
        {
            var r = instance;
            if (r == null || !r.CanStart) return;
            if (r.Host) { StartPractice(); return; }
            r.Send(Request.Start);
        }

        public static void VoteToAbandon(bool yes)
        {
            var r = instance;
            if (r == null || !r.shared.active || r.shared.ending || NetworkPlayer.Local == null) return;
            if (r.Host) r.SetVote(NetworkPlayer.Local.OwnerClientId, yes);
            else r.Send(Request.Vote, yes);
        }

        /// <summary>과반수 넘기기는 이수 처리한다(사용자 요청).</summary>
        public static void VoteToSkip(bool yes)
        {
            var r = instance;
            if (r == null || !r.shared.active || r.shared.ending || NetworkPlayer.Local == null) return;
            if (r.Host) r.SetVote(NetworkPlayer.Local.OwnerClientId, yes, true);
            else r.Send(Request.Skip, yes);
        }

        void SetVote(ulong actor, bool yes, bool skip = false)
        {
            if (!shared.active || shared.ending || !shared.participants.Contains(actor)) return;
            var votes = skip ? shared.skipVotes : shared.votes;
            var opposite = skip ? shared.votes : shared.skipVotes;
            bool changed = yes ? !votes.Contains(actor) : votes.Contains(actor);
            if (yes && opposite.Remove(actor))
            {
                RecordAction("VOTE_CHANGED", actor, skip ? "ABANDON" : "SKIP", false);
                changed = true;
            }
            if (!changed) return;
            if (yes) { if (!votes.Contains(actor)) votes.Add(actor); }
            else votes.Remove(actor);
            RecordAction("VOTE_CHANGED", actor, skip ? "SKIP" : "ABANDON", yes);
            Publish();
        }

        void Send(Request operation, bool yes = false, Vector3 position = default)
        {
            if (requests == null || channel.Session == null) return;
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe((byte)operation); writer.WriteValueSafe(shared.round);
            if (operation is Request.Vote or Request.Skip) writer.WriteValueSafe(yes);
            if (operation == Request.Placed) writer.WriteValueSafe(position);
            requests.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void SendPlacement(Vector3 position)
        {
            if (Host) AcceptPlacement(NetworkPlayer.Local.OwnerClientId, shared.round, position);
            else Send(Request.Placed, position: position);
        }

        void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (!Host || reader.Length - reader.Position < 5 || !reader.TryBeginRead(5)) return;
            reader.ReadValueSafe(out byte operation); reader.ReadValueSafe(out int round);
            var player = NetworkPlayer.All.FirstOrDefault(p => p != null && p.IsSpawned && p.OwnerClientId == sender && p.State != PlayerState.Retired);
            if (player == null) return;
            if ((Request)operation == Request.Start)
            {
                if (CanStart && FindRoom() && RehearsalCannonPracticeStation.IsNear(player.transform.position)) StartPractice();
            }
            else if (round == shared.round && (Request)operation == Request.Vote && reader.TryBeginRead(1))
            { reader.ReadValueSafe(out bool yes); SetVote(sender, yes); }
            else if (round == shared.round && (Request)operation == Request.Skip && reader.TryBeginRead(1))
            { reader.ReadValueSafe(out bool yes); SetVote(sender, yes, true); }
            else if (round == shared.round && (Request)operation == Request.Placed && reader.TryBeginRead(12))
            { reader.ReadValueSafe(out Vector3 position); AcceptPlacement(sender, round, position); }
        }

        void AcceptPlacement(ulong actor, int round, Vector3 position)
        {
            if (!Host || !shared.active || shared.ending || course == null || round != shared.round || shared.placed.Contains(actor)) return;
            var player = NetworkPlayer.All.FirstOrDefault(p => p != null && p.IsSpawned && p.OwnerClientId == actor && p.State == PlayerState.Normal);
            var floor = Checkpoint(actor) > 0 ? course.MiddleFloor : course.StartFloor;
            if (player == null || !RehearsalCannonCourse.Contains(floor, position)
                || !RehearsalCannonCourse.Contains(floor, player.transform.position) || Vector3.Distance(position, player.transform.position) > .75f) return;
            shared.placed.Add(actor); RecordAction("PLACED", actor, Checkpoint(actor) > 0 ? "MIDDLE_PLATFORM" : "START_PLATFORM"); Publish();
        }
    }
}
