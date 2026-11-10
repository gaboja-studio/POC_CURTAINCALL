using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 코스 값의 끊김 하나 대신 지정한 자리(줄·구간)마다 그네를 놓는다(리허설 구조도, Task-097).
    /// 자리의 줄 끊김은 부르는 쪽이 <see cref="TightropeCourse.BlockSegment"/>로 만든다.
    /// <see cref="SwingSpot.ChainNext"/>이면 놓은 뒤 날아가는 중 다음 자리 그네 가까이에서 F를 눌러 잡는다(공연 공중그네와 같은 조작).
    /// </summary>
    public readonly struct SwingSpot
    {
        public readonly int Lane;
        public readonly float From, To;
        public readonly bool ChainNext;
        public SwingSpot(int lane, float from, float to, bool chainNext = false)
        { Lane = lane; From = from; To = to; ChainNext = chainNext; }
    }

    public sealed partial class TightropeSwingPole
    {
        bool hasSpot;
        int spotIndex;
        float spotFrom, spotTo;
        TightropeSwingPole next;
        bool chainTarget;
        List<SwingSpot> spotList;


        float GapFrom => hasSpot ? spotFrom : course.SwingGapFrom;
        float GapTo => hasSpot ? spotTo : course.SwingGapTo;
        bool HasGap => hasSpot || course.HasSwingGapOnLane(assignedLane);
        float EntryDistance => GapFrom - SwingPoleMath.EntryOffset;
        /// <summary>놓은 뒤 내려앉을 줄이 있는지. 다음 그네로 이어지면 줄 대신 그네를 잡는다.</summary>
        bool LandingUsable => next != null || course.IsLaneUsable(assignedLane, LandingDistance);
        /// <summary>줄에서 잡을 자리가 있는지. 앞 그네에서 날아와 잡는 그네는 줄 대신 공중에서 잡는다.</summary>
        bool EntryUsable => chainTarget || course.IsLaneUsable(assignedLane, EntryDistance);
        string Channel => hasSpot ? $"spot{spotIndex}" : assignedLane.ToString();


        /// <summary>코스 값 대신 이 자리들에 그네를 놓는다(뿌리 그네에서 부른다). 빈 목록이면 코스 값으로 돌아간다.</summary>
        public void UseSpots(IReadOnlyList<SwingSpot> spots)
        {
            if (coordinator != null) { coordinator.UseSpots(spots); return; }
            foreach (var pole in poles)
                if (pole != this && pole != null) { pole.gameObject.SetActive(false); Destroy(pole.gameObject); }
            poles.Clear();
            spotList = spots != null && spots.Count > 0 ? new List<SwingSpot>(spots) : null;
            common?.Deactivate(sharedIndex); DestroyViews();
            hasSpot = false; next = null; assignedLane = 0;
            EnsureLanePoles();
            PlacePivot();
        }

        void EnsureSpotPoles()
        {
            if (poles.Count == 0) { poles.Add(this); Assign(this, 0); }
            while (poles.Count < spotList.Count)
            {
                var child = new GameObject($"Swing pole spot {poles.Count + 1}");
                child.SetActive(false); child.transform.SetParent(transform, false);
                var pole = child.AddComponent<TightropeSwingPole>();
                pole.coordinator = this;
                Assign(pole, poles.Count);
                poles.Add(pole); child.SetActive(true);
            }
            foreach (var pole in poles) pole.chainTarget = false;
            for (int i = 0; i < poles.Count; i++)
            {
                poles[i].next = spotList[i].ChainNext && i + 1 < poles.Count ? poles[i + 1] : null;
                if (poles[i].next != null) poles[i].next.chainTarget = true;
            }
        }

        void Assign(TightropeSwingPole pole, int index)
        {
            var spot = spotList[index];
            pole.hasSpot = true; pole.spotIndex = index; pole.assignedLane = spot.Lane;
            pole.spotFrom = Mathf.Min(spot.From, spot.To); pole.spotTo = Mathf.Max(spot.From, spot.To);
        }

        /// <summary>줄 위 그 위치에서 쓸 그네. 같은 줄에 여러 개면 매달림 위치가 가장 가까운 것.</summary>
        TightropeSwingPole SpotFor(int slot, float distance)
        {
            TightropeSwingPole best = null;
            float bestGap = float.MaxValue;
            foreach (var pole in Root.poles)
            {
                if (pole == null || pole.assignedLane != slot) continue;
                float gap = Mathf.Abs(distance - pole.EntryDistance);
                if (gap < bestGap) { best = pole; bestGap = gap; }
            }
            return best;
        }

        /// <summary>날아가는 중 공중에서 잡을 다음 그네가 있다.</summary>
        public bool ChainsToNext => next != null;
        /// <summary>지금 F를 누르면 잡을 수 있는 다음 그네의 봉. 범위 밖이면 null.</summary>
        public Transform AirCatchVisual => InAirCatchRange ? next.PoleVisual : null;

        bool InAirCatchRange => next != null && next.CanGrabPole(NetworkPlayer.Local);
    }
}
