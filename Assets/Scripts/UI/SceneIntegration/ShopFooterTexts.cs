using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 상점 창 아래 줄의 상황별 기본 문구를 <see cref="ShopUiTexts"/>에서 고칠 수 있게 한다(2026-11-01 사용자).
    /// 상점 키트(_ThirdParty)의 ShopPanelView가 이 문구들을 코드에 직접 적어 두었으므로, 키트는 그대로 두고
    /// 화면에 그 문구가 나온 뒤 같은 상황의 설정 문구로 바꾼다. 요청 결과·오류 메시지는 그대로 둔다.
    /// </summary>
    public sealed class ShopFooterTexts : MonoBehaviour
    {
        // 키트가 쓰는 원래 문구(ShopPanelView.RefreshDetails). 키트가 바뀌면 여기도 맞춘다.
        const string KitIdle = "플레이는 계속됩니다", KitBusy = "요청 처리 중…",
            KitLedger = "기록의 상세 내용을 확인할 수 있습니다.", KitUnavailable = "설명과 이용 불가 사유를 확인하세요.";

        [SerializeField] ShopUiTexts texts;
        [Tooltip("상점 창 아래 줄 TMP(ShopPanelView의 Footer Message)")]
        [SerializeField] TMP_Text footer;

        void LateUpdate()
        {
            if (texts == null || footer == null) return;
            string replaced = footer.text switch
            {
                KitIdle => texts.footerIdle,
                KitBusy => texts.footerBusy,
                KitLedger => texts.footerLedger,
                KitUnavailable => texts.footerUnavailable,
                _ => null,
            };
            if (replaced != null && replaced != footer.text) footer.text = replaced;
        }
    }
}
