using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 잃은 부위가 이 플레이어 몸에 주는 효과를 매 프레임 적용한다. 다른 플레이어에게는 영향이 없다(값은 이 플레이어 부품에만 넣는다).
    /// 1) 게임 공통: 두 다리를 잃으면 기어간다(속도 배율·캡슐 높이·점프 허용, 게임 기본 세팅 · 신체 손상).
    /// 2) 콘텐츠 불이익: 묘기가 <see cref="SetPolicy"/>로 끼운 정책(<see cref="BodyPenaltyPolicy"/>)의 배율. 바꾸거나 빼면(null) 다음 프레임에 이전 효과가 사라진다.
    /// 시작 정책을 비우면 외줄 정책(<see cref="TightropeBodyPenalty"/>)을 쓴다(지금 콘텐츠는 외줄뿐).
    /// 캡슐 높이는 모든 화면에서 같게 바꾸고, 속도·균형·거리는 조작하는 화면에서만 실제로 쓰인다.
    /// 겉모습(팔다리 분리·몸통 내림·애니메이터 값)은 <see cref="LimbDismemberment"/>가 한다.
    /// </summary>
    [RequireComponent(typeof(PlayerCondition))]
    [RequireComponent(typeof(PlayerMover))]
    public sealed class BodyDamageEffects : MonoBehaviour
    {
        [Tooltip("시작할 때 끼울 콘텐츠 불이익 정책. 비우면 외줄 정책.")]
        [SerializeField] BodyPenaltyPolicy startPolicy;

        PlayerCondition condition;
        PlayerMover mover;
        PlayerBalance balance;
        PlayerInteraction interaction;

        /// <summary>지금 끼워진 콘텐츠 불이익 정책. 없으면 null(콘텐츠 불이익 없음).</summary>
        public BodyPenaltyPolicy Policy { get; private set; }

        /// <summary>지금 적용 중인 콘텐츠 불이익(디버그 표시용).</summary>
        public BodyPenalty Current { get; private set; } = BodyPenalty.None;

        /// <summary>지금 기어가는 중인지(두 다리를 모두 잃음).</summary>
        public bool IsCrawling => condition != null && condition.LostLegCount >= 2;

        /// <summary>콘텐츠 불이익 정책을 바꾼다. null이면 콘텐츠 불이익을 뺀다(기어가기 같은 공통 효과는 남는다).</summary>
        public void SetPolicy(BodyPenaltyPolicy policy) => Policy = policy;

        void Awake()
        {
            condition = GetComponent<PlayerCondition>();
            mover = GetComponent<PlayerMover>();
            balance = GetComponent<PlayerBalance>();
            interaction = GetComponent<PlayerInteraction>();

            var policy = startPolicy;
            if (policy == null)
            {
                policy = ScriptableObject.CreateInstance<TightropeBodyPenalty>();
                policy.name = "TightropeBodyPenalty (기본)";
            }
            SetPolicy(policy);
        }

        void Update()
        {
            var common = GameSettings.Base.BodyDamage;
            bool crawling = IsCrawling;
            var penalty = Policy != null ? Policy.Evaluate(condition) : BodyPenalty.None;
            Current = penalty;

            mover.SpeedMultiplier = (crawling ? common.CrawlSpeedMultiplier : 1f) * penalty.MoveSpeed;
            mover.JumpAllowed = !crawling || common.CrawlCanJump;
            mover.LaneJumpAllowed = !crawling || common.CrawlCanLaneJump;
            mover.BodyHeightOverride = crawling ? common.CrawlCapsuleHeight : 0f;

            if (balance != null)
            {
                balance.SwayMultiplier = penalty.Sway;
                balance.LandingShockMultiplier = penalty.LandingShock;
                balance.LaneLandingShockMultiplier = penalty.LaneLandingShock;
            }
            if (interaction != null) interaction.ReachMultiplier = penalty.Reach;
        }

        void OnDisable()
        {
            Current = BodyPenalty.None;
            if (mover != null)
            {
                mover.SpeedMultiplier = 1f;
                mover.JumpAllowed = true;
                mover.LaneJumpAllowed = true;
                mover.BodyHeightOverride = 0f;
            }
            if (balance != null)
            {
                balance.SwayMultiplier = 1f;
                balance.LandingShockMultiplier = 1f;
                balance.LaneLandingShockMultiplier = 1f;
            }
            if (interaction != null) interaction.ReachMultiplier = 1f;
        }
    }
}
