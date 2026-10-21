using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 묘기별 세팅의 공통 부모. 묘기마다 하나(예: <see cref="TightropeSettings"/>)를 Assets/Resources/Settings/GameSettings/Tricks/에 둔다.
    /// 보상 규칙은 교체형으로, <see cref="RewardTable"/>을 구현한 파일을 보상 칸에 끼운다.
    /// </summary>
    public abstract class TrickSettings : ScriptableObject
    {
        [Header("이번 빌드 톱날 배치")]
        [Tooltip("이번 빌드는 1단계 고정: 기준 배치의 60%. 난이도 선택 기능은 구현하지 않는다. 2단계 85%·3단계 110%는 추후 계획이며 이번 빌드에 적용하지 않는다.")]
        [SerializeField, Range(0f, 1f)] float sawDensity = .6f;
        [Tooltip("위에서 본 기존 이동축과 사선 경로의 각도(도). 톱마다 +/−를 번갈아 쓴다.")]
        [SerializeField, Range(0f, 60f)] float sawDiagonalDegrees = 30f;

        public float SawDensity => Mathf.Clamp01(sawDensity);
        public float SawDiagonalDegrees => Mathf.Clamp(sawDiagonalDegrees, 0f, 60f);

        public static int SawCount(int total, float density) =>
            Mathf.CeilToInt(Mathf.Max(0, total) * Mathf.Clamp01(density));

        /// <summary>코스 전체에 고르게 남긴다. 표시와 피해 판정에서 같은 선택을 사용한다.</summary>
        public static bool IncludesSaw(int index, int total, float density)
        {
            if (index < 0 || index >= total) return false;
            int count = SawCount(total, density);
            return (long)(index + 1) * count / total > (long)index * count / total;
        }

        public static float DiagonalSlope(int index, float degrees) =>
            Mathf.Tan(Mathf.Clamp(degrees, 0f, 60f) * Mathf.Deg2Rad) * (index % 2 == 0 ? 1f : -1f);

        [Header("보상")]
        [Tooltip("이 묘기의 보상 규칙 파일(Tricks/Rewards/). 규칙을 바꾸려면 다른 파일을 끼운다. 미연결 묘기는 연결 Task에서 배정한다.")]
        [SerializeField] RewardTable reward;

        /// <summary>이 묘기의 보상 규칙. 연결 전이면 null.</summary>
        public RewardTable Reward => reward;
    }
}
