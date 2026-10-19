using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        const string CooperativeGateMessage = "CurtainCall.Tightrope.Saws.CooperativeGate";
        FastBufferWriter WriteCooperativeGate()
        {
            var writer = new FastBufferWriter(24, Allocator.Temp);
            writer.WriteValueSafe(run == null ? -1 : run.Round);
            writer.WriteValueSafe(cooperativeGateIndex);
            writer.WriteValueSafe(cooperativeGateActive);
            writer.WriteValueSafe(cooperativeGateStartedAt);
            writer.WriteValueSafe(cooperativeGateOpenedAt);
            writer.WriteValueSafe(cooperativeGateLane);
            return writer;
        }

        void BroadcastCooperativeGate()
        {
            if (Messaging == null) return;
            using var writer = WriteCooperativeGate();
            Messaging.SendNamedMessageToAll(CooperativeGateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleCooperativeGateMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || run == null || !reader.TryBeginRead(21)) return;
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out bool active);
            reader.ReadValueSafe(out float started);
            reader.ReadValueSafe(out float opened);
            reader.ReadValueSafe(out int lane);
            if (generation != run.Round || index < -1 || index >= (settings.cooperativeGateDistances?.Length ?? 0)
                || (active && index < 0) || !float.IsFinite(started) || !float.IsFinite(opened)
                || started < 0f || (opened != -1f && opened < started)
                || lane < TightropeCourse.NoLane || course == null || lane >= course.LaneCount || (active && lane < 0)) return;
            SetCooperativeGate(index, active, started, opened);
            cooperativeGateLane = lane;
        }
    }
}
