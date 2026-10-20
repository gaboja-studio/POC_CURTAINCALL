using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CurtainCall.CommonButtons.Tests
{
    public sealed class CommonButtonsPlayTests
    {
        [UnityTest]
        public IEnumerator InputSystemPointerAndKeyboardReachEachPrefab()
        {
            var canvasRoot = new GameObject("InputCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var eventRoot = new GameObject("InputEvents", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var module = eventRoot.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                foreach (string role in new[] { "Confirm", "Cancel", "Settings", "General" })
                {
                    var root = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_" + role), canvasRoot.transform);
                    var button = root.GetComponent<UnityEngine.UI.Button>();
                    var rect = (RectTransform)root.transform;
                    rect.anchoredPosition = Vector2.zero;
                    int clicks = 0;
                    button.onClick.AddListener(() => clicks++);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var position = RectTransformUtility.WorldToScreenPoint(null, rect.position);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position });
                    yield return null;
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position, buttons = 1 });
                    yield return null;
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position });
                    yield return null;
                    yield return null;
                    Assert.AreEqual(1, clicks, role + " 마우스 클릭");
                    EventSystem.current.SetSelectedGameObject(root);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter));
                    yield return null;
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    yield return null;
                    Assert.AreEqual(2, clicks, role + " Enter");
                    button.interactable = false;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position, buttons = 1 });
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position });
                    yield return null;
                    yield return null;
                    Assert.AreEqual(2, clicks, role + " 비활성");
                    Object.Destroy(root);
                    yield return null;
                }
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                Object.Destroy(canvasRoot);
                Object.Destroy(eventRoot);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourPrefabsGeometryAndAllVisualStates()
        {
            var host = new GameObject("ButtonsTestCanvas", typeof(RectTransform), typeof(Canvas));
            var systemObject = new GameObject("ButtonsTestEventSystem", typeof(EventSystem));
            var system = systemObject.GetComponent<EventSystem>();
            foreach (string role in new[] { "Confirm", "Cancel", "Settings", "General" })
            {
                var prefab = Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_" + role);
                Assert.NotNull(prefab, role);
                var root = Object.Instantiate(prefab, host.transform);
                var button = root.GetComponent<UnityEngine.UI.Button>();
                var body = root.transform.Find("10_Body").GetComponent<UnityEngine.UI.Image>();
                var focus = root.transform.Find("30_Focus").GetComponent<UnityEngine.UI.Image>();
                var content = (RectTransform)root.transform.Find("20_Content");
                var label = content.GetComponentInChildren<TextMeshProUGUI>();
                Assert.AreEqual(new Vector2(192, 56), ((RectTransform)root.transform).sizeDelta);
                Assert.AreEqual("CurtainCall.UI.ButtonsV1.CurtainCallButton", button.GetType().FullName);
                Assert.AreEqual(UnityEngine.UI.Selectable.Transition.None, button.transition);
                Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, body.type);
                Assert.AreEqual(new Vector4(32, 24, 32, 24), body.sprite.border);
                Assert.AreEqual(200, body.sprite.pixelsPerUnit);
                Assert.IsTrue(body.raycastTarget);
                Assert.IsFalse(label.raycastTarget);
                Assert.AreEqual(28, label.fontSize);
                Assert.IsTrue(label.font.HasCharacters("확인취소설정일반…"));
                int clicks = 0;
                button.onClick.AddListener(() => clicks++);
                var pointer = new PointerEventData(system) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerExitHandler);
                Assert.That(body.sprite.name, Does.Contain("_normal"));
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerEnterHandler);
                Assert.That(body.sprite.name, Does.Contain("_hover"));
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That(body.sprite.name, Does.Contain("_pressed"));
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.AreEqual(-1, content.anchoredPosition.y, 0.01f);
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerClickHandler);
                Assert.AreEqual(1, clicks);
                system.SetSelectedGameObject(root);
                ExecuteEvents.Execute(root, new BaseEventData(system), ExecuteEvents.submitHandler);
                Assert.AreEqual(2, clicks);
                yield return new WaitForSecondsRealtime(0.22f);
                Assert.AreEqual(1, focus.color.a, 0.01f);
                button.interactable = false;
                Assert.That(body.sprite.name, Does.Contain("_disabled"));
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(root, new BaseEventData(system), ExecuteEvents.submitHandler);
                Assert.AreEqual(2, clicks);
                Assert.AreEqual(1, label.color.a);
                Assert.AreEqual(0.92f, body.color.a, 0.001f);
                button.interactable = true;
                root.SetActive(false);
                root.SetActive(true);
                Assert.That(body.sprite.name, Does.Contain("_normal"));
                Object.Destroy(root);
                yield return null;
            }
            Object.Destroy(host);
            Object.Destroy(systemObject);
            yield return null;
        }
    }
}
