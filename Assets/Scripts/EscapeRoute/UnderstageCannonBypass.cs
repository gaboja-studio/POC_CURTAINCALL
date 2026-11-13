using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>One diversion per escape clock. The landing signal comes from the host-owned cannon.</summary>
    public sealed class UnderstageCannonBypass : MonoBehaviour
    {
        [SerializeField] HumanCannonSystem cannon;
        [SerializeField] HumanCannonStation firstCannon;
        [SerializeField] HumanCannonLandingZone firstWire;
        [SerializeField] Transform serviceEntry, revealStart, walkEnd, trackEnd;
        [SerializeField] Transform shutter;
        [SerializeField] BoxCollider personnelBarrier;
        [SerializeField] Transform cannonRelease, cannonDrop;
        [SerializeField] Transform upperWire;
        [SerializeField] Transform rampTurn, rampReveal, flankStart, flankEnd;

        enum Leg { ToEntry, BehindScreen, WalkOut, Alongside, ToTurn, Descending, FlankWalk, Flank }
        Leg leg;
        double round = -1;
        readonly HashSet<ulong> crossed = new();
        readonly HashSet<ulong> transferred = new();
        HumanCannonLandingZone upperLanding;
        bool parallelRun;
        BoxCollider firstSurface, upperSurface;

        internal void SetShutter(bool onRoute, Vector3 feet, Collider pursuerBody = null)
        {
            // The shutter's parent marks the sill. Keep the leaf clear until the shoulders pass.
            Vector3 local = shutter.parent.InverseTransformPoint(feet);
            float open = onRoute ? Mathf.InverseLerp(7.5f, 3.2f, new Vector2(local.x, local.z).magnitude) : 0f;
            if (open > 0f && personnelBarrier && pursuerBody && pursuerBody.enabled &&
                !Physics.GetIgnoreCollision(pursuerBody, personnelBarrier))
                Physics.IgnoreCollision(pursuerBody, personnelBarrier);
            var center = Vector3.up * (3.2f + open * 6.5f);
            if (shutter.localPosition != center) shutter.localPosition = center;
        }

        void OnEnable()
        {
            firstSurface = firstWire ? firstWire.GetComponent<BoxCollider>() : null;
            upperSurface = upperWire ? upperWire.GetComponent<BoxCollider>() : null;
            upperLanding = upperWire ? upperWire.GetComponent<HumanCannonLandingZone>() : null;
            if (cannon) cannon.LandingConfirmed += OnLanding;
            NetworkPlayer.Restarted += ResetRound;
        }

        void OnDisable()
        {
            if (cannon) cannon.LandingConfirmed -= OnLanding;
            NetworkPlayer.Restarted -= ResetRound;
            ResetRound(null);
        }

        void ResetRound(NetworkPlayer player)
        {
            crossed.Clear();
            transferred.Clear();
            round = -1;
            parallelRun = false;
            if (shutter) SetShutter(false, Vector3.zero);
        }

        void OnLanding(ulong actor, int station, uint revision, uint segment, Vector3 position, bool safe)
        {
            var timer = EscapeRouteTimer.Current;
            if (!safe || !timer || !timer.HasStarted || timer.gameObject.scene != gameObject.scene) return;
            if (round == timer.StartedAt && crossed.Contains(actor) && upperLanding && upperLanding.SupportsLanding(position))
            {
                transferred.Add(actor);
                return;
            }
            if (station < 0 || station >= cannon.StationCount || cannon.Station(station) != firstCannon ||
                !firstWire.SupportsLanding(position)) return;
            if (round != timer.StartedAt)
            {
                round = timer.StartedAt;
                leg = Leg.ToEntry;
                parallelRun = false;
                crossed.Clear();
                transferred.Clear();
            }
            crossed.Add(actor);
        }

        internal bool Active(double epoch) => isActiveAndEnabled && round >= 0 && round == epoch;

        internal bool HasCrossed(NetworkPlayer player) => crossed.Contains(player.OwnerClientId);
        internal bool InTransit(NetworkPlayer player) => cannon.IsFlying(player.OwnerClientId)
            && !(HumanCannonPoleSystem.Current && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0);
        internal bool AtGun(NetworkPlayer player) => cannon.SeatedStation(player.OwnerClientId) == firstCannon;
        internal Vector3 GunPosition(NetworkPlayer player) => AtGun(player) ? firstCannon.Seat.position : player.transform.position;
        internal Vector3 CannonRelease => cannonRelease.position;
        internal Vector3 CannonDrop => cannonDrop.position;
        internal Vector3 TrackAxis => (trackEnd.position - walkEnd.position).normalized;
        // Throw into the gap beside each deck. The west descent now occupies the old south drop.
        internal Vector3 DropOffset => leg == Leg.Flank
            ? Vector3.Cross(flankEnd.position - flankStart.position, Vector3.up).normalized * 5.8f
            : Vector3.Cross(TrackAxis, Vector3.up).normalized * 11f;
        internal bool CanAimAt(NetworkPlayer runner, Vector3 feet)
        {
            bool beyondPole = transferred.Contains(runner.OwnerClientId);
            if (!beyondPole) return leg == Leg.Alongside;
            return leg == Leg.Flank && Vector3.Distance(feet, FlankGoal(runner.transform.position)) <= 2.5f;
        }

        Vector3 FlankGoal(Vector3 runner)
        {
            var rail = flankEnd.position - flankStart.position;
            return Vector3.Lerp(flankStart.position, flankEnd.position,
                Mathf.Clamp01(Vector3.Dot(runner - flankStart.position, rail) / rail.sqrMagnitude));
        }

        internal NetworkPlayer RearmostRunner()
        {
            NetworkPlayer last = null;
            float leastProgress = float.PositiveInfinity;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal || !HasCrossed(player) || InTransit(player)) continue;
                Vector3 p = player.transform.position;
                // A runner who has left both wires (or fallen far below them) cannot draw aim away from those still crossing.
                if (!NearWire(firstSurface, p) && !NearWire(upperSurface, p)) continue;
                float progress = Vector3.Dot(p - firstWire.transform.position, TrackAxis);
                if (transferred.Contains(player.OwnerClientId))
                    progress = Vector3.Dot(upperWire.position - firstWire.transform.position, TrackAxis)
                        + Vector3.Dot(p - upperWire.position, upperWire.forward);
                if (progress >= leastProgress) continue;
                leastProgress = progress;
                last = player;
            }
            return last;
        }

        static bool NearWire(BoxCollider wire, Vector3 feet) => wire && (wire.ClosestPoint(feet) - feet).sqrMagnitude <= 25f;

        internal Vector3 Destination(Vector3 feet, out bool hurry, out Vector3 watch)
        {
            const float reached = .5f;
            if (leg == Leg.ToEntry && Vector3.Distance(feet, serviceEntry.position) <= reached) leg = Leg.BehindScreen;
            if (leg == Leg.BehindScreen && Vector3.Distance(feet, revealStart.position) <= reached) leg = Leg.WalkOut;
            if (leg == Leg.WalkOut && Vector3.Distance(feet, walkEnd.position) <= reached) leg = Leg.Alongside;
            hurry = false;
            watch = Vector3.zero;
            switch (leg)
            {
                case Leg.ToEntry: return serviceEntry.position;
                case Leg.BehindScreen:
                    hurry = Vector3.Distance(feet, revealStart.position) > 3f;
                    return revealStart.position;
                case Leg.WalkOut: return walkEnd.position;
            }

            Vector3 track = trackEnd.position - walkEnd.position;
            var axis = track.normalized;
            var runner = RearmostRunner();
            if (!runner) { parallelRun = false; return feet; }
            var position = runner.transform.position;
            watch = Vector3.ProjectOnPlane(position - feet, Vector3.up);
            if (transferred.Contains(runner.OwnerClientId))
            {
                if (leg == Leg.Alongside) leg = Leg.ToTurn;
                if (leg == Leg.ToTurn && Vector3.Distance(feet, rampTurn.position) <= reached) leg = Leg.Descending;
                if (leg == Leg.Descending && Vector3.Distance(feet, rampReveal.position) <= reached) leg = Leg.FlankWalk;
                if (leg == Leg.FlankWalk && Vector3.Distance(feet, flankStart.position) <= reached) leg = Leg.Flank;
                switch (leg)
                {
                    case Leg.ToTurn: hurry = Vector3.Distance(feet, rampTurn.position) > 3f; return rampTurn.position;
                    case Leg.Descending: return rampReveal.position;
                    case Leg.FlankWalk: return flankStart.position;
                }
                var flank = FlankGoal(position);
                parallelRun = Vector3.Distance(feet, flank) >= (parallelRun ? 1.5f : 3.5f);
                hurry = parallelRun;
                return flank;
            }
            leg = Leg.Alongside;
            float lead = Vector3.Dot(position - walkEnd.position, axis);
            // Keep the hand beyond the end of the blind wall even for an early landing.
            var goal = walkEnd.position + axis * Mathf.Clamp(lead, -2f, track.magnitude);
            float gap = Vector3.Dot(goal - feet, axis);
            parallelRun = gap >= (parallelRun ? 1.5f : 3.5f);
            hurry = parallelRun;
            return goal;
        }
    }
}
