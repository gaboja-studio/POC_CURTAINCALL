using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeTrapeze
    {
#if UNITY_EDITOR
        public event System.Action<string> RequestObserved;
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void ObserveRequest(ulong actor, string phase, Command action, int pole)
        {
#if UNITY_EDITOR
            if (RequestObserved == null || action == Command.Swing || action == Command.Sync) return;
            var p = Find(actor);
            var m = p != null ? p.GetComponent<PlayerMover>() : null;
            RequestObserved.Invoke($"{phase} actor={actor} command={action} pole={pole} active={Active} airborne={p?.IsAirborne} control={m?.ControlEnabled} pass={m?.PassThrough} eligible={(p != null && Eligible(p))} position={p?.transform.position} grab={(p != null && pole >= 0 && CanGrab(p,pole))}");
#endif
        }
        const double RequestPositionGrace = .5d;
        sealed class PendingRequest
        {
            public Command Action;
            public int Round, Pole;
            public uint Revision, Token;
            public double Until;
            public bool Armed;
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
            {
                if (PendingRequestValid(actor, previous)) TryCompletePendingRequest(actor, previous);
                return; // 중복 F는 이미 받은 입력과 원래 유효 시간을 덮어쓰지 않는다.
            }
            pendingRequests.Remove(actor);
            var request = new PendingRequest { Action = action, Round = round, Pole = pole,
                Revision = revision, Token = token, Until = until };
            if (!PendingRequestValid(actor, request)) { ObserveRequest(actor, "invalid", action, pole); Publish(actor); return; }
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
                { ObserveRequest(actor, "expired", request.Action, request.Pole); pendingRequests.Remove(actor); if (Alive(actor)) Publish(actor); continue; }
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
            if (flights.TryGetValue(actor, out var flight))
            {
                if (!request.Armed)
                {
                    if (request.Action == Command.Grab ? !CanGrab(player, request.Pole) : !CanAttach(player, request.Pole)) return false;
                    request.Armed = true;
                    request.Until = flight.Until; // 범위 안에서 받은 F는 현재 비행의 같은 토큰으로만 확정한다.
                }
                if (!Eligible(player) || !InCatchRange(player, request.Pole, request.Action == Command.Attach)) return false;
            }
            if (!request.Armed && (request.Action == Command.Grab ? !CanGrab(player, request.Pole) : !CanAttach(player, request.Pole))) return false;
            var stack = PiggybackSystem.Current;
            void CompleteCatch()
            {
                if (request.Action == Command.Grab)
                {
                    state.Upper = actor; state.Angle = state.Speed = state.Drive = 0f;
                    state.Until = Clock + HoldSeconds; state.LastInput = state.SampledAt = Clock;
                }
                else state.Lower = actor;
                stack?.ConsumeTrapezeEntry(player);
                ObserveRequest(actor, "accepted", request.Action, request.Pole);
                state.Revision++; pendingRequests.Remove(actor); flights.Remove(actor); Publish();
            }
            if (stack != null && stack.IsInStack(player)) return stack.TryReleaseTopForTrapeze(player, CompleteCatch);
            CompleteCatch();
            return true;
        }

        void ClearPendingRequests() { pendingRequests.Clear(); pendingActors.Clear(); }
    }
}
