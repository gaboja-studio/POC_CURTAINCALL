using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 대기실 세팅(세팅 5묶음 중 "대기실"): 상점·배송구·소품실·서비스 전문가·밤 정비 등 대기실 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/WaitingRoomSettings.asset. <see cref="GameSettings.WaitingRoom"/>로 읽는다.
    /// 지금은 빈 틀이다. 칸은 더미 공간 Task(CC-119)와 대기실 담당이 PM 배정 순서대로 추가한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Waiting Room Settings", fileName = "WaitingRoomSettings")]
    public sealed class WaitingRoomSettings : ScriptableObject
    {
    }
}
