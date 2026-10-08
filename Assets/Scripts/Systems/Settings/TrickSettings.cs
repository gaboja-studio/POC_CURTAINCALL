using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 묘기별 세팅의 공통 부모. 묘기마다 하나(예: <see cref="TightropeSettings"/>)를 Assets/Resources/Settings/GameSettings/Tricks/에 둔다.
    /// 보상 규칙은 교체형으로, <see cref="RewardTable"/>을 구현한 파일을 보상 칸에 끼운다.
    /// </summary>
    public abstract class TrickSettings : ScriptableObject
    {
        [Header("보상")]
        [Tooltip("이 묘기의 보상 규칙 파일(Tricks/Rewards/). 규칙을 바꾸려면 다른 파일을 끼운다. 보상 작업(015) 전에는 비워 둔다.")]
        [SerializeField] RewardTable reward;

        /// <summary>이 묘기의 보상 규칙. 연결 전이면 null.</summary>
        public RewardTable Reward => reward;
    }
}
