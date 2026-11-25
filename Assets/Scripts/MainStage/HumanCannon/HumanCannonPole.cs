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
        [Header("목마 전용 높은 봉 — 바닥에서 잡는 판정 (공중 비행 잡기는 기존 반경 유지)")]
        [SerializeField, InspectorName("목마 전용 높은 봉"), Tooltip("켜면 바닥·발판·목마 위에서 이 봉을 잡을 때 아래 판정 상자를 쓰고, GameSettings 봉 설정의 '목마 탑승자만 바닥에서 잡기'가 켜져 있으면 목마 맨 위 사람(또는 목마에서 막 도약한 사람)만 잡을 수 있다. 대포·봉 비행 중 공중에서 잡기와 매달린 동료 잡기는 기존 반경 그대로다. 씬 뷰에서 노란 상자로 보인다.")]
        bool piggybackOnly;
        [SerializeField, InspectorName("개별 판정 상자 사용"), Tooltip("끄면 GameSettings › 봉 설정의 '기본 바닥 잡기 영역'을 쓴다. 켜면 아래 상자를 이 봉에만 쓴다. 씬 뷰 상자 핸들로 크기를 조절할 수 있다.")]
        bool customGrabZone;
        [SerializeField, InspectorName("개별 판정 상자")]
        PoleGrabZone grabZone = PoleGrabZone.Default;
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
        /// <summary>바닥에서 잡을 때 판정 상자와 목마 조건을 쓰는 높은 봉. 런타임 생성 봉(외줄 공중그네 첫 봉)은 코드가 켠다.</summary>
        public bool PiggybackOnly { get => piggybackOnly; set => piggybackOnly = value; }
        public bool UsesCustomGrabZone => customGrabZone;
        public PoleGrabZone GroundGrabZone => customGrabZone ? grabZone : Rules.PiggybackGrabZone;
        /// <summary>봉의 수평 방향(판정 상자 회전). 부모 기울기·크기와 무관하다.</summary>
        public Quaternion ZoneRotation => Forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(Forward, Vector3.up) : Quaternion.identity;
        /// <summary>바닥 잡기 판정: 몸 중심이 흔들리는 봉을 따라가는 상자 안에 있는가.</summary>
        public bool GroundZoneContains(Vector3 center, float angle, float reach = 1f) => GroundGrabZone.Contains(BarPosition(angle), ZoneRotation, center, reach);
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

        internal float AddCatchMomentum(float angle, float speed, float approachSpeed) =>
            PoleSwingMotion.AddCatchMomentum(angle, speed, approachSpeed, SwingLength, GameSettings.Base.Character.Gravity,
                Rules.SwingMotionSpeed, Rules.MaxSwingAngle, Rules.MaxSwingSpeed);

        public float SwingDrive(ref PoleSwingInput impulse, float input, float dt)
            => float.IsFinite(input) && float.IsFinite(dt) && dt > 0 ? Mathf.Clamp(input, -1f, 1f) : 0;

        public bool IsInReach(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            if (Loop != null && !Loop.IsSynchronized) return false;
            RefreshMovingPose();
            var target = BarPosition(0);
            return PlayerInteraction.InRange(player, target, GrabRadius(player), transform, ignoreRope: true);
        }

        /// <summary>
        /// 자리 잡기 거리. 목마 전용 높은 봉의 맨 위 자리를 대포·봉 비행이 아닌 상태(바닥·발판·외줄·목마 위·일반 점프)에서
        /// 잡을 때는 판정 상자를 쓰고, 그 밖은 기존 구 판정이다.
        /// </summary>
        internal bool IsSeatInReach(NetworkPlayer player, float angle, int seat, double observedElapsed = -1d)
        {
            bool fromGround = player != null && (HumanCannonSystem.Current == null || !HumanCannonSystem.Current.IsFlying(player.OwnerClientId));
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || seat < 0 || seat >= RiderCapacity) return false;
            var route = Loop;
            if (route != null && !route.IsSynchronized) return false;
            RefreshMovingPose();
            Vector3 target = GrabPoint(angle, seat);
            Vector3 zoneOffset = Vector3.zero;
            if (observedElapsed >= 0d)
            {
                if (route == null || !double.IsFinite(observedElapsed) || observedElapsed > route.Elapsed + .05d
                    || route.Elapsed - observedElapsed > .35d) return false;
                Vector3 moved = route.PointAt(target, System.Math.Min(observedElapsed, route.Elapsed));
                zoneOffset = moved - target;
                target = moved;
            }
            Vector3 center = PlayerInteraction.LatestCenter(player);
            if (seat == 0 && fromGround && piggybackOnly)
                return GroundZoneContains(center - zoneOffset, angle, ReachOf(player))
                    && PlayerInteraction.ClearPathFrom(center, target, transform, player, ignoreRope: true);
            return PlayerInteraction.InRangeFrom(player, center, target,
                GrabRadius(player, passenger: seat > 0), transform, ignoreRope: true);
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
            return PlayerInteraction.InRange(player, target, GrabRadius(player), transform, ignoreRope: true);
        }

#if UNITY_EDITOR
        // 선택하지 않아도 목마 전용 봉의 판정 상자를 보이게 한다. 선택하면 HumanCannonPoleEditor가 핸들을 그린다.
        void OnDrawGizmos()
        {
            if (!piggybackOnly) return;
            var zone = GroundGrabZone;
            var previous = Gizmos.matrix;
            Gizmos.color = new Color(1f, .82f, .2f, .55f);
            Gizmos.matrix = Matrix4x4.TRS(BarPosition(0f), ZoneRotation, Vector3.one);
            Gizmos.DrawWireCube(zone.center, zone.SafeSize);
            Gizmos.matrix = previous;
        }
#endif

        static float ReachOf(NetworkPlayer player) => player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
        static float GrabRadius(NetworkPlayer player, bool passenger = false)
            => (passenger ? Rules.PassengerGrabRadius : Rules.GrabRadius) * Mathf.Clamp01(ReachOf(player));
    }
}
