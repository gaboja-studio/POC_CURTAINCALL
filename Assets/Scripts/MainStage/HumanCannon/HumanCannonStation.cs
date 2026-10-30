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
        [Tooltip("비행 중 시선 기준 WASD 수평 보정 속도(m/s). 키를 놓으면 보정은 사라진다.")]
        [Min(0f), SerializeField] float airControlSpeed = 3f;

        // Retained for existing scene serialization; all stations now use the same cooldown.
        [HideInInspector, SerializeField] bool animateBarrel;
        public const float BarrelLowerSeconds = 1.2f;
        public const float BarrelRecoverySeconds = 2.8f;
        readonly CannonBarrelCycle barrel = new();
        bool interactionAvailable = true;

        public Transform BoardingPoint => boardingPoint != null ? boardingPoint : transform;
        public Transform Seat => seat != null ? seat : transform;
        public Transform Muzzle => muzzle != null ? muzzle : Seat;
        public float BoardingDistance => boardingDistance;
        public float AirControlSpeed => airControlSpeed;
        // Scene machinery may interlock boarding/launch without disabling an in-flight station or Cancel.
        public bool InteractionAvailable
        {
            get => interactionAvailable;
            set { interactionAvailable = value; barrel.SetAvailable(value && isActiveAndEnabled, MotionTime); }
        }
        public bool BarrelMotionEnabled => GetComponentInChildren<CannonBarrelPresentation>(true) != null;
        public bool ReadyToUse => isActiveAndEnabled && InteractionAvailable && BarrelPhase == CannonBarrelPhase.Active;
        public double LastFiredAt => barrel.FiredAt;
        public double LastActivatedAt => barrel.UnlockedAt;
        public CannonBarrelPhase BarrelPhase => SampleBarrel(out _);
        public static double MotionTime => Unity.Netcode.NetworkManager.Singleton != null
            && Unity.Netcode.NetworkManager.Singleton.IsListening
            ? Unity.Netcode.NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
        public CannonBarrelPhase SampleBarrel(out float progress)
        {
            double now = MotionTime;
            // A scene may begin with this component disabled, without ever receiving OnDisable.
            barrel.SetAvailable(InteractionAvailable && isActiveAndEnabled, now);
            float rest = Mathf.Max(0, CurtainCall.Settings.GameSettings.HumanCannon.CannonCooldown - BarrelLowerSeconds - BarrelRecoverySeconds);
            return barrel.Sample(now, BarrelLowerSeconds, rest, BarrelRecoverySeconds, out progress);
        }
        internal void FireBarrel() => barrel.Fire(MotionTime);
        internal void RestoreBarrel(double firedAt, double activatedAt) => barrel.Restore(firedAt, activatedAt);
        internal void ResetBarrel() => barrel.Reset();
        void OnEnable() => barrel.SetAvailable(InteractionAvailable, MotionTime);
        void OnDisable() => barrel.SetAvailable(false, MotionTime);
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
