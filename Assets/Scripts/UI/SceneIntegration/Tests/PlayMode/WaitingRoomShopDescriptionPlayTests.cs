using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// Task-092 상점 후속(2026-11-01 사용자): 기물 설명은 "…"으로 자르지 않고 위아래 스크롤(스크롤 바 없음, 넘친 줄 반만 보임).
    /// 스크롤된다는 피드백: 더 읽을 쪽 가장자리 흐림, 아래 화살표, 처음 고를 때 살짝 내려갔다 돌아오기.
    /// 구매해 배송구에 나온 기물은 노란 상자가 아니라 기물 모델로 보인다.
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator W2_ShopDescriptionScrollsWithFeedbackAndDeliveryShowsItemModel()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            SeedShopFunds(500);
            yield return OpenShop();
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var view = (Component)Get(adapter, "View");
            Call(view, "SelectEntry", "item:spring-shoes");
            yield return null; yield return null;

            var scroll = view.GetComponentInChildren(T("CurtainCall.UI.SceneIntegration.ShopDescriptionScroll"), true);
            Assert.IsNotNull(scroll, "설명 스크롤 영역(프리팹에 배치)");
            var rect = scroll.GetComponent<ScrollRect>();
            Assert.IsTrue(rect.vertical); Assert.IsFalse(rect.horizontal);
            Assert.IsNull(rect.verticalScrollbar, "스크롤 바 없음");
            var label = (TMP_Text)rect.content.GetComponent(typeof(TMP_Text));
            StringAssert.Contains("다칠 바닥", label.text);
            Assert.AreNotEqual(TextOverflowModes.Ellipsis, label.overflowMode, "…으로 자르지 않는다");
            Assert.AreEqual(true, Get(scroll, "Overflows"), "긴 설명은 영역을 넘친다(스크롤 대상)");
            float viewport = ((RectTransform)rect.viewport).rect.height;
            Assert.Greater(viewport, 80f, "원래 80px보다 위아래로 늘어났다");
            float line = label.font.faceInfo.lineHeight / label.font.faceInfo.pointSize * label.fontSize;
            float remainder = viewport / line - Mathf.Floor(viewport / line);
            Assert.That(remainder, Is.InRange(.4f, .6f), "넘친 다음 줄이 반만 보이는 높이");

            // 스크롤 피드백: 처음 고르면 살짝 내려갔다 돌아오고, 아래 가장자리는 흐리고, 아래 화살표가 보인다.
            Assert.AreEqual(true, Get(scroll, "NudgePending"), "넘치는 설명을 고르면 살짝 내려 보이기 예정");
            float peak = 0f, nudgeUntil = Time.realtimeSinceStartup + 2f;
            while ((bool)Get(scroll, "NudgePending") && Time.realtimeSinceStartup < nudgeUntil) { peak = Mathf.Max(peak, rect.content.anchoredPosition.y); yield return null; }
            Assert.Greater(peak, 8f, "살짝 내려갔다");
            Assert.Less(rect.content.anchoredPosition.y, 1f, "다시 맨 위로 돌아왔다");
            yield return new WaitForSecondsRealtime(.4f);
            var mask = scroll.GetComponent<RectMask2D>();
            var more = (CanvasGroup)scroll.GetType().GetField("moreIndicator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(scroll);
            Assert.IsNotNull(more, "아래 화살표가 프리팹에 배치됨");
            Assert.Greater(mask.softness.y, 0, "가장자리 흐림 사용");
            Assert.AreEqual(0f, mask.padding.y, .5f, "아래로 더 읽을 글이 있으면 아래 가장자리가 흐리다");
            Assert.Less(mask.padding.w, -1f, "맨 위에서는 위 가장자리를 흐리지 않는다");
            Assert.Greater(more.alpha, .9f, "아래 화살표가 보인다");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/shop-description-scroll.png"));
            yield return null; yield return null;
            rect.verticalNormalizedPosition = 0f; yield return null;
            Assert.Less(rect.verticalNormalizedPosition, .05f, "끝까지 내려 볼 수 있다");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.Less(more.alpha, .1f, "끝까지 내리면 화살표가 사라진다");
            Assert.Less(mask.padding.y, -1f, "끝에서는 아래 가장자리를 흐리지 않는다");
            Assert.AreEqual(0f, mask.padding.w, .5f, "위로 지나간 글이 있으면 위 가장자리가 흐리다");
            Call(view, "SelectEntry", "item:magician-hairpin"); yield return null; yield return null;
            Assert.Greater(rect.verticalNormalizedPosition, .95f, "다른 항목을 고르면 맨 위부터");

            var shop = Find("CurtainCall.WaitingRoom.WaitingRoomShop");
            Call(shop, "RequestPurchase", "spring-shoes");
            // 배송구가 실제로 보이는 물건(저장하지 않는 표시 전용 오브젝트라 전체 검색 대신 배송구 목록을 읽는다).
            var slot = (Component)Object.FindObjectsByType(T("CurtainCall.WaitingRoom.ShopDeliverySlot"), FindObjectsSortMode.None).Single();
            var shown = (IDictionary)slot.GetType().GetField("shown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(slot);
            float until = Time.realtimeSinceStartup + 4f; GameObject delivered = null;
            while (delivered == null && Time.realtimeSinceStartup < until)
            {
                foreach (var item in shown.Values)
                {
                    var model = (GameObject)item.GetType().GetField("View").GetValue(item);
                    if (model != null && model.name.EndsWith("_spring-shoes")) delivered = model;
                }
                yield return null;
            }
            Assert.IsNotNull(delivered, "배송구에 구매한 기물이 나온다. 대기 " + ((IEnumerable)Get(shop, "Waiting")).Cast<object>().Count() + " · 표시 " + shown.Count);
            yield return new WaitForSecondsRealtime(.8f);
            Call(view, "Close"); yield return null;
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = delivered.transform.position + new Vector3(0f, .9f, -1.2f);
                camera.transform.LookAt(delivered.transform.position);
            }
            yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/shop-delivery-model.png"));
            yield return null; yield return null;
            var filter = delivered.GetComponent<MeshFilter>();
            Assert.IsFalse(filter != null && filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("Cube"), "노란 임시 상자가 아니다");
            Assert.Greater(delivered.GetComponentsInChildren<Renderer>().Length, 0, "기물 모델이 보인다");
        }
    }
}
