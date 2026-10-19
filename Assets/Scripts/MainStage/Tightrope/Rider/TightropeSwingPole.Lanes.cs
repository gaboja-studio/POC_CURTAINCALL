using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeSwingPole
    {
        int assignedLane;
        TightropeSwingPole coordinator;
        readonly List<TightropeSwingPole> poles = new();
        TightropeSwingPole Root => coordinator != null ? coordinator : this;

        void EnsureLanePoles()
        {
            if (coordinator != null || course == null) return;
            if (poles.Count == 0) poles.Add(this);
            while (poles.Count < course.LaneCount)
            {
                var child = new GameObject($"Swing pole lane {poles.Count + 1}");
                child.SetActive(false); child.transform.SetParent(transform, false);
                var pole = child.AddComponent<TightropeSwingPole>();
                pole.assignedLane = poles.Count; pole.coordinator = this;
                poles.Add(pole); child.SetActive(true);
            }
        }

        /// <summary>0부터 세는 줄의 독립 봉. 배치 대상으로 설정하지 않은 줄에서는 잡을 수 없다.</summary>
        public TightropeSwingPole ForLane(int slot) => slot >= 0 && slot < Root.poles.Count ? Root.poles[slot] : null;

        public TightropeSwingPole ForPlayer(NetworkPlayer player)
        {
            if (player != null)
            {
                foreach (var pole in Root.poles)
                    if (pole.OwnHeld(player) || pole.OwnFlight(player)) return pole;
                if (course != null)
                    return ForLane(course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position))) ?? this;
            }
            return this;
        }
        TightropeSwingPole LocalPole => coordinator != null ? this : ForPlayer(NetworkPlayer.Local);

        bool OwnHeld(NetworkPlayer player) => Active && mode == PoleMode.Holding && player != null && holder == player.OwnerClientId;
        bool OwnFlight(NetworkPlayer player) => Active && flightMover != null && player != null && player == flightPlayer;
        public bool IsHeld(NetworkPlayer player)
        {
            foreach (var pole in Root.poles) if (pole.OwnHeld(player)) return true;
            return false;
        }
        public bool IsInFlight(NetworkPlayer player)
        {
            foreach (var pole in Root.poles) if (pole.OwnFlight(player)) return true;
            return false;
        }
        public bool IsCrossing(NetworkPlayer player)
        {
            foreach (var pole in Root.poles)
                if (pole.OwnHeld(player) || pole.OwnFlight(player) || (pole.Active && player != null
                    && pole.flights.TryGetValue(player.OwnerClientId, out double until) && pole.Clock <= until)) return true;
            return false;
        }
        string LatestFeedback()
        {
            TightropeSwingPole latest = null;
            foreach (var pole in Root.poles)
                if (pole.Clock < pole.feedbackUntil && (latest == null || pole.feedbackUntil > latest.feedbackUntil)) latest = pole;
            return latest != null ? latest.feedback : null;
        }
    }
}
