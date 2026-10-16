using System;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Reward
{
    /// <summary>보상 기획 전 검증용 값. Play 중 Inspector 변경도 다음 이벤트·정산부터 읽는다.</summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Temporary Reward Table")]
    public sealed class TemporaryRewardTable : RewardTable
    {
        [SerializeField, Min(0)] int cheer = 10;
        [SerializeField, Min(0)] int jeer = 5;
        [SerializeField, Min(0)] int difficultyOneCap = 100;
        [SerializeField, Min(0)] int difficultyTwoCap = 150;
        [SerializeField, Min(0)] int difficultyThreeCap = 200;
        [SerializeField, Min(0)] int allSurvivedBonus;
        [SerializeField, Min(0)] int cutBonus;

        public override RewardRules ReadRules(int difficulty) => new(Mathf.Max(0, cheer), Mathf.Max(0, jeer), difficulty switch
        {
            1 => Mathf.Max(0, difficultyOneCap), 2 => Mathf.Max(0, difficultyTwoCap), 3 => Mathf.Max(0, difficultyThreeCap),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
        }, Mathf.Max(0, allSurvivedBonus), Mathf.Max(0, cutBonus));
    }
}
