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
            if (!CanUsePole(request.Pole, Find(actor))) return false;
            // 끈끈이 장갑이면 잡기 제한 시간을 늘린다(Task-092). 잡기에 실패하면 다음 확인에서 소모 없이 정리된다.
            // 지정 자리 봉(리허설)은 0 = 무제한 점유로 넘긴다. 큰 유한값은 클라이언트 스냅샷 검증(유지 시각 상한)에서
            // 버려져 다른 화면에 줄 연결이 반영되지 않았다(2026-11-04 2인 플레이테스트).
            float hold = UsesSpots ? 0f : course.Shape.RelayHoldSeconds + (float)stickyGloves.Begin(actor, Clock, course.Shape.RelayHoldSeconds);
            if (!stations[request.Pole].State.TryGrab(actor, request.Revision, Clock, hold)) return false;
            // 잡을 때마다 이 봉이 열 줄을 새로 고른다(호스트만). 스냅샷으로 모두에게 같은 줄을 알린다.
            stations[request.Pole].Opened = PickLane(request.Pole);
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
