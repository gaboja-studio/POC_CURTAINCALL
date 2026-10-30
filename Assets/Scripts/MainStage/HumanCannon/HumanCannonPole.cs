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
        [Tooltip("함께 매달릴 수 있는 인원. 기존 톱 관문 봉은 1명을 유지한다."), Range(1, 4), SerializeField] int riderCapacity = 1;
        [Tooltip("봉 위 고정 손잡이 자리 간격(m)."), Min(0.65f), SerializeField] float riderSpacing = 0.8f;
        [Header("반동 — Grip은 중립점, 천장 앵커는 회전 중심")]
        [SerializeField] Transform visualBar, leftCable, rightCable, leftAnchor, rightAnchor;
        [Tooltip("봉에서 발까지 거리(m)."), Min(0.2f), SerializeField] float hangLength = 1.5f;
        [Tooltip("봉 중심의 구형 잡기 거리(m)."), Min(0.1f), SerializeField] float grabDistance = 2f;
        [Tooltip("벽/전망 프레임 너머로 잡아 몸이 통과하는 것을 막는다. 기존 씬 기본값은 꺼짐."), SerializeField] bool requireClearGrabPath;
        readonly RaycastHit[] grabObstacles = new RaycastHit[32];
        readonly Collider[] grabOverlaps = new Collider[32];
        [Tooltip("잡은 뒤 최대 유지 시간(초). 0이면 스스로 놓거나 사망할 때까지 유지한다."), Min(0f), SerializeField] float holdSeconds;
        [Tooltip("놓기/제한 시간 만료/사망 뒤 지상 관문 톱 재동작 예고(초)."), Min(0.1f), SerializeField] float warningSeconds = 2f;
        [Tooltip("W/S 반동 가속(도/초²)."), Min(0f), SerializeField] float swingAcceleration = 300f;
        [Tooltip("최대 반동 각도(도)."), Range(1f, 85f), SerializeField] float maxSwingAngle = 80f;
        [Tooltip("최대 각속도(도/초)."), Min(1f), SerializeField] float maxSwingSpeed = 180f;
        [Tooltip("반동 감쇠(1/초)."), Min(0f), SerializeField] float swingDamping = 0.4f;
        [Tooltip("손을 놓을 때 접선 속도에 더하는 상승 속도(m/s)."), Min(0f), SerializeField] float releaseUpwardSpeed = 6f;
        [Tooltip("이 봉의 도약 상승 속도 상한(m/s). 0이면 원본 반동을 그대로 사용한다. 긴 케이블의 레벨별 높이 조정용."), Min(0f), SerializeField] float maxReleaseUpwardSpeed;
        [Tooltip("봉 도약 뒤 WASD 수평 조절(m/s)."), Min(0f), SerializeField] float airControlSpeed = 3f;
        HumanCannonPoleLoop loop;
        bool loopSearched;

        public HumanCannonPoleLoop Loop
        {
            get
            {
                if (!loopSearched) { loop = GetComponentInParent<HumanCannonPoleLoop>(); loopSearched = true; }
                return loop != null && loop.Contains(transform) ? loop : null;
            }
        }

        void OnTransformParentChanged() { loop = null; loopSearched = false; }
        public void RefreshMovingPose() => Loop?.RefreshPose();

        public Transform Grip => grip != null ? grip : transform;
        public HumanCannonSawGate LinkedGate => linkedGate;
        public float HoldSeconds => holdSeconds;
        public float WarningSeconds => warningSeconds;
        public float AirControlSpeed => airControlSpeed;
        public float MaxReleaseUpwardSpeed => maxReleaseUpwardSpeed;
        public float HangLength => hangLength;
        public int RiderCapacity => linkedGate != null ? 1 : Mathf.Clamp(riderCapacity, 1, PoleGateState.MaxRiders);
        public float RiderSpacing => Mathf.Max(0.65f, riderSpacing);
        public Vector3 Forward => Vector3.ProjectOnPlane(Grip.forward, Vector3.up).normalized;
        public float SwingLength => TryCeilingPivot(out var pivot) ? Vector3.Distance(pivot, Grip.position) : hangLength;
        public Vector3 HangingPosition(float angle) => BarPosition(angle) - Vector3.up * hangLength;
        public Vector3 SeatOffset(int seat) => Grip.right * ((Mathf.Clamp(seat, 0, RiderCapacity - 1) - (RiderCapacity - 1) * .5f) * RiderSpacing);
        public Vector3 HangingPosition(float angle, int seat) => HangingPosition(angle) + SeatOffset(seat);

        /// <summary>실제 줄 길이로 천장 중간점 주위를 돈다. 앵커가 없으면 기존 짧은 진자 형상을 유지한다.</summary>
        public Vector3 BarPosition(float angle)
        {
            if (TryCeilingPivot(out var pivot))
                return pivot + Quaternion.AngleAxis(angle, Vector3.Cross(Forward, Vector3.up)) * (Grip.position - pivot);
            return Grip.position + PoleSwingMotion.Offset(angle, hangLength, Forward) + Vector3.up * hangLength;
        }

        public Vector3 ReleaseVelocity(float angle, float speed) => ReleaseVelocity(angle, speed, -1);
        public Vector3 ReleaseVelocity(float angle, float speed, int seat)
        {
            Vector3 tangent = TryCeilingPivot(out var pivot)
                ? Vector3.Cross(Vector3.Cross(Forward, Vector3.up), BarPosition(angle) - pivot) * (speed * Mathf.Deg2Rad)
                : PoleSwingMotion.Velocity(angle, speed, hangLength, Forward);
            Vector3 velocity = tangent + Vector3.up * releaseUpwardSpeed;
            // 레일 병진·회전 관성을 진자 접선에 더한 뒤 기존 상승/수평 제한을 적용한다.
            if (Loop != null) velocity += Loop.PointVelocity(seat < 0 ? HangingPosition(angle) : HangingPosition(angle, seat));
            // 수평 반동과 하강은 보존한다. 원본 씬은 기본값 0으로 기존 도약을 유지한다.
            if (maxReleaseUpwardSpeed > 0f) velocity.y = Mathf.Min(velocity.y, maxReleaseUpwardSpeed);
            return velocity;
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

        public void Step(ref float angle, ref float speed, float input, float dt, float gravity) =>
            PoleSwingMotion.Step(ref angle, ref speed, input, dt, gravity, SwingLength,
                swingAcceleration, maxSwingAngle, maxSwingSpeed, swingDamping);

        public bool IsInReach(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            if (Loop != null && !Loop.IsSynchronized) return false;
            RefreshMovingPose();
            var target = BarPosition(0);
            return PlayerInteraction.InRange(player, target, GrabRadius(player, 0f)) && HasClearGrabPath(player, target);
        }

        internal bool IsSeatInReach(NetworkPlayer player, float angle, int seat, double observedElapsed = -1d)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || seat < 0 || seat >= RiderCapacity) return false;
            var route = Loop;
            if (route != null && !route.IsSynchronized) return false;
            RefreshMovingPose();
            Vector3 target = BarPosition(angle) + SeatOffset(seat);
            if (observedElapsed >= 0d)
            {
                if (route == null || !double.IsFinite(observedElapsed) || observedElapsed > route.Elapsed + .05d
                    || route.Elapsed - observedElapsed > .35d) return false;
                target = route.PointAt(target, System.Math.Min(observedElapsed, route.Elapsed));
            }
            return PlayerInteraction.InRange(player, target, GrabRadius(player, 0f)) && HasClearGrabPath(player, target);
        }

        /// <summary>빠르게 지나간 순환 봉만 짧은 수신 지연을 허용한다. 거리·부상·점유 검사는 원본을 유지한다.</summary>
        internal bool IsInReachAt(NetworkPlayer player, double observedElapsed)
        {
            var route = Loop;
            if (route == null || !route.IsSynchronized || player == null || !player.IsSpawned
                || player.State != PlayerState.Normal || !double.IsFinite(observedElapsed) || observedElapsed < 0d) return false;
            double now = route.Elapsed;
            if (observedElapsed > now + .05d || now - observedElapsed > .35d) return false;
            route.RefreshPose();
            Vector3 target = route.PointAt(BarPosition(0), System.Math.Min(observedElapsed, now));
            return PlayerInteraction.InRange(player, target, GrabRadius(player, 0f)) && HasClearGrabPath(player, target);
        }

        bool HasClearGrabPath(NetworkPlayer player, Vector3 target)
        {
            if (!requireClearGrabPath) return true;
            Vector3 origin = PlayerInteraction.Center(player), span = target - origin;
            float distance = span.magnitude;
            if (distance < .001f) return true;
            float radius = player.TryGetComponent(out PlayerMover mover) ? mover.BodyRadius : .3f;
            int overlaps = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(.01f, radius - .01f), grabOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlaps == grabOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++)
                if (BlocksGrab(grabOverlaps[i], player)
                    && Vector3.Dot(grabOverlaps[i].bounds.ClosestPoint(origin) - origin, span) >= -.001f) return false;
            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(.01f, radius), span / distance,
                grabObstacles, Mathf.Max(0, distance - .03f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == grabObstacles.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = grabObstacles[i].collider;
                if (BlocksGrab(hit, player)) return false;
            }
            return true;
        }

        bool BlocksGrab(Collider hit, NetworkPlayer player) => hit != null && !hit.transform.IsChildOf(transform)
            && !hit.transform.IsChildOf(Grip) && (visualBar == null || !hit.transform.IsChildOf(visualBar))
            && hit.GetComponentInParent<NetworkPlayer>() == null
            && !Physics.GetIgnoreLayerCollision(player.gameObject.layer, hit.gameObject.layer);

        public float GrabRadius(NetworkPlayer player, float minimumReach)
        {
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            // 씬별 기본 거리와 부상 배율에 25% 접근 여유를 적용한다. 표시와 호스트도 이 판정을 공유한다.
            return grabDistance * (reach > 0f ? Mathf.Max(minimumReach, reach) : reach) * 1.25f;
        }
    }
}
