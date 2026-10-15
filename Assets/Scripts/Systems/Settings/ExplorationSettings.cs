using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 탐색 세팅(세팅 5묶음 중 "탐색"): 백스테이지 탐색·탈출 단계·추격자·탐색 중 부상 패널티 등 쉬는 시간 탐색 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/ExplorationSettings.asset. <see cref="GameSettings.Exploration"/>로 읽는다.
    /// 지금은 빈 틀이다. 칸은 더미 공간 Task(CC-119)와 백스테이지 담당이 PM 배정 순서대로 추가한다.
    /// 쉬는 시간 길이·타이머 방식은 공통 세팅(<see cref="BaseGameSettings.Run"/>)에 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Exploration Settings", fileName = "ExplorationSettings")]
    public sealed class ExplorationSettings : ScriptableObject
    {
    }
}
