using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitActor
    {
        public static float CrouchBodyHeight => Mathf.Max(.6f, 1.8f * GameSettings.Base.Character.CrouchHeightRatio);
        public bool IsCrouched { get; private set; }
        readonly Collider[] clearance = new Collider[32];

        void InitializeExtraMotion()
        {
            if (Motion == null) return;
            foreach (var state in new[] { PursuitMotion.Crouch, PursuitMotion.CrouchWalk, PursuitMotion.Death }) {
                var clip = Resources.Load<AnimationClip>("Animations/Pursuit/" + state);
                if (clip != null) Motion.AddClip(clip, state.ToString());
            }
        }

        bool BodyClear(Vector3 feet, float height)
        {
            float radius = .24f;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (radius + .08f),
                feet + Vector3.up * Mathf.Max(radius + .08f, height - radius), radius, clearance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == clearance.Length) return false;
            for (int i = 0; i < count; i++) if (!PursuitDirectClimb.Ignored(clearance[i], transform)) return false;
            return true;
        }

        void UpdateCrouch()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || climbing) return;
            float scale = Placement != null ? Placement.SizeScale : 1;
            var ahead = transform.position + agent.desiredVelocity.normalized * .55f;
            IsCrouched = !BodyClear(transform.position, 1.8f * scale) || !BodyClear(ahead, 1.8f * scale);
            agent.height = IsCrouched ? CrouchBodyHeight : 1.8f * scale;
            if (!IsCrouched) return;
            bool canMove = BodyClear(ahead, CrouchBodyHeight);
            agent.speed = canMove ? Mathf.Min(agent.speed, GameSettings.Base.Character.FreeMoveSpeed * GameSettings.Base.Character.CrouchSpeedMultiplier) : 0;
            bool moving = canMove && agent.hasPath && agent.desiredVelocity.sqrMagnitude > .01f;
            SetMotion(moving ? PursuitMotion.CrouchWalk : PursuitMotion.Crouch);
        }
    }
}
