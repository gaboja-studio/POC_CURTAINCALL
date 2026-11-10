using CurtainCall.Campaign;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitDirector
    {
        const string PoseMessage = "CurtainCall.WaitingRoom.Pursuit.Poses.v4";
        const int PosesPerPacket = 16; // 5 header ints + timestamp + 16*(index+position+rotation) = 540 bytes.
        int poseEpoch;
        float poseSendAt;
        void TickPoseBroadcast()
        {
            if (messaging == null || manager == null || !manager.IsServer || readyClients.Count == 0 || Time.unscaledTime < poseSendAt) return;
            poseSendAt = Time.unscaledTime + .05f;
            double stamp = Time.realtimeSinceStartupAsDouble;
            RecordContactHistory(stamp);
            for (int start = 0; start < actors.Count; start += PosesPerPacket) {
                using var writer = new FastBufferWriter(1024, Allocator.Temp);
                writer.WriteValueSafe(generation); writer.WriteValueSafe(day); writer.WriteValueSafe(performance);
                writer.WriteValueSafe(poseEpoch); writer.WriteValueSafe(stamp);
                int count = Mathf.Min(PosesPerPacket, actors.Count - start); writer.WriteValueSafe(count);
                for (int i = start; i < start + count; i++) {
                    writer.WriteValueSafe(i); writer.WriteValueSafe(actors[i].transform.position); writer.WriteValueSafe(actors[i].transform.rotation);
                }
                foreach (var id in readyClients) if (manager.ConnectedClients.ContainsKey(id))
                    messaging.SendNamedMessage(PoseMessage, id, writer, NetworkDelivery.Unreliable);
            }
        }
        void ReceivePoses(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || run == null || reader.Length < 28) return;
            reader.ReadValueSafe(out int g); reader.ReadValueSafe(out int d); reader.ReadValueSafe(out int p);
            reader.ReadValueSafe(out int epoch); reader.ReadValueSafe(out double stamp); reader.ReadValueSafe(out int count);
            var state = run.State;
            if (!IsPursuitPhase(state.Phase) || g != state.Generation || d != state.Day || p != state.PerformanceIndex || epoch != poseEpoch
                || count < 0 || count > PosesPerPacket || reader.Length - reader.Position < count * 32) return;
            for (int i = 0; i < count; i++) {
                reader.ReadValueSafe(out int index); reader.ReadValueSafe(out Vector3 position); reader.ReadValueSafe(out Quaternion rotation);
                if (index >= 0 && index < actors.Count) actors[index].ReceivePose(stamp, position, rotation);
            }
        }
    }
}
