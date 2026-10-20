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

namespace CurtainCall.CommonIcons.Tests
{
    public sealed class CommonIconsPlayTests
    {
        const string ScenePath = "Assets/Scenes/Tests/CommonIcons/CommonIcons.unity";
        const string ResourceRoot = "Prefabs/UIs/CommonIcons/";
        const string IconSprites = "Assets/_ThirdParty/curtaincall_icons_v1/Assets/CurtainCallUI/CommonIcons/Sprites/";
        const string ButtonSprites = "Assets/_ThirdParty/curtaincall_buttons_v1/Assets/CurtainCallUI/CommonButtons/Sprites/";
        const string PanelSprites = "Assets/_ThirdParty/curtaincall_panels_v1/Assets/CurtainCallUI/BasicPanels/Sprites/";
        static readonly string[] Keys = {
            "currency", "clock", "fire", "person", "eye_open", "eye_closed", "copy", "settings", "close", "warning",
            "megaphone", "delivery", "lock", "skull", "flag", "star", "thumb_down", "phone", "schedule", "medical",
            "tools", "bread", "water", "bandage", "backpack"
        };
        static readonly Color32[] Tints = {
            new Color32(0xF0, 0xEE, 0xDC, 255), new Color32(0x73, 0xC7, 0xA1, 255),
            new Color32(0xE0, 0x6C, 0x78, 255), new Color32(0xF2, 0xB4, 0x5B, 255),
            new Color32(0xAA, 0xB0, 0xAD, 255), new Color32(0x4C, 0x8D, 0xDB, 255),
            new Color32(0xDE, 0x92, 0x53, 255), new Color32(0xCD, 0xA6, 0xD8, 255),
            new Color32(0xBA, 0xE5, 0xDA, 255), new Color32(255, 255, 255, 255)
        };
        readonly List<EventSystem> suspendedEvents = new List<EventSystem>();
        readonly List<Canvas> suspendedCanvases = new List<Canvas>();
        Scene previousScene;
        Scene testScene;
        MonoBehaviour preview;
        ScriptableObject catalog;
        Mouse mouse;
        Keyboard keyboard;
        float previousTimeScale;
        bool sceneLoadRequested;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            previousScene = SceneManager.GetActiveScene();
            Assert.IsFalse(SceneManager.GetSceneByPath(ScenePath).isLoaded, "기존에 열린 공개 테스트 씬은 테스트가 정리하지 않습니다. 먼저 닫아 주세요.");
            // 기존 씬은 언로드하지 않고 입력을 가로채는 컴포넌트만 일시 정지합니다.
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (system.enabled) { suspendedEvents.Add(system); system.enabled = false; }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled) { suspendedCanvases.Add(canvas); canvas.enabled = false; }
            mouse = InputSystem.AddDevice<Mouse>("CommonIconsVirtualMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("CommonIconsVirtualKeyboard");
#if UNITY_EDITOR
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), "공개 아이콘 테스트 씬 누락");
            sceneLoadRequested = true;
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(ScenePath), "빌드에 공개 아이콘 테스트 씬이 필요합니다.");
            sceneLoadRequested = true;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
#endif
            Assert.NotNull(load);
            yield return load;
            testScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.IsTrue(testScene.IsValid() && testScene.isLoaded);
            SceneManager.SetActiveScene(testScene);
            preview = Components<MonoBehaviour>().Single(x => x != null && x.GetType().FullName == "CurtainCall.CommonIcons.CommonIconsPreview");
            catalog = Resources.Load<ScriptableObject>(ResourceRoot + "CommonIconCatalog");
            Assert.NotNull(catalog, "CommonIconCatalog.asset 누락");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 실패한 테스트도 가상 장치와 추가 로드한 씬만 정리하고 기존 상태를 복구합니다.
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (sceneLoadRequested && !testScene.IsValid()) testScene = SceneManager.GetSceneByPath(ScenePath);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (sceneLoadRequested && testScene.IsValid() && testScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(testScene);
            sceneLoadRequested = false;
            foreach (var system in suspendedEvents) if (system != null) system.enabled = true;
            foreach (var canvas in suspendedCanvases) if (canvas != null) canvas.enabled = true;
            suspendedEvents.Clear();
            suspendedCanvases.Clear();
            Time.timeScale = previousTimeScale;
            preview = null;
            catalog = null;
            testScene = default;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PublicPrefabCatalogAndSceneHaveCompleteBindings()
        {
            var prefab = Resources.Load<GameObject>(ResourceRoot + "CommonIcons");
            Assert.NotNull(prefab, "CommonIcons.prefab 누락");
            AssertNoMissingScripts(prefab);
            foreach (var root in testScene.GetRootGameObjects()) AssertNoMissingScripts(root);
            // 공개 프리팹은 비교 보드만 보관하고 Preview와 Catalog 연결은 씬 Controls가 담당합니다.
            var catalogSprites = new HashSet<Sprite>(Keys.Select(CatalogSprite));
            Image[] prefabSamples = prefab.GetComponentsInChildren<Image>(true)
                .Where(x => x.sprite != null && catalogSprites.Contains(x.sprite)).ToArray();
            Assert.AreEqual(75, prefabSamples.Length, "프리팹의 비교 아이콘 Image 75개");
            Assert.AreEqual(75, prefabSamples.Distinct().Count());
            foreach (string key in Keys)
                Assert.AreEqual(3, prefabSamples.Count(x => x.sprite == CatalogSprite(key)), key + " 프리팹 비교 Image는 정확히 3개");
            foreach (var image in prefabSamples) AssertSample(image, 32, Tints[0]);
            Assert.AreSame(catalog, Property(preview, "Catalog"));
            Assert.AreEqual(25, Property(catalog, "Count"));
            Assert.AreEqual(75, Property(preview, "SampleCount"));
            var sprites = new HashSet<Sprite>();
            for (int i = 0; i < Keys.Length; i++)
            {
                object entry = Call(catalog, "GetEntry", i);
                Assert.AreEqual(Keys[i], Field(entry, "key"));
                Assert.IsNotEmpty((string)Field(entry, "label"));
                Sprite sprite = (Sprite)Field(entry, "sprite");
                Assert.NotNull(sprite, Keys[i]);
                Assert.IsTrue(sprites.Add(sprite), "종별 Sprite 중복: " + Keys[i]);
                Assert.AreSame(sprite, CatalogSprite(Keys[i]));
                Assert.AreEqual(new Vector2(64, 64), sprite.rect.size, Keys[i]);
                Assert.AreEqual(64, sprite.texture.width, Keys[i]);
                Assert.AreEqual(64, sprite.texture.height, Keys[i]);
                Assert.AreEqual(200, sprite.pixelsPerUnit, Keys[i]);
                Assert.AreEqual(new Vector2(32, 32), sprite.pivot, Keys[i]);
                Assert.AreEqual(Vector4.zero, sprite.border, Keys[i]);
#if UNITY_EDITOR
                string folder = Keys[i] == "warning" ? PanelSprites : Keys[i] == "settings" || Keys[i] == "close" ? ButtonSprites : IconSprites;
                Assert.AreEqual(folder + "ui_icon_" + Keys[i] + "_normal@2x.png", AssetDatabase.GetAssetPath(sprite), Keys[i]);
#endif
                Assert.AreEqual(3, Samples().Count(x => x.sprite == sprite), Keys[i] + " 비교 Image는 정확히 3개");
            }
            Assert.AreEqual(75, Samples().Distinct().Count());
            foreach (var image in Samples()) AssertSample(image, 32, Tints[0]);
            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true)
                .Concat(preview.GetComponentsInChildren<TMP_Text>(true)))
            {
                Assert.NotNull(text.font, text.name);
                Assert.IsTrue(text.font.HasCharacters(text.text), text.name + " 글리프 누락");
                Assert.IsFalse(text.raycastTarget, text.name);
            }
            Assert.IsNotEmpty(preview.GetComponentsInChildren<TMP_Text>(true), "보드·아이콘 라벨 누락");
            var systems = Components<EventSystem>().Where(x => x.isActiveAndEnabled).ToArray();
            Assert.AreEqual(1, systems.Length, "씬의 활성 EventSystem은 하나여야 합니다.");
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled));
            Assert.NotNull(systems[0].GetComponent<InputSystemUIInputModule>());
            var buttons = Components<Button>().ToArray();
            CollectionAssert.AreEquivalent(new[] { "NextTint", "NextSize", "ResetPreview", "SampleButton" }, buttons.Select(x => x.name));
            foreach (var button in buttons)
            {
                var expectedType = button.name == "SampleButton"
                    ? Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_Settings").GetComponent<Button>().GetType()
                    : typeof(Button);
                Assert.AreEqual(expectedType, button.GetType(), button.name);
                Assert.AreEqual(1, button.onClick.GetPersistentEventCount(), button.name);
                Assert.AreSame(preview, button.onClick.GetPersistentTarget(0), button.name);
                Assert.AreEqual(button.name == "SampleButton" ? "CountSampleClick" : button.name,
                    button.onClick.GetPersistentMethodName(0), button.name);
                Assert.AreNotSame(Samples()[0].GetComponentInParent<Canvas>().rootCanvas,
                    button.GetComponentInParent<Canvas>().rootCanvas, "조작부는 비교 보드와 별도인 Controls Canvas");
                Assert.NotNull(button.GetComponentInParent<GraphicRaycaster>(), button.name);
            }
            Image buttonIcon = SampleButtonIcon();
            Assert.AreEqual(Image.Type.Simple, buttonIcon.type);
            Assert.IsTrue(buttonIcon.preserveAspect);
            Assert.IsFalse(buttonIcon.raycastTarget, "backpack 장식은 부모 버튼 입력을 가로채지 않아야 합니다.");
            Assert.AreSame(CatalogSprite("backpack"), buttonIcon.sprite);
            var settingsPrefab = Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_Settings");
            Assert.NotNull(settingsPrefab);
            Assert.AreNotSame(settingsPrefab, ButtonNamed("SampleButton").gameObject);
            Assert.AreEqual(1, settingsPrefab.GetComponentsInChildren<Image>(true).Count(x => x.sprite == CatalogSprite("settings")),
                "별도 SampleButton 인스턴스의 override가 Settings 정본을 바꾸면 안 됩니다.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TenTintsFourSizesAndResetPreserveAllSpritesAndCenters()
        {
            Image[] samples = Samples();
            Sprite[] originals = samples.Select(x => x.sprite).ToArray();
            Vector2[] centers = samples.Select(x => (Vector2)x.rectTransform.TransformPoint(x.rectTransform.rect.center)).ToArray();
            for (int step = 1; step <= 10; step++)
            {
                Call(preview, "NextTint");
                Assert.AreEqual(step % 10, Property(preview, "TintIndex"));
                for (int i = 0; i < samples.Length; i++)
                {
                    AssertSample(samples[i], 32, Tints[step % 10]);
                    Assert.AreSame(originals[i], samples[i].sprite);
                }
            }
            foreach (int size in new[] { 48, 16, 24, 32 })
            {
                Call(preview, "NextSize");
                Assert.AreEqual(size, Property(preview, "DisplaySize"));
                for (int i = 0; i < samples.Length; i++)
                {
                    AssertSample(samples[i], size, Tints[0]);
                    Assert.AreSame(originals[i], samples[i].sprite);
                    Vector2 center = samples[i].rectTransform.TransformPoint(samples[i].rectTransform.rect.center);
                    Assert.That(Vector2.Distance(centers[i], center), Is.LessThan(0.01f), samples[i].name + " 중심 유지");
                }
            }
            Call(preview, "NextTint");
            Call(preview, "NextSize");
            Call(preview, "CountSampleClick");
            Assert.AreEqual(1, Property(preview, "Clicks"));
            Call(preview, "ResetPreview");
            Assert.AreEqual(0, Property(preview, "TintIndex"));
            Assert.AreEqual(32, Property(preview, "DisplaySize"));
            Assert.AreEqual(0, Property(preview, "Clicks"));
            for (int i = 0; i < samples.Length; i++)
            {
                AssertSample(samples[i], 32, Tints[0]);
                Assert.AreSame(originals[i], samples[i].sprite);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator VirtualPointerAndEnterReachControlsThroughBackpackIcon()
        {
            Assert.AreEqual(0, Property(preview, "TintIndex"));
            yield return Click(ButtonNamed("NextTint").transform as RectTransform);
            Assert.AreEqual(1, Property(preview, "TintIndex"));
            yield return Click(ButtonNamed("NextSize").transform as RectTransform);
            Assert.AreEqual(48, Property(preview, "DisplaySize"));
            yield return Click(SampleButtonIcon().rectTransform);
            Assert.AreEqual(1, Property(preview, "Clicks"), "장식 아이콘 위 클릭도 부모 버튼에 전달");
            yield return Click(ButtonNamed("ResetPreview").transform as RectTransform);
            AssertReset();
            yield return Submit(ButtonNamed("NextTint"));
            Assert.AreEqual(1, Property(preview, "TintIndex"));
            yield return Submit(ButtonNamed("NextSize"));
            Assert.AreEqual(48, Property(preview, "DisplaySize"));
            yield return Submit(ButtonNamed("SampleButton"));
            Assert.AreEqual(1, Property(preview, "Clicks"), "Enter Submit은 한 번만 전달");
            yield return Submit(ButtonNamed("ResetPreview"));
            AssertReset();
        }

        void AssertReset()
        {
            Assert.AreEqual(0, Property(preview, "TintIndex"));
            Assert.AreEqual(32, Property(preview, "DisplaySize"));
            Assert.AreEqual(0, Property(preview, "Clicks"));
            foreach (var image in Samples()) AssertSample(image, 32, Tints[0]);
        }

        static void AssertSample(Image image, int size, Color32 tint, bool compareTint = true)
        {
            Assert.NotNull(image);
            Assert.NotNull(image.sprite, image.name);
            Assert.AreEqual(Image.Type.Simple, image.type, image.name);
            Assert.IsTrue(image.preserveAspect, image.name);
            Assert.IsFalse(image.raycastTarget, image.name);
            Assert.IsFalse(image.useSpriteMesh, image.name);
            Assert.AreEqual(Vector2.one * size, image.rectTransform.sizeDelta, image.name);
            if (compareTint) Assert.AreEqual(tint, (Color32)image.color, image.name);
        }

        static void AssertNoMissingScripts(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>())
                    Assert.IsNotNull(component, root.name + "/" + transform.name + " Missing Script");
        }

        IEnumerable<T> Components<T>() where T : Component => testScene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true));
        Image[] Samples() => Enumerable.Range(0, (int)Property(preview, "SampleCount")).Select(i => (Image)Call(preview, "GetSample", i)).ToArray();
        Sprite CatalogSprite(string key) => (Sprite)Call(catalog, "GetSprite", key);
        Button ButtonNamed(string name) => Components<Button>().Single(x => x.name == name);
        Image SampleButtonIcon() => ButtonNamed("SampleButton").GetComponentsInChildren<Image>(true).Single(x => x.sprite == CatalogSprite("backpack"));

        static object Property(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property, target.GetType().Name + "." + name);
            return property.GetValue(target);
        }

        static object Field(object target, string name)
        {
            Assert.NotNull(target);
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(field, target.GetType().Name + "." + name);
            return field.GetValue(target);
        }

        static object Call(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method, target.GetType().Name + "." + name);
            return method.Invoke(target, args);
        }

        IEnumerator Click(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            Assert.That(position.x, Is.InRange(0f, (float)Screen.width), rect.name);
            Assert.That(position.y, Is.InRange(0f, (float)Screen.height), rect.name);
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

        IEnumerator Submit(Button button)
        {
            var system = Components<EventSystem>().Single(x => x.isActiveAndEnabled);
            system.SetSelectedGameObject(button.gameObject);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }
    }
}
