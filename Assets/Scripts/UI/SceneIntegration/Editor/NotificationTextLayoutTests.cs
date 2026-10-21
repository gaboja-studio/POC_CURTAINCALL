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
        public void InteractionReappliesTwoLinesAfterViewResetsLabelAndRestoresShortHeight()
        {
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
                    view.ShowEditImmediate("F", "서비스를 구매한 고객만 이용 가능합니다",
                        CurtainCall.UI.InteractionV1.InteractionAvailability.Unavailable, false, 0, "사용 불가");
                    layout.RefreshLayout();
                    Assert.Greater(panel.rect.height, 80);
                    Assert.Greater(action.rectTransform.rect.height, 32);
                    Assert.LessOrEqual(-action.rectTransform.anchoredPosition.y + action.rectTransform.rect.height,
                        -helper.rectTransform.anchoredPosition.y);
                    Assert.AreEqual(2, action.maxVisibleLines);
                }
                view.ShowEditImmediate("F", "입장", CurtainCall.UI.InteractionV1.InteractionAvailability.Available, false, 0);
                layout.RefreshLayout();
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
