using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 공연 환호·야유·관심 감소 값. 기준: 보상 확정 규칙(reward-rules.md, Confluence 27459631).
    /// 일반 회피 +5, 대포 플랫폼·그네 건너편 착지 +10, 협동 양쪽 각 +10. 기존 야유·관심 감소도 여기서 조정한다.
    /// 절단 보너스·상한·완주 보너스는 단계별 보상 표(<see cref="RewardTable"/>)가 가진다. 거리 기준은 플레이 테스트 후 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Curtain Call/Settings/Audience reactions")]
    public sealed class AudienceReactionSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/GameSettings/Tricks/Rewards/temporary-audience";
        static AudienceReactionSettings cached;
        // 매 프레임 동기 Resources.Load 금지: 공연 배경 비동기 로드 중이면 그 로드가 끝날 때까지 메인 스레드가 멈춰 Relay가 끊긴다(Task-20261105-108).
        public static AudienceReactionSettings Current => cached != null ? cached : cached = Resources.Load<AudienceReactionSettings>(ResourcePath);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Preload() => _ = Current;
        [Header("단계별 행동 환호 배율")]
        [InspectorName("1단계 환호 배율")]
        [Tooltip("1단계의 장애물 회피·협동·기물·플랫폼·그네·환호 포인트 등 행동 환호에 곱할 값입니다. 2이면 기본 +5가 +10이 됩니다. 야유·관심 감소·절단 보너스·호응도 상한·완주 보너스에는 적용하지 않습니다.")]
        [Min(0f)] public float difficultyOneCheerMultiplier = 2f;
        [InspectorName("2단계 환호 배율")]
        [Tooltip("2단계 행동 환호에 곱할 값입니다. 1.5이면 기본 +5는 +8, +10은 +15가 됩니다. 소수점은 반올림합니다. 야유·관심 감소·절단 보너스·호응도 상한·완주 보너스에는 적용하지 않습니다.")]
        [Min(0f)] public float difficultyTwoCheerMultiplier = 1.5f;
        [InspectorName("3단계 환호 배율")]
        [Tooltip("3단계 행동 환호에 곱할 값입니다. 1이면 기본 환호량을 그대로 지급합니다. 야유·관심 감소·절단 보너스·호응도 상한·완주 보너스에는 적용하지 않습니다.")]
        [Min(0f)] public float difficultyThreeCheerMultiplier = 1f;

        public int ScaleReaction(int amount, int difficulty)
        {
            if (amount <= 0) return amount;
            float multiplier = difficulty switch
            {
                1 => difficultyOneCheerMultiplier,
                2 => difficultyTwoCheerMultiplier,
                3 => difficultyThreeCheerMultiplier,
                _ => 1f
            };
            if (!float.IsFinite(multiplier)) multiplier = 1f;
            return (int)System.Math.Min(int.MaxValue,
                System.Math.Floor((double)amount * Mathf.Max(0f, multiplier) + .5d));
        }

        [Header("완주·추가 환호")]
        // 이전 에셋 호환용. 성공 시 모든 생존자를 완주 처리하므로 진행률 제한은 더 이상 사용하지 않는다.
        [HideInInspector] public float completionProgress = .8f;
        [InspectorName("장애물 통과 환호량")]
        [Tooltip("피격 없이 장애물 지역을 통과한 사람에게 더할 호응도입니다. 아슬아슬하게 피할 필요는 없습니다. 같은 장애물을 다시 지나도 추가 지급하지 않으며, 근접 회피와 겹치면 두 환호량 중 큰 값만 적용합니다.")][Min(0)] public int obstacleDodge = 5;
        [InspectorName("대포 플랫폼 첫 도달 환호량")]
        [Tooltip("대포가 있는 플랫폼에 처음 도달한 사람에게 더할 호응도입니다. 출발 플랫폼도 포함합니다. 한 공연에서 사람마다 플랫폼별 1회만 지급하며, 같은 플랫폼의 다른 대포로 이동하거나 재진입해도 추가 지급하지 않습니다.")][Min(0)] public int cannonPlatform = 10;
        [InspectorName("그네 건너편 착지 환호량")]
        [Tooltip("그네로 건너편 줄에 정상 착지한 사람에게 더할 호응도입니다. 한 공연에서 사람마다 같은 그네 구간은 1회만 지급합니다. 착지 실패나 같은 구간 재도전에는 추가 지급하지 않습니다.")][Min(0)] public int swingLanding = 10;
        public static bool HasCompletionProgress(float distance, float length)
        {
            if (!float.IsFinite(distance) || !float.IsFinite(length) || length <= 0f) return false;
            // 실제 도착으로 성공이 확정된 뒤에만 호출한다. 출발점·후진한 생존자도 완주 대상이다.
            return true;
        }

        [Header("기존 환호·야유")]
        [InspectorName("협동·구조 성공 환호량")]
        [Tooltip("동료 구조·받기 등 협동이 실제로 성공했을 때 참여한 두 사람에게 각각 더할 호응도입니다. 구조·받기는 성공할 때마다 지급하며, 시작하거나 기다리는 것만으로는 지급하지 않습니다.")][Min(0)] public int cooperation = 10;
        [InspectorName("기물 첫 성공 환호량")]
        [Tooltip("트램펄린 등 기물의 동작에 처음 성공했을 때 더할 호응도입니다. 같은 공연에서 중복 지급하지 않습니다. 협동 성공으로 인정되면 이 값 대신 협동 환호량을 적용합니다.")][Min(0)] public int objectFirst = 5;
        [InspectorName("아슬아슬 회피 환호량")]
        [Tooltip("장애물에 가까이 접근했다가 피격 없이 벗어났을 때 더할 호응도입니다. 일반 통과 환호와 합산하지 않고 두 값 중 큰 값만 적용합니다.")][Min(0)] public int closeDodge = 5;
        [InspectorName("추락 야유 감점량")]
        [Tooltip("한 번 추락할 때 뺄 호응도입니다. 양수를 입력합니다. 외줄에서는 매달림이 시작될 때 적용하며, 그 뒤 낙사해도 같은 추락으로 다시 차감하지 않습니다.")][Min(0)] public int fallJeer = 15;
        [InspectorName("진행 정체 야유 감점량")]
        [Tooltip("진행할 수 있는데도 앞으로 나아가지 않을 때 반복해서 뺄 호응도입니다. 감점 간격은 아래 대기 시간으로 정합니다. 구조·동료 받기 대기·강제 이동 등은 정체 판정에서 제외합니다.")][Min(0)] public int stallJeer = 10;
        [InspectorName("진행 정체 감점 간격 (초)")]
        [Tooltip("진행이 없을 때 야유가 발생하는 간격입니다. 15이면 정체가 15초 계속될 때마다 진행 정체 감점량을 차감합니다.")][Min(1)] public float stallSeconds = 15;
        [InspectorName("정체 해제에 필요한 전진 거리 (m)")]
        [Tooltip("이 거리 이상 앞으로 진행하면 정체 대기 시간을 처음부터 다시 셉니다. 1이면 1m 이상 전진해야 초기화됩니다. 뒤로 갔다가 같은 자리로 돌아오는 것만으로는 초기화되지 않습니다.")][Min(.01f)] public float stallProgress = 1;
        [InspectorName("관심 감소 유예 시간 (초)")]
        [Tooltip("공연 시작 또는 본인의 마지막 환호 이후, 호응도가 자연 감소하기 전까지 기다리는 시간입니다. 동료가 환호를 받아도 본인의 대기 시간은 초기화되지 않습니다.")][Min(0)] public float interestGrace = 20;
        [InspectorName("관심 감소량 (초당)")]
        [Tooltip("관심 감소 유예 시간이 지난 뒤 1초마다 뺄 호응도입니다. 0이면 자연 감소를 끕니다. 호응도는 0 아래로 내려가지 않으며, 완주·사망 후에는 감소하지 않습니다.")][Min(0)] public int interestDecay = 1;
        [InspectorName("아슬아슬 회피 판정 여유 (m)")]
        [Tooltip("실제 몸 충돌 범위 바깥으로 이 거리만큼 넓혀 근접 회피를 판정합니다. 값이 클수록 아슬아슬 회피로 인정하는 범위가 넓어집니다. 실제 피격 범위는 바뀌지 않습니다."), Min(.01f)] public float closeDodgeMargin = .3f;
        [InspectorName("장애물 앞 관찰 시작 거리 (m)")]
        [Tooltip("장애물에 이 거리 안으로 접근하면 회피 관찰을 시작합니다. 관찰 중 피격·부상이 있었는지 확인해 통과 환호 여부를 판단합니다."), Min(.1f)] public float approachDistance = 8;
        [InspectorName("장애물 뒤 통과 확정 거리 (m)")]
        [Tooltip("장애물을 지난 뒤 이 거리 이상 벗어나면 일반 통과를 확정합니다. 예를 들어 2이면 장애물 뒤 2m에 도달했을 때 통과 환호를 판정합니다."), Min(.1f)] public float clearanceDistance = 2;
    }
}
