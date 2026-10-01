using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>균형 게이지 구간. 가운데부터 초록 → 노랑 → 빨강.</summary>
    public enum BalanceZone
    {
        Green,
        Yellow,
        Red,
    }

    /// <summary>
    /// 플레이어의 균형 값(-1 왼쪽 ~ 0 중앙 ~ +1 오른쪽)을 관리한다.
    /// A/D 자세 명령으로 바늘을 민다. 규칙: Harness/Project/Decisions/tightrope-balance.md
    /// 다른 기능(외줄·목마·UI)은 <see cref="Value"/>, <see cref="Zone"/>만 읽는다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerBalance : MonoBehaviour
    {
        [Header("구간 (중앙에서 끝까지 0~1)")]
        [Tooltip("이 값까지는 초록 구간.")]
        [SerializeField, Range(0f, 1f)] float greenLimit = 0.35f;

        [Tooltip("이 값까지는 노랑 구간, 넘으면 빨강 구간.")]
        [SerializeField, Range(0f, 1f)] float yellowLimit = 0.7f;

        [Header("보정")]
        [Tooltip("A/D를 끝까지 눌렀을 때 초당 바늘이 움직이는 양.")]
        [SerializeField, Min(0f)] float correctionSpeed = 1.2f;

        PlayerInputReader input;

        /// <summary>현재 균형. -1 왼쪽 끝, 0 중앙, +1 오른쪽 끝.</summary>
        public float Value { get; private set; }

        /// <summary>현재 균형이 속한 구간.</summary>
        public BalanceZone Zone => ZoneOf(Value);

        /// <summary>초록 구간 경계(0~1). 게이지 표시용.</summary>
        public float GreenLimit => greenLimit;

        /// <summary>노랑 구간 경계(0~1). 게이지 표시용.</summary>
        public float YellowLimit => yellowLimit;

        /// <summary>균형 값으로 구간을 구한다.</summary>
        public BalanceZone ZoneOf(float value)
        {
            float distance = Mathf.Abs(value);
            if (distance <= greenLimit) return BalanceZone.Green;
            if (distance <= yellowLimit) return BalanceZone.Yellow;
            return BalanceZone.Red;
        }

        /// <summary>균형을 중앙으로 되돌린다.</summary>
        public void ResetBalance() => Value = 0f;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
        }

        void OnValidate()
        {
            if (yellowLimit < greenLimit) yellowLimit = greenLimit;
        }

        void Update()
        {
            float push = input.Current.Posture * correctionSpeed;
            Value = Mathf.Clamp(Value + push * Time.deltaTime, -1f, 1f);
        }
    }
}
