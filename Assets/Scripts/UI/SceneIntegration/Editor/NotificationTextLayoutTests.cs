using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class NotificationTextLayoutTests
    {
        [TestCase("SystemBanner", "C8_Banner", "60_TitleTMP")]
        [TestCase("Toast", "Toast", "60_MessageTMP")]
        public void LongMessageGrowsDownwardAndShortMessageRestoresHeight(string prefab, string viewName, string labelName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Notifications/" + prefab + ".prefab");
            var instance = Object.Instantiate(asset);
            try
            {
                RectTransform panel = null;
                foreach (var rect in instance.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == viewName) panel = rect;
                Assert.NotNull(panel);
                var label = panel.Find("Motion/" + labelName).GetComponent<TMP_Text>();
                // EditMode에서는 TMP의 Awake가 설정하는 UI 투영을 직접 지정한다.
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true)) text.isOrthographic = true;
                var layout = panel.gameObject.AddComponent<NotificationTextLayout>();
                label.text = "안내";
                layout.RefreshLayout();
                float shortHeight = panel.rect.height;
                var top = Top(panel);
                label.text = new string('가', 100);
                layout.RefreshLayout();
                Assert.Greater(panel.rect.height, shortHeight);
                Assert.AreEqual(top, Top(panel));
                Assert.AreEqual(2, label.maxVisibleLines);
                Assert.AreEqual(TextWrappingModes.Normal, label.textWrappingMode);
                label.text = "안내";
                layout.RefreshLayout();
                Assert.AreEqual(shortHeight, panel.rect.height);
                Assert.AreEqual(top, Top(panel));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void InteractionWidensForLongTextWrapsToThreeLinesAndRestoresShortSize()
        {
            // 2026-11-01 사용자: [F] 안내(도구 힌트 등)가 잘리지 않게. 넓힐 수 있으면 넓히고(최대 640), 그래도 길면 3줄까지.
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab");
            var instance = Object.Instantiate(asset);
            try
            {
                var view = instance.GetComponentInChildren<CurtainCall.UI.InteractionV1.InteractionPromptView>(true);
                var panel = (RectTransform)view.transform;
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true)) text.isOrthographic = true;
                var layout = panel.gameObject.AddComponent<NotificationTextLayout>();
                var action = panel.Find("Motion/70_ActionTMP").GetComponent<TMP_Text>();
                var helper = panel.Find("Motion/80_HelperTMP").GetComponent<TMP_Text>();
                for (int i = 0; i < 2; i++)
                {
                    // 뷰가 매번 글자 칸을 272×32로 되돌려도 다시 맞춘다.
                    view.ShowEditImmediate("F", "서비스를 구매한 고객만 이용 가능합니다",
                        CurtainCall.UI.InteractionV1.InteractionAvailability.Unavailable, false, 0, "사용 불가");
                    layout.RefreshLayout();
                    Assert.Greater(panel.rect.width, 384, "한 줄에 들어가도록 넓힌다");
                    Assert.AreEqual(80, panel.rect.height, "넓혀서 들어가면 줄바꿈하지 않는다");
                    action.ForceMeshUpdate();
                    Assert.IsFalse(action.isTextOverflowing);

                    view.ShowEditImmediate("F", "자물쇠가 걸린 트렁크 — 가늘고 뾰족한 도구가 있으면 열 수 있을 것 같습니다.",
                        CurtainCall.UI.InteractionV1.InteractionAvailability.Unavailable, false, 0, "사용 불가");
                    layout.RefreshLayout();
                    Assert.AreEqual(640, panel.rect.width, "최대 폭");
                    Assert.Greater(panel.rect.height, 80, "최대 폭으로도 넘치면 줄바꿈하고 아래로 늘린다");
                    Assert.AreEqual(3, action.maxVisibleLines);
                    Assert.LessOrEqual(-action.rectTransform.anchoredPosition.y + action.rectTransform.rect.height,
                        -helper.rectTransform.anchoredPosition.y, "보조 줄과 겹치지 않는다");
                    action.ForceMeshUpdate();
                    Assert.IsFalse(action.isTextOverflowing, "말줄임 없이 다 보인다");
                }
                view.ShowEditImmediate("F", "입장", CurtainCall.UI.InteractionV1.InteractionAvailability.Available, false, 0);
                layout.RefreshLayout();
                Assert.AreEqual(384, panel.rect.width);
                Assert.AreEqual(80, panel.rect.height);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static Vector3 Top(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[1];
        }
    }
}
