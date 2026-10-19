using CurtainCall.Network.Session;
using CurtainCall.Network.PlayerSync;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeSwingPole
    {
        enum Operation : byte { Grab, LetGo, Abort, Sync, Swing, Landed }
        string RequestMessage => $"CurtainCall.Tightrope.Swing.Request.{assignedLane}";
        string StateMessage => $"CurtainCall.Tightrope.Swing.State.{assignedLane}";
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        void Connect()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening ? next.NetworkManager.CustomMessagingManager : null;
            if (session == next && messaging == manager) return;
            ResetLocal(); Disconnect(); ResetState(); session = next; messaging = manager;
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestMessage, ReadRequest);
            messaging.RegisterNamedMessageHandler(StateMessage, ReadState); Send(Operation.Sync);
        }
        void Disconnect()
        {
            messaging?.UnregisterNamedMessageHandler(RequestMessage); messaging?.UnregisterNamedMessageHandler(StateMessage);
            messaging = null; session = null;
        }
        void Send(Operation operation, float input = 0f)
        {
            if (session == null || messaging == null) return;
            if (session.IsHost) { ServerHandle(session.NetworkManager.LocalClientId, operation, input); return; }
            using var writer = new FastBufferWriter(9, Allocator.Temp);
            writer.WriteValueSafe((byte)operation); writer.WriteValueSafe(round); writer.WriteValueSafe(input);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !reader.TryBeginRead(9)) return;
            reader.ReadValueSafe(out byte operation); reader.ReadValueSafe(out int generation); reader.ReadValueSafe(out float input);
            if (operation > (byte)Operation.Landed || generation != round || !float.IsFinite(input) || input < -1f || input > 1f) return;
            ServerHandle(sender, (Operation)operation, input);
        }
        FastBufferWriter WriteState()
        {
            var writer = new FastBufferWriter(54, Allocator.Temp);
            writer.WriteValueSafe(round); writer.WriteValueSafe(version); writer.WriteValueSafe((byte)mode);
            writer.WriteValueSafe(holder); writer.WriteValueSafe(lane); writer.WriteValueSafe(startedAt); writer.WriteValueSafe(changedAt);
            writer.WriteValueSafe(angle); writer.WriteValueSafe(angularSpeed); writer.WriteValueSafe(drive); writer.WriteValueSafe(returnAngle); writer.WriteValueSafe(released);
            return writer;
        }
        void Publish(ulong? recipient = null)
        {
            if (session == null || !session.IsHost || messaging == null) return;
            using var writer = WriteState();
            if (recipient.HasValue) messaging.SendNamedMessage(StateMessage, recipient.Value, writer, NetworkDelivery.ReliableSequenced);
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || run == null || !reader.TryBeginRead(54)) return;
            reader.ReadValueSafe(out int generation); reader.ReadValueSafe(out int sequence); reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out ulong owner); reader.ReadValueSafe(out int slot); reader.ReadValueSafe(out double start); reader.ReadValueSafe(out double changed);
            reader.ReadValueSafe(out float value); reader.ReadValueSafe(out float speed); reader.ReadValueSafe(out float input);
            reader.ReadValueSafe(out float returning); reader.ReadValueSafe(out bool launch);
            if (generation != run.Round || sequence < 0 || (generation == round && sequence < version) || state > (byte)PoleMode.Returning
                || !double.IsFinite(start) || !double.IsFinite(changed) || start > Clock + 5d || changed > Clock + 5d
                || !float.IsFinite(value) || Mathf.Abs(value) > 85f || !float.IsFinite(speed) || Mathf.Abs(speed) > 180f
                || !float.IsFinite(input) || Mathf.Abs(input) > 1f || !float.IsFinite(returning) || Mathf.Abs(returning) > 85f
                || course == null || slot < -1 || slot >= course.LaneCount
                || (state != (byte)PoleMode.Idle && (slot != assignedLane || owner == Nobody))) return;
            if (round != generation) { ResetLocal(); ResetState(); round = generation; }
            version = sequence; mode = (PoleMode)state; holder = owner; lane = slot; startedAt = start; changedAt = changed;
            angle = value; angularSpeed = speed; drive = input; returnAngle = returning; released = launch; pendingUntil = 0d;
            if (mode == PoleMode.Idle || holder != NetworkPlayer.Local?.OwnerClientId) localAbort = false;
            if (binding != null && mode != PoleMode.Holding) EndBinding(released && binding.Player.OwnerClientId == holder);
        }
    }
}
