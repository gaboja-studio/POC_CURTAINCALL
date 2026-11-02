using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// Task-097(2026-11-02 사용자): 상점 기물 툴팁은 기물 목록의 툴팁 전용 칸(shopTooltip)을 쓰고,
    /// 비어 있으면 설명 칸과 같은 설명을 쓴다. 서비스·장부 줄은 그대로 설명을 쓴다.
    /// </summary>
    public sealed class ShopItemTooltipPlayTests
    {
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

        static object Entry(string id, string description)
        {
            var type = T("CurtainCall.UI.ShopV1.ShopEntryPresentation");
            var tab = Enum.ToObject(T("CurtainCall.UI.ShopV1.ShopTab"), 0);
            var actions = Array.CreateInstance(T("CurtainCall.UI.ShopV1.ShopActionPresentation"), 0);
            return Activator.CreateInstance(type, id, tab, "", "제목", "", "10", description, "", true, "", "", "", actions);
        }

        static string Tooltip(object entry) => (string)T("CurtainCall.UI.SceneIntegration.PanelHoverTooltip")
            .GetMethod("TooltipDescription", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { entry });

        [Test]
        public void ItemRowTooltipUsesDedicatedTextAndFallsBackToDescription()
        {
            // 플레이 중 ItemCatalog.Current는 에셋 복사본이라 원본 에셋은 바뀌지 않는다.
            var catalog = T("CurtainCall.WaitingRoom.ItemCatalog");
            var item = catalog.GetMethod("Get").Invoke(null, new object[] { "spring-shoes" });
            Assert.IsNotNull(item, "기물 목록에 spring-shoes가 있다");
            var field = item.GetType().GetField("shopTooltip");
            var previous = field.GetValue(item);
            try
            {
                field.SetValue(item, "툴팁 전용 문구");
                Assert.AreEqual("툴팁 전용 문구", Tooltip(Entry("item:spring-shoes", "설명 칸 문구")));
                field.SetValue(item, "");
                Assert.AreEqual("설명 칸 문구", Tooltip(Entry("item:spring-shoes", "설명 칸 문구")), "비우면 설명 칸 문구");
                Assert.AreEqual("서비스 설명", Tooltip(Entry("service:medical", "서비스 설명")), "기물이 아닌 줄은 설명 그대로");
                Assert.AreEqual("설명", Tooltip(Entry("item:no-such-item", "설명")), "목록에 없는 기물은 설명 그대로");
            }
            finally { field.SetValue(item, previous); }
        }

        /// <summary>상점에 파는 기물 설명·툴팁은 같은 형식이다(2026-11-02 사용자: 단답형, 형식 통일).</summary>
        [Test]
        public void SoldItemsShareDescriptionAndTooltipTemplate()
        {
            var catalog = T("CurtainCall.WaitingRoom.ItemCatalog");
            var current = catalog.GetProperty("Current").GetValue(null);
            var items = (System.Collections.IEnumerable)catalog.GetProperty("Items").GetValue(current);
            int checkedCount = 0;
            foreach (var e in items)
            {
                var type = e.GetType();
                if ((int)type.GetField("price").GetValue(e) <= 0) continue;
                string id = (string)type.GetField("id").GetValue(e);
                string description = (string)type.GetField("shopDescription").GetValue(e);
                string tooltip = (string)type.GetField("shopTooltip").GetValue(e);
                // 설명: 습니다체 문장 한 줄 · 빈 줄 · 적용(공연)/사용/내구도 · 소모 한 줄(2026-11-02 사용자).
                var lines = description.Split('\n');
                Assert.AreEqual(6, lines.Length, id + " 설명: 문장·빈 줄·적용·사용·내구도·소모");
                StringAssert.EndsWith("다.", lines[0], id + " 설명 문장은 습니다체");
                Assert.IsEmpty(lines[1], id);
                StringAssert.StartsWith("• 적용: ", lines[2], id);
                StringAssert.StartsWith("• 사용: ", lines[3], id);
                StringAssert.StartsWith("• 내구도: ", lines[4], id);
                StringAssert.StartsWith("• ", lines[5], id);
                StringAssert.Contains("내구도", lines[5], id + " 마지막 줄은 내구도 소모 조건");
                StringAssert.DoesNotContain("소모:", description, id + " '소모:' 이름표는 쓰지 않는다");
                var tip = tooltip.Split('\n');
                Assert.AreEqual(2, tip.Length, id + " 툴팁: 문장 한 줄·사용/내구도 한 줄");
                StringAssert.EndsWith("다.", tip[0], id);
                StringAssert.StartsWith("사용: ", tip[1], id);
                StringAssert.Contains(" · 내구도: ", tip[1], id);
                StringAssert.DoesNotContain("가격", tooltip, id + " 가격은 상점 문구(Row Tooltip)가 붙인다");
                checkedCount++;
            }
            Assert.AreEqual(7, checkedCount, "판매 기물 7종");
        }
    }
}
