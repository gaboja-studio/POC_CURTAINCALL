using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.TransitionCurtain.Tests
{
    public sealed class TransitionCurtainPlayTests
    {
        private GameObject instance;
        private MonoBehaviour view;
        private float previousScale;
        private int covered;
        private int opened;
        private object Call(string method, params object[] args) { return view.GetType().GetMethod(method).Invoke(view, args); }
        private object Property(string name) { return view.GetType().GetProperty(name).GetValue(view); }
        private T Field<T>(string name) { return (T)view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view); }
        private string State { get { return Property("State").ToString(); } }
        private float Amount { get { return (float)Property("ClosedAmount"); } }
        private bool FullyCovered { get { return (bool)Property("IsFullyCovered"); } }
        private void Subscribe(string name, Action handler) { view.GetType().GetEvent(name).AddEventHandler(view, handler); }
        private IEnumerator Covered()
        {
            Assert.IsTrue((bool)Call("Close"));
            float until = Time.realtimeSinceStartup + 4;
            while (!FullyCovered && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(FullyCovered, "렌더 프레임 장벽이 완료되지 않았습니다. Game View 활성 상태를 확인하세요.");
        }
        private void Hidden()
        {
            Assert.AreEqual("Open", State);
            Assert.AreEqual(0, Amount);
            Assert.IsFalse(FullyCovered);
            Assert.IsFalse((bool)Property("IsBusy"));
            Assert.IsFalse(Field<RectTransform>("viewport").gameObject.activeSelf);
            Assert.IsFalse(Field<RectTransform>("loading").gameObject.activeSelf);
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScale = Time.timeScale;
            Time.timeScale = 1;
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/TransitionCurtain/TransitionCurtain");
            Assert.NotNull(prefab);
            instance = Object.Instantiate(prefab);
            view = instance.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.TransitionCurtainView");
            Subscribe("Covered", () => covered++);
            Subscribe("Opened", () => opened++);
            covered = opened = 0;
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            try { if (instance != null) Object.Destroy(instance); yield return null; }
            finally { Time.timeScale = previousScale; }
        }
        [UnityTest] public IEnumerator InitialStructureAndTypography()
        {
            Hidden();
            Assert.AreEqual(8, instance.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length);
            Assert.AreEqual(Vector4.one * -2, instance.GetComponentInChildren<UnityEngine.UI.RectMask2D>(true).padding);
            var labels = instance.GetComponentsInChildren<TMP_Text>(true);
            Assert.AreEqual(1, labels.Length);
            var label = labels[0];
            Assert.AreEqual(24, label.fontSize);
            Assert.AreEqual(AtlasPopulationMode.Static, label.font.atlasPopulationMode);
            Assert.IsTrue(label.font.HasCharacters("장소 전환 중…가나다힣"));
            Assert.IsFalse(label.richText);
            Assert.IsFalse(label.parseCtrlCharacters);
            Assert.IsFalse(label.enableAutoSizing);
            Assert.AreEqual(TextWrappingModes.NoWrap, label.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
            Assert.AreEqual(new Vector2(240, 32), label.rectTransform.sizeDelta);
            foreach (var image in instance.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                Assert.NotNull(image.sprite);
                Assert.IsFalse(image.raycastTarget);
                Assert.AreEqual(UnityEngine.UI.Image.Type.Simple, image.type);
                Assert.IsFalse(image.useSpriteMesh);
            }
            Assert.IsFalse(label.raycastTarget);
            foreach (var tr in instance.GetComponentsInChildren<Transform>(true))
                foreach (var component in tr.GetComponents<Component>()) Assert.NotNull(component);
            yield return null;
        }
        [UnityTest] public IEnumerator ReferencesStayInsidePrefabClone()
        {
            foreach (string name in new[] { "rootCanvas", "viewport", "bodyStage", "leftLeaf", "rightLeaf", "valance", "loading", "leftBody", "rightBody", "star", "starShadow", "loadingLabel" })
            {
                Component reference = Field<Component>(name);
                Assert.NotNull(reference);
                Assert.IsTrue(reference.transform == instance.transform || reference.transform.IsChildOf(instance.transform));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ClosedFrameBarrierAndIdempotence()
        {
            Assert.IsTrue((bool)Call("Close"));
            Assert.IsFalse(FullyCovered);
            Assert.AreEqual(0, covered);
            float until = Time.realtimeSinceStartup + 4;
            while (Amount < 1 && Time.realtimeSinceStartup < until) { Assert.IsFalse(FullyCovered); yield return null; }
            Assert.AreEqual(1, Amount);
            while (!FullyCovered && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(FullyCovered);
            Assert.AreEqual(1, covered);
            Assert.AreEqual("Covered", State);
            Assert.IsTrue(Field<RectTransform>("loading").gameObject.activeSelf);
            Assert.IsTrue((bool)Call("Close"));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.AreEqual(1, covered);
            Assert.AreEqual(1, Amount);
        }
        [UnityTest] public IEnumerator AnimatedOpenEmitsOnceAndHides()
        {
            yield return Covered();
            Assert.IsTrue((bool)Call("Open"));
            Assert.IsFalse(FullyCovered);
            yield return new WaitForSecondsRealtime(.6f);
            Hidden();
            Assert.AreEqual(1, opened);
            Assert.IsTrue((bool)Call("Open"));
            yield return null;
            Assert.AreEqual(1, opened);
        }
        [UnityTest] public IEnumerator ReversalKeepsCurrentPositionAndCancelsBarrier()
        {
            Call("Close");
            yield return new WaitForSecondsRealtime(.17f);
            float before = Amount;
            Assert.Greater(before, 0);
            Assert.Less(before, 1);
            Call("Open");
            Assert.AreEqual(before, Amount);
            yield return new WaitForSecondsRealtime(.6f);
            Hidden();
            Assert.AreEqual(0, covered);
            Assert.AreEqual(1, opened);
        }
        [UnityTest] public IEnumerator RepeatedCloseDoesNotRestartMotion()
        {
            Call("Close");
            yield return new WaitForSecondsRealtime(.2f);
            float before = Amount;
            Call("Close");
            Assert.AreEqual(before, Amount);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(1, Amount);
            Assert.IsTrue(FullyCovered);
        }
        [UnityTest] public IEnumerator UnscaledMotionAndStarPreserveTimeScale()
        {
            Time.timeScale = 0;
            yield return Covered();
            var star = Field<UnityEngine.UI.Image>("star").rectTransform;
            var shadow = Field<UnityEngine.UI.Image>("starShadow").rectTransform;
            Quaternion before = star.localRotation;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.Greater(Quaternion.Angle(before, star.localRotation), 1);
            Assert.Less(Quaternion.Angle(star.localRotation, shadow.localRotation), .01f);
            Assert.AreEqual(Vector2.one * .5f, star.pivot);
            Assert.AreEqual(new Vector2(0, -1), shadow.anchoredPosition - star.anchoredPosition);
            Call("Open");
            yield return new WaitForSecondsRealtime(.6f);
            Hidden();
            Assert.AreEqual(0, Time.timeScale);
        }
        [UnityTest] public IEnumerator CoveredCallbackCanOpenReentrantly()
        {
            Subscribe("Covered", () => Call("Open"));
            Call("Close");
            yield return new WaitForSecondsRealtime(1.2f);
            Hidden();
            Assert.AreEqual(1, covered);
            Assert.AreEqual(1, opened);
        }
        [UnityTest] public IEnumerator OpenedCallbackCanCloseReentrantly()
        {
            yield return Covered();
            Subscribe("Opened", () => Call("Close"));
            Call("Open");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual("Covered", State);
            Assert.IsTrue(FullyCovered);
            Assert.AreEqual(2, covered);
            Assert.AreEqual(1, opened);
        }
        [UnityTest] public IEnumerator DisableCancelsAndReenableStartsOpen()
        {
            Call("Close");
            yield return new WaitForSecondsRealtime(.12f);
            view.enabled = false;
            Hidden();
            Assert.IsFalse((bool)Call("Close"));
            Assert.IsFalse((bool)Call("Open"));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.AreEqual(0, covered);
            view.enabled = true;
            yield return null;
            Hidden();
            yield return Covered();
        }
        [UnityTest] public IEnumerator CoveredCallbackCanDisable()
        {
            Subscribe("Covered", () => view.enabled = false);
            Call("Close");
            yield return new WaitForSecondsRealtime(.7f);
            Hidden();
            Assert.AreEqual(1, covered);
        }
        [UnityTest] public IEnumerator ImmediateOpenCancelsWithoutCompletionEvents()
        {
            Call("Close");
            yield return new WaitForSecondsRealtime(.2f);
            Call("OpenImmediate");
            yield return new WaitForSecondsRealtime(.7f);
            Hidden();
            Assert.AreEqual(0, covered);
            Assert.AreEqual(0, opened);
        }
        [UnityTest] public IEnumerator CanvasLossAbortsWithoutFalseCoverage()
        {
            Call("Close");
            Field<Canvas>("rootCanvas").enabled = false;
            LogAssert.Expect(LogType.Warning, "C16 rendering contract was lost; pending Covered notification was cancelled.");
            yield return null;
            Hidden();
            Assert.AreEqual(0, covered);
            Assert.IsFalse((bool)Call("Close"));
        }
        [UnityTest] public IEnumerator BodyAlphaLossAbortsCoveredState()
        {
            yield return Covered();
            Field<UnityEngine.UI.Image>("leftBody").color = new Color(1, 1, 1, .5f);
            Assert.IsFalse(FullyCovered);
            LogAssert.Expect(LogType.Warning, "C16 body visibility/alpha changed; a fully covered frame cannot be guaranteed.");
            yield return null;
            Hidden();
            Assert.AreEqual(1, covered);
        }
        [UnityTest] public IEnumerator TextRemainsLiteralAndNullIsEmpty()
        {
            string text = "<b>장소</b>\\n 전환 중 장소 전환 중 장소 전환 중 장소 전환 중";
            Call("SetLoadingText", text);
            yield return Covered();
            var label = Field<TMP_Text>("loadingLabel");
            label.ForceMeshUpdate();
            Assert.AreEqual(text, label.text);
            Assert.IsTrue(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.character == '…' && c.isVisible));
            Assert.Less(label.textInfo.characterCount, text.Length);
            Assert.Greater(label.GetPreferredValues(text, Mathf.Infinity, Mathf.Infinity).x, label.rectTransform.rect.width);
            Assert.IsFalse(label.richText);
            Call("SetLoadingText", new object[] { null });
            Assert.AreEqual(string.Empty, label.text);
        }
        [UnityTest] public IEnumerator CoverLayoutKeepsUniformScaleAndExactPairs()
        {
            yield return Covered();
            var stage = Field<RectTransform>("bodyStage");
            var viewport = Field<RectTransform>("viewport");
            float cover = Mathf.Max(viewport.rect.width / 1920, viewport.rect.height / 1080);
            Assert.That(stage.localScale.x, Is.EqualTo(cover).Within(.001f));
            Assert.AreEqual(stage.localScale.x, stage.localScale.y);
            Assert.AreEqual(new Vector2(-32, 32), Field<RectTransform>("leftLeaf").anchoredPosition);
            Assert.AreEqual(new Vector2(912, 32), Field<RectTransform>("rightLeaf").anchoredPosition);
            foreach (string name in new[] { "leftLeaf", "rightLeaf", "valance" })
            {
                var parent = Field<RectTransform>(name);
                var body = (RectTransform)parent.GetChild(0);
                var fringe = (RectTransform)parent.GetChild(1);
                Assert.AreEqual(body.rect, fringe.rect);
                Assert.AreEqual(Vector2.zero, body.offsetMin);
                Assert.AreEqual(Vector2.zero, body.offsetMax);
            }
        }
    }
}
