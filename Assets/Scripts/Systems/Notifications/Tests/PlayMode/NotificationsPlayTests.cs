using System;
using System.Collections;
using System.Collections.Generic;
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

namespace CurtainCall.Notifications.Tests
{
    // Assembly-CSharp를 직접 참조하지 않고 공개 계약을 호출합니다. 비공개 필드는 관찰만 합니다.
    public sealed class NotificationsPlayTests
    {
        const string ScenePath = "Assets/Scenes/Tests/Notifications/Notifications.unity";
        const string ViewNamespace = "CurtainCall.UI.NotificationsV1.";
        readonly List<GameObject> instances = new List<GameObject>();
        readonly List<EventSystem> suspendedEvents = new List<EventSystem>();
        readonly List<Canvas> suspendedCanvases = new List<Canvas>();
        Scene previousScene, testScene;
        MonoBehaviour preview;
        Mouse mouse;
        Keyboard keyboard;
        float previousTimeScale;
        bool loadRequested;
        object Current => Property(preview, "Current");
        object Banner => Property(Current, "Banner");
        object Toast => Property(Current, "Toast");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousScene = SceneManager.GetActiveScene();
            Assert.IsFalse(SceneManager.GetSceneByPath(ScenePath).isLoaded, "이미 열린 확인 씬은 변경하지 않습니다. 먼저 닫아 주세요.");
            Time.timeScale = 1f;
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (system.enabled) { suspendedEvents.Add(system); system.enabled = false; }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled) { suspendedCanvases.Add(canvas); canvas.enabled = false; }
            mouse = InputSystem.AddDevice<Mouse>("NotificationsVirtualMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("NotificationsVirtualKeyboard");
#if UNITY_EDITOR
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), "알림 확인 씬 누락");
            loadRequested = true;
            testScene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(ScenePath));
            loadRequested = true;
            testScene = SceneManager.LoadScene(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            testScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.IsTrue(testScene.IsValid() && testScene.isLoaded);
            SceneManager.SetActiveScene(testScene);
            preview = Components<MonoBehaviour>().Single(x => x != null &&
                x.GetType().FullName == "CurtainCall.Notifications.NotificationsPreview");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 프리뷰 소유 시간을 먼저 반납하고, 실패 지점과 관계없이 원래 시간을 최종 복구합니다.
            if (preview != null) preview.gameObject.SetActive(false);
            Time.timeScale = previousTimeScale;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (var instance in instances) if (instance != null) Object.Destroy(instance);
            instances.Clear();
            if (loadRequested && !testScene.IsValid()) testScene = SceneManager.GetSceneByPath(ScenePath);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (loadRequested && testScene.IsValid() && testScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(testScene);
            foreach (var system in suspendedEvents) if (system != null) system.enabled = true;
            foreach (var canvas in suspendedCanvases) if (canvas != null) canvas.enabled = true;
            suspendedEvents.Clear(); suspendedCanvases.Clear();
            Time.timeScale = previousTimeScale;
            loadRequested = false; preview = null; testScene = default;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreePrefabsPreserveGeometryLayersAndStartHidden()
        {
            Assert.AreEqual(0, Property(preview, "ActionCount"));
            AssertHidden(Banner); AssertHidden(Toast);
            var cheer = Components<MonoBehaviour>().Single(x => x != null && x.GetType().FullName == "CurtainCall.CheerGauge.C1CheerGaugeView");
            Assert.NotNull(cheer.GetComponentInParent<Canvas>(), "문맥용 C1도 Canvas 안에 있어야 합니다.");
            foreach (var text in Components<TMP_Text>())
            {
                Assert.AreEqual(AtlasPopulationMode.Static, text.font.atlasPopulationMode, text.name);
                Assert.IsTrue(text.font.HasCharacters(text.text), text.name + " 문맥 글리프 누락");
            }
            foreach (var root in testScene.GetRootGameObjects()) AssertNoMissingScripts(root);
            foreach (string name in new[] { "SystemNotifications", "SystemBanner", "Toast" })
            {
                var prefab = Resources.Load<GameObject>("Prefabs/UIs/Notifications/" + name);
                Assert.NotNull(prefab, name);
                AssertNoMissingScripts(prefab);
                var clone = Object.Instantiate(prefab);
                instances.Add(clone);
                clone.SetActive(true);
                yield return null;
                var views = clone.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x != null).ToArray();
                object banner = views.SingleOrDefault(x => x.GetType().FullName == ViewNamespace + "SystemBannerView");
                object toast = views.SingleOrDefault(x => x.GetType().FullName == ViewNamespace + "ToastView");
                Assert.AreEqual(name != "Toast", banner != null);
                Assert.AreEqual(name != "SystemBanner", toast != null);
                if (banner != null) AssertGeometry(banner, new Vector2(640f, 104f), new Vector2(0f, -244f), new Vector2(0f, 8f), .180f, .160f);
                if (toast != null) AssertGeometry(toast, new Vector2(384f, 56f), new Vector2(-48f, -160f), new Vector2(16f, 0f), .160f, .140f);
                foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true))
                {
                    Assert.NotNull(text.font); Assert.IsFalse(text.raycastTarget);
                    Assert.IsFalse(text.richText); Assert.IsFalse(text.parseCtrlCharacters);
                    Assert.AreEqual(TextOverflowModes.Ellipsis, text.overflowMode);
                }
                // 세 프리팹은 차례로 하나씩만 활성화합니다.
                clone.SetActive(false);
                Object.Destroy(clone);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator StatesIconsColorsAndPreparedFontGlyphsMatchSamples()
        {
            for (int sample = 0; sample <= 4; sample++)
            {
                Call(preview, "ShowSample", sample, 3f);
                object banner = Banner, toast = Toast;
                string bannerSprite = sample == 1 ? "skullSprite" : sample == 2 ? "warningSprite" : "megaphoneSprite";
                string toastSprite = sample == 3 ? "starSprite" : sample == 4 ? "thumbDownSprite" : "megaphoneSprite";
                Assert.AreSame(Field(banner, bannerSprite), GetImage(banner, "icon").sprite);
                Assert.AreSame(Field(toast, toastSprite), GetImage(toast, "icon").sprite);
                Assert.AreEqual(sample == 1 || sample == 2 ? ColorOf(0xE06C78) : ColorOf(0xC7D8D9), (Color32)GetImage(banner, "accent").color);
                Assert.AreEqual(ColorOf(sample == 3 ? 0x73C7A1 : sample == 4 ? 0xE06C78 : 0xC7D8D9), (Color32)GetImage(toast, "accent").color);
                yield return null;
                foreach (var text in ((Component)Current).GetComponentsInChildren<TMP_Text>(true))
                {
                    Assert.IsTrue(text.font.HasCharacters(text.text), text.name + " 글리프 누락");
                    text.ForceMeshUpdate(true);
                    if (text.isActiveAndEnabled && text.text.Length > 0) Assert.Greater(text.textInfo.characterCount, 0);
                }
            }
        }

        [UnityTest]
        public IEnumerator ShowRejectsInvalidInputsBeforeReplacementButBlankTextHidesFirst()
        {
            foreach (object view in new[] { Banner, Toast })
            {
                Assert.AreEqual(true, Show(view, "유지", "부제", 0, 3f, 0));
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Assert.AreEqual(false, Show(view, "교체 금지", "부제", 0, invalid, 0));
                    Assert.AreEqual("유지", Label(view).text);
                    Assert.AreEqual(true, Property(view, "HasRuntimePresentation"));
                }
                Assert.AreEqual(false, Show(view, "교체 금지", "", 99, 3f, 0));
                Assert.AreEqual(false, Show(view, "교체 금지", "", 0, 3f, 99));
                Assert.AreEqual("유지", Label(view).text);
                Assert.AreEqual(false, Show(view, "\r\n\t ", "", 99, float.NaN, 99));
                AssertHidden(view); Assert.AreEqual(string.Empty, Label(view).text);
                var component = (MonoBehaviour)view;
                component.enabled = false;
                Assert.AreEqual(false, Show(view, "비활성", "", 0, 3f, 0));
                component.enabled = true;
                AssertHidden(view);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NaturalExpiryUsesUnscaledUpdateAndHideDoesNotRestartExit()
        {
            Time.timeScale = 0f;
            Call(preview, "ShowSample", 0, .22f);
            yield return new WaitForSecondsRealtime(.24f);
            Assert.IsTrue((bool)Property(Banner, "IsVisible"));
            Assert.IsTrue((bool)Property(Toast, "IsVisible"));
            yield return new WaitForSecondsRealtime(.50f);
            AssertHidden(Banner); AssertHidden(Toast);
            Call(preview, "ShowSample", 0, 3f);
            yield return new WaitForSecondsRealtime(.22f);
            Call(Current, "HideAll");
            float deadline = Time.realtimeSinceStartup + .45f;
            while ((bool)Property(Banner, "HasRuntimePresentation") || (bool)Property(Toast, "HasRuntimePresentation"))
            {
                Call(Current, "HideAll");
                Assert.Less(Time.realtimeSinceStartup, deadline, "재 Hide가 퇴장 시간을 초기화하면 안 됩니다.");
                yield return null;
            }
            AssertHidden(Banner); AssertHidden(Toast);
        }

        [UnityTest]
        public IEnumerator UpdateConsumesPhaseBoundariesAndReplacementRestartsOnlyItsOwnSlot()
        {
            yield return null;
            Assert.Greater(Time.unscaledDeltaTime, 0f);
            Call(preview, "ShowSample", 0, .000001f);
            object bannerMotion = Field(Banner, "motion"), toastMotion = Field(Toast, "motion");
            // 필드를 조작하지 않고 실제 Update를 동일 프레임의 비축척 델타로 반복 실행합니다.
            int steps = Mathf.CeilToInt(.6f / Time.unscaledDeltaTime) + 4;
            for (int i = 0; i < steps; i++) { Tick(bannerMotion); Tick(toastMotion); }
            AssertHidden(Banner); AssertHidden(Toast);
            Assert.AreEqual(true, Show(Banner, "첫 배너", "부제", 0, 3f, 0));
            Assert.AreEqual(true, Show(Toast, "독립 토스트", "", 0, 3f, 0));
            Tick(bannerMotion); Tick(toastMotion);
            float toastAlpha = Group(Toast).alpha;
            Assert.AreEqual(true, Show(Banner, "교체 배너", "", 1, 3f, 0));
            Assert.AreEqual(0f, Group(Banner).alpha);
            Assert.AreEqual(new Vector2(0f, 8f), Visual(Banner).anchoredPosition);
            Assert.AreEqual(toastAlpha, Group(Toast).alpha);
            Assert.AreEqual("독립 토스트", Label(Toast).text);
            Tick(bannerMotion);
            Assert.Greater(Group(Banner).alpha, 0f);
            Assert.AreEqual(true, Show(Banner, "교체 배너", "", 1, 3f, 0));
            Assert.AreEqual(0f, Group(Banner).alpha);
            Call(Banner, "HideImmediate");
            AssertHidden(Banner);
            Assert.AreEqual(true, Property(Toast, "HasRuntimePresentation"));
        }

        [UnityTest]
        public IEnumerator ControlTextNoneIconsAndEmptySubtitleKeepTheirContracts()
        {
            string controls = "가\r\n나\r다\n라\t마\v바\f사\u0085아" + (char)0x2028 + "자" + (char)0x2029 + "차";
            const string expected = "가 나 다 라 마 바 사 아 자 차";
            Assert.AreEqual(true, Show(Banner, controls, controls, 0, 3f, 1));
            Assert.AreEqual(true, Show(Toast, controls, "", 0, 3f, 1));
            Assert.AreEqual(expected, Label(Banner).text);
            Assert.AreEqual(expected, ((TMP_Text)Field(Banner, "subtitleLabel")).text);
            Assert.AreEqual(expected, Label(Toast).text);
            Assert.IsFalse(GetImage(Banner, "icon").enabled); Assert.IsNull(GetImage(Banner, "icon").sprite);
            Assert.IsFalse(GetImage(Toast, "icon").enabled); Assert.IsNull(GetImage(Toast, "icon").sprite);
            Show(Banner, "제목만", " \t", 0, 3f, 0);
            Assert.IsFalse(((TMP_Text)Field(Banner, "subtitleLabel")).gameObject.activeSelf);
            Assert.AreEqual(new Vector2(72f, -32f), Label(Banner).rectTransform.anchoredPosition);
            Show(Banner, "제목", "부제", 0, 3f, 0);
            Assert.IsTrue(((TMP_Text)Field(Banner, "subtitleLabel")).gameObject.activeSelf);
            Assert.AreEqual(new Vector2(72f, -18f), Label(Banner).rectTransform.anchoredPosition);
            Call(preview, "ShowSample", 8, 3f);
            Assert.AreEqual("<b>알림</b> 다음 줄", Label(Banner).text);
            Assert.IsFalse(Label(Banner).richText);
            Assert.IsFalse(Label(Toast).parseCtrlCharacters);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisableClearsPresentationAndLateAwakePreservesAnEarlierShow()
        {
            Call(preview, "ShowSample", 0, 3f);
            object combined = Current;
            ((Component)combined).gameObject.SetActive(false);
            ((Component)combined).gameObject.SetActive(true);
            AssertHidden(Banner); AssertHidden(Toast);
            Assert.AreEqual(string.Empty, Label(Banner).text);
            Assert.AreEqual(string.Empty, Label(Toast).text);
            Call(preview, "ShowSample", 0, 3f);
            // 다른 Awake에서 먼저 Show한 상황을 동일 초기화 메서드 재진입으로 검사합니다.
            InvokePrivate(Current, "Awake");
            InvokePrivate(Field(Banner, "motion"), "Awake");
            InvokePrivate(Field(Toast, "motion"), "Awake");
            Assert.AreEqual(true, Property(Banner, "HasRuntimePresentation"));
            Assert.AreEqual(true, Property(Toast, "HasRuntimePresentation"));
            yield return null;
            Assert.IsTrue((bool)Property(Banner, "IsVisible"));
            Call(preview, "ShowSample", 9, 3f);
            Assert.NotNull(Banner); Assert.IsNull(Toast);
            Call(preview, "ShowSample", 10, 3f);
            Assert.IsNull(Banner); Assert.NotNull(Toast);
        }

        [UnityTest]
        public IEnumerator AllNineteenButtonsReceiveMouseAndEnterExactlyOnce()
        {
            var system = Components<EventSystem>().Single(x => x.isActiveAndEnabled);
            Assert.NotNull(system.GetComponent<InputSystemUIInputModule>());
            string[] methods = { "Normal", "Danger", "Alarm", "Success", "Failure", "TitleOnly", "NoIcon", "LongText", "ControlText",
                "BannerOnly", "ToastOnly", "Replace", "Restart", "Hide", "HideImmediate", "TogglePause", "ToggleBackground", "ToggleContext", "ResetPreview" };
            for (int mode = 0; mode < 2; mode++)
                for (int index = 0; index < methods.Length; index++)
                {
                    Call(preview, "ResetPreview");
                    Call(preview, "ShowSample", 0, 3f);
                    var button = Components<Button>().Single(x => x.name == "Sample_" + index && x.transform.parent.name == "Controls");
                    Assert.IsTrue(button.interactable); Assert.IsTrue(button.targetGraphic.raycastTarget);
                    Assert.NotNull(button.GetComponentInParent<GraphicRaycaster>());
                    Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
                    Assert.AreSame(preview, button.onClick.GetPersistentTarget(0));
                    Assert.AreEqual(methods[index], button.onClick.GetPersistentMethodName(0));
                    if (mode == 0) yield return Click((RectTransform)button.transform);
                    else yield return Submit(system, button);
                    Assert.AreEqual(index == 18 ? 0 : 1, Property(preview, "ActionCount"), index + "/" + mode);
                    if (index == 18) { AssertHidden(Banner); AssertHidden(Toast); }
                    if (index == 9) { Assert.NotNull(Banner); Assert.IsNull(Toast); }
                    if (index == 10) { Assert.IsNull(Banner); Assert.NotNull(Toast); }
                }
        }

        [UnityTest]
        public IEnumerator PreviewRestoresOwnedTimeScaleOnResetDisableAndDestroy()
        {
            Time.timeScale = .37f;
            Call(preview, "TogglePause"); Assert.AreEqual(0f, Time.timeScale);
            Call(preview, "TogglePause"); Assert.AreEqual(.37f, Time.timeScale);
            Call(preview, "TogglePause"); Call(preview, "ResetPreview"); Assert.AreEqual(.37f, Time.timeScale);
            Call(preview, "TogglePause"); preview.gameObject.SetActive(false); Assert.AreEqual(.37f, Time.timeScale);
            preview.gameObject.SetActive(true);
            Call(preview, "TogglePause"); Object.Destroy(preview.gameObject);
            yield return null;
            Assert.AreEqual(.37f, Time.timeScale);
        }

        static void AssertGeometry(object view, Vector2 size, Vector2 position, Vector2 offset, float enter, float exit)
        {
            AssertHidden(view);
            var root = (RectTransform)((Component)view).transform;
            Assert.AreEqual(size, root.sizeDelta); Assert.AreEqual(position, root.anchoredPosition);
            var motion = Field(view, "motion");
            Assert.AreEqual(offset, Field(motion, "hiddenOffset"));
            Assert.AreEqual(enter, Field(motion, "enterSeconds")); Assert.AreEqual(exit, Field(motion, "exitSeconds"));
            string[] layers = { "00_Shadow", "10_Body", "20_Frame", "30_Corners", "40_Accent", "50_Icon" };
            foreach (string layer in layers)
            {
                var image = Visual(view).Find(layer).GetComponent<Image>();
                Assert.NotNull(image.sprite, layer); Assert.IsFalse(image.raycastTarget);
                Assert.AreEqual(layer == "50_Icon" ? Image.Type.Simple : Image.Type.Sliced, image.type);
            }
            Assert.IsFalse(Group(view).blocksRaycasts); Assert.IsFalse(Group(view).interactable);
        }
        static void AssertHidden(object view)
        {
            Assert.AreEqual(false, Property(view, "HasRuntimePresentation"));
            Assert.AreEqual(false, Property(view, "IsVisible"));
            Assert.AreEqual(0f, Group(view).alpha);
            Assert.IsFalse(Visual(view).gameObject.activeSelf);
        }
        static object Show(object view, string text, string subtitle, int state, float hold, int icon)
        {
            bool banner = view.GetType().Name == "SystemBannerView";
            object stateValue = Enum.ToObject(view.GetType().Assembly.GetType(ViewNamespace + (banner ? "BannerState" : "ToastState"), true), state);
            object iconValue = Enum.ToObject(view.GetType().Assembly.GetType(ViewNamespace + (banner ? "BannerIcon" : "ToastIcon"), true), icon);
            return banner ? Call(view, "Show", text, subtitle, stateValue, hold, iconValue) : Call(view, "Show", text, stateValue, hold, iconValue);
        }
        static TMP_Text Label(object view) => (TMP_Text)Field(view, view.GetType().Name == "SystemBannerView" ? "titleLabel" : "messageLabel");
        static Image GetImage(object view, string name) => (Image)Field(view, name);
        static CanvasGroup Group(object view) => (CanvasGroup)Field(Field(view, "motion"), "visualGroup");
        static RectTransform Visual(object view) => (RectTransform)Field(Field(view, "motion"), "visualRoot");
        static Color32 ColorOf(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        static void Tick(object motion) => InvokePrivate(motion, "Update");
        static void InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, name); method.Invoke(target, null);
        }
        static object Property(object target, string name)
        {
            Assert.NotNull(target);
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property, name); return property.GetValue(target);
        }
        static object Field(object target, string name)
        {
            Assert.NotNull(target);
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(field, name); return field.GetValue(target);
        }
        static object Call(object target, string name, params object[] args)
        {
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == name && !x.ContainsGenericParameters && x.GetParameters().Length == args.Length)
                .Where(x => x.GetParameters().Select((p, i) => args[i] == null ? !p.ParameterType.IsValueType : p.ParameterType.IsInstanceOfType(args[i])).All(x => x)).ToArray();
            Assert.AreEqual(1, methods.Length, target.GetType().Name + "." + name);
            return methods[0].Invoke(target, args);
        }
        IEnumerable<T> Components<T>() where T : Component => testScene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true));
        static void AssertNoMissingScripts(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>()) Assert.NotNull(component, transform.name);
        }
        IEnumerator Click(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
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
        IEnumerator Submit(EventSystem system, Button button)
        {
            system.SetSelectedGameObject(button.gameObject);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }
    }
}
