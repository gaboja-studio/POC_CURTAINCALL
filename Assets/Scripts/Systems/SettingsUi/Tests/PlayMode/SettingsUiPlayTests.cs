using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.SettingsUi.Tests
{
    public sealed class SettingsUiPlayTests
    {
        private GameObject instance;
        private MonoBehaviour view;
        private EventSystem events;
        private GameObject ownedEvents;
        private float previousScale;
        private object request;
        private int requests;
        private Type ViewType => view.GetType();
        private object Call(string method, params object[] args) => ViewType.GetMethod(method).Invoke(view, args);
        private object Prop(string name) => ViewType.GetProperty(name).GetValue(view);
        private object Field(string name) => ViewType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        private static object Read(object source, string name) => source.GetType().GetProperty(name).GetValue(source);
        private bool Visible => (bool)Prop("IsVisible");
        private bool Busy => (bool)Prop("IsBusy");
        private bool Dirty => (bool)Prop("IsDirty");
        private long Version => (long)Prop("SessionVersion");
        private Array Rows => (Array)Field("rows");
        private Array Tabs => (Array)Field("tabs");
        private T Row<T>(int index, string name) => (T)Rows.GetValue(index).GetType().GetField(name).GetValue(Rows.GetValue(index));
        private Slider Slider(int index) => (Slider)Read(Row<object>(index, "slider"), "NativeSlider");
        private Toggle Toggle(int index) => (Toggle)Read(Row<object>(index, "toggle"), "NativeToggle");
        private Toggle Tab(int index) => (Toggle)Read(Tabs.GetValue(index), "NativeToggle");
        private Button Apply => (Button)Field("applyButton");
        private Button Cancel => (Button)Field("cancelButton");
        private void Open() => Call("OpenDemo", true);
        private Array Pages() => (Array)ViewType.GetMethod("CreateDemoPages").Invoke(null, null);
        private static Array Definitions(Array pages, int index) => (Array)pages.GetValue(index).GetType().GetField("rows").GetValue(pages.GetValue(index));
        private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
        private static float Value(object snapshot, string id)
        {
            foreach (object value in (IEnumerable)Read(snapshot, "Values"))
                if ((string)Read(value, "Id") == id) return (float)Read(value, "Value");
            Assert.Fail("스냅샷 항목 누락: " + id); return -1;
        }
        private void Subscribe(Action<object> handler)
        {
            EventInfo info = ViewType.GetEvent("ApplyRequested");
            var parameter = Expression.Parameter(info.EventHandlerType.GenericTypeArguments[0]);
            var callback = Expression.Lambda(info.EventHandlerType,
                Expression.Invoke(Expression.Constant(handler), Expression.Convert(parameter, typeof(object))), parameter).Compile();
            info.AddEventHandler(view, callback);
        }
        private void Capture()
        {
            Subscribe(value => { request = value; requests++; });
        }
        private void Change() { Slider(0).value = 0.25f; Assert.IsTrue(Dirty); }
        private void RejectInvalid(Array pages)
        {
            var error = Assert.Throws<TargetInvocationException>(() => Call("Open", pages, true));
            Assert.IsInstanceOf<ArgumentException>(error.InnerException);
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScale = Time.timeScale; Time.timeScale = 1; requests = 0; request = null;
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/SettingsUi/SettingsPanel");
            Assert.NotNull(prefab);
            instance = Object.Instantiate(prefab);
            view = instance.GetComponentsInChildren<MonoBehaviour>(true).Single(x => x != null && x.GetType().FullName == "CurtainCall.UI.SettingsPanelView");
            events = EventSystem.current;
            if (events == null) { ownedEvents = new GameObject("SettingsTestEventSystem", typeof(EventSystem)); events = ownedEvents.GetComponent<EventSystem>(); }
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = previousScale;
            if (instance != null) Object.Destroy(instance);
            if (ownedEvents != null) Object.Destroy(ownedEvents);
            yield return null;
        }
        [UnityTest] public IEnumerator InitialHiddenAndTypography()
        {
            Assert.IsFalse(Visible); Assert.IsFalse(Busy); Assert.IsFalse(Dirty);
            Assert.IsFalse(((CanvasGroup)Field("visuals")).gameObject.activeSelf);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, instance.GetComponent<Canvas>().renderMode);
            Assert.NotNull(instance.GetComponent<GraphicRaycaster>());
            var scaler = instance.GetComponent<CanvasScaler>();
            Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution); Assert.AreEqual(0.5f, scaler.matchWidthOrHeight);
            foreach (var label in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.NotNull(label.font); Assert.AreEqual(AtlasPopulationMode.Static, label.font.atlasPopulationMode);
                Assert.IsFalse(label.richText); Assert.IsFalse(label.parseCtrlCharacters); Assert.IsFalse(label.enableAutoSizing); Assert.IsFalse(label.raycastTarget);
                Assert.IsTrue(label.font.HasCharacters(label.text));
                if (label.font.name.Contains("Medium") || label.font.name.Contains("SemiBold"))
                    Assert.IsTrue(label.font.HasCharacters("설정소리화면조작적용취소끔켬…가나다힣"));
            }
            foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                foreach (var component in node.GetComponents<Component>()) Assert.NotNull(component);
            yield return null;
        }
        [UnityTest] public IEnumerator OpenCopiesCallerDataAndBindsWithoutChanges()
        {
            var pages = Pages(); Call("Open", pages, true);
            Set(Definitions(pages, 0).GetValue(0), "value", 0f);
            Assert.AreEqual(0.8f, Value(Call("GetSnapshot"), "master_volume"));
            Assert.IsTrue(Visible); Assert.IsFalse(Dirty); Assert.IsFalse(Apply.interactable);
            Assert.AreEqual(0, (int)Prop("CurrentPage"));
            yield return null;
        }
        [UnityTest] public IEnumerator TabsPreserveEditsAndReturningToBaselineClearsDirty()
        {
            Open(); Change(); Call("ShowPage", 1); Toggle(1).isOn = false;
            Call("ShowPage", 0); Assert.AreEqual(0.25f, Slider(0).value);
            Slider(0).value = 0.8f; Assert.IsTrue(Dirty);
            Call("ShowPage", 1); Toggle(1).isOn = true; Assert.IsFalse(Dirty);
            Assert.IsFalse(Apply.interactable); yield return null;
        }
        [UnityTest] public IEnumerator SnapshotIsIndependentAcrossEditsAndCancel()
        {
            Open(); var old = Call("GetSnapshot"); Change(); Call("Cancel"); Open();
            Assert.AreEqual(0.8f, Value(old, "master_volume"));
            Assert.AreEqual(0.8f, Value(Call("GetSnapshot"), "master_volume")); yield return null;
        }
        [UnityTest] public IEnumerator ApplyLocksControlsAndMovesFocusToCancel()
        {
            Open(); Change(); Capture(); events.SetSelectedGameObject(Apply.gameObject); Call("RequestApply");
            Assert.AreEqual(1, requests); Assert.IsTrue(Busy); Assert.IsFalse(Apply.interactable);
            Assert.IsFalse(Tab(0).interactable); Assert.IsFalse(Slider(0).interactable); Assert.IsTrue(Cancel.interactable);
            Assert.AreEqual(Cancel.gameObject, events.currentSelectedGameObject);
            Call("ShowPage", 1); Assert.AreEqual(0, (int)Prop("CurrentPage"));
            Call("RequestApply"); Assert.AreEqual(1, requests); yield return null;
        }
        [UnityTest] public IEnumerator RejectRetryAndDuplicateResponsesUseDistinctTokens()
        {
            Open(); Change(); Capture(); Call("RequestApply"); long first = Version;
            Assert.IsTrue((bool)Call("RejectApply", first, "검증용 거절")); Assert.IsFalse(Busy); Assert.IsTrue(Dirty);
            Assert.IsTrue(Slider(0).interactable); yield return null;
            Call("RequestApply"); long second = Version; Assert.Greater(second, first);
            Assert.IsFalse((bool)Call("AcceptApplied", first)); Assert.IsTrue(Busy);
            Assert.IsTrue((bool)Call("AcceptApplied", second)); Assert.IsFalse(Visible);
            Assert.IsFalse((bool)Call("AcceptApplied", second));
        }
        [UnityTest] public IEnumerator CancelAndReopenRejectsLateResponse()
        {
            Open(); Change(); Capture(); Call("RequestApply"); long old = Version;
            Call("Cancel"); Open(); Assert.IsFalse((bool)Call("RejectApply", old, "늦은 응답"));
            Assert.IsFalse((bool)Call("AcceptApplied", old)); Assert.IsTrue(Visible); Assert.IsFalse(Busy); yield return null;
        }
        [UnityTest] public IEnumerator UnconnectedApplyReturnsToInteraction()
        {
            Open(); Change(); Call("RequestApply"); Assert.IsFalse(Busy); Assert.IsTrue(Visible); Assert.IsTrue(Dirty);
            Assert.AreEqual("적용 동작을 연결해 주세요", ((TMP_Text)Field("footerMessage")).text); yield return null;
        }
        [UnityTest] public IEnumerator CancelHideAndDisabledNotifications()
        {
            int cancelled = 0, closed = 0;
            ((UnityEvent)Prop("CancelRequested")).AddListener(() => cancelled++);
            ((UnityEvent)Prop("Closed")).AddListener(() => closed++);
            Open(); Call("Hide"); Assert.AreEqual(0, cancelled); Assert.AreEqual(1, closed);
            Open(); Call("Cancel"); Assert.AreEqual(1, cancelled); Assert.AreEqual(2, closed);
            Open(); view.enabled = false; Assert.AreEqual(3, closed); Assert.IsFalse(Visible);
            view.enabled = true; Open(); Slider(0).value = 0.3f; Capture(); Call("RequestApply"); Assert.AreEqual(1, requests); yield return null;
        }
        [UnityTest] public IEnumerator DisableInvalidatesPendingToken()
        {
            Open(); Change(); Capture(); Call("RequestApply"); long token = Version;
            view.gameObject.SetActive(false); view.gameObject.SetActive(true);
            Assert.IsFalse((bool)Call("AcceptApplied", token)); Assert.IsFalse(Visible); Open(); Assert.IsFalse(Busy); yield return null;
        }
        [UnityTest] public IEnumerator CallbackCanReopenWithoutOldStateOverwrite()
        {
            Open(); Change(); Subscribe(_ => { Call("Cancel"); Open(); }); Call("RequestApply");
            Assert.IsTrue(Visible); Assert.IsFalse(Busy); Assert.IsFalse(Dirty); yield return null;
        }
        [UnityTest] public IEnumerator ClosedCallbackCanReopen()
        {
            var closed = (UnityEvent)Prop("Closed");
            UnityAction reopen = Open;
            closed.AddListener(reopen);
            try { Open(); Call("Hide"); Assert.IsTrue(Visible); Assert.IsFalse(Dirty); }
            finally { closed.RemoveListener(reopen); }
            yield return null;
        }
        [UnityTest] public IEnumerator DisabledRowsRetainValuesAndNativeInputCannotChangeModel()
        {
            var pages = Pages(); Set(Definitions(pages, 0).GetValue(0), "enabled", false);
            Call("Open", pages, true); Assert.IsFalse(Slider(0).interactable); Slider(0).value = 0;
            Assert.AreEqual(0.8f, Value(Call("GetSnapshot"), "master_volume")); Assert.IsFalse(Dirty); yield return null;
        }
        [UnityTest] public IEnumerator InvalidDefinitionsLeaveOpenSessionUnchanged()
        {
            Open(); var pages = Pages(); Set(Definitions(pages, 0).GetValue(0), "value", float.NaN); RejectInvalid(pages);
            pages = Pages(); Set(Definitions(pages, 1).GetValue(1), "value", 0.5f); RejectInvalid(pages);
            pages = Pages(); Set(Definitions(pages, 1).GetValue(0), "id", "master_volume"); RejectInvalid(pages);
            pages = Pages(); Set(pages.GetValue(1), "id", "audio"); RejectInvalid(pages);
            pages = Pages(); Set(pages.GetValue(0), "label", " "); RejectInvalid(pages);
            foreach (float value in new[] { float.PositiveInfinity, -0.1f, 1.1f })
            { pages = Pages(); Set(Definitions(pages, 0).GetValue(0), "value", value); RejectInvalid(pages); }
            pages = Pages(); RejectInvalid(Array.CreateInstance(pages.GetType().GetElementType(), 0));
            RejectInvalid(Array.CreateInstance(pages.GetType().GetElementType(), 4));
            pages = Pages(); var tooMany = Array.CreateInstance(Definitions(pages, 0).GetType().GetElementType(), 4);
            Set(pages.GetValue(0), "rows", tooMany); RejectInvalid(pages);
            Assert.IsTrue(Visible); Assert.AreEqual(0.8f, Value(Call("GetSnapshot"), "master_volume")); yield return null;
        }
        [UnityTest] public IEnumerator EmptyPageAndSinglePageNavigationAreValid()
        {
            var pages = Pages(); var one = Array.CreateInstance(pages.GetType().GetElementType(), 1); one.SetValue(pages.GetValue(0), 0);
            var rows = Definitions(one, 0); Set(one.GetValue(0), "rows", Array.CreateInstance(rows.GetType().GetElementType(), 0));
            Call("Open", one, false); Assert.IsFalse(Row<RectTransform>(0, "root").gameObject.activeSelf);
            Assert.IsFalse(Tab(1).gameObject.activeSelf); Assert.IsNull(Tab(0).navigation.selectOnRight);
            Assert.IsFalse(((TMP_Text)Field("gameplayHint")).gameObject.activeSelf); yield return null;
        }
        [UnityTest] public IEnumerator NativeMoveSubmitAndOwnedCancelWork()
        {
            Open(); var axis = new AxisEventData(events) { moveDir = MoveDirection.Right, moveVector = Vector2.right };
            float before = Slider(0).value; ExecuteEvents.Execute(Slider(0).gameObject, axis, ExecuteEvents.moveHandler);
            Assert.Greater(Slider(0).value, before); Assert.IsNull(Slider(0).navigation.selectOnLeft); Assert.IsNull(Slider(0).navigation.selectOnRight);
            ExecuteEvents.Execute(Tab(1).gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            Assert.AreEqual(1, (int)Prop("CurrentPage")); bool on = Toggle(1).isOn;
            ExecuteEvents.Execute(Toggle(1).gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Assert.AreNotEqual(on, Toggle(1).isOn);
            ExecuteEvents.Execute(Toggle(1).gameObject, new BaseEventData(events), ExecuteEvents.cancelHandler); Assert.IsFalse(Visible); yield return null;
        }
        [UnityTest] public IEnumerator SliderGeometryAtZeroHalfAndOne()
        {
            Open();
            foreach (float value in new[] { 0f, 0.5f, 1f })
            {
                Slider(0).value = value; Canvas.ForceUpdateCanvases(); yield return null;
                Assert.That(Slider(0).handleRect.rect.height, Is.EqualTo(32).Within(0.01f));
                Assert.That(Slider(0).handleRect.rect.width, Is.EqualTo(32).Within(0.01f));
                Assert.That(Slider(0).fillRect.anchorMax.x, Is.EqualTo(value).Within(0.001f));
                Assert.AreEqual((value * 100).ToString("0") + "%", Row<TMP_Text>(0, "sliderValue").text);
            }
        }
        [UnityTest] public IEnumerator FadeAndToggleMotionUseUnscaledTime()
        {
            Time.timeScale = 0; Open(); yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(((CanvasGroup)Field("visuals")).alpha, Is.EqualTo(1).Within(0.001f));
            Call("ShowPage", 1); Toggle(1).isOn = false; yield return new WaitForSecondsRealtime(0.12f);
            Call("Hide"); yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(((CanvasGroup)Field("visuals")).gameObject.activeSelf); Assert.AreEqual(0, Time.timeScale);
        }
        [UnityTest] public IEnumerator LongLabelsRemainLiteralAndEllipsized()
        {
            var pages = Pages(); string text = "<b>" + string.Concat(Enumerable.Repeat("설정 항목 가나다 ", 12)) + "</b>";
            Set(Definitions(pages, 0).GetValue(0), "label", text); Call("Open", pages, true);
            var label = Row<TMP_Text>(0, "label"); label.ForceMeshUpdate();
            Assert.AreEqual(text, label.text); Assert.IsFalse(label.richText); Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
            Assert.IsTrue(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(x => x.character == '…' && x.isVisible)); yield return null;
        }
        [UnityTest] public IEnumerator PanelDoesNotCreateFullscreenInputBlocker()
        {
            Open(); yield return new WaitForSecondsRealtime(0.2f); Canvas.ForceUpdateCanvases();
            var point = new PointerEventData(events) { position = new Vector2(4, 4) };
            var hits = new System.Collections.Generic.List<RaycastResult>(); events.RaycastAll(point, hits);
            Assert.IsFalse(hits.Any(hit => hit.gameObject.transform.IsChildOf(instance.transform)));
            Assert.AreEqual(1, Time.timeScale);
        }
        [UnityTest] public IEnumerator HidePreservesAnotherPanelsSelection()
        {
            var other = new GameObject("OtherPanelControl", typeof(RectTransform), typeof(Button));
            other.transform.SetParent(instance.transform, false);
            Open(); events.SetSelectedGameObject(other); Call("Hide");
            Assert.AreEqual(other, events.currentSelectedGameObject); yield return null;
        }
        [UnityTest] public IEnumerator RebindingDoesNotEmitNativeValueEvents()
        {
            int changes = 0; Open();
            foreach (object tab in Tabs) ((Toggle)Read(tab, "NativeToggle")).onValueChanged.AddListener(_ => changes++);
            for (int i = 0; i < 3; i++) { Slider(i).onValueChanged.AddListener(_ => changes++); Toggle(i).onValueChanged.AddListener(_ => changes++); }
            Call("ShowPage", 1); Call("ShowPage", 2); Call("ShowPage", 0);
            Assert.AreEqual(0, changes); Assert.IsFalse(Dirty); yield return null;
        }
        [UnityTest] public IEnumerator StandaloneControlsHaveLocalReferencesAndRespond()
        {
            foreach (string name in new[] { "CommonTab", "CommonSlider", "CommonToggle" })
            {
                var prefab = Resources.Load<GameObject>("Prefabs/UIs/SettingsUi/" + name); Assert.NotNull(prefab);
                var control = Object.Instantiate(prefab, instance.transform, false);
                foreach (var node in control.GetComponentsInChildren<Transform>(true))
                    foreach (var component in node.GetComponents<Component>()) Assert.NotNull(component);
                Assert.IsFalse(control.GetComponentsInChildren<MonoBehaviour>(true).Any(x => x != null && x.GetType().Name == "SettingsCancelRelay"));
                foreach (var behaviour in control.GetComponentsInChildren<MonoBehaviour>(true))
                    for (Type type = behaviour.GetType(); type != typeof(MonoBehaviour) && type != null; type = type.BaseType)
                        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                            if ((field.IsPublic || field.IsDefined(typeof(SerializeField), true)) && field.GetValue(behaviour) is Component reference)
                                Assert.IsTrue(reference.transform == control.transform || reference.transform.IsChildOf(control.transform), name + ": " + field.Name);
                var slider = control.GetComponentInChildren<Slider>(true);
                var toggle = control.GetComponentInChildren<Toggle>(true);
                if (slider != null) { slider.value = 0.75f; Assert.AreEqual(0.75f, slider.value); }
                if (toggle != null) { bool initial = toggle.isOn; ExecuteEvents.Execute(toggle.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler); Assert.AreNotEqual(initial, toggle.isOn); }
                Object.Destroy(control); yield return null;
            }
        }
    }
}
