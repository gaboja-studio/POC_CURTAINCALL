using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.WorldIndicators;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        const float TorsoProjectileRadius = .55f;
        SweptVolumeIndicatorView attackVolume;
        TargetIndicatorView grabTarget;
        AreaIndicatorView torsoLanding, debrisLanding, routeLanding;
        readonly RaycastHit[] worldUiSupports = new RaycastHit[32];
        bool WorldUiActive => isActiveAndEnabled && GameSettings.WorldUi.enabled;

        // Presentation only. Never substitute a guessed floor for missing support.
        internal bool TryWorldUiSupport(Vector3 feet, out RaycastHit support)
        {
            support = default;
            if (!float.IsFinite(feet.sqrMagnitude)) return false;
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * .2f, Vector3.down,
                worldUiSupports, .55f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == worldUiSupports.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = worldUiSupports[i];
                if (hit.transform.IsChildOf(transform) || hit.transform.GetComponentInParent<NetworkPlayer>()
                    || hit.transform.GetComponentInParent<EscapePursuerDebris>() || hit.distance >= nearest) continue;
                nearest = hit.distance;
                support = hit;
            }
            return support.collider && support.normal.y >= .8f;
        }

        internal bool TryWorldUiFootprint(Vector3 feet, Quaternion facing, out Vector3 center, out float radius)
        {
            center = feet; radius = 0;
            var shell = body ? body : GetComponent<CapsuleCollider>();
            // This actor's physical body is upright; do not invent a circle for a different shape.
            if (!shell || !shell.enabled || shell.direction != 1) return false;
            var scale = shell.transform.lossyScale;
            radius = shell.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var offset = Quaternion.Inverse(transform.rotation) * (shell.transform.TransformPoint(shell.center) - transform.position);
            center += Vector3.ProjectOnPlane(facing * offset, Vector3.up);
            return float.IsFinite(radius) && radius > 0;
        }

        bool PresentAttackVolume(AttackCue cue, float elapsed)
        {
            bool warning = cue.Phase == AttackPhase.Warning;
            if (!WorldUiActive || !GameSettings.WorldUi.style || !GameSettings.WorldUi.showGrabPath
                || !warning && cue.Phase != AttackPhase.Strike)
            { if (attackVolume) attackVolume.Hide(); return false; }
            if (!attackVolume) attackVolume = WorldIndicatorFactory.Volume(transform);
            if (!attackVolume) return false;
            var from = warning ? cue.Origin : Vector3.Lerp(cue.Origin, cue.Aim, elapsed / StrikeTime);
            attackVolume.Show(from, cue.Aim, ArmRadius, warning ? elapsed / Warning(cue.Reach) : 0, warning);
            return true;
        }

        bool TryAttackTargetPoint(AttackCue cue, out Vector3 point)
        {
            point = cue.Aim;
            if (!Targeting(cue.Phase)) return false;
            if (cue.Phase != AttackPhase.Preparing) return float.IsFinite(point.sqrMagnitude);
            if (InShowcase && cue.Actor == ulong.MaxValue)
                return float.IsFinite(point.sqrMagnitude);
            var player = AttackActor(cue);
            if (!player || player.State != PlayerState.Normal || WaitingForConveyor(player.OwnerClientId)
                || !player.TryGetComponent(out PlayerMover mover)) return false;
            point = GrabAimPoint(PlayerFeet(player), mover);
            return float.IsFinite(point.sqrMagnitude);
        }

        void PresentAttackTarget(AttackCue cue, float elapsed)
        {
            if (aimRing) aimRing.enabled = false;
            if (!WorldUiActive || !TryAttackTargetPoint(cue, out var point))
            { if (grabTarget) grabTarget.Hide(); return; }
            bool locked = cue.Phase != AttackPhase.Preparing;
            float progress = cue.Phase == AttackPhase.Preparing ? elapsed / Mathf.Max(.001f, PrepareTime)
                : cue.Phase == AttackPhase.Warning ? elapsed / Warning(cue.Reach) : 1;
            if (!grabTarget && GameSettings.WorldUi.style) grabTarget = WorldIndicatorFactory.Target(transform);
            if (grabTarget && GameSettings.WorldUi.style)
            {
                var actor = AttackActor(cue);
                var mover = actor ? actor.GetComponent<PlayerMover>() : null;
                float aimHeight = locked || !mover ? Mathf.Abs(cue.Aim.y - cue.MarkedFeet.y) : mover.BodyHeight * .68f;
                grabTarget.Show(point, progress, locked, aimHeight + (mover ? mover.BodyRadius : .5f));
                return;
            }
            if (grabTarget) grabTarget.Hide();
            // Kit-less scenes retain one billboard target, never a floor AoE or an unlocked cable.
            if (aimRing)
            {
                aimRing.enabled = true;
                aimRing.transform.SetPositionAndRotation(point, Camera.main ? Camera.main.transform.rotation : Quaternion.identity);
            }
        }

        // Returns true only when a supported legacy endpoint should remain visible.
        bool PresentEndpointWorldUi(ref AreaIndicatorView view, bool active, Vector3 feet, float radius,
            float progress, out RaycastHit support)
        {
            support = default;
            if (!WorldUiActive || !active || !TryWorldUiSupport(feet, out support))
            { if (view) view.Hide(); return false; }
            if (!GameSettings.WorldUi.style) { if (view) view.Hide(); return true; }
            if (!view) view = WorldIndicatorFactory.Area(transform, true);
            if (!view) return true;
            view.Show(support.point, Quaternion.FromToRotation(Vector3.up, support.normal),
                Vector2.one * (radius * 2f), progress);
            return false;
        }

        // Call with the same committed endpoints and sampled time used by the jump pose.
        // Remote snapshots must supply endpoints; an interpolated airborne position is not a landing.
        internal void PresentRouteLandingWorldUi(float time, Vector3 start, Vector3 end)
        {
            bool active = time >= 0 && jumpClip && time < ContactTime && (end - start).sqrMagnitude > .001f;
            var forward = Vector3.ProjectOnPlane(end - start, Vector3.up);
            var facing = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : transform.rotation;
            active &= TryWorldUiFootprint(end, facing, out var center, out float radius);
            PresentEndpointWorldUi(ref routeLanding, active, center, radius,
                jumpClip ? time / Mathf.Max(.001f, ContactTime) : 0, out _);
        }

        internal void HideRouteLandingWorldUi() { if (routeLanding) routeLanding.Hide(); }

        internal void HideChaseWorldUi()
        {
            if (grabTarget) grabTarget.Hide();
            if (attackVolume) attackVolume.Hide();
            if (torsoLanding) torsoLanding.Hide();
            if (debrisLanding) debrisLanding.Hide();
            HideRouteLandingWorldUi();
        }
    }
}
