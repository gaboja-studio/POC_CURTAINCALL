using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Authored, continuous service route. All poses are scene anchors, never world-coordinate teleports.</summary>
    public sealed class EscapePursuerRoute : MonoBehaviour
    {
        [Serializable]
        public sealed class Point
        {
            public string label;
            public Transform feet;
            public Transform playerPath;
            public Transform throwEnd;
            public bool reveal;
            public bool finalSprint;
            public bool jump;
            public bool exposed;
        }
        [SerializeField] Point[] points = Array.Empty<Point>();
        [SerializeField] HumanCannonSystem cannon;
        [SerializeField] HumanCannonStation firstCannon;
        [SerializeField] HumanCannonLandingZone firstWire;
        readonly HashSet<ulong> departed = new();
        readonly Dictionary<ulong, double> pressured = new();
        NetworkPlayer focus;
        double nextFocus, teamDepartedAt = -1;
        static CurtainCall.Settings.EscapeRouteSettings.PursuerTuning Tuning => CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer;
        double epoch = -1;
        float cursor, desired;
        bool entered;
        double finaleReady;

        public IReadOnlyList<Point> Points => points;
        public float Progress => cursor;
        public bool Following => entered;
        public bool Exposed => Valid && points[Mathf.Min(points.Length - 1, Mathf.FloorToInt(cursor))].exposed;
        public void ResumeAt(int point) { cursor = Mathf.Clamp(point, 0, points.Length - 1); entered = true; }
        public bool Valid => points.Length > 1;
        public Vector3 Entry => points[0].feet.position;
        public Vector3 NextPosition => points[Mathf.Min(points.Length - 1, Mathf.FloorToInt(cursor) + 1)].feet.position;
        public bool FinalSprint => Valid && points[Mathf.Min(points.Length - 1, Mathf.FloorToInt(cursor))].finalSprint;
        public string CurrentSection => Valid ? points[Mathf.Min(points.Length - 1, Mathf.FloorToInt(cursor))].label : "";

        void OnEnable()
        {
            if (cannon) cannon.LandingConfirmed += Landed;
            NetworkPlayer.Restarted += ResetRoute;
        }
        void OnDisable()
        {
            if (cannon) cannon.LandingConfirmed -= Landed;
            NetworkPlayer.Restarted -= ResetRoute;
            ResetRoute(null);
        }
        void ResetRoute(NetworkPlayer _)
        {
            epoch = -1; cursor = desired = 0; entered = false; departed.Clear();
            finaleReady = 0; pressured.Clear(); focus = null;
            nextFocus = 0; teamDepartedAt = -1;
        }
        void Landed(ulong actor, int station, uint revision, uint segment, Vector3 position, bool safe)
        {
            SynchronizeRound();
            if (safe && cannon && station >= 0 && station < cannon.StationCount && cannon.Station(station) == firstCannon
                && firstWire && firstWire.SupportsLanding(position)) departed.Add(actor);
        }
        void SynchronizeRound()
        {
            var timer = EscapeRouteTimer.Current;
            double next = timer && timer.HasStarted ? timer.StartedAt : -1;
            if (epoch == next) return;
            ResetRoute(null); epoch = next;
        }
        public bool HasDeparted(NetworkPlayer player)
        {
            SynchronizeRound();
            if (!Valid || !player) return false;
            if (departed.Contains(player.OwnerClientId)) return true;
            // Also recover state when a dev entry/checkpoint starts farther down the actual course.
            float progress = Project(PlayerFeet(player), out float distance);
            if (progress >= 1f && distance < 8f) departed.Add(player.OwnerClientId);
            return departed.Contains(player.OwnerClientId);
        }
        public bool InTransit(NetworkPlayer player) => cannon && cannon.IsFlying(player.OwnerClientId)
            && !(HumanCannonPoleSystem.Current && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0);
        public Vector3 PlayerFeet(NetworkPlayer player)
        {
            var station = cannon ? cannon.SeatedStation(player.OwnerClientId) : null;
            return station && station.Seat ? station.Seat.position : player.transform.position;
        }
        public bool AtStation(NetworkPlayer player) => cannon && cannon.SeatedStation(player.OwnerClientId);

        public bool TeamNeedsPursuit
        {
            get
            {
                var runner = SelectTarget();
                var timer = EscapeRouteTimer.Current;
                if (!runner || !timer) return false;
                if (teamDepartedAt < 0) teamDepartedAt = timer.Clock;
                return timer.Clock - teamDepartedAt >= Tuning.distractionSeconds;
            }
        }

        public void MarkPressured(NetworkPlayer player)
        {
            if (player && EscapeRouteTimer.Current)
                pressured[player.OwnerClientId] = EscapeRouteTimer.Current.Clock;
            nextFocus = 0;
        }

        public float RecentPressure(ulong actor, double now) => pressured.TryGetValue(actor, out double last)
            ? Mathf.Clamp01(1f - (float)(now - last) / Mathf.Max(.1f, Tuning.targetMemorySeconds)) : 0f;

        public NetworkPlayer SelectTarget()
        {
            SynchronizeRound();
            var timer = EscapeRouteTimer.Current;
            double now = timer ? timer.Clock : 0;
            if (focus && focus.IsSpawned && focus.State == PlayerState.Normal && now < nextFocus)
            {
                desired = Mathf.Max(cursor, Project(PlayerFeet(focus), out _));
                return focus;
            }
            NetworkPlayer best = null;
            float highest = float.NegativeInfinity;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal || !entered && !HasDeparted(player)) continue;
                float progress = Project(PlayerFeet(player), out _);
                // Progress keeps the creature with the escaping team. Recent victims get a short
                // reduction so one nearby decoy cannot monopolize every attack.
                float score = progress;
                score -= 5f * RecentPressure(player.OwnerClientId, now);
                if (player == focus) score += .5f;
                if (score > highest || score == highest && best && player.OwnerClientId < best.OwnerClientId)
                { highest = score; best = player; }
            }
            focus = best;
            nextFocus = now + Tuning.targetRefreshSeconds;
            if (best) desired = Mathf.Max(cursor, Mathf.Clamp(Project(PlayerFeet(best), out _), 0, points.Length - 1));
            return focus;
        }
        public bool TryEnter(Vector3 position)
        {
            if (entered) return true;
            if (!Valid || Vector3.Distance(position, Entry) > .65f) return false;
            entered = true; cursor = 0;
            return true;
        }
        public bool CanAim(NetworkPlayer player, Vector3 feet)
        {
            if (!entered || !player) return false;
            return (PlayerFeet(player) - feet).sqrMagnitude < Tuning.armRange * Tuning.armRange;
        }
        public Vector3 ThrowEnd
        {
            get
            {
                int i = Mathf.Min(points.Length - 2, Mathf.FloorToInt(cursor));
                return Vector3.Lerp(points[i].throwEnd.position, points[i + 1].throwEnd.position, cursor - i);
            }
        }
        public Vector3 CarryToNextBay(Vector3 from, float speed, float dt, double now)
        {
            desired = Mathf.Max(desired, Mathf.Min(points.Length - 1, Mathf.FloorToInt(cursor) + 1));
            return Advance(from, speed, speed, dt, now, out _);
        }
        public bool TryJump(out Vector3 start, out Vector3 end)
        {
            int i = Mathf.FloorToInt(cursor);
            start = end = default;
            if (!entered || i + 1 >= points.Length || !points[i+1].jump || cursor > i + .001f || desired <= cursor) return false;
            start = points[i].feet.position; end = points[i+1].feet.position; return true;
        }
        public void CompleteJump() => cursor = Mathf.Min(points.Length - 1, Mathf.FloorToInt(cursor) + 1);

        public Vector3 Advance(Vector3 from, float walkSpeed, float runSpeed, float dt, double now, out float run)
        {
            run = 0;
            if (!entered || !Valid) return from;
            int index = Mathf.Min(points.Length - 2, Mathf.FloorToInt(cursor));
            if (FinalSprint && finaleReady == 0) finaleReady = now + CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer.finalChaseLeadSeconds;
            if (FinalSprint && now < finaleReady) return from;
            float goal = Mathf.Max(cursor, desired);
            run = 1;
            float budget = Mathf.Max(0, dt * runSpeed);
            while (budget > 0 && cursor < goal)
            {
                index = Mathf.Min(points.Length - 2, Mathf.FloorToInt(cursor));
                if (points[index+1].jump) break;
                float length = Vector3.Distance(points[index].feet.position, points[index + 1].feet.position);
                float step = Mathf.Min(goal, index + 1f) - cursor;
                float consumed = Mathf.Min(budget, step * length);
                cursor += length > .001f ? consumed / length : step;
                budget -= length > .001f ? consumed : .001f;
            }
            return Position(cursor);
        }
        Vector3 Position(float progress)
        {
            int i = Mathf.Min(points.Length - 2, Mathf.FloorToInt(progress));
            return Vector3.Lerp(points[i].feet.position, points[i + 1].feet.position, progress - i);
        }
        public float Project(Vector3 feet, out float distance)
        {
            float best = float.PositiveInfinity, progress = 0;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i].playerPath.position, ab = points[i + 1].playerPath.position - a;
                float t = Mathf.Clamp01(Vector3.Dot(feet - a, ab) / Mathf.Max(.0001f, ab.sqrMagnitude));
                float d = (feet - a - ab * t).sqrMagnitude;
                if (d < best) { best = d; progress = i + t; }
            }
            distance = Mathf.Sqrt(best); return progress;
        }
        void OnDrawGizmosSelected()
        {
            for (int i = 0; i + 1 < points.Length; i++)
            {
                if (!points[i].feet || !points[i+1].feet || !points[i].playerPath || !points[i+1].playerPath) continue;
                Gizmos.color = Color.red; Gizmos.DrawLine(points[i].feet.position, points[i+1].feet.position);
                Gizmos.color = Color.cyan; Gizmos.DrawLine(points[i].playerPath.position, points[i+1].playerPath.position);
            }
        }
    }
}
