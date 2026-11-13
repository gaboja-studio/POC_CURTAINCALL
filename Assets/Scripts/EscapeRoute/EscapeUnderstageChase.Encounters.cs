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
            public Transform staging, approach, exit, landing;
            public BoxCollider platform;
            public bool continueOutside;
        }
        [SerializeField] PhysicalEncounter[] encounters = Array.Empty<PhysicalEncounter>();
        readonly HashSet<int> visitedEncounters = new();
        int encounterIndex = -1, encounterPhase;
        double encounterRound = -1, exposedAt;
        Vector3 encounterReturn;
        bool encounterJump, returningJump;
        bool EncounterVisible => encounterIndex >= 0 && encounterPhase == 6;
        bool CanAttackFromRoute => !fullRoute || !fullRoute.Following || EncounterVisible || fullRoute.Exposed;

        void ResetEncounters()
        {
            encounterIndex = -1; encounterPhase = 0; encounterRound = -1;
            visitedEncounters.Clear(); encounterJump = returningJump = false;
        }

        bool AdvanceEncounter(NetworkPlayer runner)
        {
            var timer = EscapeRouteTimer.Current;
            if (encounterRound != timer.StartedAt)
            {
                ResetEncounters(); encounterRound = timer.StartedAt;
            }
            if (encounterIndex < 0 && runner && attack.Phase == AttackPhase.None)
            {
                float targetProgress = fullRoute.Project(PlayerFeet(runner), out _);
                for (int i = 0; i < encounters.Length; i++)
                {
                    var candidate = encounters[i];
                    if (visitedEncounters.Contains(i) || fullRoute.Progress < candidate.routePoint - 1f ||
                        fullRoute.Progress > candidate.untilPoint + .5f || targetProgress < candidate.routePoint - 1f ||
                        !candidate.continueOutside && targetProgress > candidate.untilPoint + 1f) continue;
                    encounterIndex = i; encounterPhase = 1; encounterReturn = transform.position;
                    break;
                }
            }
            if (encounterIndex < 0) return false;
            var e = encounters[encounterIndex];
            Vector3 before = transform.position;
            bool moving = false;
            switch (encounterPhase)
            {
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
                    if (walls.PrepareBreach(e.gate, transform.position)) encounterPhase = 4;
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
                    if (e.continueOutside)
                    {
                        fullRoute.ResumeAt(e.routePoint);
                        FinishEncounter();
                        return true;
                    }
                    Vector3 goal = transform.position, watch = Vector3.zero;
                    bool acting = UpdateAttack(true, false, ref goal, ref watch, runner);
                    if (watch.sqrMagnitude > .001f) FaceEncounter(watch);
                    if (!acting && runner && !HoldingRunner)
                    {
                        Vector3 local = e.platform.transform.InverseTransformPoint(PlayerFeet(runner));
                        local.x = Mathf.Clamp(local.x, -e.platform.size.x * .5f + 1.5f, e.platform.size.x * .5f - 1.5f);
                        local.z = Mathf.Clamp(local.z, -e.platform.size.z * .5f + 1.5f, e.platform.size.z * .5f - 1.5f);
                        local.y = 0;
                        moving = MoveEncounter(e.platform.transform.TransformPoint(local), true);
                    }
                    if (attack.Phase == AttackPhase.None && timer.Clock - exposedAt >= Tuning.encounterMinimumSeconds &&
                        (!runner || fullRoute.Project(PlayerFeet(runner), out _) >= e.untilPoint + .5f)) encounterPhase = 7;
                    break;
                case 7:
                    moving = MoveEncounter(e.landing.position, true);
                    if (!moving)
                    {
                        encounterPhase = 8;
                        StartEncounterJump(e.landing.position, e.exit.position, true);
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
                    moving = MoveEncounter(encounterReturn, false);
                    if (!moving) FinishEncounter();
                    break;
            }
            float distance = Vector3.Distance(before, transform.position);
            if (!jumping) AdvanceGait(distance, 0, moving ? 1 : 0, distance < .001f, Time.deltaTime);
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
        }
        void FinishEncounter()
        {
            visitedEncounters.Add(encounterIndex);
            encounterIndex = -1; encounterPhase = 0;
        }
        void FaceEncounter(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240 * Time.deltaTime);
        }
        bool MoveEncounter(Vector3 goal, bool avoidScenery)
        {
            Vector3 delta = goal - transform.position;
            if (delta.magnitude < .08f) return false;
            float step = runSpeed * Time.deltaTime;
            Vector3 next = Vector3.MoveTowards(transform.position, goal, step);
            if (avoidScenery && !ClearEncounterStep(next))
            {
                bool found = false;
                for (int angle = 35; angle <= 105 && !found; angle += 35)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var alternate = transform.position + Quaternion.Euler(0, angle * side, 0) * delta.normalized * step;
                        var area = encounters[encounterIndex].platform;
                        var p = area.transform.InverseTransformPoint(alternate);
                        if (Mathf.Abs(p.x) > area.size.x * .5f - 1.4f || Mathf.Abs(p.z) > area.size.z * .5f - 1.4f || !ClearEncounterStep(alternate)) continue;
                        next = alternate; found = true; break;
                    }
                if (!found) return true;
            }
            FaceEncounter(next - transform.position);
            transform.position = next;
            return true;
        }
        bool ClearEncounterStep(Vector3 to)
        {
            var from = transform.position;
            var delta = to - from;
            foreach (var hit in Physics.CapsuleCastAll(from + Vector3.up * 1.5f, from + Vector3.up * 3.6f, 1.35f,
                         delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.GetComponentInParent<NetworkPlayer>()) return false;
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
