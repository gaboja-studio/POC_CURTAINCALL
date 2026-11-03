using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeHangSystem
    {
        enum Request : byte { Hang, Rescue, Cancel, Sync }
        const string RequestMessage = "CurtainCall.Tightrope.Hang.Request";
        const string StateMessage = "CurtainCall.Tightrope.Hang.State";
        const string RescuedMessage = "CurtainCall.Tightrope.Hang.Rescued";
        const int RequestSize = 21; // operation + round + target + lane + distance
        const int EntrySize = 36;   // id + lane + distance + remaining + rescuer + pull + total
        NetworkSessionManager session;
        CustomMessagingManager messaging;

        bool IsHost => session != null && session.IsHost && messaging != null;

        void Connect()
        {
            var next = NetworkSessionManager.Instance;
            if (session != next) { Disconnect(); session = next; }
            var manager = session != null && session.NetworkManager != null && session.NetworkManager.IsListening
                ? session.NetworkManager.CustomMessagingManager : null;
            if (messaging == manager) return;
            Unregister();
            messaging = manager;
            book.Clear();
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestMessage, ReadRequest);
            messaging.RegisterNamedMessageHandler(StateMessage, ReadState);
            messaging.RegisterNamedMessageHandler(RescuedMessage, ReadRescued);
            if (!session.IsHost) Send(Request.Sync);
        }

        struct PendingHang { public int Round, Lane; public float Distance; public double Expires; }
        readonly Dictionary<ulong, PendingHang> pendingHangs = new();
        readonly List<ulong> pendingHangActors = new();
        void Disconnect() { pendingHangs.Clear(); Unregister(); session = null; }

        bool AcceptHang(ulong sender, int lane, float distance)
        {
            var player = Find(sender);
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            if (!course.TryGetRopePoint(player.transform.position, out int observedLane, out float observedDistance)
                || lane != observedLane || Mathf.Abs(distance - observedDistance) > Rules.LaneOccupyDistance) return false;
            ServerTryHang(player, observedDistance, observedLane);
            return true;
        }
        void RetryPendingHangs()
        {
            pendingHangActors.Clear(); pendingHangActors.AddRange(pendingHangs.Keys);
            foreach (var sender in pendingHangActors)
            {
                var item = pendingHangs[sender]; var player = Find(sender);
                if (!Active || item.Round != Round || Now > item.Expires || player == null || !player.IsSpawned
                    || player.State != PlayerState.Normal || book.IsHanging(sender) || AcceptHang(sender, item.Lane, item.Distance))
                    pendingHangs.Remove(sender);
            }
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(RescuedMessage);
            messaging = null;
        }

        void Send(Request request, ulong target = 0, int lane = -1, float distance = 0f)
        {
            if (session == null || messaging == null) return;
            if (session.IsHost) { ServerHandle(session.NetworkManager.LocalClientId, request, target, lane, distance); return; }
            using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
            writer.WriteValueSafe((byte)request);
            writer.WriteValueSafe(Round);
            writer.WriteValueSafe(target);
            writer.WriteValueSafe(lane);
            writer.WriteValueSafe(distance);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !reader.TryBeginRead(RequestSize)) return;
            reader.ReadValueSafe(out byte operation);
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out ulong target);
            reader.ReadValueSafe(out int lane);
            reader.ReadValueSafe(out float distance);
            if (operation > (byte)Request.Sync || !float.IsFinite(distance)) return;
            if ((Request)operation == Request.Sync) { Publish(sender); return; }
            if (generation != Round) return;
            ServerHandle(sender, (Request)operation, target, lane, distance);
        }

        void ServerHandle(ulong sender, Request request, ulong target, int lane, float distance)
        {
            if (!Active) return;
            switch (request)
            {
                case Request.Hang:
                    // 요청 좌표로 멀리 있는 줄을 잡지 않도록 호스트가 관측한 위치와 대조한다.
                    if (!AcceptHang(sender, lane, distance) && Find(sender) != null && !pendingHangs.ContainsKey(sender))
                        pendingHangs.Add(sender, new PendingHang { Round = Round, Lane = lane, Distance = distance, Expires = Now + 1.25d });
                    break;
                case Request.Rescue: ServerRescue(sender, target); break;
                case Request.Cancel: ServerCancel(sender); break;
                case Request.Sync: Publish(sender); break;
            }
        }

        void Publish(ulong? recipient = null)
        {
            if (!IsHost) return;
            var list = new List<TightropeHangBook.Hang>(book.All);
            using var writer = new FastBufferWriter(8 + list.Count * EntrySize, Allocator.Temp);
            writer.WriteValueSafe(Round);
            writer.WriteValueSafe(list.Count);
            foreach (var hang in list)
            {
                writer.WriteValueSafe(hang.Id);
                writer.WriteValueSafe(hang.Lane);
                writer.WriteValueSafe(hang.Distance);
                writer.WriteValueSafe((float)hang.Remaining);
                writer.WriteValueSafe(hang.Rescuer);
                writer.WriteValueSafe((float)hang.PullRemaining);
                writer.WriteValueSafe((float)hang.Total);
            }
            if (recipient.HasValue)
            {
                if (recipient.Value != NetworkManager.ServerClientId)
                    messaging.SendNamedMessage(StateMessage, recipient.Value, writer, NetworkDelivery.ReliableSequenced);
            }
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || !reader.TryBeginRead(8)) return;
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out int count);
            if (generation != Round || count < 0 || count > 16 || !reader.TryBeginRead(count * EntrySize)) return;
            var snapshot = new List<TightropeHangBook.Hang>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong id);
                reader.ReadValueSafe(out int lane);
                reader.ReadValueSafe(out float distance);
                reader.ReadValueSafe(out float remaining);
                reader.ReadValueSafe(out ulong rescuer);
                reader.ReadValueSafe(out float pull);
                reader.ReadValueSafe(out float total);
                if (!float.IsFinite(distance) || !float.IsFinite(remaining) || !float.IsFinite(pull) || !float.IsFinite(total)) return;
                snapshot.Add(new TightropeHangBook.Hang
                {
                    Id = id, Lane = lane, Distance = distance, Remaining = remaining, Rescuer = rescuer, PullRemaining = pull, Total = total
                });
            }
            book.Replace(snapshot);
            snapshotAt = Now;
            UpdateRescuerBalance();
        }

        void SendRescued(TightropeHangBook.Hang hang)
        {
            if (IsHost)
            {
                using var writer = new FastBufferWriter(20, Allocator.Temp);
                writer.WriteValueSafe(Round);
                writer.WriteValueSafe(hang.Id);
                writer.WriteValueSafe(hang.Lane);
                writer.WriteValueSafe(hang.Distance);
                messaging.SendNamedMessageToAll(RescuedMessage, writer, NetworkDelivery.ReliableSequenced);
            }
            ApplyRescued(hang.Id, hang.Lane, hang.Distance);
        }

        void ReadRescued(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || !reader.TryBeginRead(20)) return;
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out ulong id);
            reader.ReadValueSafe(out int lane);
            reader.ReadValueSafe(out float distance);
            if (generation != Round || !float.IsFinite(distance) || course == null || lane < 0 || lane >= course.LaneCount) return;
            ApplyRescued(id, lane, distance);
        }
    }
}
