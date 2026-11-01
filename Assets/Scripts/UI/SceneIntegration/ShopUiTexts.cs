using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 대기실 상점 화면의 바뀌는 문구·아이콘 키(2026-11-01 사용자: 인스펙터에서 고칠 수 있게).
    /// - 고정 문구(창 제목 등)는 상점 프리팹 TMP에서 직접 고치고, 여기에는 값이 들어가는 형식·상황별 문구만 둔다.
    /// - {price}, {n} 같은 이름표는 게임이 실제 값으로 바꾼다. 각 칸의 설명(마우스를 올리면 보임)에 쓸 수 있는 이름표를 적었다.
    ///   조사 자동: {item:을} → 받침에 맞춰 을/를 (대기실 문구 파일과 같은 규칙).
    /// - 빈 칸이면 그 줄을 표시하지 않는다.
    /// - 기물 이름·설명·가격은 기물 목록(ItemCatalog)에서 고친다.
    /// </summary>
    [CreateAssetMenu(menuName = "Curtain Call/UI/Shop UI Texts", fileName = "ShopUiTexts")]
    public sealed class ShopUiTexts : ScriptableObject
    {
        [Header("탭 위 안내줄 (빈 칸이면 숨김)")]
        [Tooltip("{balance} = 공동 자금, {n} = 배송구 대기 개수")]
        public string itemsContext;
        [Tooltip("{balance} = 공동 자금, {day} = 일차")]
        public string servicesContext;
        [Tooltip("{balance} = 공동 자금")]
        public string ledgerContext;

        [Header("창 아래 줄")]
        [Tooltip("선택한 항목을 이용할 수 있을 때. 빈 칸이면 숨김")]
        public string footerIdle;
        [Tooltip("요청을 보내고 답을 기다리는 동안")]
        public string footerBusy;
        [Tooltip("선택한 항목을 이용할 수 없을 때. 빈 칸이면 숨김")]
        public string footerUnavailable;
        [Tooltip("장부 탭에서. 빈 칸이면 숨김")]
        public string footerLedger;
        [Tooltip("응답이 오지 않을 때")]
        public string responseTimeout;
        [Tooltip("창이 닫힌 뒤 요청했을 때")]
        public string reopenWindow;
        [Tooltip("처리할 수 없는 요청")]
        public string invalidRequest;

        [Header("기물 탭")]
        [Tooltip("목록 줄의 둘째 줄")]
        public string itemSubtitle;
        [Tooltip("모든 기물 설명 아래에 붙는 안내. 빈 칸이면 붙이지 않음")]
        [TextArea(1, 4)] public string itemDescriptionFooter;
        [Tooltip("설명 칸의 가격 줄. {price} = 1개 가격")]
        public string itemPriceDetail;
        [Tooltip("설명 칸의 상태 줄. {n} = 배송구 대기 개수")]
        public string itemWaiting;
        [Tooltip("구매 버튼")]
        public string buyButton;
        [Tooltip("공동 자금이 모자랄 때 (기물·서비스 공통)")]
        public string reasonNoFunds;

        [Header("서비스 탭 · 신체 복구")]
        [Tooltip("{n} = 고른 팔·다리 수")]
        public string medicalTitle;
        [Tooltip("{lost} = 잃은 팔·다리 수, {max} = 고를 수 있는 최대 수")]
        public string medicalSubtitle;
        [Tooltip("설명 칸. 줄을 바꾸려면 Enter")]
        [TextArea(2, 6)] public string medicalDescription;
        [Tooltip("설명 칸의 가격 줄. {price} = 잘린 팔·다리 1개당 지금 가격")]
        public string medicalPriceDetail;
        [Tooltip("예약 버튼. {n} = 고른 수")]
        public string bookButton;
        [Tooltip("수 고르기 버튼. {n} = 고른 수, {max} = 최대 수")]
        public string countButton;
        [Tooltip("수 고르기 버튼을 누른 뒤 창 아래 줄. {n} = 고른 수")]
        public string countSelected;
        [Tooltip("서비스 정보를 아직 받지 못했을 때")]
        public string serviceLoading;
        [Tooltip("설명 칸의 상태 줄. {player} = 이용 중인 공연 준비자, {phase} = 진행 단계")]
        public string serviceStatus;

        [Header("장부 탭")]
        [Tooltip("공동 자금 기록 둘째 줄. {number} = 기록 번호, {day} = 일차")]
        public string ledgerSubtitle;
        [Tooltip("{before} = 이전 잔액, {after} = 이후 잔액")]
        public string ledgerBalance;
        public string ledgerSettled;
        [Tooltip("배송구 도착(수령 전) 둘째 줄. {order} = 주문 번호, {player} = 주문한 공연 준비자")]
        public string deliverySubtitle;
        public string deliveryWaiting;
        [TextArea(1, 4)] public string deliveryDescription;
        [Tooltip("{order} = 주문 번호")]
        public string receiptSubtitle;
        public string receiptDone;
        [Tooltip("{buyer} = 주문한 공연 준비자, {claimer} = 받아 간 공연 준비자")]
        public string receiptDescription;

        [Header("목록에 마우스를 올렸을 때 설명")]
        [Tooltip("{description} = 항목 설명, {price} = 가격, {reason} = 이용 불가 사유(없으면 빈 줄)")]
        [TextArea(2, 6)] public string rowTooltip;

        [Header("아이콘 키 (상점 프리팹 ShopPanelView의 Icon Bindings에서 키별 그림을 고침)")]
        [Tooltip("기물 그림이 등록되지 않았을 때 쓰는 키. 기물은 기물 ID와 같은 키가 있으면 그 그림을 씀")]
        public string itemFallbackIcon;
        [Tooltip("기물 설명 칸 상태 줄 아이콘")]
        public string itemStatusIcon;
        public string medicalIcon;
        public string ledgerIcon;
        public string deliveryIcon;
    }
}
