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
