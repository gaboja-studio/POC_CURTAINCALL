using CurtainCall.Network.PlayerSync;
using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        const string FlightSnapshotMessage = "CurtainCall.HumanCannon.Flights";
        const string FlightRemovedMessage = "CurtainCall.HumanCannon.FlightRemoved";
        const int FlightSize = 20; // actor + station + launch + segment
        bool awaitingFlightSnapshot;

        void SendFlightSnapshot(ulong client)
        {
            var snapshot = flying.Capture();
            using var writer = new FastBufferWriter(sizeof(int) + snapshot.Length * FlightSize, Allocator.Temp);
            WriteFlightSnapshot(writer, snapshot);
            messaging.SendNamedMessage(FlightSnapshotMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        static void WriteFlightSnapshot(FastBufferWriter writer, CannonFlightState.Flight[] snapshot)
        {
            writer.WriteValueSafe(snapshot.Length);
            foreach (var flight in snapshot) WriteFlight(writer, flight);
        }

        static void WriteFlight(FastBufferWriter writer, CannonFlightState.Flight flight)
        {
            writer.WriteValueSafe(flight.Actor);
            writer.WriteValueSafe(flight.StationIndex);
            writer.WriteValueSafe(flight.LaunchRevision);
            writer.WriteValueSafe(flight.Segment);
        }

        static CannonFlightState.Flight ReadFlight(FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out int station);
            reader.ReadValueSafe(out uint launch);
            reader.ReadValueSafe(out uint segment);
            return new CannonFlightState.Flight(actor, station, launch, segment);
        }

        void HandleFlightSnapshot(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || !awaitingFlightSnapshot || reader.Length - reader.Position < sizeof(int)) return;
            reader.ReadValueSafe(out int count);
            int remaining = reader.Length - reader.Position;
            if (count < 0 || remaining % FlightSize != 0 || count != remaining / FlightSize) return;
            var snapshot = new CannonFlightState.Flight[count];
            for (int i = 0; i < count; i++) snapshot[i] = ReadFlight(reader);
            if (!flying.Restore(snapshot, stations.Length)) return;
            // 위치/물리는 기존 소유자 동기화를 사용한다. 과거 Launch를 재생하거나 로컬 pilot을 재발사하지 않는다.
            awaitingFlightSnapshot = false;
        }

        void PruneFlights()
        {
            // 접속 스냅샷이 원격 플레이어의 Spawn보다 먼저 도착해도 목록을 버리지 않는다.
            // 사망/퇴장은 호스트만 확정하고, 아직 Spawn되지 않은 관전자에게도 제거를 전달한다.
            if (session == null || !session.IsHost) return;
            flying.RemoveWhere(id =>
            {
                var player = FindPlayer(id);
                return player == null || player.State != PlayerState.Normal;
            }, SendFlightRemoved);
        }

        void SendFlightRemoved(CannonFlightState.Flight flight)
        {
            if (messaging == null || session == null || !session.IsHost) return;
            using var writer = new FastBufferWriter(FlightSize, Allocator.Temp);
            WriteFlight(writer, flight);
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId)
                    messaging.SendNamedMessage(FlightRemovedMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleFlightRemoved(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != FlightSize) return;
            var flight = ReadFlight(reader);
            flying.TryComplete(flight.Actor, flight.StationIndex, flight.LaunchRevision, flight.Segment);
        }
    }
}
