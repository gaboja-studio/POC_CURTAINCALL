using System;
using System.Collections.Generic;

namespace CurtainCall.HumanCannon
{
    public enum CannonAction : byte { Sync, Board, Launch, Cancel, Release, Land, Reset }

    /// <summary>대포 한 자리의 호스트 판정. 입력 시점의 revision이 달라지면 오래된 요청을 버린다.</summary>
    public sealed class CannonSeatState
    {
        public const ulong Nobody = ulong.MaxValue;
        public ulong Occupant { get; private set; } = Nobody;
        public uint Revision { get; private set; }
        public bool IsOccupied => Occupant != Nobody;

        public bool TryApply(CannonAction action, ulong actor, uint expectedRevision)
        {
            if (actor == Nobody || expectedRevision != Revision) return false;
            switch (action)
            {
                case CannonAction.Board:
                    if (IsOccupied) return false;
                    Occupant = actor;
                    break;
                case CannonAction.Launch:
                case CannonAction.Cancel:
                case CannonAction.Release:
                    if (Occupant != actor) return false;
                    Occupant = Nobody;
                    break;
                default:
                    return false;
            }
            Revision++;
            return true;
        }

        public bool Receive(uint revision, ulong occupant)
        {
            if (revision < Revision) return false;
            Revision = revision;
            Occupant = occupant;
            return true;
        }

        public void Reset()
        {
            // 빈 자리도 이전 탑승 요청을 무효화해야 한다.
            Occupant = Nobody;
            Revision++;
        }
    }

    /// <summary>현재 비행의 발사 식별자. 재시작 전이나 다른 비행의 늦은 착지 요청은 무시한다.</summary>
    public sealed class CannonFlightState
    {
        readonly Dictionary<ulong, (int StationIndex, uint LaunchRevision, uint Segment)> flights = new();
        readonly List<ulong> expired = new();

        public bool Contains(ulong actor) => flights.ContainsKey(actor);

        public void Begin(ulong actor, int stationIndex, uint launchRevision) =>
            flights[actor] = (stationIndex, launchRevision, 0);

        public bool Matches(ulong actor, int stationIndex, uint launchRevision, uint segment) =>
            flights.TryGetValue(actor, out var flight) && flight.StationIndex == stationIndex
                && flight.LaunchRevision == launchRevision && flight.Segment == segment;

        public bool TryRebound(ulong actor, int stationIndex, uint launchRevision, uint segment)
        {
            if (!Matches(actor, stationIndex, launchRevision, segment)) return false;
            flights[actor] = (stationIndex, launchRevision, segment + 1);
            return true;
        }

        public bool TryComplete(ulong actor, int stationIndex, uint launchRevision, uint segment = 0)
        {
            if (!Matches(actor, stationIndex, launchRevision, segment)) return false;
            flights.Remove(actor);
            return true;
        }

        public void Remove(ulong actor) => flights.Remove(actor);
        public void Clear() => flights.Clear();

        public void RemoveWhere(Predicate<ulong> predicate)
        {
            expired.Clear();
            foreach (ulong actor in flights.Keys)
                if (predicate(actor)) expired.Add(actor);
            foreach (ulong actor in expired) flights.Remove(actor);
            expired.Clear();
        }
    }

    /// <summary>탑승 완료 전부터 누르고 있던 F는 발사/취소로 재사용하지 않는다.</summary>
    public sealed class CannonInputLatch
    {
        bool ready;
        int releasedFrames;
        public void Reset() { ready = false; releasedFrames = 0; }

        public CannonAction Read(bool tapped, bool held, float progress)
        {
            if (!ready)
            {
                // 누르기 시작 프레임도 progress가 0일 수 있다. 두 번 연속 중립이어야
                // 탑승 응답 전부터 누르던 F가 아니라는 것을 확인할 수 있다.
                releasedFrames = !tapped && !held && progress == 0f ? releasedFrames + 1 : 0;
                ready = releasedFrames >= 2;
                return CannonAction.Sync;
            }
            // 합성 입력에서도 긴 누르기가 발사보다 우선한다.
            if (held) return CannonAction.Cancel;
            return tapped ? CannonAction.Launch : CannonAction.Sync;
        }
    }
}
