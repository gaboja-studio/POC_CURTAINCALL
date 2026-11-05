using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 대기실 세팅(세팅 5묶음 중 "대기실"): 상점·배송구·소품실·서비스 전문가·밤 정비 등 대기실 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/WaitingRoomSettings.asset. <see cref="GameSettings.WaitingRoom"/>로 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Waiting Room Settings", fileName = "WaitingRoomSettings")]
    public sealed class WaitingRoomSettings : ScriptableObject
    {
        [Header("더미 검증 전용 — 정식 상품 가격 아님")]
        [SerializeField, Min(1)] int dummySpendAmount = 10;

        [Header("기물 구매 가능 시간")]
        [Tooltip("켜면 아침·낮·첫날 가이드 쉬는 시간에도 기물을 구매할 수 있습니다. 밤 정비에는 항상 구매할 수 있습니다.")]
        [SerializeField, InspectorName("쉬는 시간에도 기물 구매")] bool allowPurchasesDuringRest = true;
        public bool AllowPurchasesDuringRest => allowPurchasesDuringRest;

        [Header("신체 복구")]
        [Tooltip("팔다리 1개당 기본 가격(토큰). 런(세대)이 새로 시작되면 이 값으로 돌아간다.")]
        [SerializeField, Min(1), InspectorName("최초 부위당 가격")] int bodyRestoreBasePrice = 50;
        [Tooltip("팀 전체가 한 번 이용할 때마다 오르는 다음 1개당 가격(토큰). 여러 개를 고쳐도 한 번만 오른다.")]
        [SerializeField, Min(0), InspectorName("이용 1회당 단가 상승")] int bodyRestorePriceStep = 25;
        [Tooltip("한 번에 복구할 수 있는 최대 팔다리 수.")]
        [SerializeField, Range(1, 4)] int bodyRestoreMaxParts = 3;

        [Header("추격 시스템 (M2)")]
        [SerializeField] PursuitSettings pursuit = new();
        public PursuitSettings Pursuit => pursuit ??= new();

        public int DummySpendAmount => Mathf.Max(1, dummySpendAmount);
        public int BodyRestoreBasePrice => Mathf.Max(1, bodyRestoreBasePrice);
        public int BodyRestorePriceStep => Mathf.Max(0, bodyRestorePriceStep);
        public int BodyRestoreMaxParts => Mathf.Clamp(bodyRestoreMaxParts, 1, 4);
    }
}
