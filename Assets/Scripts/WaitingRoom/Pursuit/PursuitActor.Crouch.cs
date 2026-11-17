using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

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

        /// <summary>
        /// 발 아래 계단 단·팔레트·경사로처럼 걸어서 넘는 높이(NavMesh agentClimb 0.55)는 머리 위 장애물이 아니다.
        /// 2026-11-17: 몸통 검사가 발목 높이부터 시작해 계단 경사 충돌체·바닥 팔레트를 막힘으로 보고 속도 0으로 계단 앞에 멈췄다.
        /// </summary>
        public const float StepClearance = .45f;
        // 앉아서도 앞이 막혀 멈춘 뒤 이 시간이 지나면 길찾기를 믿고 다시 움직인다(움직이는 소품에 영구 정지 방지).
        const float CrouchBlockedSeconds = .75f;
        float crouchBlockedFor;

        bool BodyClear(Vector3 feet, float height)
        {
            // NavMesh는 벽·난간에서 0.24m 떨어져 구워진다. 같은 반경이면 통로 옆 난간에 스쳐 '막힘'이 되므로 조금 작게 본다.
            float radius = .2f;
            float bottom = StepClearance + radius;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * bottom,
                feet + Vector3.up * Mathf.Max(bottom, height - radius), radius, clearance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == clearance.Length) return false;
            for (int i = 0; i < count; i++) if (!PursuitDirectClimb.Ignored(clearance[i], transform)) return false;
            return true;
        }

        // 다음 경로 방향으로 0.55m 앞의 발 위치. 계단·경사에서는 NavMesh 높이를 따른다(현재 발 높이로 계단 속을 검사하지 않게).
        Vector3 AheadFeet()
        {
            var direction = Vector3.ProjectOnPlane(agent.hasPath ? agent.steeringTarget - transform.position : agent.desiredVelocity, Vector3.up);
            if (direction.sqrMagnitude < .0001f) direction = Vector3.ProjectOnPlane(agent.desiredVelocity, Vector3.up);
            var ahead = transform.position + (direction.sqrMagnitude > .0001f ? direction.normalized * .55f : Vector3.zero);
            if (NavMesh.SamplePosition(ahead, out var hit, .6f, agent.areaMask)) ahead = hit.position;
            return ahead;
        }

        void UpdateCrouch()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || climbing) return;
            float scale = Placement != null ? Placement.SizeScale : 1;
            var ahead = AheadFeet();
            IsCrouched = !BodyClear(transform.position, 1.8f * scale) || !BodyClear(ahead, 1.8f * scale);
            agent.height = IsCrouched ? CrouchBodyHeight : 1.8f * scale;
            if (!IsCrouched) { crouchBlockedFor = 0; return; }
            bool canMove = BodyClear(ahead, CrouchBodyHeight);
            crouchBlockedFor = canMove ? 0 : crouchBlockedFor + Time.deltaTime;
            // 앉아서도 막힌 곳은 잠시 멈췄다가 경로를 다시 찾는다. 그래도 같은 길이면 NavMesh를 믿고 지나간다(제자리 영구 정지 금지).
            if (crouchBlockedFor >= CrouchBlockedSeconds && crouchBlockedFor - Time.deltaTime < CrouchBlockedSeconds) replanRequested = true;
            bool hold = !canMove && crouchBlockedFor < CrouchBlockedSeconds;
            agent.speed = hold ? 0 : Mathf.Min(agent.speed, GameSettings.Base.Character.FreeMoveSpeed * GameSettings.Base.Character.CrouchSpeedMultiplier);
            bool moving = !hold && agent.hasPath && agent.desiredVelocity.sqrMagnitude > .01f;
            SetMotion(moving ? PursuitMotion.CrouchWalk : PursuitMotion.Crouch);
        }
    }
}
