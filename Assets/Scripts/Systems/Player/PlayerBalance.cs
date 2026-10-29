using System;
using CurtainCall.Settings;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CurtainCall.Player
{
    /// <summary>균형 게이지 구간. 초록 안정 → 노랑 경고 → 빨강 추락 위험.</summary>
    public enum BalanceZone
    {
        Green,
        Red,
        Yellow, // 기존 Red의 직렬화 값(1)은 유지한다.
    }

    /// <summary>
    /// 플레이어의 균형 값(−100 왼쪽 ~ 0 중앙 ~ +100 오른쪽)을 관리한다.
    /// 매 초 변화량 = 자연 흔들림 × 목마 배율 + 기울기 가속(균형값 × 계수) + 위층 전달 + 보정 입력(조작 규칙이 넣음).
    /// 규칙: Harness/Project/Decisions/tightrope-balance.md · 수치: Docs/References/tightrope-balance-summary.md
    /// 균형은 줄 위에서만 쓴다. 외줄 기능이 <see cref="SetBalanceActive"/>로 켜고 끈다.
    /// 빨강에 일정 시간 머물면 <see cref="Fell"/> 신호를 한 번 보내고 멈춘다. 실제 낙하는 외줄 기능이 처리한다.
    /// 다른 기능(외줄·목마·UI)은 <see cref="IsActive"/>, <see cref="Value"/>, <see cref="Zone"/>, <see cref="HasFallen"/>을 읽는다.
    /// 수치는 세팅에서 매 프레임 읽는다: 균형 공통 = 게임 기본 세팅, 착지 충격·옆줄 흔들림·목마 = 외줄 세팅(<see cref="GameSettings"/>).
    /// </summary>
    public class PlayerBalance : MonoBehaviour
    {
        /// <summary>균형 값의 끝(±). 바늘은 여기서 멈춘다.</summary>
        public const float MaxValue = 100f;

        [Tooltip("시작할 때 균형을 켤지. 테스트 씬용. 실제 게임에서는 외줄 기능이 켠다.")]
        [SerializeField] bool activeOnStart = true;

        [Header("목마 (테스트)")]
        [Tooltip("현재 목마 전체 인원. 목마 기능이 SetStackSize로 바꾼다. 연결 전에는 여기서 테스트.")]
        [SerializeField, Range(1, 4)] int stackSize = 1;

        [Tooltip("내 위에 있는 층들의 균형값 합. 목마 기능이 매 프레임 SetUpperBalanceSum으로 넣는다. 맨 위는 0. 연결 전에는 여기서 테스트.")]
        [SerializeField, Range(-300f, 300f)] float upperBalanceSum;

        float correctionInput;
        PlayerMover mover;
        float airborneSince;
        float laneBoostUntil = -1f;
        float laneBoostScale = 1f;
        float laneLandingReduction;
        float swayDirection = 1f;
        float nextDirectionChange;
        float surfaceSwayMultiplier = 1f;
        float surfaceSway;
        float landingDirection = 1f;

        static BaseGameSettings.BalanceSettings Rules => GameSettings.Base.Balance;
        static TightropeSettings.RopeMovementSettings Rope => GameSettings.Tightrope.RopeMovement;

        /// <summary>누적 추락 위험이 추락 기준에 닿았을 때 한 번 보낸다.</summary>
        public event Action Fell;

        /// <summary>균형을 중앙으로 되돌렸을 때(재시작·켜기/끄기) 보낸다. 추락 연출을 되돌리는 데 쓴다.</summary>
        public event Action BalanceReset;

        /// <summary>균형이 켜져 있는지. 꺼져 있으면 계산하지 않고 값은 0.</summary>
        public bool IsActive { get; private set; }

        /// <summary>현재 균형. −100 왼쪽 끝, 0 중앙, +100 오른쪽 끝.</summary>
        public float Value { get; private set; }

        /// <summary>현재 균형이 속한 구간.</summary>
        public BalanceZone Zone => ZoneOf(Value);

        /// <summary>초록 구간 경계(±). 게이지 표시용.</summary>
        public float GreenLimit => Rules.GreenLimit;

        /// <summary>노란 경고 구간 끝(±). 노랑은 느리게, 이 값 바깥의 빨강은 정상 속도로 위험 시간을 센다.</summary>
        public float WarningLimit => Rules.WarningLimit;

        /// <summary>이번 프레임의 자연 흔들림(초당, 방향 포함). 디버그 표시용.</summary>
        public float CurrentSway { get; private set; }

        /// <summary>노랑·빨강과 옆줄 착지로 누적한 추락 위험 시간(빨강 초 환산). 줄 위 초록 구간에서 설정한 비율로 서서히 회복한다.</summary>
        public float RedTime { get; private set; }

        /// <summary>추락 기준 시간(초).</summary>
        public float FallTime => Rules.FallTime;

        /// <summary>누적 추락 위험 비율(0~1). 노랑·빨강·옆줄 착지 위험을 합치며 1이면 추락한다.</summary>
        public float RedTimeRatio => Mathf.Clamp01(RedTime / FallTime);

        /// <summary>추락했는지. 추락하면 다시 시작(<see cref="ResetBalance"/>)할 때까지 균형 계산을 멈춘다.</summary>
        public bool HasFallen { get; private set; }

        /// <summary>현재 목마 전체 인원(1~4).</summary>
        public int StackSize => stackSize;

        /// <summary>현재 목마 인원 배율. 자연 흔들림과 충격에 곱한다.</summary>
        public float StackMultiplier => GameSettings.Tightrope.Piggyback.GetStackMultiplier(stackSize);

        /// <summary>이번 프레임 위층에서 전달된 흔들림(초당). 디버그 표시용.</summary>
        public float UpperPush => upperBalanceSum * GameSettings.Tightrope.Piggyback.UpperTransfer;

        /// <summary>목마 전체 인원을 정한다(혼자 1). 같은 목마의 모든 층에 같은 값을 넣는다.</summary>
        public void SetStackSize(int size) => stackSize = Mathf.Clamp(size, 1, 4);

        /// <summary>내 위에 있는 층들의 균형값 합을 넣는다. 맨 위·혼자면 0. 매 프레임 갱신한다.</summary>
        public void SetUpperBalanceSum(float sum) => upperBalanceSum = sum;

        /// <summary>자연 흔들림 배율(×, 0 이상). 기본 1. 예: 팔·다리를 잃으면 콘텐츠의 신체 손상 불이익이 넣는다.</summary>
        public float SwayMultiplier
        {
            get => swayMultiplier;
            set => swayMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>제자리 점프·낙하 착지 충격 배율(×, 0 이상). 기본 1.</summary>
        public float LandingShockMultiplier
        {
            get => landingShockMultiplier;
            set => landingShockMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>옆줄 점프 착지 충격 배율(×, 0 이상). 기본 1. 착지 후 흔들림 증가 시간·배율은 바꾸지 않는다.</summary>
        public float LaneLandingShockMultiplier
        {
            get => laneLandingShockMultiplier;
            set => laneLandingShockMultiplier = Mathf.Max(0f, value);
        }

        float swayMultiplier = 1f;
        float landingShockMultiplier = 1f;
        float laneLandingShockMultiplier = 1f;

        /// <summary>공중에 있는지. 착지 충격과 콘텐츠의 공중 균형 규칙에 사용한다.</summary>
        public bool IsAirborne { get; private set; }

        /// <summary>일반 도약 중에도 균형·위험 누적을 계속한다. 봉 등 외부 이동에서는 콘텐츠가 해제한다.</summary>
        public bool ContinueWhileAirborne { get; set; }

        /// <summary>줄·봉에 매달린 동안 자연 흔들림·충격·일반 추락 위험 누적을 멈춘다. 제한 시간은 콘텐츠가 별도 표시한다.</summary>
        public bool IsSuspended { get; set; }

        /// <summary>동료 구조 중 균형·충격·추락 위험 누적을 멈춘다. 매달림 상태와 별도로 구조 기능이 관리한다.</summary>
        public bool IsRescueSupported { get; set; }
        bool BalancePaused => IsSuspended || IsRescueSupported;

        /// <summary>
        /// 공중에 뜸·착지를 알린다. 착지하면(<c>false</c>) 착지 충격을 준다.
        /// 같은 오브젝트의 PlayerMover가 있으면 자동으로 연결된다. 목마 전체 점프처럼 다른 기능이 몸을 띄울 때 직접 부른다.
        /// </summary>
        public void SetAirborne(bool airborne)
        {
            if (IsAirborne == airborne) return;
            IsAirborne = airborne;
            if (airborne)
            {
                airborneSince = Time.time;
                landingDirection = mover != null && mover.LaneJumpDirection != 0
                    ? mover.LaneJumpDirection : Mathf.Abs(Value) > .001f ? Mathf.Sign(Value) : swayDirection;
                return;
            }

            bool lane = mover != null && mover.CurrentJump == JumpKind.Lane;
            float reduction = laneLandingReduction;
            laneLandingReduction = 0f;

            if (lane) ApplyLaneLanding(reduction);
            else if (Time.time - airborneSince >= Rope.MinAirTimeForShock)
                ApplyLandingShock(Rope.LandingShock * landingShockMultiplier * (1f - reduction));
        }

        /// <summary>
        /// 다음 옆줄 착지의 충격·흔들림 감소율(0~1)을 정한다. 외줄 기능이 착지 전에 넣는다.
        /// 예: 도착점이 같은 줄 동료 앞뒤 1.0m 이내면 0.5. 착지하면 0으로 돌아간다.
        /// </summary>
        public void SetLaneLandingReduction(float reduction) => laneLandingReduction = Mathf.Clamp01(reduction);

        /// <summary>다음 실제 착지 한 번의 충격 완충율(0~1). 일반 도약과 옆줄 도약 모두 착지 후 자동 초기화한다.</summary>
        public void SetLandingReduction(float reduction) => laneLandingReduction = Mathf.Clamp01(reduction);

        /// <summary>옆줄 착지 충격·흔들림 증가·고정 비율의 추락 위험을 준다. 감소율은 충격과 흔들림 증가분에 적용한다.</summary>
        public void ApplyLaneLanding(float reduction)
        {
            if (!IsActive || HasFallen || BalancePaused) return;
            float keep = 1f - Mathf.Clamp01(reduction);
            ApplyLandingShock(Rope.LaneLandingShock * laneLandingShockMultiplier * keep);
            laneBoostScale = 1f + (Rope.LaneSwayBoost - 1f) * keep;
            laneBoostUntil = Time.time + Rope.LaneSwayBoostDuration; // 쌓지 않고 시간만 갱신
            RedTime = Mathf.Min(FallTime, RedTime + FallTime * Rope.LaneLandingDangerRatio);
            if (RedTime >= FallTime) GaugeFall();
        }

        /// <summary>옆줄 착지 후 흔들림 증가가 남아 있는 시간(초). 디버그 표시용.</summary>
        public float LaneBoostRemaining => Mathf.Max(0f, laneBoostUntil - Time.time);

        /// <summary>지금 자연 흔들림에 곱해지는 옆줄 착지 배율.</summary>
        public float LaneBoostMultiplier => LaneBoostRemaining > 0f ? laneBoostScale : 1f;

        void ApplyLandingShock(float magnitude)
        {
            if (!IsActive || HasFallen || BalancePaused || !float.IsFinite(magnitude) || magnitude <= 0f) return;
            // 착지가 우연한 중앙 복귀가 되지 않게 현재 기울기를 강화한다. 중앙이면 이륙 방향을 쓴다.
            float direction = Mathf.Abs(Value) > .001f ? Mathf.Sign(Value) : landingDirection;
            float impact = direction * magnitude * StackMultiplier;
            Value = Mathf.Clamp(Value + impact, -MaxValue, MaxValue);
            LastShock = impact;
        }

        /// <summary>균형에 순간 충격을 준다(±크기, 랜덤 방향, 목마 배율 적용).</summary>
        public void ApplyShock(float magnitude)
        {
            if (!IsActive || HasFallen || BalancePaused) return;
            float direction = Random.value < 0.5f ? -1f : 1f;
            Value = Mathf.Clamp(Value + direction * magnitude * StackMultiplier, -MaxValue, MaxValue);
            LastShock = direction * magnitude * StackMultiplier;
        }

        /// <summary>마지막으로 받은 충격(방향 포함). 디버그 표시용.</summary>
        public float LastShock { get; private set; }

        /// <summary>이번 프레임 균형 보정 입력. -1 왼쪽, +1 오른쪽으로 바늘을 민다. 매 프레임 넣어야 한다.</summary>
        public void SetCorrectionInput(float direction) => correctionInput = Mathf.Clamp(direction, -1f, 1f);

        /// <summary>이번 프레임 지지면의 흔들림. 기본 흔들림 배율과 추가 힘(초당)을 넣으며 입력하지 않으면 자동 해제한다.</summary>
        public void SetSurfaceSway(float multiplier, float force)
        {
            surfaceSwayMultiplier = float.IsFinite(multiplier) ? Mathf.Max(0f, multiplier) : 1f;
            surfaceSway = float.IsFinite(force) ? force : 0f;
        }

        /// <summary>균형 값으로 구간을 구한다.</summary>
        public BalanceZone ZoneOf(float value) =>
            Mathf.Abs(value) <= GreenLimit ? BalanceZone.Green :
            Mathf.Abs(value) <= WarningLimit ? BalanceZone.Yellow : BalanceZone.Red;

        /// <summary>균형을 켜거나 끈다. 켜고 끌 때 모두 중앙에서 시작한다.</summary>
        public void SetBalanceActive(bool active)
        {
            IsActive = active;
            ResetBalance();
        }

        /// <summary>
        /// 다른 컴퓨터가 계산한 균형 값을 보여 주기만 할 때 쓴다(온라인에서 남의 캐릭터). 흔들림 계산·추락 판정은 하지 않으므로
        /// 이 컴포넌트를 끈 상태에서 부른다. 몸 기울기 표시(<see cref="BalanceTiltView"/>)가 이 값을 따른다.
        /// </summary>
        public void SetDisplayedState(bool active, float value)
        {
            IsActive = active;
            Value = Mathf.Clamp(value, -MaxValue, MaxValue);
        }

        /// <summary>균형을 중앙으로 되돌리고 추락 상태를 푼다(재시작).</summary>
        public void ResetBalance()
        {
            Value = 0f;
            CurrentSway = 0f;
            RedTime = 0f;
            HasFallen = false;
            laneBoostUntil = -1f;
            laneLandingReduction = 0f;
            BalanceReset?.Invoke();
            IsAirborne = false;
            LastShock = 0f;
            surfaceSwayMultiplier = 1f;
            surfaceSway = 0f;
            landingDirection = 1f;
            PickSwayDirection();
        }

        void Awake()
        {
            mover = GetComponent<PlayerMover>();
            if (mover != null) mover.AirborneChanged += SetAirborne;
            SetBalanceActive(activeOnStart);
        }

        void OnDestroy()
        {
            if (mover != null) mover.AirborneChanged -= SetAirborne;
        }

        void Update() => Step(Time.deltaTime);

        void Step(float deltaTime)
        {
            float correction = correctionInput;
            correctionInput = 0f; // 입력은 한 프레임만 유효하다. 조작 규칙이 매 프레임 다시 넣는다.
            float surfaceMultiplier = surfaceSwayMultiplier;
            float surfaceForce = surfaceSway;
            surfaceSwayMultiplier = 1f;
            surfaceSway = 0f;
            if (!IsActive || HasFallen || BalancePaused || IsAirborne && !ContinueWhileAirborne) return;

            if (Time.time >= nextDirectionChange)
                PickSwayDirection();

            var rules = Rules;
            bool moving = mover != null && (mover.IsMoving || IsAirborne && mover.Body != null
                && Vector3.ProjectOnPlane(mover.Body.velocity, Vector3.up).sqrMagnitude > .0001f);
            CurrentSway = (swayDirection * (moving ? rules.MovingSway : rules.IdleSway) * surfaceMultiplier + surfaceForce)
                         * StackMultiplier * LaneBoostMultiplier * swayMultiplier;

            float correctionRate = correction * rules.CorrectionSpeed;
            float rate = CurrentSway
                         + Value * rules.TiltAcceleration
                         + UpperPush
                         + correctionRate;
            // 중앙으로 보정할 때는 바깥 흔들림이 입력을 상쇄하지 못하게 한다.
            // 기본 보정 속도로 즉시 복귀하며, 복귀 방향으로 부는 흔들림은 그대로 더한다.
            if (Value * correctionRate < 0f)
                rate = correctionRate > 0f ? Mathf.Max(rate, correctionRate) : Mathf.Min(rate, correctionRate);
            Value = Mathf.Clamp(Value + rate * deltaTime, -MaxValue, MaxValue);

            UpdateRedTime(deltaTime);
        }

        bool IsLaneJumping => IsAirborne && mover != null && mover.CurrentJump == JumpKind.Lane;

        void UpdateRedTime(float deltaTime)
        {
            // 옆줄 점프 중에는 추락 위험을 쌓지도 줄이지도 않는다. 착지 위험은 ApplyLaneLanding이 한 번 준다.
            if (IsLaneJumping) return;
            if (Zone == BalanceZone.Green)
            {
                if (!IsAirborne)
                    RedTime = Mathf.Max(0f, RedTime - FallTime * Rules.GreenRecoveryRate * Mathf.Max(0f, deltaTime));
                return;
            }

            RedTime += Mathf.Max(0f, deltaTime) * (Zone == BalanceZone.Yellow ? Rules.WarningFallRate : 1f);
            float fallTime = FallTime;
            if (RedTime < fallTime) return;

            RedTime = fallTime;
            GaugeFall();
        }

        /// <summary>
        /// 추락 게이지가 넘쳤을 때 먼저 묻는 곳(M2 외줄 매달림). true를 돌려주면 이 균형은 추락하지 않고 게이지만 비운다.
        /// 줄 밖 착지·바닥선 같은 <see cref="ForceFall"/>은 거치지 않는다.
        /// </summary>
        public static Func<PlayerBalance, bool> GaugeFallCatcher;

        void GaugeFall()
        {
            if (HasFallen) return;
            if (GaugeFallCatcher != null && GaugeFallCatcher(this))
            {
                RedTime = 0f;
                Value = 0f;
                CurrentSway = 0f;
                return;
            }
            ForceFall();
        }

        /// <summary>
        /// 봉에 매달려 몸이 지지되는 동안 쌓인 추락 위험을 초록 구간과 같은 속도로 천천히 줄인다.
        /// 매달린 동안은 공중 상태라 균형 계산이 멈추므로, 봉 기능이 매 프레임 한 번 부른다. 균형 값·흔들림은 바꾸지 않는다.
        /// </summary>
        public void RecoverWhileSupported(float deltaTime)
        {
            if (!IsActive || HasFallen) return;
            RedTime = Mathf.Max(0f, RedTime - FallTime * Rules.GreenRecoveryRate * Mathf.Max(0f, deltaTime));
        }

        /// <summary>균형과 상관없이 바로 추락시킨다(예: 줄이 아닌 곳에 착지). 이미 추락했으면 무시한다. <see cref="Fell"/>을 보낸다.</summary>
        public void ForceFall()
        {
            if (HasFallen) return;
            HasFallen = true;
            CurrentSway = 0f;
            Fell?.Invoke();
        }

        void PickSwayDirection()
        {
            swayDirection = Random.value < 0.5f ? -1f : 1f;
            Vector2 interval = Rules.SwayDirectionInterval;
            nextDirectionChange = Time.time + Random.Range(interval.x, interval.y);
        }
    }
}
