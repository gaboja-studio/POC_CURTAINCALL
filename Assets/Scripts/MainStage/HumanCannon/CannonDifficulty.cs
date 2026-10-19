using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>2026-10-16 승인 시험 프리셋. 10분은 목표이며 시간 초과 실패 규칙은 추가하지 않는다.</summary>
    public static class CannonDifficulty
    {
        public const float TargetSeconds = 600f;
        public static bool Valid(int level) => level >= 1 && level <= 3;
        public static float HazardSpeed(int level) => 1f + (Mathf.Clamp(level, 1, 3) - 1) * 0.25f;
        public static float WindSpeed(int level) => 0.6f + (Mathf.Clamp(level, 1, 3) - 1) * 0.2f;
    }
}
