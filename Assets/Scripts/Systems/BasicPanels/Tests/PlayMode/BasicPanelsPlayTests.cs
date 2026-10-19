using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CurtainCall.BasicPanels.Tests
{
    public sealed class BasicPanelsPlayTests
    {
        GameObject root;
        GameObject events;
        MonoBehaviour controller;
        Mouse mouse;
        Keyboard keyboard;
        float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            events = new GameObject("PanelTestEvents", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/BasicPanels/BasicPanels");
            Assert.NotNull(prefab, "패널 조립 프리팹");
            root = Object.Instantiate(prefab);
            controller = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "CurtainCallMenuPanels");
            yield return null;
            Invoke("Show");
            yield return new WaitForSecondsRealtime(0.22f);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (root != null) Object.Destroy(root);
            if (events != null) Object.Destroy(events);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrefabGeometryLayersFontsAndBindings()
        {
            var canvas = root.GetComponent<Canvas>();
            var scaler = root.GetComponent<CanvasScaler>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);
            Assert.AreEqual(0.5f, scaler.matchWidthOrHeight);
            Assert.AreEqual(100, scaler.referencePixelsPerUnit);
            Assert.NotNull(root.GetComponent<GraphicRaycaster>());
            var menu = (RectTransform)root.transform.Find("60_Panels/Menu_C13");
            var dialog = (RectTransform)root.transform.Find("70_ConfirmDialogs/LeaveConfirmation");
            Assert.AreEqual(new Vector2(384, 400), menu.sizeDelta);
            Assert.AreEqual(new Vector2(512, 304), dialog.sizeDelta);
            Assert.AreEqual(Vector2.zero, menu.anchoredPosition);
            Assert.AreEqual(5, root.GetComponentsInChildren<Button>(true).Length);
            foreach (var panel in new[] { menu, dialog })
            {
                Assert.AreEqual(new Vector2(0.5f, 0.5f), panel.anchorMin);
                var images = panel.GetComponentsInChildren<Image>(true);
                var body = images.Single(x => x.sprite != null && x.sprite.name.Contains("panel_base_body"));
                Assert.AreEqual(0.92f, body.color.a, 0.001f);
                Assert.AreEqual(Image.Type.Sliced, body.type);
                Assert.AreEqual(new Vector4(64, 64, 64, 64), body.sprite.border);
                Assert.AreEqual(200, body.sprite.pixelsPerUnit);
            }
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.NotNull(text.font, text.name);
                Assert.IsTrue(text.font.HasCharacters(text.text.Replace("\n", "").Replace("\r", "")), text.text);
                Assert.IsFalse(text.raycastTarget);
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                Assert.AreEqual("CurtainCall.UI.ButtonsV1.CurtainCallButton", button.GetType().FullName);
                Assert.AreEqual(1, button.onClick.GetPersistentEventCount(), button.name);
                Assert.AreEqual(controller, button.onClick.GetPersistentTarget(0), button.name);
                Assert.IsTrue(button.GetComponents<MonoBehaviour>().Any(x => x.GetType().Name == "CurtainCallPanelCancelRelay"));
            }
            Assert.AreEqual(new Vector2(320, 56), ((RectTransform)ButtonWithLabel("계속하기").transform).sizeDelta);
            Assert.AreEqual(new Vector2(216, 56), ((RectTransform)ButtonWithLabel("돌아가기").transform).sizeDelta);
            Invoke("RequestLeave");
            yield return new WaitForSecondsRealtime(0.22f);
            Assert.AreEqual(new Vector2(0, -64), dialog.anchoredPosition);
            Assert.AreEqual(0, menu.Find("50_Content/ActionsAndFooter").GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(1, menu.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(ButtonWithLabel("돌아가기").gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator UnscaledTransitionsCancelAndSingleLeaveSignal()
        {
            int leaves = 0;
            Event("OnLeaveConfirmed").AddListener(() => leaves++);
            Time.timeScale = 0;
            Invoke("RequestLeave");
            yield return new WaitForSecondsRealtime(0.22f);
            Assert.IsTrue(State("IsConfirmationOpen"));
            Invoke("Cancel");
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.IsFalse(State("IsConfirmationOpen"));
            Assert.IsTrue(State("IsMenuOpen"));
            Assert.AreEqual(ButtonWithLabel("계속하기").gameObject, EventSystem.current.currentSelectedGameObject);
            Invoke("RequestLeave");
            yield return new WaitForSecondsRealtime(0.22f);
            Invoke("ConfirmLeave");
            Invoke("ConfirmLeave");
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.AreEqual(1, leaves);
            Assert.IsFalse(State("IsMenuOpen"));
            Assert.IsFalse(State("IsConfirmationOpen"));
            Invoke("Show");
            yield return new WaitForSecondsRealtime(0.22f);
            root.SetActive(false);
            root.SetActive(true);
            yield return null;
            Invoke("Show");
            yield return new WaitForSecondsRealtime(0.22f);
            Invoke("RequestLeave");
            yield return new WaitForSecondsRealtime(0.22f);
            Invoke("ReturnToMenu");
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.IsTrue(State("IsMenuOpen"));
            Assert.IsFalse(State("IsConfirmationOpen"));
            Assert.AreEqual(1, leaves, "재활성화 후 중복 신호 없음");
        }

        [UnityTest]
        public IEnumerator InputSystemPointerSubmitAndCancelReachPanels()
        {
            int settings = 0;
            int leaves = 0;
            Event("OnSettingsRequested").AddListener(() => settings++);
            Event("OnLeaveConfirmed").AddListener(() => leaves++);
            yield return Click(ButtonWithLabel("설정"));
            Assert.AreEqual(1, settings);
            var menuLeave = root.transform.Find("60_Panels/Menu_C13").GetComponentsInChildren<Button>(true)
                .Single(x => x.GetComponentInChildren<TextMeshProUGUI>().text == "나가기");
            yield return Click(menuLeave);
            yield return new WaitForSecondsRealtime(0.22f);
            Assert.IsTrue(State("IsConfirmationOpen"));
            yield return PressKey(Key.Escape);
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.IsFalse(State("IsConfirmationOpen"));
            Assert.AreEqual(menuLeave.gameObject, EventSystem.current.currentSelectedGameObject);
            yield return PressKey(Key.Enter);
            yield return new WaitForSecondsRealtime(0.22f);
            Assert.AreEqual(ButtonWithLabel("돌아가기").gameObject, EventSystem.current.currentSelectedGameObject);
            yield return PressKey(Key.Enter);
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.IsFalse(State("IsConfirmationOpen"));
            Assert.AreEqual(0, leaves, "안전한 첫 선택의 Submit은 나가기 확정이 아닙니다.");
            yield return Click(menuLeave);
            yield return new WaitForSecondsRealtime(0.22f);
            var dialogLeave = root.transform.Find("70_ConfirmDialogs/LeaveConfirmation").GetComponentsInChildren<Button>(true)
                .Single(x => x.GetComponentInChildren<TextMeshProUGUI>().text == "나가기");
            EventSystem.current.SetSelectedGameObject(dialogLeave.gameObject);
            yield return PressKey(Key.Enter);
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.AreEqual(1, leaves);
            Assert.IsFalse(State("IsMenuOpen"));
        }

        Button ButtonWithLabel(string label)
        {
            return root.GetComponentsInChildren<Button>(true).Single(x => x.GetComponentInChildren<TextMeshProUGUI>().text == label);
        }

        void Invoke(string method) { controller.GetType().GetMethod(method).Invoke(controller, null); }
        bool State(string property) { return (bool)controller.GetType().GetProperty(property).GetValue(controller); }
        UnityEvent Event(string property) { return (UnityEvent)controller.GetType().GetProperty(property).GetValue(controller); }

        IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
        }

        IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }
    }
}
