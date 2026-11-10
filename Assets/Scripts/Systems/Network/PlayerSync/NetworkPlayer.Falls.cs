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
        public bool IsDroppingFromSupport => droppingFromSupport.Value;
        float fallPeak;
        bool fallTracking, fallAirborne, landingPending, naturalFallQueued;
        int fallScene, fallRestart, fallRevive;
        uint fallSequence, acceptedFallSequence;
        float nextLandingReport;
        float reportedPeak;
        bool fallTeleportPending;
        Vector3 fallTeleportDestination;

        public void ResetFallTracking()
        {
            fallPeak = transform.position.y;
            reportedPeak = fallPeak;
            fallTracking = true; fallAirborne = landingPending = false;
            fallTeleportPending = naturalFallQueued = false;
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
        void ReportFallPeakRpc(float peak, int restart, int revive)
        {
            if (CurrentFallEpoch(restart, revive) && State == PlayerState.Normal && !HeldAboveGround()
                && float.IsFinite(peak) && peak <= transform.position.y + 2f)
            {
                fallPeak = Mathf.Max(fallPeak, peak);
                fallAirborne = true;
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ResetFallTeleportRpc(Vector3 feet, int restart, int revive)
        {
            if (!CurrentFallEpoch(restart, revive) || State != PlayerState.Normal || !FiniteFallPoint(feet)) return;
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
            || PiggybackSystem.Current != null && PiggybackSystem.Current.GetBelow(this) != null;

        void LateUpdate()
        {
            if (!IsSpawned || (!IsOwner && !IsServer) || mover == null) return;
            if (naturalFallQueued) { naturalFallQueued = false; BeginNaturalFall(); }
            if (!fallTracking || fallScene != SceneManager.GetActiveScene().handle
                || fallRestart != restartCount.Value || fallRevive != reviveCount.Value
                || State != PlayerState.Normal)
            { ResetFallTracking(); return; }
            if (!ResumeAfterFallTeleport()) return;
            if (HeldAboveGround()
                || CurtainCall.UI.SceneIntegration.SceneCurtainTransition.HoldingEscapeMovement
                || EscapeSceneTransfer.HoldingForDeparture || EscapeDevelopmentSession.AwaitingEntrance)
            { ResetFallTracking(); return; }

            Vector3 feet = transform.position;
            bool flying = HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(OwnerClientId);
            bool grounded = !flying && WalkableSupport.Contains(mover, feet)
                && (!IsOwner || mover.Body != null && mover.Body.enabled && mover.Body.isGrounded);
            if (!grounded)
            {
                fallAirborne = true;
                fallPeak = Mathf.Max(fallPeak, feet.y);
                if (IsOwner && !IsServer && fallPeak > reportedPeak + .1f)
                { reportedPeak = fallPeak; ReportFallPeakRpc(fallPeak, RestartVersion, reviveCount.Value); }
                return;
            }
            if (!fallAirborne) { fallPeak = feet.y; return; }
            // Flight landings are resolved by HumanCannonSystem with its revision/segment validation.
            if (flying || !IsOwner || landingPending && Time.unscaledTime < nextLandingReport) return;
            if (!landingPending) { fallSequence++; landingPending = true; }
            nextLandingReport = Time.unscaledTime + .25f;
            ReportFallLandingRpc(fallSequence, RestartVersion, reviveCount.Value, fallPeak, feet);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void ReportFallLandingRpc(uint sequence, int restart, int revive, float peak, Vector3 feet)
        {
            if (!CurrentFallEpoch(restart, revive) || State != PlayerState.Normal || HeldAboveGround()
                || sequence == 0 || unchecked((int)(sequence - acceptedFallSequence)) <= 0
                || !float.IsFinite(peak) || !FiniteFallPoint(feet)
                || (feet - transform.position).sqrMagnitude > 25f
                || peak < feet.y || peak > Mathf.Max(fallPeak, transform.position.y) + 2f
                || !WalkableSupport.Contains(mover, feet)
                || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(OwnerClientId)) return;
            acceptedFallSequence = sequence;
            bool waitingForTeleport = fallTeleportPending;
            bool rebounded = CompleteFall(Mathf.Max(peak, fallPeak) - feet.y, feet, TryOrdinarySpringRebound);
            // Valid owner landings can finish even if the exact teleport destination frame was skipped.
            // If this pose is also delayed, keep waiting at its reported feet instead of sampling the old server height.
            if (waitingForTeleport && !rebounded && State == PlayerState.Normal) WaitForFallTeleport(feet);
            ConfirmFallLandingRpc(sequence, restart, revive);
        }

        [Rpc(SendTo.Owner)]
        void ConfirmFallLandingRpc(uint sequence, int restart, int revive)
        {
            if (CurrentFallEpoch(restart, revive) && sequence == fallSequence && landingPending) ResetFallTracking();
        }

        static bool FiniteFallPoint(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        public float FallHeightAt(Vector3 feet) => Mathf.Max(0f, (fallTracking ? fallPeak : transform.position.y) - feet.y);

        /// <summary>모든 착지의 피해/장비 순서. 반동이 성공하면 착지 종료와 피해를 실행하지 않는다.</summary>
        public bool CompleteFall(float height, Vector3 feet, Func<bool> rebound = null, Action land = null)
        {
            if (!IsServer || !ServerCanTakeDamage() || !float.IsFinite(height)) { land?.Invoke(); ResetFallTracking(); return false; }
            if (WaitingRoomRehearsal.ShouldRestartAfterFloorLanding(this, feet))
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
                && HumanCannonLandingZone.ContainsLanding(feet);
            var outcome = ropeLanding || safePlatformLanding ? FallOutcome.Safe : CommonDamageRules.Fall(height);
            bool protectedFall = EscapeDevelopmentSession.FallInjuriesSuppressed(this);
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
            if (!IsOwner || State != PlayerState.Normal) return;
            TightropeHangSystem.Current?.ReleaseLocalIntoFall(this);
            PrepareNaturalFall();
            if (balance != null) { balance.ResetBalance(); balance.SetBalanceActive(false); }
            if (ragdoll != null) ragdoll.Restore();
            mover.Simulated = true; mover.SetControlEnabled(true); mover.FreeMovement = true;
            if (mover.Body != null) mover.Body.enabled = true;
            var course = TightropeCourse.Current;
            if (course != null && course.TryGetRopePoint(transform.position, out int lane, out float distance)
                && Mathf.Abs(transform.position.y - course.GetLanePosition(lane, distance).y) <= course.RopeHeightTolerance)
            {
                Vector3 side = Vector3.Cross(Vector3.up, course.ForwardAt(transform.position)).normalized;
                mover.Body.Move(side * (mover.BodyRadius + course.RopeWalkWidth * .5f + .1f));
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
