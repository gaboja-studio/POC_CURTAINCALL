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

namespace CurtainCall.ScheduleUi.Tests
{
    /// <summary>Assembly-CSharp 공개 표시 API를 리플렉션으로 검사한다. 실제 게임 서비스는 연결하지 않는다.</summary>
    public sealed class ScheduleUiPlayTests
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
        }
        static Type Domain(string name) => Type.GetType("CurtainCall.UI.ScheduleV1." + name + ", Assembly-CSharp", true);
        static object Property(object source, string name) => source.GetType().GetProperty(name).GetValue(source);
        static object Field(object source, string name) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(source);
        static void SetField(object source, string name, object value) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(source, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
        object Call(string method, params object[] args) => Invoke(view, method, args);
        bool Visible => (bool)Property(view, "IsVisible");
        CanvasGroup Visuals => (CanvasGroup)Field(view, "visuals");
        Array Days => (Array)Field(view, "dayLayers");
        object DayLayer(int i) => Days.GetValue(i);
        RectTransform Content(int i) => (RectTransform)Field(DayLayer(i), "content");
        ScrollRect Scroll(int i) => (ScrollRect)Field(DayLayer(i), "scroll");
        IList Cards(int i) => (IList)Field(DayLayer(i), "cards");
        MonoBehaviour Card(int day, int index) => (MonoBehaviour)Cards(day)[index];
        Button CloseButton => (Button)Field(view, "closeButton");
        static Array Typed(string name, params object[] items)
        { var result = Array.CreateInstance(Domain(name), items.Length); for (int i = 0; i < items.Length; i++) result.SetValue(items[i], i); return result; }
        static object Entry(string id = "entry", string title = "외줄타기", bool current = false, bool confirmed = false)
            => Activator.CreateInstance(Domain("ScheduleEntryPresentation"), id, title, "공연 01", current ? "현재" : "예정", current, confirmed);
        static object Day(string id, int count = 0, bool current = false)
        {
            var entries = new object[count];
            for (int i = 0; i < count; i++) entries[i] = Entry(id + "-" + i, "외줄타기", (i & 1) != 0, (i & 2) != 0);
            return Activator.CreateInstance(Domain("ScheduleDayPresentation"), id, "1일차", current, "공연 일정 없음", Typed("ScheduleEntryPresentation", entries));
        }
        static object Model(string session = "schedule-preview", string revision = "test", params object[] days)
        {
            if (days.Length == 0) days = new[] { Day("a", 2), Day("b", 2), Day("c") };
            return Activator.CreateInstance(Domain("SchedulePresentation"), session, revision, "공연 일정표", "3일 공연 일정", "일정을 보는 동안에도 플레이는 계속됩니다.", Typed("ScheduleDayPresentation", days));
        }
        static object Demo() => Domain("ScheduleDemoData").GetMethod("Create").Invoke(null, null);
        static object Sample(string kind) => Domain("ScheduleUiPreview").GetMethod("CreateSample").Invoke(null, new object[] { kind, "sample", "schedule-preview" });
        void Open(object model = null, bool focus = false) => Call("Open", model ?? Demo(), focus);
        static void Invalid(Action action)
        { var error = Assert.Throws<TargetInvocationException>(() => action()); Assert.IsInstanceOf<ArgumentException>(error.InnerException); }
        void Subscribe(string name, Action action) => view.GetType().GetEvent(name).AddEventHandler(view, action);
        void Unsubscribe(string name, Action action) => view.GetType().GetEvent(name).RemoveEventHandler(view, action);
        static MonoBehaviour Find(GameObject root) => root.GetComponentsInChildren<MonoBehaviour>(true).Single(b => b != null && b.GetType().FullName == "CurtainCall.UI.ScheduleV1.SchedulePanelView");
        [UnitySetUp] public IEnumerator SetUp()
        {
            originalScale = Time.timeScale; originalBackground = Application.runInBackground; Time.timeScale = 1; extra.Clear(); fontSnapshots.Clear();
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/ScheduleUi/SchedulePanel"); Assert.NotNull(prefab, "W3 소유 프리팹을 먼저 조립해야 합니다.");
            instance = Object.Instantiate(prefab); view = Find(instance);
            events = EventSystem.current;
            if (events == null) { ownedEvents = new GameObject("ScheduleTestEventSystem", typeof(EventSystem)); events = ownedEvents.GetComponent<EventSystem>(); }
            foreach (var font in instance.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).Distinct())
                fontSnapshots.Add(new FontSnapshot { Font = font, Characters = font.characterTable.Select(c => c.unicode).ToArray(),
                    GlyphIndices = font.characterTable.Select(c => c.glyphIndex).ToArray(), Glyphs = font.glyphTable.Select(g => g.index).ToArray(),
                    Material = font.material, Atlases = font.atlasTextures.ToArray(), Fallbacks = font.fallbackFontAssetTable == null ? new TMP_FontAsset[0] : font.fallbackFontAssetTable.ToArray() });
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var go in extra) if (go != null) Object.Destroy(go);
            if (instance != null) Object.Destroy(instance);
            if (ownedEvents != null) Object.Destroy(ownedEvents);
            yield return null;
            Time.timeScale = originalScale; Application.runInBackground = originalBackground;
            foreach (var snapshot in fontSnapshots)
            {
                CollectionAssert.AreEqual(snapshot.Characters, snapshot.Font.characterTable.Select(c => c.unicode).ToArray(), "기존 폰트의 글리프가 변경되었습니다.");
                CollectionAssert.AreEqual(snapshot.GlyphIndices, snapshot.Font.characterTable.Select(c => c.glyphIndex).ToArray());
                CollectionAssert.AreEqual(snapshot.Glyphs, snapshot.Font.glyphTable.Select(g => g.index).ToArray());
                Assert.AreSame(snapshot.Material, snapshot.Font.material); CollectionAssert.AreEqual(snapshot.Atlases, snapshot.Font.atlasTextures);
                CollectionAssert.AreEqual(snapshot.Fallbacks, snapshot.Font.fallbackFontAssetTable ?? new List<TMP_FontAsset>());
            }
        }
        [UnityTest] public IEnumerator InitialHiddenCanvasConfigAndFonts()
        {
            Assert.IsFalse(Visible); Assert.IsFalse(Visuals.gameObject.activeSelf); Assert.AreEqual(0, Visuals.alpha);
            Assert.IsFalse(Visuals.interactable); Assert.IsFalse(Visuals.blocksRaycasts);
            var canvas = instance.GetComponent<Canvas>(); Assert.NotNull(canvas); Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(100, canvas.referencePixelsPerUnit); Assert.NotNull(instance.GetComponent<GraphicRaycaster>());
            var scaler = instance.GetComponent<CanvasScaler>(); Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);
            Assert.AreEqual(.5f, scaler.matchWidthOrHeight); Assert.AreEqual(100, scaler.referencePixelsPerUnit);
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Open();
            foreach (var label in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.NotNull(label.font); Assert.AreEqual(AtlasPopulationMode.Static, label.font.atlasPopulationMode);
                Assert.IsFalse(label.font.isMultiAtlasTexturesEnabled); Assert.AreEqual(1, label.font.atlasTextures.Length);
                Assert.IsTrue(label.font.fallbackFontAssetTable == null || label.font.fallbackFontAssetTable.Count == 0);
                Assert.IsFalse(label.richText); Assert.IsFalse(label.parseCtrlCharacters); Assert.IsFalse(label.enableAutoSizing); Assert.IsFalse(label.raycastTarget);
                Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
                foreach (char c in label.text.Where(c => !char.IsControl(c))) Assert.IsTrue(label.font.HasCharacter(c, false, false), label.name + " 글리프 누락 " + c);
            }
            Assert.AreSame(Resources.Load<TMP_FontAsset>("Fonts/Spectator/Pretendard-Medium SDF"), ((TMP_Text)Field(view, "context")).font);
            Assert.AreSame(Resources.Load<TMP_FontAsset>("Fonts/SettingsUi/Pretendard-SemiBold SDF"), ((TMP_Text)Field(DayLayer(0), "label")).font);
            yield return null;
        }
        [UnityTest] public IEnumerator DayHeaderLabelsActuallyRenderAtProvidedTypography()
        {
            Open(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 3; i++)
            {
                var label = (TMP_Text)Field(DayLayer(i), "label"); label.ForceMeshUpdate();
                Assert.AreEqual((i + 1) + "일차", label.text);
                Assert.Greater(label.textInfo.characterCount, 0, label.name + " 일차 문구가 세로 잘림으로 사라졌습니다.");
                Assert.IsTrue(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.isVisible), label.name);
            }
        }
        [UnityTest] public IEnumerator ExactPanelPaperColumnsCardsAndCloseGeometry()
        {
            var stage = (RectTransform)Field(view, "layoutStage"); var panel = (RectTransform)stage.Find("00_SchedulePanel");
            Assert.AreEqual(new Vector2(432, -272), panel.anchoredPosition); Assert.AreEqual(new Vector2(1056, 552), panel.sizeDelta);
            var paper = (RectTransform)panel.Find("40_Content/10_Paper"); Assert.AreEqual(new Vector2(32, -88), paper.anchoredPosition); Assert.AreEqual(new Vector2(992, 344), paper.sizeDelta);
            var texture = paper.Find("10_Material").GetComponent<Image>(); Assert.AreEqual(Image.Type.Simple, texture.type); Assert.That(texture.color.a, Is.EqualTo(.16f).Within(.001));
            Assert.AreEqual(new Vector2(992, 344), texture.rectTransform.sizeDelta);
            Assert.AreEqual(new Vector2(32, -464), ((RectTransform)CloseButton.transform).anchoredPosition); Assert.AreEqual(new Vector2(192, 56), ((RectTransform)CloseButton.transform).sizeDelta);
            Open(Sample("many"));
            for (int d = 0; d < 3; d++)
            {
                var column = (RectTransform)Scroll(d).transform.parent;
                Assert.AreEqual(new Vector2(48 + d * 328, -104), column.anchoredPosition); Assert.AreEqual(new Vector2(304, 312), column.sizeDelta);
                Assert.AreEqual(new Vector2(304, 248), Scroll(d).viewport.sizeDelta); Assert.AreEqual(ScrollRect.MovementType.Clamped, Scroll(d).movementType);
                Assert.IsFalse(Scroll(d).horizontal); Assert.IsFalse(Scroll(d).inertia); Assert.NotNull(Scroll(d).viewport.GetComponent<RectMask2D>());
                for (int i = 0; i < 12; i++)
                { var rect = (RectTransform)Card(d, i).transform; Assert.AreEqual(new Vector2(304, 112), rect.sizeDelta); Assert.AreEqual(new Vector2(0, -124 * i), rect.anchoredPosition); Assert.IsEmpty(rect.GetComponentsInChildren<Selectable>(true)); }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AllSerializedReferencesStayWithinOwnedCanvasAndNoFullscreenDim()
        {
            foreach (var node in instance.GetComponentsInChildren<Transform>(true)) foreach (var component in node.GetComponents<Component>()) Assert.NotNull(component, node.name);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                for (Type type = behaviour.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if ((field.IsPublic || field.IsDefined(typeof(SerializeField), true)) && field.GetValue(behaviour) is Component reference)
                            Assert.IsTrue(reference.transform == instance.transform || reference.transform.IsChildOf(instance.transform), field.Name);
            Open(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = new Vector2(4, 4) }, hits);
            Assert.IsFalse(hits.Any(h => h.gameObject.transform.IsChildOf(instance.transform)));
        }
        [UnityTest] public IEnumerator CurrentAndConfirmedAreIndependentFourCombinations()
        {
            Open(Model("s", "r", Day("a", 4, true), Day("b"), Day("c")));
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual((i & 1) != 0, ((Image)Field(Card(0, i), "currentOutline")).enabled);
                Assert.AreEqual((i & 1) != 0, ((Image)Field(Card(0, i), "currentMarker")).enabled);
                Assert.AreEqual((i & 2) != 0, ((Image)Field(Card(0, i), "seal")).enabled);
                Assert.AreEqual((i & 2) != 0, ((Image)Field(Card(0, i), "check")).enabled);
                Assert.AreEqual((i & 2) != 0, ((TMP_Text)Field(Card(0, i), "confirmedLabel")).enabled);
                Assert.AreEqual((i & 2) == 0, ((TMP_Text)Field(Card(0, i), "unconfirmedLabel")).enabled);
            }
            Assert.IsTrue(((TMP_Text)Field(DayLayer(0), "currentLabel")).enabled);
            Assert.IsFalse(((TMP_Text)Field(DayLayer(1), "currentLabel")).enabled); yield return null;
        }
        [UnityTest] public IEnumerator DtoCopiesEveryLevelAndCollectionsAreReadOnly()
        {
            var entry = Entry(); var entries = Typed("ScheduleEntryPresentation", entry);
            object day = Activator.CreateInstance(Domain("ScheduleDayPresentation"), "a", "1일차", false, "", entries);
            entries.SetValue(Entry("replaced"), 0); var dayEntries = (IList)Property(day, "Entries");
            Assert.AreEqual("entry", Property(dayEntries[0], "Id")); Assert.IsTrue(dayEntries.IsReadOnly); Assert.AreNotSame(entry, dayEntries[0]);
            var days = Typed("ScheduleDayPresentation", day, Day("b"), Day("c"));
            var model = Activator.CreateInstance(Domain("SchedulePresentation"), "s", "r", "공연 일정표", "", "", days);
            days.SetValue(Day("replacement"), 0); var copiedDays = (IList)Property(model, "Days"); Assert.IsTrue(copiedDays.IsReadOnly);
            Assert.AreEqual("a", Property(copiedDays[0], "Id")); Assert.AreNotSame(day, copiedDays[0]);
            Open(model); var rendered = Field(view, "model"); Assert.AreNotSame(model, rendered);
            Assert.AreNotSame(copiedDays[0], ((IList)Property(rendered, "Days"))[0]); yield return null;
        }
        [UnityTest] public IEnumerator InvalidDtosRequireExactlyThreeUniqueDaysAndGloballyUniqueEntries()
        {
            Invalid(() => Entry(" ")); Invalid(() => Entry(title: " "));
            Invalid(() => Day(" "));
            Invalid(() => Activator.CreateInstance(Domain("ScheduleDayPresentation"), "a", "1일차", false, "", Typed("ScheduleEntryPresentation", Entry(), Entry())));
            Invalid(() => Activator.CreateInstance(Domain("ScheduleDayPresentation"), "a", "1일차", false, "", Typed("ScheduleEntryPresentation", new object[] { null })));
            Invalid(() => Model(" ")); Invalid(() => Model(revision: " "));
            Invalid(() => Model("s", "r", Day("a"))); Invalid(() => Model("s", "r", Day("a"), Day("b")));
            Invalid(() => Model("s", "r", Day("a"), Day("b"), Day("c"), Day("d")));
            Invalid(() => Model("s", "r", Day("a"), Day("a"), Day("c")));
            Invalid(() => Model("s", "r", Day("a"), null, Day("c")));
            object repeated = Activator.CreateInstance(Domain("ScheduleDayPresentation"), "b", "2일차", false, "", Typed("ScheduleEntryPresentation", Entry("a-0")));
            Invalid(() => Model("s", "r", Day("a", 1), repeated, Day("c"))); yield return null;
        }
        [UnityTest] public IEnumerator EachColumnSupportsZeroOneTwoAndTwelveWithoutStaleCards()
        {
            foreach (int count in new[] { 0, 1, 2, 12, 0 })
            {
                Open(Model("s", "r", Day("a", count), Day("b", count), Day("c", count)));
                for (int d = 0; d < 3; d++)
                {
                    Assert.AreEqual(count, Cards(d).Cast<MonoBehaviour>().Count(c => c.gameObject.activeSelf));
                    Assert.AreEqual(Mathf.Max(248, count * 124 - 12), Content(d).sizeDelta.y);
                    Assert.AreEqual(count == 0, ((TMP_Text)Field(DayLayer(d), "emptyMessage")).gameObject.activeSelf);
                    Assert.AreEqual(0, Content(d).anchoredPosition.y);
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ScrollIsClampedIndependentAndIgnoresInvalidInput()
        {
            Open(Sample("many")); Canvas.ForceUpdateCanvases();
            for (int d = 0; d < 3; d++)
            {
                Call("ScrollDay", d, 100000f); Assert.That(Content(d).anchoredPosition.y, Is.EqualTo(12 * 124 - 12 - 248).Within(.01));
                Call("ScrollDay", d, float.NaN); Call("ScrollDay", d, float.PositiveInfinity);
                Assert.That(Content(d).anchoredPosition.y, Is.EqualTo(1228).Within(.01));
                Call("ScrollDay", d, -100000f); Assert.AreEqual(0, Content(d).anchoredPosition.y);
            }
            Call("ScrollDay", 0, 100f); Assert.AreEqual(0, Content(1).anchoredPosition.y); Assert.AreEqual(0, Content(2).anchoredPosition.y);
            Call("ScrollDay", -1, 50f); Call("ScrollDay", 3, 50f); Assert.AreEqual(100, Content(0).anchoredPosition.y);
            Call("Hide"); Call("ScrollDay", 0, 100f); Assert.AreEqual(100, Content(0).anchoredPosition.y); yield return null;
        }
        [UnityTest] public IEnumerator RefreshPreservesNormalizedScrollByDayIdAcrossReorderAndResetsNewId()
        {
            Open(Model("s", "r", Day("a", 12), Day("b", 12), Day("c", 12))); Canvas.ForceUpdateCanvases();
            Call("ScrollDay", 0, 614f); Call("ScrollDay", 1, 1228f); Call("ScrollDay", 2, 307f);
            Assert.IsTrue((bool)Call("SetPresentation", Model("s", "r2", Day("c", 6), Day("a", 12), Day("new", 12))));
            Assert.That(Content(0).anchoredPosition.y, Is.EqualTo((6 * 124 - 12 - 248) * .25f).Within(.01));
            Assert.That(Content(1).anchoredPosition.y, Is.EqualTo(614).Within(.01)); Assert.AreEqual(0, Content(2).anchoredPosition.y);
            Assert.IsTrue((bool)Call("SetPresentation", Model("s", "r3", Day("a"), Day("c", 1), Day("new", 2))));
            for (int d = 0; d < 3; d++) Assert.AreEqual(0, Content(d).anchoredPosition.y);
            yield return null;
        }
        [UnityTest] public IEnumerator OtherSessionsRejectedAndRevisionOrderingRemainsCallerResponsibility()
        {
            Assert.IsFalse((bool)Call("SetPresentation", Demo())); Open(Model("s", "revision-20")); object before = Field(view, "model");
            Assert.IsFalse((bool)Call("SetPresentation", Model("other", "revision-21"))); Assert.AreSame(before, Field(view, "model"));
            Assert.IsTrue((bool)Call("SetPresentation", Model("s", "revision-01"))); Assert.AreEqual("revision-01", Property(Field(view, "model"), "Revision"));
            Assert.IsTrue((bool)Call("SetPresentation", Model("s", "revision-01"))); yield return null;
        }
        [UnityTest] public IEnumerator CloseSubmitAndCancelDispatchIntentOnceHideDoesNot()
        {
            int intents = 0, closed = 0; Subscribe("CloseRequested", () => intents++); Subscribe("Closed", () => closed++);
            Open(focus: true); Assert.AreEqual(CloseButton.gameObject, events.currentSelectedGameObject);
            ExecuteEvents.Execute(CloseButton.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            Assert.IsFalse(Visible); Assert.AreEqual(1, intents); Assert.AreEqual(1, closed); Assert.IsNull(events.currentSelectedGameObject);
            Call("Close"); Assert.AreEqual(1, intents);
            Open(); var cancel = new BaseEventData(events); ExecuteEvents.Execute(CloseButton.gameObject, cancel, ExecuteEvents.cancelHandler);
            Assert.IsTrue(cancel.used); Assert.IsFalse(Visible); Assert.AreEqual(2, intents);
            Open(); Call("Hide"); Assert.AreEqual(2, intents); Assert.AreEqual(3, closed); yield return null;
        }
        [UnityTest] public IEnumerator DisabledCloseRejectsSubmitAndCancel()
        {
            int intents = 0; Subscribe("CloseRequested", () => intents++); Open(); CloseButton.interactable = false;
            ExecuteEvents.Execute(CloseButton.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            var cancel = new BaseEventData(events); ExecuteEvents.Execute(CloseButton.gameObject, cancel, ExecuteEvents.cancelHandler);
            Assert.IsTrue(Visible); Assert.AreEqual(0, intents); Assert.IsFalse(cancel.used); CloseButton.interactable = true; yield return null;
        }
        [UnityTest] public IEnumerator ClosedCallbackReentryCannotLeaveOldFadeHidingNewView()
        {
            Action reopen = () => Open(); Subscribe("Closed", reopen);
            try { Open(); Call("Hide"); Assert.IsTrue(Visible); yield return new WaitForSecondsRealtime(.3f); Assert.IsTrue(Visible); Assert.IsTrue(Visuals.gameObject.activeSelf); Assert.That(Visuals.alpha, Is.EqualTo(1).Within(.001)); }
            finally { Unsubscribe("Closed", reopen); }
        }
        [UnityTest] public IEnumerator CloseRequestedCallbackReopenDoesNotSendStaleClosed()
        {
            int closed = 0; Action reopen = () => Open(); Subscribe("CloseRequested", reopen); Subscribe("Closed", () => closed++);
            try { Open(); Call("Close"); Assert.IsTrue(Visible); Assert.AreEqual(0, closed); yield return new WaitForSecondsRealtime(.3f); Assert.IsTrue(Visible); }
            finally { Unsubscribe("CloseRequested", reopen); }
        }
        [UnityTest] public IEnumerator MotionRunsAtTimeScaleZeroAndDisableReopenDoesNotDuplicateBinding()
        {
            int intents = 0; Subscribe("CloseRequested", () => intents++); Time.timeScale = 0; Open();
            yield return new WaitForSecondsRealtime(.24f); Assert.That(Visuals.alpha, Is.EqualTo(1).Within(.001));
            Call("Hide"); Assert.IsFalse(Visuals.interactable); Assert.IsFalse(Visuals.blocksRaycasts);
            yield return new WaitForSecondsRealtime(.18f); Assert.IsFalse(Visuals.gameObject.activeSelf); Assert.AreEqual(0, Time.timeScale);
            Open(); view.enabled = false; Assert.IsFalse(Visible); Assert.IsFalse(Visuals.gameObject.activeSelf);
            var error = Assert.Throws<TargetInvocationException>(() => Open()); Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
            view.enabled = true; Open(); ExecuteEvents.Execute(CloseButton.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            Assert.AreEqual(1, intents); yield return null;
        }
        [UnityTest] public IEnumerator InvalidConfigurationFailsBeforeInitialOrExistingStateMutation()
        {
            object saved = Field(view, "title"); SetField(view, "title", null);
            try { var error = Assert.Throws<TargetInvocationException>(() => Open()); Assert.IsInstanceOf<InvalidOperationException>(error.InnerException); Assert.IsFalse(Visible); Assert.IsFalse(Visuals.gameObject.activeSelf); }
            finally { SetField(view, "title", saved); }
            Open(); object before = Field(view, "model");
            foreach (string name in new[] { "visuals", "layoutStage", "title", "context", "footer", "closeButton", "dayLayers" })
            {
                saved = Field(view, name); SetField(view, name, null);
                try { var error = Assert.Throws<TargetInvocationException>(() => Open()); Assert.IsInstanceOf<InvalidOperationException>(error.InnerException); Assert.IsTrue(Visible); Assert.AreSame(before, Field(view, "model")); }
                finally { SetField(view, name, saved); }
            }
            var scroll = Scroll(0); var viewport = scroll.viewport; scroll.viewport = null;
            try { Assert.Throws<TargetInvocationException>(() => Open()); Assert.AreSame(before, Field(view, "model")); }
            finally { scroll.viewport = viewport; }
            yield return null;
        }
        [UnityTest] public IEnumerator LongTextRemainsLiteralFixedSizeAndActuallyEllipsizes()
        {
            Open(Sample("text")); yield return null; Canvas.ForceUpdateCanvases();
            var label = (TMP_Text)Field(Card(0, 0), "title"); label.ForceMeshUpdate();
            Assert.IsTrue(label.text.StartsWith("<b>")); Assert.IsFalse(label.richText); Assert.IsFalse(label.enableAutoSizing); Assert.AreEqual(24, label.fontSize);
            Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
            Assert.IsTrue(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.character == '…' && c.isVisible));
        }
        MonoBehaviour MakePreview(out Button[] buttons, out Camera camera)
        {
            var go = new GameObject("SchedulePreviewTest"); extra.Add(go); var preview = (MonoBehaviour)go.AddComponent(Domain("ScheduleUiPreview"));
            camera = new GameObject("PreviewTestCamera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(go.transform); camera.backgroundColor = new Color(.2f, .3f, .4f);
            buttons = new Button[21];
            for (int i = 0; i < buttons.Length; i++) { var button = new GameObject("Button_" + i, typeof(RectTransform), typeof(Button)); button.transform.SetParent(go.transform, false); buttons[i] = button.GetComponent<Button>(); }
            Invoke(preview, "Configure", view, null, camera, buttons); return preview;
        }
        [UnityTest] public IEnumerator PreviewGuardsEveryCallbackAndRestoresGlobalsOnDisableAndDestroy()
        {
            Time.timeScale = .75f; bool background = Application.runInBackground;
            var preview = MakePreview(out var buttons, out var camera); Color color = camera.backgroundColor;
            string[] methods = { "OpenDemo", "Empty", "Many", "LongText", "FourStates", "Unconfirmed", "Day1Top", "Day1Bottom", "Day2Top", "Day2Bottom", "Day3Top", "Day3Bottom", "RefreshPresentation", "OtherSession", "Close", "Hide", "Reopen", "TogglePause", "ToggleBackground", "ToggleRunInBackground", "ResetPreview" };
            for (int i = 0; i < methods.Length; i++) { buttons[i].interactable = false; Invoke(preview, methods[i]); buttons[i].interactable = true; }
            Assert.AreEqual(0, Property(preview, "ActionCount")); Assert.IsFalse(Visible); Assert.AreEqual(.75f, Time.timeScale);
            buttons[0].gameObject.SetActive(false); Invoke(preview, "OpenDemo"); Assert.IsFalse(Visible); buttons[0].gameObject.SetActive(true);
            Invoke(preview, "TogglePause"); Invoke(preview, "ToggleBackground"); Invoke(preview, "ToggleRunInBackground");
            Assert.AreEqual(0, Time.timeScale); Assert.AreNotEqual(color, camera.backgroundColor); Assert.AreNotEqual(background, Application.runInBackground);
            preview.enabled = false; Assert.AreEqual(.75f, Time.timeScale); Assert.AreEqual(background, Application.runInBackground); Assert.AreEqual(color, camera.backgroundColor);
            Invoke(preview, "OpenDemo"); Assert.IsFalse(Visible); preview.enabled = true;
            Invoke(preview, "TogglePause"); Object.Destroy(preview); yield return null;
            Assert.AreEqual(.75f, Time.timeScale); Assert.AreEqual(background, Application.runInBackground); Assert.AreEqual(color, camera.backgroundColor);
        }
        [UnityTest] public IEnumerator PreviewExceptionRestoresGlobalsAndResetReopensExplicitly()
        {
            Time.timeScale = .8f; bool background = Application.runInBackground;
            var preview = MakePreview(out _, out var camera); Color color = camera.backgroundColor;
            Invoke(preview, "TogglePause"); Invoke(preview, "ToggleBackground"); Invoke(preview, "ToggleRunInBackground");
            object title = Field(view, "title"); SetField(view, "title", null);
            try { Assert.Throws<TargetInvocationException>(() => Invoke(preview, "OpenDemo")); }
            finally { SetField(view, "title", title); }
            Assert.AreEqual(.8f, Time.timeScale); Assert.AreEqual(background, Application.runInBackground); Assert.AreEqual(color, camera.backgroundColor);
            Invoke(preview, "OpenDemo"); Assert.IsTrue(Visible); Invoke(preview, "ResetPreview"); Assert.IsFalse(Visible);
            Invoke(preview, "Reopen"); Assert.IsTrue(Visible); yield return null;
        }
    }
}
