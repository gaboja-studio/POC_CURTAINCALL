using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 탐색 세팅(세팅 5묶음 중 "탐색"): 백스테이지 탐색·탈출 단계·추격자·탐색 중 부상 패널티 등 쉬는 시간 탐색 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/ExplorationSettings.asset. <see cref="GameSettings.Exploration"/>로 읽는다.
    /// 더미 공간 Task(CC-119)의 조회 입력만 포함한다. 실제 부상 규칙은 SurvivalRules 소유다.
    /// 쉬는 시간 길이·타이머 방식은 공통 세팅(<see cref="BaseGameSettings.Run"/>)에 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Exploration Settings", fileName = "ExplorationSettings")]
    public sealed class ExplorationSettings : ScriptableObject
    {
        [Header("더미 검증 전용 — 실제 신체 상태 아님")]
        [SerializeField, Range(0, 4)] int dummyLostCount;

        public int DummyLostCount => Mathf.Clamp(dummyLostCount, 0, 4);
    }
}
