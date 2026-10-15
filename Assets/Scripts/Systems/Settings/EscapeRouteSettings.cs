using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 탈출로 세팅(세팅 5묶음 중 "탈출로"): 탈출로 구간·추격·리타이어·엔딩 등 탈출로 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset. <see cref="GameSettings.EscapeRoute"/>로 읽는다.
    /// 지금은 빈 틀이다. 칸은 더미 공간 Task(CC-119)와 탈출로 담당이 PM 배정 순서대로 추가한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Escape Route Settings", fileName = "EscapeRouteSettings")]
    public sealed class EscapeRouteSettings : ScriptableObject
    {
    }
}
