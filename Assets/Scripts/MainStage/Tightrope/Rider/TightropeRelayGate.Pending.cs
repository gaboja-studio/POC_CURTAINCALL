using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeRelayGate
    {
        const double RequestPositionGrace = .35d;
        sealed class PendingGrab
        {
            public int Round, Pole;
            public uint Revision;
            public double Until;
        }
        readonly Dictionary<ulong, PendingGrab> pendingGrabs = new();
        readonly List<ulong> pendingGrabActors = new();

        void SubmitGrab(ulong actor, int pole, uint revision)
        {
            double until = Clock + RequestPositionGrace;
            if (pendingGrabs.TryGetValue(actor, out var old) && old.Round == round
                && old.Pole == pole && old.Revision == revision) until = old.Until;
            pendingGrabs.Remove(actor);
            var request = new PendingGrab { Round = round, Pole = pole, Revision = revision, Until = until };
            if (!IsPendingGrabValid(actor, request)) { Publish(actor); return; }
            // 신뢰성 입력이 소유자의 위치/착지 정보보다 먼저 도착할 수 있다.
            if (!CompleteGrab(actor, request)) pendingGrabs[actor] = request;
        }

        bool IsPendingGrabValid(ulong actor, PendingGrab request)
        {
            var player = Find(actor);
            return IsActive && session != null && session.IsHost && request.Round == round && Clock <= request.Until
                && player != null && player.IsSpawned && player.State == PlayerState.Normal && run.IsParticipantReady(player)
                && !IsHeld(player) && IsPoleAvailable(request.Pole) && stations[request.Pole].State.Revision == request.Revision;
        }

        bool CompleteGrab(ulong actor, PendingGrab request)
        {
            if (!CanUsePole(request.Pole, Find(actor)) || !stations[request.Pole].State.TryGrab(
                actor, request.Revision, Clock, course.Shape.RelayHoldSeconds)) return false;
            pendingGrabs.Remove(actor); version++; ApplyConnections(); Publish();
            return true;
        }

        void ProcessPendingGrabs()
        {
            pendingGrabActors.Clear(); pendingGrabActors.AddRange(pendingGrabs.Keys);
            foreach (ulong actor in pendingGrabActors)
            {
                if (!pendingGrabs.TryGetValue(actor, out var request)) continue;
                if (!IsPendingGrabValid(actor, request))
                { pendingGrabs.Remove(actor); if (Find(actor) != null) Publish(actor); continue; }
                CompleteGrab(actor, request);
            }
            pendingGrabActors.Clear();
        }

        void ClearPendingGrabs() { pendingGrabs.Clear(); pendingGrabActors.Clear(); }
    }
}
