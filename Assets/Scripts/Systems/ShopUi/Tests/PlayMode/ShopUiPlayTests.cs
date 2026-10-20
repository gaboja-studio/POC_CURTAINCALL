using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.ShopUi.Tests
{
    /// <summary>Assembly-CSharp 표시 API를 리플렉션으로 검사한다. 거래 서비스·경제 계산은 테스트하지 않는다.</summary>
    public sealed class ShopUiPlayTests
    {
        GameObject instance, ownedEvents;
        MonoBehaviour view;
        EventSystem events;
        float previousScale;
        bool previousBackground;
        object request;
        int requests;
        readonly List<GameObject> extra = new List<GameObject>();
        static Type Domain(string name) => Type.GetType("CurtainCall.UI.ShopV1." + name + ", Assembly-CSharp", true);
        static object Read(object source, string name) => source.GetType().GetProperty(name).GetValue(source);
        static object Field(object source, string name) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        static void SetField(object source, string name, object value) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
        object Call(string method, params object[] args) => Invoke(view, method, args);
        object Prop(string name) => Read(view, name);
        object TabValue(int index) => Enum.ToObject(Domain("ShopTab"), index);
        bool Visible => (bool)Prop("IsVisible");
        bool Busy => (bool)Prop("IsBusy");
        string Selected => (string)Prop("SelectedEntryId");
        IList Rows => (IList)Field(view, "rows");
        Array Tabs => (Array)Field(view, "tabs");
        UnityEngine.UI.Toggle Tab(int index) => (UnityEngine.UI.Toggle)Read(Tabs.GetValue(index), "NativeToggle");
        MonoBehaviour Row(int index) => (MonoBehaviour)Rows[index];
        UnityEngine.UI.Button RowButton(int index) => (UnityEngine.UI.Button)Read(Rows[index], "NativeButton");
        UnityEngine.UI.Button Primary => (UnityEngine.UI.Button)Field(view, "primaryButton");
        UnityEngine.UI.Button Secondary => (UnityEngine.UI.Button)Field(view, "secondaryButton");
        UnityEngine.UI.Button CloseButton => (UnityEngine.UI.Button)Field(view, "closeButton");
        static Array Typed(string type, params object[] items)
        { var array = Array.CreateInstance(Domain(type), items.Length); for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i); return array; }
        object Action(string id = "buy", string label = "구매", bool enabled = true) => Activator.CreateInstance(Domain("ShopActionPresentation"), id, label, enabled);
        object Entry(string id = "a", int tab = 0, bool available = true, string text = "기물", params object[] actions)
        {
            return Activator.CreateInstance(Domain("ShopEntryPresentation"), id, TabValue(tab), "bandage", text,
                "기물 설명", "60", text + "\n" + text, "구매 60 · 판매 30", available, available ? "" : "이용 불가", "배송 안내", "delivery", Typed("ShopActionPresentation", actions));
        }
        object Model(string session = "visual-demo", string revision = "test-1", int tab = 0, string selected = "a", params object[] entries)
        {
            return Activator.CreateInstance(Domain("ShopPresentation"), session, revision, "백스테이지 수화기", TabValue(tab), selected,
                "기물 목록", "서비스 목록", "거래 기록", Typed("ShopEntryPresentation", entries));
        }
        object Demo() => view.GetType().GetMethod("CreateDemoPresentation").Invoke(null, null);
        object Sample(string kind) => Domain("ShopUiPreview").GetMethod("CreateSample").Invoke(null, new object[] { kind });
        void Open() => Call("Open", Demo(), true);
        static void Subscribe(MonoBehaviour target, Action<object> handler)
        {
            EventInfo info = target.GetType().GetEvent("ActionRequested");
            var parameter = Expression.Parameter(info.EventHandlerType.GenericTypeArguments[0]);
            var callback = Expression.Lambda(info.EventHandlerType,
                Expression.Invoke(Expression.Constant(handler), Expression.Convert(parameter, typeof(object))), parameter).Compile();
            info.AddEventHandler(target, callback);
        }
        void Capture() => Subscribe(view, value => { request = value; requests++; });
        void Request() { Capture(); Open(); Call("RequestAction", "buy"); Assert.NotNull(request); Assert.IsTrue(Busy); }
        void Invalid(Action action)
        {
            var error = Assert.Throws<TargetInvocationException>(() => action());
            Assert.IsInstanceOf<ArgumentException>(error.InnerException);
        }
        MonoBehaviour Find(GameObject root) => root.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.ShopV1.ShopPanelView");
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScale = Time.timeScale; previousBackground = Application.runInBackground;
            Time.timeScale = 1; requests = 0; request = null; extra.Clear();
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/ShopUi/ShopPanel"); Assert.NotNull(prefab, "W2 소유 프리팹을 먼저 조립해야 합니다.");
            instance = Object.Instantiate(prefab); view = Find(instance);
            events = EventSystem.current;
            if (events == null) { ownedEvents = new GameObject("ShopTestEventSystem", typeof(EventSystem)); events = ownedEvents.GetComponent<EventSystem>(); }
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var go in extra) if (go != null) Object.Destroy(go);
            if (instance != null) Object.Destroy(instance);
            if (ownedEvents != null) Object.Destroy(ownedEvents);
            yield return null;
            Time.timeScale = previousScale; Application.runInBackground = previousBackground;
        }
        [UnityTest] public IEnumerator InitialHiddenCanvasAndTypography()
        {
            Assert.IsFalse(Visible); Assert.IsFalse(Busy); Assert.IsNull(Prop("PendingRequest"));
            Assert.IsFalse(((CanvasGroup)Field(view, "visuals")).gameObject.activeSelf);
            var canvas = instance.GetComponent<Canvas>(); Assert.NotNull(canvas); Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(100, canvas.referencePixelsPerUnit); Assert.NotNull(instance.GetComponent<UnityEngine.UI.GraphicRaycaster>());
            var scaler = instance.GetComponent<UnityEngine.UI.CanvasScaler>(); Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);
            Assert.AreEqual(.5f, scaler.matchWidthOrHeight); Assert.AreEqual(100, scaler.referencePixelsPerUnit);
            var originalMedium = Resources.Load<TMP_FontAsset>("Fonts/Spectator/Pretendard-Medium SDF");
            var dedicatedMedium = Resources.Load<TMP_FontAsset>("Fonts/ShopUi/Pretendard-Medium SDF");
            Assert.NotNull(originalMedium); Assert.NotNull(dedicatedMedium);
            Assert.AreSame(originalMedium.material, dedicatedMedium.material); Assert.AreSame(originalMedium.atlasTextures[0], dedicatedMedium.atlasTextures[0]);
            CollectionAssert.AreEqual(originalMedium.characterTable.Select(c => c.unicode).ToArray(), dedicatedMedium.characterTable.Select(c => c.unicode).ToArray());
            CollectionAssert.AreEqual(originalMedium.characterTable.Select(c => c.glyphIndex).ToArray(), dedicatedMedium.characterTable.Select(c => c.glyphIndex).ToArray());
            Assert.IsTrue(originalMedium.fallbackFontAssetTable == null || originalMedium.fallbackFontAssetTable.Count == 0);
            Assert.IsFalse(originalMedium.HasCharacter('−', false, false)); Assert.IsTrue(dedicatedMedium.HasCharacter('−', true, false));
            Assert.AreEqual(1, dedicatedMedium.fallbackFontAssetTable.Count);
            var support = dedicatedMedium.fallbackFontAssetTable[0]; Assert.NotNull(support); Assert.AreEqual(AtlasPopulationMode.Static, support.atlasPopulationMode);
            Assert.IsTrue(support.fallbackFontAssetTable == null || support.fallbackFontAssetTable.Count == 0);
            foreach (var label in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.NotNull(label.font); Assert.AreEqual(AtlasPopulationMode.Static, label.font.atlasPopulationMode);
                Assert.IsFalse(label.richText); Assert.IsFalse(label.parseCtrlCharacters); Assert.IsFalse(label.enableAutoSizing); Assert.IsFalse(label.raycastTarget);
                Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode); Assert.IsTrue(label.font.HasCharacters(label.text));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator DtoCopiesInputArraysAndExposesReadOnlyCollections()
        {
            object action = Action(); var actions = Typed("ShopActionPresentation", action);
            object entry = Activator.CreateInstance(Domain("ShopEntryPresentation"), "a", TabValue(0), "bandage", "기물", "", "60", "", "", true, "", "", "", actions);
            actions.SetValue(Action("sell", "판매"), 0);
            Assert.AreEqual("buy", Read(((IList)Read(entry, "Actions"))[0], "Id"));
            var entries = Typed("ShopEntryPresentation", entry);
            object model = Activator.CreateInstance(Domain("ShopPresentation"), "s", "r", "제목", TabValue(0), "a", "", "", "", entries);
            entries.SetValue(Entry("b"), 0);
            Assert.AreEqual("a", Read(((IList)Read(model, "Entries"))[0], "Id"));
            Assert.IsTrue(((IList)Read(model, "Entries")).IsReadOnly); Assert.IsTrue(((IList)Read(entry, "Actions")).IsReadOnly);
            Assert.AreNotSame(action, ((IList)Read(entry, "Actions"))[0]); yield return null;
        }
        [UnityTest] public IEnumerator InvalidDtosRejectIdsActionsTabsAndLedgerMutations()
        {
            Invalid(() => Action(" ")); Invalid(() => Action("buy", " "));
            Invalid(() => Entry(" ")); Invalid(() => Entry(tab: 99)); Invalid(() => Entry(text: " "));
            Invalid(() => Entry("a", 0, true, "기물", Action(), Action()));
            Invalid(() => Entry("a", 0, true, "기물", Action(), Action("sell"), Action("other")));
            Invalid(() => Entry("a", 2, true, "기물", Action()));
            Invalid(() => Entry("a", 0, true, "기물", new object[] { null }));
            Invalid(() => Model(" ")); Invalid(() => Model(revision: " ")); Invalid(() => Model(tab: 9));
            Invalid(() => Model("s", "r", 0, "a", Entry(), Entry()));
            yield return null;
        }
        [UnityTest] public IEnumerator OpenDeepCopiesPresentationEntriesAndActions()
        {
            object model = Demo(); Call("Open", model, true); object copied = Field(view, "model");
            Assert.AreNotSame(model, copied);
            var before = (IList)Read(model, "Entries"); var after = (IList)Read(copied, "Entries"); Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreNotSame(before[i], after[i]);
                var oldActions = (IList)Read(before[i], "Actions"); var newActions = (IList)Read(after[i], "Actions");
                for (int j = 0; j < oldActions.Count; j++) Assert.AreNotSame(oldActions[j], newActions[j]);
            }
            Assert.AreEqual("bandage", Selected); yield return null;
        }
        [UnityTest] public IEnumerator AllTabsHaveExpectedRowsAndLedgerIsReadOnly()
        {
            Open(); int[] counts = { 4, 2, 3 };
            for (int i = 0; i < 3; i++)
            {
                Call("SelectTab", TabValue(i)); Assert.AreEqual(TabValue(i), Prop("CurrentTab"));
                Assert.AreEqual(counts[i], ((IList)Field(view, "pageEntries")).Count); Assert.IsTrue(Tab(i).isOn);
            }
            Assert.IsFalse(Primary.gameObject.activeSelf); Assert.IsFalse(Secondary.gameObject.activeSelf); yield return null;
        }
        [UnityTest] public IEnumerator SelectionIgnoresOtherTabsAndInvalidInitialFallsBack()
        {
            Open(); Call("SelectEntry", "medical"); Assert.AreEqual("bandage", Selected);
            Call("SelectEntry", "bread"); Assert.AreEqual("bread", Selected);
            Call("SelectEntry", "unknown"); Assert.AreEqual("bread", Selected);
            Call("Open", Model("s", "r", 0, "missing", Entry("first"), Entry("second")), false);
            Assert.AreEqual("first", Selected); yield return null;
        }
        [UnityTest] public IEnumerator UnavailableRowIsSelectableButNeverRequests()
        {
            Capture(); Open(); Call("SelectEntry", "backpack"); Assert.IsTrue(RowButton(3).interactable);
            Assert.IsFalse(Primary.interactable); Call("RequestAction", "buy"); Assert.AreEqual(0, requests);
            Assert.IsFalse(Busy); Assert.AreEqual("backpack", Selected);
            var detail = Field(view, "detail"); Assert.IsNotEmpty(((TMP_Text)detail.GetType().GetField("reason").GetValue(detail)).text); yield return null;
        }
        [UnityTest] public IEnumerator DisabledNativeRowCannotSelectViaSubmit()
        {
            Open(); RowButton(0).interactable = false;
            ExecuteEvents.Execute(RowButton(0).gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            Assert.AreEqual("bandage", Selected); RowButton(0).interactable = true;
            ExecuteEvents.Execute(RowButton(0).gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            Assert.AreEqual("bread", Selected); yield return null;
        }
        [UnityTest] public IEnumerator DisabledActionRemainsVisibleWithoutDispatch()
        {
            Capture(); Call("Open", Model("s", "r", 0, "a", Entry("a", 0, true, "기물", Action(enabled: false))), true);
            Assert.IsTrue(Primary.gameObject.activeSelf); Assert.IsFalse(Primary.interactable);
            Call("RequestAction", "buy"); Assert.IsFalse(Busy); Assert.AreEqual(0, requests); yield return null;
        }
        [UnityTest] public IEnumerator BuyRequestHasCompleteCorrelationAndFocusMovesBeforeDisable()
        {
            Capture(); Open(); events.SetSelectedGameObject(Primary.gameObject); Call("RequestAction", "buy");
            Assert.AreEqual(1, requests); Assert.AreEqual("buy", Read(request, "ActionId")); Assert.AreEqual("bandage", Read(request, "EntryId"));
            Assert.AreEqual("visual-demo", Read(request, "SessionId")); Assert.AreEqual("demo-1", Read(request, "Revision"));
            Assert.IsNotEmpty((string)Read(request, "ViewId")); Assert.Greater((long)Read(request, "Epoch"), 0); Assert.Greater((long)Read(request, "RequestToken"), 0);
            Assert.IsFalse(Primary.interactable); Assert.IsFalse(Secondary.interactable); Assert.IsTrue(CloseButton.interactable);
            Assert.AreEqual(CloseButton.gameObject, events.currentSelectedGameObject); yield return null;
        }
        [UnityTest] public IEnumerator SellRequestsTheExplicitSecondAction()
        {
            Capture(); Open(); ExecuteEvents.Execute(Secondary.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            Assert.AreEqual(1, requests); Assert.AreEqual("sell", Read(request, "ActionId")); Assert.AreEqual("bandage", Read(request, "EntryId")); yield return null;
        }
        [UnityTest] public IEnumerator BusyStillAllowsTabsRowsAndClose()
        {
            Request(); Call("SelectTab", TabValue(1)); Assert.AreEqual(TabValue(1), Prop("CurrentTab")); Assert.IsTrue(Tab(1).interactable);
            Call("SelectEntry", "facility"); Assert.AreEqual("facility", Selected); Assert.IsTrue(RowButton(1).interactable);
            Call("RequestAction", "request"); Assert.AreEqual(1, requests); Assert.IsTrue(Busy);
            Call("Close"); Assert.IsFalse(Visible); Assert.IsFalse(Busy); yield return null;
        }
        [UnityTest] public IEnumerator BusyPresentationUpdateKeepsOriginalPendingRevision()
        {
            Request(); object old = request;
            Assert.IsTrue((bool)Call("SetPresentation", Model("visual-demo", "revision-new", 0, "a", Entry("new", 0, true, "기물", Action()))));
            Assert.IsTrue(Busy); Assert.AreSame(old, Prop("PendingRequest")); Assert.AreEqual("new", Selected);
            Assert.AreEqual("demo-1", Read(old, "Revision")); Assert.IsTrue((bool)Call("AcceptAction", old, "성공", null)); Assert.IsFalse(Busy); yield return null;
        }
        [UnityTest] public IEnumerator DifferentSessionRefreshIsRejectedWithoutConsumingPending()
        {
            Request(); object wrong = Model("other", "r", 0, "a", Entry());
            Assert.IsFalse((bool)Call("SetPresentation", wrong)); Assert.IsFalse((bool)Call("AcceptAction", request, "성공", wrong));
            Assert.IsTrue(Busy); Assert.AreSame(request, Prop("PendingRequest")); yield return null;
        }
        [UnityTest] public IEnumerator SameFrameDuplicateIsBlockedEvenAfterSynchronousCompletion()
        {
            Open(); Subscribe(view, value => { request = value; requests++; Call("AcceptAction", value, "성공", null); });
            Call("RequestAction", "buy"); Call("RequestAction", "sell"); Assert.AreEqual(1, requests); Assert.IsFalse(Busy);
            yield return null; Call("RequestAction", "sell"); Assert.AreEqual(2, requests);
        }
        [UnityTest] public IEnumerator DuplicateAcceptAndRejectResponsesAreIgnored()
        {
            Request(); Assert.IsTrue((bool)Call("AcceptAction", request, "성공", null)); Assert.IsTrue(Visible); Assert.IsFalse(Busy);
            Assert.IsFalse((bool)Call("AcceptAction", request, "중복", null)); Assert.IsFalse((bool)Call("RejectAction", request, "중복"));
            yield return null; Call("RequestAction", "sell"); Assert.IsTrue((bool)Call("RejectAction", request, "거절"));
            Assert.IsFalse((bool)Call("RejectAction", request, "중복")); Assert.IsTrue(Primary.interactable);
        }
        [UnityTest] public IEnumerator CloseAndReopenInvalidateStaleResponses()
        {
            Request(); object old = request; Call("Close"); Open();
            Assert.IsFalse((bool)Call("AcceptAction", old, "늦은 응답", null)); Assert.IsFalse((bool)Call("RejectAction", old, "늦은 응답"));
            Assert.IsTrue(Visible); Assert.IsFalse(Busy); Assert.AreEqual("bandage", Selected); yield return null;
        }
        [UnityTest] public IEnumerator DisableInvalidatesPendingAndReenableDoesNotDuplicateBinding()
        {
            Request(); object old = request; view.enabled = false; view.enabled = true;
            Assert.IsFalse(Visible); Assert.IsFalse((bool)Call("AcceptAction", old, "늦은 응답", null));
            yield return null; Open(); Call("RequestAction", "buy"); Assert.AreEqual(2, requests);
        }
        [UnityTest] public IEnumerator OtherViewsTokenCannotConsumeThisViewsPending()
        {
            Request(); var clone = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/ShopUi/ShopPanel")); extra.Add(clone);
            var other = Find(clone); object otherRequest = null; Subscribe(other, value => otherRequest = value);
            Invoke(other, "Open", Demo(), false); Invoke(other, "RequestAction", "buy"); Assert.NotNull(otherRequest);
            Assert.AreNotEqual(Read(request, "ViewId"), Read(otherRequest, "ViewId"));
            Assert.IsFalse((bool)Call("AcceptAction", otherRequest, "잘못된 화면", null)); Assert.IsTrue(Busy); yield return null;
        }
        [UnityTest] public IEnumerator TokensIncreaseAndOldTokenCannotReleaseRetry()
        {
            Request(); object first = request; Call("RejectAction", first, "거절"); yield return null;
            Call("RequestAction", "buy"); object second = request;
            Assert.Greater((long)Read(second, "RequestToken"), (long)Read(first, "RequestToken"));
            Assert.IsFalse((bool)Call("AcceptAction", first, "이전", null)); Assert.IsTrue(Busy);
            Call("Hide"); Open(); yield return null; Call("RequestAction", "sell");
            Assert.Greater((long)Read(request, "Epoch"), (long)Read(second, "Epoch"));
        }
        [UnityTest] public IEnumerator RequestCallbackMayCloseAndReopenWithoutOldWrites()
        {
            Open(); Subscribe(view, _ => { Call("Close"); Open(); }); Call("RequestAction", "buy");
            Assert.IsTrue(Visible); Assert.IsFalse(Busy); Assert.IsNull(Prop("PendingRequest")); Assert.AreEqual("bandage", Selected); yield return null;
        }
        [UnityTest] public IEnumerator SynchronousAcceptMaySupplyFreshAuthorityDisplay()
        {
            Open(); Subscribe(view, value => Assert.IsTrue((bool)Call("AcceptAction", value, "완료", Model("visual-demo", "fresh", 0, "new", Entry("new")))));
            Call("RequestAction", "buy"); Assert.IsFalse(Busy); Assert.AreEqual("new", Selected);
            Assert.AreEqual("fresh", Read(Field(view, "model"), "Revision")); yield return null;
        }
        [UnityTest] public IEnumerator ClosedCallbackCanReopenWithoutOldFadeHidingNewView()
        {
            Action reopen = Open; var info = view.GetType().GetEvent("Closed"); info.AddEventHandler(view, reopen);
            try
            {
                Open(); Call("Hide"); Assert.IsTrue(Visible); yield return new WaitForSecondsRealtime(.3f);
                Assert.IsTrue(((CanvasGroup)Field(view, "visuals")).gameObject.activeSelf); Assert.IsTrue(Visible);
            }
            finally { info.RemoveEventHandler(view, reopen); }
        }
        [UnityTest] public IEnumerator CloseHideAndDisableHaveDistinctIntentNotifications()
        {
            int intent = 0, closed = 0;
            view.GetType().GetEvent("CloseRequested").AddEventHandler(view, (Action)(() => intent++));
            view.GetType().GetEvent("Closed").AddEventHandler(view, (Action)(() => closed++));
            Open(); Call("Hide"); Assert.AreEqual(0, intent); Assert.AreEqual(1, closed);
            Open(); Call("Close"); Assert.AreEqual(1, intent); Assert.AreEqual(2, closed);
            Open(); view.enabled = false; Assert.AreEqual(1, intent); Assert.AreEqual(3, closed); yield return null;
        }
        [UnityTest] public IEnumerator EmptyTabsHaveNoSelectionDetailOrActions()
        {
            Call("Open", Sample("empty"), true);
            for (int i = 0; i < 3; i++)
            {
                Call("SelectTab", TabValue(i)); Assert.AreEqual("", Selected); Assert.IsFalse(Primary.gameObject.activeSelf); Assert.IsFalse(Secondary.gameObject.activeSelf);
                Assert.IsTrue(((TMP_Text)Field(view, "emptyMessage")).gameObject.activeSelf);
                Assert.IsFalse(((RectTransform)Field(view, "detail").GetType().GetField("root").GetValue(Field(view, "detail"))).gameObject.activeSelf);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LongListClonesRowsAndFocusScrollsLastIntoView()
        {
            Call("Open", Sample("long"), true); Canvas.ForceUpdateCanvases(); yield return null;
            Assert.GreaterOrEqual(Rows.Count, 12); var content = (RectTransform)Field(view, "listContent");
            Assert.AreEqual(12 * 84 - 4, content.sizeDelta.y); Assert.AreEqual(12, ((IList)Field(view, "pageEntries")).Count);
            for (int i = 0; i < 12; i++)
            {
                var rect = (RectTransform)Row(i).transform;
                Assert.AreEqual(new Vector2(592, 80), rect.sizeDelta);
                Assert.AreEqual(new Vector2(0, -84 * i), rect.anchoredPosition);
                var title = ((TMP_Text)Field(Row(i), "title")).rectTransform;
                var subtitle = ((TMP_Text)Field(Row(i), "subtitle")).rectTransform;
                Assert.AreEqual(-12, title.anchoredPosition.y); Assert.AreEqual(-44, subtitle.anchoredPosition.y);
                Assert.AreEqual(12, 80 + subtitle.anchoredPosition.y - subtitle.sizeDelta.y);
            }
            events.SetSelectedGameObject(RowButton(11).gameObject);
            Assert.Greater(content.anchoredPosition.y, 0); var scroll = (UnityEngine.UI.ScrollRect)Field(view, "listScroll");
            Assert.That(content.anchoredPosition.y + scroll.viewport.rect.height, Is.GreaterThanOrEqualTo(11 * 84 + 80 - .01f));
            Assert.IsFalse(scroll.inertia); Assert.IsFalse(scroll.horizontal);
        }
        [UnityTest] public IEnumerator NativeMoveSubmitAndCancelAreWired()
        {
            Open(); events.SetSelectedGameObject(Tab(0).gameObject);
            ExecuteEvents.Execute(Tab(0).gameObject, new AxisEventData(events) { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
            Assert.AreEqual(Tab(1).gameObject, events.currentSelectedGameObject);
            ExecuteEvents.Execute(Tab(1).gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Assert.AreEqual(TabValue(1), Prop("CurrentTab"));
            Call("SelectTab", TabValue(0)); ExecuteEvents.Execute(RowButton(0).gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Assert.AreEqual("bread", Selected);
            Capture(); ExecuteEvents.Execute(Primary.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Assert.AreEqual(1, requests);
            var cancel = new BaseEventData(events); ExecuteEvents.Execute(RowButton(0).gameObject, cancel, ExecuteEvents.cancelHandler);
            Assert.IsFalse(Visible); Assert.IsTrue(cancel.used); yield return null;
        }
        [UnityTest] public IEnumerator FadeRunsUnscaledAndDoesNotPauseGameplay()
        {
            Time.timeScale = 0; Open(); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(((CanvasGroup)Field(view, "visuals")).alpha, Is.EqualTo(1).Within(.001f));
            Call("Hide"); yield return new WaitForSecondsRealtime(.2f);
            Assert.IsFalse(((CanvasGroup)Field(view, "visuals")).gameObject.activeSelf); Assert.AreEqual(0, Time.timeScale);
        }
        [UnityTest] public IEnumerator LongTextRemainsLiteralAndEllipsizedAtFixedSize()
        {
            Call("Open", Sample("text"), true); yield return null; Canvas.ForceUpdateCanvases();
            var label = (TMP_Text)Field(Row(0), "title"); label.ForceMeshUpdate();
            Assert.IsTrue(label.text.StartsWith("<b>")); Assert.IsFalse(label.richText); Assert.IsFalse(label.enableAutoSizing);
            Assert.AreEqual(24, label.fontSize); Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
            Assert.IsTrue(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.character == '…' && c.isVisible)); yield return null;
        }
        [UnityTest] public IEnumerator AllSerializedComponentReferencesRemainInsideOwnedCanvas()
        {
            foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                foreach (var component in node.GetComponents<Component>()) Assert.NotNull(component, node.name);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                for (Type type = behaviour.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if ((field.IsPublic || field.IsDefined(typeof(SerializeField), true)) && field.GetValue(behaviour) is Component reference)
                            Assert.IsTrue(reference.transform == instance.transform || reference.transform.IsChildOf(instance.transform), field.Name);
            Open(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = new Vector2(4, 4) }, hits);
            Assert.IsFalse(hits.Any(h => h.gameObject.transform.IsChildOf(instance.transform)));
        }
        [UnityTest] public IEnumerator MissingConfigurationFailsBeforeMutatingOpenState()
        {
            Open(); object before = Field(view, "model");
            foreach (string field in new[] { "layoutStage", "context", "title", "emptyMessage", "footerMessage", "primaryLabel", "secondaryLabel" })
            {
                object saved = Field(view, field); SetField(view, field, null);
                try
                {
                    var error = Assert.Throws<TargetInvocationException>(() => Call("Open", Demo(), true));
                    Assert.IsInstanceOf<InvalidOperationException>(error.InnerException); Assert.IsTrue(Visible); Assert.AreSame(before, Field(view, "model"));
                }
                finally { SetField(view, field, saved); }
            }
            var scroll = (UnityEngine.UI.ScrollRect)Field(view, "listScroll"); var viewport = scroll.viewport; scroll.viewport = null;
            try
            {
                var error = Assert.Throws<TargetInvocationException>(() => Call("Open", Demo(), true));
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException); Assert.AreSame(before, Field(view, "model"));
            }
            finally { scroll.viewport = viewport; }
            yield return null;
        }
        [UnityTest] public IEnumerator OpenDemoInitialFocusMatchesEveryRequestedTab()
        {
            for (int i = 0; i < 3; i++)
            {
                Call("OpenDemo", TabValue(i)); Assert.AreEqual(TabValue(i), Prop("CurrentTab")); Assert.IsTrue(Tab(i).isOn);
                Assert.AreEqual(Tab(i).gameObject, events.currentSelectedGameObject); Call("Hide");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator NullRowBindingClearsPreviouslyVisibleIcons()
        {
            Open(); var row = Row(0); var icon = (UnityEngine.UI.Image)Field(row, "icon"); var price = (UnityEngine.UI.Image)Field(row, "priceIcon");
            Assert.NotNull(icon.sprite); Assert.IsTrue(icon.enabled);
            Invoke(row, "BindEntry", view, null, false);
            Assert.IsNull(icon.sprite); Assert.IsFalse(icon.enabled); Assert.IsNull(price.sprite); Assert.IsFalse(price.enabled);
            Assert.IsFalse(RowButton(0).interactable); Assert.AreEqual("", Read(row, "EntryId")); yield return null;
        }
        [UnityTest] public IEnumerator UnknownActionAndUnconnectedRequestLeaveViewInteractive()
        {
            Open(); Call("RequestAction", "unknown"); Assert.IsFalse(Busy); Assert.IsNull(Prop("PendingRequest"));
            Call("RequestAction", "buy"); Assert.IsFalse(Busy); Assert.IsNull(Prop("PendingRequest")); Assert.IsTrue(Primary.interactable);
            Assert.AreEqual("현재 요청할 수 없습니다.", ((TMP_Text)Field(view, "footerMessage")).text);
            yield return null;
        }
        [UnityTest] public IEnumerator PreviewRestoresGlobalValuesAndGuardsDisabledCallbacks()
        {
            Time.timeScale = .75f; bool background = Application.runInBackground;
            var go = new GameObject("ShopPreviewTest"); extra.Add(go); var preview = (MonoBehaviour)go.AddComponent(Domain("ShopUiPreview"));
            var buttons = new UnityEngine.UI.Button[24];
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = new GameObject("PreviewButton_" + i, typeof(RectTransform), typeof(UnityEngine.UI.Button)); button.transform.SetParent(go.transform, false);
                buttons[i] = button.GetComponent<UnityEngine.UI.Button>();
            }
            Invoke(preview, "Configure", view, null, null, buttons);
            buttons[15].interactable = false; Invoke(preview, "TogglePause"); Assert.AreEqual(.75f, Time.timeScale); Assert.AreEqual(0, Read(preview, "ActionCount"));
            buttons[15].interactable = true; Invoke(preview, "TogglePause"); Assert.AreEqual(0, Time.timeScale);
            Invoke(preview, "ToggleRunInBackground"); Assert.AreNotEqual(background, Application.runInBackground);
            preview.enabled = false; Assert.AreEqual(.75f, Time.timeScale); Assert.AreEqual(background, Application.runInBackground);
            Invoke(preview, "OpenDemo"); Assert.IsFalse(Visible); yield return null;
        }
    }
}
