using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CurtainCall.Player
{
    /// <summary>균형 게이지 구간. 가운데 초록, 양쪽 끝 빨강.</summary>
    public enum BalanceZone
    {
        Green,
        Red,
    }

    /// <summary>
    /// 플레이어의 균형 값(−100 왼쪽 ~ 0 중앙 ~ +100 오른쪽)을 관리한다.
    /// 매 초 변화량 = 자연 흔들림 × 목마 배율 + 기울기 가속(균형값 × 계수) + 위층 전달 + 보정 입력(조작 규칙이 넣음).
    /// 규칙: Harness/Project/Decisions/tightrope-balance.md · 수치: Docs/References/tightrope-balance-summary.md
    /// 균형은 줄 위에서만 쓴다. 외줄 기능이 <see cref="SetBalanceActive"/>로 켜고 끈다.
    /// 빨강에 일정 시간 머물면 <see cref="Fell"/> 신호를 한 번 보내고 멈춘다. 실제 낙하는 외줄 기능이 처리한다.
    /// 다른 기능(외줄·목마·UI)은 <see cref="IsActive"/>, <see cref="Value"/>, <see cref="Zone"/>, <see cref="HasFallen"/>을 읽는다.
    /// </summary>
    public class PlayerBalance : MonoBehaviour
    {
        /// <summary>균형 값의 끝(±). 바늘은 여기서 멈춘다.</summary>
        public const float MaxValue = 100f;

        [Tooltip("시작할 때 균형을 켤지. 테스트 씬용. 실제 게임에서는 외줄 기능이 켠다.")]
        [SerializeField] bool activeOnStart = true;

        [Header("구간")]
        [Tooltip("중앙에서 이 값(±)까지 초록, 넘으면 빨강. 기획 제안값 40.")]
        [SerializeField, Range(0f, MaxValue)] float greenLimit = 40f;

        [Header("추락")]
        [Tooltip("빨강에 이 시간(초) 이상 머물면 추락. 기획 확정값 2초.")]
        [SerializeField, Min(0.05f)] float fallTime = 2f;

        [Header("보정")]
        [Tooltip("보정 입력(외줄 규칙에서는 A/D)을 끝까지 넣었을 때 초당 바늘이 움직이는 양. 기획 제안값 60.")]
        [SerializeField, Min(0f)] float correctionSpeed = 60f;

        [Header("자연 흔들림")]
        [Tooltip("이동 중(W/S) 초당 흔들림. 기획 제안값 15.")]
        [SerializeField, Min(0f)] float movingSway = 15f;

        [Tooltip("정지 중 초당 흔들림. 기획 제안값 5.")]
        [SerializeField, Min(0f)] float idleSway = 5f;

        [Tooltip("흔들림 방향이 바뀌는 간격(초). 이 범위에서 랜덤. 기획 제안값 1~2초.")]
        [SerializeField] Vector2 swayDirectionInterval = new Vector2(1f, 2f);

        [Header("기울기 가속")]
        [Tooltip("초당 (현재 균형값 × 이 값)만큼 바깥으로 민다. 많이 기울수록 빨리 넘어간다. 기획 제안값 0.5.")]
        [SerializeField, Min(0f)] float tiltAcceleration = 0.5f;

        [Header("충격")]
        [Tooltip("착지 충격 크기(±, 랜덤 방향). 목마 배율을 곱한다. 기획 제안값 10.")]
        [SerializeField, Min(0f)] float landingShock = 10f;

        [Tooltip("이 시간(초)보다 짧게 떠 있었으면 착지 충격을 주지 않는다(작은 턱·접지 흔들림 무시).")]
        [SerializeField, Min(0f)] float minAirTimeForShock = 0.15f;

        [Tooltip("단독 옆줄 점프 착지 충격(±, 랜덤 방향). 목마 배율을 곱한다. 기획 제안값 35.")]
        [SerializeField, Min(0f)] float laneLandingShock = 35f;

        [Tooltip("단독 옆줄 점프 착지 후 자연 흔들림 배율. 기획 제안값 2.5.")]
        [SerializeField, Min(1f)] float laneSwayBoost = 2.5f;

        [Tooltip("단독 옆줄 점프 착지 후 흔들림이 커지는 시간(초). 반복해도 쌓이지 않고 시간만 갱신. 기획 제안값 3.")]
        [SerializeField, Min(0f)] float laneSwayBoostDuration = 3f;

        [Header("목마")]
        [Tooltip("목마 전체 인원별 배율(1인~4인). 자연 흔들림과 충격에 곱한다. 기획 제안값 1.0 / 1.3 / 1.6 / 2.0.")]
        [SerializeField] float[] stackMultipliers = { 1f, 1.3f, 1.6f, 2f };

        [Tooltip("초당 (내 위층 균형값 합 × 이 값)만큼 같은 방향으로 민다. 기획 제안값 0.2.")]
        [SerializeField, Min(0f)] float upperTransfer = 0.2f;

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

        /// <summary>빨강 체류 시간이 추락 기준에 닿았을 때 한 번 보낸다.</summary>
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
        public float GreenLimit => greenLimit;

        /// <summary>이번 프레임의 자연 흔들림(초당, 방향 포함). 디버그 표시용.</summary>
        public float CurrentSway { get; private set; }

        /// <summary>빨강에 연속으로 머문 시간(초). 초록으로 돌아오면 0.</summary>
        public float RedTime { get; private set; }

        /// <summary>추락 기준 시간(초).</summary>
        public float FallTime => fallTime;

        /// <summary>빨강 체류 비율(0~1). 1이면 추락. 게이지 표시용.</summary>
        public float RedTimeRatio => Mathf.Clamp01(RedTime / fallTime);

        /// <summary>추락했는지. 추락하면 다시 시작(<see cref="ResetBalance"/>)할 때까지 균형 계산을 멈춘다.</summary>
        public bool HasFallen { get; private set; }

        /// <summary>현재 목마 전체 인원(1~4).</summary>
        public int StackSize => stackSize;

        /// <summary>현재 목마 인원 배율. 자연 흔들림과 충격에 곱한다.</summary>
        public float StackMultiplier =>
            stackMultipliers == null || stackMultipliers.Length == 0
                ? 1f
                : stackMultipliers[Mathf.Clamp(stackSize, 1, stackMultipliers.Length) - 1];

        /// <summary>이번 프레임 위층에서 전달된 흔들림(초당). 디버그 표시용.</summary>
        public float UpperPush => upperBalanceSum * upperTransfer;

        /// <summary>목마 전체 인원을 정한다(혼자 1). 같은 목마의 모든 층에 같은 값을 넣는다.</summary>
        public void SetStackSize(int size) => stackSize = Mathf.Clamp(size, 1, 4);

        /// <summary>내 위에 있는 층들의 균형값 합을 넣는다. 맨 위·혼자면 0. 매 프레임 갱신한다.</summary>
        public void SetUpperBalanceSum(float sum) => upperBalanceSum = sum;

        /// <summary>공중에 있는지. 공중에서는 균형 값이 멈춘다.</summary>
        public bool IsAirborne { get; private set; }

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
                return;
            }

            bool lane = mover != null && mover.CurrentJump == JumpKind.Lane;
            float reduction = laneLandingReduction;
            laneLandingReduction = 0f;

            if (lane) ApplyLaneLanding(reduction);
            else if (Time.time - airborneSince >= minAirTimeForShock) ApplyShock(landingShock);
        }

        /// <summary>
        /// 다음 옆줄 착지의 충격·흔들림 감소율(0~1)을 정한다. 외줄 기능이 착지 전에 넣는다.
        /// 예: 도착점이 같은 줄 동료 앞뒤 1.0m 이내면 0.5. 착지하면 0으로 돌아간다.
        /// </summary>
        public void SetLaneLandingReduction(float reduction) => laneLandingReduction = Mathf.Clamp01(reduction);

        /// <summary>옆줄 착지 충격과 일정 시간 흔들림 증가를 준다. 감소율은 충격과 흔들림 증가분에 같이 적용한다.</summary>
        public void ApplyLaneLanding(float reduction)
        {
            if (!IsActive || HasFallen) return;
            float keep = 1f - Mathf.Clamp01(reduction);
            ApplyShock(laneLandingShock * keep);
            laneBoostScale = 1f + (laneSwayBoost - 1f) * keep;
            laneBoostUntil = Time.time + laneSwayBoostDuration; // 쌓지 않고 시간만 갱신
        }

        /// <summary>옆줄 착지 후 흔들림 증가가 남아 있는 시간(초). 디버그 표시용.</summary>
        public float LaneBoostRemaining => Mathf.Max(0f, laneBoostUntil - Time.time);

        /// <summary>지금 자연 흔들림에 곱해지는 옆줄 착지 배율.</summary>
        public float LaneBoostMultiplier => LaneBoostRemaining > 0f ? laneBoostScale : 1f;

        /// <summary>균형에 순간 충격을 준다(±크기, 랜덤 방향, 목마 배율 적용).</summary>
        public void ApplyShock(float magnitude)
        {
            if (!IsActive || HasFallen) return;
            float direction = Random.value < 0.5f ? -1f : 1f;
            Value = Mathf.Clamp(Value + direction * magnitude * StackMultiplier, -MaxValue, MaxValue);
            LastShock = direction * magnitude * StackMultiplier;
        }

        /// <summary>마지막으로 받은 충격(방향 포함). 디버그 표시용.</summary>
        public float LastShock { get; private set; }

        /// <summary>이번 프레임 균형 보정 입력. -1 왼쪽, +1 오른쪽으로 바늘을 민다. 매 프레임 넣어야 한다.</summary>
        public void SetCorrectionInput(float direction) => correctionInput = Mathf.Clamp(direction, -1f, 1f);

        /// <summary>균형 값으로 구간을 구한다.</summary>
        public BalanceZone ZoneOf(float value) =>
            Mathf.Abs(value) <= greenLimit ? BalanceZone.Green : BalanceZone.Red;

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

        void OnValidate()
        {
            if (swayDirectionInterval.x < 0.05f) swayDirectionInterval.x = 0.05f;
            if (swayDirectionInterval.y < swayDirectionInterval.x) swayDirectionInterval.y = swayDirectionInterval.x;
        }

        void Update()
        {
            float correction = correctionInput;
            correctionInput = 0f; // 입력은 한 프레임만 유효하다. 조작 규칙이 매 프레임 다시 넣는다.
            if (!IsActive || HasFallen || IsAirborne) return;

            if (Time.time >= nextDirectionChange)
                PickSwayDirection();

            bool moving = mover != null && mover.IsMoving;
            CurrentSway = swayDirection * (moving ? movingSway : idleSway) * StackMultiplier * LaneBoostMultiplier;

            float rate = CurrentSway
                         + Value * tiltAcceleration
                         + UpperPush
                         + correction * correctionSpeed;
            Value = Mathf.Clamp(Value + rate * Time.deltaTime, -MaxValue, MaxValue);

            UpdateRedTime();
        }

        void UpdateRedTime()
        {
            if (Zone == BalanceZone.Green)
            {
                RedTime = 0f;
                return;
            }

            RedTime += Time.deltaTime;
            if (RedTime < fallTime) return;

            RedTime = fallTime;
            ForceFall();
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
            nextDirectionChange = Time.time + Random.Range(swayDirectionInterval.x, swayDirectionInterval.y);
        }
    }
}
