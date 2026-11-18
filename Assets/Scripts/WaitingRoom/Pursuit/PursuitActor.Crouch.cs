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
        // agent가 꺼진 직접 이동(소품 위·환풍구 윗면)에서는 몸이 향한 쪽을 본다.
        Vector3 AheadFeet()
        {
            bool navigating = agent != null && agent.enabled && agent.isOnNavMesh;
            var direction = navigating
                ? Vector3.ProjectOnPlane(agent.hasPath ? agent.steeringTarget - transform.position : agent.desiredVelocity, Vector3.up)
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (navigating && direction.sqrMagnitude < .0001f) direction = Vector3.ProjectOnPlane(agent.desiredVelocity, Vector3.up);
            var ahead = transform.position + (direction.sqrMagnitude > .0001f ? direction.normalized * .55f : Vector3.zero);
            if (navigating && NavMesh.SamplePosition(ahead, out var hit, .6f, agent.areaMask)) ahead = hit.position;
            return ahead;
        }

        /// <summary>
        /// 2026-11-18 핫픽스: 환풍구처럼 앉아서 가야 하는 좁은 길에서 선 채로 쫓아오던 문제.
        /// - 천장 이음매·창살 틈에서 한 프레임 검사가 '트임'으로 나와 바로 일어서던 것을 막기 위해, 머리 위가 잠시 계속 트여야 일어선다.
        /// - 앉은 판정을 이동 판단보다 먼저 정해 <see cref="SetMotion"/>이 달리기·걷기·대기를 앉은 동작으로 바꾼다(소품 위 직접 이동·펜스 넘기 포함).
        /// </summary>
        const float StandUpClearSeconds = .35f;
        float standClearFor;

        void RefreshCrouch()
        {
            if (climbing || vaulting) return;
            float height = 1.8f * (Placement != null ? Placement.SizeScale : 1);
            bool narrow = !BodyClear(transform.position, height) || !BodyClear(AheadFeet(), height);
            if (narrow) { IsCrouched = true; standClearFor = 0; return; }
            if (!IsCrouched) return;
            standClearFor += Time.deltaTime;
            if (standClearFor >= StandUpClearSeconds) { IsCrouched = false; standClearFor = 0; }
        }

        // 앉은 동안 서서 하는 이동·대기 동작은 앉은 동작으로 바꾼다. 원격 화면은 호스트가 정한 동작을 그대로 쓴다.
        PursuitMotion CrouchAdjusted(PursuitMotion next)
        {
            if (remote || !IsCrouched) return next;
            return next switch {
                PursuitMotion.Run or PursuitMotion.Walk => PursuitMotion.CrouchWalk,
                PursuitMotion.Awaken => PursuitMotion.Crouch,
                _ => next,
            };
        }

        static float CrouchMoveSpeed => GameSettings.Base.Character.FreeMoveSpeed * GameSettings.Base.Character.CrouchSpeedMultiplier;

        void UpdateCrouch()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || climbing) return;
            float scale = Placement != null ? Placement.SizeScale : 1;
            var ahead = AheadFeet();
            agent.height = IsCrouched ? CrouchBodyHeight : 1.8f * scale;
            if (!IsCrouched) { crouchBlockedFor = 0; return; }
            bool canMove = BodyClear(ahead, CrouchBodyHeight);
            crouchBlockedFor = canMove ? 0 : crouchBlockedFor + Time.deltaTime;
            // 앉아서도 막힌 곳은 잠시 멈췄다가 경로를 다시 찾는다. 그래도 같은 길이면 NavMesh를 믿고 지나간다(제자리 영구 정지 금지).
            if (crouchBlockedFor >= CrouchBlockedSeconds && crouchBlockedFor - Time.deltaTime < CrouchBlockedSeconds) replanRequested = true;
            bool hold = !canMove && crouchBlockedFor < CrouchBlockedSeconds;
            agent.speed = hold ? 0 : Mathf.Min(agent.speed, CrouchMoveSpeed);
            bool moving = !hold && agent.hasPath && agent.desiredVelocity.sqrMagnitude > .01f;
            SetMotion(moving ? PursuitMotion.CrouchWalk : PursuitMotion.Crouch);
        }
    }
}
