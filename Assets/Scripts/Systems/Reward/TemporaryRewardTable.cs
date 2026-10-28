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
        [SerializeField, Min(0)] int cheer = 10;
        [SerializeField, Min(0)] int jeer = 5;
        [SerializeField, Min(0)] int difficultyOneCap = 100;
        [SerializeField, Min(0)] int difficultyTwoCap = 150;
        [SerializeField, Min(0)] int difficultyThreeCap = 200;
        [SerializeField, Min(0)] int difficultyOneCompletion = 100;
        [SerializeField, Min(0)] int difficultyTwoCompletion = 150;
        [SerializeField, Min(0)] int difficultyThreeCompletion = 200;
        [SerializeField, Min(0)] int cutBonus = 10;

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
