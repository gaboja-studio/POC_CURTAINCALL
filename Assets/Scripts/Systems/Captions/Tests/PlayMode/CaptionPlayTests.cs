using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.Captions.Tests
{
    public sealed class CaptionPlayTests
    {
        const string Prefab = "Prefabs/UIs/Captions/Captions";
        const string DialogueBody = "잠깐, 저쪽 문 뒤에서 무슨 소리가 났어.";
        const string BroadcastBody = "공연 준비가 끝났습니다.\n단원들은 무대로 모여 주십시오.";
        GameObject instance;
        MonoBehaviour view;
        float previousScale;
        CanvasGroup Group => instance.GetComponentsInChildren<CanvasGroup>(true).Single();
        RectTransform Root => (RectTransform)view.transform;
        TMP_Text Body => instance.GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "50_BodyTMP");
        TMP_Text Speaker => instance.GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "50_NameTMP");
        Transform SpeakerRoot => Speaker.transform.parent;
        Image Icon => SpeakerRoot.GetComponentsInChildren<Image>(true).Single(x => x.name == "40_BroadcastIcon");
        object Call(string name, params object[] args) => view.GetType().GetMethod(name).Invoke(view, args);
        bool Flag(string name) => (bool)view.GetType().GetProperty(name).GetValue(view);
        bool Dialogue(string body = DialogueBody, string speaker = "단원 2") => (bool)Call("TryShowDialogue", speaker, body);
        bool Broadcast(string body = BroadcastBody, string speaker = "단장 방송") => (bool)Call("TryShowBroadcast", speaker, body);
        bool Width(float strip, float speaker) => (bool)Call("TrySetWidths", strip, speaker);
        bool Fits(int kind, string body, string speaker = "단원 2")
        {
            var method = view.GetType().GetMethod("CanFit");
            return (bool)method.Invoke(view, new[] { Enum.ToObject(method.GetParameters()[0].ParameterType, kind), speaker, body });
        }
        void Hidden()
        {
            Assert.IsFalse(Flag("IsVisible")); Assert.IsFalse(Flag("HasRuntimePresentation")); Assert.AreEqual(0, Group.alpha);
            Assert.IsTrue(view.gameObject.activeSelf);
        }
        void HasGlyphs(TMP_Text text)
        {
            Assert.AreEqual(AtlasPopulationMode.Static, text.font.atlasPopulationMode);
            Assert.IsTrue(text.font.HasCharacters(text.text.Replace("\n", "").Replace("\r", "")));
            text.ForceMeshUpdate(); Assert.Greater(text.textInfo.characterCount, 0);
        }
        Vector3[][] Mesh(bool rebuild = true)
        {
            if (rebuild) Body.ForceMeshUpdate(true, true);
            return Body.textInfo.meshInfo.Select(x => x.vertices == null ? Array.Empty<Vector3>() : x.vertices.Take(x.vertexCount).ToArray()).ToArray();
        }
        void SameMesh(Vector3[][] before)
        {
            var after = Mesh(false); Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; i++) CollectionAssert.AreEqual(before[i], after[i]);
        }
        IEnumerator Settle() { yield return new WaitForSecondsRealtime(.18f); }
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScale = Time.timeScale; Time.timeScale = 1;
            var prefab = Resources.Load<GameObject>(Prefab); Assert.NotNull(prefab);
            instance = Object.Instantiate(prefab);
            view = instance.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.CaptionsV1.CaptionView");
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            try { if (instance != null) Object.Destroy(instance); yield return null; }
            finally { Time.timeScale = previousScale; }
        }
        [UnityTest] public IEnumerator PrefabGeometryAndPassiveInput()
        {
            Hidden(); Assert.AreEqual(9, instance.GetComponentsInChildren<Image>(true).Length);
            Assert.AreEqual(2, instance.GetComponentsInChildren<TMP_Text>(true).Length);
            Assert.AreEqual(new Vector2(1120, 88), Root.sizeDelta); Assert.AreEqual(new Vector2(0, 32), Root.anchoredPosition);
            Assert.AreEqual(new Vector2(.5f, 0), Root.anchorMin); Assert.AreEqual(Root.anchorMin, Root.anchorMax); Assert.AreEqual(Root.anchorMin, Root.pivot);
            Assert.IsEmpty(instance.GetComponentsInChildren<GraphicRaycaster>(true)); Assert.IsFalse(Group.blocksRaycasts); Assert.IsFalse(Group.interactable);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(graphic.raycastTarget);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true)) foreach (var c in t.GetComponents<Component>()) Assert.NotNull(c);
            Assert.IsEmpty(Body.text); Assert.IsEmpty(Speaker.text); yield return null;
        }
        [UnityTest] public IEnumerator FirstDialogueAndNoSpeakerGeometry()
        {
            Assert.IsTrue(Dialogue()); yield return Settle(); Assert.IsTrue(Flag("IsVisible")); Assert.IsTrue(SpeakerRoot.gameObject.activeSelf); Assert.IsFalse(Icon.enabled);
            Assert.AreEqual(new Vector2(176, 48), ((RectTransform)SpeakerRoot).sizeDelta);
            Assert.AreEqual(new Vector2(224, -12), Body.rectTransform.anchoredPosition); Assert.AreEqual(new Vector2(872, 64), Body.rectTransform.sizeDelta);
            Assert.AreEqual(new Vector2(16, -10), Speaker.rectTransform.anchoredPosition); HasGlyphs(Body); HasGlyphs(Speaker);
            Assert.IsTrue(Dialogue("커튼 너머로 낡은 오르골 소리가 들려온다.", "")); yield return null;
            Assert.IsFalse(SpeakerRoot.gameObject.activeSelf); Assert.AreEqual(new Vector2(24, -12), Body.rectTransform.anchoredPosition); Assert.AreEqual(new Vector2(1072, 64), Body.rectTransform.sizeDelta);
        }
        [UnityTest] public IEnumerator BroadcastDefaultSpeakerAndActualTwoLineMesh()
        {
            Assert.IsTrue(Broadcast()); yield return Settle(); Assert.IsTrue(Icon.enabled); Assert.AreEqual(new Vector2(32, 32), Icon.rectTransform.sizeDelta);
            Assert.AreEqual(new Vector2(56, -10), Speaker.rectTransform.anchoredPosition); Assert.AreEqual(new Vector2(104, 28), Speaker.rectTransform.sizeDelta);
            Assert.AreEqual(BroadcastBody, Body.text); HasGlyphs(Body); HasGlyphs(Speaker); Assert.AreEqual(2, Body.textInfo.lineCount);
            Assert.LessOrEqual(Body.preferredHeight, 64); Assert.IsTrue(Broadcast(speaker: "")); Assert.AreEqual("방송", Speaker.text);
        }
        [UnityTest] public IEnumerator TypographyAndLongSpeakerAreLiteralAndFixed()
        {
            Assert.IsTrue(Dialogue(@"<b>대사</b> \n 문자 그대로 …", "아주 긴 화자 이름은 화자 칸 안에서 한 줄로 말줄임됩니다")); yield return Settle();
            foreach (var label in new[] { Body, Speaker })
            {
                Assert.IsFalse(label.enableAutoSizing); Assert.IsFalse(label.richText); Assert.IsFalse(label.parseCtrlCharacters);
                Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode); HasGlyphs(label);
            }
            Assert.AreEqual(24, Body.fontSize); Assert.AreEqual(18, Speaker.fontSize); Assert.AreEqual(1, Speaker.maxVisibleLines);
            Assert.AreEqual(TextWrappingModes.NoWrap, Speaker.textWrappingMode); Assert.AreEqual(TextWrappingModes.Normal, Body.textWrappingMode);
            Assert.That(Body.text.Contains(@"<b>대사</b> \n"));
        }
        [UnityTest] public IEnumerator OverflowAndEmptyRejectWithoutReplacingCueOrMesh()
        {
            Assert.IsTrue(Dialogue()); yield return Settle(); var before = Mesh(); string text = Body.text, name = Speaker.text;
            Assert.IsFalse(Dialogue(new string('가', 800))); Assert.AreEqual(text, Body.text); Assert.AreEqual(name, Speaker.text); SameMesh(before);
            Assert.IsFalse(Dialogue(" \t\r\n")); Assert.AreEqual(text, Body.text); SameMesh(before);
            Assert.IsFalse(Dialogue("첫 번째 줄입니다.\n두 번째 줄입니다.\n대사")); Assert.AreEqual(text, Body.text); SameMesh(before);
            Assert.IsTrue(Flag("HasRuntimePresentation"));
        }
        [UnityTest] public IEnumerator SuccessfulAndRejectedCanFitPreserveVisibleMeshAndLayout()
        {
            Assert.IsTrue(Dialogue()); yield return Settle(); var before = Mesh(); var size = Body.rectTransform.sizeDelta; var position = Body.rectTransform.anchoredPosition;
            Assert.IsTrue(Fits(1, BroadcastBody, "")); SameMesh(before); Assert.AreEqual(DialogueBody, Body.text);
            Assert.IsFalse(Fits(0, new string('가', 800))); SameMesh(before); Assert.IsFalse(Fits(999, DialogueBody));
            Assert.AreEqual(size, Body.rectTransform.sizeDelta); Assert.AreEqual(position, Body.rectTransform.anchoredPosition); Assert.AreEqual(1, Group.alpha);
        }
        [UnityTest] public IEnumerator HiddenCanFitDoesNotRevealOrStoreCandidate()
        {
            Assert.IsTrue(Fits(0, DialogueBody)); Hidden(); Assert.IsEmpty(Body.text); Assert.IsFalse(Group.gameObject.activeSelf);
            Assert.IsTrue(Dialogue()); yield return Settle(); Call("Hide"); yield return Settle();
            Assert.AreEqual(DialogueBody, Body.text); Assert.IsTrue(Fits(1, BroadcastBody)); Hidden(); Assert.AreEqual(DialogueBody, Body.text);
        }
        [UnityTest] public IEnumerator IndependentWidthsAndAllInvalidValuesAreAtomic()
        {
            Assert.IsTrue(Dialogue()); Assert.IsTrue(Width(1536, 320));
            Assert.AreEqual(new Vector2(1536, 88), Root.sizeDelta); Assert.AreEqual(new Vector2(320, 48), ((RectTransform)SpeakerRoot).sizeDelta);
            Assert.AreEqual(new Vector2(368, -12), Body.rectTransform.anchoredPosition); Assert.AreEqual(new Vector2(1144, 64), Body.rectTransform.sizeDelta);
            Assert.IsTrue(Width(960, 128)); Assert.AreEqual(new Vector2(760, 64), Body.rectTransform.sizeDelta);
            foreach (var pair in new[] { new Vector2(959, 176), new Vector2(1537, 176), new Vector2(1120, 127), new Vector2(1120, 321), new Vector2(float.NaN, 176), new Vector2(1120, float.PositiveInfinity) })
            {
                Assert.IsFalse(Width(pair.x, pair.y)); Assert.AreEqual(new Vector2(960, 88), Root.sizeDelta); Assert.AreEqual(new Vector2(128, 48), ((RectTransform)SpeakerRoot).sizeDelta);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator WidthReductionRejectsWhenStoredCueWouldOverflow()
        {
            Assert.IsTrue(Width(1536, 128)); string longBody = new string('가', 80); Assert.IsTrue(Dialogue(longBody)); yield return Settle();
            var before = Mesh(); Assert.IsFalse(Width(960, 320)); Assert.AreEqual(new Vector2(1536, 88), Root.sizeDelta);
            Assert.AreEqual(128, ((RectTransform)SpeakerRoot).sizeDelta.x); Assert.AreEqual(longBody, Body.text); SameMesh(before);
        }
        [UnityTest] public IEnumerator BodyNewlinesAndSpeakerSeparatorsHaveDifferentPolicies()
        {
            Assert.IsTrue(Dialogue("첫 번째 줄입니다.\r\n두 번째 줄입니다.", "단원\r\n2\t")); yield return Settle();
            Assert.AreEqual("단원 2", Speaker.text); Assert.AreEqual("첫 번째 줄입니다.\n두 번째 줄입니다.", Body.text); Assert.AreEqual(2, Body.textInfo.lineCount);
        }
        [UnityTest] public IEnumerator RealMotionAndImmediateHideHaveDistinctContentPolicies()
        {
            Assert.IsTrue(Dialogue()); Assert.AreEqual(0, Group.alpha); yield return Settle(); Assert.AreEqual(1, Group.alpha); Assert.AreEqual(Vector2.zero, ((RectTransform)Group.transform).anchoredPosition);
            Call("Hide"); yield return Settle(); Hidden(); Assert.AreEqual(DialogueBody, Body.text);
            Call("HideImmediate"); Hidden(); Assert.IsEmpty(Body.text); Assert.IsEmpty(Speaker.text);
        }
        [UnityTest] public IEnumerator RepeatedShowAndExitReversalKeepCurrentAlphaAndPosition()
        {
            Assert.IsTrue(Dialogue()); yield return null; float entering = Group.alpha; Assert.IsTrue(Dialogue()); Assert.AreEqual(entering, Group.alpha);
            yield return Settle(); Assert.IsTrue(Broadcast()); Assert.AreEqual(1, Group.alpha); Call("Hide"); yield return null;
            float alpha = Group.alpha; Vector2 position = ((RectTransform)Group.transform).anchoredPosition;
            Assert.IsTrue(Dialogue()); Assert.AreEqual(alpha, Group.alpha); Assert.AreEqual(position, ((RectTransform)Group.transform).anchoredPosition);
            yield return Settle(); Assert.AreEqual(1, Group.alpha);
        }
        [UnityTest] public IEnumerator MotionUsesUnscaledTime()
        {
            Time.timeScale = 0; Assert.IsTrue(Dialogue()); yield return Settle(); Assert.AreEqual(1, Group.alpha);
            Call("Hide"); yield return Settle(); Hidden(); Assert.AreEqual(0, Time.timeScale);
        }
        [UnityTest] public IEnumerator DisableRejectsApisAndReenableStartsEmpty()
        {
            Assert.IsTrue(Dialogue()); yield return Settle(); view.enabled = false;
            string text = Body.text; Vector2 size = Root.sizeDelta;
            Assert.IsFalse(Dialogue()); Assert.IsFalse(Fits(0, DialogueBody)); Assert.IsFalse(Width(1536, 320)); Call("HideImmediate"); Call("ResetForRuntime");
            Assert.AreEqual(text, Body.text); Assert.AreEqual(size, Root.sizeDelta);
            view.enabled = true; yield return null; Hidden(); Assert.IsEmpty(Body.text);
            Assert.IsTrue(Dialogue()); instance.SetActive(false); Assert.IsFalse(Dialogue()); instance.SetActive(true); yield return null; Hidden();
        }
        [UnityTest] public IEnumerator EarlyAwakeShowSurvivesBothComponentAwakes()
        {
            var parent = new GameObject("C9_EarlyAwake"); parent.SetActive(false);
            try
            {
                var clone = Object.Instantiate(Resources.Load<GameObject>(Prefab), parent.transform); clone.SetActive(true);
                var target = clone.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.CaptionsV1.CaptionView");
                var presenter = target.gameObject.AddComponent<CaptionEarlyAwakePresenter>(); parent.SetActive(true);
                Assert.IsTrue(presenter.Accepted, "초기 호출 거부: " + presenter.Issue + " / Play=" + presenter.RuntimePlaying +
                    " / View=" + presenter.ViewActive + " / Motion=" + presenter.MotionActive + " / Root=" + presenter.RootActive);
                yield return Settle(); Assert.IsTrue((bool)target.GetType().GetProperty("IsVisible").GetValue(target));
            }
            finally { Object.Destroy(parent); }
        }
        [UnityTest] public IEnumerator PreviewPauseRestoresOnDisableAndCountsInput()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("CurtainCall.Captions.CaptionPreview")).First(x => x != null);
            var host = new GameObject("C9_PreviewPause"); host.SetActive(false);
            try
            {
                var preview = (MonoBehaviour)host.AddComponent(type); type.GetMethod("Configure").Invoke(preview, new object[] { view, null, null }); host.SetActive(true);
                Time.timeScale = .5f;
                type.GetMethod("TogglePause").Invoke(preview, null); Assert.AreEqual(0, Time.timeScale);
                type.GetMethod("Dialogue").Invoke(preview, null); Assert.AreEqual(2, type.GetProperty("InvocationCount").GetValue(preview));
                yield return Settle(); Assert.AreEqual(1, Group.alpha);
                host.SetActive(false); Assert.AreEqual(.5f, Time.timeScale);
            }
            finally { Object.Destroy(host); }
        }
    }
    [DefaultExecutionOrder(-32000)]
    public sealed class CaptionEarlyAwakePresenter : MonoBehaviour
    {
        public bool Accepted { get; private set; }
        public string Issue { get; private set; }
        public bool RuntimePlaying { get; private set; }
        public bool ViewActive { get; private set; }
        public bool MotionActive { get; private set; }
        public bool RootActive { get; private set; }
        void Awake()
        {
            var target = GetComponents<MonoBehaviour>().Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.CaptionsV1.CaptionView");
            var motion = GetComponents<MonoBehaviour>().Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.CaptionsV1.CaptionMotion");
            RuntimePlaying = Application.IsPlaying(target.gameObject);
            ViewActive = target.isActiveAndEnabled; MotionActive = motion.isActiveAndEnabled; RootActive = target.gameObject.activeInHierarchy;
            Accepted = (bool)target.GetType().GetMethod("TryShowDialogue").Invoke(target, new object[] { "단원 2", "잠깐, 저쪽 문 뒤에서 무슨 소리가 났어." });
            Issue = (string)target.GetType().GetProperty("LastIssue").GetValue(target);
        }
    }
}
