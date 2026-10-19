using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeTrapeze
    {
        enum Command : byte { Sync, Grab, Attach, Release, Swing, Landed, Abort }
        const string RequestMessage = "CurtainCall.Tightrope.Trapeze.Request";
        const string StateMessage = "CurtainCall.Tightrope.Trapeze.State";
        const int RequestBytes = 21, PoleBytes = 48, FlightBytes = 56;
        NetworkSessionManager session;
        CustomMessagingManager messaging;

        void Connect()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                ? next.NetworkManager.CustomMessagingManager : null;
            if (session == next && messaging == manager) return;
            ResetLocal(); Disconnect(); ClearState(); round = -1; session = next; messaging = manager;
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestMessage, ReadRequest);
            messaging.RegisterNamedMessageHandler(StateMessage, ReadState); Send(Command.Sync);
        }
        void Disconnect()
        {
            messaging?.UnregisterNamedMessageHandler(RequestMessage); messaging?.UnregisterNamedMessageHandler(StateMessage);
            session = null; messaging = null;
        }
        void Send(Command command, int index = -1, float input = 0f)
        {
            ObserveRequest(NetworkPlayer.Local != null ? NetworkPlayer.Local.OwnerClientId : Nobody, "send", command, index);
            if (session == null || messaging == null) return;
            var local = NetworkPlayer.Local;
            uint revision = index >= 0 && index < PoleTotal ? poles[index].Revision : 0;
            uint token = local != null && flights.TryGetValue(local.OwnerClientId, out var flight) ? flight.Token : 0;
            if (session.IsHost) { HandleRequest(session.NetworkManager.LocalClientId, command, index, revision, input, token); return; }
            using var writer = new FastBufferWriter(RequestBytes, Allocator.Temp);
            writer.WriteValueSafe((byte)command); writer.WriteValueSafe(round); writer.WriteValueSafe(index);
            writer.WriteValueSafe(revision); writer.WriteValueSafe(input); writer.WriteValueSafe(token);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != RequestBytes) return;
            reader.ReadValueSafe(out byte action); reader.ReadValueSafe(out int generation); reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision); reader.ReadValueSafe(out float input); reader.ReadValueSafe(out uint token);
            if (action > (byte)Command.Abort || !float.IsFinite(input) || Mathf.Abs(input) > 1f) return;
            if ((Command)action != Command.Sync && generation != round) return;
            HandleRequest(sender, (Command)action, index, revision, input, token);
        }
        void HandleRequest(ulong sender, Command command, int index, uint revision, float input, uint token)
        {
            ObserveRequest(sender, "receive", command, index);
            if (command == Command.Sync) { Publish(sender); return; }
            if (!Active) return;
            if (command == Command.Abort) { AbortActor(sender); return; }
            var player = Find(sender);
            if (player == null || !Alive(sender)) return;
            if (command == Command.Landed || command == Command.Grab || command == Command.Attach)
            {
                SubmitPendingRequest(sender, command, index, revision, token);
                return;
            }
            if (index < 0 || index >= PoleTotal || revision != poles[index].Revision) { Publish(sender); return; }
            var state = poles[index];
            if (command == Command.Swing && state.Upper == sender)
            { state.Drive = input; state.LastInput = Clock; }
            else if (command == Command.Release && state.Lower == sender)
            { LaunchActor(sender, index, true, true); state.Lower = Nobody; state.Revision++; Publish(); }
            else if (command == Command.Release && state.Upper == sender && state.Lower == Nobody)
                RemovePole(index, true);
            else Publish(sender);
        }
        void LaunchActor(ulong actor, int index, bool lower, bool propel)
        {
            pendingRequests.Remove(actor);
            if (!Alive(actor)) return;
            Sample(index, out float angle, out float speed);
            flights[actor] = new FlightState { Token = ++nextFlightToken, From = index, Position = Feet(index, lower),
                Velocity = propel ? poles[index].Physics.ReleaseVelocity(angle, speed) : Vector3.zero,
                Started = Clock, Until = Clock + 8d };
            TightropeLandingWaves.Current?.RegisterExternalLaunch(Find(actor), sharedAirborne: true);
            TightropeCatchSystem.Current?.RegisterSwingRelease(Find(actor), -1, trapeze: true);
        }
        void RemovePole(int index, bool propel)
        {
            var state = poles[index];
            if (state.Lower != Nobody) LaunchActor(state.Lower, index, true, propel);
            if (propel && state.Upper != Nobody) LaunchActor(state.Upper, index, false, true);
            state.Upper = state.Lower = Nobody; state.Angle = state.Speed = state.Drive = 0f;
            state.Until = 0d; state.Revision++; Publish();
        }
        void AbortActor(ulong actor)
        {
            pendingRequests.Remove(actor);
            flights.Remove(actor);
            for (int i = 0; i < PoleTotal; i++)
            {
                if (poles[i].Upper == actor) RemovePole(i, false);
                else if (poles[i].Lower == actor) { poles[i].Lower = Nobody; poles[i].Revision++; }
            }
            Publish();
        }
        void Publish(ulong? recipient = null)
        {
            if (session == null || !session.IsHost || messaging == null) return;
            version++;
            using var writer = new FastBufferWriter(17 + PoleTotal * PoleBytes + flights.Count * FlightBytes, Allocator.Temp);
            writer.WriteValueSafe(round); writer.WriteValueSafe(version); writer.WriteValueSafe(Clock);
            foreach (var state in poles)
            {
                writer.WriteValueSafe(state.Revision); writer.WriteValueSafe(state.Upper); writer.WriteValueSafe(state.Lower);
                writer.WriteValueSafe(state.Angle); writer.WriteValueSafe(state.Speed); writer.WriteValueSafe(state.Drive);
                writer.WriteValueSafe(state.Until); writer.WriteValueSafe(state.SampledAt);
            }
            writer.WriteValueSafe((byte)flights.Count);
            foreach (var pair in flights)
            {
                var flight = pair.Value; writer.WriteValueSafe(pair.Key); writer.WriteValueSafe(flight.Token); writer.WriteValueSafe(flight.From);
                writer.WriteValueSafe(flight.Position); writer.WriteValueSafe(flight.Velocity);
                writer.WriteValueSafe(flight.Started); writer.WriteValueSafe(flight.Until);
            }
            if (recipient.HasValue) messaging.SendNamedMessage(StateMessage, recipient.Value, writer, NetworkDelivery.ReliableSequenced);
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || run == null
                || reader.Length - reader.Position < 17 + PoleTotal * PoleBytes) return;
            reader.ReadValueSafe(out int generation); reader.ReadValueSafe(out uint sequence); reader.ReadValueSafe(out double timestamp);
            if (generation != run.Round || generation != round || sequence <= version || !double.IsFinite(timestamp) || timestamp > Clock + 5d) return;
            var incoming = new PoleState[PoleTotal];
            var owners = new System.Collections.Generic.HashSet<ulong>();
            for (int i = 0; i < PoleTotal; i++)
            {
                var state = incoming[i] = new PoleState();
                reader.ReadValueSafe(out state.Revision); reader.ReadValueSafe(out state.Upper); reader.ReadValueSafe(out state.Lower);
                reader.ReadValueSafe(out state.Angle); reader.ReadValueSafe(out state.Speed); reader.ReadValueSafe(out state.Drive);
                reader.ReadValueSafe(out state.Until); reader.ReadValueSafe(out state.SampledAt);
                if (!float.IsFinite(state.Angle) || Mathf.Abs(state.Angle) > 85f || !float.IsFinite(state.Speed) || Mathf.Abs(state.Speed) > 181f
                    || !float.IsFinite(state.Drive) || Mathf.Abs(state.Drive) > 1f || !double.IsFinite(state.Until) || !double.IsFinite(state.SampledAt)
                    || state.Until < 0d || state.Until > timestamp + HoldSeconds + 1d || state.SampledAt > timestamp + 1d
                    || (state.Upper == Nobody && state.Lower != Nobody)) return;
                if (state.Upper != Nobody && !owners.Add(state.Upper)) return;
                if (state.Lower != Nobody && !owners.Add(state.Lower)) return;
            }
            reader.ReadValueSafe(out byte count);
            if (count > 8 || reader.Length - reader.Position != count * FlightBytes) return;
            var incomingFlights = new System.Collections.Generic.Dictionary<ulong, FlightState>();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong actor);
                var flight = new FlightState(); reader.ReadValueSafe(out flight.Token); reader.ReadValueSafe(out flight.From);
                reader.ReadValueSafe(out flight.Position); reader.ReadValueSafe(out flight.Velocity);
                reader.ReadValueSafe(out flight.Started); reader.ReadValueSafe(out flight.Until);
                if (actor == Nobody || !owners.Add(actor) || flight.Token == 0 || flight.From < 0 || flight.From >= PoleTotal
                    || !Finite(flight.Position) || !Finite(flight.Velocity) || flight.Velocity.sqrMagnitude > 400f
                    || !double.IsFinite(flight.Started) || !double.IsFinite(flight.Until) || flight.Started > timestamp + 1d
                    || flight.Until <= flight.Started || flight.Until > flight.Started + 9d) return;
                incomingFlights.Add(actor, flight);
            }
            for (int i = 0; i < PoleTotal; i++)
            {
                var state = poles[i]; var next = incoming[i];
                state.Revision = next.Revision; state.Upper = next.Upper; state.Lower = next.Lower;
                state.Angle = next.Angle; state.Speed = next.Speed; state.Drive = next.Drive; state.Until = next.Until; state.SampledAt = next.SampledAt;
            }
            flights.Clear(); foreach (var pair in incomingFlights) flights.Add(pair.Key, pair.Value);
            var local = NetworkPlayer.Local;
            if (localAbortWaiting && local != null && !FindHeld(local, out _, out _) && !flights.ContainsKey(local.OwnerClientId))
                localAbortWaiting = false;
            // Periodic snapshots are not a rejection of an in-flight catch request.
            version = sequence; ReconcileLocal();
        }
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
