using System;
using System.Collections.Generic;
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
    /// 공중에서는 뛴 순간의 수평 움직임(앞뒤·옆줄 방향)을 착지할 때까지 그대로 유지하고 입력으로 바꾸지 않는다.
    /// 옆줄 점프의 허용 여부·착지 판정(줄 유무·합체·보정)은 외줄 기능이 <see cref="LaneJumpFilter"/> 등으로 붙인다.
    /// 목마 중 점프 규칙은 목마 기능이 정한다.
    /// 플레이어끼리는 물리로 밀지 않는다: 몸통 충돌을 끄고, 같은 줄 앞뒤에 다른 플레이어가 있으면 닿기 직전까지만 간다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        [Tooltip("목적지. 지정하면 시작 위치에서 이 지점을 향하는 방향이 전진이 된다.")]
        [SerializeField] Transform destination;

        [Tooltip("목적지가 없을 때 쓰는 월드 전진 방향. 높이(Y)는 무시한다.")]
        [SerializeField] Vector3 courseDirection = Vector3.forward;

        [Tooltip("초당 이동 거리(m).")]
        [SerializeField, Min(0f)] float moveSpeed = 3f;

        [Tooltip("중력 크기(m/s²). 기획 기준 9.81.")]
        [SerializeField, Min(0.1f)] float gravityStrength = 9.81f;

        [Tooltip("제자리 점프 높이(m, 발 기준 최고점). 기획 제안값 1.0. 점프 초속 = √(2 × 중력 × 높이).")]
        [SerializeField, Min(0f)] float jumpHeight = 1f;

        [Tooltip("옆줄 점프로 옆으로 이동할 거리(m) = 줄 간격. 기획 제안값 1.5 → 2026-10-02 2.0으로 조정(기울기 25°에서 옆줄 플레이어와 겹치지 않게). 같은 높이에 착지하는 공중 시간 동안 이 거리를 간다.")]
        [SerializeField, Min(0f)] float laneSpacing = 2f;

        [Tooltip("다른 플레이어와 앞뒤로 닿을 때 남길 틈(m).")]
        [SerializeField, Min(0f)] float playerGap = 0.05f;

        [Tooltip("임시(목마 기능 구현 전): 옆줄 점프 도착점이 다른 플레이어 몸통과 겹치면 점프하지 않는다. 목마 기능이 합체로 바꿀 때 끈다.")]
        [SerializeField] bool blockLaneJumpOntoPlayer = true;

        static readonly List<PlayerMover> active = new();

        CharacterController controller;
        float moveInput;
        bool jumpRequested;
        int laneJumpRequested;
        Vector3 startPosition;
        Quaternion startRotation;
        float verticalSpeed;
        Vector3 groundVelocity; // 땅에서의 수평 속도
        Vector3 airVelocity;    // 공중에서 고정해 쓰는 수평 속도
        float laneTargetSide;   // 옆줄 점프 도착 줄의 옆 좌표(CourseRight 방향 월드 좌표)

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

        /// <summary>이번 프레임 땅에서 코스 이동 중인지(조작 잠금 반영). 균형의 이동 중 흔들림 판정에 쓴다.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>이번 프레임 코스 이동 입력. +1 전진, -1 후진. 매 프레임 넣어야 하며 넣지 않으면 정지. 공중에서는 무시한다.</summary>
        public void SetMoveInput(float forward) => moveInput = Mathf.Clamp(forward, -1f, 1f);

        /// <summary>제자리 점프를 요청한다. 땅에 있고 조작이 켜져 있을 때만 이번 프레임에 뛴다.</summary>
        public void RequestJump() => jumpRequested = true;

        /// <summary>옆줄 점프를 요청한다(-1 왼쪽, +1 오른쪽). 땅에 있고 조작이 켜져 있고 <see cref="LaneJumpFilter"/>가 허용할 때만 뛴다. 방향은 뛴 순간 고정된다.</summary>
        public void RequestLaneJump(int direction) => laneJumpRequested = Math.Sign(direction);

        /// <summary>지금 켜져 있는 모든 플레이어 이동 부품.</summary>
        public static IReadOnlyList<PlayerMover> All => active;

        /// <summary>몸통 충돌체.</summary>
        public CharacterController Body => controller;

        /// <summary>줄 간격(m). 옆줄 점프 거리와 같다.</summary>
        public float LaneSpacing => laneSpacing;

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
            Vector3 landing = transform.position
                + CourseRight * (Math.Sign(direction) * laneSpacing)
                + CourseForward * (Vector3.Dot(groundVelocity, CourseForward) * AirTime);

            foreach (var other in active)
            {
                if (other == this) continue;
                Vector3 delta = other.transform.position - landing;
                if (Mathf.Abs(delta.y) >= Mathf.Max(BodyHeight, other.BodyHeight)) continue; // 위아래로 떨어져 있음(목마 등)
                delta.y = 0f;
                if (delta.magnitude < BodyRadius + other.BodyRadius) return other;
            }
            return null;
        }

        /// <summary>점프 초속(m/s).</summary>
        public float JumpSpeed => Mathf.Sqrt(2f * gravityStrength * jumpHeight);

        /// <summary>같은 높이로 착지할 때까지의 공중 시간(초).</summary>
        public float AirTime => 2f * JumpSpeed / gravityStrength;

        /// <summary>이동 명령을 받는지. 꺼져 있으면 이동·점프를 무시한다(중력은 계속 적용).</summary>
        public bool ControlEnabled { get; private set; } = true;

        /// <summary>
        /// 이 컴퓨터가 이동을 계산하는지. 끄면 중력·이동·회전을 하지 않고 위치를 다른 곳(예: 온라인 위치 공유)에 맡긴다.
        /// 꺼져 있어도 다른 플레이어의 앞뒤 막힘·옆줄 착지 검사 대상에는 남는다.
        /// </summary>
        public bool Simulated { get; set; } = true;

        /// <summary>조작을 켜거나 끈다. 예: 추락한 플레이어는 재시작까지 끈다.</summary>
        public void SetControlEnabled(bool enabled) => ControlEnabled = enabled;

        /// <summary>출발 위치·방향으로 되돌린다(재시작).</summary>
        public void ResetToStart()
        {
            controller.enabled = false; // CharacterController는 꺼야 위치를 바로 옮길 수 있다
            transform.SetPositionAndRotation(startPosition, startRotation);
            controller.enabled = true;
            IgnoreOtherPlayers();
            verticalSpeed = 0f;
            groundVelocity = Vector3.zero;
            SetAirborne(false);
            ClearJump();
            Teleported?.Invoke();
        }

        /// <summary>출발 위치·방향을 바꾸고 그 자리로 옮긴다. 예: 온라인 접속 후 자리 번호에 맞는 출발점에 세울 때.</summary>
        public void SetStartPose(Vector3 position, Quaternion rotation)
        {
            startPosition = position;
            startRotation = rotation;
            ResetToStart();
        }

        /// <summary>코스 전진 방향(수평, 정규화). 방향을 정할 수 없으면 Vector3.zero.</summary>
        public Vector3 CourseForward
        {
            get
            {
                Vector3 dir = destination != null ? destination.position - startPosition : courseDirection;
                dir.y = 0f;
                return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
            }
        }

        /// <summary>코스 기준 오른쪽(수평, 정규화).</summary>
        public Vector3 CourseRight => Vector3.Cross(Vector3.up, CourseForward);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
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

        void Update()
        {
            if (!Simulated) return;

            Vector3 forward = CourseForward;
            if (forward != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            bool grounded = controller.isGrounded && verticalSpeed <= 0f;
            float move = ControlEnabled ? moveInput : 0f;

            if (grounded)
            {
                groundVelocity = forward * (move * moveSpeed);
                IsMoving = !Mathf.Approximately(move, 0f);

                if (ControlEnabled && laneJumpRequested != 0 && CanLaneJump(laneJumpRequested))
                    StartJump(JumpKind.Lane, laneJumpRequested);
                else if (ControlEnabled && jumpRequested)
                    StartJump(JumpKind.InPlace, 0);
                else
                    verticalSpeed = -1f; // 땅에 붙어 있도록 작은 하강 속도를 유지한다
            }
            else
            {
                IsMoving = false;
                verticalSpeed -= gravityStrength * Time.deltaTime;
            }

            Vector3 horizontal = CurrentJump != JumpKind.None ? airVelocity : groundVelocity;
            Vector3 step = LimitAlongCourse(horizontal * Time.deltaTime, forward);
            if (CurrentJump == JumpKind.Lane) step = LimitToLaneTarget(step);
            controller.Move(step + Vector3.up * (verticalSpeed * Time.deltaTime));

            // 입력은 한 프레임만 유효하다. 조작 규칙이 매 프레임 다시 넣는다.
            moveInput = 0f;
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
                if (other == this) continue;
                Vector3 delta = other.transform.position - origin;
                if (Mathf.Abs(delta.y) >= Mathf.Max(BodyHeight, other.BodyHeight)) continue;

                float reach = BodyRadius + other.BodyRadius;
                float side = Vector3.Dot(delta, right);
                if (Mathf.Abs(side) >= reach) continue; // 다른 줄

                float ahead = Vector3.Dot(delta, forward) * sign;
                if (ahead <= 0f) continue; // 가는 방향의 반대쪽

                float contact = Mathf.Sqrt(reach * reach - side * side) + playerGap;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, ahead - contact));
            }

            return step + forward * (sign * allowed - along);
        }

        /// <summary>옆줄 점프 중 옆 이동이 도착 줄을 넘어가지 않게 줄인다.</summary>
        Vector3 LimitToLaneTarget(Vector3 step)
        {
            Vector3 right = CourseRight;
            float side = Vector3.Dot(step, right);
            float remaining = laneTargetSide - Vector3.Dot(transform.position, right);
            float limited = Mathf.Sign(side) == Mathf.Sign(remaining) ? Mathf.Sign(remaining) * Mathf.Min(Mathf.Abs(side), Mathf.Abs(remaining)) : 0f;
            return step + right * (limited - side);
        }

        void StartJump(JumpKind kind, int laneDirection)
        {
            CurrentJump = kind;
            LaneJumpDirection = laneDirection;
            verticalSpeed = JumpSpeed;

            // 뛴 순간의 수평 움직임을 착지까지 고정한다
            airVelocity = groundVelocity;
            if (kind == JumpKind.Lane)
            {
                airVelocity += CourseRight * (laneDirection * laneSpacing / AirTime);
                laneTargetSide = Vector3.Dot(transform.position, CourseRight) + laneDirection * laneSpacing;
            }
        }

        void UpdateAirborne()
        {
            bool airborne = !controller.isGrounded;
            if (airborne == IsAirborne) return;

            if (airborne && CurrentJump == JumpKind.None)
            {
                // 점프 없이 떨어지는 중: 걷던 속도를 그대로 유지
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
            LaneJumpDirection = 0;
            airVelocity = Vector3.zero;
        }
    }
}
