using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 잃은 부위가 주는 불이익 묶음. 모두 배율(×)이고 1이면 불이익 없음.
    /// 종류는 게임 공통(팔 → 상호작용, 다리 → 이동, 균형 있는 묘기는 흔들림·착지 충격), 크기는 묘기마다 정책이 정한다.
    /// </summary>
    public struct BodyPenalty
    {
        public float MoveSpeed;
        public float Sway;
        public float LandingShock;
        public float LaneLandingShock;
        public float Reach;

        /// <summary>불이익 없음(모두 1).</summary>
        public static BodyPenalty None => new()
        {
            MoveSpeed = 1f,
            Sway = 1f,
            LandingShock = 1f,
            LaneLandingShock = 1f,
            Reach = 1f,
        };
    }

    /// <summary>
    /// 콘텐츠(묘기)마다 끼우는 신체 손상 불이익 정책. 잃은 부위를 보고 <see cref="BodyPenalty"/>를 돌려준다.
    /// 정책은 여러 플레이어가 함께 쓰므로 상태를 갖지 않는다(플레이어별 값은 <see cref="BodyDamageEffects"/>가 들고 적용한다).
    /// 수치는 정책이 아니라 그 묘기의 세팅에서 읽는다.
    /// </summary>
    public abstract class BodyPenaltyPolicy : ScriptableObject
    {
        public abstract BodyPenalty Evaluate(PlayerCondition condition);
    }
}
