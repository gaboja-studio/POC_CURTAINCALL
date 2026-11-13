using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuerBoss
    {
        struct BossState : INetworkSerializeByMemcpy
        {
            public double Round, Started;
            public uint Revision;
            public Phase Phase;
            public byte Mask;
            public Vector3 From, Goal, HitPoint;
        }
        const string Message = "CurtainCall.Escape.PursuerBoss.v1";
        BossState state;
        CustomMessagingManager messaging;
        double nextSend;
        uint sentRevision;

        void Attach()
        {
            var net = NetworkManager.Singleton;
            var next = net && net.IsListening ? net.CustomMessagingManager : null;
            if (next == messaging) return;
            Detach(); messaging = next;
            if (messaging != null && !net.IsServer) messaging.RegisterNamedMessageHandler(Message, Read);
        }
        void Detach() { messaging?.UnregisterNamedMessageHandler(Message); messaging = null; nextSend = 0; }
        void Publish(double now)
        {
            if (messaging == null || now < nextSend && sentRevision == state.Revision) return;
            nextSend = now + .25; sentRevision = state.Revision;
            using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize<BossState>(), Allocator.Temp);
            writer.WriteValueSafe(state);
            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId) messaging.SendNamedMessage(Message, id, writer, NetworkDelivery.ReliableSequenced);
        }
        void Read(ulong sender, FastBufferReader reader)
        {
            var timer = EscapeRouteTimer.Current;
            if (sender != NetworkManager.ServerClientId || !timer || !timer.HasStarted
                || reader.Length - reader.Position != FastBufferWriter.GetWriteSize<BossState>()) return;
            reader.ReadValueSafe(out BossState next);
            if (next.Round != timer.StartedAt || !double.IsFinite(next.Started) || next.Started < 0
                || (byte)next.Phase > (byte)Phase.Dead || !float.IsFinite(next.From.sqrMagnitude)
                || !float.IsFinite(next.Goal.sqrMagnitude) || !float.IsFinite(next.HitPoint.sqrMagnitude)
                || next.Phase >= Phase.Dying && next.Mask != byte.MaxValue
                || state.Round == next.Round && (next.Revision < state.Revision || (next.Mask & state.Mask) != state.Mask)) return;
            state = next;
        }
    }
}
