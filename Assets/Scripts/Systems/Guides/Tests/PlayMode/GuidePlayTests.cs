using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.Guides.Tests
{
    public sealed class GuidePlayTests
    {
        GameObject instance;
        MonoBehaviour view;
        float previousScale;
        CanvasGroup Group => instance.GetComponentsInChildren<CanvasGroup>(true).Single();
        RectTransform Root => (RectTransform)view.transform;
        TMP_Text[] Labels => instance.GetComponentsInChildren<TMP_Text>(true);
        object Call(string name, params object[] args) => view.GetType().GetMethod(name).Invoke(view, args);
        bool Flag(string name) => (bool)view.GetType().GetProperty(name).GetValue(view);
        object EnumValue(string method, int index, int value) => Enum.ToObject(view.GetType().GetMethod(method).GetParameters()[index].ParameterType, value);
        bool Key(string key = "F", string text = "행동 안내", int width = 0) => (bool)Call("ShowKey", key, text, EnumValue("ShowKey", 2, width));
        bool Mouse(int mode = 0, string text = "행동 안내") => (bool)Call("ShowMouse", EnumValue("ShowMouse", 0, mode), text);
        void Hidden()
        {
            Assert.IsFalse(Flag("IsVisible"));
            Assert.IsFalse(Flag("HasRuntimePresentation"));
            Assert.AreEqual(0, Group.alpha);
            Assert.IsTrue(view.gameObject.activeSelf);
        }
        IEnumerator Settle(float seconds = .18f) { yield return new WaitForSecondsRealtime(seconds); }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScale = Time.timeScale; Time.timeScale = 1;
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Guides/Guides");
            Assert.NotNull(prefab);
            instance = Object.Instantiate(prefab);
            view = instance.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.GuidesV1.GuideView");
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try { if (instance != null) Object.Destroy(instance); yield return null; }
            finally { Time.timeScale = previousScale; }
        }
        [UnityTest]
        public IEnumerator PrefabWiringAndInitialHidden()
        {
            Hidden(); Assert.AreEqual(8, instance.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length); Assert.AreEqual(2, Labels.Length);
            Assert.AreEqual(new Vector2(544, 80), Root.sizeDelta); Assert.AreEqual(new Vector2(0, 176), Root.anchoredPosition);
            Assert.AreEqual(new Vector2(.5f, 0), Root.anchorMin); Assert.AreEqual(Root.anchorMin, Root.anchorMax);
            Assert.IsEmpty(instance.GetComponentsInChildren<GraphicRaycaster>(true));
            Assert.IsFalse(Group.blocksRaycasts); Assert.IsFalse(Group.interactable);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(graphic.raycastTarget);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true)) foreach (var c in t.GetComponents<Component>()) Assert.NotNull(c);
            foreach (var label in Labels) Assert.IsEmpty(label.text);
            yield return null;
        }
        [UnityTest]
        public IEnumerator AllThreeKeyWidthsHaveCorrectGeometry()
        {
            float[] widths = {40, 96, 144}; string[] keys = {"F", "Space", "Left Shift"};
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(Key(keys[i], width:i)); yield return Settle();
                Assert.IsTrue(Flag("IsVisible")); Assert.IsTrue(Flag("HasRuntimePresentation"));
                Assert.That(Labels.Any(x => x.text == keys[i]));
                var bodies = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(x => x.enabled && x.gameObject.activeInHierarchy && x.sprite != null && x.sprite.name.Contains("keycap") && x.sprite.name.Contains("body")).ToArray();
                Assert.AreEqual(1, bodies.Length); Assert.AreEqual(new Vector2(widths[i], 40), bodies[0].rectTransform.sizeDelta);
            }
        }
        [UnityTest]
        public IEnumerator AllThreeMouseModesUse32PixelIcons()
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(Mouse(i)); yield return Settle(); Assert.IsTrue(Flag("IsVisible"));
                var icons = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(x => x.enabled && x.gameObject.activeInHierarchy && x.sprite != null && x.sprite.name.Contains("mouse")).ToArray();
                Assert.AreEqual(2, icons.Length);
                foreach (var icon in icons) { Assert.AreEqual(new Vector2(32, 32), icon.rectTransform.sizeDelta); Assert.AreEqual(UnityEngine.UI.Image.Type.Simple, icon.type); }
                Assert.That(icons.Any(x => x.sprite.name.Contains(i == 0 ? "left" : i == 1 ? "right" : "move")));
            }
        }
        [UnityTest]
        public IEnumerator TypographyIsStaticLiteralAndSingleLine()
        {
            Assert.IsTrue(Key("F", "행동 안내")); yield return Settle(); Canvas.ForceUpdateCanvases();
            foreach (var label in Labels)
            {
                Assert.AreEqual(24, label.fontSize); Assert.IsFalse(label.enableAutoSizing); Assert.IsFalse(label.richText); Assert.IsFalse(label.parseCtrlCharacters);
                Assert.AreEqual(TextWrappingModes.NoWrap, label.textWrappingMode); Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
                Assert.NotNull(label.font); Assert.AreEqual(AtlasPopulationMode.Static, label.font.atlasPopulationMode); Assert.IsTrue(label.font.HasCharacters(label.text));
                label.ForceMeshUpdate(); Assert.Greater(label.textInfo.characterCount, 0);
            }
        }
        [UnityTest]
        public IEnumerator LongTextRetainsSizeAndDoesNotWrap()
        {
            Assert.IsTrue(Key("Left Shift", string.Concat(Enumerable.Repeat("행동 안내 ", 40)), 2)); yield return Settle();
            foreach (var label in Labels) { label.ForceMeshUpdate(); Assert.AreEqual(24, label.fontSize); Assert.LessOrEqual(label.textInfo.lineCount, 1); }
        }
        [UnityTest]
        public IEnumerator ControlCharactersCleanedButRichTextRemainsLiteral()
        {
            string text = "<b>행동</b>\\n\t안내\r\n" + (char)0x2028 + (char)0x2029;
            Assert.IsTrue(Key("F\n\t", text)); yield return Settle();
            Assert.That(Labels.Any(x => x.text.Contains("<b>행동</b>\\n")));
            foreach (var label in Labels) Assert.IsFalse(label.text.Any(c => char.IsControl(c) || c == 0x2028 || c == 0x2029));
        }
        [UnityTest]
        public IEnumerator InvalidEnumsRejectWithoutMutatingExistingPresentation()
        {
            Assert.IsTrue(Key()); yield return Settle(); var before = Labels.Select(x => x.text).ToArray();
            Assert.IsFalse(Key("", "", 999)); Assert.IsFalse(Mouse(999, ""));
            CollectionAssert.AreEqual(before, Labels.Select(x => x.text)); Assert.IsTrue(Flag("HasRuntimePresentation"));
        }
        [UnityTest]
        public IEnumerator EmptyValidContentHidesAndClears()
        {
            Assert.IsTrue(Key()); Assert.IsFalse(Key(" ", "행동 안내")); Hidden();
            Assert.IsTrue(Key()); Assert.IsFalse(Mouse(0, "\t\r\n")); Hidden();
            foreach (var label in Labels) Assert.IsEmpty(label.text); yield return null;
        }
        [UnityTest]
        public IEnumerator BlockingIsImmediateAndUnblockingNeverRestores()
        {
            Assert.IsTrue(Key()); yield return Settle(); Call("SetContextBlocked", true); Hidden();
            foreach (var label in Labels) Assert.IsEmpty(label.text);
            Assert.IsFalse(Key()); Assert.IsFalse(Mouse()); Call("SetContextBlocked", false); Hidden();
            Assert.IsTrue(Mouse()); yield return Settle(); Assert.IsTrue(Flag("IsVisible"));
        }
        [UnityTest]
        public IEnumerator HideImmediatePreservesBlockButResetClearsBlock()
        {
            Call("SetContextBlocked", true); Call("HideImmediate"); Assert.IsFalse(Key());
            Call("ResetForRuntime"); Hidden(); Assert.IsTrue(Key()); yield return null;
        }
        [UnityTest]
        public IEnumerator RealUpdateCompletesEnterAndExit()
        {
            Assert.IsTrue(Key()); Assert.IsTrue(Flag("HasRuntimePresentation")); Assert.AreEqual(0, Group.alpha);
            yield return Settle(); Assert.AreEqual(1, Group.alpha, .001); Assert.AreEqual(Vector2.zero, ((RectTransform)Group.transform).anchoredPosition);
            Call("Hide"); Assert.IsFalse(Flag("HasRuntimePresentation")); yield return Settle(); Hidden();
        }
        [UnityTest]
        public IEnumerator RepeatedShowDoesNotRestartAndReversalPreservesCurrentValue()
        {
            Assert.IsTrue(Key()); yield return Settle(); Assert.AreEqual(1, Group.alpha);
            Assert.IsTrue(Mouse()); Assert.AreEqual(1, Group.alpha); Call("Hide"); yield return null;
            float alpha = Group.alpha; Vector2 position = ((RectTransform)Group.transform).anchoredPosition;
            Assert.IsTrue(Key()); Assert.AreEqual(alpha, Group.alpha, .0001); Assert.AreEqual(position, ((RectTransform)Group.transform).anchoredPosition);
            yield return Settle(); Assert.AreEqual(1, Group.alpha);
        }
        [UnityTest]
        public IEnumerator MotionRunsWithTimeScaleZero()
        {
            Time.timeScale = 0; Assert.IsTrue(Key()); yield return Settle(); Assert.AreEqual(1, Group.alpha);
            Call("Hide"); yield return Settle(); Hidden();
        }
        [UnityTest]
        public IEnumerator DisableClearsAndKeepsBlockedContext()
        {
            Call("SetContextBlocked", true); instance.SetActive(false); instance.SetActive(true); yield return null;
            Hidden(); Assert.IsFalse(Key()); Call("SetContextBlocked", false); Assert.IsTrue(Key());
            instance.SetActive(false); Assert.IsFalse(Key()); instance.SetActive(true); yield return null; Hidden();
        }
        [UnityTest]
        public IEnumerator EarlierAwakeShowSurvivesViewAndMotionInitialization()
        {
            var parent = new GameObject("C7_EarlyAwakeTest"); parent.SetActive(false);
            try
            {
                var clone = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/Guides/Guides"), parent.transform);
                clone.SetActive(true);
                var target = clone.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.GuidesV1.GuideView");
                var presenter = target.gameObject.AddComponent<GuideEarlyAwakePresenter>();
                parent.SetActive(true); Assert.IsTrue(presenter.Accepted);
                yield return Settle();
                Assert.IsTrue((bool)target.GetType().GetProperty("IsVisible").GetValue(target));
            }
            finally { Object.Destroy(parent); }
        }
        [UnityTest]
        public IEnumerator DisabledComponentRejectsBeforeEmptyInput()
        {
            Assert.IsTrue(Key()); yield return Settle(); view.enabled = false;
            Assert.IsFalse(Key()); Assert.IsFalse(Mouse()); view.enabled = true; yield return null; Hidden();
        }
    }
    [DefaultExecutionOrder(-32000)]
    public sealed class GuideEarlyAwakePresenter : MonoBehaviour
    {
        public bool Accepted { get; private set; }
        void Awake()
        {
            var target = GetComponents<MonoBehaviour>().Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.GuidesV1.GuideView");
            var method = target.GetType().GetMethod("ShowKey");
            Accepted = (bool)method.Invoke(target, new object[] { "F", "행동 안내", Enum.ToObject(method.GetParameters()[2].ParameterType, 0) });
        }
    }
}
