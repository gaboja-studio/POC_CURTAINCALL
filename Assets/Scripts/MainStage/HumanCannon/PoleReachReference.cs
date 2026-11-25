using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 높은 봉 판정 상자를 조절할 때 쓰는 기준 높이(발판 윗면 기준, 플레이어 몸 중심, m).
    /// 현재 GameSettings 캐릭터·목마 값에서 계산한다. 2단 점프는 같은 높이를 한 번 더 뛴다고 보는 근사값이다.
    /// 판정·이동에는 쓰지 않고 에디터 표시·검사에만 쓴다.
    /// </summary>
    public static class PoleReachReference
    {
        static float Capsule => GameSettings.Base.Character.CapsuleHeight;
        static float Jump => GameSettings.Base.Character.JumpHeight;

        public static float SoloStand => Capsule * .5f;
        public static float SoloJump => SoloStand + Jump;
        public static float SoloDoubleJump => SoloStand + Jump * 2f;
        public static float PiggybackStand => Capsule + Capsule * .5f;
        public static float PiggybackJump => PiggybackStand + Jump * GameSettings.Tightrope.Piggyback.JumpHeightMultiplier;

        /// <summary>판정 상자 기준으로 바로 아래 발판에서 그 높이의 몸 중심이 상자 안에 드는지.</summary>
        public static bool Reaches(PoleGrabZone zone, Vector3 bar, Quaternion yaw, float floorY, float centerHeight)
        {
            Vector3 point = bar + yaw * new Vector3(zone.center.x, 0f, zone.center.z);
            point.y = floorY + centerHeight;
            return zone.Contains(bar, yaw, point);
        }
    }
}
