using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.ResultsUi.Tests
{
    /// <summary>C12 표시 API만 리플렉션으로 검사한다. 결과 판정·정산·게임 흐름은 연결하지 않는다.</summary>
    public sealed class ResultsUiPlayTests
    {
        GameObject instance, ownedEvents;
        MonoBehaviour view;
        EventSystem events;
        float originalScale;
        bool originalBackground;
        readonly List<GameObject> extra = new List<GameObject>();
        readonly List<FontSnapshot> fontSnapshots = new List<FontSnapshot>();
        sealed class FontSnapshot
        {
            public TMP_FontAsset Font;
            public uint[] Characters, GlyphIndices, Glyphs;
            public Material Material;
            public Texture2D[] Atlases;
            public TMP_FontAsset[] Fallbacks;
            public string Path, Hash;
        }
        static Type Domain(string name) => Type.GetType("CurtainCall.UI." + name + ", Assembly-CSharp", true);
        static Type PreviewType => Domain("ResultsV1.ResultsUiPreview");
        static object Property(object source, string name) => source.GetType().GetProperty(name).GetValue(source);
        static object Field(object source, string name) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(source);
        static void SetField(object source, string name, object value) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(source, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
        object Call(string method, params object[] args) => Invoke(view, method, args);
        bool Visible => (bool)Property(view, "IsVisible");
        CanvasGroup Visuals => (CanvasGroup)Field(view, "visuals");
        Array Members => (Array)Field(view, "memberLayers");
        object MemberLayer(int index) => Members.GetValue(index);
        object Texts => Field(view, "textLayers");
        TMP_Text Text(string name) => (TMP_Text)Field(Texts, name);
        Image Image(int index, string name) => (Image)Field(MemberLayer(index), name);
        TMP_Text MemberText(int index, string name) => (TMP_Text)Field(MemberLayer(index), name);
        Button Confirm => (Button)Field(view, "acknowledge");
        MonoBehaviour Transfer => (MonoBehaviour)Field(view, "rewardTransfer");
        Image[] Pieces => (Image[])Field(Transfer, "pieces");
        RectTransform Destination => (RectTransform)Field(Transfer, "destination");
        bool Playing => (bool)Property(Transfer, "IsPlaying");
        static object Sample(string kind = "grand", string revision = "sample", string session = "results-preview", bool enabled = true, bool label = true)
            => PreviewType.GetMethod("CreateSample").Invoke(null, new object[] { kind, revision, session, enabled, label });
        static object Outcome(int value) => Enum.ToObject(Domain("ResultsOutcome"), value);
        static object Member(int id = 1, string label = "단원 1", float? progress = .5f, bool local = false, Color? color = null, float? fall = null)
            => Activator.CreateInstance(Domain("ResultsMemberPresentation"), id, label, progress, "80 m", color ?? Color.blue, local, fall);
        static Array Typed(params object[] values)
        { var result = Array.CreateInstance(Domain("ResultsMemberPresentation"), values.Length); for (int i = 0; i < values.Length; i++) result.SetValue(values[i], i); return result; }
        static object[] ModelArguments(string session = "results-preview", string revision = "test", string title = "성공", Array members = null, int outcome = 1)
        {
            return new object[] { session, revision, Outcome(outcome), title, "1일차\n외줄타기", "공연을 무사히 마쳤습니다.", "단원별 도달 거리", "전체 160 m", 1f,
                "기본 보상", "200", "추가 보상", "40", "공동 자금", "이번 공연 +240", "2,960", "확인 후 다음 안내를 기다려 주세요.", "확인", true,
                members ?? Typed(Member(), Member(2)) };
        }
        static object Model(string session = "results-preview", string revision = "test", Array members = null)
            => Activator.CreateInstance(Domain("ResultsPresentation"), ModelArguments(session, revision, members: members));
        void Open(object model = null, bool focus = false) => Call("Open", model ?? Sample(), focus);
        static void Invalid(Action action)
        { var error = Assert.Throws<TargetInvocationException>(() => action()); Assert.IsInstanceOf<ArgumentException>(error.InnerException); }
        void Subscribe(string name, Delegate action) => view.GetType().GetEvent(name).AddEventHandler(view, action);
        void Unsubscribe(string name, Delegate action) => view.GetType().GetEvent(name).RemoveEventHandler(view, action);
        static string FileHash(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(System.IO.File.ReadAllBytes(path)));
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            originalScale = Time.timeScale; originalBackground = Application.runInBackground; Time.timeScale = 1; extra.Clear(); fontSnapshots.Clear();
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/ResultsUi/ResultsPanel"); Assert.NotNull(prefab, "C12 소유 프리팹을 먼저 조립해야 합니다.");
            instance = Object.Instantiate(prefab);
            view = instance.GetComponentsInChildren<MonoBehaviour>(true).Single(b => b != null && b.GetType().FullName == "CurtainCall.UI.ResultsPanelView");
            events = EventSystem.current;
            if (events == null) { ownedEvents = new GameObject("ResultsTestEventSystem", typeof(EventSystem)); events = ownedEvents.GetComponent<EventSystem>(); }
            var fonts = instance.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).ToList();
            foreach (string weight in new[] { "Medium", "SemiBold", "Bold" })
            { var shared = Resources.Load<TMP_FontAsset>("Fonts/PerformanceHud/Pretendard-" + weight + " SDF"); Assert.NotNull(shared); fonts.Add(shared); }
            foreach (var font in fonts.Distinct())
            {
                var snapshot = new FontSnapshot { Font = font, Characters = font.characterTable.Select(c => c.unicode).ToArray(), GlyphIndices = font.characterTable.Select(c => c.glyphIndex).ToArray(),
                    Glyphs = font.glyphTable.Select(g => g.index).ToArray(), Material = font.material, Atlases = font.atlasTextures.ToArray(), Fallbacks = font.fallbackFontAssetTable == null ? new TMP_FontAsset[0] : font.fallbackFontAssetTable.ToArray() };
#if UNITY_EDITOR
                snapshot.Path = UnityEditor.AssetDatabase.GetAssetPath(font); snapshot.Hash = FileHash(snapshot.Path);
#endif
                fontSnapshots.Add(snapshot);
            }
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var go in extra) if (go != null) Object.Destroy(go);
            if (instance != null) Object.Destroy(instance); if (ownedEvents != null) Object.Destroy(ownedEvents);
            yield return null;
            Time.timeScale = originalScale; Application.runInBackground = originalBackground;
            foreach (var snapshot in fontSnapshots)
            {
                CollectionAssert.AreEqual(snapshot.Characters, snapshot.Font.characterTable.Select(c => c.unicode).ToArray(), "기존 폰트 문자표 불변");
                CollectionAssert.AreEqual(snapshot.GlyphIndices, snapshot.Font.characterTable.Select(c => c.glyphIndex).ToArray());
                CollectionAssert.AreEqual(snapshot.Glyphs, snapshot.Font.glyphTable.Select(g => g.index).ToArray()); Assert.AreSame(snapshot.Material, snapshot.Font.material);
                CollectionAssert.AreEqual(snapshot.Atlases, snapshot.Font.atlasTextures); CollectionAssert.AreEqual(snapshot.Fallbacks, snapshot.Font.fallbackFontAssetTable ?? new List<TMP_FontAsset>());
                if (snapshot.Path != null) Assert.AreEqual(snapshot.Hash, FileHash(snapshot.Path), "공유/사용 폰트 asset SHA 불변");
            }
        }
        [UnityTest] public IEnumerator InitialHiddenAndCanvasContract()
        {
            Assert.IsFalse(Visible); Assert.IsFalse(Visuals.gameObject.activeSelf); Assert.AreEqual(0, Visuals.alpha); Assert.IsFalse(Visuals.interactable); Assert.IsFalse(Visuals.blocksRaycasts);
            var canvas = instance.GetComponent<Canvas>(); Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode); Assert.AreEqual(100, canvas.referencePixelsPerUnit);
            var scaler = canvas.GetComponent<CanvasScaler>(); Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution); Assert.AreEqual(.5f, scaler.matchWidthOrHeight); Assert.AreEqual(100, scaler.referencePixelsPerUnit);
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode); Assert.NotNull(canvas.GetComponent<GraphicRaycaster>());
            Assert.AreEqual(8, Pieces.Length); Assert.IsFalse(Playing); Assert.IsTrue(Pieces.All(p => !p.gameObject.activeSelf)); yield return null;
        }
        [UnityTest] public IEnumerator GeometryReferencesAndNoFullscreenDim()
        {
            var stage = (RectTransform)Field(view, "layoutStage"); var panel = (RectTransform)stage.Find("00_ResultsPanel");
            Assert.AreEqual(new Vector2(400, -256), panel.anchoredPosition); Assert.AreEqual(new Vector2(1120, 640), panel.sizeDelta);
            var title = panel.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "02_TitleTMP");
            var subtitle = panel.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "04_SubtitleTMP");
            Assert.AreEqual(28, -subtitle.anchoredPosition.y + title.anchoredPosition.y - title.sizeDelta.y);
            Assert.AreEqual(new Vector2(896, -560), ((RectTransform)Confirm.transform).anchoredPosition); Assert.AreEqual(new Vector2(192, 56), ((RectTransform)Confirm.transform).sizeDelta);
            foreach (var node in instance.GetComponentsInChildren<Transform>(true)) foreach (var component in node.GetComponents<Component>()) Assert.NotNull(component, node.name);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                for (Type type = behaviour.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if ((field.IsPublic || field.IsDefined(typeof(SerializeField), true)) && field.GetValue(behaviour) is Component reference)
                            Assert.IsTrue(reference.transform == instance.transform || reference.transform.IsChildOf(instance.transform), field.Name);
            Open(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = new Vector2(4, 4) }, hits);
            Assert.IsFalse(hits.Any(h => h.gameObject.transform.IsChildOf(instance.transform)));
            var content = panel.Find("40_Content"); CollectionAssert.AreEqual(new[] { "00_Art", "70_CoinEffects", "80_TMP", "90_Acknowledge" }, content.Cast<Transform>().Select(t => t.name));
        }
        [UnityTest] public IEnumerator DtoCopiesMembersAndExposesReadOnlyCollection()
        {
            var member = Member(); var input = Typed(member, Member(2)); var model = Model(members: input); input.SetValue(Member(3), 0);
            var members = (IList)Property(model, "Members"); Assert.IsTrue(members.IsReadOnly); Assert.AreEqual(1, Property(members[0], "Id")); Assert.AreNotSame(member, members[0]);
            Open(model); var rendered = Field(view, "model"); Assert.AreNotSame(model, rendered); Assert.AreNotSame(members[0], ((IList)Property(rendered, "Members"))[0]);
            foreach (var property in Domain("ResultsPresentation").GetProperties()) Assert.IsFalse(property.GetSetMethod() != null);
            foreach (var property in Domain("ResultsMemberPresentation").GetProperties()) Assert.IsFalse(property.GetSetMethod() != null); yield return null;
        }
        [UnityTest] public IEnumerator RequiredStringsAndOutcomeAreValidated()
        {
            Invalid(() => Activator.CreateInstance(Domain("ResultsPresentation"), ModelArguments(session: " ")));
            Invalid(() => Activator.CreateInstance(Domain("ResultsPresentation"), ModelArguments(revision: " ")));
            Invalid(() => Activator.CreateInstance(Domain("ResultsPresentation"), ModelArguments(title: " ")));
            Invalid(() => Activator.CreateInstance(Domain("ResultsPresentation"), ModelArguments(outcome: 99)));
            Invalid(() => Member(label: " ")); Invalid(() => Member(0)); Invalid(() => Member(5));
            Invalid(() => Member(color: new Color(float.NaN, 0, 0))); Invalid(() => Member(color: new Color(0, float.PositiveInfinity, 0))); Invalid(() => Member(color: new Color(-.1f, 0, 0)));
            yield return null;
        }
        [UnityTest] public IEnumerator MemberCountUniqueIdentityAndAtMostOneLocal()
        {
            Invalid(() => Model(members: Typed())); Invalid(() => Model(members: Typed(Member())));
            Invalid(() => Model(members: Typed(Member(), Member(2), Member(3), Member(4), Member())));
            Invalid(() => Model(members: Typed(Member(), Member()))); Invalid(() => Model(members: Typed(Member(), null)));
            Invalid(() => Model(members: Typed(Member(local: true), Member(2, local: true))));
            var args = ModelArguments(); args[19] = null; Invalid(() => Activator.CreateInstance(Domain("ResultsPresentation"), args));
            Assert.DoesNotThrow(() => Model(members: Typed(Member(), Member(2)))); yield return null;
        }
        [UnityTest] public IEnumerator OptionalStringsNormalizeAndColorAlphaIsOpaque()
        {
            var args = ModelArguments(); foreach (int index in new[] { 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15, 16, 17 }) args[index] = null;
            var model = Activator.CreateInstance(Domain("ResultsPresentation"), args);
            foreach (string name in new[] { "Context", "Subtitle", "ProgressHeading", "CourseLabel", "BaseLabel", "BaseAmount", "BonusLabel", "BonusAmount", "FundsLabel", "ContributionText", "FundsAmount", "Footer", "AcknowledgeLabel" }) Assert.AreEqual("", Property(model, name));
            Assert.AreEqual(1f, ((Color)Property(Member(color: new Color(.2f, .3f, .4f, 0)), "Color")).a); yield return null;
        }
        [UnityTest] public IEnumerator ThreeOutcomesUseExternalCopyAndExpectedRibbon()
        {
            string[] kinds = { "failure", "success", "grand" }; string[] titles = { "실패", "성공", "대성공" }; string[] ribbons = { "failureRibbon", "successRibbon", "grandRibbon" };
            for (int i = 0; i < kinds.Length; i++)
            { Open(Sample(kinds[i])); Assert.AreEqual(titles[i], Text("title").text); Assert.AreSame(Field(view, ribbons[i]), ((Image)Field(view, "ribbon")).sprite); Assert.AreEqual(i == 2, ((Image)Field(view, "grandCrest")).enabled); }
            var args = ModelArguments(title: "실패 성공", outcome: 0); Open(Activator.CreateInstance(Domain("ResultsPresentation"), args)); Assert.AreEqual("실패 성공", Text("title").text); yield return null;
        }
        [UnityTest] public IEnumerator DisplayStringsAndInputOrderRemainExactWithoutRewardCalculation()
        {
            var args = ModelArguments(members: Typed(Member(4, "단원 4", .25f), Member(2, "단원 2", .75f)));
            args[10] = "9,999"; args[12] = "-80"; args[14] = "이번 공연 +0"; args[15] = "0";
            var model = Activator.CreateInstance(Domain("ResultsPresentation"), args); Open(model);
            string[] fields = { "title", "context", "subtitle", "progressHeading", "course", "baseLabel", "baseAmount", "bonusLabel", "bonusAmount", "fundsLabel", "contribution", "fundsAmount", "footer", "acknowledge" };
            int[] indices = { 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15, 16, 17 };
            for (int i = 0; i < fields.Length; i++) Assert.AreEqual(args[indices[i]], Text(fields[i]).text);
            Assert.AreEqual("4", MemberText(0, "number").text); Assert.AreEqual("2", MemberText(1, "number").text);
            Assert.AreEqual("단원 4", MemberText(0, "label").text); Assert.AreEqual("80 m", MemberText(0, "distance").text);
            Assert.AreEqual("9,999", Property(model, "BaseAmount")); yield return null;
        }
        [UnityTest] public IEnumerator TwoThreeFourMembersAndLocalZeroOneDoNotLeaveStaleRows()
        {
            foreach (var pair in new[] { ("four", 4), ("two", 2), ("three", 3), ("four", 4) })
            {
                Open(Sample(pair.Item1));
                for (int i = 0; i < 4; i++)
                { bool active = i < pair.Item2; Assert.AreEqual(active, ((RectTransform)Field(MemberLayer(i), "root")).gameObject.activeSelf); Assert.AreEqual(active, MemberText(i, "label").enabled); Assert.AreEqual(active, MemberText(i, "distance").enabled); Assert.AreEqual(active, Image(i, "connector").enabled); Assert.AreEqual(active, MemberText(i, "number").enabled); }
            }
            Open(Sample("no-local")); for (int i = 0; i < 4; i++) Assert.AreEqual(new Vector2(24, 24), Image(i, "marker").rectTransform.sizeDelta);
            Open(); Assert.AreEqual(new Vector2(32, 32), Image(0, "marker").rectTransform.sizeDelta); Assert.AreSame(Field(view, "selfMarker"), Image(0, "marker").sprite); yield return null;
        }
        [UnityTest] public IEnumerator OutsidePositionsClampWithoutChangingRawValuesOrLabels()
        {
            var model = Sample("outside"); Open(model);
            for (int i = 0; i < 4; i++)
            { var marker = Image(i, "marker").rectTransform; Assert.AreEqual(i % 2 == 0 ? 256 : 896, marker.anchoredPosition.x + marker.sizeDelta.x / 2); Assert.AreEqual(i % 2 == 0 ? -2f : 2f, Property(((IList)Property(model, "Members"))[i], "NormalizedProgress")); }
            Assert.AreEqual(888, ((Image)Field(view, "maximumMarker")).rectTransform.anchoredPosition.x); Assert.AreEqual("160 m", MemberText(0, "distance").text); yield return null;
        }
        [UnityTest] public IEnumerator NullNaNAndBothInfinitiesHideOnlyPositionElements()
        {
            Open(Sample("missing")); Assert.IsFalse(((Image)Field(view, "maximumMarker")).enabled);
            for (int i = 0; i < 4; i++)
            { Assert.IsFalse(Image(i, "marker").enabled); Assert.IsFalse(Image(i, "connector").enabled); Assert.IsFalse(Image(i, "fall").enabled); Assert.IsFalse(MemberText(i, "number").enabled); Assert.IsTrue(MemberText(i, "label").enabled); Assert.IsNotEmpty(MemberText(i, "distance").text); }
            yield return null;
        }
        [UnityTest] public IEnumerator OverlapKeepsTrueXAndOffsetsOnlyFallY()
        {
            Open(Sample("overlap"));
            for (int i = 0; i < 4; i++)
            {
                var marker = Image(i, "marker").rectTransform; var fall = Image(i, "fall").rectTransform;
                Assert.AreEqual(576, marker.anchoredPosition.x + marker.sizeDelta.x / 2); Assert.AreEqual(576, fall.anchoredPosition.x + 10);
                Assert.AreEqual(-(260 + i * 40 + 14) + 10, fall.anchoredPosition.y); Assert.AreEqual((i + 1).ToString(), MemberText(i, "number").text);
                Assert.AreEqual(new Vector2(2, 36 + i * 40), Image(i, "connector").rectTransform.sizeDelta);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator FontsAreStaticCompleteAndTextActuallyRenders()
        {
            Open(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
            foreach (var label in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.AreEqual(AtlasPopulationMode.Static, label.font.atlasPopulationMode); Assert.IsFalse(label.font.isMultiAtlasTexturesEnabled); Assert.AreEqual(1, label.font.atlasTextures.Length);
                Assert.IsTrue(label.font.fallbackFontAssetTable == null || label.font.fallbackFontAssetTable.Count == 0);
                Assert.IsFalse(label.richText); Assert.IsFalse(label.parseCtrlCharacters); Assert.IsFalse(label.enableAutoSizing); Assert.IsFalse(label.raycastTarget); Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
                foreach (char c in label.text.Where(c => !char.IsControl(c))) Assert.IsTrue(label.font.HasCharacter(c, false, false), label.name + " 글리프 누락 " + c);
                if (!label.isActiveAndEnabled || string.IsNullOrEmpty(label.text)) continue;
                label.ForceMeshUpdate(); Assert.IsTrue(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.isVisible), label.name + " 실제 렌더 없음");
            }
        }
        [UnityTest] public IEnumerator LongCopyIsLiteralAndActuallyEllipsizes()
        {
            Open(Sample("text")); yield return null; Canvas.ForceUpdateCanvases(); var label = MemberText(0, "label"); label.ForceMeshUpdate();
            Assert.IsTrue(label.text.StartsWith("<b>")); Assert.AreEqual(24, label.fontSize); Assert.IsFalse(label.richText); Assert.IsFalse(label.enableAutoSizing);
            Assert.IsTrue(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.character == '…' && c.isVisible));
        }
        [UnityTest] public IEnumerator SameSessionAcceptsCallerRevisionAndOtherSessionsAreRejected()
        {
            Assert.IsFalse((bool)Call("SetPresentation", Sample())); Open(Sample(revision: "revision-20")); var before = Field(view, "model");
            Assert.IsFalse((bool)Call("SetPresentation", Sample(session: "other"))); Assert.AreSame(before, Field(view, "model"));
            Assert.IsTrue((bool)Call("SetPresentation", Sample(revision: "revision-01"))); Assert.AreEqual("revision-01", Property(Field(view, "model"), "Revision"));
            Assert.IsTrue((bool)Call("SetPresentation", Sample(revision: "revision-01"))); yield return null;
        }
        [UnityTest] public IEnumerator AcknowledgeClickSubmitAndRepeatedIntentNeverAutoHide()
        {
            int count = 0; Action<string> handler = session => { Assert.AreEqual("results-preview", session); count++; }; Subscribe("AcknowledgeRequested", handler);
            Open(focus: true); Assert.AreEqual(Confirm.gameObject, events.currentSelectedGameObject);
            ExecuteEvents.Execute(Confirm.gameObject, new PointerEventData(events) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(Confirm.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Call("Acknowledge");
            Assert.AreEqual(3, count); Assert.IsTrue(Visible); yield return null;
        }
        [UnityTest] public IEnumerator DisabledAndEmptyAcknowledgeRejectClickSubmitAndDirectIntent()
        {
            int count = 0; Subscribe("AcknowledgeRequested", (Action<string>)(_ => count++));
            foreach (var model in new[] { Sample(enabled: false), Sample(label: false) })
            {
                Open(model); ExecuteEvents.Execute(Confirm.gameObject, new PointerEventData(events), ExecuteEvents.pointerClickHandler); ExecuteEvents.Execute(Confirm.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Call("Acknowledge");
                Assert.AreEqual(0, count); Assert.IsTrue(Visible);
            }
            Open(); Confirm.interactable = false; Call("Acknowledge"); Assert.AreEqual(0, count); Confirm.interactable = true;
            Confirm.gameObject.SetActive(false); Call("Acknowledge"); Assert.AreEqual(0, count); Confirm.gameObject.SetActive(true);
            Visuals.interactable = false; Call("Acknowledge"); Assert.AreEqual(0, count); Visuals.interactable = true;
            Call("Hide"); Call("Acknowledge"); Assert.AreEqual(0, count); yield return null;
        }
        [UnityTest] public IEnumerator AcknowledgeReentryCanRefreshDisableAndReopenWithoutRecursiveIntent()
        {
            int count = 0; Action<string> handler = _ => { count++; Call("Acknowledge"); Call("SetPresentation", Sample(enabled: false)); Open(Sample("success")); Call("Acknowledge"); };
            Subscribe("AcknowledgeRequested", handler); Open(); Call("Acknowledge"); Assert.AreEqual(1, count); Assert.IsTrue(Visible); Assert.AreEqual("성공", Text("title").text);
            Unsubscribe("AcknowledgeRequested", handler); yield return null;
        }
        [UnityTest] public IEnumerator HiddenIsImmediateAndCallbackReopenSurvivesOldFade()
        {
            int hidden = 0; Subscribe("Hidden", (Action)(() => hidden++)); Open(focus: true); Call("Hide");
            Assert.AreEqual(1, hidden); Assert.IsFalse(Visible); Assert.IsFalse(Visuals.interactable); Assert.IsFalse(Visuals.blocksRaycasts); Assert.IsNull(events.currentSelectedGameObject);
            Action reopen = () => Open(); Subscribe("Hidden", reopen); Open(); Call("Hide"); Assert.IsTrue(Visible);
            yield return new WaitForSecondsRealtime(.3f); Assert.IsTrue(Visible); Assert.AreEqual(1, Visuals.alpha); Unsubscribe("Hidden", reopen);
        }
        [UnityTest] public IEnumerator FadeUsesUnscaledTimeAndDisableCleansAndCanRebind()
        {
            int count = 0; Subscribe("AcknowledgeRequested", (Action<string>)(_ => count++)); Time.timeScale = 0; Open(); yield return new WaitForSecondsRealtime(.24f); Assert.AreEqual(1, Visuals.alpha);
            Call("Hide"); yield return new WaitForSecondsRealtime(.18f); Assert.IsFalse(Visuals.gameObject.activeSelf); Assert.AreEqual(0, Time.timeScale);
            Open(); view.enabled = false; Assert.IsFalse(Visible); Assert.IsFalse(Visuals.gameObject.activeSelf); Assert.IsFalse(Playing);
            var error = Assert.Throws<TargetInvocationException>(() => Open()); Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
            view.enabled = true; Open(); ExecuteEvents.Execute(Confirm.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Assert.AreEqual(1, count);
        }
        [UnityTest] public IEnumerator MissingConfigurationFailsBeforeMutatingVisibleSnapshot()
        {
            Open(); var before = Field(view, "model"); var title = Field(Texts, "title"); SetField(Texts, "title", null);
            try { var error = Assert.Throws<TargetInvocationException>(() => Open()); Assert.IsInstanceOf<InvalidOperationException>(error.InnerException); Assert.IsTrue(Visible); Assert.AreSame(before, Field(view, "model")); }
            finally { SetField(Texts, "title", title); } yield return null;
        }
        [UnityTest] public IEnumerator CoinSessionGuardsPoolEightAndClampedCounts()
        {
            Assert.IsFalse((bool)Call("PlayRewardTransfer", "results-preview", 5)); Open(); Assert.IsFalse((bool)Call("PlayRewardTransfer", "other", 5));
            Assert.IsTrue((bool)Call("PlayRewardTransfer", "results-preview", -100)); Assert.IsTrue(Playing); Assert.IsFalse((bool)Call("PlayRewardTransfer", "results-preview", 100));
            yield return new WaitForSecondsRealtime(.15f); Assert.AreEqual(2, Pieces.Count(p => p.gameObject.activeSelf)); Assert.IsTrue(Pieces.All(p => !p.raycastTarget));
            Invoke(Transfer, "Stop"); Assert.IsTrue((bool)Call("PlayRewardTransfer", "results-preview", 100)); yield return new WaitForSecondsRealtime(.51f);
            Assert.AreEqual(8, Pieces.Count(p => p.gameObject.activeSelf)); Assert.AreEqual(8, Pieces.Length);
        }
        [UnityTest] public IEnumerator CoinUnscaledCompletionAllowsExplicitReplayAndNoCurrencyMutation()
        {
            Time.timeScale = 0; Open(); string amount = Text("fundsAmount").text;
            Assert.IsTrue((bool)Call("PlayRewardTransfer", "results-preview", 8)); yield return new WaitForSecondsRealtime(1.3f);
            Assert.IsFalse(Playing); Assert.IsTrue(Pieces.All(p => !p.gameObject.activeSelf && p.color.a == 0)); Assert.AreEqual(amount, Text("fundsAmount").text);
            Assert.IsTrue((bool)Call("PlayRewardTransfer", "results-preview", 2)); yield return null;
        }
        [UnityTest] public IEnumerator CoinDestinationNullDisabledDestroyedAndForeignCanvasAreRejected()
        {
            Open(); var original = Destination; Call("SetRewardTransferDestination", (object)null); Assert.IsFalse((bool)Call("PlayRewardTransfer", "results-preview", 5));
            Call("SetRewardTransferDestination", original); original.gameObject.SetActive(false); Assert.IsFalse((bool)Call("PlayRewardTransfer", "results-preview", 5)); original.gameObject.SetActive(true);
            var foreign = new GameObject("ForeignCanvas", typeof(RectTransform), typeof(Canvas)); extra.Add(foreign); foreign.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Call("SetRewardTransferDestination", foreign.GetComponent<RectTransform>()); Assert.IsFalse((bool)Call("PlayRewardTransfer", "results-preview", 5));
            var local = new GameObject("ExternalDestination", typeof(RectTransform)); extra.Add(local); local.transform.SetParent(instance.transform, false);
            Call("SetRewardTransferDestination", local.GetComponent<RectTransform>()); Assert.IsTrue((bool)Call("PlayRewardTransfer", "results-preview", 5)); Object.Destroy(local);
            yield return null; yield return null; Assert.IsFalse(Playing); Assert.IsTrue(Pieces.All(p => !p.gameObject.activeSelf)); Call("SetRewardTransferDestination", original);
        }
        [UnityTest] public IEnumerator CoinStopsOnRefreshHideReopenDisableAndDestinationLoss()
        {
            Open(); var target = Destination;
            for (int mode = 0; mode < 6; mode++)
            {
                Open(); target.gameObject.SetActive(true); Call("SetRewardTransferDestination", target); Assert.IsTrue((bool)Call("PlayRewardTransfer", "results-preview", 5));
                if (mode == 0) Call("SetPresentation", Sample()); else if (mode == 1) Call("Hide"); else if (mode == 2) Open();
                else if (mode == 3) Transfer.enabled = false; else if (mode == 4) Call("SetRewardTransferDestination", target); else target.gameObject.SetActive(false);
                yield return null; Assert.IsFalse(Playing); Assert.IsTrue(Pieces.All(p => !p.gameObject.activeSelf)); Transfer.enabled = true;
            }
        }
        static readonly string[] PreviewMethods = { "OpenFailure", "OpenSuccess", "OpenGrand", "TwoMembers", "ThreeMembers", "FourMembers", "NoLocal", "Overlap", "MissingPositions", "OutsidePositions", "LongText", "RefreshPresentation", "OtherSession", "ToggleAcknowledge", "ToggleAcknowledgeLabel", "RequestAcknowledge", "Hide", "Reopen", "CoinMinimum", "CoinMaximum", "CoinWrongSession", "ToggleDestination", "NullDestination", "RestoreDestination", "TogglePause", "ToggleBackground", "ToggleRunInBackground", "ResetPreview" };
        MonoBehaviour MakePreview(out Button[] buttons, out Camera camera)
        {
            var go = new GameObject("ResultsPreviewTest"); extra.Add(go); var preview = (MonoBehaviour)go.AddComponent(PreviewType);
            camera = new GameObject("PreviewTestCamera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(go.transform); camera.backgroundColor = new Color(.2f, .3f, .4f);
            buttons = new Button[28]; for (int i = 0; i < buttons.Length; i++) { var button = new GameObject("PreviewAction_" + i, typeof(RectTransform), typeof(Button)); button.transform.SetParent(go.transform, false); buttons[i] = button.GetComponent<Button>(); }
            Invoke(preview, "Configure", view, null, camera, buttons, Destination); return preview;
        }
        [UnityTest] public IEnumerator PreviewEveryCallbackHasActiveInteractableAndEnabledGuards()
        {
            var preview = MakePreview(out var buttons, out _);
            for (int i = 0; i < PreviewMethods.Length; i++)
            {
                buttons[i].interactable = false; Invoke(preview, PreviewMethods[i]); buttons[i].interactable = true;
                buttons[i].gameObject.SetActive(false); Invoke(preview, PreviewMethods[i]); buttons[i].gameObject.SetActive(true);
            }
            Assert.AreEqual(0, Property(preview, "ActionCount")); Assert.IsFalse(Visible);
            preview.enabled = false; Invoke(preview, "OpenGrand"); preview.enabled = true; view.enabled = false; Invoke(preview, "OpenGrand"); view.enabled = true;
            Assert.AreEqual(0, Property(preview, "ActionCount")); Invoke(preview, "OpenGrand"); Assert.AreEqual(1, Property(preview, "ActionCount")); Assert.AreEqual("OpenGrand", Property(preview, "LastAction")); yield return null;
        }
        [UnityTest] public IEnumerator PreviewRestoresGlobalsOnDisableDestroyAndReset()
        {
            Time.timeScale = .75f; bool background = Application.runInBackground; var preview = MakePreview(out _, out var camera); var color = camera.backgroundColor; var destination = Destination;
            Invoke(preview, "OpenGrand"); Invoke(preview, "TogglePause"); Invoke(preview, "ToggleBackground"); Invoke(preview, "ToggleRunInBackground"); Invoke(preview, "ToggleDestination"); Invoke(preview, "NullDestination");
            Assert.AreEqual(0, Time.timeScale); preview.enabled = false;
            Assert.AreEqual(.75f, Time.timeScale); Assert.AreEqual(background, Application.runInBackground); Assert.AreEqual(color, camera.backgroundColor); Assert.IsTrue(destination.gameObject.activeSelf); Assert.AreSame(destination, Destination);
            preview.enabled = true; Invoke(preview, "TogglePause"); Invoke(preview, "ResetPreview"); Assert.AreEqual(.75f, Time.timeScale); Assert.IsFalse(Visible);
            Invoke(preview, "Reopen"); Assert.IsTrue(Visible); Invoke(preview, "TogglePause"); Object.Destroy(preview); yield return null;
            Assert.AreEqual(.75f, Time.timeScale); Assert.AreEqual(background, Application.runInBackground); Assert.AreEqual(color, camera.backgroundColor);
        }
        [UnityTest] public IEnumerator PreviewExceptionRestoresGlobalsAndSampleFactoryCoversEveryVariant()
        {
            foreach (string kind in new[] { "failure", "success", "grand", "two", "three", "four", "no-local", "overlap", "missing", "outside", "text" }) Assert.NotNull(Sample(kind));
            Time.timeScale = .8f; bool background = Application.runInBackground; var preview = MakePreview(out _, out var camera); var color = camera.backgroundColor;
            Invoke(preview, "TogglePause"); Invoke(preview, "ToggleBackground"); Invoke(preview, "ToggleRunInBackground");
            var title = Field(Texts, "title"); SetField(Texts, "title", null);
            try { Assert.Throws<TargetInvocationException>(() => Invoke(preview, "OpenGrand")); } finally { SetField(Texts, "title", title); }
            Assert.AreEqual(.8f, Time.timeScale); Assert.AreEqual(background, Application.runInBackground); Assert.AreEqual(color, camera.backgroundColor); yield return null;
        }
    }
}
