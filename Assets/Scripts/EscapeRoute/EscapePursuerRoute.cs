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
        [Serializable]
        public sealed class PlayerGuide
        {
            public Transform from, to;
            public float fromProgress, toProgress;
        }
        [SerializeField] Point[] points = Array.Empty<Point>();
        [Tooltip("Optional independent player-path segments mapped onto the continuous service-route progress.")]
        [SerializeField] PlayerGuide[] playerGuides = Array.Empty<PlayerGuide>();
        [SerializeField] HumanCannonSystem cannon;
        [SerializeField] HumanCannonStation firstCannon;
        [Tooltip("Additional entrance cannons that land on the same first wire. The original firstCannon remains valid.")]
        [SerializeField] HumanCannonStation[] additionalFirstCannons = Array.Empty<HumanCannonStation>();
        [SerializeField] HumanCannonLandingZone firstWire;
        readonly HashSet<ulong> departed = new();
        readonly Dictionary<ulong, double> pressured = new();
        readonly Dictionary<ulong, ProgressWatch> progressWatch = new();
        readonly Dictionary<ulong, float> trustedProgress = new();
        float[] pathMetres;
        double nextProgressSample;
        double lastProgressClock = double.NaN;
        NetworkPlayer focus;
        bool focusInHall;
        double nextFocus, teamDepartedAt = -1;
        static CurtainCall.Settings.EscapeRouteSettings.PursuerTuning Tuning => CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer;
        double epoch = -1;
        float cursor, desired;
        int jumpDestination = -1;
        bool entered;
        double finaleReady;

        struct ProgressWatch
        {
            public float Furthest;
            public double AdvancedAt;
        }

        public IReadOnlyList<Point> Points => points;
        public IReadOnlyList<PlayerGuide> PlayerGuides => playerGuides ?? Array.Empty<PlayerGuide>();
        public float Progress => cursor;
        public bool Following => entered;
        internal bool TargetInHall => focusInHall;
        internal bool ReturningToHall => entered && (!focus || focusInHall);
        int CurrentPoint => Mathf.Clamp(Mathf.FloorToInt(cursor), 0, points.Length - 1);
        public bool Exposed => Valid && points[CurrentPoint].exposed;
        public void ResumeAt(int point) { cursor = Mathf.Clamp(point, 0, points.Length - 1); entered = true; }
        // A stationary rider can be between authored player markers. Travel to a real
        // emergence bay instead of stopping at the corresponding point behind an opaque wall.
        public void ApproachEncounter(int point)
        {
            if (entered && Valid) desired = Mathf.Clamp(point, 0, points.Length - 1);
        }
        public bool Valid => points.Length > 1;
        public Vector3 Entry => points[0].feet.position;
        int NextPoint => Mathf.Clamp(desired < cursor ? Mathf.CeilToInt(cursor) - 1
            : Mathf.FloorToInt(cursor) + 1, 0, points.Length - 1);
        public Vector3 NextPosition => points[NextPoint].feet.position;

        internal bool AlignReachedAnchor(Vector3 position)
        {
            if (!entered || !Valid) return false;
            float goal = Mathf.Clamp(desired, 0, points.Length - 1);
            bool reverse = goal < cursor;
            int next = NextPoint;
            if (cursor == goal || (reverse ? next < goal : next > goal)
                || (position - points[next].feet.position).sqrMagnitude > .0001f * .0001f) return false;
            int edge = Mathf.Clamp(reverse ? Mathf.CeilToInt(cursor) : Mathf.FloorToInt(cursor) + 1, 0, points.Length - 1);
            if (points[edge].jump) return false; // Only CompleteJump may cross an airborne edge.
            // World-space interpolation can already equal this anchor while its float cursor
            // remains 0.99999964. Align only the cursor; never move the actor or skip a wall.
            cursor = next;
            return true;
        }
        public bool FinalSprint => Valid && points[CurrentPoint].finalSprint;
        public string CurrentSection => Valid ? points[CurrentPoint].label : "";

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
            nextFocus = 0; teamDepartedAt = -1; focusInHall = false;
            nextProgressSample = 0;
            lastProgressClock = double.NaN;
            pathMetres = null;
            progressWatch.Clear();
            trustedProgress.Clear();
            jumpDestination = -1;
        }
        void Landed(ulong actor, int station, uint revision, uint segment, Vector3 position, bool safe)
        {
            SynchronizeRound();
            if (safe && cannon && station >= 0 && station < cannon.StationCount && IsFirstCannon(cannon.Station(station))
                && firstWire && firstWire.SupportsLanding(position)) departed.Add(actor);
        }
        bool IsFirstCannon(HumanCannonStation station)
        {
            if (!station) return false;
            if (station == firstCannon) return true;
            if (additionalFirstCannons != null)
                foreach (var entry in additionalFirstCannons) if (entry == station) return true;
            return false;
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
            if (progress >= 1f && distance < 8f)
            { departed.Add(player.OwnerClientId); trustedProgress[player.OwnerClientId] = progress; }
            return departed.Contains(player.OwnerClientId);
        }

        // Projection onto a distant floor is not evidence that an entrant reached that floor.
        // Keep a runner's last nearby sample through flights and wide decks; only a grounded
        // return to the actual entrance volume hands that runner back to hall navigation.
        internal float PlayerProgress(NetworkPlayer player, out bool inHall)
        {
            SynchronizeRound();
            inHall = true;
            if (!Valid || !player) return 0;
            var feet = PlayerFeet(player);
            var timer = EscapeRouteTimer.Current;
            var poles = HumanCannonPoleSystem.Current;
            bool airborne = InTransit(player) || player.IsAirborne || poles && poles.HeldBy(player.OwnerClientId) >= 0;
            if (!airborne && timer && timer.StartArea && timer.StartArea.enabled && EscapeEntrance.Contains(timer.StartArea, feet))
            { trustedProgress.Remove(player.OwnerClientId); return 0; }
            if (!HasDeparted(player)) return 0;
            float progress = Project(feet, out float distance);
            bool known = trustedProgress.TryGetValue(player.OwnerClientId, out float trusted);
            if (distance <= 12f && (known || distance < 8f && progress >= 1f))
            {
                trustedProgress[player.OwnerClientId] = progress;
                trusted = progress; known = true;
            }
            // Departure history stays intact across hall visits, but it must not immediately
            // reclassify a returned runner still walking between the hall and first cannon.
            inHall = !known;
            return known ? trusted : 0;
        }

        internal bool IsRouteRunner(NetworkPlayer player)
        { PlayerProgress(player, out bool inHall); return !inHall; }

        internal bool AtHallEntry(Vector3 position) => ReturningToHall && cursor <= .001f
            && jumpDestination < 0 && Vector3.Distance(position, Entry) <= .65f;

        internal bool TryLeaveAtEntry(Vector3 position, bool navMeshConnected)
        {
            if (!navMeshConnected || !AtHallEntry(position)) return false;
            entered = false; cursor = desired = 0; nextFocus = 0;
            return true;
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
                if (!runner || focusInHall || !timer) return false;
                if (teamDepartedAt < 0) teamDepartedAt = timer.Clock;
                return timer.Clock - teamDepartedAt >= Tuning.distractionSeconds;
            }
        }

        public void MarkPressured(NetworkPlayer player)
        {
            if (player && EscapeRouteTimer.Current)
            {
                double now = EscapeRouteTimer.Current.Clock;
                SampleProgress(now);
                pressured[player.OwnerClientId] = now;
            }
            nextFocus = 0;
        }

        public float RecentPressure(ulong actor, double now) => pressured.TryGetValue(actor, out double last)
            ? Mathf.Clamp01(1f - (float)(now - last) / Mathf.Max(.1f, Tuning.targetMemorySeconds)) : 0f;

        internal void SampleProgress(double now)
        {
            SynchronizeRound();
            if (!Valid || epoch < 0) return;
            if (now < lastProgressClock)
            {
                // Rebase only temporal observations after a clock correction. Route position,
                // departure history and trusted airborne progress must remain physical.
                progressWatch.Clear(); pressured.Clear();
                nextFocus = nextProgressSample = 0; teamDepartedAt = -1;
            }
            lastProgressClock = now;
            if (now < nextProgressSample) return;
            nextProgressSample = now + .5;
            if (pathMetres == null || pathMetres.Length != points.Length)
            {
                pathMetres = new float[points.Length];
                for (int i = 1; i < pathMetres.Length; i++)
                    pathMetres[i] = pathMetres[i - 1] + Vector3.Distance(points[i - 1].playerPath.position, points[i].playerPath.position);
            }
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                float progress = PlayerProgress(player, out bool inHall);
                // An entrant has real progress zero, even when a distant guide projects to
                // a high route index. Observe their waiting time without inventing travel.
                if (!inHall)
                {
                    Project(PlayerFeet(player), out float distance);
                    if (distance > 12f) continue;
                }
                int edge = Mathf.Min(points.Length - 2, Mathf.FloorToInt(progress));
                float metres = inHall ? 0 : Mathf.Lerp(pathMetres[edge], pathMetres[edge + 1], progress - edge);
                if (!progressWatch.TryGetValue(player.OwnerClientId, out var prior)
                    || metres >= prior.Furthest + Tuning.pressureProgressMetres)
                    progressWatch[player.OwnerClientId] = new ProgressWatch { Furthest = metres, AdvancedAt = now };
            }
        }

        internal float Escalation(NetworkPlayer player, double now)
        {
            if (epoch < 0 || !player || !player.IsSpawned || player.State != PlayerState.Normal) return 0f;
            double idle = progressWatch.TryGetValue(player.OwnerClientId, out var watch) ? now - watch.AdvancedAt : 0;
            return PressureAt(now - epoch, idle, Tuning.pressureRunGrace, Tuning.pressureIdleGrace, Tuning.pressureRiseSeconds);
        }

        internal static float PressureAt(double elapsed, double idle, float runGrace, float idleGrace, float rise)
            => Mathf.Clamp01((float)(Math.Max(elapsed - runGrace, idle - idleGrace) / Math.Max(1f, rise)));

        double OverduePressure(NetworkPlayer player, double now)
        {
            if (!progressWatch.TryGetValue(player.OwnerClientId, out var watch)) return -1;
            double lastActivity = watch.AdvancedAt;
            if (pressured.TryGetValue(player.OwnerClientId, out double lastAttack))
                lastActivity = Math.Max(lastActivity, lastAttack);
            double neglected = Math.Max(0, now - lastActivity);
            return neglected >= Tuning.pressureIdleGrace + Tuning.pressureRiseSeconds ? neglected : -1;
        }

        public NetworkPlayer SelectTarget()
        {
            SynchronizeRound();
            var timer = EscapeRouteTimer.Current;
            double now = timer ? timer.Clock : 0;
            SampleProgress(now);
            var boss = EscapePursuerBoss.Current;
            bool prioritizeRear = boss && boss.HasRunnerBehindFinale;
            if (focus && focus.IsSpawned && focus.State == PlayerState.Normal && now < nextFocus)
            {
                float progress = PlayerProgress(focus, out bool inHall);
                if (inHall == focusInHall)
                {
                    desired = TravelGoal(progress);
                    return focus;
                }
            }
            NetworkPlayer best = null;
            bool bestInHall = false;
            float highest = float.NegativeInfinity;
            double longestNeglect = -1;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                float progress = PlayerProgress(player, out bool inHall);
                // Progress keeps the creature with the escaping team. Recent victims get a short
                // reduction so one nearby decoy cannot monopolize every attack.
                float score = prioritizeRear ? -progress : progress;
                score -= 5f * RecentPressure(player.OwnerClientId, now);
                score += 2f * Escalation(player, now);
                if (player == focus) score += .5f;
                // A bounded progress bonus cannot overcome a fifty-point lead. Once the
                // existing idle pressure has fully risen, serve the oldest neglected runner.
                // Selection and travel never refresh this age; only progress or an attack do.
                double neglected = OverduePressure(player, now);
                if (!best || neglected > longestNeglect || neglected == longestNeglect
                    && (score > highest || score == highest && player.OwnerClientId < best.OwnerClientId))
                { longestNeglect = neglected; highest = score; best = player; bestInHall = inHall; }
            }
            focus = best;
            focusInHall = bestInHall;
            nextFocus = now + Tuning.targetRefreshSeconds;
            desired = best ? TravelGoal(PlayerProgress(best, out _)) : 0;
            return focus;
        }
        float TravelGoal(float projectedProgress)
        {
            float progress = Mathf.Clamp(projectedProgress, 0, points.Length - 1);
            int next = Mathf.Min(points.Length - 1, Mathf.FloorToInt(progress) + 1);
            // A gap has two supported ends. Never reverse toward a point in mid-air.
            return points[next].jump ? Mathf.Round(progress) : progress;
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
                int i = Mathf.Clamp(Mathf.FloorToInt(cursor), 0, points.Length - 2);
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
            start = end = default;
            int i = Mathf.RoundToInt(cursor);
            int direction = desired < cursor ? -1 : 1;
            int next = i + direction;
            if (!entered || Mathf.Abs(cursor - i) > .001f || Mathf.Abs(desired - cursor) < .001f
                || next < 0 || next >= points.Length || !points[Mathf.Max(i, next)].jump) return false;
            jumpDestination = next;
            start = points[i].feet.position; end = points[next].feet.position; return true;
        }
        public void CompleteJump()
        {
            if (jumpDestination >= 0) cursor = jumpDestination;
            jumpDestination = -1;
        }

        public Vector3 Advance(Vector3 from, float walkSpeed, float runSpeed, float dt, double now, out float run)
        {
            run = 0;
            if (!entered || !Valid) return from;
            cursor = Mathf.Clamp(cursor, 0, points.Length - 1);
            if (FinalSprint && finaleReady == 0) finaleReady = now + CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer.finalChaseLeadSeconds;
            if (FinalSprint && now < finaleReady) return from;
            float goal = Mathf.Clamp(desired, 0, points.Length - 1);
            run = 1;
            float budget = Mathf.Max(0, dt * runSpeed);
            while (budget > 0 && cursor != goal)
            {
                bool forward = goal > cursor;
                int index = forward ? Mathf.Min(points.Length - 2, Mathf.FloorToInt(cursor))
                    : Mathf.Max(0, Mathf.CeilToInt(cursor) - 1);
                if (points[index+1].jump) break;
                float length = Vector3.Distance(points[index].feet.position, points[index + 1].feet.position);
                float boundary = forward ? Mathf.Min(goal, index + 1f) : Mathf.Max(goal, index);
                float remaining = Mathf.Abs(boundary - cursor) * length;
                // Assign reached anchors exactly. Subtracting a rounded distance can put a
                // reverse step just below zero, poisoning every subsequent route lookup.
                if (length <= .001f || budget >= remaining)
                {
                    cursor = boundary;
                    budget -= remaining;
                }
                else
                {
                    cursor = Mathf.MoveTowards(cursor, boundary, budget / length);
                    budget = 0;
                }
            }
            return Position(cursor);
        }
        Vector3 Position(float progress)
        {
            progress = Mathf.Clamp(progress, 0, points.Length - 1);
            int i = Mathf.Clamp(Mathf.FloorToInt(progress), 0, points.Length - 2);
            return Vector3.Lerp(points[i].feet.position, points[i + 1].feet.position, progress - i);
        }
        public float Project(Vector3 feet, out float distance)
        {
            float best = float.PositiveInfinity, progress = 0;
            if (playerGuides != null && points.Length > 0)
                foreach (var guide in playerGuides)
                {
                    if (guide == null || !guide.from || !guide.to
                        || !float.IsFinite(guide.fromProgress) || !float.IsFinite(guide.toProgress)) continue;
                    Vector3 a = guide.from.position, ab = guide.to.position - a;
                    float lengthSquared = ab.sqrMagnitude;
                    if (!float.IsFinite(lengthSquared) || lengthSquared <= 0f) continue;
                    float t = Mathf.Clamp01(Vector3.Dot(feet - a, ab) / lengthSquared);
                    float d = (feet - a - ab * t).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        progress = Mathf.Clamp(Mathf.Lerp(guide.fromProgress, guide.toProgress, t), 0, points.Length - 1);
                    }
                }
            // Guide rows are separate edges: never connect the end of one branch to the next row.
            if (!float.IsPositiveInfinity(best)) { distance = Mathf.Sqrt(best); return progress; }
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
