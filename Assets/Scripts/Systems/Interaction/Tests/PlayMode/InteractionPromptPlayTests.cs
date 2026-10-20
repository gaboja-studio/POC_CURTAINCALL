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

namespace CurtainCall.Interaction.Tests
{
    // Assembly-CSharp를 직접 참조하지 않고 실제 Resources 프리팹의 공개 계약을 호출합니다.
    public sealed class InteractionPromptPlayTests
    {
        const string ResourcePath = "Prefabs/UIs/Interaction/InteractionPrompt";
        internal const string ViewNamespace = "CurtainCall.UI.InteractionV1.";
        readonly List<GameObject> instances = new List<GameObject>();
        GameObject instance;
        MonoBehaviour view, motion;
        float previousTimeScale;
        RectTransform Visual => (RectTransform)Field(motion, "visualRoot");
        CanvasGroup Group => (CanvasGroup)Field(motion, "visualGroup");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            Assert.NotNull(prefab, "C6 Resources 프리팹 누락");
            instance = Object.Instantiate(prefab);
            instances.Add(instance);
            instance.SetActive(true);
            view = Find(instance, "InteractionPromptView");
            motion = Find(instance, "InteractionPromptMotion");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            foreach (var clone in instances) if (clone != null) Object.Destroy(clone);
            instances.Clear();
            yield return null;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator ActualPrefabWiringLayersAndInitialHiddenStateMatchContract()
        {
            Assert.NotNull(instance.GetComponent<Canvas>(), "프리팹 루트 Canvas");
            Assert.AreSame(instance.transform.Find("30_Prompts/C6_InteractionPrompt"), view.transform);
            Assert.AreSame(view.gameObject, motion.gameObject);
            Assert.AreSame(motion, Field(view, "motion"));
            Assert.AreSame(view.transform.Find("Motion"), Visual);
            Assert.AreSame(Visual.GetComponent<CanvasGroup>(), Group);
            Assert.AreEqual(new Vector2(0f, -4f), Field(motion, "hiddenOffset"));
            Assert.AreEqual(.120f, Field(motion, "enterSeconds"));
            Assert.AreEqual(.100f, Field(motion, "exitSeconds"));
            string[] layers = { "00_Shadow", "10_Body", "20_Frame", "30_Corners", "40_KeyBody", "41_KeyFrame",
                "50_HoldTrack", "51_HoldFill", "60_KeyTMP", "70_ActionTMP", "80_HelperTMP" };
            CollectionAssert.AreEqual(layers, Enumerable.Range(0, Visual.childCount).Select(i => Visual.GetChild(i).name).ToArray());
            string[] fields = { "frame", "corners", "keyFrame", "holdTrack", "holdFill", "keyLabel", "actionLabel", "helperLabel" };
            string[] nodes = { "20_Frame", "30_Corners", "41_KeyFrame", "50_HoldTrack", "51_HoldFill", "60_KeyTMP", "70_ActionTMP", "80_HelperTMP" };
            for (int i = 0; i < fields.Length; i++)
                Assert.AreSame(Visual.Find(nodes[i]).gameObject, ((Component)Field(view, fields[i])).gameObject, fields[i]);
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>()) Assert.NotNull(component, transform.name);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(graphic.raycastTarget, graphic.name);
            Assert.IsEmpty(instance.GetComponentsInChildren<GraphicRaycaster>(true));
            Assert.IsFalse(Group.interactable); Assert.IsFalse(Group.blocksRaycasts); Assert.IsFalse(Group.ignoreParentGroups);
            Assert.AreEqual(Image.Type.Filled, ImageOf("holdFill").type);
            Assert.AreEqual(Image.FillMethod.Radial360, ImageOf("holdFill").fillMethod);
            AssertHidden(); AssertCleared();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourAvailabilityAndHoldCombinationsPreserveColorsAndEmptyUnavailableRing()
        {
            for (int state = 0; state < 2; state++)
                foreach (bool hold in new[] { false, true })
                {
                    Assert.IsTrue(Show("F", "상호작용", state, hold, .65f));
                    Assert.AreEqual(hold, ImageOf("holdTrack").enabled);
                    Assert.AreEqual(hold, ImageOf("holdFill").enabled);
                    Assert.AreEqual(state == 0 && hold ? .65f : 0f, ImageOf("holdFill").fillAmount);
                    Assert.AreEqual(state == 0, ImageOf("corners").enabled);
                    Assert.AreEqual(ColorOf(state == 0 ? 0xC7D8D9 : 0x7A6A5C), (Color32)ImageOf("frame").color);
                    Assert.AreEqual(ImageOf("frame").color, ImageOf("keyFrame").color);
                    Assert.AreEqual(ColorOf(state == 0 ? 0xF0EEDC : 0xAAB0AD), (Color32)Label("keyLabel").color);
                    Assert.AreEqual(Label("keyLabel").color, Label("actionLabel").color);
                    Assert.AreEqual(1f, Label("actionLabel").color.a);
                    yield return null;
                }
        }

        [UnityTest]
        public IEnumerator DefaultAndExplicitHelpersSwitchActionBetweenCenteredAndTopRows()
        {
            Assert.IsTrue(Show()); AssertHelper("", -24f);
            Assert.IsTrue(Show(hold: true)); AssertHelper("길게 누르기", -12f);
            Assert.IsTrue(Show(state: 1)); AssertHelper("사용 불가", -12f);
            Assert.IsTrue(Show(state: 1, hold: true)); AssertHelper("사용 불가", -12f);
            Assert.IsTrue(Show(helper: "직접 안내")); AssertHelper("직접 안내", -12f);
            Assert.IsTrue(Show(helper: " \t\r\n")); AssertHelper("", -24f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiniteProgressClampsAndNewTargetsUseOnlyExplicitProgress()
        {
            Assert.IsTrue(Show(hold: true, progress: -2f)); AssertFill(0f);
            Assert.IsTrue(Show(hold: true, progress: 2f)); AssertFill(1f);
            Assert.IsTrue(Progress(-1f)); AssertFill(0f);
            Assert.IsTrue(Progress(5f)); AssertFill(1f);
            Assert.IsTrue(Show("E", "새 대상", hold: true, progress: .25f));
            AssertFill(.25f); Assert.AreEqual("E", Label("keyLabel").text);
            Assert.IsTrue(Show("F", "다음 대상", hold: true, progress: 0f)); AssertFill(0f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidShowAndProgressPreserveCurrentPresentationBeforeBlankValidation()
        {
            Assert.IsTrue(Show(hold: true, progress: .65f));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.IsFalse(Show("", "", hold: true, progress: invalid));
                Assert.IsFalse(Progress(invalid));
                AssertCurrent("F", "상호작용", .65f);
            }
            Assert.IsFalse(Show("", "", state: 99)); AssertCurrent("F", "상호작용", .65f);
            // Motion 비활성은 표시만 숨깁니다. 빈 키를 먼저 검사하면 남은 텍스트까지 잘못 지워집니다.
            motion.enabled = false;
            Assert.IsFalse(Show("", ""));
            Assert.AreEqual("상호작용", Label("actionLabel").text);
            motion.enabled = true;
            Assert.IsTrue(Show(hold: true, progress: .65f));
            Assert.IsFalse(Show(" \r\n\t", "상호작용")); AssertHidden(); AssertCleared();
            Assert.IsTrue(Show());
            Assert.IsFalse(Show("F", null)); AssertHidden(); AssertCleared();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnconfiguredComponentsRejectShowProgressAndPresentWithoutThrowing()
        {
            var root = new GameObject("C6_UnconfiguredTest");
            instances.Add(root);
            var bareMotion = root.AddComponent(motion.GetType());
            var bareView = root.AddComponent(view.GetType());
            var availability = Enum.ToObject(view.GetType().Assembly.GetType(ViewNamespace + "InteractionAvailability", true), 0);
            Assert.AreEqual(false, Call(bareMotion, "CanPresent"));
            Assert.AreEqual(false, Call(bareMotion, "Present"));
            Assert.AreEqual(false, Call(bareView, "Show", "F", "미설정", availability, true, .5f, ""));
            Assert.AreEqual(false, Call(bareView, "Show", "", "", availability, true, .5f, ""));
            Assert.AreEqual(false, Call(bareView, "SetProgress01", .5f));
            Assert.AreEqual(false, Property(bareView, "HasRuntimePresentation"));
            // 별도 미설정 컴포넌트가 실제 프리팹의 표시 상태를 오염시키지 않는지도 확인합니다.
            AssertHidden();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectedProgressIsNeverCachedAcrossHiddenNormalUnavailableOrExitingStates()
        {
            Assert.IsFalse(Progress(.9f));
            Assert.IsTrue(Show(hold: true, progress: .25f)); AssertFill(.25f);
            Assert.IsTrue(Show(progress: .3f)); Assert.IsFalse(Progress(.9f)); AssertFill(0f);
            Assert.IsTrue(Show(state: 1, hold: true, progress: .3f)); Assert.IsFalse(Progress(.9f)); AssertFill(0f);
            Assert.IsTrue(Show(hold: true, progress: .5f)); AssertFill(.5f);
            Call(view, "Hide"); Assert.IsFalse(Progress(.9f)); AssertFill(.5f);
            Assert.IsTrue(Show(hold: true, progress: .25f)); AssertFill(.25f);
            Call(view, "HideImmediate"); Assert.IsFalse(Progress(.9f)); AssertFill(0f);
            Assert.IsTrue(Show(hold: true, progress: .3f)); AssertFill(.3f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullProgressRemainsVisibleWithoutAutomaticCompletionOrLifetime()
        {
            Assert.IsTrue(Show(hold: true, progress: 1f));
            yield return new WaitForSecondsRealtime(.45f);
            AssertCurrent("F", "상호작용", 1f);
            Assert.IsTrue((bool)Property(view, "IsVisible")); Assert.AreEqual(1f, Group.alpha);
            yield return new WaitForSecondsRealtime(.35f);
            AssertCurrent("F", "상호작용", 1f); Assert.AreEqual(1f, Group.alpha);
        }

        [UnityTest]
        public IEnumerator ControlCharactersAreSingleLineAndTagsEscapesAndLongStringsStayLiteral()
        {
            string controls = "가" + (char)13 + (char)10 + "나" + (char)13 + "다" + (char)10 + "라" + (char)9 + "마" + (char)11 + "바" + (char)12 + "사" + (char)0x85 + "아" + (char)0x2028 + "자" + (char)0x2029 + "차" + (char)0 + "끝";
            const string expected = "가 나 다 라 마 바 사 아 자 차 끝";
            Assert.IsTrue(Show(controls, controls, helper: controls));
            foreach (string field in new[] { "keyLabel", "actionLabel", "helperLabel" }) Assert.AreEqual(expected, Label(field).text);
            const string literal = @"<b>문구</b>\n\t";
            string longText = string.Concat(Enumerable.Repeat("아주 긴 상호작용 ", 50));
            Assert.IsTrue(Show(literal, longText, helper: literal));
            Assert.AreEqual(literal, Label("keyLabel").text); Assert.AreEqual(literal, Label("helperLabel").text);
            Assert.AreEqual(longText, Label("actionLabel").text);
            foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.IsFalse(text.richText); Assert.IsFalse(text.parseCtrlCharacters); Assert.IsFalse(text.enableAutoSizing);
                Assert.AreEqual(TextOverflowModes.Ellipsis, text.overflowMode);
                Assert.NotNull(text.font);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedShowUpdatesContentWithoutRestartingActualEntryUpdate()
        {
            Assert.IsTrue(Show(hold: true, progress: .25f));
            float deadline = Time.realtimeSinceStartup + .8f;
            while (Group.alpha < 1f)
            {
                float alpha = Group.alpha; Vector2 position = Visual.anchoredPosition;
                Assert.IsTrue(Show("E", "교체", hold: true, progress: .75f));
                Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(position, Visual.anchoredPosition);
                Assert.Less(Time.realtimeSinceStartup, deadline, "반복 Show는 등장 시계를 재시작하지 않아야 합니다.");
                yield return null;
            }
            AssertCurrent("E", "교체", .75f);
            Assert.AreEqual(Vector2.zero, Visual.anchoredPosition);
            Assert.IsTrue(Show()); Assert.AreEqual(1f, Group.alpha);
        }

        [UnityTest]
        public IEnumerator HideAndShowReverseFromCurrentAlphaAndPositionThroughRealUpdate()
        {
            // 중간 프레임을 관찰할 여유만 늘립니다. 기본 120/100ms는 별도 테스트에서 검사합니다.
            Call(motion, "Configure", Visual, Group, new Vector2(0f, -4f), 2f, 2f);
            Assert.IsTrue(Show());
            yield return WaitForAlphaBetweenZeroAndOne();
            float alpha = Group.alpha; Vector2 position = Visual.anchoredPosition;
            Call(view, "Hide");
            Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(position, Visual.anchoredPosition);
            Assert.IsFalse((bool)Property(view, "HasRuntimePresentation"));
            yield return null;
            Assert.Less(Group.alpha, alpha);
            alpha = Group.alpha; position = Visual.anchoredPosition;
            Assert.IsTrue(Show("E", "다시 표시"));
            Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(position, Visual.anchoredPosition);
            yield return null;
            Assert.Greater(Group.alpha, alpha);
            Assert.Greater(Visual.anchoredPosition.y, position.y);
        }

        [UnityTest]
        public IEnumerator RepeatedHideDoesNotRestartExitAndDefaultBoundariesSetExactEndpoints()
        {
            Assert.IsTrue(Show());
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(1f, Group.alpha); Assert.AreEqual(Vector2.zero, Visual.anchoredPosition);
            Call(view, "Hide");
            float deadline = Time.realtimeSinceStartup + .8f;
            while (Visual.gameObject.activeSelf)
            {
                Call(view, "Hide");
                Assert.Less(Time.realtimeSinceStartup, deadline, "반복 Hide는 퇴장 시계를 재시작하지 않아야 합니다.");
                yield return null;
            }
            AssertHidden(); Assert.Less(Vector2.Distance(new Vector2(0f, -4f), Visual.anchoredPosition), .001f);
            Call(view, "Hide"); AssertHidden();
        }

        [UnityTest]
        public IEnumerator TimeScaleZeroStillRunsActualEntryAndExitUpdate()
        {
            Time.timeScale = 0f;
            Assert.IsTrue(Show()); Assert.AreEqual(0f, Group.alpha);
            Assert.IsTrue((bool)Property(view, "HasRuntimePresentation"));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(1f, Group.alpha); Assert.AreEqual(Vector2.zero, Visual.anchoredPosition);
            Call(view, "Hide");
            yield return new WaitForSecondsRealtime(.3f);
            AssertHidden(); Assert.AreEqual(0f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator ViewMotionAndParentDisableNeverResurrectPresentationOnReenable()
        {
            foreach (string target in new[] { "view", "motion", "parent" })
            {
                Assert.IsTrue(Show(hold: true, progress: .65f));
                yield return null;
                if (target == "parent") instance.SetActive(false);
                else (target == "view" ? view : motion).enabled = false;
                AssertHidden(); Assert.IsFalse(Show("", "")); Assert.IsFalse(Progress(.9f));
                if (target == "parent") instance.SetActive(true);
                else (target == "view" ? view : motion).enabled = true;
                yield return null;
                AssertHidden();
                if (target != "motion") AssertCleared();
                // Motion 단독 disable은 View 텍스트 초기화까지 보장하지 않지만 이전 진행률을 재사용해서는 안 됩니다.
                Assert.IsTrue(Show(hold: true, progress: .25f)); AssertFill(.25f);
                Call(view, "ResetForRuntime"); AssertHidden(); AssertCleared();
            }
        }

        [UnityTest]
        public IEnumerator ImmediateHideAndResetClearTextProgressAndCanReopen()
        {
            foreach (string method in new[] { "HideImmediate", "ResetForRuntime" })
            {
                Assert.IsTrue(Show(hold: true, progress: .75f, helper: "안내"));
                Call(view, method); AssertHidden(); AssertCleared();
                Assert.IsTrue(view.gameObject.activeSelf); Assert.IsTrue(view.enabled); Assert.IsTrue(motion.enabled);
                Assert.IsTrue(Show());
                yield return null;
                Assert.IsTrue((bool)Property(view, "HasRuntimePresentation"));
            }
        }

        [UnityTest]
        public IEnumerator EarlierAwakeShowSurvivesViewAndMotionAwakeOnActualPrefabActivation()
        {
            var parent = new GameObject("C6_EarlyAwakeTest");
            instances.Add(parent); parent.SetActive(false);
            var clone = Object.Instantiate(Resources.Load<GameObject>(ResourcePath), parent.transform);
            clone.SetActive(true);
            var presenter = Find(clone, "InteractionPromptView").gameObject.AddComponent<InteractionPromptEarlyAwakePresenter>();
            parent.SetActive(true);
            Assert.IsTrue(presenter.ShowAccepted, "선행 Awake의 Show가 거부되었습니다.");
            var earlyView = Find(clone, "InteractionPromptView");
            Assert.IsTrue((bool)Property(earlyView, "HasRuntimePresentation"));
            Assert.AreEqual("선행 표시", ((TMP_Text)Field(earlyView, "actionLabel")).text);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.IsTrue((bool)Property(earlyView, "IsVisible"));
        }

        IEnumerator WaitForAlphaBetweenZeroAndOne()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Group.alpha <= 0f)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "실제 Update가 등장 모션을 진행하지 않습니다.");
                yield return null;
            }
            Assert.Less(Group.alpha, 1f, "한 프레임이 2초를 초과해 중간 전환을 관찰할 수 없습니다.");
        }
        void AssertHelper(string expected, float y)
        {
            Assert.AreEqual(expected, Label("helperLabel").text);
            Assert.AreEqual(expected.Length > 0, Label("helperLabel").gameObject.activeSelf);
            Assert.AreEqual(new Vector2(92f, y), Label("actionLabel").rectTransform.anchoredPosition);
        }
        void AssertCurrent(string key, string action, float progress)
        {
            Assert.AreEqual(key, Label("keyLabel").text); Assert.AreEqual(action, Label("actionLabel").text);
            AssertFill(progress); Assert.IsTrue((bool)Property(view, "HasRuntimePresentation"));
        }
        void AssertFill(float expected) => Assert.AreEqual(expected, ImageOf("holdFill").fillAmount, .0001f);
        void AssertHidden()
        {
            Assert.IsFalse((bool)Property(view, "HasRuntimePresentation"));
            Assert.IsFalse((bool)Property(view, "IsVisible"));
            Assert.AreEqual(0f, Group.alpha); Assert.IsFalse(Visual.gameObject.activeSelf);
        }
        void AssertCleared()
        {
            AssertFill(0f); Assert.IsFalse(ImageOf("holdTrack").enabled); Assert.IsFalse(ImageOf("holdFill").enabled);
            foreach (string field in new[] { "keyLabel", "actionLabel", "helperLabel" }) Assert.AreEqual(string.Empty, Label(field).text);
            Assert.IsFalse(Label("helperLabel").gameObject.activeSelf);
        }
        bool Show(string key = "F", string action = "상호작용", int state = 0, bool hold = false, float progress = 0f, string helper = "")
        {
            var availability = Enum.ToObject(view.GetType().Assembly.GetType(ViewNamespace + "InteractionAvailability", true), state);
            return (bool)Call(view, "Show", key, action, availability, hold, progress, helper);
        }
        bool Progress(float value) => (bool)Call(view, "SetProgress01", value);
        TMP_Text Label(string name) => (TMP_Text)Field(view, name);
        Image ImageOf(string name) => (Image)Field(view, name);
        static Color32 ColorOf(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        internal static MonoBehaviour Find(GameObject root, string type) => root.GetComponentsInChildren<MonoBehaviour>(true)
            .Single(x => x != null && x.GetType().FullName == ViewNamespace + type);
        static object Field(object target, string name)
        {
            Assert.NotNull(target);
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, name); return field.GetValue(target);
        }
        static object Property(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property, name); return property.GetValue(target);
        }
        internal static object Call(object target, string name, params object[] args)
        {
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == name && !x.ContainsGenericParameters && x.GetParameters().Length == args.Length)
                .Where(x => x.GetParameters().Select((p, i) => args[i] == null ? !p.ParameterType.IsValueType : p.ParameterType.IsInstanceOfType(args[i])).All(x => x)).ToArray();
            Assert.AreEqual(1, methods.Length, target.GetType().Name + "." + name);
            return methods[0].Invoke(target, args);
        }
    }

    // 프리팹을 비활성 부모 아래 만든 뒤 활성화하여 View/Motion보다 먼저 실제 Awake에서 호출합니다.
    [DefaultExecutionOrder(-32000)]
    public sealed class InteractionPromptEarlyAwakePresenter : MonoBehaviour
    {
        public bool ShowAccepted { get; private set; }
        void Awake()
        {
            var view = InteractionPromptPlayTests.Find(gameObject, "InteractionPromptView");
            var availability = Enum.ToObject(view.GetType().Assembly.GetType(InteractionPromptPlayTests.ViewNamespace + "InteractionAvailability", true), 0);
            ShowAccepted = (bool)InteractionPromptPlayTests.Call(view, "Show", "F", "선행 표시", availability, true, .25f, "");
        }
    }
}
