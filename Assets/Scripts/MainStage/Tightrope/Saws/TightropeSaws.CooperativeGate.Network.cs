using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        const string CooperativeGateMessage = "CurtainCall.Tightrope.Saws.CooperativeGate";
        FastBufferWriter WriteCooperativeGate()
        {
            var writer = new FastBufferWriter(41 + cooperativeAdmittedCount * 8 + releasedGates.Count * 20, Allocator.Temp);
            writer.WriteValueSafe(run == null ? -1 : run.Round);
            writer.WriteValueSafe(cooperativeGateIndex);
            writer.WriteValueSafe(cooperativeGateActive);
            writer.WriteValueSafe(cooperativeGateStartedAt);
            writer.WriteValueSafe(cooperativeGateOpenedAt);
            writer.WriteValueSafe(cooperativeGateLane);
            writer.WriteValueSafe(cooperativeGateClockStartedAt);
            writer.WriteValueSafe(releasedGates.Count);
            writer.WriteValueSafe(cooperativeGateRevision);
            writer.WriteValueSafe(cooperativeAdmittedCount);
            for (int i = 0; i < cooperativeAdmittedCount; i++) writer.WriteValueSafe(cooperativeAdmittedPlayers[i]);
            foreach (var gate in releasedGates)
            {
                writer.WriteValueSafe(gate.Index); writer.WriteValueSafe(gate.Lane);
                writer.WriteValueSafe(gate.At); writer.WriteValueSafe(gate.InitialLift);
            }
            return writer;
        }

        void BroadcastCooperativeGate()
        {
            cooperativeGateRevision++;
            if (Messaging == null) return;
            using var writer = WriteCooperativeGate();
            Messaging.SendNamedMessageToAll(CooperativeGateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleCooperativeGateMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || run == null || !reader.TryBeginRead(41)) return;
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out bool active);
            reader.ReadValueSafe(out float started);
            reader.ReadValueSafe(out float opened);
            reader.ReadValueSafe(out int lane);
            reader.ReadValueSafe(out double clockStarted);
            reader.ReadValueSafe(out int count);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out int admittedCount);
            if (generation != run.Round || index < -1 || index >= (settings.cooperativeGateDistances?.Length ?? 0)
                || revision < receivedCooperativeGateRevision
                || (active && index < 0) || !float.IsFinite(started) || !float.IsFinite(opened) || !double.IsFinite(clockStarted)
                || started < 0f || (opened != -1f && opened < started)
                || lane < TightropeCourse.NoLane || course == null || lane >= course.LaneCount || (active && lane < 0)
                || count < 0 || count > (settings.cooperativeGateDistances?.Length ?? 0) || count > 256
                || (admittedCount != 0 && admittedCount != FinaleStackSize) || (admittedCount > 0 && index < 0)
                || !reader.TryBeginRead(admittedCount * 8 + count * 20)) return;
            var admitted = new ulong[admittedCount];
            for (int i = 0; i < admittedCount; i++)
            {
                reader.ReadValueSafe(out admitted[i]);
                if (admitted[i] == ulong.MaxValue) return;
                for (int j = 0; j < i; j++) if (admitted[j] == admitted[i]) return;
            }
            var released = new List<ReleasedGate>(count);
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int releasedIndex); reader.ReadValueSafe(out int releasedLane);
                reader.ReadValueSafe(out double at); reader.ReadValueSafe(out float lift);
                if (releasedIndex <= previous || releasedIndex > index || releasedLane < 0 || releasedLane >= course.LaneCount
                    || !double.IsFinite(at) || !float.IsFinite(lift) || lift < 0f || lift > 100f) return;
                previous = releasedIndex;
                released.Add(new ReleasedGate { Index = releasedIndex, Lane = releasedLane, At = at, InitialLift = lift });
            }
            SetCooperativeGate(index, active, started, opened);
            cooperativeGateLane = lane;
            cooperativeGateClockStartedAt = clockStarted;
            receivedCooperativeGateRevision = revision;
            cooperativeGateRevision = revision;
            cooperativeAdmittedCount = admittedCount;
            System.Array.Clear(cooperativeAdmittedPlayers, 0, cooperativeAdmittedPlayers.Length);
            System.Array.Copy(admitted, cooperativeAdmittedPlayers, admittedCount);
            SetReleasedGates(released);
        }
    }
}
