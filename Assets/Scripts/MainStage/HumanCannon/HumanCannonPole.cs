using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>봉의 배치·조정값. 점유/시간/반동 판정은 PoleSystem의 호스트가 맡는다.</summary>
    public sealed class HumanCannonPole : MonoBehaviour
    {
        [SerializeField] Transform grip;
        [SerializeField] HumanCannonSawGate linkedGate;
        [Tooltip("봉에서 발까지 거리(m)."), Min(0.2f), SerializeField] float hangLength = 1.5f;
        [Tooltip("봉 아래 발 기준 잡기 거리(m)."), Min(0.1f), SerializeField] float grabDistance = 2f;
        [Tooltip("잡은 뒤 최대 유지 시간(초)."), Min(0.1f), SerializeField] float holdSeconds = 8f;
        [Tooltip("놓기/시간 끝/사망 뒤 톱 재동작 예고(초)."), Min(0.1f), SerializeField] float warningSeconds = 2f;
        [Tooltip("W/S 반동 가속(도/초²)."), Min(0f), SerializeField] float swingAcceleration = 120f;
        [Tooltip("최대 반동 각도(도)."), Range(1f, 85f), SerializeField] float maxSwingAngle = 65f;
        [Tooltip("최대 각속도(도/초)."), Min(1f), SerializeField] float maxSwingSpeed = 150f;
        [Tooltip("반동 감쇠(1/초)."), Min(0f), SerializeField] float swingDamping = 0.4f;
        [Tooltip("손을 놓을 때 접선 속도에 더하는 상승 속도(m/s)."), Min(0f), SerializeField] float releaseUpwardSpeed = 6f;
        [Tooltip("봉 도약 뒤 WASD 수평 조절(m/s)."), Min(0f), SerializeField] float airControlSpeed = 3f;

        public Transform Grip => grip != null ? grip : transform;
        public HumanCannonSawGate LinkedGate => linkedGate;
        public float HoldSeconds => holdSeconds;
        public float WarningSeconds => warningSeconds;
        public float AirControlSpeed => airControlSpeed;
        public Vector3 Forward => Vector3.ProjectOnPlane(Grip.forward, Vector3.up).normalized;
        public Vector3 HangingPosition(float angle) => Grip.position + PoleSwingMotion.Offset(angle, hangLength, Forward);
        public Vector3 ReleaseVelocity(float angle, float speed) =>
            PoleSwingMotion.Velocity(angle, speed, hangLength, Forward) + Vector3.up * releaseUpwardSpeed;

        internal void Step(ref float angle, ref float speed, float input, float dt, float gravity) =>
            PoleSwingMotion.Step(ref angle, ref speed, input, dt, gravity, hangLength,
                swingAcceleration, maxSwingAngle, maxSwingSpeed, swingDamping);

        public bool IsInReach(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            float radius = grabDistance * reach;
            return radius > 0f && (player.transform.position - HangingPosition(0)).sqrMagnitude <= radius * radius;
        }
    }
}
