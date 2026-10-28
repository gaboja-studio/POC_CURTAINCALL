using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>1920×1080 기준 실게임 HUD 배치. 정본 프리팹의 내부 그림은 바꾸지 않는다.</summary>
    public static class HudLayout
    {
        public static void PlaceBalance(RectTransform rect, BaseGameSettings.BalanceHudPosition position)
        {
            bool top = position == BaseGameSettings.BalanceHudPosition.TopCenter;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, top ? 1 : 0);
            rect.anchoredPosition = new Vector2(0, top ? -40 : 28);
            // 하단 배치는 추락 게이지가 위, 상단 배치는 아래에 오도록 순서를 바꾼다.
            PlaceFallGauge(rect.Find("60_DwellTrack") as RectTransform, top ? -56 : 13);
            PlaceFallGauge(rect.Find("61_DwellFill") as RectTransform, top ? -60 : 9);
        }

        static void PlaceFallGauge(RectTransform rect, float y)
        {
            if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        public static void PlaceBanner(RectTransform rect, bool balanceAtTop)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, balanceAtTop ? -140 : -40);
        }
    }
}
