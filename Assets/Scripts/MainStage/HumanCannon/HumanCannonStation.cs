using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>대포의 배치·임시 조정값. 동작 요청/조회는 HumanCannonSystem을 사용한다.</summary>
    public sealed class HumanCannonStation : MonoBehaviour
    {
        [Tooltip("탑승 거리의 기준점. 비우면 대포 원점.")]
        [SerializeField] Transform boardingPoint;
        [Tooltip("탑승한 캐릭터의 발 위치·방향. 비우면 대포 원점.")]
        [SerializeField] Transform seat;
        [Tooltip("발사 시작 위치. forward가 수평 발사 방향이다.")]
        [SerializeField] Transform muzzle;
        [Tooltip("임시 탑승 거리(m). 상호작용 거리 배율을 곱한다.")]
        [Min(0.1f), SerializeField] float boardingDistance = 2f;
        [Tooltip("임시 수평 발사 속도(m/s). 최종 밸런스 값이 아니다.")]
        [Min(0f), SerializeField] float forwardSpeed = 8f;
        [Tooltip("임시 위쪽 발사 속도(m/s). 최종 밸런스 값이 아니다.")]
        [Min(0.1f), SerializeField] float upwardSpeed = 12f;
        [Tooltip("비행 중 WASD 수평 보정 속도(m/s). 키를 놓으면 보정은 사라진다. 임시값 3.")]
        [Min(0f), SerializeField] float airControlSpeed = 3f;

        public Transform BoardingPoint => boardingPoint != null ? boardingPoint : transform;
        public Transform Seat => seat != null ? seat : transform;
        public Transform Muzzle => muzzle != null ? muzzle : Seat;
        public float BoardingDistance => boardingDistance;
        public float AirControlSpeed => airControlSpeed;
        public Vector3 LaunchVelocity
        {
            get
            {
                var forward = Vector3.ProjectOnPlane(Muzzle.forward, Vector3.up).normalized;
                return forward * forwardSpeed + Vector3.up * upwardSpeed;
            }
        }
    }
}
