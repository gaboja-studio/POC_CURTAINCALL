using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// Task-092 상점 문구(2026-11-01 사용자):
    /// - 문구는 코드가 아니라 설정에서 온다: 창 제목은 프리팹 TMP, 바뀌는 문구는 ShopUiTexts 에셋, 기물 설명은 기물 목록.
    /// - 가격 줄은 가격만("개당 n 토큰", "잘린 팔·다리 한 개당 n 토큰"), 신체 복구 설명 첫 줄은 가격 인상 주의, 수 고르기 버튼은 "팔·다리 n/m".
    /// - 탭 위 안내줄과 창 아래 고정 안내("설명과 이용 불가 사유…", "기록의 상세 내용…")는 보이지 않는다.
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator W2_ShopWordingComesFromSettingsAndShowsPriceOnly()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            SeedShopFunds(500);
            yield return OpenShop();
            var adapter = (Component)Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var view = (Component)Get(adapter, "View");
            var texts = (ScriptableObject)Get(adapter, "Texts");
            Assert.IsNotNull(texts, "상점 문구 설정 에셋이 씬에 연결됨");
            var tabType = T("CurtainCall.UI.ShopV1.ShopTab");
            var titleText = (TMP_Text)view.GetType().GetField("title", Hidden).GetValue(view);
            Assert.AreEqual("상점", titleText.text, "창 제목은 프리팹(씬 인스턴스) TMP에 적힌 글");
            var model = Call(adapter, "Build");
            Assert.AreEqual(titleText.text, Get(model, "Title"));
            foreach (var tab in Enum.GetValues(tabType))
                Assert.AreEqual("", Call(model, "GetContext", tab), "탭 위 안내줄 없음: " + tab);

            var item = Entry(adapter, "item:spring-shoes");
            Assert.AreEqual("개당 " + Get(item, "PriceText") + " 토큰", Get(item, "PriceDetail"), "기물 가격 줄은 가격만");
            var shoes = Static("CurtainCall.WaitingRoom.ItemCatalog", "Current");
            StringAssert.StartsWith((string)Call(shoes, "Description", "spring-shoes"), (string)Get(item, "Description"), "기물 설명은 기물 목록에서");

            var medical = Entry(adapter, "medical");
            StringAssert.StartsWith("잘린 팔·다리 한 개당 ", (string)Get(medical, "PriceDetail"));
            StringAssert.EndsWith(" 토큰", (string)Get(medical, "PriceDetail"));
            StringAssert.DoesNotContain("팀 이용", (string)Get(medical, "PriceDetail"));
            StringAssert.StartsWith("주의: 서비스 이용 시마다 이용 가격이 인상됩니다.\n", (string)Get(medical, "Description"));
            var count = ((IEnumerable)Get(medical, "Actions")).Cast<object>().Single(a => (string)Get(a, "Id") == "count");
            StringAssert.StartsWith("팔·다리 ", (string)Get(count, "Label"));

            // 설정을 바꾸면 화면 문구가 바뀐다(에셋 원본은 건드리지 않도록 복사본으로 확인).
            var field = adapter.GetType().GetField("texts", Hidden);
            var copy = UnityEngine.Object.Instantiate(texts);
            copy.GetType().GetField("itemPriceDetail").SetValue(copy, "시험 {price}");
            field.SetValue(adapter, copy);
            try { Assert.AreEqual("시험 " + Get(Entry(adapter, "item:spring-shoes"), "PriceText"), Get(Entry(adapter, "item:spring-shoes"), "PriceDetail")); }
            finally { field.SetValue(adapter, texts); UnityEngine.Object.Destroy(copy); }

            var footer = (TMP_Text)view.GetType().GetField("footerMessage", Hidden).GetValue(view);
            var context = (TMP_Text)view.GetType().GetField("context", Hidden).GetValue(view);
            Call(view, "SelectTab", Enum.Parse(tabType, "Services"));
            Call(view, "SelectEntry", "medical"); yield return null; yield return null;
            Assert.IsFalse((bool)Get(Entry(adapter, "medical"), "Available"), "잃은 신체가 없어 이용 불가인 항목");
            Assert.AreEqual("", footer.text, "이용 불가 항목에도 아래 고정 안내 없음");
            Assert.AreEqual("", context.text, "서비스 탭 위 안내줄 없음");

            Call(view, "SelectTab", Enum.Parse(tabType, "Ledger"));
            var ledger = Entries(adapter).First(e => Get(e, "Tab").ToString() == "Ledger");
            Call(view, "SelectEntry", (string)Get(ledger, "Id")); yield return null; yield return null;
            Assert.AreEqual("", footer.text, "장부 아래 고정 안내 없음");
            Assert.AreEqual("", context.text, "장부 탭 위 안내줄 없음");
            Call(view, "Close"); yield return null;
        }
    }
}
