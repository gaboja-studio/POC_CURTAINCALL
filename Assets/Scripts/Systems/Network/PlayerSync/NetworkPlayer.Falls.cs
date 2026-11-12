using System;
using CurtainCall.Damage;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        readonly NetworkVariable<bool> droppingFromSupport = new(false);
        bool localSupportDrop;
        public bool IsDroppingFromSupport => droppingFromSupport.Value || IsOwner && localSupportDrop;
        float fallPeak;
        bool fallTracking, fallAirborne, landingPending, naturalFallQueued;
        int fallScene, fallRestart, fallRevive;
        uint fallSequence, acceptedFallSequence;
        uint ownerPeakSequence;
        float ownerFallPeak;
        float nextLandingReport, nextSupportReport;
        float nextWorldDeathReport;
        float pendingLandingPeak;
        Vector3 pendingLandingFeet;
        float reportedPeak;
        bool fallTeleportPending;
        Vector3 fallTeleportDestination;

        public void ResetFallTracking()
        {
            fallPeak = transform.position.y;
            reportedPeak = fallPeak;
            ownerPeakSequence = 0; ownerFallPeak = fallPeak;
            fallTracking = true; fallAirborne = landingPending = false;
            nextWorldDeathReport = 0f;
            fallTeleportPending = naturalFallQueued = false;
            localSupportDrop = false;
            fallScene = SceneManager.GetActiveScene().handle;
            fallRestart = restartCount.Value; fallRevive = reviveCount.Value;
            if (IsServer) droppingFromSupport.Value = false;
        }

        public void BeginFallAt(Vector3 feet)
        {
            ResetFallTracking();
            fallPeak = reportedPeak = feet.y;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ReportFallPeakRpc(float peak, int restart, int revive, uint sequence)
            => AcceptFallPeak(peak, restart, revive, sequence);

        bool AcceptFallPeak(float peak, int restart, int revive, uint sequence)
        {
            if (!IsServer || !CurrentFallEpoch(restart, revive) || State != PlayerState.Normal || HeldAboveGround()
                || sequence == 0 || !NewerFallSequence(sequence, acceptedFallSequence)
                || NewerFallSequence(ownerPeakSequence, sequence) || !float.IsFinite(peak)
                || peak > transform.position.y + 2f) return false;
            ownerFallPeak = ownerPeakSequence == sequence ? Mathf.Max(ownerFallPeak, peak) : peak;
            ownerPeakSequence = sequence;
            fallPeak = Mathf.Max(fallPeak, peak);
            fallAirborne = true;
            return true;
        }

        static bool NewerFallSequence(uint sequence, uint previous) => unchecked((int)(sequence - previous)) > 0;

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ReportFallSupportRpc(uint sequence, int restart, int revive, Vector3 feet)
            => AcceptFallSupport(sequence, restart, revive, feet);

        bool AcceptFallSupport(uint sequence, int restart, int revive, Vector3 feet)
        {
            // A walking owner may never become airborne while interpolation briefly loses the host's
            // support probe. Only an acknowledged segment can replace that stale host-only peak.
            if (!IsServer || !CurrentFallEpoch(restart, revive) || sequence != acceptedFallSequence
                || NewerFallSequence(ownerPeakSequence, sequence)
                || State != PlayerState.Normal || HeldAboveGround() || !FiniteFallPoint(feet)
                || (feet - transform.position).sqrMagnitude > 25f || !WalkableSupport.Contains(mover, feet)
                || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(OwnerClientId)) return false;
            WaitForFallTeleport(feet);
            return true;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ResetFallTeleportRpc(Vector3 feet, int restart, int revive)
        {
            if (!CurrentFallEpoch(restart, revive) || State != PlayerState.Normal || !FiniteFallPoint(feet)) return;
            CurtainCall.SpaceLink.CampaignPerformanceEntry.ConfirmPlayerPlacement(this, feet);
            WaitForFallTeleport(feet);
        }

        void WaitForFallTeleport(Vector3 feet)
        {
            BeginFallAt(feet);
            fallTeleportDestination = feet;
            fallTeleportPending = true;
            ResumeAfterFallTeleport();
        }

        bool CurrentFallEpoch(int restart, int revive) => restart == RestartVersion && revive == reviveCount.Value;

        bool ResumeAfterFallTeleport()
        {
            if (!fallTeleportPending) return true;
            // The owner's reset RPC can arrive before NetworkTransform's teleport.
            // One metre is transport tolerance, not fall height: never sample the old pose while waiting.
            if ((transform.position - fallTeleportDestination).sqrMagnitude > 1f) return false;
            // Owner peak reports remain valid while the exact destination pose is in transit.
            fallTeleportPending = false;
            return true;
        }

        bool HeldAboveGround() => restingBed.Value >= 0
            || HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(OwnerClientId) >= 0
            || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsSeated(OwnerClientId)
            || TightropeHangSystem.Current != null && TightropeHangSystem.Current.IsHanging(this)
            || PiggybackSystem.Current != null && PiggybackSystem.Current.GetSupportingPlayer(this) != null;

        void LateUpdate()
        {
            if (!IsSpawned || (!IsOwner && !IsServer) || mover == null) return;
            if (naturalFallQueued) { naturalFallQueued = false; BeginNaturalFall(); }
            if (!fallTracking || fallScene != SceneManager.GetActiveScene().handle
                || fallRestart != restartCount.Value || fallRevive != reviveCount.Value
                || State != PlayerState.Normal)
            { ResetFallTracking(); return; }
            if (CurtainCall.UI.SceneIntegration.SceneCurtainTransition.HoldingEscapeMovement
                || EscapeSceneTransfer.HoldingForDeparture || EscapeDevelopmentSession.AwaitingEntrance)
            { ResetFallTracking(); return; }
            if (CheckWorldDeath()) return;
            if (HeldAboveGround()) { ResetFallTracking(); return; }

            Vector3 feet = transform.position;
            bool flying = HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(OwnerClientId);
            bool grounded = !flying && WalkableSupport.Contains(mover, feet)
                && (!IsOwner || mover.Body != null && mover.Body.enabled && mover.Body.isGrounded);
            // Placement acknowledgement already excludes pre-respawn poses. Do not
            // let a pending ordinary fall segment suppress this tutorial recovery.
            if (grounded && TryTutorialFloorRecovery(feet)) return;
            if (!ResumeAfterFallTeleport()) return;
            if (IsOwner && landingPending && Time.unscaledTime >= nextLandingReport) SendPendingFallLanding();
            if (!grounded)
            {
                fallAirborne = true;
                fallPeak = Mathf.Max(fallPeak, feet.y);
                if (IsOwner && !IsServer && fallPeak > reportedPeak + .1f)
                { reportedPeak = fallPeak; ReportFallPeakRpc(fallPeak, RestartVersion, reviveCount.Value, fallSequence + 1u); }
                return;
            }
            if (!fallAirborne)
            {
                fallPeak = reportedPeak = feet.y;
                if (IsOwner && !IsServer && !landingPending && Time.unscaledTime >= nextSupportReport)
                {
                    nextSupportReport = Time.unscaledTime + .25f;
                    ReportFallSupportRpc(fallSequence, RestartVersion, reviveCount.Value, feet);
                }
                return;
            }
            // Flight landings are resolved by HumanCannonSystem with its revision/segment validation.
            if (flying || !IsOwner) return;
            QueueFallLanding(fallPeak, feet);
            SendPendingFallLanding();
        }

        bool CheckWorldDeath()
        {
            // The owner has the current pose. Sampling a remote host pose here could
            // kill a revived player again while their respawn teleport is in transit.
            if (!IsOwner || !CommonDamageRules.IsBelowWorldDeathY(transform.position.y)) return false;
            if (Time.unscaledTime >= nextWorldDeathReport)
            {
                nextWorldDeathReport = Time.unscaledTime + .25f;
                ReportWorldDeathRpc(transform.position, RestartVersion, reviveCount.Value);
            }
            return true;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ReportWorldDeathRpc(Vector3 feet, int restart, int revive)
        {
            if (!CurrentFallEpoch(restart, revive) || State != PlayerState.Normal || !FiniteFallPoint(feet)
                || !CommonDamageRules.IsBelowWorldDeathY(feet.y)
                || (feet - transform.position).sqrMagnitude > 25f) return;
            ServerKill(DeathOrigin.MapExit);
        }

        bool TryTutorialFloorRecovery(Vector3 feet)
        {
            if (!IsServer || !IsTutorialFloorFall(feet)
                || !ServerKill(DeathOrigin.Fall)) return false;
            ResetFallTracking();
            return true;
        }

        bool IsTutorialFloorFall(Vector3 feet) => WaitingRoomRehearsal.ShouldRestartAfterFloorLanding(this, feet)
            || WaitingRoomCannonRehearsal.ShouldRestartAfterFloorLanding(this, feet);

        void QueueFallLanding(float peak, Vector3 feet)
        {
            fallSequence++;
            landingPending = true;
            pendingLandingPeak = peak; pendingLandingFeet = feet;
            // The host owner does not send peak RPCs to itself. Preserve its measured segment before
            // starting the next one, so the server's upper-bound check still accepts a real high fall.
            if (IsServer && IsOwner) { ownerPeakSequence = fallSequence; ownerFallPeak = peak; }
            // Begin the next movement segment immediately. An old ACK cannot erase its jump peak,
            // and retransmissions retain the original contact instead of a later point down the stairs.
            fallAirborne = false;
            fallPeak = reportedPeak = feet.y;
        }

        void SendPendingFallLanding()
        {
            nextLandingReport = Time.unscaledTime + .25f;
            ReportFallLandingRpc(fallSequence, RestartVersion, reviveCount.Value, pendingLandingPeak, pendingLandingFeet);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ReportFallLandingRpc(uint sequence, int restart, int revive, float peak, Vector3 feet)
        {
            if (AcceptFallLanding(sequence, restart, revive, peak, feet)) ConfirmFallLandingRpc(sequence, restart, revive);
        }

        bool AcceptFallLanding(uint sequence, int restart, int revive, float peak, Vector3 feet)
        {
            if (!IsServer || !CurrentFallEpoch(restart, revive) || sequence == 0) return false;
            // A retry of an already accepted landing needs its ACK, never another damage evaluation.
            if (sequence == acceptedFallSequence) return true;
            float observedPeak = Mathf.Max(fallPeak, transform.position.y);
            if (ownerPeakSequence == sequence) observedPeak = Mathf.Max(observedPeak, ownerFallPeak);
            if (State != PlayerState.Normal || HeldAboveGround() || !NewerFallSequence(sequence, acceptedFallSequence)
                || !float.IsFinite(peak) || !FiniteFallPoint(feet)
                || (feet - transform.position).sqrMagnitude > 25f
                || peak < feet.y || peak > observedPeak + 2f
                || !WalkableSupport.Contains(mover, feet)
                || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(OwnerClientId)) return false;
            acceptedFallSequence = sequence;
            uint futureSequence = NewerFallSequence(ownerPeakSequence, sequence) ? ownerPeakSequence : 0;
            float futurePeak = ownerFallPeak;
            float segmentPeak = ownerPeakSequence == sequence ? Mathf.Max(peak, ownerFallPeak) : peak;
            // The owner reports the peak/contact of this exact segment. A host pose or owner peak
            // belonging to the following jump is an upper-bound check, not extra height for this landing.
            bool rebounded = CompleteFall(segmentPeak - feet.y, feet, TryOrdinarySpringRebound);
            // Valid owner landings can finish even if the exact teleport destination frame was skipped.
            // Every accepted ordinary landing uses reported contact, even when the server pose is delayed.
            if (!rebounded && State == PlayerState.Normal)
            {
                WaitForFallTeleport(feet);
                if (futureSequence != 0)
                {
                    ownerPeakSequence = futureSequence; ownerFallPeak = futurePeak;
                    fallPeak = Mathf.Max(fallPeak, futurePeak); fallAirborne = true;
                }
            }
            return true;
        }

        [Rpc(SendTo.Owner)]
        void ConfirmFallLandingRpc(uint sequence, int restart, int revive)
        {
            AcknowledgeFallLanding(sequence, restart, revive);
        }

        void AcknowledgeFallLanding(uint sequence, int restart, int revive)
        {
            if (!landingPending || !CurrentFallEpoch(restart, revive) || sequence != fallSequence) return;
            landingPending = false;
            // Host tracking resets on acceptance; a remote owner only receives this ACK.
            // Finish this contact without clearing the peak of a jump already in progress.
            localSupportDrop = false;
        }

        static bool FiniteFallPoint(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        public float FallHeightAt(Vector3 feet) => Mathf.Max(0f, (fallTracking ? fallPeak : transform.position.y) - feet.y);

        /// <summary>모든 착지의 피해/장비 순서. 반동이 성공하면 착지 종료와 피해를 실행하지 않는다.</summary>
        public bool CompleteFall(float height, Vector3 feet, Func<bool> rebound = null, Action land = null)
        {
            if (!IsServer || !ServerCanTakeDamage() || !float.IsFinite(height)) { land?.Invoke(); ResetFallTracking(); return false; }
            if (IsTutorialFloorFall(feet))
            {
                land?.Invoke();
                ResetFallTracking();
                ServerKill(DeathOrigin.Fall);
                return false;
            }
            var course = TightropeCourse.Current;
            bool ropeLanding = GameSettings.Base.BodyDamage.SafeRopeLanding && course != null && course.IsTraversalActive
                && course.TryGetSupportingRopeAt(mover, feet, out _);
            bool safePlatformLanding = GameSettings.Base.BodyDamage.SafeLandingZones
                && HumanCannonLandingZone.ContainsLanding(mover, feet);
            bool safeWaitingRoom = GameSettings.WaitingRoom.SafeFallLandings
                && WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene());
            var outcome = ropeLanding || safePlatformLanding || safeWaitingRoom ? FallOutcome.Safe : CommonDamageRules.Fall(height);
            bool protectedFall = EscapeDevelopmentSession.FallConsequencesSuppressed(this);
            if (outcome != FallOutcome.Safe && !protectedFall && rebound != null && rebound())
            { ResetFallTracking(); return true; }
            land?.Invoke();
            ResetFallTracking();
            if (protectedFall || outcome == FallOutcome.Safe) return false;
            HumanCannonSystem.NotifyCommonFall(this);
            if (outcome == FallOutcome.Dead) ServerKill(DeathOrigin.Fall);
            else ServerCut(GameSettings.Base.BodyDamage.FallInjuryCutCount, null, DeathOrigin.Fall);
            return false;
        }

        bool TryOrdinarySpringRebound()
        {
            var carry = PropCarry.Instance;
            if (carry == null || !carry.EquipmentReady(OwnerClientId, ItemUses.SpringShoes)) return false;
            float scale = ItemCatalog.Get(ItemUses.SpringShoes)?.effect ?? 1f;
            float speed = GameSettings.Trampoline.UpwardSpeed * (scale > 0f ? scale : 1f);
            if (!carry.UseEquipment(OwnerClientId, ItemUses.SpringShoes)) return false;
            SpringFromGroundRpc(speed, RestartVersion, reviveCount.Value);
            HumanCannonSystem.NotifyCommonSpring(this);
            return true;
        }

        [Rpc(SendTo.Owner)]
        void SpringFromGroundRpc(float speed, int restart, int revive)
        {
            if (!CurrentFallEpoch(restart, revive) || State != PlayerState.Normal) return;
            ResetFallTracking();
            if (balance != null) { balance.ResetBalance(); balance.SetBalanceActive(false); }
            if (ragdoll != null) ragdoll.Restore();
            mover.FreeMovement = true; mover.Simulated = true;
            mover.SetControlEnabled(true);
            mover.LaunchBounce(speed);
        }

        public void ServerReleaseIntoFall()
        {
            if (!IsServer || State != PlayerState.Normal) return;
            PrepareNaturalFall();
            droppingFromSupport.Value = true;
            ReleaseIntoFallRpc(RestartVersion, reviveCount.Value);
        }

        [Rpc(SendTo.Owner)]
        void ReleaseIntoFallRpc(int restart, int revive)
        {
            if (CurrentFallEpoch(restart, revive) && State == PlayerState.Normal) BeginNaturalFall();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void RequestNaturalFallRpc(int restart, int revive)
        {
            if (CurrentFallEpoch(restart, revive) && State == PlayerState.Normal) droppingFromSupport.Value = true;
        }

        public void RequestNaturalFall() { if (IsOwner && State == PlayerState.Normal && !IsDroppingFromSupport) naturalFallQueued = true; }

        void BeginNaturalFall()
        {
            if (!IsOwner || State != PlayerState.Normal || localSupportDrop) return;
            TightropeHangSystem.Current?.ReleaseLocalIntoFall(this);
            PrepareNaturalFall();
            localSupportDrop = true;
            PiggybackSystem.Current?.GetComponent<PiggybackFollow>()?.ReleaseForFall(this);
            if (balance != null) { balance.ResetBalance(); balance.SetBalanceActive(false); }
            if (ragdoll != null) ragdoll.Restore();
            mover.Simulated = true; mover.SetControlEnabled(true); mover.FreeMovement = true;
            if (mover.Body != null) mover.Body.enabled = true;
            var course = TightropeCourse.Current;
            if (course != null && course.TryGetRopePoint(transform.position, out int lane, out float distance)
                && Mathf.Abs(transform.position.y - course.GetLanePosition(lane, distance).y) <= course.RopeHeightTolerance)
            {
                Vector3 side = Vector3.Cross(Vector3.up, course.ForwardAt(transform.position)).normalized;
                mover.Body.Move(side * (mover.BodyRadius + course.RopeWidthAt(lane) * .5f + .1f));
            }
            mover.ResetExternalMotion(true);
            RequestNaturalFallRpc(RestartVersion, reviveCount.Value);
        }

        void PrepareNaturalFall()
        {
            // 줄을 벗어난 뒤 감지된 경우 이미 지나온 낙하 높이를 잃지 않는다.
            float peak = fallTracking && fallAirborne ? Mathf.Max(fallPeak, transform.position.y) : transform.position.y;
            ResetFallTracking();
            fallPeak = reportedPeak = peak;
            fallAirborne = true;
        }
    }
}
