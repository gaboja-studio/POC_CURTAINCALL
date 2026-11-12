using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonRouteSystem
    {
        enum RouteAction : byte { Sync, ResetSync = 3 }
        const string RequestName = "CurtainCall.HumanCannon.Route.Request.v2";
        const string StateName = "CurtainCall.HumanCannon.Route.State.v2";
        const int RequestBytes = 9;
        CustomMessagingManager messaging;

        void SendRequest(RouteAction action, int index)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(RequestBytes, Allocator.Temp);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(index);
            writer.WriteValueSafe(revision);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (reader.Length - reader.Position != RequestBytes) return;
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint expected);
            HandleRequest(sender, (RouteAction)action, index, expected);
        }

        void Publish()
        {
            if (messaging == null || session == null || !session.IsHost) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client);
        }

        void SendState(ulong client, int resetAcknowledgment = 0)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            writer.WriteValueSafe(revision);
            writer.WriteValueSafe(resetAcknowledgment);
            writer.WriteValueSafe(AliveFlyers);
            writer.WriteValueSafe(ArrivedFlyers);
            writer.WriteValueSafe(runtime.Length);
            foreach (var transit in runtime)
            {
                writer.WriteValueSafe(transit.State.Revision);
                writer.WriteValueSafe((byte)transit.State.Phase);
                writer.WriteValueSafe(transit.State.Rider);
                writer.WriteValueSafe(transit.State.StartedAt);
                writer.WriteValueSafe(transit.State.Duration);
                writer.WriteValueSafe((byte)(transit.State.Delivered ? 1 : 0));
                writer.WriteValueSafe(transit.Offset);
                writer.WriteValueSafe(transit.Rotation);
                writer.WriteValueSafe((byte)(transit.Trap.IsOpen ? 1 : 0));
                writer.WriteValueSafe(transit.Trap.OpenedAt);
                writer.WriteValueSafe(transit.Trap.FrozenProgress);
                writer.WriteValueSafe(transit.Boarding.Rider);
                writer.WriteValueSafe(transit.Boarding.BoardedAt);
                writer.WriteValueSafe((byte)(transit.Occupied ? 1 : 0));
                writer.WriteValueSafe((byte)(transit.Rejected ? 1 : 0));
            }
            writer.WriteValueSafe(fires.Length);
            foreach (var fire in fires)
            {
                writer.WriteValueSafe(fire != null ? fire.ArmedAt : -1d);
                writer.WriteValueSafe((byte)(fire != null && fire.Finished ? 1 : 0));
                if (fire != null) for (int stage = 0; stage < fire.StageCount; stage++) writer.WriteValueSafe(fire.StageAt(stage));
            }
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            int expectedBytes = 24 + runtime.Length * 85 + fires.Length * 9;
            foreach (var fire in fires) if (fire != null) expectedBytes += fire.StageCount * 8;
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != expectedBytes) return;
            reader.ReadValueSafe(out uint incoming);
            reader.ReadValueSafe(out int resetAcknowledgment);
            reader.ReadValueSafe(out int alive);
            reader.ReadValueSafe(out int arrived);
            reader.ReadValueSafe(out int count);
            if (incoming < revision || alive < 0 || arrived < 0 || arrived > alive || count != runtime.Length) return;
            var snapshot = new Transit[count];
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out uint stateRevision);
                reader.ReadValueSafe(out byte phase);
                reader.ReadValueSafe(out ulong rider);
                reader.ReadValueSafe(out double startedAt);
                reader.ReadValueSafe(out float duration);
                reader.ReadValueSafe(out byte delivered);
                reader.ReadValueSafe(out Vector3 offset);
                reader.ReadValueSafe(out Quaternion rotation);
                reader.ReadValueSafe(out byte trapOpen);
                reader.ReadValueSafe(out double openedAt);
                reader.ReadValueSafe(out float frozenProgress);
                reader.ReadValueSafe(out ulong boardingRider);
                reader.ReadValueSafe(out double boardedAt);
                reader.ReadValueSafe(out byte occupied);
                reader.ReadValueSafe(out byte rejected);
                if (rejected > 1 || delivered > 1) return;
                var transit = new Transit { Offset = offset, Rotation = rotation, Occupied = occupied == 1, Rejected = rejected == 1 };
                if (!Finite(offset) || !Finite(rotation) || !transit.State.Receive(stateRevision, (DeparturePhase)phase, rider, startedAt, duration, delivered == 1)) return;
                if (trapOpen > 1 || !transit.Trap.Receive(trapOpen == 1, openedAt, frozenProgress)) return;
                if (occupied > 1 || !transit.Boarding.Receive(boardingRider, boardedAt)) return;
                if (transit.Boarding.IsCounting && (transit.Trap.IsOpen || transit.State.Phase != DeparturePhase.Waiting || !transit.Occupied)) return;
                if (runtime[i].RevokedTravel == (rider, startedAt)) transit.RevokedTravel = runtime[i].RevokedTravel;
                snapshot[i] = transit;
            }
            reader.ReadValueSafe(out int fireCount);
            if (fireCount != fires.Length) return;
            var armed = new double[fireCount];
            var ended = new byte[fireCount];
            var stages = new double[fireCount][];
            for (int i = 0; i < fireCount; i++)
            {
                reader.ReadValueSafe(out armed[i]);
                reader.ReadValueSafe(out ended[i]);
                if (!double.IsFinite(armed[i]) || armed[i] < -1 || ended[i] > 1 || armed[i] < 0 && ended[i] != 0) return;
                stages[i] = new double[fires[i] != null ? fires[i].StageCount : 0];
                for (int stage = 0; stage < stages[i].Length; stage++)
                {
                    reader.ReadValueSafe(out stages[i][stage]);
                    // 단계 시각은 앞 단계보다 이르지 않고, 켜지기 전에는 없다.
                    double previous = stage > 0 ? stages[i][stage - 1] : armed[i];
                    if (!double.IsFinite(stages[i][stage]) || stages[i][stage] < -1
                        || stages[i][stage] >= 0 && (armed[i] < 0 || previous < 0 || stages[i][stage] < previous)) return;
                }
            }
            if (!resetBarrier.Accept(resetAcknowledgment)) return;
            revision = incoming;
            AliveFlyers = alive; ArrivedFlyers = arrived; runtime = snapshot;
            for (int i = 0; i < fireCount; i++) if (fires[i] != null) fires[i].Receive(armed[i], ended[i] == 1, stages[i]);
        }

        void Connection(SessionConnectionState connection)
        {
            Unregister();
            if (connection == SessionConnectionState.Offline)
            {
                if (carriedPilot != null) { carriedPilot.EndTransport(); carriedPilot = null; }
                carriedTransit = -1; revision = 0; resetBarrier.Clear();
                System.Array.Clear(deliveredArrival, 0, deliveredArrival.Length);
                ResetRoute();
                return;
            }
            if (connection != SessionConnectionState.Host && connection != SessionConnectionState.Client) return;
            messaging = session.NetworkManager.CustomMessagingManager;
            if (connection == SessionConnectionState.Host) messaging.RegisterNamedMessageHandler(RequestName, ReceiveRequest);
            else
            {
                messaging.RegisterNamedMessageHandler(StateName, ReceiveState);
                SendRequest(resetBarrier.Waiting ? RouteAction.ResetSync : RouteAction.Sync,
                    resetBarrier.Waiting ? resetBarrier.Token : -1);
            }
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestName);
            messaging.UnregisterNamedMessageHandler(StateName);
            messaging = null;
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static bool Finite(Quaternion value) => float.IsFinite(value.x) && float.IsFinite(value.y)
            && float.IsFinite(value.z) && float.IsFinite(value.w) && Quaternion.Dot(value, value) > 0.0001f;
    }
}
