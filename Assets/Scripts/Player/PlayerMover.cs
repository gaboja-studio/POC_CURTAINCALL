using System;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 동작 부품: 코스 이동과 제자리 점프. 키도 콘텐츠도 모르고, 조작 규칙이 함수를 불러 움직인다.
    /// 전진 방향은 카메라가 아니라 월드에 정해진 코스 방향(목적지 쪽)이다.
    /// 목마 중 점프 규칙은 목마 기능이 정한다.
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

        CharacterController controller;
        float moveInput;
        bool jumpRequested;
        Vector3 startPosition;
        Quaternion startRotation;
        float verticalSpeed;

        /// <summary>땅에서 떨어질 때(true)·착지할 때(false) 보낸다. 점프·낙하 모두 포함.</summary>
        public event Action<bool> AirborneChanged;

        /// <summary>공중에 있는지.</summary>
        public bool IsAirborne { get; private set; }

        /// <summary>이번 프레임 코스 이동 입력이 있었는지(조작 잠금 반영). 균형의 이동 중 흔들림 판정에 쓴다.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>이번 프레임 코스 이동 입력. +1 전진, -1 후진. 매 프레임 넣어야 하며 넣지 않으면 정지.</summary>
        public void SetMoveInput(float forward) => moveInput = Mathf.Clamp(forward, -1f, 1f);

        /// <summary>제자리 점프를 요청한다. 땅에 있고 조작이 켜져 있을 때만 이번 프레임에 뛴다.</summary>
        public void RequestJump() => jumpRequested = true;

        /// <summary>점프 초속(m/s).</summary>
        public float JumpSpeed => Mathf.Sqrt(2f * gravityStrength * jumpHeight);

        /// <summary>이동 명령을 받는지. 꺼져 있으면 W/S를 무시한다(중력은 계속 적용).</summary>
        public bool ControlEnabled { get; private set; } = true;

        /// <summary>조작을 켜거나 끈다. 예: 추락한 플레이어는 재시작까지 끈다.</summary>
        public void SetControlEnabled(bool enabled) => ControlEnabled = enabled;

        /// <summary>출발 위치·방향으로 되돌린다(재시작).</summary>
        public void ResetToStart()
        {
            controller.enabled = false; // CharacterController는 꺼야 위치를 바로 옮길 수 있다
            transform.SetPositionAndRotation(startPosition, startRotation);
            controller.enabled = true;
            verticalSpeed = 0f;
            SetAirborne(false);
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

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        void Update()
        {
            Vector3 forward = CourseForward;
            if (forward != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            bool grounded = controller.isGrounded;
            if (grounded && ControlEnabled && jumpRequested)
                verticalSpeed = JumpSpeed;
            else if (grounded && verticalSpeed <= 0f)
                verticalSpeed = -1f; // 땅에 붙어 있도록 작은 하강 속도를 유지한다
            else
                verticalSpeed -= gravityStrength * Time.deltaTime;

            float move = ControlEnabled ? moveInput : 0f;
            IsMoving = !Mathf.Approximately(move, 0f);
            Vector3 velocity = forward * (move * moveSpeed) + Vector3.up * verticalSpeed;
            controller.Move(velocity * Time.deltaTime);

            // 입력은 한 프레임만 유효하다. 조작 규칙이 매 프레임 다시 넣는다.
            moveInput = 0f;
            jumpRequested = false;

            SetAirborne(!controller.isGrounded);
        }

        void SetAirborne(bool airborne)
        {
            if (IsAirborne == airborne) return;
            IsAirborne = airborne;
            AirborneChanged?.Invoke(airborne);
        }
    }
}
