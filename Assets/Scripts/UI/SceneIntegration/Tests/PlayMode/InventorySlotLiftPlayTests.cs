using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class InventorySlotLiftPlayTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        Sprite sprite;
        Texture2D texture;
        Material original;

        [UnityTest]
        public IEnumerator BreakLiftsBeforeDissolvingAndRestoresOnReplayAndDisable()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CurtainCall.UI.SceneIntegration.InventorySlotUseEffect"))
                .First(t => t != null);
            root = new GameObject("SlotFxTest", typeof(RectTransform)); root.SetActive(false);
            UnityEngine.UI.Image Image(string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(root.transform, false); return go.GetComponent<UnityEngine.UI.Image>();
            }
            var icon = Image("Icon"); var ghost = Image("Ghost"); var flash = Image("Flash");
            texture = new Texture2D(8, 8); texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray()); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f); icon.sprite = sprite;
            original = new Material(Shader.Find("UI/Default")); icon.material = original;
            Vector2 rest = new(9, 13); Vector3 scale = Vector3.one * .8f;
            ghost.rectTransform.anchoredPosition = rest; ghost.rectTransform.localScale = scale;
            var fx = root.AddComponent(type);
            void Set(string name, object value) => type.GetField(name, Private).SetValue(fx, value);
            Set("icon", icon); Set("breakGhost", ghost); Set("flash", flash);
            Set("breakSeconds", 1f); Set("breakLiftFraction", .3f);
#if UNITY_EDITOR
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_ThirdParty/curtaincall/InventorySlotFx/InventorySlotFx.mat");
            Assert.IsNotNull(material, "Installed All In 1 UI material must be present.");
            Set("effectMaterial", material);
#endif
            root.SetActive(true);
            type.GetMethod("PlayUse").Invoke(fx, null);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreSame(original, icon.material, "Normal use must restore the icon's original material.");
            type.GetMethod("PlayBreak").Invoke(fx, new object[] { sprite });
            yield return new WaitForSecondsRealtime(.12f);
            Assert.IsTrue(ghost.enabled);
            Assert.Greater(ghost.rectTransform.anchoredPosition.y, rest.y + 2, "The lost icon must visibly rise out of its slot.");
            Assert.AreEqual(-.1f, ghost.material.GetFloat("_FadeAmount"), .001f, "Dissolve must wait for the initial lift.");
            Assert.AreSame(original, icon.material, "The replacement/current item must not be dissolved.");
            yield return new WaitForSecondsRealtime(.45f);
            Assert.Greater(ghost.material.GetFloat("_FadeAmount"), -.05f, "All In 1 dissolve progresses after the lift.");
            yield return new WaitForSecondsRealtime(.6f);
            Assert.IsFalse(ghost.enabled);
            Assert.AreEqual(rest, ghost.rectTransform.anchoredPosition);
            Assert.AreEqual(scale, ghost.rectTransform.localScale);
            type.GetMethod("PlayBreak").Invoke(fx, new object[] { sprite });
            yield return new WaitForSecondsRealtime(.12f);
            root.SetActive(false);
            Assert.IsFalse(ghost.enabled);
            Assert.AreEqual(rest, ghost.rectTransform.anchoredPosition, "Interrupted/repeated use must not drift the slot layout.");
            Assert.AreEqual(scale, ghost.rectTransform.localScale);
        }

        [TearDown]
        public void Cleanup()
        {
            if (root) UnityEngine.Object.DestroyImmediate(root);
            if (sprite) UnityEngine.Object.DestroyImmediate(sprite);
            if (texture) UnityEngine.Object.DestroyImmediate(texture);
            if (original) UnityEngine.Object.DestroyImmediate(original);
        }
    }
}
