using CurtainCall.Settings;
using UnityEngine;
using TMPro;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>1920×1080 기준 실게임 HUD 배치. 정본 프리팹의 내부 그림은 바꾸지 않는다.</summary>
    public static class HudLayout
    {
        /// <summary>Keep the card's horizontal anchor and clear an overlapping HUD element.</summary>
        public static Vector2 PlaceAbove(RectTransform card, Vector2 position, RectTransform obstacle, float gap)
        {
            if (card.parent == null) return position;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(card.parent, obstacle);
            var pivot = (Vector2)card.localPosition + position - card.anchoredPosition;
            if (pivot.x + card.rect.xMin >= bounds.max.x || pivot.x + card.rect.xMax <= bounds.min.x)
                return position;
            position.y += Mathf.Max(0, bounds.max.y + gap - (pivot.y + card.rect.yMin));
            return position;
        }

        public static void FitBalanceKeys(Transform balance)
        {
            foreach (string path in new[] { "72_KeyTexts/LeftTMP", "72_KeyTexts/RightTMP" })
                if (balance.Find(path)?.GetComponent<TMP_Text>() is TMP_Text label)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMax = 24;
                    label.fontSizeMin = 10;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }
        }
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

        public static void PlaceBanner(RectTransform rect, bool balanceAtTop, float reservedTop = 0)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -Mathf.Max(balanceAtTop ? 140 : 40, reservedTop));
        }
    }
}
