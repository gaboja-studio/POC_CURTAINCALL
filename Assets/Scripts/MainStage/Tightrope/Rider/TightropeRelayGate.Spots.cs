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
        /// <summary>지정 자리 봉은 시간 제한 없이 잡고 있는 동안 계속 잇는다(리허설, 사용자 결정 2026-11-02).</summary>
        List<RelaySpot> spotList;

        bool UsesSpots => spotList != null;
        // 리허설도 공연의 네 봉·무작위 연결·교대 규칙을 쓴다. 연습 모드에서는 유지 시간만 제한하지 않는다.
        bool UnlimitedHold => UsesSpots || (run != null && run.PracticeMode);
        bool HasGate => course != null && (course.HasRelayGap || UsesSpots);

        /// <summary>코스 값의 출발·교대 봉 4개 대신 이 자리들에 봉을 놓는다. 빈 목록이면 코스 값으로 돌아간다.</summary>
        public void UseSpots(IReadOnlyList<RelaySpot> spots)
        {
            RestoreBinding(false); ResetState(); DestroyViews();
            foreach (var station in stations) if (station?.Pole != null) Destroy(station.Pole.gameObject);
            spotList = spots != null && spots.Count > 0 ? new List<RelaySpot>(spots) : null;
            stations = new Station[UsesSpots ? spotList.Count : 4];
            CreateStations();
            PlacePoles();
            if (session != null && session.IsHost) { version++; Publish(); }
        }

        void CreateStations()
        {
            for (int i = 0; i < stations.Length; i++)
            {
                var root = new GameObject($"RelayPolePhysics{i}");
                root.transform.SetParent(transform, false);
                stations[i] = new Station { Far = !UsesSpots && i >= 2, Pole = root.AddComponent<HumanCannonPole>() };
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
                return $"{LaneName(OpenedLane(held))} 연결 중 · 동료가 건널 때까지 잡고 있으세요\nF / Space 놓기";
            for (int i = 0; i < PoleCount; i++)
                if (CanUsePole(i, local)) return $"F로 봉 잡기 · 잡는 동안 {LaneName(spotList[i].TargetLane)}이 이어져요";
            return null;
        }
    }
}
