using System;
using System.Collections.Generic;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>점프 종류. 착지했을 때 균형 충격을 고르는 데 쓴다.</summary>
    public enum JumpKind
    {
        None,
        /// <summary>제자리 점프(기본 액션).</summary>
        InPlace,
        /// <summary>옆줄로 건너뛰기.</summary>
        Lane,
        /// <summary>점프 없이 발밑이 없어져 떨어짐.</summary>
        Fall,
    }

    /// <summary>
    /// 동작 부품: 코스 이동, 제자리 점프, 옆줄 점프. 키도 콘텐츠도 모르고, 조작 규칙이 함수를 불러 움직인다.
    /// 전진 방향은 카메라가 아니라 월드에 정해진 코스 방향(목적지 쪽)이다.
    /// 기본적으로 공중에서는 뛴 순간의 수평 움직임을 유지한다. FreeAirControl을 켠 일반 이동은 체공 중에도 조작한다.
    /// 옆줄 점프의 허용 여부·착지 판정(줄 유무·합체·보정)은 외줄 기능이 <see cref="LaneJumpFilter"/> 등으로 붙인다.
    /// 목마 중 점프 규칙은 목마 기능이 정한다.
    /// 플레이어끼리는 물리로 밀지 않는다: 몸통 충돌을 끄고, 같은 줄 앞뒤에 다른 플레이어가 있으면 닿기 직전까지만 간다.
    /// 걷기·몸통 크기는 세팅(<see cref="GameSettings"/>)에서 읽는다. 점프 높이·부상 배율·중력은 이륙 때 고정하며,
    /// 줄 위 높이·속도는 콘텐츠 override가 없으면 게임 기본 · 캐릭터 세팅, 줄 간격은 외줄 세팅을 쓴다.
    /// 달리기·앉기(<see cref="SetLocomotionInput"/>): 속도 = 기본 × 부상 배율 × 자세 배율, 앉으면 제자리 점프 금지·몸통이 발 기준으로 낮아지고,
    /// 머리 위가 막혀 있으면 떼도 앉은 채 유지한다(2026-10-19 PM, 배율·비율 = 게임 기본 · 캐릭터).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        [Tooltip("목적지. 지정하면 시작 위치에서 이 지점을 향하는 방향이 전진이 된다.")]
        [SerializeField] Transform destination;

        [Tooltip("목적지가 없을 때 쓰는 월드 전진 방향. 높이(Y)는 무시한다.")]
        [SerializeField] Vector3 courseDirection = Vector3.forward;

        [Tooltip("임시(목마 기능 구현 전): 옆줄 점프 도착점이 다른 플레이어 몸통과 겹치면 점프하지 않는다. 목마 기능이 합체로 바꿀 때 끈다.")]
        [SerializeField] bool blockLaneJumpOntoPlayer = true;

        static readonly List<PlayerMover> active = new();
        static readonly Collider[] headroomHits = new Collider[16];

        CharacterController controller;
        float moveInput;
        float sideInput; // 일반 이동의 옆 입력(+1 오른쪽)
        bool sprintInput;
        bool crouchInput;
        bool jumpRequested;
        int laneJumpRequested;
        Vector3 startPosition;
        Quaternion startRotation;
        float verticalSpeed;
        Vector3 groundVelocity; // 땅에서의 수평 속도
        Vector3 airVelocity;    // 이륙 수평 속력을 보존하며 공중 입력은 방향을 바꾼다.
        float laneTargetSide;   // 옆줄 점프 도착 줄의 옆 좌표(CourseRight 방향 월드 좌표)
        bool laneAlongLimited;  // 옆줄 점프 착지 지점을 앞뒤로 보정했으면 그 거리에서 멈춘다
        float laneTargetAlong;  // 보정한 착지 지점의 앞뒤 좌표(CourseForward 방향 월드 좌표)
        float laneSpacing;      // 옆줄 점프 거리. 외줄 코스가 자기 줄 간격으로 맞춘다

        static BaseGameSettings.CharacterSettings Character => GameSettings.Base.Character;

        /// <summary>땅에서 떨어질 때(true)·착지할 때(false) 보낸다. 점프·낙하 모두 포함. 착지 알림 때 <see cref="CurrentJump"/>는 아직 남아 있다.</summary>
        public event Action<bool> AirborneChanged;

        /// <summary>출발 위치 복귀 등으로 위치를 순간이동했을 때. 온라인 위치 공유가 보간 없이 옮기는 데 쓴다.</summary>
        public event Action Teleported;

        /// <summary>공중에 있는지.</summary>
        public bool IsAirborne { get; private set; }

        /// <summary>지금 공중에 있는 이유. 땅에서는 None.</summary>
        public JumpKind CurrentJump { get; private set; }

        /// <summary>옆줄 점프 중 고정된 방향(-1 왼쪽, +1 오른쪽). 옆줄 점프가 아니면 0.</summary>
        public int LaneJumpDirection { get; private set; }

        /// <summary>
        /// 옆줄 점프를 해도 되는지 묻는 함수(방향 → 허용). 비어 있으면 항상 허용.
        /// 외줄 기능이 "그 방향에 착지할 줄이 있는지·불타지 않는지·내 위에 사람이 없는지"를 여기서 판단한다. 허용하지 않으면 입력을 무시한다.
        /// </summary>
        public Func<int, bool> LaneJumpFilter { get; set; }

        /// <summary>
        /// 옆줄 점프를 뛰는 순간 착지 지점을 코스 앞뒤로 얼마나 옮길지 묻는 함수(방향 → 미터, 음수면 뒤쪽). 비어 있으면 0.
        /// 외줄 기능이 착지 자리에 동료가 있을 때 뒤쪽으로 보정하는 데 쓴다. 공중 이동 속도에 더해 착지까지 고정된다.
        /// </summary>
        public Func<int, float> LaneLandingShift { get; set; }

        /// <summary>이번 프레임 땅에서 코스 이동 중인지(조작 잠금 반영). 균형의 이동 중 흔들림 판정에 쓴다.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>이번 프레임 코스 이동 입력. +1 전진, -1 후진. 매 프레임 넣어야 하며 넣지 않으면 정지. 공중 조작은 FreeAirControl이 허용한다.</summary>
        public void SetMoveInput(float forward) => moveInput = Mathf.Clamp(forward, -1f, 1f);

        /// <summary>
        /// 이번 프레임 일반 이동 입력(<see cref="FreeMovement"/>일 때). 코스 기준 옆(+1 오른쪽)·앞(+1 전진). 대각선도 같은 속도.
        /// 매 프레임 넣어야 하며 공중에서는 FreeAirControl을 켰을 때 반영한다.
        /// </summary>
        public void SetMoveInput(float side, float forward)
        {
            sideInput = Mathf.Clamp(side, -1f, 1f);
            moveInput = Mathf.Clamp(forward, -1f, 1f);
        }

        /// <summary>
        /// 이번 프레임 달리기·앉기 요청(누르고 있는지). 매 프레임 넣어야 하며 넣지 않으면 요청이 없는 것으로 본다(조작 규칙이 바뀌면 자동으로 풀린다).
        /// 앉기 요청이 달리기보다 우선한다. 앉기를 떼도 머리 위가 막혀 있으면 일어날 때까지 앉은 상태(<see cref="IsCrouched"/>)를 유지한다.
        /// </summary>
        public void SetLocomotionInput(bool sprint, bool crouch)
        {
            sprintInput = sprint;
            crouchInput = crouch;
        }

        /// <summary>실제로 앉아 있는지(앉기 요청과 다르다: 떼도 머리 위가 막혀 있으면 계속 앉아 있다). 앉으면 느리게 걷고 제자리 점프를 할 수 없다.</summary>
        public bool IsCrouched { get; private set; }

        /// <summary>지금 달리는 배율을 쓰는지. 앉아 있으면 달리지 않는다.</summary>
        public bool IsSprinting { get; private set; }

        /// <summary>
        /// 자세 때문에 줄어든 높이 비율(0~1). 서 있으면 1. 겉모습·1인칭 눈높이가 같이 낮아지는 데 쓴다.
        /// 부상으로 이미 낮아진 높이(<see cref="BodyHeightOverride"/>)를 기준으로 하므로 부상 높이를 두 번 줄이지 않는다.
        /// </summary>
        public float PostureHeightScale
        {
            get
            {
                float standing = StandingHeight;
                return controller != null && standing > 0.0001f ? Mathf.Clamp01(controller.height / standing) : 1f;
            }
        }

        /// <summary>남의 캐릭터(<see cref="Simulated"/>가 꺼짐)의 앉은 상태를 소유자가 보낸 값으로 맞춘다. 공간 검사는 소유자가 이미 했다.</summary>
        public void SetRemoteCrouched(bool crouched)
        {
            if (Simulated) return;
            IsCrouched = crouched;
            IsSprinting = false;
        }

        /// <summary>앉아 있으면 머리 위 공간이 있을 때만 일어선다. 대포 탑승처럼 이동을 다른 기능에 맡기기 직전에 부른다. 일어섰거나 원래 서 있으면 true.</summary>
        public bool TryStand()
        {
            crouchInput = false;
            IsSprinting = false;
            if (!IsCrouched) return true;
            if (!HasHeadroom(StandingHeight)) return false;
            IsCrouched = false;
            ApplyBodySize();
            return true;
        }

        /// <summary>
        /// 일반 이동(플랫폼): 코스 기준 8방향으로 걷고 이동 방향을 바라본다. 끄면 줄타기 이동(코스 앞뒤만, 코스 정면 바라봄).
        /// 옆줄 점프는 줄타기 이동에서만 한다.
        /// </summary>
        public bool FreeMovement { get; set; }

        /// <summary>일반 이동의 점프·낙하 중에도 매 프레임 방향 입력을 반영한다. 기본 꺼짐이며 옆줄 점프에는 적용하지 않는다.</summary>
        public bool FreeAirControl { get; set; }

        /// <summary>공간 정책으로 플레이어 간 이동 제한만 무시한다. 벽·바닥 등 환경 충돌은 유지한다.</summary>
        public bool IgnorePlayerBlocking { get; set; }

        /// <summary>
        /// 다른 플레이어가 이 플레이어 몸통을 통과하는지. 켜면 플레이어끼리 막기·옆줄 착지 겹침 검사에서 빠지고, 래그돌 등 물체와도 부딪히지 않는다.
        /// 예: 추락하면 모델은 래그돌로 떨어져 나가고 보이지 않는 몸통만 남으므로 외줄 기능이 켠다. 재시작하면 끈다.
        /// </summary>
        public bool PassThrough
        {
            get => passThrough;
            set
            {
                passThrough = value;
                if (controller != null) controller.detectCollisions = !value;
            }
        }

        bool passThrough;

        /// <summary>
        /// 이동 속도 배율(×, 0 이상). 줄 위·플랫폼 걷기 속도에 곱한다. 기본 1.
        /// 예: 두 다리를 잃으면 신체 손상 기능이 기어가기 배율을 넣는다.
        /// </summary>
        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Max(0f, value);
        }

        float speedMultiplier = 1f;

        /// <summary>콘텐츠의 줄 위 기본 이동 속도(m/s). 0 이하면 공통 세팅. 부상 배율과 별도로 적용한다.</summary>
        public float CourseSpeedOverride { get; set; }

        /// <summary>줄 위 점프 중력 배율. 점프 높이는 보존한다. 기본 1, 플랫폼에는 적용하지 않는다.</summary>
        public float CourseJumpGravityMultiplier { get; set; } = 1f;

        /// <summary>콘텐츠의 줄 위 기본 점프 높이(m). 0 이하면 공통 세팅. 플랫폼은 무시하며 부상 배율은 별도로 곱한다.</summary>
        public float CourseJumpHeightOverride { get; set; }

        float jumpGravity, jumpHeight;
        float LaunchGravity => Character.Gravity * (FreeMovement ? 1f : Mathf.Max(0.1f, CourseJumpGravityMultiplier));
        float EffectiveGravity => CurrentJump != JumpKind.None && jumpGravity > 0f ? jumpGravity : LaunchGravity;
        float LaunchHeight => (FreeMovement || CourseJumpHeightOverride <= 0f ? Character.JumpHeight : CourseJumpHeightOverride) * jumpHeightMultiplier;
        float EffectiveJumpHeight => CurrentJump != JumpKind.None ? jumpHeight : LaunchHeight;

        /// <summary>현재 점프에 적용되는 중력(m/s²). 목마 위 제자리 점프도 같은 높이·체공 규칙을 쓴다.</summary>
        public float JumpGravity => EffectiveGravity;

        /// <summary>
        /// 점프 높이 배율(×, 0 이상). 콘텐츠가 고른 높이 또는 게임 기본 · 캐릭터 높이에 곱한다. 기본 1.
        /// 예: 다리를 잃으면 신체 손상 기능이 다리 단계별 배율을 넣는다(부상 기획: 탐색·공연 공통).
        /// </summary>
        public float JumpHeightMultiplier
        {
            get => jumpHeightMultiplier;
            set => jumpHeightMultiplier = Mathf.Max(0f, value);
        }

        float jumpHeightMultiplier = 1f;

        /// <summary>제자리 점프를 할 수 있는지. 끄면 점프 요청을 무시한다. 기본 켜짐.</summary>
        public bool JumpAllowed { get; set; } = true;

        /// <summary>옆줄 점프를 할 수 있는지. 끄면 옆줄 점프 요청을 무시한다(제자리 점프로 바뀌지 않음). 기본 켜짐.</summary>
        public bool LaneJumpAllowed { get; set; } = true;

        /// <summary>
        /// 몸통(캡슐) 높이를 세팅 대신 이 값(m)으로 쓴다. 0 이하면 세팅 값. 발 위치는 그대로다.
        /// 예: 두 다리를 잃어 기어가면 신체 손상 기능이 낮은 높이를 넣는다.
        /// </summary>
        public float BodyHeightOverride { get; set; }

        /// <summary>제자리 점프를 요청한다. 땅에 있고 조작이 켜져 있을 때만 이번 프레임에 뛴다.</summary>
        public void RequestJump() => jumpRequested = true;

        /// <summary>옆줄 점프를 요청한다(-1 왼쪽, +1 오른쪽). 땅에 있고 조작이 켜져 있고 <see cref="LaneJumpFilter"/>가 허용할 때만 뛴다. 방향은 뛴 순간 고정된다.</summary>
        public void RequestLaneJump(int direction) => laneJumpRequested = Math.Sign(direction);

        /// <summary>
        /// 땅에 없어도 지금 자리에서 제자리 점프를 바로 시작한다(예: 목마 맨 위 앞·뒤 분리 점프). 이동을 계산 중(<see cref="Simulated"/>)이어야 한다.
        /// <paramref name="alongSpeed"/>: 코스 앞(+)·뒤(−) 수평 속도(m/s), 착지까지 고정. 높이·공중·착지 처리는 보통 점프와 같다.
        /// </summary>
        public void LaunchJump(float alongSpeed)
        {
            groundVelocity = CourseForward * alongSpeed;
            IsMoving = false;
            StartJump(JumpKind.InPlace, 0);
        }

        /// <summary>봉 등 외부 이동과 인계할 때 이전 점프·옆줄 목표를 비운다. 위치·조작 규칙은 바꾸지 않는다.</summary>
        public void ResetExternalMotion(bool airborne)
        {
            ClearPendingInput();
            verticalSpeed = 0f;
            groundVelocity = Vector3.zero;
            IsMoving = false;
            ClearJump();
            if (airborne) CurrentJump = JumpKind.Fall;
            SetAirborne(airborne);
        }

        /// <summary>목마 머리 위에서도 기존 허용·착지 규칙으로 옆줄 점프를 시작한다. 불가능하면 이동 상태를 바꾸지 않는다.</summary>
        public bool LaunchLaneJump(int direction)
        {
            direction = Math.Sign(direction);
            if (direction == 0 || !ControlEnabled || FreeMovement) return false;
            Vector3 before = groundVelocity;
            groundVelocity = Vector3.zero; // 위층에서 이동하지 않는 동안 남은 지상 속도는 쓰지 않는다
            if (!CanLaneJump(direction))
            {
                groundVelocity = before;
                return false;
            }
            IsMoving = false;
            StartJump(JumpKind.Lane, direction);
            SetAirborne(true); // 추종 부품이 공중 상태를 이동 부품에 바로 인계할 수 있게 한다
            return true;
        }

        /// <summary>지금 켜져 있는 모든 플레이어 이동 부품.</summary>
        public static IReadOnlyList<PlayerMover> All => active;

        /// <summary>몸통 충돌체.</summary>
        public CharacterController Body => controller;

        /// <summary>줄 간격(m). 옆줄 점프 거리와 같다. 처음에는 외줄 세팅의 줄 간격, 코스가 있으면 코스가 자기 줄 간격으로 맞춘다.</summary>
        public float LaneSpacing
        {
            get => laneSpacing;
            set => laneSpacing = Mathf.Max(0f, value);
        }

        /// <summary>옆줄 점프 도착점이 다른 플레이어와 겹치면 점프를 막을지. 목마 기능이 합체 규칙을 붙일 때 끈다.</summary>
        public bool BlockLaneJumpOntoPlayer
        {
            get => blockLaneJumpOntoPlayer;
            set => blockLaneJumpOntoPlayer = value;
        }

        /// <summary>몸통 반지름(m, 수평).</summary>
        public float BodyRadius => controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

        /// <summary>몸통 높이(m).</summary>
        public float BodyHeight => controller.height * transform.lossyScale.y;

        /// <summary>
        /// 지금 그 방향(-1 왼쪽, +1 오른쪽)으로 옆줄 점프하면 도착점에서 겹치는 다른 플레이어. 없으면 null.
        /// 도착점 = 옆으로 줄 간격 + 앞뒤로 뛸 때 속도 × 공중 시간. 목마 기능은 이 결과로 합체할 상대를 고른다.
        /// </summary>
        public PlayerMover FindPlayerAtLaneLanding(int direction)
        {
            Vector3 landing = GetLaneLandingPosition(direction);

            foreach (var other in active)
            {
                if (other == this || other.PassThrough) continue;
                Vector3 delta = other.transform.position - landing;
                if (Mathf.Abs(delta.y) >= Mathf.Max(BodyHeight, other.BodyHeight)) continue; // 위아래로 떨어져 있음(목마 등)
                delta.y = 0f;
                if (delta.magnitude < BodyRadius + other.BodyRadius) return other;
            }
            return null;
        }

        /// <summary>지금 그 방향(-1 왼쪽, +1 오른쪽)으로 옆줄 점프하면 착지할 예상 위치. 옆으로 줄 간격 + 앞뒤로 뛸 때 속도 × 공중 시간.</summary>
        public Vector3 GetLaneLandingPosition(int direction) =>
            transform.position
            + CourseRight * (Math.Sign(direction) * laneSpacing)
            + CourseForward * (Vector3.Dot(groundVelocity, CourseForward) * AirTime);

        /// <summary>점프 초속(m/s). 공중에서는 이륙 시 선택한 높이·부상 배율·중력으로 계산한 값을 유지한다.</summary>
        public float JumpSpeed => Mathf.Sqrt(2f * EffectiveGravity * EffectiveJumpHeight);

        /// <summary>같은 높이로 착지할 때까지의 공중 시간(초). 이륙 후 설정·조작 규칙·부상 배율이 바뀌어도 유지한다.</summary>
        public float AirTime => 2f * JumpSpeed / EffectiveGravity;

        /// <summary>이동 명령을 받는지. 꺼져 있으면 이동·점프를 무시한다(중력은 계속 적용).</summary>
        public bool ControlEnabled { get; private set; } = true;

        /// <summary>
        /// 이 컴퓨터가 이동을 계산하는지. 끄면 중력·이동·회전을 하지 않고 위치를 다른 곳(예: 온라인 위치 공유)에 맡긴다.
        /// 꺼져 있어도 다른 플레이어의 앞뒤 막힘·옆줄 착지 검사 대상에는 남는다.
        /// </summary>
        public bool Simulated { get; set; } = true;

        /// <summary>조작을 켜거나 끈다. 예: 추락한 플레이어는 재시작까지 끈다.</summary>
        public void SetControlEnabled(bool enabled)
        {
            ControlEnabled = enabled;
            if (!enabled) ClearPendingInput();
        }

        /// <summary>아직 실행하지 않은 입력만 비운다. 공중 속도·중력·착지 상태와 실제 앉은 상태(머리 위가 막혀 있을 수 있음)는 유지한다.</summary>
        public void ClearPendingInput()
        {
            moveInput = sideInput = 0f;
            sprintInput = crouchInput = false;
            IsSprinting = false;
            jumpRequested = false;
            laneJumpRequested = 0;
        }

        /// <summary>출발 위치·방향으로 되돌린다(재시작).</summary>
        public void ResetToStart() => Relocate(startPosition, startRotation);

        /// <summary>출발 위치는 그대로 두고 지금 위치만 옮긴다(예: 코스 밖으로 떨어진 뒤 마지막 대포 앞으로). 다른 화면도 보간 없이 옮긴다.</summary>
        public void Relocate(Vector3 position, Quaternion rotation)
        {
            ClearPendingInput();
            controller.enabled = false; // CharacterController는 꺼야 위치를 바로 옮길 수 있다
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            IgnoreOtherPlayers();
            verticalSpeed = 0f;
            groundVelocity = Vector3.zero;
            SetAirborne(false);
            ClearJump();
            Teleported?.Invoke();
        }

        /// <summary>출발 위치·방향을 바꾸고 그 자리로 옮긴다. 예: 온라인 접속 후 자리 번호에 맞는 출발점에 세울 때.</summary>
        public void SetStartPose(Vector3 position, Quaternion rotation) => SetStartPose(position, rotation, true);

        /// <summary>출발 위치·방향을 바꾼다. <paramref name="moveNow"/>가 false면 옮기지 않고 다음 재시작부터 쓴다.</summary>
        public void SetStartPose(Vector3 position, Quaternion rotation, bool moveNow)
        {
            startPosition = position;
            startRotation = rotation;
            if (moveNow) ResetToStart();
        }

        /// <summary>코스 전진 방향(수평, 정규화). 방향을 정할 수 없으면 Vector3.zero.</summary>
        public Vector3 CourseForward
        {
            get
            {
                Vector3 dir = CourseForwardOverride ?? (destination != null ? destination.position - startPosition : courseDirection);
                dir.y = 0f;
                return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
            }
        }

        /// <summary>코스 기준 오른쪽(수평, 정규화).</summary>
        public Vector3 CourseRight => Vector3.Cross(Vector3.up, CourseForward);

        /// <summary>구간 조작 동안만 사용할 전진축. null이면 원래 목적지/코스 방향을 사용하며 재시작 위치는 바꾸지 않는다.</summary>
        public Vector3? CourseForwardOverride { get; set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            // 높은 프레임에서 작은 이동·접지 보정도 누락 없이 적용한다.
            controller.minMoveDistance = 0f;
            laneSpacing = GameSettings.Tightrope.Course.LaneSpacing;
            ApplyBodySize(true);
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        void OnEnable()
        {
            active.Add(this);
            IgnoreOtherPlayers();
        }

        /// <summary>
        /// 플레이어 몸통끼리는 물리로 밀지 않는다(앞뒤 막힘은 LimitAlongCourse가 처리).
        /// IgnoreCollision은 콜라이더를 껐다 켜면 풀리므로 다시 켤 때마다 부른다.
        /// </summary>
        void IgnoreOtherPlayers()
        {
            foreach (var other in active)
                if (other != this) Physics.IgnoreCollision(controller, other.controller, true);
        }

        void OnDisable() => active.Remove(this);

        /// <summary>서 있을 때 몸통 높이(m, 로컬). 부상 높이(<see cref="BodyHeightOverride"/>)가 있으면 그 값.</summary>
        float StandingHeight => BodyHeightOverride > 0f ? BodyHeightOverride : Character.CapsuleHeight;

        /// <summary>지금 자세의 목표 몸통 높이(m, 로컬). 앉으면 정상 캡슐 × 앉기 비율과 부상 높이 중 낮은 값(부상 높이를 두 번 줄이지 않는다).</summary>
        float TargetHeight => IsCrouched
            ? Mathf.Min(Character.CapsuleHeight * Character.CrouchHeightRatio, StandingHeight)
            : StandingHeight;

        /// <summary>자세에 따른 이동 속도 배율. 앉기가 달리기보다 우선한다.</summary>
        float PostureSpeedMultiplier => IsCrouched ? Character.CrouchSpeedMultiplier : IsSprinting ? Character.SprintSpeedMultiplier : 1f;

        /// <summary>
        /// 앉기 요청 → 실제 앉은 상태·달리기 상태를 정한다. 앉기를 떼면 머리 위 공간이 있을 때만 일어선다.
        /// 위치를 다른 곳에 맡긴 동안(<see cref="Simulated"/> 꺼짐)은 바꾸지 않는다(남의 캐릭터는 <see cref="SetRemoteCrouched"/>).
        /// </summary>
        void UpdatePosture()
        {
            if (!Simulated)
            {
                IsSprinting = false;
                return;
            }
            if (ControlEnabled && crouchInput) IsCrouched = true;
            else if (IsCrouched && HasHeadroom(StandingHeight)) IsCrouched = false;
            IsSprinting = ControlEnabled && sprintInput && !IsCrouched;
        }

        /// <summary>
        /// 몸통을 <paramref name="height"/>(m, 로컬)까지 늘려도 머리 위가 막히지 않는지. 지금 몸통 윗부분부터 목표 높이까지만 검사한다.
        /// 자기 몸·자식(모델)·트리거·다른 플레이어 몸통(서로 통과)은 무시한다.
        /// </summary>
        bool HasHeadroom(float height)
        {
            if (controller == null || !controller.enabled) return true;
            float scale = transform.lossyScale.y;
            float current = controller.height * scale;
            float target = height * scale;
            if (target <= current + 0.001f) return true;

            float radius = Mathf.Min(Character.CapsuleRadius, height * 0.5f)
                * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * 0.95f; // 옆벽에 닿은 것은 막힘으로 보지 않는다
            Vector3 feet = transform.position;
            Vector3 bottom = feet + Vector3.up * Mathf.Max(radius, current - radius);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius, target - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, headroomHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = headroomHits[i];
                if (hit == null || hit == controller || hit.transform.IsChildOf(transform)) continue;
                if (hit is CharacterController && hit.TryGetComponent(out PlayerMover _)) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 몸통(캡슐) 크기를 세팅(또는 <see cref="BodyHeightOverride"/>, 앉은 자세)에 맞춘다. 발 위치(원점)는 그대로 두고 중심만 옮긴다.
        /// 몸이 커질 때(기립·부상 회복·세팅 변경)는 머리 위가 막혀 있으면 지금 높이를 유지한다. <paramref name="force"/>면 검사하지 않는다(시작·남의 캐릭터).
        /// </summary>
        void ApplyBodySize(bool force = false)
        {
            float height = TargetHeight;
            if (!force && Simulated && height > controller.height + 0.001f && !HasHeadroom(height))
                height = controller.height;
            float radius = Mathf.Min(Character.CapsuleRadius, height * 0.5f);
            if (Mathf.Approximately(controller.height, height) && Mathf.Approximately(controller.radius, radius)) return;
            controller.height = height;
            controller.radius = radius;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        void Update()
        {
            // 부상 높이·배율은 부상 기능이 따로 넣고(자세와 독립), 여기서 자세를 정한 뒤 둘을 합쳐 몸통 크기를 맞추고 이동한다.
            UpdatePosture();
            ApplyBodySize(!Simulated);
            if (!Simulated)
            {
                ClearPendingInput();
                return;
            }

            Vector3 forward = CourseForward;
            if (!FreeMovement && forward != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            bool grounded = controller.isGrounded && verticalSpeed <= 0f;
            float move = ControlEnabled ? moveInput : 0f;

            if (grounded)
            {
                if (FreeMovement)
                {
                    float side = ControlEnabled ? sideInput : 0f;
                    Vector3 input = Vector3.ClampMagnitude(forward * move + CourseRight * side, 1f);
                    groundVelocity = input * (Character.FreeMoveSpeed * speedMultiplier * PostureSpeedMultiplier);
                    IsMoving = input.sqrMagnitude > 0.0001f;
                    if (IsMoving)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation,
                            Quaternion.LookRotation(input, Vector3.up), Character.TurnSpeed * Time.deltaTime);
                }
                else
                {
                    float speed = CourseSpeedOverride > 0f ? CourseSpeedOverride : Character.CourseMoveSpeed;
                    groundVelocity = forward * (move * speed * speedMultiplier * PostureSpeedMultiplier);
                    IsMoving = !Mathf.Approximately(move, 0f);
                }

                if (ControlEnabled && !FreeMovement && laneJumpRequested != 0)
                {
                    if (LaneJumpAllowed && CanLaneJump(laneJumpRequested)) StartJump(JumpKind.Lane, laneJumpRequested);
                    else verticalSpeed = -1f; // 옆줄 점프를 막아도 제자리 점프로 바꾸지 않는다
                }
                else if (ControlEnabled && jumpRequested && JumpAllowed && !IsCrouched) // 앉아 있으면(일어설 공간이 없어 유지 중이어도) 뛰지 않는다
                    StartJump(JumpKind.InPlace, 0);
                else
                    verticalSpeed = -1f; // 땅에 붙어 있도록 작은 하강 속도를 유지한다
            }
            else
            {
                IsMoving = false;
                if (FreeMovement && FreeAirControl && ControlEnabled && CurrentJump != JumpKind.Lane)
                {
                    Vector3 input = Vector3.ClampMagnitude(forward * move + CourseRight * sideInput, 1f);
                    if (CurrentJump == JumpKind.None) airVelocity = groundVelocity;
                    if (input.sqrMagnitude > .0001f)
                    {
                        // Keep takeoff speed while steering; releasing movement stops horizontal travel.
                        // A stationary jump can still begin moving using the existing common settings.
                        float speed = airVelocity.magnitude;
                        airVelocity = speed > .0001f ? input.normalized * speed
                            : input * (Character.FreeMoveSpeed * speedMultiplier * PostureSpeedMultiplier);
                    }
                    else airVelocity = Vector3.zero;
                    // 걸어서 낭떠러지를 벗어난 첫 프레임도 같은 입력으로 이동한다.
                    if (CurrentJump == JumpKind.None) groundVelocity = airVelocity;
                    if (input.sqrMagnitude > .0001f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation,
                            Quaternion.LookRotation(input, Vector3.up), Character.TurnSpeed * Time.deltaTime);
                }
            }

            Vector3 horizontal = CurrentJump != JumpKind.None ? airVelocity : groundVelocity;
            bool ballistic = CurrentJump != JumpKind.None || !grounded;
            var velocity = new Vector3(horizontal.x, verticalSpeed, horizontal.z);
            Vector3 displacement = ballistic ? BallisticMotion.Step(ref velocity, Vector3.zero, EffectiveGravity, Time.deltaTime)
                : velocity * Time.deltaTime;
            verticalSpeed = velocity.y;
            Vector3 horizontalStep = new(displacement.x, 0f, displacement.z);
            Vector3 step = FreeMovement
                ? LimitAgainstPlayers(horizontalStep)
                : LimitAlongCourse(horizontalStep, forward);
            if (CurrentJump == JumpKind.Lane) step = LimitToLaneTarget(step);
            controller.Move(step + Vector3.up * displacement.y);

            // 입력은 한 프레임만 유효하다. 조작 규칙이 매 프레임 다시 넣는다.
            moveInput = 0f;
            sideInput = 0f;
            sprintInput = crouchInput = false;
            jumpRequested = false;
            laneJumpRequested = 0;

            UpdateAirborne();
        }

        bool CanLaneJump(int direction)
        {
            if (LaneJumpFilter != null && !LaneJumpFilter(direction)) return false;
            return !blockLaneJumpOntoPlayer || FindPlayerAtLaneLanding(direction) == null;
        }

        /// <summary>
        /// 이번 프레임 수평 이동 중 코스 앞뒤 성분을, 같은 줄(옆으로 몸이 겹치는 범위) 앞뒤 플레이어에 닿기 직전까지로 줄인다.
        /// 옆 성분(옆줄 점프)은 그대로 둔다.
        /// </summary>
        Vector3 LimitAlongCourse(Vector3 step, Vector3 forward)
        {
            if (forward == Vector3.zero) return step;
            float along = Vector3.Dot(step, forward);
            if (Mathf.Approximately(along, 0f)) return step;

            float sign = Mathf.Sign(along);
            float allowed = Mathf.Abs(along);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 origin = transform.position + (step - forward * along); // 옆으로 먼저 간 위치 기준

            foreach (var other in active)
            {
                if (other == this || other.PassThrough) continue;
                Vector3 delta = other.transform.position - origin;
                if (Mathf.Abs(delta.y) >= Mathf.Max(BodyHeight, other.BodyHeight)) continue;

                float reach = BodyRadius + other.BodyRadius;
                float side = Vector3.Dot(delta, right);
                if (Mathf.Abs(side) >= reach) continue; // 다른 줄

                float ahead = Vector3.Dot(delta, forward) * sign;
                if (ahead <= 0f) continue; // 가는 방향의 반대쪽

                float contact = Mathf.Sqrt(reach * reach - side * side) + Character.PlayerGap;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, ahead - contact));
            }

            return step + forward * (sign * allowed - along);
        }

        /// <summary>
        /// 일반 이동(플랫폼): 이번 프레임 수평 이동이 다른 플레이어 몸통 안으로 들어가지 않게, 그 플레이어 쪽 성분을 뺀다(옆으로 미끄러짐).
        /// 이미 겹쳐 있어도 멀어지는 방향으로는 움직일 수 있다.
        /// </summary>
        Vector3 LimitAgainstPlayers(Vector3 step)
        {
            if (IgnorePlayerBlocking) return step;
            Vector3 position = transform.position;
            foreach (var other in active)
            {
                if (other == this || other.PassThrough || other.IgnorePlayerBlocking) continue;
                Vector3 toOther = other.transform.position - position;
                if (Mathf.Abs(toOther.y) >= Mathf.Max(BodyHeight, other.BodyHeight)) continue;
                toOther.y = 0f;

                float reach = BodyRadius + other.BodyRadius + Character.PlayerGap;
                Vector3 next = toOther - step;
                if (next.sqrMagnitude >= reach * reach || Vector3.Dot(step, toOther) <= 0f) continue;

                Vector3 normal = toOther.sqrMagnitude > 0.0001f ? toOther.normalized : transform.forward;
                step -= normal * Vector3.Dot(step, normal);
            }
            return step;
        }

        /// <summary>몸통이 부딪힌 물체(래그돌 등 움직이는 Rigidbody)를 미는 방향으로 민다. 위에서 밟은 경우는 밀지 않는다.</summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.rigidbody;
            float pushSpeed = Character.PushSpeed;
            if (pushSpeed <= 0f || body == null || body.isKinematic || hit.moveDirection.y < -0.3f) return;

            Vector3 direction = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();
            float current = Vector3.Dot(body.linearVelocity, direction);
            if (current < pushSpeed) body.AddForce(direction * (pushSpeed - current), ForceMode.VelocityChange);
        }

        /// <summary>옆줄 점프 중 옆 이동이 도착 줄을 넘어가지 않게 줄인다. 착지 지점을 앞뒤로 보정했으면 앞뒤도 그 지점에서 멈춘다.</summary>
        Vector3 LimitToLaneTarget(Vector3 step)
        {
            step = LimitAxis(step, CourseRight, laneTargetSide);
            return laneAlongLimited ? LimitAxis(step, CourseForward, laneTargetAlong) : step;
        }

        Vector3 LimitAxis(Vector3 step, Vector3 axis, float target)
        {
            float amount = Vector3.Dot(step, axis);
            float remaining = target - Vector3.Dot(transform.position, axis);
            float limited = Mathf.Sign(amount) == Mathf.Sign(remaining) ? Mathf.Sign(remaining) * Mathf.Min(Mathf.Abs(amount), Mathf.Abs(remaining)) : 0f;
            return step + axis * (limited - amount);
        }

        void StartJump(JumpKind kind, int laneDirection)
        {
            jumpGravity = LaunchGravity; // 착지 전 조작 규칙이 바뀌어도 궤적과 옆줄 도착 위치를 유지한다.
            jumpHeight = LaunchHeight;
            CurrentJump = kind;
            LaneJumpDirection = laneDirection;
            verticalSpeed = JumpSpeed;

            // 뛴 순간의 수평 움직임을 착지까지 고정한다
            airVelocity = groundVelocity;
            if (kind == JumpKind.Lane)
            {
                airVelocity += CourseRight * (laneDirection * laneSpacing / AirTime);
                laneTargetSide = Vector3.Dot(transform.position, CourseRight) + laneDirection * laneSpacing;

                float shift = LaneLandingShift != null ? LaneLandingShift(laneDirection) : 0f;
                laneAlongLimited = !Mathf.Approximately(shift, 0f);
                if (laneAlongLimited)
                {
                    airVelocity += CourseForward * (shift / AirTime);
                    laneTargetAlong = Vector3.Dot(transform.position, CourseForward)
                        + Vector3.Dot(groundVelocity, CourseForward) * AirTime + shift;
                }
            }
        }

        void UpdateAirborne()
        {
            bool airborne = !controller.isGrounded;
            if (airborne == IsAirborne) return;

            if (airborne && CurrentJump == JumpKind.None)
            {
                // 점프 없이 떨어지는 중: 걷던 속도를 그대로 유지
                jumpGravity = LaunchGravity;
                jumpHeight = LaunchHeight;
                CurrentJump = JumpKind.Fall;
                airVelocity = groundVelocity;
            }

            // 착지 프레임·땅 판정 여유로 생긴 옆 오차를 없애 도착 줄 위에 정확히 세운다
            if (!airborne && CurrentJump == JumpKind.Lane)
                controller.Move(CourseRight * (laneTargetSide - Vector3.Dot(transform.position, CourseRight))
                    + Vector3.down * controller.skinWidth); // 아래로 살짝 눌러 땅 판정을 유지한다

            SetAirborne(airborne);
            if (!airborne) ClearJump();
        }

        void SetAirborne(bool airborne)
        {
            if (IsAirborne == airborne) return;
            IsAirborne = airborne;
            AirborneChanged?.Invoke(airborne); // 착지 알림 때 CurrentJump를 읽을 수 있도록 지우기 전에 보낸다
        }

        void ClearJump()
        {
            CurrentJump = JumpKind.None;
            jumpGravity = 0f;
            jumpHeight = 0f;
            LaneJumpDirection = 0;
            airVelocity = Vector3.zero;
            laneAlongLimited = false;
        }
    }
}
