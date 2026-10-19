using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace CurtainCall.BackstageHud.Tests
{
    // 기존 PlayMode 테스트처럼 Assembly-CSharp의 공개 계약을 리플렉션으로 호출합니다.
    public sealed class BackstageHudPlayTests
    {
        const string ScenePath = "Assets/Scenes/Tests/BackstageHud/BackstageHud.unity";
        const string ViewNamespace = "CurtainCall.UI.BackstageHudV1.";
        readonly List<EventSystem> suspendedEvents = new List<EventSystem>();
        readonly List<Canvas> suspendedCanvases = new List<Canvas>();
        readonly List<GameObject> instances = new List<GameObject>();
        Scene previousScene, testScene;
        MonoBehaviour preview;
        Mouse mouse;
        Keyboard keyboard;
        float previousTimeScale;
        bool sceneLoadRequested;
        object Backstage => Property(preview, "Backstage");
        object Clock => Property(Backstage, "Clock");
        object Funds => Property(Backstage, "Funds");
        object Inventory => Property(Backstage, "Inventory");
        object Gather => Property(Backstage, "Gather");
        object Lobby => Property(preview, "Lobby");
        object Standalone => Property(preview, "Standalone");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            previousScene = SceneManager.GetActiveScene();
            Assert.IsFalse(SceneManager.GetSceneByPath(ScenePath).isLoaded, "기존 확인 씬은 먼저 닫아 주세요.");
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (system.enabled) { suspendedEvents.Add(system); system.enabled = false; }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled) { suspendedCanvases.Add(canvas); canvas.enabled = false; }
            mouse = InputSystem.AddDevice<Mouse>("BackstageHudVirtualMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("BackstageHudVirtualKeyboard");
#if UNITY_EDITOR
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), "대기실 HUD 확인 씬 누락");
            sceneLoadRequested = true;
            testScene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(ScenePath));
            sceneLoadRequested = true;
            testScene = SceneManager.LoadScene(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            // PlayMode 씬 로드 완료는 다음 프레임에서 확정됩니다.
            yield return null;
            testScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.IsTrue(testScene.IsValid() && testScene.isLoaded);
            SceneManager.SetActiveScene(testScene);
            preview = Components<MonoBehaviour>().Single(x => x != null &&
                x.GetType().FullName == "CurtainCall.BackstageHud.BackstageHudPreview");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (var instance in instances) if (instance != null) Object.Destroy(instance);
            instances.Clear();
            if (sceneLoadRequested && !testScene.IsValid()) testScene = SceneManager.GetSceneByPath(ScenePath);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (sceneLoadRequested && testScene.IsValid() && testScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(testScene);
            foreach (var system in suspendedEvents) if (system != null) system.enabled = true;
            foreach (var canvas in suspendedCanvases) if (canvas != null) canvas.enabled = true;
            suspendedEvents.Clear(); suspendedCanvases.Clear();
            Time.timeScale = previousTimeScale;
            sceneLoadRequested = false; preview = null; testScene = default;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneBindingsAndThreePrefabsStartWithoutRuntimeSampleData()
        {
            foreach (var root in testScene.GetRootGameObjects()) AssertNoMissingScripts(root);
            foreach (object view in new[] { Backstage, Clock, Funds, Inventory, Gather, Lobby, Standalone })
                Assert.IsTrue(view.GetType().FullName.StartsWith(ViewNamespace, StringComparison.Ordinal));
            foreach (var text in Components<TMP_Text>())
            {
                Assert.NotNull(text.font, text.name);
                Assert.IsFalse(text.raycastTarget, text.name);
            }
            Assert.AreEqual(1, Components<EventSystem>().Count(x => x.isActiveAndEnabled));
            Assert.NotNull(Components<EventSystem>().Single(x => x.isActiveAndEnabled).GetComponent<InputSystemUIInputModule>());
            foreach (string name in new[] { "BackstageHud", "LobbyHud", "InventorySlotTemplate" })
            {
                var prefab = Resources.Load<GameObject>("Prefabs/UIs/BackstageHud/" + name);
                Assert.NotNull(prefab, name);
                AssertNoMissingScripts(prefab);
                var clone = Object.Instantiate(prefab);
                instances.Add(clone);
                clone.SetActive(true);
                yield return null;
                var views = clone.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x != null).ToArray();
                if (name == "BackstageHud")
                {
                    object hud = views.Single(x => x.GetType().FullName == ViewNamespace + "BackstageHUDView");
                    object clock = Property(hud, "Clock"), funds = Property(hud, "Funds");
                    Assert.IsFalse(((Component)clock).gameObject.activeSelf);
                    Assert.IsFalse(((Component)funds).gameObject.activeSelf);
                    Assert.AreEqual(false, Property(clock, "HasValidData"));
                    Assert.AreEqual("—", Text(clock, "timeText"));
                    Assert.AreEqual("—", Text(funds, "amountText"));
                    Assert.AreEqual(0f, Group(Property(hud, "Inventory")).alpha);
                    Assert.AreEqual(0f, Group(Property(hud, "Gather")).alpha);
                }
                else
                {
                    object view = views.Single(x => x.GetType().FullName == ViewNamespace +
                        (name == "LobbyHud" ? "LobbyRoomInfoView" : "InventorySlotsView"));
                    Assert.AreEqual(0f, Group(view).alpha);
                    if (name == "LobbyHud")
                    {
                        Assert.AreEqual(false, Property(view, "IsCodeVisible"));
                        Assert.AreEqual("—", Text(view, "codeText"));
                    }
                    else Assert.AreEqual(0, Property(view, "SlotCount"));
                }
            }
        }

        [UnityTest]
        public IEnumerator ClockRoundingInvalidValuesStatesAndNoAutomaticCountdown()
        {
            Call(preview, "ShowSample", 0);
            foreach (double? invalid in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity, -.01d, 5999.01d })
            {
                Assert.AreEqual(false, SetClock(invalid, .5f, 0));
                Assert.AreEqual(false, Property(Clock, "HasValidData"));
                Assert.AreEqual("—", Text(Clock, "timeText"));
                Assert.IsFalse(Image(Clock, "fillRing").enabled);
            }
            foreach (float? invalid in new float?[] { null, float.NaN, float.PositiveInfinity, float.NegativeInfinity, -.01f, 1.01f })
                Assert.AreEqual(false, SetClock(30d, invalid, 0));
            Assert.AreEqual(false, SetClock(30d, .5f, 99));
            double[] values = { 0d, .01d, 59d, 59.01d, 60d, 61.01d, 5999d };
            string[] expected = { "0:00", "0:01", "0:59", "1:00", "1:00", "1:02", "99:59" };
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(true, SetClock(values[i], i % 2, 0));
                Assert.AreEqual(expected[i], Text(Clock, "timeText"));
                Assert.IsTrue(Image(Clock, "fillRing").enabled);
                Assert.AreEqual((float)(i % 2), Image(Clock, "fillRing").fillAmount);
            }
            for (int state = 0; state < 3; state++)
            {
                SetClock(61.01d, .37f, state);
                Assert.AreEqual(new[] { "탐색", "멈춤", "소집" }[state], Text(Clock, "stateText"));
                Assert.AreEqual(state == 2, Image(Clock, "alarmRing").enabled);
            }
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual("1:02", Text(Clock, "timeText"), "표시 뷰가 시간을 자체 감소시키면 안 됩니다.");
            Assert.AreEqual(.37f, Image(Clock, "fillRing").fillAmount);
            Call(Clock, "SetUnavailable");
            Assert.IsFalse(Image(Clock, "alarmRing").enabled);
        }

        [UnityTest]
        public IEnumerator FundsPreserveLongRangeAndExplicitAccent()
        {
            Call(preview, "ShowSample", 0);
            foreach (long value in new[] { 0L, -1L, 1280L, long.MinValue, long.MaxValue })
            {
                Assert.AreEqual(true, Call(Funds, "SetAmount", value));
                Assert.AreEqual(value.ToString("N0", CultureInfo.InvariantCulture), Text(Funds, "amountText"));
                Assert.AreEqual(true, Property(Funds, "HasValidData"));
            }
            Call(preview, "ShowSample", 10);
            Assert.AreEqual(long.MaxValue.ToString("N0", CultureInfo.InvariantCulture), Text(Funds, "amountText"));
            Call(Funds, "SetChangeAccent", EnumValue(Funds, "FundsChangeAccent", 1));
            Assert.IsTrue(Image(Funds, "changeAccent").enabled);
            Assert.AreEqual(new Color32(0x73, 0xC7, 0xA1, 255), (Color32)Image(Funds, "changeAccent").color);
            Call(Funds, "SetChangeAccent", EnumValue(Funds, "FundsChangeAccent", 2));
            Assert.AreEqual(new Color32(0xE0, 0x6C, 0x78, 255), (Color32)Image(Funds, "changeAccent").color);
            Call(Funds, "SetChangeAccent", EnumValue(Funds, "FundsChangeAccent", 0));
            Assert.IsFalse(Image(Funds, "changeAccent").enabled);
            Assert.AreEqual(false, Call(Funds, "SetAmount", new object[] { null }));
            Assert.AreEqual("—", Text(Funds, "amountText"));
            Call(Funds, "SetChangeAccent", EnumValue(Funds, "FundsChangeAccent", 1));
            Assert.IsFalse(Image(Funds, "changeAccent").enabled);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InventoryResizesClearsSelectionEmptySlotsAndContext()
        {
            Call(preview, "ShowSample", 0);
            Call(Inventory, "ResetForRuntime");
            Call(Inventory, "SetContext", true);
            foreach (int count in new[] { 0, 1, 4, 1, 0 })
            {
                Call(Inventory, "SetSlots", Call(preview, "Slots", count));
                Call(Inventory, "ShowInventory");
                Assert.AreEqual(count, Property(Inventory, "SlotCount"));
                Assert.AreEqual(count > 0 ? 1f : 0f, Group(Inventory).alpha);
                Assert.AreEqual(count == 0 ? 64f : count * 64f + (count - 1) * 8f,
                    ((RectTransform)Field(Inventory, "slotsRoot")).rect.width);
                for (int i = 0; i < count; i++)
                {
                    object slot = Call(Inventory, "GetSlot", i);
                    Assert.AreEqual(new Vector2(i * 72f, 0f), ((RectTransform)((Component)slot).transform).anchoredPosition);
                    Assert.AreEqual(i == 1, Image(slot, "selectedAccent").enabled);
                    Assert.AreEqual((i + 1).ToString(), Text(slot, "keyText"));
                }
                yield return null;
            }
            Call(Inventory, "SetSlots", new object[] { null });
            Assert.AreEqual(0, Property(Inventory, "SlotCount"));
            Assert.AreEqual(0f, Group(Inventory).alpha);
            Assert.IsNull(Call(Inventory, "GetSlot", -1));
            Assert.IsNull(Call(Inventory, "GetSlot", 0));
            Call(preview, "ShowSample", 4);
            Assert.AreEqual(6, Property(Inventory, "SlotCount"));
            object empty = Call(Inventory, "GetSlot", 4);
            Assert.IsFalse(Image(empty, "itemIcon").enabled);
            Assert.IsNull(Image(empty, "itemIcon").sprite);
            Assert.AreEqual(string.Empty, Text(empty, "quantityText"));
            Assert.AreEqual("5", Text(empty, "keyText"));
            Call(Inventory, "SetContext", false);
            Assert.AreEqual(0f, Group(Inventory).alpha);
            Call(Inventory, "SetContext", true);
            Assert.AreEqual(0f, Group(Inventory).alpha, "컨텍스트 복귀만으로 표시 요청을 복구하지 않습니다.");
            Call(Inventory, "ShowInventory");
            Assert.AreEqual(1f, Group(Inventory).alpha);
            Call(Inventory, "HideInventory");
            Assert.AreEqual(0f, Group(Inventory).alpha);
            Assert.IsFalse(Group(Inventory).blocksRaycasts);
            Assert.IsFalse(Group(Inventory).interactable);
        }

        [UnityTest]
        public IEnumerator GatherAcceptsZeroAndOverTargetButRejectsInvalidCountsAndHonorsContext()
        {
            Call(preview, "ShowSample", 0);
            Assert.AreEqual(true, Call(Gather, "SetCounts", 0, 0));
            Assert.AreEqual("0 / 0", Text(Gather, "countText"));
            Assert.AreEqual(true, Call(Gather, "SetCounts", 9, 4));
            Assert.AreEqual("9 / 4", Text(Gather, "countText"));
            foreach (object[] pair in new[] { new object[] { null, 4 }, new object[] { 3, null },
                new object[] { -1, 4 }, new object[] { 3, -1 } })
            {
                Assert.AreEqual(false, Call(Gather, "SetCounts", pair));
                Assert.AreEqual("— / —", Text(Gather, "countText"));
            }
            foreach (bool area in new[] { false, true })
                foreach (bool available in new[] { false, true })
                {
                    Call(Gather, "SetContext", area, available);
                    Assert.AreEqual(area && available ? 1f : 0f, Group(Gather).alpha);
                }
            Assert.IsFalse(Group(Gather).blocksRaycasts);
            Assert.IsFalse(Group(Gather).interactable);
            Call(Gather, "ResetForRuntime");
            Assert.AreEqual(0f, Group(Gather).alpha);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyMasksCodePreservesSameCodeHidesChangedCodeAndGatesRequests()
        {
            Call(preview, "ShowSample", 6);
            Assert.AreEqual("••••••", Text(Lobby, "codeText"));
            Assert.AreEqual(false, Property(Lobby, "IsCodeVisible"));
            Call(Lobby, "SetCodeVisible", true);
            Assert.AreEqual("DEMO42", Text(Lobby, "codeText"));
            Call(Lobby, "SetRoomInfo", "DEMO42", "단장", 0, 0);
            Assert.AreEqual(true, Property(Lobby, "IsCodeVisible"));
            Assert.AreEqual("0 / 0", Text(Lobby, "memberText"));
            Call(Lobby, "SetRoomInfo", "OTHER", "단장", 9, 4);
            Assert.AreEqual(false, Property(Lobby, "IsCodeVisible"));
            Assert.AreEqual("•••••", Text(Lobby, "codeText"));
            Assert.AreEqual("9 / 4", Text(Lobby, "memberText"));
            Call(Lobby, "SetControlsEnabled", false);
            Call(Lobby, "SetCodeVisible", true);
            Call(Lobby, "RequestCopy");
            Assert.AreEqual(0, Property(preview, "CopyCount"));
            Assert.AreEqual(false, Property(Lobby, "IsCodeVisible"));
            Assert.IsFalse(((Button)Field(Lobby, "copyButton")).interactable);
            Call(Lobby, "SetControlsEnabled", true);
            Call(Lobby, "SetCodeVisible", true);
            ((Component)Lobby).gameObject.SetActive(false);
            ((Component)Lobby).gameObject.SetActive(true);
            Assert.AreEqual(false, Property(Lobby, "IsCodeVisible"), "비활성 전환은 보안을 위해 가립니다.");
            Call(Lobby, "SetContext", false);
            Call(Lobby, "RequestCopy");
            Assert.AreEqual(0, Property(preview, "CopyCount"));
            Call(Lobby, "SetContext", true);
            Call(Lobby, "SetRoomInfo", " ", null, null, -1);
            Assert.AreEqual("—", Text(Lobby, "codeText"));
            Assert.AreEqual("방장 —", Text(Lobby, "hostText"));
            Assert.AreEqual("— / —", Text(Lobby, "memberText"));
            Call(Lobby, "RequestCopy");
            Assert.AreEqual(0, Property(preview, "CopyCount"));
            Call(preview, "ShowSample", 7);
            Assert.AreEqual("••••••••", Text(Lobby, "codeText"));
            Call(Lobby, "SetCodeVisible", true);
            Assert.AreEqual("DEMO42LONGROOMCODE", Text(Lobby, "codeText"));
            Assert.IsFalse(((TMP_Text)Field(Lobby, "codeText")).richText);
            Assert.AreEqual(TextOverflowModes.Ellipsis, ((TMP_Text)Field(Lobby, "codeText")).overflowMode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ElevenSceneButtonsAndLobbyButtonsReceiveRealMouseAndEnter()
        {
            for (int mode = 0; mode < 2; mode++)
            {
                for (int sample = 0; sample <= 10; sample++)
                {
                    Call(preview, "ShowSample", sample == 9 ? 0 : 9);
                    var button = Components<Button>().Single(x => x.name == "Sample_" + sample && x.transform.parent.name == "Controls");
                    Assert.IsTrue(button.interactable);
                    Assert.IsTrue(button.targetGraphic.raycastTarget);
                    Assert.NotNull(button.GetComponentInParent<GraphicRaycaster>());
                    Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
                    Assert.AreSame(preview, button.onClick.GetPersistentTarget(0));
                    if (mode == 0) yield return Click((RectTransform)button.transform);
                    else yield return Submit(button);
                    Assert.AreEqual(sample == 10 ? 0 : sample, Property(preview, "Sample"), "입력 누락: " + sample + "/" + mode);
                    AssertSample(sample == 10 ? 0 : sample);
                    if (sample == 7)
                    {
                        if (mode == 0) yield return Click((RectTransform)button.transform);
                        else yield return Submit(button);
                        Assert.AreEqual(10, Property(preview, "Sample"));
                        Assert.AreEqual(long.MaxValue.ToString("N0", CultureInfo.InvariantCulture), Text(Funds, "amountText"));
                        if (mode == 0) yield return Click((RectTransform)button.transform);
                        else yield return Submit(button);
                        Assert.AreEqual(7, Property(preview, "Sample"));
                        AssertSample(7);
                    }
                    if (sample == 10) Assert.AreEqual(0, Property(preview, "CopyCount"));
                }
                Call(preview, "ResetPreview");
                Call(preview, "ShowSample", 6);
                var eye = (Button)Field(Lobby, "revealButton");
                var copy = (Button)Field(Lobby, "copyButton");
                // 가림 상태에서도 유효한 코드의 복사 요청은 한 번 전달합니다.
                preview.gameObject.SetActive(false);
                preview.gameObject.SetActive(true);
                if (mode == 0) yield return Click((RectTransform)copy.transform);
                else yield return Submit(copy);
                Assert.AreEqual(1, Property(preview, "CopyCount"));
                Assert.AreEqual("DEMO42", Property(preview, "LastCopiedCode"));
                Assert.AreEqual(false, Property(Lobby, "IsCodeVisible"));
                Call(preview, "ResetPreview");
                Call(preview, "ShowSample", 6);
                if (mode == 0) yield return Click((RectTransform)eye.transform);
                else yield return Submit(eye);
                Assert.AreEqual(true, Property(Lobby, "IsCodeVisible"));
                if (mode == 0) yield return Click((RectTransform)copy.transform);
                else yield return Submit(copy);
                Assert.AreEqual(1, Property(preview, "CopyCount"), "입력 한 번은 복사 요청 한 번이어야 합니다.");
                Assert.AreEqual("DEMO42", Property(preview, "LastCopiedCode"));
                Call(preview, "ResetPreview");
                Assert.IsNull(Property(preview, "LastCopiedCode"));
            }
        }

        void AssertSample(int sample)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in Components<TMP_Text>())
            {
                Assert.IsTrue(text.font.HasCharacters(text.text), text.name + " 글리프 누락");
                text.ForceMeshUpdate(true);
                if (text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text))
                    Assert.Greater(text.textInfo.characterCount, 0, text.name + " 텍스트 전체 숨김");
            }
            bool room = sample >= 6 && sample <= 8;
            Assert.AreEqual(room ? 1f : 0f, Group(Lobby).alpha);
            Assert.AreEqual(sample == 9 ? 1f : 0f, Group(Standalone).alpha);
            if (room)
            {
                Assert.AreEqual(false, Property(Lobby, "IsCodeVisible"));
                Assert.AreEqual(sample != 8, ((Button)Field(Lobby, "copyButton")).interactable);
                Assert.AreEqual(sample == 7 ? "••••••••" : "••••••", Text(Lobby, "codeText"));
            }
            else if (sample == 9) Assert.AreEqual(1, Property(Standalone, "SlotCount"));
            else
            {
                Assert.AreEqual(sample == 3 ? "—" : sample == 2 ? "0:09" : "2:14", Text(Clock, "timeText"));
                Assert.AreEqual(sample == 3 ? "—" : sample == 1 ? "멈춤" : sample == 2 ? "소집" : "탐색", Text(Clock, "stateText"));
                Assert.AreEqual(sample == 3 ? "—" : "1,280", Text(Funds, "amountText"));
                Assert.AreEqual(sample == 4 ? 6 : sample == 3 ? 0 : 4, Property(Inventory, "SlotCount"));
                Assert.AreEqual(sample == 5 ? 0f : 1f, Group(Gather).alpha);
            }
        }

        object SetClock(double? seconds, float? fraction, int state) =>
            Call(Clock, "SetClock", seconds, fraction, EnumValue(Clock, "BackstageClockState", state));
        static object EnumValue(object view, string name, int value) =>
            Enum.ToObject(view.GetType().Assembly.GetType(ViewNamespace + name, true), value);
        static string Text(object view, string field) => ((TMP_Text)Field(view, field)).text;
        static Image Image(object view, string field) => (Image)Field(view, field);
        static CanvasGroup Group(object view) => (CanvasGroup)Field(view, "visibility");
        IEnumerable<T> Components<T>() where T : Component => testScene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true));
        static void AssertNoMissingScripts(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>()) Assert.NotNull(component, transform.name);
        }
        static object Property(object target, string name)
        {
            Assert.NotNull(target);
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property, target.GetType().Name + "." + name);
            return property.GetValue(target);
        }
        static object Field(object target, string name)
        {
            Assert.NotNull(target);
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, target.GetType().Name + "." + name);
            return field.GetValue(target);
        }
        static object Call(object target, string name, params object[] args)
        {
            Assert.NotNull(target);
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == name && !x.ContainsGenericParameters && x.GetParameters().Length == args.Length)
                .Where(x => x.GetParameters().Select((p, i) => Matches(p.ParameterType, args[i])).All(x => x)).ToArray();
            Assert.AreEqual(1, methods.Length, target.GetType().Name + "." + name + " 서명 일치");
            return methods[0].Invoke(target, args);
        }
        static bool Matches(Type type, object value)
        {
            Type underlying = Nullable.GetUnderlyingType(type);
            return value == null ? !type.IsValueType || underlying != null :
                type.IsInstanceOfType(value) || underlying == value.GetType();
        }
        IEnumerator Click(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            Assert.That(position.x, Is.InRange(0f, (float)Screen.width));
            Assert.That(position.y, Is.InRange(0f, (float)Screen.height));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
        }
        IEnumerator Submit(Button button)
        {
            Components<EventSystem>().Single(x => x.isActiveAndEnabled).SetSelectedGameObject(button.gameObject);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }
    }
}
