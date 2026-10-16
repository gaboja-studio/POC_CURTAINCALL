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
        [Header("반동 — Grip은 중립점, 천장 앵커는 회전 중심")]
        [SerializeField] Transform visualBar, leftCable, rightCable, leftAnchor, rightAnchor;
        [Tooltip("봉에서 발까지 거리(m)."), Min(0.2f), SerializeField] float hangLength = 1.5f;
        [Tooltip("봉 아래 발 기준 잡기 거리(m)."), Min(0.1f), SerializeField] float grabDistance = 2f;
        [Tooltip("잡은 뒤 최대 유지 시간(초). 0이면 스스로 놓거나 사망할 때까지 유지한다."), Min(0f), SerializeField] float holdSeconds;
        [Tooltip("놓기/제한 시간 만료/사망 뒤 지상 관문 톱 재동작 예고(초)."), Min(0.1f), SerializeField] float warningSeconds = 2f;
        [Tooltip("W/S 반동 가속(도/초²)."), Min(0f), SerializeField] float swingAcceleration = 300f;
        [Tooltip("최대 반동 각도(도)."), Range(1f, 85f), SerializeField] float maxSwingAngle = 80f;
        [Tooltip("최대 각속도(도/초)."), Min(1f), SerializeField] float maxSwingSpeed = 180f;
        [Tooltip("반동 감쇠(1/초)."), Min(0f), SerializeField] float swingDamping = 0.4f;
        [Tooltip("손을 놓을 때 접선 속도에 더하는 상승 속도(m/s)."), Min(0f), SerializeField] float releaseUpwardSpeed = 6f;
        [Tooltip("봉 도약 뒤 WASD 수평 조절(m/s)."), Min(0f), SerializeField] float airControlSpeed = 3f;

        public Transform Grip => grip != null ? grip : transform;
        public HumanCannonSawGate LinkedGate => linkedGate;
        public float HoldSeconds => holdSeconds;
        public float WarningSeconds => warningSeconds;
        public float AirControlSpeed => airControlSpeed;
        public Vector3 Forward => Vector3.ProjectOnPlane(Grip.forward, Vector3.up).normalized;
        public float SwingLength => TryCeilingPivot(out var pivot) ? Vector3.Distance(pivot, Grip.position) : hangLength;
        public Vector3 HangingPosition(float angle) => BarPosition(angle) - Vector3.up * hangLength;

        /// <summary>실제 줄 길이로 천장 중간점 주위를 돈다. 앵커가 없으면 기존 짧은 진자 형상을 유지한다.</summary>
        public Vector3 BarPosition(float angle)
        {
            if (TryCeilingPivot(out var pivot))
                return pivot + Quaternion.AngleAxis(angle, Vector3.Cross(Forward, Vector3.up)) * (Grip.position - pivot);
            return Grip.position + PoleSwingMotion.Offset(angle, hangLength, Forward) + Vector3.up * hangLength;
        }

        public Vector3 ReleaseVelocity(float angle, float speed)
        {
            Vector3 tangent = TryCeilingPivot(out var pivot)
                ? Vector3.Cross(Vector3.Cross(Forward, Vector3.up), BarPosition(angle) - pivot) * (speed * Mathf.Deg2Rad)
                : PoleSwingMotion.Velocity(angle, speed, hangLength, Forward);
            return tangent + Vector3.up * releaseUpwardSpeed;
        }

        bool TryCeilingPivot(out Vector3 pivot)
        {
            pivot = default;
            if (leftAnchor == null || rightAnchor == null) return false;
            pivot = (leftAnchor.position + rightAnchor.position) * 0.5f;
            return float.IsFinite(pivot.x) && float.IsFinite(pivot.y) && float.IsFinite(pivot.z)
                && pivot.y > Grip.position.y + 0.001f;
        }

        /// <summary>공유 반동 각도에 봉·줄을 맞춘다. 천장과 중립 Grip은 움직이지 않는다.</summary>
        public void PresentSwing(float angle)
        {
            if (visualBar == null) return;
            Vector3 position = BarPosition(angle);
            visualBar.SetPositionAndRotation(position, Grip.rotation);
            PresentCable(leftCable, leftAnchor, position);
            PresentCable(rightCable, rightAnchor, position);
        }

        void PresentCable(Transform cable, Transform anchor, Vector3 barPosition)
        {
            if (cable == null || anchor == null) return;
            Vector3 attachment = TryCeilingPivot(out var pivot) ? anchor.position - pivot
                : Vector3.ProjectOnPlane(anchor.position - Grip.position, Vector3.up);
            Vector3 end = barPosition + attachment;
            Vector3 span = anchor.position - end;
            Vector3 localSpan = cable.parent != null ? cable.parent.InverseTransformVector(span) : span;
            if (localSpan.sqrMagnitude <= 0.000001f) return;
            cable.position = (anchor.position + end) * 0.5f;
            cable.localRotation = Quaternion.FromToRotation(Vector3.up, localSpan);
            Vector3 scale = cable.localScale;
            scale.y = localSpan.magnitude; // 케이블은 높이 1의 Cube, 굵기는 기존 값을 유지한다.
            cable.localScale = scale;
        }

        internal void Step(ref float angle, ref float speed, float input, float dt, float gravity) =>
            PoleSwingMotion.Step(ref angle, ref speed, input, dt, gravity, SwingLength,
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
