using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>봉의 배치·조정값. 점유/시간/반동 판정은 PoleSystem의 호스트가 맡는다.</summary>
    public sealed partial class HumanCannonPole : MonoBehaviour
    {
        [SerializeField] Transform grip;
        [SerializeField] HumanCannonSawGate linkedGate;
        [Header("반동 — Grip은 중립점, 천장 앵커는 회전 중심")]
        [SerializeField] Transform visualBar, leftCable, rightCable, leftAnchor, rightAnchor;
        [SerializeField, Min(.1f)] float warningSeconds = 2f;
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
        public Transform VisualBar => visualBar;
        public HumanCannonSawGate LinkedGate => linkedGate;
        public bool InteractionAvailable { get; set; } = true;
        public bool IsSwitch { get; set; }
        static PoleSettings Rules => GameSettings.Poles;
        public float HoldSeconds => Rules.HoldSeconds;
        public float WarningSeconds => warningSeconds;
        public float AirControlSpeed => Rules.AirControlSpeed;
        public float FlightSpeedLimit(float sharedLimit) => Rules.HorizontalSpeedLimit;
        public float MaxReleaseUpwardSpeed => 0f;
        public float HangLength => Rules.HangLength;
        internal float MaxSwingAngle => Rules.MaxSwingAngle;
        public int RiderCapacity => Rules.Capacity;
        public float RiderSpacing => Rules.PassengerSeparation;
        public Vector3 Forward => Vector3.ProjectOnPlane(Grip.forward, Vector3.up).normalized;
        public float SwingLength => Rules.SwingLength;
        public Vector3 HangingPosition(float angle) => BarPosition(angle) - Vector3.up * HangLength;
        public Vector3 SeatOffset(int seat) => Vector3.down * (Mathf.Clamp(seat, 0, PoleGateState.MaxRiders - 1) * RiderSpacing);
        public Vector3 HangingPosition(float angle, int seat) => HangingPosition(angle) + SeatOffset(seat);
        public Vector3 GrabPoint(float angle, int seat) => seat <= 0 ? BarPosition(angle)
            : HangingPosition(angle, seat - 1) + Vector3.up * (HangLength * .5f);

        /// <summary>외형 케이블 길이와 무관한 공통 진자. 모든 봉의 이동 범위와 관성이 같다.</summary>
        public Vector3 BarPosition(float angle)
        {
            return Grip.position + PoleSwingMotion.Offset(angle, SwingLength, Forward) + Vector3.up * SwingLength;
        }

        public Vector3 ReleaseVelocity(float angle, float speed) => ReleaseVelocity(angle, speed, -1);
        public Vector3 ReleaseVelocity(float angle, float speed, int seat) => ReleaseVelocity(angle, speed, seat, 0f);
        public Vector3 ReleaseVelocity(float angle, float speed, int seat, float direction)
        {
            // The user-approved release assist converts the current pendulum energy to a
            // forward launch even at a turning point. No separate charge survives damping.
            if (!float.IsFinite(direction) || Mathf.Abs(direction) < .01f)
                direction = Mathf.Abs(speed) > .01f ? Mathf.Sign(speed) : Mathf.Sign(angle);
            float momentum = PoleSwingMotion.StoredSpeed(angle, speed, SwingLength,
                GameSettings.Base.Character.Gravity, Rules.SwingMotionSpeed, Rules.MaxSwingAngle, Rules.MaxSwingSpeed);
            Vector3 velocity = Forward * (Mathf.Sign(direction) * momentum) + Vector3.up * Rules.ReleaseUpwardSpeed;
            return CannonAirControl.LimitPoleHorizontalSpeed(velocity, Rules.HorizontalSpeedLimit);
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
            EnsureCommonVisual();
            PresentCommonRoot();
            if (visualBar == null) return;
            Vector3 position = BarPosition(angle);
            Quaternion rotation = Grip.rotation;
            if (commonVisual != null)
            {
                position += commonVisual.TransformPoint(authoredBarPosition) - Grip.position;
                rotation = commonVisual.rotation * authoredBarRotation;
            }
            if ((visualBar.position - position).sqrMagnitude > .0000000001f
                || Quaternion.Angle(visualBar.rotation, rotation) > .00001f)
                visualBar.SetPositionAndRotation(position, rotation);
            if (commonVisual != null)
            {
                PresentCommonCable(commonLeftCable, position);
                PresentCommonCable(commonRightCable, position);
            }
            else
            {
                PresentCable(leftCable, leftAnchor, position);
                PresentCable(rightCable, rightAnchor, position);
            }
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
            Vector3 position = (anchor.position + end) * 0.5f;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, localSpan);
            if ((cable.position - position).sqrMagnitude > .0000000001f) cable.position = position;
            if (Quaternion.Angle(cable.localRotation, rotation) > .00001f) cable.localRotation = rotation;
            Vector3 scale = cable.localScale;
            scale.y = localSpan.magnitude; // 케이블은 높이 1의 Cube, 굵기는 기존 값을 유지한다.
            if ((cable.localScale - scale).sqrMagnitude > .0000000001f) cable.localScale = scale;
        }

        // The same swing clock drives presentation and stored release energy.
        public void Step(ref float angle, ref float speed, float input, float dt, float gravity) =>
            PoleSwingMotion.Step(ref angle, ref speed, input, dt * Rules.SwingMotionSpeed, gravity, SwingLength,
                Rules.SwingAcceleration, Rules.MaxSwingAngle, Rules.MaxSwingSpeed, Rules.SwingDamping);

        public float SwingDrive(ref PoleSwingInput impulse, float input, float dt)
            => float.IsFinite(input) && float.IsFinite(dt) && dt > 0 ? Mathf.Clamp(input, -1f, 1f) : 0;

        public bool IsInReach(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            if (Loop != null && !Loop.IsSynchronized) return false;
            RefreshMovingPose();
            var target = BarPosition(0);
            return PlayerInteraction.InRange(player, target, GrabRadius(player, 0f), transform);
        }

        internal bool IsSeatInReach(NetworkPlayer player, float angle, int seat, double observedElapsed = -1d)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || seat < 0 || seat >= RiderCapacity) return false;
            var route = Loop;
            if (route != null && !route.IsSynchronized) return false;
            RefreshMovingPose();
            Vector3 target = GrabPoint(angle, seat);
            if (observedElapsed >= 0d)
            {
                if (route == null || !double.IsFinite(observedElapsed) || observedElapsed > route.Elapsed + .05d
                    || route.Elapsed - observedElapsed > .35d) return false;
                target = route.PointAt(target, System.Math.Min(observedElapsed, route.Elapsed));
            }
            return PlayerInteraction.InRange(player, target, GrabRadius(player, 0f), transform);
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
            return PlayerInteraction.InRange(player, target, GrabRadius(player, 0f), transform);
        }

        public float GrabRadius(NetworkPlayer player, float minimumReach)
        {
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            return Rules.GrabRadius * Mathf.Clamp01(reach);
        }
    }
}
