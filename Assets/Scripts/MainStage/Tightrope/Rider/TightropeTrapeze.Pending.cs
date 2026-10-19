using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeTrapeze
    {
        const double RequestPositionGrace = .35d;
        sealed class PendingRequest
        {
            public Command Action;
            public int Round, Pole;
            public uint Revision, Token;
            public double Until;
        }
        readonly Dictionary<ulong, PendingRequest> pendingRequests = new();
        readonly List<ulong> pendingActors = new();

        void SubmitPendingRequest(ulong actor, Command action, int pole, uint revision, uint token)
        {
            // Reliable requests can arrive before their owner transform or jump state.
            // Retain only the original short window; duplicate packets must not extend it.
            double until = Clock + RequestPositionGrace;
            if (pendingRequests.TryGetValue(actor, out var previous) && previous.Round == round
                && previous.Action == action && previous.Pole == pole && previous.Revision == revision && previous.Token == token)
                until = previous.Until;
            pendingRequests.Remove(actor);
            var request = new PendingRequest { Action = action, Round = round, Pole = pole,
                Revision = revision, Token = token, Until = until };
            if (!PendingRequestValid(actor, request)) { Publish(actor); return; }
            if (!TryCompletePendingRequest(actor, request)) pendingRequests[actor] = request;
        }

        void ProcessPendingRequests()
        {
            if (!Active || session == null || !session.IsHost) { ClearPendingRequests(); return; }
            pendingActors.Clear();
            pendingActors.AddRange(pendingRequests.Keys);
            foreach (ulong actor in pendingActors)
            {
                if (!pendingRequests.TryGetValue(actor, out var request)) continue;
                if (!PendingRequestValid(actor, request))
                { pendingRequests.Remove(actor); if (Alive(actor)) Publish(actor); continue; }
                TryCompletePendingRequest(actor, request);
            }
            pendingActors.Clear();
        }

        bool PendingRequestValid(ulong actor, PendingRequest request)
        {
            if (!Active || session == null || !session.IsHost || request.Round != round || Clock > request.Until || !Alive(actor)) return false;
            var player = Find(actor);
            if (FindHeld(player, out _, out _)) return false;
            bool flying = flights.TryGetValue(actor, out var flight);
            if (request.Token != (flying ? flight.Token : 0) || (flying && Clock > flight.Until)) return false;
            if (request.Action == Command.Landed) return flying;
            if (request.Pole < 0 || request.Pole >= PoleTotal) return false;
            var state = poles[request.Pole];
            if (state.Revision != request.Revision || state.Lower != Nobody) return false;
            return request.Action == Command.Grab ? state.Upper == Nobody
                : request.Action == Command.Attach && state.Upper != Nobody && Alive(state.Upper) && Clock < state.Until;
        }

        bool TryCompletePendingRequest(ulong actor, PendingRequest request)
        {
            var player = Find(actor);
            if (request.Action == Command.Landed)
            {
                if (!player.TryGetComponent(out PlayerMover mover) || !course.TryGetSupportingRope(mover, out int lane)
                    || !course.IsLaneUsable(lane, course.GetDistance(player.transform.position))
                    || Mathf.Abs(player.transform.position.y - course.transform.position.y) > .3f) return false;
                pendingRequests.Remove(actor); flights.Remove(actor);
                TightropeLandingWaves.Current?.RegisterExternalLanding(player, sharedAirborne: true); Publish();
                return true;
            }
            var state = poles[request.Pole];
            if (request.Action == Command.Grab)
            {
                if (!CanGrab(player, request.Pole)) return false;
                state.Upper = actor; state.Angle = state.Speed = state.Drive = 0f;
                state.Until = Clock + HoldSeconds; state.LastInput = state.SampledAt = Clock;
            }
            else
            {
                if (!CanAttach(player, request.Pole)) return false;
                state.Lower = actor;
            }
            state.Revision++; pendingRequests.Remove(actor); flights.Remove(actor); Publish();
            return true;
        }

        void ClearPendingRequests() { pendingRequests.Clear(); pendingActors.Clear(); }
    }
}
