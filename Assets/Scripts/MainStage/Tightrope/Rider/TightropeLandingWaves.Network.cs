using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeLandingWaves
    {
        const string Message = "CurtainCall.Tightrope.LandingWave";
        NetworkSessionManager session;
        CustomMessagingManager messaging;

        void Connect()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                ? next.NetworkManager.CustomMessagingManager : null;
            if (session == next && messaging == manager) return;
            Disconnect(); session = next; messaging = manager;
            Clear();
            messaging?.RegisterNamedMessageHandler(Message, Receive);
        }

        void Disconnect()
        {
            messaging?.UnregisterNamedMessageHandler(Message);
            messaging = null; session = null;
        }

        void Publish(Pulse pulse)
        {
            if (messaging == null || session == null || !session.IsHost) return;
            using var writer = new FastBufferWriter(44, Allocator.Temp);
            writer.WriteValueSafe(round); writer.WriteValueSafe(pulse.Serial);
            writer.WriteValueSafe(pulse.Source); writer.WriteValueSafe(pulse.Lane);
            writer.WriteValueSafe(pulse.Distance); writer.WriteValueSafe(pulse.At);
            writer.WriteValueSafe(pulse.Range); writer.WriteValueSafe(pulse.Speed); writer.WriteValueSafe(pulse.Shock);
            messaging.SendNamedMessageToAll(Message, writer, NetworkDelivery.ReliableSequenced);
        }

        void Receive(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId
                || !Active || !reader.TryBeginRead(44)) return;
            reader.ReadValueSafe(out int generation); reader.ReadValueSafe(out int sequence);
            reader.ReadValueSafe(out ulong source); reader.ReadValueSafe(out int lane);
            reader.ReadValueSafe(out float distance); reader.ReadValueSafe(out double at);
            reader.ReadValueSafe(out float range); reader.ReadValueSafe(out float speed); reader.ReadValueSafe(out float shock);
            if (run == null || generation != run.Round || sequence < 1
                || (generation == round && sequence <= receivedSerial) || lane < 0 || lane >= course.LaneCount
                || !float.IsFinite(distance) || !double.IsFinite(at) || !float.IsFinite(range)
                || !float.IsFinite(speed) || !float.IsFinite(shock) || distance < 0f || distance >= course.FinishDistance
                || range <= 0f || range > 100f || speed < .1f || speed > 100f || shock < 0f || shock > 15f
                || at > Clock + 5d || Clock > at + range / speed + .25d) return;
            if (round != generation) { Clear(); round = generation; }
            receivedSerial = sequence;
            AddPulse(new Pulse { Serial = sequence, Source = source, Lane = lane, Distance = distance,
                At = at, Range = range, Speed = speed, Shock = shock });
        }
    }
}
