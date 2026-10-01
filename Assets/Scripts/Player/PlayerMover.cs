using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 이동 명령을 받아 캐릭터를 움직인다.
    /// 전진 방향은 카메라가 아니라 월드에 정해진 코스 방향(목적지 쪽)이다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerMover : MonoBehaviour
    {
        [Tooltip("목적지. 지정하면 시작 위치에서 이 지점을 향하는 방향이 전진이 된다.")]
        [SerializeField] Transform destination;

        [Tooltip("목적지가 없을 때 쓰는 월드 전진 방향. 높이(Y)는 무시한다.")]
        [SerializeField] Vector3 courseDirection = Vector3.forward;

        [Tooltip("초당 이동 거리(m).")]
        [SerializeField, Min(0f)] float moveSpeed = 3f;

        [Tooltip("중력 가속도(m/s²). 음수.")]
        [SerializeField] float gravity = -20f;

        CharacterController controller;
        PlayerInputReader input;
        Vector3 startPosition;
        float verticalSpeed;

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
            input = GetComponent<PlayerInputReader>();
            startPosition = transform.position;
        }

        void Update()
        {
            Vector3 forward = CourseForward;
            if (forward != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            // 땅에 붙어 있도록 작은 하강 속도를 유지한다
            verticalSpeed = controller.isGrounded ? -1f : verticalSpeed + gravity * Time.deltaTime;

            Vector3 velocity = forward * (input.Current.Move * moveSpeed) + Vector3.up * verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
