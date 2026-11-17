using Unity.Netcode;
using UnityEngine;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Motion = CurtainCall.EscapeRoute.EscapePursuerDoll.Motion;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        [SerializeField] EscapePursuerDoll doll;
        internal bool AuthoredDoll => doll && doll.revision >= 7;
        // The host's committed attack overrides its route target. Clients use that same ID,
        // then resolve the live body position (including seats, jumps and lost legs) locally.
        ulong HostLookActor => boss && boss.Defeated ? ulong.MaxValue
            : attack.Phase != AttackPhase.None ? attack.Actor
            : threat.Phase != ThreatPhase.None && threat.Actor != ulong.MaxValue ? threat.Actor
            : target ? target.OwnerClientId : ulong.MaxValue;

        bool CurrentSnapshot(EscapeRouteTimer timer) => hasSnapshot && timer && timer.HasStarted
            && latest.Round == timer.StartedAt;

        internal bool DollLookTarget(out Vector3 point)
        {
            point = default;
            var net = NetworkManager.Singleton;
            var timer = EscapeRouteTimer.Current;
            if (!net || !net.IsListening || !timer || !timer.HasStarted || !EscapeCampaignContent.IsTraversalActive
                || boss && boss.Defeated) return false;
            if (!net.IsServer && !CurrentSnapshot(timer)) return false;
            if (breach && !breach.EntranceComplete)
            {
                if (breach.SampleTime < breach.ImpactTime + EscapePursuerWalls.FollowThrough)
                { point = breach.ContactPoint; return true; }
                return false;
            }
            if (walls && walls.StrikePose(transform.position, out _, out _, out var contact))
            { point = contact; return true; }
            var cue = net.IsServer ? attack : latest.Attack;
            if (cue.Phase == AttackPhase.Warning || cue.Phase == AttackPhase.Strike)
            { point = cue.Aim; return true; }
            return LookPoint(net.IsServer ? HostLookActor : latest.LookActor, out point);
        }

        bool LookPoint(ulong actor, out Vector3 point)
        {
            point = default;
            if (actor == ulong.MaxValue) return false;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.OwnerClientId != actor) continue;
                if (player.State != PlayerState.Normal && player.State != PlayerState.EscapeCaptured) return false;
                var mover = player.GetComponent<PlayerMover>();
                if (!mover) return false;
                point = PlayerFeet(player) + Vector3.up * (mover.BodyHeight * .85f);
                return float.IsFinite(point.sqrMagnitude);
            }
            return false;
        }

        internal bool DollStretch(out Vector3 point)
        {
            point = graspingHand ? graspingHand.position : default;
            var net = NetworkManager.Singleton;
            if (!InShowcase && net && !net.IsServer && !CurrentSnapshot(EscapeRouteTimer.Current)) return false;
            return CanUseAttackHand && graspingHand && graspingHand.gameObject.activeSelf;
        }
        internal AppearancePose DollPose => DollPoseAt(EscapeRouteTimer.Current,
            NetworkManager.Singleton && !NetworkManager.Singleton.IsServer);

        AppearancePose DollPoseAt(EscapeRouteTimer timer, bool remote)
        {
            double now = timer ? timer.Clock : 0;
            // Clock/reset messages and pursuer snapshots can arrive on different frames.
            // Never sample a prior round's grab, throw or boss pose while waiting for this one.
            if (remote && !InShowcase && !CurrentSnapshot(timer))
                return new AppearancePose { Action = -1, Resting = true };
            var cue = remote ? latest.Attack : attack;
            var intent = remote ? latest.Threat : threat;
            if (InShowcase) { cue = previewAttack; now = previewAttackTime; }
            float age = Mathf.Max(0, (float)(now - cue.Started));
            var pose = new AppearancePose { Action = -1, Stride = stride, Run = runWeight, Duck = duckWeight,
                Resting = remote ? latest.Stage == Stage.Idle || latest.Position == previous.Position : gaitResting };
            if (AuthoredDoll && !InShowcase && boss && boss.DollAction(now, out var bossMotion, out var bossTime))
            { pose.Action = (int)bossMotion; pose.Time = bossTime; return pose; }
            if (!InShowcase && breach && !breach.EntranceComplete)
            {
                float t = breach.SampleTime;
                float ageFromImpact = t - breach.ImpactTime;
                if (ageFromImpact <= EscapePursuerWalls.FollowThrough)
                {
                    float windup = Mathf.Max(0, breach.ImpactTime - 1.1f);
                    Strike(ageFromImpact <= 0 ? Mathf.InverseLerp(windup, breach.ImpactTime, t)
                        : 1 + ageFromImpact / Mathf.Max(.1f, Tuning.wallBreakSeconds), breach.ContactPoint);
                }
                else
                {
                    float travelTime = ageFromImpact - EscapePursuerWalls.FollowThrough;
                    // The breach is six metres high. Its old forced CrouchWalk looked
                    // like a bow even in this clear opening; low passages still use Duck.
                    pose.Stride = Mathf.Repeat(breach.TravelAfterImpact(t) / Mathf.Max(.01f, doll.walkCycleDistance), 1);
                    const float recoveryBlend = .35f;
                    if (travelTime < recoveryBlend)
                    {
                        Set(Motion.WallStrike, doll.clips[(int)Motion.WallStrike].length);
                        pose.WalkBlend = Mathf.SmoothStep(0, 1, travelTime / recoveryBlend);
                    }
                    else Set(Motion.Walk, pose.Stride * doll.clips[(int)Motion.Walk].length);
                    pose.IdleBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(breach.Duration - .55f, breach.Duration, t));
                }
                return pose;
            }
            var reach = AuthoredDoll ? Vector3.Distance(cue.Origin, cue.Aim) > 20 ? Motion.ReachFullRange
                : Vector3.Distance(cue.Origin, cue.Aim) > 8 ? Motion.ReachLong : Motion.GrabStretch : Motion.GrabStretch;
            float grasp = AuthoredDoll ? doll.clips[(int)reach].length * .5f : doll.graspTime;
            if (intent.Phase == ThreatPhase.ChargeWarning)
            {
                var warning = AuthoredDoll ? Motion.ChargeWarning : Motion.Roar;
                Set(warning, Mathf.Clamp01((float)(now - intent.Started) / intent.Duration) * doll.clips[(int)warning].length);
            }
            else if (intent.Phase == ThreatPhase.DebrisWarning)
                Set(Motion.Throw, doll.releaseTime * Mathf.Clamp01((float)(now - intent.Started) / Tuning.debrisWarning));
            else if (intent.Phase == ThreatPhase.DebrisFlight || intent.Phase == ThreatPhase.Impact)
                Set(Motion.Throw, doll.releaseTime + Mathf.Max(0, (float)(now - intent.Started)) / Mathf.Max(.05f, Tuning.throwSwingSeconds) * (doll.clips[(int)Motion.Throw].length - doll.releaseTime));
            else if (cue.Phase == AttackPhase.Preparing)
                Set(reach, 0);
            else if (cue.Phase == AttackPhase.Warning)
                Set(reach, grasp * .6f * Mathf.Clamp01(age / Warning(cue.Reach)));
            else if (cue.Phase == AttackPhase.Strike)
                Set(reach, grasp * Mathf.Lerp(.6f, 1, Mathf.Clamp01(age / StrikeTime)));
            else if (cue.Phase == AttackPhase.Retract)
            {
                var retract = AuthoredDoll ? Motion.GrabMiss : Motion.GrabStretch;
                Set(retract, Mathf.Lerp(AuthoredDoll ? doll.clips[(int)retract].length * .62f : doll.graspTime,
                    doll.clips[(int)retract].length, Mathf.Clamp01(age / RetractTime)));
            }
            else if (cue.Phase == AttackPhase.Grab)
            {
                if (!AuthoredDoll) Set(Motion.GrabStretch, doll.graspTime);
                else if (age < SeverTime) Set(Motion.CaptureTear, age / SeverTime * doll.clips[(int)Motion.CaptureTear].length);
                else if (!pose.Resting) { pose.Action = (int)Motion.CarryWalk; pose.Time = pose.Stride; }
                else Set(Motion.GrabHold, Mathf.Repeat(age - SeverTime, doll.clips[(int)Motion.GrabHold].length));
            }
            else if (cue.Phase == AttackPhase.Throw)
                Set(Motion.Throw, age <= SwingTime ? doll.releaseTime * Mathf.Clamp01(age / SwingTime)
                    : doll.releaseTime + (age - SwingTime) / Mathf.Max(.05f, Tuning.throwSwingSeconds) * (doll.clips[(int)Motion.Throw].length - doll.releaseTime));
            if (jumping || remote && latest.Stage == Stage.CartJump)
            {
                float t = jumping ? jumpTime : latest.JumpTime;
                SetJump(t < ContactTime ? 7 : 8, t < ContactTime ? t / ContactTime : (t - ContactTime) / landingClip.length);
            }
            else if (sampledFrame == Time.frameCount && sampledAction > 0) SetJump(sampledAction, sampledTime);
            if (!InShowcase && walls && !jumping && !(remote && latest.Stage == Stage.CartJump) &&
                walls.StrikePose(transform.position, out _, out float strike, out var contact))
                Strike(strike, contact);
            if (AuthoredDoll && pose.Action < 0 && boss && boss.IsCut(4) && boss.IsCut(5))
            { pose.Action = (int)Motion.MaimedCrawl; pose.Time = pose.Resting ? 0 : pose.Stride; }
            return pose;

            void Set(Motion motion, float seconds) { pose.Action = (int)motion; pose.Time = doll.Normalized(motion, seconds); }
            void Strike(float t, Vector3 contact)
            {
                float release = (t - 1) * Tuning.wallBreakSeconds / EscapePursuerWalls.FollowThrough;
                float end = doll.clips[(int)Motion.WallStrike].length;
                Set(Motion.WallStrike, t <= 1 ? doll.impactTime * t : Mathf.Lerp(doll.impactTime, end, release));
                pose.Aim = contact;
                pose.WallContactWeight = t <= 1 ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.68f, 1, t))
                    : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1f, 1.3f, t));
            }
            void SetJump(int action, float time)
            {
                float seconds = action == 7 ? time < .45f ? doll.takeoffTime * time / .45f
                    : Mathf.Lerp(doll.takeoffTime, doll.landingTime, Mathf.InverseLerp(.45f, 1, time))
                    : Mathf.Lerp(doll.landingTime, doll.clips[(int)Motion.JumpLand].length, Mathf.Clamp01(time));
                Set(Motion.JumpLand, seconds);
            }
        }
    }
}
