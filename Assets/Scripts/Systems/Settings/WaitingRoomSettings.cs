using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 대기실 세팅(세팅 5묶음 중 "대기실"): 상점·배송구·소품실·서비스 전문가·밤 정비 등 대기실 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/WaitingRoomSettings.asset. <see cref="GameSettings.WaitingRoom"/>로 읽는다.
    /// 더미 공간 Task(CC-119)의 검증 입력만 포함한다. 실제 공간 정책·가격은 별도 연결한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Waiting Room Settings", fileName = "WaitingRoomSettings")]
    public sealed class WaitingRoomSettings : ScriptableObject
    {
        [Header("더미 검증 전용 — 정식 상품 가격 아님")]
        [SerializeField, Min(1)] int dummySpendAmount = 10;

        public int DummySpendAmount => Mathf.Max(1, dummySpendAmount);
    }
}
