using NUnit.Framework;
using System.Collections;
using System.Reflection;
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

        [Test]
        public void CannonAndPolePromptFitsLongKeysAndAllHelperLinesThenRestoresOtherPrompts()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab"));
            try
            {
                var view = instance.GetComponentInChildren<CurtainCall.UI.InteractionV1.InteractionPromptView>(true);
                foreach (var text in view.GetComponentsInChildren<TMP_Text>(true)) text.isOrthographic = true;
                var layout = view.gameObject.AddComponent<NotificationTextLayout>();
                var key = view.transform.Find("Motion/60_KeyTMP").GetComponent<TMP_Text>();
                float originalKeyWidth = key.rectTransform.rect.width;
                view.ShowEditImmediate("Left Shift", "함께 그네 잡기 (3/4)", CurtainCall.UI.InteractionV1.InteractionAvailability.Available,
                    false, 0, new string('가', 180));
                layout.SetDeviceText(true); layout.RefreshLayout();
                foreach (var text in view.GetComponentsInChildren<TMP_Text>(true))
                { text.ForceMeshUpdate(true); Assert.IsFalse(text.isTextOverflowing, text.name); }
                Assert.Greater(key.rectTransform.rect.width, originalKeyWidth);
                Assert.AreEqual(int.MaxValue, view.transform.Find("Motion/80_HelperTMP").GetComponent<TMP_Text>().maxVisibleLines);
                view.ShowEditImmediate("F", "입장", CurtainCall.UI.InteractionV1.InteractionAvailability.Available, false, 0);
                layout.SetDeviceText(false); layout.RefreshLayout();
                Assert.AreEqual(originalKeyWidth, key.rectTransform.rect.width);
                Assert.AreEqual(384, ((RectTransform)view.transform).rect.width);
                Assert.AreEqual(80, ((RectTransform)view.transform).rect.height);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void VisibleDeviceGuideRefitsWhenOnlyItsKeyOrHelperVisibilityChanges()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab"));
            try
            {
                var view = instance.GetComponentInChildren<CurtainCall.UI.InteractionV1.InteractionPromptView>(true);
                foreach (var text in view.GetComponentsInChildren<TMP_Text>(true)) text.isOrthographic = true;
                var layout = view.gameObject.AddComponent<NotificationTextLayout>();
                var key = view.transform.Find("Motion/60_KeyTMP").GetComponent<TMP_Text>();
                var action = view.transform.Find("Motion/70_ActionTMP").GetComponent<TMP_Text>();
                var helper = view.transform.Find("Motion/80_HelperTMP");
                view.ShowEditImmediate("F", "도약", CurtainCall.UI.InteractionV1.InteractionAvailability.Available, false, 0, "그네");
                layout.SetDeviceText(true);
                layout.RefreshLayout();
                var update = typeof(NotificationTextLayout).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                float narrow = key.rectTransform.rect.width;
                key.text = "Left Shift";
                update.Invoke(layout, null);
                Assert.Greater(key.rectTransform.rect.width, narrow);
                helper.gameObject.SetActive(false);
                update.Invoke(layout, null);
                Assert.That(action.rectTransform.anchoredPosition.y, Is.EqualTo(-24));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CannonAndPoleKoreanGlyphsAreWarmedOnRuntimeCopiesOnly(bool separateToast)
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(separateToast
                ? "Assets/Resources/Prefabs/UIs/Notifications/Toast.prefab"
                : "Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab"));
            var owner = new GameObject("Offline cannon prompt fonts"); owner.SetActive(false);
            owner.AddComponent<CommonInteractionUI>();
            if (!separateToast) instance.transform.SetParent(owner.transform, false);
            var fonts = owner.AddComponent<UiRuntimeFontFallback>();
            var label = separateToast ? instance.GetComponentInChildren<TMP_Text>(true)
                : instance.GetComponentInChildren<CurtainCall.UI.InteractionV1.InteractionPromptView>(true).transform.Find("Motion/80_HelperTMP").GetComponent<TMP_Text>();
            var original = label.font;
            int fallbackCount = original.fallbackFontAssetTable.Count;
            try
            {
                fonts.Configure(original);
                var source = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath("cc7bb308443682348a1354a3ef95192b"));
                Assert.NotNull(source); fonts.ConfigureSources(source, source);
                owner.SetActive(true);
                const string message = "대포가활성화되지않았습니다동료와그네박자를맞춰각자도약하세요맞은편도착가동고정 상부 유지 버튼 해제 → 상승 대포 · 양쪽 통로 유지봉 해제 → 합류 다리 · 반출교 레버 · 권상교 그네 · 복귀 계단 열림 · 정지 · 가동 중";
                fonts.WarmUpLocalText(instance.transform, message);
                Assert.AreNotSame(original, label.font);
                bool complete = label.font.HasCharacters(message, out uint[] missing, true, true);
                Assert.IsTrue(complete, missing != null ? string.Join(",", missing) : "Missing Korean glyphs");
                Assert.AreEqual(fallbackCount, original.fallbackFontAssetTable.Count);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                foreach (TMP_FontAsset copy in ((IDictionary)typeof(UiRuntimeFontFallback).GetField("copies", flags).GetValue(fonts)).Values)
                { copy.atlasTextures = System.Array.Empty<Texture2D>(); copy.material = null; Object.DestroyImmediate(copy); }
                foreach (TMP_FontAsset generated in ((IDictionary)typeof(UiRuntimeFontFallback).GetField("generated", flags).GetValue(fonts)).Values)
                    Object.DestroyImmediate(generated);
                ((IDictionary)typeof(UiRuntimeFontFallback).GetField("copies", flags).GetValue(fonts)).Clear();
                ((IDictionary)typeof(UiRuntimeFontFallback).GetField("generated", flags).GetValue(fonts)).Clear();
                var interaction = owner.GetComponent<CommonInteractionUI>();
                var materialField = typeof(CommonInteractionUI).GetField("material", flags);
                if (materialField.GetValue(interaction) is Material material) { Object.DestroyImmediate(material); materialField.SetValue(interaction, null); }
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MechanismToastKeepsAllLinesAndOtherToastKeepsOriginalLimit()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Notifications/Toast.prefab"));
            try
            {
                var label = instance.GetComponentInChildren<TMP_Text>(true);
                label.isOrthographic = true;
                var layout = label.GetComponentInParent<CurtainCall.UI.NotificationsV1.ToastView>().gameObject.AddComponent<NotificationTextLayout>();
                label.text = new string('가', 150);
                layout.SetDeviceText(true); layout.RefreshLayout(); label.ForceMeshUpdate(true);
                Assert.IsFalse(label.isTextOverflowing);
                Assert.AreEqual(int.MaxValue, label.maxVisibleLines);
                layout.SetDeviceText(false); layout.RefreshLayout();
                Assert.AreEqual(2, label.maxVisibleLines);
                Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
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
