using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 보상 규칙 파일의 부모(교체형). 매 이벤트·정산 때 현재 값 묶음을 읽는다.
    /// 규칙을 바꿀 때는 코드를 고치지 않고 새 구현 파일을 만들어 묘기 세팅(<see cref="TrickSettings.Reward"/>)에 갈아 끼운다.
    /// 에셋 위치: Assets/Resources/Settings/GameSettings/Tricks/Rewards/
    /// </summary>
    public abstract class RewardTable : ScriptableObject
    {
        public abstract RewardRules ReadRules(int difficulty);
    }
}
