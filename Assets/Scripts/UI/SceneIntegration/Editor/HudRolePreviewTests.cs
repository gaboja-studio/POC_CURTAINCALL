using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class HudRolePreviewTests
    {
        [Test]
        public void GuideFitsItsLongestWrappedLineInsteadOfTheWidthLimit()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Guide.prefab");
            var source = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/_ThirdParty/curtaincall_notifications_v1/Source/Fonts/Pretendard-SemiBold.ttf");
            Assert.NotNull(source);
            var font = TMP_FontAsset.CreateFontAsset(source);
            var canvas = new GameObject("Guide preview", typeof(Canvas));
            try
            {
                var card = Object.Instantiate(asset, canvas.transform, false).GetComponent<HudRoleCard>();
                const string first = "줄 위에서는 [A/D]를 눌러 균형을";
                const string second = "유지합니다.";
                const string title = "외줄 이동";
                font.TryAddCharacters(first + second + title);
                card.body.font = card.title.font = font;
                card.body.isOrthographic = card.title.isOrthographic = true;
                float expected = Mathf.Ceil(card.body.GetPreferredValues(first).x) + card.rows.padding.horizontal;
                card.maxWidth = Mathf.Ceil(card.body.GetPreferredValues(first + " 유").x) + card.rows.padding.horizontal + 2;
                card.SetText(first + " " + second, title);
                Canvas.ForceUpdateCanvases();
                card.body.ForceMeshUpdate(true, true);
                Assert.That(card.Rect.rect.width, Is.EqualTo(expected).Within(1f));
                Assert.Less(card.Rect.rect.width, card.maxWidth);
                Assert.AreEqual(2, card.body.textInfo.lineCount);
                Assert.AreEqual(1, card.body.textInfo.characterInfo.Take(card.body.textInfo.characterCount)
                    .First(glyph => glyph.character == '유').lineNumber);
            }
            finally { Object.DestroyImmediate(canvas); Object.DestroyImmediate(font); }
        }

        [Test]
        public void SystemEventPrefabContainsAndRendersEveryKoreanPreviewGlyph()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/SystemEvent.prefab");
            var canvas=new GameObject("Prefab preview",typeof(RectTransform),typeof(Canvas));
            try
            {
                canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var card=Object.Instantiate(asset,canvas.transform,false);
                var line=card.GetComponent<HudEventAppearance>().line;
                Assert.AreEqual("시스템 알림",line.text);
                Assert.IsTrue(line.font.HasCharacters(line.text),"font must contain the preview without Play-mode warmup");
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
                line.ForceMeshUpdate(true,true);
                foreach(char ch in line.text.Where(c=>c!=' '))
                {
                    Assert.IsTrue(line.textInfo.characterInfo.Take(line.textInfo.characterCount)
                        .Any(c=>c.isVisible && c.character==ch && c.textElement.unicode==ch),"missing: "+ch);
                }
            }
            finally{Object.DestroyImmediate(canvas);}
        }
    }
}
