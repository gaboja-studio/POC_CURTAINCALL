using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.Tooltips.Tests
{
    // Assembly-CSharp 직접 참조 없이 실제 Resources 프리팹의 계약을 검사합니다.
    public sealed class TooltipPlayTests
    {
        internal const string ResourcePath = "Prefabs/UIs/Tooltips/Tooltips";
        const string RuntimeNamespace = "CurtainCall.UI.TooltipsV1.";
        internal const string Title = "낡은 수화기";
        internal const string Description = "필요한 물품을 주문합니다.\n배달은 잠시 뒤 도착합니다.";
        const string LongDescription = "설명 내용의 길이에 따라 받침 높이가 늘어납니다.\n한계 높이 이후에는 말줄임 처리됩니다. 글자 크기는 줄이지 않습니다. 이 문구는 긴 내용을 확인하기 위한 예시입니다. 아래 꼬리는 고정 크기로 유지됩니다.";
        static readonly Vector2 Position = new Vector2(48f, 408f);
        readonly List<GameObject> clones = new List<GameObject>();
        GameObject instance;
        MonoBehaviour view, motion;
        float previousTimeScale;
        RectTransform Root => (RectTransform)Field(view, "rootRect");
        RectTransform Visual => (RectTransform)Field(motion, "visualRoot");
        CanvasGroup Group => (CanvasGroup)Field(motion, "visualGroup");
        Vector2 Body => (Vector2)Property(view, "BodySizeUi");
        float Tail => (float)Property(view, "TailCenterUi");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            Assert.NotNull(prefab, "C15 Resources 프리팹 누락");
            instance = Object.Instantiate(prefab);
            clones.Add(instance);
            instance.SetActive(true);
            view = Find(instance, "TooltipView");
            motion = Find(instance, "TooltipMotion");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                foreach (var clone in clones) if (clone != null) Object.Destroy(clone);
                clones.Clear();
                yield return null;
            }
            finally { Time.timeScale = previousTimeScale; }
        }

        [UnityTest]
        public IEnumerator ActualPrefabWiringTenLayersAndInitialHiddenState()
        {
            Assert.NotNull(instance.GetComponent<Canvas>());
            Assert.AreSame(instance.transform.Find("80_Tooltips/C15_Tooltip"), view.transform);
            Assert.AreSame(view.gameObject, motion.gameObject);
            Assert.AreSame(view.transform, Root);
            Assert.AreSame(motion, Field(view, "motion"));
            Assert.AreSame(view.transform.Find("Motion"), Visual);
            Assert.AreSame(Visual.GetComponent<CanvasGroup>(), Group);
            Assert.AreEqual(new Vector2(0f, -4f), Field(motion, "hiddenOffset"));
            Assert.AreEqual(.120f, Field(motion, "enterSeconds"));
            Assert.AreEqual(.100f, Field(motion, "exitSeconds"));
            string[] nodes = { "00_Shadow", "10_Body", "20_Frame", "30_Corners", "40_BottomEdgeLeft", "41_BottomEdgeRight", "50_TailBody", "51_TailFrame", "60_TitleTMP", "70_DescriptionTMP" };
            CollectionAssert.AreEqual(nodes, Enumerable.Range(0, Visual.childCount).Select(i => Visual.GetChild(i).name).ToArray());
            string[] fields = { "bottomEdgeLeft", "bottomEdgeRight", "tailBody", "tailFrame", "titleLabel", "descriptionLabel" };
            for (int i = 0; i < fields.Length; i++)
                Assert.AreSame(Visual.Find(nodes[i + 4]).gameObject, ((Component)Field(view, fields[i])).gameObject);
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>()) Assert.NotNull(component, transform.name);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(graphic.raycastTarget, graphic.name);
            Assert.IsEmpty(instance.GetComponentsInChildren<GraphicRaycaster>(true));
            Assert.IsFalse(Group.interactable); Assert.IsFalse(Group.blocksRaycasts); Assert.IsFalse(Group.ignoreParentGroups);
            AssertHidden(); AssertCleared();
            Assert.IsTrue(view.gameObject.activeSelf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NearbyGeometrySplitsFourHighSimpleRailsAroundFixedTail()
        {
            Assert.IsTrue(Nearby(tail: 96f));
            Assert.AreEqual("Nearby", Property(view, "Mode").ToString());
            AssertRect(Root, Position, Body);
            AssertRect(Label("titleLabel").rectTransform, new Vector2(24, 20), new Vector2(336, 36));
            AssertRect(Label("descriptionLabel").rectTransform, new Vector2(24, 64), new Vector2(336, Body.y - 84));
            AssertBottomGeometry(true);
            Assert.AreEqual(Body + new Vector2(0, 8.5f), Property(view, "PresentationSizeUi"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PanelGeometryUsesOneContinuousRailAndNoTailFootprint()
        {
            Assert.IsTrue(Panel());
            Assert.AreEqual("Panel", Property(view, "Mode").ToString());
            AssertRect(Root, Position, Body);
            AssertBottomGeometry(false);
            Assert.AreEqual(Body, Property(view, "PresentationSizeUi"));
            Assert.IsTrue(Nearby()); AssertBottomGeometry(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiniteWidthAndTailClampToContractBoundaries()
        {
            foreach (float width in new[] { -100f, 256f, 384f, 480f, 10000f })
            {
                Assert.IsTrue(Nearby(width: width));
                Assert.AreEqual(Mathf.Clamp(width, 256, 480), Body.x);
                Assert.AreEqual(Body.x / 2, Tail);
                Assert.IsTrue(Nearby(width: width, tail: -100f)); Assert.AreEqual(24f, Tail); AssertBottomGeometry(true);
                Assert.IsTrue(Nearby(width: width, tail: 10000f)); Assert.AreEqual(Body.x - 24, Tail); AssertBottomGeometry(true);
                Assert.IsTrue(Panel(width: width)); Assert.AreEqual(Mathf.Clamp(width, 256, 480), Body.x);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MeasuredHeightClamps160To240InFourUnitSteps()
        {
            bool sawMinimum = false, sawMaximum = false;
            foreach (float width in new[] { 256f, 384f, 480f })
                foreach (string text in new[] { "물품", Description, LongDescription, string.Join("\n", Enumerable.Repeat(Description, 12)) })
                {
                    Assert.IsTrue(Nearby(description: text, width: width));
                    float measured = Label("descriptionLabel").GetPreferredValues(text, width - 48, float.PositiveInfinity).y;
                    float expected = Mathf.Clamp(Mathf.Ceil((Mathf.Min(measured, 240) + 84) / 4) * 4, 160, 240);
                    Assert.AreEqual(expected, Body.y);
                    Assert.That(Body.y, Is.InRange(160f, 240f)); Assert.AreEqual(0f, Body.y % 4);
                    sawMinimum |= Body.y == 160; sawMaximum |= Body.y == 240;
                    AssertBottomGeometry(true);
                }
            Assert.IsTrue(sawMinimum); Assert.IsTrue(sawMaximum);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LongDescriptionWrapsAtRealWidthWithoutShrinkingFont()
        {
            var description = Label("descriptionLabel");
            float fontSize = description.fontSize;
            string text = LongDescription.Replace("\n", " ");
            Assert.IsTrue(Nearby(description: text, width: 256));
            Canvas.ForceUpdateCanvases(); description.ForceMeshUpdate();
            Assert.Greater(description.textInfo.lineCount, 1, "명시적 개행 없는 긴 문구도 실제 TMP에서 줄바꿈해야 합니다.");
            float narrowPreferred = description.GetPreferredValues(text, 208, float.PositiveInfinity).y;
            float narrowHeight = Body.y;
            Assert.IsTrue(Nearby(description: text, width: 480));
            Canvas.ForceUpdateCanvases(); description.ForceMeshUpdate();
            Assert.GreaterOrEqual(narrowPreferred, description.GetPreferredValues(text, 432, float.PositiveInfinity).y);
            Assert.GreaterOrEqual(narrowHeight, Body.y);
            Assert.AreEqual(TextWrappingModes.Normal, description.textWrappingMode);
            Assert.AreEqual(TextWrappingModes.NoWrap, Label("titleLabel").textWrappingMode);
            Assert.AreEqual(fontSize, description.fontSize);
            foreach (var label in new[] { Label("titleLabel"), description })
            {
                Assert.NotNull(label.font); Assert.IsFalse(label.enableAutoSizing);
                Assert.IsFalse(label.richText); Assert.IsFalse(label.parseCtrlCharacters);
                Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonfiniteShowAndSettersPreservePresentationEvenWithEmptyText()
        {
            Assert.IsTrue(Nearby(tail: 96));
            Vector2 size = Body, position = Root.anchoredPosition;
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.IsFalse(Nearby("", "", new Vector2(invalid, 0)));
                Assert.IsFalse(Nearby("", "", new Vector2(0, invalid)));
                Assert.IsFalse(Nearby("", "", width: invalid));
                Assert.IsFalse(Nearby("", "", tail: invalid));
                Assert.IsFalse(Panel("", "", width: invalid));
                Assert.IsFalse(Panel("", "", new Vector2(invalid, 0)));
                Assert.IsFalse(Panel("", "", new Vector2(0, invalid)));
                Assert.IsFalse(SetPosition(new Vector2(invalid, 0)));
                Assert.IsFalse(SetPosition(new Vector2(0, invalid)));
                Assert.IsFalse(SetTail(invalid));
                Assert.AreEqual(size, Body); Assert.AreEqual(position, Root.anchoredPosition); Assert.AreEqual(96f, Tail);
                Assert.AreEqual(Title, Label("titleLabel").text); Assert.AreEqual(Description, Label("descriptionLabel").text);
                Assert.IsTrue((bool)Property(view, "HasRuntimePresentation"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnavailableValidationPrecedesEmptyTextButValidEmptyHidesImmediately()
        {
            Assert.IsTrue(Nearby());
            motion.enabled = false;
            Assert.IsFalse(Nearby("", "")); Assert.IsFalse(Panel("", ""));
            Assert.AreEqual(Title, Label("titleLabel").text);
            Assert.AreEqual(Description, Label("descriptionLabel").text);
            motion.enabled = true;
            foreach (string blank in new[] { null, "", " \r\n\t" })
            {
                Assert.IsTrue(Nearby()); Assert.IsFalse(Nearby(title: blank)); AssertHidden(); AssertCleared();
                Assert.IsTrue(Panel()); Assert.IsFalse(Panel(description: blank)); AssertHidden(); AssertCleared();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenAndExitingSettersRejectWithoutCachingForNextShow()
        {
            Vector2 originalPosition = Root.anchoredPosition; float originalTail = Tail;
            Assert.IsFalse(SetPosition(new Vector2(640, 400))); Assert.IsFalse(SetTail(160));
            Assert.AreEqual(originalPosition, Root.anchoredPosition); Assert.AreEqual(originalTail, Tail);
            Assert.IsTrue(Nearby(tail: 96));
            yield return new WaitForSecondsRealtime(.3f);
            Call(view, "Hide");
            Assert.IsTrue(Visual.gameObject.activeSelf); Assert.IsFalse((bool)Property(view, "HasRuntimePresentation"));
            Assert.IsFalse(SetPosition(new Vector2(640, 400))); Assert.IsFalse(SetTail(160));
            Assert.AreEqual(new Vector2(48, -408), Root.anchoredPosition); Assert.AreEqual(96f, Tail);
            yield return new WaitForSecondsRealtime(.3f);
            AssertHidden(); Assert.IsFalse(SetPosition(Vector2.zero)); Assert.IsFalse(SetTail(160));
            Assert.IsTrue(Nearby()); Assert.AreEqual(192f, Tail); Assert.AreEqual(new Vector2(48, -408), Root.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator ActiveSettersUpdateOnlyStableGeometryWithoutRestartingMotion()
        {
            SlowMotion(); Assert.IsTrue(Nearby());
            yield return WaitForIntermediateAlpha();
            float alpha = Group.alpha, elapsed = (float)Field(motion, "elapsed"); Vector2 visual = Visual.anchoredPosition;
            Assert.IsTrue(SetPosition(new Vector2(640, 400)));
            Assert.AreEqual(new Vector2(640, -400), Root.anchoredPosition);
            Assert.IsTrue(SetTail(-100)); Assert.AreEqual(24f, Tail); AssertBottomGeometry(true);
            Assert.IsTrue(SetTail(10000)); Assert.AreEqual(360f, Tail); AssertBottomGeometry(true);
            Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(visual, Visual.anchoredPosition);
            Assert.AreEqual(elapsed, Field(motion, "elapsed"));
            yield return null;
            Assert.Greater(Group.alpha, alpha);
        }

        [UnityTest]
        public IEnumerator PanelRejectsTailUpdatesWithoutMutatingGeometryOrFutureNearby()
        {
            Assert.IsTrue(Panel());
            float tail = Tail; Vector2 rail = ImageOf("bottomEdgeLeft").rectTransform.sizeDelta;
            Assert.IsFalse(SetTail(24)); Assert.AreEqual(tail, Tail);
            Assert.AreEqual(rail, ImageOf("bottomEdgeLeft").rectTransform.sizeDelta); AssertBottomGeometry(false);
            Assert.IsTrue(SetPosition(new Vector2(640, 400)));
            Assert.IsTrue(Nearby()); Assert.AreEqual(192f, Tail); AssertBottomGeometry(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewControlTextAndLiteralTagsKeepActualCleaningContract()
        {
            const string title = "낡은\r\n수화기\t예제";
            string description = "첫 줄\r\n둘째 줄\r셋째 줄" + (char)0x2028 + "넷째 줄" + (char)0x2029 + "다섯째 줄\t예제";
            Assert.IsTrue(Nearby(title, description));
            Assert.AreEqual("낡은 수화기 예제", Label("titleLabel").text);
            Assert.AreEqual("첫 줄\n둘째 줄\n셋째 줄\n넷째 줄\n다섯째 줄 예제", Label("descriptionLabel").text);
            const string literalTitle = "<b>낡은 수화기</b>";
            const string literalDescription = @"<color=red>물품</color> \n 문자 …";
            Assert.IsTrue(Nearby(literalTitle, literalDescription));
            Assert.AreEqual(literalTitle, Label("titleLabel").text);
            Assert.AreEqual(literalDescription, Label("descriptionLabel").text);
            Assert.IsFalse(Label("titleLabel").richText); Assert.IsFalse(Label("descriptionLabel").parseCtrlCharacters);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImmediateHideAndResetClearTextAndAllowReopening()
        {
            foreach (string method in new[] { "HideImmediate", "ResetForRuntime" })
            {
                Assert.IsTrue(Nearby()); yield return null;
                Call(view, method); AssertHidden(); AssertCleared();
                Assert.AreEqual(new Vector2(0, -4), Visual.anchoredPosition);
                Assert.IsTrue(view.gameObject.activeSelf); Assert.IsTrue(view.enabled); Assert.IsTrue(motion.enabled);
                Assert.IsTrue(Panel()); Assert.IsTrue((bool)Property(view, "HasRuntimePresentation"));
            }
        }

        [UnityTest]
        public IEnumerator RepeatedShowChangesContentAndModeWithoutRestartingRealEntry()
        {
            Assert.IsTrue(Nearby());
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Group.alpha < 1f)
            {
                float alpha = Group.alpha, elapsed = (float)Field(motion, "elapsed"); Vector2 position = Visual.anchoredPosition;
                Assert.IsTrue(Panel("빵", "품목의 효과와 사용 조건을\n이곳에 표시합니다."));
                Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(position, Visual.anchoredPosition);
                Assert.AreEqual(elapsed, Field(motion, "elapsed"));
                Assert.Less(Time.realtimeSinceStartup, deadline, "반복 Show가 실제 Update의 등장을 재시작했습니다.");
                yield return null;
            }
            Assert.AreEqual("빵", Label("titleLabel").text); Assert.AreEqual(Vector2.zero, Visual.anchoredPosition);
            Assert.IsTrue(Nearby()); Assert.AreEqual(1f, Group.alpha);
        }

        [UnityTest]
        public IEnumerator RepeatedHideDoesNotRestartRealExitAndEndsExactlyHidden()
        {
            Assert.IsTrue(Nearby()); yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(1f, Group.alpha); Assert.AreEqual(Vector2.zero, Visual.anchoredPosition);
            Call(view, "Hide");
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Visual.gameObject.activeSelf)
            {
                float elapsed = (float)Field(motion, "elapsed");
                Call(view, "Hide"); Assert.AreEqual(elapsed, Field(motion, "elapsed"));
                Assert.Less(Time.realtimeSinceStartup, deadline, "반복 Hide가 실제 Update의 퇴장을 재시작했습니다.");
                yield return null;
            }
            AssertHidden(); Assert.AreEqual(new Vector2(0, -4), Visual.anchoredPosition);
            Call(view, "Hide"); AssertHidden();
        }

        [UnityTest]
        public IEnumerator RealUpdateReversesEntryAndExitFromCurrentAlphaAndPosition()
        {
            SlowMotion(); Assert.IsTrue(Nearby());
            yield return WaitForIntermediateAlpha();
            float alpha = Group.alpha; Vector2 position = Visual.anchoredPosition;
            Call(view, "Hide");
            Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(position, Visual.anchoredPosition);
            yield return null;
            Assert.Less(Group.alpha, alpha); Assert.Less(Visual.anchoredPosition.y, position.y);
            alpha = Group.alpha; position = Visual.anchoredPosition;
            Assert.IsTrue(Nearby());
            Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(position, Visual.anchoredPosition);
            yield return null;
            Assert.Greater(Group.alpha, alpha); Assert.Greater(Visual.anchoredPosition.y, position.y);
        }

        [UnityTest]
        public IEnumerator ReflectionSampleAtHalfDurationUsesCubicEaseNotARealFrameTimingClaim()
        {
            // 수치 샘플만 검사: elapsed를 반사로 주입하며 실제 프레임의 50% 도달을 주장하지 않습니다.
            // 실제 시간 진행은 RealUpdate / TimeScaleZero의 별도 테스트에서 검증합니다.
            SlowMotion(); Assert.IsTrue(Nearby());
            SampleAtHalfDuration();
            Assert.AreEqual(.875f, Group.alpha, .0001f);
            Assert.That(Vector2.Distance(new Vector2(0, -.5f), Visual.anchoredPosition), Is.LessThan(.0001f));
            Call(view, "Hide"); SampleAtHalfDuration();
            Assert.AreEqual(.109375f, Group.alpha, .0001f);
            Assert.That(Vector2.Distance(new Vector2(0, -3.5625f), Visual.anchoredPosition), Is.LessThan(.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TimeScaleZeroStillCompletesActualUpdateEntryAndExit()
        {
            float saved = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                Assert.IsTrue(Nearby()); Assert.AreEqual(0f, Group.alpha);
                yield return new WaitForSecondsRealtime(.35f);
                Assert.AreEqual(1f, Group.alpha); Assert.AreEqual(Vector2.zero, Visual.anchoredPosition);
                Call(view, "Hide"); yield return new WaitForSecondsRealtime(.3f);
                AssertHidden(); Assert.AreEqual(0f, Time.timeScale);
            }
            finally { Time.timeScale = saved; }
        }

        [UnityTest]
        public IEnumerator ParentViewAndMotionDisableDoNotResurrectOnReenable()
        {
            foreach (string target in new[] { "parent", "view", "motion" })
            {
                Assert.IsTrue(Nearby()); yield return null;
                if (target == "parent") instance.SetActive(false);
                else (target == "view" ? view : motion).enabled = false;
                AssertHidden(); Assert.IsFalse(Nearby("", ""));
                Assert.IsFalse(SetPosition(Vector2.zero)); Assert.IsFalse(SetTail(160));
                if (target == "parent") instance.SetActive(true);
                else (target == "view" ? view : motion).enabled = true;
                yield return null;
                AssertHidden();
                if (target != "motion") AssertCleared();
                Assert.IsTrue(Nearby(tail: 96)); Assert.AreEqual(96f, Tail);
                Call(view, "ResetForRuntime"); AssertHidden(); AssertCleared();
            }
        }

        [UnityTest]
        public IEnumerator EarlierAwakeShowSurvivesActualViewAndMotionAwake()
        {
            var parent = new GameObject("C15_EarlyAwakeTest");
            clones.Add(parent); parent.SetActive(false);
            var clone = Object.Instantiate(Resources.Load<GameObject>(ResourcePath), parent.transform);
            clone.SetActive(true);
            var presenter = Find(clone, "TooltipView").gameObject.AddComponent<TooltipEarlyAwakePresenter>();
            parent.SetActive(true);
            Assert.IsTrue(presenter.ShowAccepted);
            var earlyView = Find(clone, "TooltipView");
            Assert.IsTrue((bool)Property(earlyView, "HasRuntimePresentation"));
            Assert.AreEqual(Title, ((TMP_Text)Field(earlyView, "titleLabel")).text);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.IsTrue((bool)Property(earlyView, "IsVisible"));
        }

        void SampleAtHalfDuration()
        {
            // SlowMotion gives a 2s duration. Update adds this frame's unscaled delta.
            SetField(motion, "elapsed", 1f - Time.unscaledDeltaTime);
            motion.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(motion, null);
        }
        void SlowMotion() => Call(motion, "Configure", Visual, Group, new Vector2(0, -4), 2f, 2f);
        IEnumerator WaitForIntermediateAlpha()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Group.alpha <= 0f)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline);
                yield return null;
            }
            Assert.Less(Group.alpha, 1f, "중간 모션을 관찰하기 전에 한 프레임이 2초를 초과했습니다.");
        }
        void AssertBottomGeometry(bool nearby)
        {
            var left = ImageOf("bottomEdgeLeft"); var right = ImageOf("bottomEdgeRight");
            Assert.IsTrue(left.enabled); Assert.AreEqual(nearby, right.enabled);
            AssertRect(left.rectTransform, new Vector2(12, Body.y - 4), new Vector2(nearby ? Tail - 20 : Body.x - 24, 4));
            AssertRect(right.rectTransform, new Vector2(Tail + 8, Body.y - 4), new Vector2(Body.x - Tail - 20, 4));
            foreach (var rail in new[] { left, right })
            { Assert.AreEqual(Image.Type.Simple, rail.type); Assert.IsFalse(rail.preserveAspect); Assert.AreEqual(4f, rail.rectTransform.sizeDelta.y); }
            foreach (string field in new[] { "tailBody", "tailFrame" })
            {
                var tail = ImageOf(field); Assert.AreEqual(nearby, tail.enabled);
                AssertRect(tail.rectTransform, new Vector2(Tail - 8, Body.y - 3.5f), new Vector2(16, 12));
            }
        }
        static void AssertRect(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            Assert.AreEqual(Vector2.up, rect.anchorMin); Assert.AreEqual(Vector2.up, rect.anchorMax);
            Assert.AreEqual(Vector2.up, rect.pivot); Assert.AreEqual(size, rect.sizeDelta);
            Assert.AreEqual(new Vector2(topLeft.x, -topLeft.y), rect.anchoredPosition);
        }
        void AssertHidden()
        {
            Assert.IsFalse((bool)Property(view, "HasRuntimePresentation")); Assert.IsFalse((bool)Property(view, "IsVisible"));
            Assert.AreEqual(0f, Group.alpha); Assert.IsFalse(Visual.gameObject.activeSelf);
        }
        void AssertCleared()
        { Assert.AreEqual(string.Empty, Label("titleLabel").text); Assert.AreEqual(string.Empty, Label("descriptionLabel").text); }
        bool Nearby(string title = Title, string description = Description, Vector2? position = null, float width = 384, float? tail = null)
            => (bool)Call(view, "ShowNearby", title, description, position ?? Position, width, tail);
        bool Panel(string title = Title, string description = Description, Vector2? position = null, float width = 384)
            => (bool)Call(view, "ShowPanel", title, description, position ?? Position, width);
        bool SetPosition(Vector2 position) => (bool)Call(view, "SetPosition", position);
        bool SetTail(float value) => (bool)Call(view, "SetTailCenter", value);
        TMP_Text Label(string field) => (TMP_Text)Field(view, field);
        Image ImageOf(string field) => (Image)Field(view, field);
        internal static MonoBehaviour Find(GameObject root, string type) => root.GetComponentsInChildren<MonoBehaviour>(true)
            .Single(x => x != null && x.GetType().FullName == RuntimeNamespace + type);
        static object Field(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(field, name); return field.GetValue(target);
        }
        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name); field.SetValue(target, value);
        }
        static object Property(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property, name); return property.GetValue(target);
        }
        internal static object Call(object target, string name, params object[] args)
        {
            // Runtime public method names are unique; Invoke also accepts boxed float for float?.
            var method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method, name); return method.Invoke(target, args);
        }
    }

    [DefaultExecutionOrder(-32000)]
    public sealed class TooltipEarlyAwakePresenter : MonoBehaviour
    {
        public bool ShowAccepted { get; private set; }
        void Awake()
        {
            var view = TooltipPlayTests.Find(gameObject, "TooltipView");
            ShowAccepted = (bool)TooltipPlayTests.Call(view, "ShowNearby", TooltipPlayTests.Title,
                TooltipPlayTests.Description, new Vector2(48, 408), 384f, null);
        }
    }
}
