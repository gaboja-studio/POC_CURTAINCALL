using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 탈출로 세팅(세팅 5묶음 중 "탈출로"): 탈출로 구간·추격·리타이어·엔딩 등 탈출로 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset. <see cref="GameSettings.EscapeRoute"/>로 읽는다.
    /// 더미 공간 Task(CC-119)의 입력 행 수만 포함한다. 실제 참가 인원·집계는 정의하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Escape Route Settings", fileName = "EscapeRouteSettings")]
    public sealed class EscapeRouteSettings : ScriptableObject
    {
        [Header("더미 검증 전용 — 실제 참가 인원 아님")]
        [SerializeField, Range(1, 4)] int dummyInputRows = 2;

        public int DummyInputRows => Mathf.Clamp(dummyInputRows, 1, 4);
    }
}
