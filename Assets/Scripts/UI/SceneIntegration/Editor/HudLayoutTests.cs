using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class HudLayoutTests
    {
        [Test]
        public void BalanceSwitchesBothWaysAndBannerStaysBelowTopGauge()
        {
            var gauge = new GameObject("Balance", typeof(RectTransform));
            var popup = new GameObject("Banner", typeof(RectTransform));
            try
            {
                var balance = (RectTransform)gauge.transform;
                var banner = (RectTransform)popup.transform;
                balance.sizeDelta = new Vector2(544, 80);
                var track = new GameObject("60_DwellTrack", typeof(RectTransform)).GetComponent<RectTransform>();
                var fill = new GameObject("61_DwellFill", typeof(RectTransform)).GetComponent<RectTransform>();
                track.SetParent(balance, false); fill.SetParent(balance, false);
                track.anchoredPosition = new Vector2(80, 13); track.sizeDelta = new Vector2(384, 23);
                fill.anchoredPosition = new Vector2(84, 9); fill.sizeDelta = new Vector2(376, 15);
                for (int i = 0; i < 2; i++)
                {
                    HudLayout.PlaceBalance(balance, BaseGameSettings.BalanceHudPosition.TopCenter);
                    HudLayout.PlaceBanner(banner, true);
                    Assert.AreEqual(new Vector2(.5f, 1), balance.anchorMin);
                    Assert.AreEqual(balance.anchorMin, balance.anchorMax);
                    Assert.AreEqual(balance.anchorMin, balance.pivot);
                    Assert.AreEqual(new Vector2(0, -40), balance.anchoredPosition);
                    Assert.AreEqual(new Vector2(80, -56), track.anchoredPosition);
                    Assert.AreEqual(new Vector2(84, -60), fill.anchoredPosition);
                    Assert.LessOrEqual(track.anchoredPosition.y, -48 - 8, "균형 바 아래에 8px 간격을 둔다.");
                    Assert.LessOrEqual(banner.anchoredPosition.y, balance.anchoredPosition.y - balance.rect.height - 20);
                    HudLayout.PlaceBalance(balance, BaseGameSettings.BalanceHudPosition.BottomCenter);
                    HudLayout.PlaceBanner(banner, false);
                    Assert.AreEqual(new Vector2(.5f, 0), balance.anchorMin);
                    Assert.AreEqual(balance.anchorMin, balance.anchorMax);
                    Assert.AreEqual(balance.anchorMin, balance.pivot);
                    Assert.AreEqual(new Vector2(0, 28), balance.anchoredPosition);
                    Assert.AreEqual(new Vector2(80, 13), track.anchoredPosition);
                    Assert.AreEqual(new Vector2(84, 9), fill.anchoredPosition);
                    Assert.AreEqual(new Vector2(384, 23), track.sizeDelta);
                    Assert.AreEqual(new Vector2(376, 15), fill.sizeDelta);
                    Assert.AreEqual(new Vector2(0, -40), banner.anchoredPosition);
                    Assert.AreEqual(new Vector2(544, 80), balance.sizeDelta);
                }
            }
            finally { Object.DestroyImmediate(gauge); Object.DestroyImmediate(popup); }
        }

        [Test]
        public void DefaultSettingsUseBottomCenterAndTwoInventorySlots()
        {
            var settings = ScriptableObject.CreateInstance<BaseGameSettings>();
            try
            {
                Assert.AreEqual(BaseGameSettings.BalanceHudPosition.BottomCenter, settings.HudLayout.BalancePosition);
                Assert.AreEqual(2, CommonInventoryUI.VisibleSlotCount);
                Assert.AreEqual(CommonInventoryUI.VisibleSlotCount, settings.ItemCarry.BaseSlots);
            }
            finally { Object.DestroyImmediate(settings); }
        }
        [Test]
        public void TimerReservesBannerSpaceAndOtherScenesKeepTheirPosition()
        {
            var popup = new GameObject("Banner", typeof(RectTransform));
            try
            {
                var banner = (RectTransform)popup.transform;
                HudLayout.PlaceBanner(banner, false, 244);
                Assert.AreEqual(new Vector2(0, -244), banner.anchoredPosition);
                HudLayout.PlaceBanner(banner, true, 244);
                Assert.AreEqual(new Vector2(0, -244), banner.anchoredPosition);
                HudLayout.PlaceBanner(banner, true);
                Assert.AreEqual(new Vector2(0, -140), banner.anchoredPosition);
            }
            finally { Object.DestroyImmediate(popup); }
        }
    }
}
