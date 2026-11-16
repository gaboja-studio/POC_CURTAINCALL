using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        [Serializable]
        public sealed class PhysicalEncounter
        {
            public string label;
            public int gate, routePoint, untilPoint;
            public Transform ingress, staging, approach, exit, landing;
            public BoxCollider platform;
            public bool continueOutside;
        }
        [SerializeField] PhysicalEncounter[] encounters = Array.Empty<PhysicalEncounter>();
        readonly Dictionary<int, double> visitedEncounters = new();
        readonly EscapePursuerNavigation encounterNavigation = new();
        int encounterIndex = -1, encounterPhase;
        int outsideEncounter = -1;
        int pressureEncounter = -1;
        ulong pressureActor = ulong.MaxValue;
        float pressureTargetProgress;
        double encounterRound = -1, exposedAt, pressureProgressAt, nextAttackPosition;
        float nextEscapeCheck;
        float routeDeltaTime;
        Vector3 encounterReturn, attackPosition, pressureProgressPosition;
        bool encounterJump, returningJump;
        bool EncounterVisible => encounterIndex >= 0 && encounterPhase == 6;
        bool CanAttackFromRoute => !fullRoute || !fullRoute.Following || EncounterVisible || fullRoute.Exposed;

        void ResetEncounters()
        {
            encounterIndex = -1; encounterPhase = 0; encounterRound = -1;
            outsideEncounter = -1;
            pressureEncounter = -1;
            pressureActor = ulong.MaxValue;
            pressureTargetProgress = 0;
            nextAttackPosition = 0;
            nextEscapeCheck = 0;
            visitedEncounters.Clear(); encounterJump = returningJump = false;
            encounterNavigation.Reset();
        }

        bool AdvanceEncounter(NetworkPlayer runner, bool returningToHall = false)
        {
            var timer = EscapeRouteTimer.Current;
            if (encounterRound != timer.StartedAt)
            {
                ResetEncounters(); encounterRound = timer.StartedAt;
            }
            if (encounterIndex < 0 && outsideEncounter >= 0 && (runner || returningToHall) && attack.Phase == AttackPhase.None)
            {
                var outside = encounters[outsideEncounter];
                // The final deck starts before its service-route landing index.
                // Keep a runner on that deck in the outdoor chase instead of reversing
                // the hidden jump solely to match their lower projected progress.
                bool onPlatform = runner && OnEncounterPlatform(outside, PlayerFeet(runner));
                if (!returningToHall && onPlatform && fullRoute.PlayerProgress(runner, out _) < outside.routePoint)
                    fullRoute.ApproachEncounter(outside.routePoint);
                if (fullRoute.Progress <= outside.routePoint + .01f &&
                    (returningToHall || !onPlatform && fullRoute.PlayerProgress(runner, out _) < outside.routePoint - .5f))
                {
                    // Re-enter through the same broken bay, not a reverse jump through its opaque baffle.
                    encounterIndex = outsideEncounter; outsideEncounter = -1;
                    encounterPhase = 7; encounterReturn = outside.ingress.position;
                    fullRoute.ResumeAt(outside.routePoint - 1);
                    encounterNavigation.Reset();
                }
            }
            if (!returningToHall && encounterIndex < 0 && outsideEncounter < 0 && runner && attack.Phase == AttackPhase.None)
            {
                float targetProgress = fullRoute.PlayerProgress(runner, out _);
                ChoosePressureEncounter(runner.OwnerClientId, PlayerFeet(runner), targetProgress, timer.Clock);
                // Follow the authored route to a bay that can engage the rider. Exact player
                // progress often corresponds to the middle of an opaque service corridor.
                if (pressureEncounter >= 0)
                    fullRoute.ApproachEncounter(encounters[pressureEncounter].routePoint - 1);
                for (int i = 0; i < encounters.Length; i++)
                {
                    var candidate = encounters[i];
                    if (candidate.ingress && i != pressureEncounter) continue;
                    if (!candidate.continueOutside && visitedEncounters.TryGetValue(i, out double revisitAt) && timer.Clock < revisitAt || fullRoute.Progress < candidate.routePoint - 1f ||
                        !candidate.ingress && (fullRoute.Progress > candidate.untilPoint + .5f || targetProgress < candidate.routePoint - 1f ||
                        !candidate.continueOutside && targetProgress > candidate.untilPoint + 1f)) continue;
                    if (candidate.ingress && Vector3.Distance(transform.position, fullRoute.Points[candidate.routePoint - 1].feet.position) > 3f) continue;
                    encounterIndex = i; encounterPhase = candidate.ingress ? 0 : 1; encounterReturn = transform.position;
                    pressureEncounter = -1;
                    encounterNavigation.Reset();
                    break;
                }
            }
            if (encounterIndex < 0 && !runner) pressureEncounter = -1;
            if (encounterIndex < 0) return false;
            var e = encounters[encounterIndex];
            ReturnFromStaleApproach(e, runner, timer.Clock);
            Vector3 before = transform.position;
            bool moving = false;
            switch (encounterPhase)
            {
                case 0:
                    moving = MoveEncounter(e.ingress.position, false);
                    if (!moving) encounterPhase = 1;
                    break;
                case 1:
                    moving = MoveEncounter(e.staging.position, false);
                    if (!moving) encounterPhase = 2;
                    break;
                case 2:
                    moving = MoveEncounter(e.approach.position, false);
                    if (!moving) encounterPhase = 3;
                    break;
                case 3:
                    FaceEncounter(e.exit.position - transform.position);
                    // Stay behind the wall while the runner waits for the moving pole.
                    if (runner && WaitingForConveyor(runner.OwnerClientId)
                        && e.landing && AtConveyorDeck(e.landing.position)) break;
                    if (walls.PrepareBreach(e.gate, transform.position) && !walls.FinishingStrike(e.gate, timer.Clock))
                        encounterPhase = 4;
                    break;
                case 4:
                    moving = MoveEncounter(e.exit.position, false);
                    if (!moving)
                    {
                        encounterPhase = 5;
                        StartEncounterJump(e.exit.position, e.landing.position, false);
                    }
                    break;
                case 5: return true;
                case 6:
                    if (returningToHall)
                    { encounterPhase = 7; encounterNavigation.Reset(); break; }
                    if (e.continueOutside && runner && (OnEncounterPlatform(e, PlayerFeet(runner))
                        || fullRoute.PlayerProgress(runner, out _) >= e.routePoint - 1f))
                    {
                        fullRoute.ResumeAt(e.routePoint);
                        if (e.ingress) outsideEncounter = encounterIndex;
                        FinishEncounter();
                        return true;
                    }
                    if ((transform.position - pressureProgressPosition).sqrMagnitude >= .16f || attack.Phase != AttackPhase.None)
                    {
                        pressureProgressAt = timer.Clock;
                        pressureProgressPosition = transform.position;
                    }
                    // Bay selection is geometric: an ahead-of-route perch may already reach
                    // this runner. Do not immediately abandon it because its source index is ahead.
                    bool outside = !runner || !WithinEncounterReach(e, PlayerFeet(runner), AttackRange(runner));
                    if (ShouldReposition(timer.Clock, outside))
                    {
                        encounterPhase = 7;
                        encounterNavigation.Reset();
                        break;
                    }
                    Vector3 goal = transform.position, watch = Vector3.zero;
                    bool acting = TryDebrisAttack(runner) || UpdateAttack(true, false, ref goal, ref watch, runner);
                    if (watch.sqrMagnitude > .001f) FaceEncounter(watch);
                    if (!acting && runner && WaitingForConveyor(runner.OwnerClientId))
                    {
                        FaceEncounter(PlayerFeet(runner) - transform.position);
                        break;
                    }
                    if (!acting && runner && !HoldingRunner)
                    {
                        if (timer.Clock >= nextAttackPosition)
                        {
                            nextAttackPosition = timer.Clock + .75f;
                            Vector3 feet = PlayerFeet(runner);
                            if (!EscapePursuerNavigation.TryAttackPosition(transform, e.platform, feet + Vector3.up,
                                Mathf.Max(Tuning.contactRange, AttackRange(runner) - 2), out attackPosition,
                                StrikePositionFilter(runner, feet)))
                                attackPosition = EscapePursuerNavigation.Clamp(e.platform, PlayerFeet(runner));
                        }
                        moving = MoveEncounter(attackPosition, true);
                        if (!moving) FaceEncounter(PlayerFeet(runner) - transform.position);
                    }
                    break;
                case 7:
                    moving = MoveEncounter(e.landing.position, true);
                    bool alternate = false;
                    if (moving && (transform.position - before).sqrMagnitude < .00001f && Time.time >= nextEscapeCheck)
                    {
                        nextEscapeCheck = Time.time + .75f;
                        // A parked cannon or carried saw can occupy the exact return mark.
                        // Leave from a nearby supported spot only if the entire real arc clears scenery.
                        alternate = Vector3.Distance(transform.position, e.landing.position) <= 5f
                            && EscapePursuerNavigation.ClearJump(transform, transform.position, e.exit.position);
                    }
                    if (!moving || alternate)
                    {
                        encounterPhase = 8;
                        StartEncounterJump(alternate ? transform.position : e.landing.position, e.exit.position, true);
                    }
                    break;
                case 8: return true;
                case 9:
                    moving = MoveEncounter(e.approach.position, false);
                    if (!moving) encounterPhase = 10;
                    break;
                case 10:
                    moving = MoveEncounter(e.staging.position, false);
                    if (!moving) encounterPhase = 11;
                    break;
                case 11:
                    if (e.ingress)
                    {
                        moving = MoveEncounter(e.ingress.position, false);
                        if (!moving) encounterPhase = 12;
                        break;
                    }
                    moving = MoveEncounter(encounterReturn, false);
                    if (!moving) FinishEncounter();
                    break;
                case 12:
                    moving = MoveEncounter(encounterReturn, false);
                    if (!moving) FinishEncounter();
                    break;
            }
            float distance = Vector3.Distance(before, transform.position);
            if (!jumping) AdvanceGait(distance, 0, moving ? 1 : 0, distance < .001f, routeDeltaTime);
            previousPosition = transform.position;
            return true;
        }

        void StartEncounterJump(Vector3 from, Vector3 to, bool returning)
        {
            BeginServiceJump(from, to);
            encounterJump = true; returningJump = returning;
        }
        void CompleteEncounterJump()
        {
            encounterJump = false;
            encounterPhase = returningJump ? 9 : 6;
            exposedAt = EscapeRouteTimer.Current.Clock;
            pressureProgressAt = exposedAt;
            pressureProgressPosition = transform.position;
            nextAttackPosition = 0;
        }
        void FinishEncounter()
        {
            visitedEncounters[encounterIndex] = EscapeRouteTimer.Current.Clock + Tuning.revealCooldownMax;
            encounterNavigation.Reset();
            encounterIndex = -1; encounterPhase = 0;
        }

        // The runner can cross a whole deck while we approach its breach. Do not
        // complete an obsolete outward jump, wait on the empty deck, and jump back
        // before pursuing them again. Retrace only the approach legs already walked.
        bool ReturnFromStaleApproach(PhysicalEncounter encounter, NetworkPlayer runner, double now)
        {
            if (encounterPhase < 0 || encounterPhase > 4 || encounter.continueOutside || !runner
                || attack.Phase != AttackPhase.None || threat.Phase != ThreatPhase.None
                || fullRoute.PlayerProgress(runner, out _) <= encounter.routePoint
                || walls && walls.FinishingStrike(encounter.gate, now)) return false;
            foreach (var player in NetworkPlayer.All)
                if (player && player.IsSpawned && player.State == PlayerState.Normal
                    && WithinEncounterReach(encounter, PlayerFeet(player), AttackRange(player))) return false;
            if (now < nextAttackPosition) return false;
            nextAttackPosition = now + .75;
            int next = SelectPressureEncounter(PlayerFeet(runner), fullRoute.PlayerProgress(runner, out _), now);
            if (next < 0 || encounters[next] == encounter) return false;
            encounterPhase = encounterPhase == 0 ? 12 : encounterPhase == 1 ? 11
                : encounterPhase == 2 ? 10 : 9;
            encounterNavigation.Reset();
            return true;
        }

        bool ShouldReposition(double now, bool targetOutside)
        {
            if (attack.Phase != AttackPhase.None || threat.Phase != ThreatPhase.None) return false;
            double elapsed = now - exposedAt;
            if (elapsed < Tuning.encounterMinimumSeconds) return false;
            return targetOutside || now - pressureProgressAt >= Tuning.encounterRepositionSeconds
                || elapsed >= Tuning.encounterMaximumSeconds;
        }

        void RecordEncounterPressure(double now)
        {
            if (!EncounterVisible) return;
            // AdvanceThreat owns the whole warning/flight/impact interval before this director
            // runs again. That is active pressure, not four seconds of standing idle.
            pressureProgressAt = now;
            pressureProgressPosition = transform.position;
        }

        bool WithinEncounterReach(PhysicalEncounter e, Vector3 targetFeet, float range)
        {
            if (!e.platform) return false;
            Vector3 near = EscapePursuerNavigation.Clamp(e.platform, targetFeet);
            return Vector3.Distance(near + Vector3.up * 3, targetFeet + Vector3.up) <= range;
        }

        static bool OnEncounterPlatform(PhysicalEncounter encounter, Vector3 feet)
            => encounter.platform && new Bounds(encounter.platform.center, encounter.platform.size)
                .Contains(encounter.platform.transform.InverseTransformPoint(feet + Vector3.up));

        void ChoosePressureEncounter(ulong actor, Vector3 targetFeet, float targetProgress, double now)
        {
            // Finish travelling to the chosen bay while the rider stays in this route interval.
            // A previous bay's cooldown expiring must not turn us around midway through a jump chain.
            if (pressureEncounter >= 0 && pressureActor == actor && Mathf.Abs(targetProgress - pressureTargetProgress) < 1f) return;
            pressureActor = actor;
            pressureEncounter = SelectPressureEncounter(targetFeet, targetProgress, now);
            pressureTargetProgress = targetProgress;
        }

        int SelectPressureEncounter(Vector3 targetFeet, float targetProgress, double now)
        {
            int best = -1;
            float score = float.PositiveInfinity;
            bool bestCanAttack = false;
            float range = Tuning.exposedGrabRange;
            NetworkPlayer pressureRunner = null;
            foreach (var runner in NetworkPlayer.All)
                if (runner && runner.IsSpawned && runner.OwnerClientId == pressureActor)
                { range = AttackRange(runner); pressureRunner = runner; break; }
            var clearStrike = StrikePositionFilter(pressureRunner, targetFeet);
            for (int i = 0; i < encounters.Length; i++)
            {
                var e = encounters[i];
                if (!e.ingress || !e.platform || !e.landing || e.routePoint < 1 || e.routePoint >= fullRoute.Points.Count) continue;
                // Search only when choosing a bay, not every frame. The closest clamped point
                // may be behind a pillar even though another supported point can attack.
                bool canAttack = EscapePursuerNavigation.TryAttackPosition(transform, e.platform,
                    targetFeet + Vector3.up, range, out Vector3 near, clearStrike);
                // The last bay can look back along the route. Only approach it early
                // when its actual supported attack positions reach the lagging runner.
                if (e.continueOutside && targetProgress < e.routePoint - 1 && !canAttack) continue;
                if (!canAttack) near = EscapePursuerNavigation.Clamp(e.platform, targetFeet);
                float distance = Vector3.Distance(near + Vector3.up * 3, targetFeet + Vector3.up);
                float candidate = distance + Mathf.Abs(e.routePoint - 1 - fullRoute.Progress) * .3f;
                if (distance > range) candidate += (distance - range) * 4;
                if (!EscapePursuerNavigation.Sees(transform, near, targetFeet + Vector3.up)) candidate += 12;
                if (visitedEncounters.TryGetValue(i, out double revisitAt))
                {
                    if (!e.continueOutside && now < revisitAt) candidate += 30;
                    candidate += Mathf.Max(0, 10 - (float)(now - revisitAt));
                }
                // Cooldowns vary the choice among attack-capable bays. They must not send
                // us on a long retreat to an unreachable perch while a usable one remains.
                if (best < 0 || canAttack && !bestCanAttack || canAttack == bestCanAttack && candidate < score)
                { score = candidate; best = i; bestCanAttack = canAttack; }
            }
            return best;
        }
        void FaceEncounter(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240 * routeDeltaTime);
        }
        bool MoveEncounter(Vector3 goal, bool avoidScenery)
        {
            Vector3 delta = goal - transform.position;
            if (delta.magnitude < .08f) return false;
            float step = runSpeed * PressureSpeed(target) * routeDeltaTime;
            Vector3 next = Vector3.MoveTowards(transform.position, goal, step);
            if (avoidScenery)
            {
                next = encounterNavigation.Step(transform, encounters[encounterIndex].platform, goal, step);
            }
            FaceEncounter(next - transform.position);
            transform.position = next;
            // A blocked step is pending. Phase transitions require reaching the actual anchor.
            return true;
        }
        void PresentWallStrike()
        {
            if (!walls || !walls.StrikePose(transform.position, out var toward, out float t)) return;
            FaceEncounter(toward);
            SampleGait(0, 0);
            // Wind up with the normal arm, then hit the wall at the end of its break timer.
            grabArm.localRotation *= Quaternion.Euler(t < .65f ? Mathf.Lerp(-30, -140, t / .65f)
                : Mathf.Lerp(-140, -70, (t - .65f) / .35f), 0, 0);
        }
    }
}
