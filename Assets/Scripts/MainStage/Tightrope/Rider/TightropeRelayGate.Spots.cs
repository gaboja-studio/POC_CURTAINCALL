using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 지정 자리 해제봉(리허설 구조도, Task-097). 봉은 <see cref="Lane"/> 줄의 <see cref="Distance"/>에 걸리고,
    /// 잡고 있는 동안 <see cref="TargetLane"/> 줄의 <see cref="GapFrom"/>~<see cref="GapTo"/> 끊김이 이어진다.
    /// 끊김 자체는 부르는 쪽이 <see cref="TightropeCourse.BlockSegment"/>로 만들고 <see cref="TightropeRelayGate.IsLaneConnected"/>를 보고 연다.
    /// </summary>
    public readonly struct RelaySpot
    {
        public readonly int Lane, TargetLane;
        public readonly float Distance, GapFrom, GapTo;
        public RelaySpot(int lane, float distance, int targetLane, float gapFrom, float gapTo)
        { Lane = lane; Distance = distance; TargetLane = targetLane; GapFrom = gapFrom; GapTo = gapTo; }
    }

    public sealed partial class TightropeRelayGate
    {
        /// <summary>지정 자리는 배치만 바꾸며 공통 봉의 30초 제한과 조작을 사용한다.</summary>
        List<RelaySpot> spotList;

        bool UsesSpots => spotList != null;
        public bool TryGetSpotLaneGap(int lane, out float from, out float to)
        {
            from = to = 0f;
            if (!UsesSpots) return false;
            foreach (var spot in spotList)
                if (spot.TargetLane == lane) { from = spot.GapFrom; to = spot.GapTo; return true; }
            return false;
        }
        // 지정 연습 배치도 원본 봉의 점유·입력·복제 상태를 사용한다. 연습에서는 유지 시간을 제한하지 않는다.
        bool UnlimitedHold => false;
        bool HasGate => course != null && (course.HasRelayGap || UsesSpots);

        /// <summary>코스 값의 출발·교대 봉 4개 대신 이 자리들에 봉을 놓는다. 빈 목록이면 코스 값으로 돌아간다.</summary>
        public void UseSpots(IReadOnlyList<RelaySpot> spots)
        {
            Deactivate(); ResetState(); DestroyViews();
            foreach (var station in stations) if (station?.Pole != null) Destroy(station.Pole.gameObject);
            spotList = spots != null && spots.Count > 0 ? new List<RelaySpot>(spots) : null;
            stations = new Station[UsesSpots ? spotList.Count : 4];
            CreateStations();
            PlacePoles();
            if (session != null && session.IsHost) { version++; Publish(); }
        }

        void CreateStations()
        {
            if (course == null) course = GetComponent<TightropeCourse>();
            if (common == null) common = HumanCannonPoleSystem.Ensure(gameObject);
            for (int i = 0; i < stations.Length; i++)
            {
                var root = new GameObject($"RelayPolePhysics{i}");
                root.transform.SetParent(transform, false);
                stations[i] = new Station { Far = !UsesSpots && i >= 2, Pole = root.AddComponent<HumanCannonPole>() };
                stations[i].Pole.IsSwitch = true;
                stations[i].Pole.InteractionAvailable = false;
                stations[i].Index = common.Register(stations[i].Pole);
            }
        }

        /// <summary>봉 아래 줄 위 거리. 지정 자리면 그 거리, 아니면 출발 봉은 끊김 앞·교대 봉은 끊김 뒤.</summary>
        float StationDistance(int index) => UsesSpots ? spotList[index].Distance : stations[index].Far ? ExitDistance : EntryDistance;

        /// <summary>지정 자리 봉이 잇는 끊김(표시용).</summary>
        bool TryGetSpotGap(int index, out int lane, out float from, out float to)
        {
            lane = -1; from = to = 0f;
            if (!UsesSpots || index < 0 || index >= spotList.Count) return false;
            var spot = spotList[index];
            lane = spot.TargetLane; from = spot.GapFrom; to = spot.GapTo;
            return true;
        }

        string SpotHint(NetworkPlayer local)
        {
            int held = HeldPole(local);
            if (held >= 0)
            {
                if (common.IsReleaseBlockedByPassenger(local.OwnerClientId))
                    return $"{LaneName(OpenedLane(held))} 연결 중 · 아래 동료가 먼저 손을 놓아야 합니다";
                return $"{LaneName(OpenedLane(held))} 연결 중 · 동료가 건널 때까지 잡고 있으세요\nSpace / F 길게 놓기";
            }
            for (int i = 0; i < PoleCount; i++)
                if (CanUsePole(i, local)) return $"F로 봉 잡기 · 잡는 동안 {LaneName(spotList[i].TargetLane)}이 이어져요";
            return null;
        }
    }
}
