using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 공연 환호·야유·관심 감소 값. 기준: 보상 확정 규칙(reward-rules.md, Confluence 27459631).
    /// 협동 양쪽 각 +10, 오브제 최초 성공 +5, 근접 회피 +5, 추락 −15, 시간 끌기 15초마다 −10, 관심 감소 20초 유예 뒤 초당 −1.
    /// 절단 보너스·상한·완주 보너스는 단계별 보상 표(<see cref="RewardTable"/>)가 가진다. 거리 기준은 플레이 테스트 후 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Curtain Call/Settings/Audience reactions")]
    public sealed class AudienceReactionSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/GameSettings/Tricks/Rewards/temporary-audience";
        public static AudienceReactionSettings Current => Resources.Load<AudienceReactionSettings>(ResourcePath);
        [Tooltip("협동 상호작용 성공 시 참여한 각자")][Min(0)] public int cooperation = 10;
        [Tooltip("오브제 의도 행동 성공(같은 오브제 공연당 최초 1회)")][Min(0)] public int objectFirst = 5;
        [Tooltip("장애물 근접 회피 성공")][Min(0)] public int closeDodge = 5;
        [Tooltip("추락 1회 감점(외줄은 매달림 시작에 적용)")][Min(0)] public int fallJeer = 15;
        [Tooltip("진행 정지 반복 감점")][Min(0)] public int stallJeer = 10;
        [Tooltip("같은 자리에서 진행이 없을 때 감점 간격(초)")][Min(1)] public float stallSeconds = 15;
        [Tooltip("이만큼 진행하면 시간 끌기 타이머를 다시 센다(m)")][Min(.01f)] public float stallProgress = 1;
        [Tooltip("공연 시작·마지막 환호 뒤 관심 감소 유예(초)")][Min(0)] public float interestGrace = 20;
        [Tooltip("유예 뒤 1초마다 줄어드는 호응도")][Min(0)] public int interestDecay = 1;
        [Tooltip("실제 장애물 판정 캡슐에 더하는 아슬아슬 회피 여유(m)"), Min(.01f)] public float closeDodgeMargin = .3f;
        [Tooltip("장애물 앞 접근 관찰 거리(m)"), Min(.1f)] public float approachDistance = 8;
        [Tooltip("장애물을 지나 통과를 확정하는 거리(m)"), Min(.1f)] public float clearanceDistance = 2;
    }
}
