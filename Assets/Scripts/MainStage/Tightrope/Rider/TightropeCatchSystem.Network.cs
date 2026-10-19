using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCatchSystem
    {
        enum Request : byte { Prepare, Cancel, Landed, Sync }
        const string RequestMessage = "CurtainCall.Tightrope.Catch.Request";
        const string StateMessage = "CurtainCall.Tightrope.Catch.State";
        const string SuccessMessage = "CurtainCall.Tightrope.Catch.Success";
        NetworkSessionManager session;
        CustomMessagingManager messaging;

        void Connect()
        {
            var next = NetworkSessionManager.Instance;
            if (session != next)
            {
                Disconnect();
                session = next;
            }
            var manager = session != null && session.NetworkManager != null && session.NetworkManager.IsListening
                ? session.NetworkManager.CustomMessagingManager : null;
            if (messaging == manager) return;
            Unregister();
            messaging = manager;
            Clear();
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestMessage, ReadRequest);
            messaging.RegisterNamedMessageHandler(StateMessage, ReadState);
            messaging.RegisterNamedMessageHandler(SuccessMessage, ReadSuccess);
            Send(Request.Sync);
        }

        void Disconnect() { Unregister(); session = null; }
        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(SuccessMessage);
            messaging = null;
        }

        void Send(Request request, ulong target = 0)
        {
            if (session == null || messaging == null) return;
            if (request == Request.Prepare)
            {
                if (Now < nextRequestAt) return;
                nextRequestAt = Now + .2;
            }
            if (session.IsHost) { ServerHandle(session.NetworkManager.LocalClientId, request, target); return; }
            using var writer = new FastBufferWriter(13, Allocator.Temp);
            writer.WriteValueSafe((byte)request);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(target);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !reader.TryBeginRead(13)) return;
            reader.ReadValueSafe(out byte operation);
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out ulong target);
            if (generation != round || operation > (byte)Request.Sync) return;
            ServerHandle(sender, (Request)operation, target);
        }

        void Publish(ulong? recipient = null)
        {
            if (session == null || !session.IsHost || messaging == null) return;
            using var writer = new FastBufferWriter(8 + ready.Count * 28, Allocator.Temp);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(ready.Count);
            foreach (var pair in ready)
            {
                writer.WriteValueSafe(pair.Key);
                writer.WriteValueSafe(pair.Value.Until);
                writer.WriteValueSafe(pair.Value.Position);
            }
            if (recipient.HasValue) messaging.SendNamedMessage(StateMessage, recipient.Value, writer, NetworkDelivery.ReliableSequenced);
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || !reader.TryBeginRead(8)) return;
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out int count);
            if (generation != round || count < 0 || count > 16 || !reader.TryBeginRead(count * 28)) return;
            var snapshot = new System.Collections.Generic.Dictionary<ulong, ReadyWindow>();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong id);
                reader.ReadValueSafe(out double until);
                reader.ReadValueSafe(out Vector3 position);
                if (!double.IsFinite(until) || !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)
                    || snapshot.ContainsKey(id)) return;
                snapshot[id] = new ReadyWindow { Until = until, Position = position };
            }
            ready.Clear();
            foreach (var pair in snapshot) ready.Add(pair.Key, pair.Value);
        }

        void PublishSuccess(ulong jumper, ulong catcher, bool relay = false)
        {
            ShowSuccess(jumper, catcher, relay);
            if (messaging == null) return;
            using var writer = new FastBufferWriter(21, Allocator.Temp);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(jumper);
            writer.WriteValueSafe(catcher);
            writer.WriteValueSafe(relay);
            messaging.SendNamedMessageToAll(SuccessMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadSuccess(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || !reader.TryBeginRead(21)) return;
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out ulong jumper);
            reader.ReadValueSafe(out ulong catcher);
            reader.ReadValueSafe(out bool relay);
            if (generation == round && Active) ShowSuccess(jumper, catcher, relay);
        }
    }
}
