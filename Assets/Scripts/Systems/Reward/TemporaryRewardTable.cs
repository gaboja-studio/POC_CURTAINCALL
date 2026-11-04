using System;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Reward
{
    /// <summary>
    /// 단계별 보상 값. Play 중 Inspector 변경도 다음 이벤트·정산부터 읽는다.
    /// 기준: 보상 확정 규칙 — 1인 호응 상한·도착자 완주 보너스 1/2/3단계 100/150/200, 절단 1개당 +10.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Temporary Reward Table")]
    public sealed class TemporaryRewardTable : RewardTable
    {
        [Header("기본 환호·야유")]
        [SerializeField, Min(0), InspectorName("기본 환호 증가량")]
        [Tooltip("기본 환호 1회의 호응도 증가량입니다. 장애물 회피·구조 등 상황별 환호량은 호응도 설정(temporary-audience)에서 따로 조정합니다.")] int cheer = 10;
        [SerializeField, Min(0), InspectorName("기본 야유 감점량")]
        [Tooltip("기본 야유 1회의 호응도 감소량입니다. 양수를 입력합니다. 추락·진행 정체 등 상황별 감점량은 호응도 설정(temporary-audience)에서 따로 조정합니다.")] int jeer = 5;
        [Header("난이도별 개인 호응도 상한")]
        [SerializeField, Min(0), InspectorName("1단계 호응도 상한")]
        [Tooltip("1단계 공연에서 한 사람이 쌓을 수 있는 최대 호응도입니다. 완주 보너스 토큰은 이 상한과 별도로 지급합니다.")] int difficultyOneCap = 100;
        [SerializeField, Min(0), InspectorName("2단계 호응도 상한")]
        [Tooltip("2단계 공연에서 한 사람이 쌓을 수 있는 최대 호응도입니다. 완주 보너스 토큰은 이 상한과 별도로 지급합니다.")] int difficultyTwoCap = 150;
        [SerializeField, Min(0), InspectorName("3단계 호응도 상한")]
        [Tooltip("3단계 공연에서 한 사람이 쌓을 수 있는 최대 호응도입니다. 완주 보너스 토큰은 이 상한과 별도로 지급합니다.")] int difficultyThreeCap = 200;
        [Header("난이도별 완주 보너스")]
        [SerializeField, Min(0), InspectorName("1단계 완주 보너스 (토큰)")]
        [Tooltip("1단계 완주 인정자 한 명당 추가 지급할 토큰입니다. 실제 도착자와 종료 순간 완주 인정 진행률을 충족한 생존자에게 호응 보상과 별도로 지급합니다.")] int difficultyOneCompletion = 100;
        [SerializeField, Min(0), InspectorName("2단계 완주 보너스 (토큰)")]
        [Tooltip("2단계 완주 인정자 한 명당 추가 지급할 토큰입니다. 실제 도착자와 종료 순간 완주 인정 진행률을 충족한 생존자에게 호응 보상과 별도로 지급합니다.")] int difficultyTwoCompletion = 150;
        [SerializeField, Min(0), InspectorName("3단계 완주 보너스 (토큰)")]
        [Tooltip("3단계 완주 인정자 한 명당 추가 지급할 토큰입니다. 실제 도착자와 종료 순간 완주 인정 진행률을 충족한 생존자에게 호응 보상과 별도로 지급합니다.")] int difficultyThreeCompletion = 200;
        [Header("부상 환호")]
        [SerializeField, Min(0), InspectorName("팔다리 절단 환호량 (부위당)")]
        [Tooltip("공연 중 팔다리를 새로 잃을 때 부위 하나당 더할 호응도입니다. 즉시 토큰으로 지급하지 않으며, 같은 부위를 복구한 뒤 다시 잃어도 해당 공연에서는 중복 지급하지 않습니다.")] int cutBonus = 10;

        public override RewardRules ReadRules(int difficulty) => difficulty switch
        {
            1 => Read(difficultyOneCap, difficultyOneCompletion),
            2 => Read(difficultyTwoCap, difficultyTwoCompletion),
            3 => Read(difficultyThreeCap, difficultyThreeCompletion),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
        };

        RewardRules Read(int cap, int completion) => new(Mathf.Max(0, cheer), Mathf.Max(0, jeer), Mathf.Max(0, cap),
            Mathf.Max(0, completion), Mathf.Max(0, cutBonus));
    }
}
