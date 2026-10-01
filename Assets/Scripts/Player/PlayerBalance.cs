using UnityEngine;

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
    /// 매 초 변화량 = 자연 흔들림 + 기울기 가속(균형값 × 계수) + A/D 보정.
    /// 규칙: Harness/Project/Decisions/tightrope-balance.md · 수치: Docs/References/tightrope-balance-summary.md
    /// 균형은 줄 위에서만 쓴다. 외줄 기능이 <see cref="SetBalanceActive"/>로 켜고 끈다.
    /// 다른 기능(외줄·목마·UI)은 <see cref="IsActive"/>, <see cref="Value"/>, <see cref="Zone"/>을 읽는다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerBalance : MonoBehaviour
    {
        /// <summary>균형 값의 끝(±). 바늘은 여기서 멈춘다.</summary>
        public const float MaxValue = 100f;

        [Tooltip("시작할 때 균형을 켤지. 테스트 씬용. 실제 게임에서는 외줄 기능이 켠다.")]
        [SerializeField] bool activeOnStart = true;

        [Header("구간")]
        [Tooltip("중앙에서 이 값(±)까지 초록, 넘으면 빨강. 기획 제안값 40.")]
        [SerializeField, Range(0f, MaxValue)] float greenLimit = 40f;

        [Header("보정")]
        [Tooltip("A/D를 끝까지 눌렀을 때 초당 바늘이 움직이는 양. 기획 제안값 60.")]
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

        PlayerInputReader input;
        float swayDirection = 1f;
        float nextDirectionChange;

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

        /// <summary>균형 값으로 구간을 구한다.</summary>
        public BalanceZone ZoneOf(float value) =>
            Mathf.Abs(value) <= greenLimit ? BalanceZone.Green : BalanceZone.Red;

        /// <summary>균형을 켜거나 끈다. 켜고 끌 때 모두 중앙에서 시작한다.</summary>
        public void SetBalanceActive(bool active)
        {
            IsActive = active;
            ResetBalance();
        }

        /// <summary>균형을 중앙으로 되돌린다.</summary>
        public void ResetBalance()
        {
            Value = 0f;
            CurrentSway = 0f;
            PickSwayDirection();
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            SetBalanceActive(activeOnStart);
        }

        void OnValidate()
        {
            if (swayDirectionInterval.x < 0.05f) swayDirectionInterval.x = 0.05f;
            if (swayDirectionInterval.y < swayDirectionInterval.x) swayDirectionInterval.y = swayDirectionInterval.x;
        }

        void Update()
        {
            if (!IsActive) return;

            if (Time.time >= nextDirectionChange)
                PickSwayDirection();

            PlayerCommand cmd = input.Current;
            bool moving = !Mathf.Approximately(cmd.Move, 0f);
            CurrentSway = swayDirection * (moving ? movingSway : idleSway);

            float rate = CurrentSway
                         + Value * tiltAcceleration
                         + cmd.Posture * correctionSpeed;
            Value = Mathf.Clamp(Value + rate * Time.deltaTime, -MaxValue, MaxValue);
        }

        void PickSwayDirection()
        {
            swayDirection = Random.value < 0.5f ? -1f : 1f;
            nextDirectionChange = Time.time + Random.Range(swayDirectionInterval.x, swayDirectionInterval.y);
        }
    }
}
