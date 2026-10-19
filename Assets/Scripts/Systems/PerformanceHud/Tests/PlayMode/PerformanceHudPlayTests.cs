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

namespace CurtainCall.PerformanceHud.Tests
{
    // Runtime은 Assembly-CSharp에 있으므로 실제 공개 API와 직렬화 연결을 reflection으로 검사합니다.
    public sealed class PerformanceHudPlayTests
    {
        const string ScenePath = "Assets/Scenes/Tests/PerformanceHud/PerformanceHud.unity";
        const string HudNamespace = "CurtainCall.UI.PerformanceHudV1.";
        static readonly string[] Actions = { "Normal", "Warning", "Overlap", "Unavailable", "ToggleObserving",
            "ToggleTightrope", "TogglePerformance", "ResetPreview" };
        readonly List<EventSystem> suspendedEvents = new List<EventSystem>();
        readonly List<Canvas> suspendedCanvases = new List<Canvas>();
        readonly List<GameObject> instances = new List<GameObject>();
        Scene previousScene;
        Scene testScene;
        MonoBehaviour preview;
        Mouse mouse;
        Keyboard keyboard;
        float previousTimeScale;
        bool sceneLoadRequested;
        object Hud => Property(preview, "Hud");
        object Timer => Property(Hud, "Timer");
        object Progress => Property(Hud, "Progress");
        object Balance => Property(Hud, "Balance");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            previousScene = SceneManager.GetActiveScene();
            Assert.IsFalse(SceneManager.GetSceneByPath(ScenePath).isLoaded, "기존에 열린 확인 씬은 먼저 닫아 주세요.");
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (system.enabled) { suspendedEvents.Add(system); system.enabled = false; }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled) { suspendedCanvases.Add(canvas); canvas.enabled = false; }
            mouse = InputSystem.AddDevice<Mouse>("PerformanceHudVirtualMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("PerformanceHudVirtualKeyboard");
#if UNITY_EDITOR
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), "공연 HUD 확인 씬 누락");
            sceneLoadRequested = true;
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(ScenePath));
            sceneLoadRequested = true;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
#endif
            Assert.NotNull(load);
            yield return load;
            testScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.IsTrue(testScene.IsValid() && testScene.isLoaded);
            SceneManager.SetActiveScene(testScene);
            preview = Components<MonoBehaviour>().Single(x => x != null &&
                x.GetType().FullName == "CurtainCall.PerformanceHud.PerformanceHudPreview");
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
            suspendedEvents.Clear();
            suspendedCanvases.Clear();
            sceneLoadRequested = false;
            Time.timeScale = previousTimeScale;
            preview = null;
            testScene = default;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BindingsGlyphsRaycastsAndPrefabRuntimeResetAreComplete()
        {
            foreach (var root in testScene.GetRootGameObjects()) AssertNoMissingScripts(root);
            Assert.AreEqual(HudNamespace + "PerformanceHUDView", Hud.GetType().FullName);
            foreach (object view in new[] { Timer, Progress, Balance })
                Assert.IsTrue(view.GetType().FullName.StartsWith(HudNamespace, StringComparison.Ordinal));
            AssertTimerBindings(Timer);
            foreach (string field in new[] { "playerRoots", "playerImages", "playerUnderlays", "playerNumbers" })
            {
                var array = (Array)Field(Progress, field);
                Assert.AreEqual(4, array.Length, field);
                foreach (object entry in array) Assert.NotNull(entry, field);
            }
            foreach (string field in new[] { "selfSprite", "teammateSprite", "progressTrack", "fireMarker", "fireRoot",
                "arrivalMarker", "maximumMarker", "fallTemplate", "remainingText", "overlapText", "warningAccent" })
                Assert.NotNull(Field(Progress, field), field);
            foreach (string field in new[] { "visibility", "needle", "needleUnderlay", "dwellFill" })
                Assert.NotNull(Field(Balance, field), field);
            foreach (string field in new[] { "zoneRects", "hatchMasks", "boundaries", "keyBodies", "keyLabels" })
            {
                var array = (Array)Field(Balance, field);
                Assert.AreEqual(field == "zoneRects" ? 5 : field == "boundaries" ? 4 : 2, array.Length, field);
                foreach (object entry in array) Assert.NotNull(entry, field);
            }
            foreach (var text in Components<TMP_Text>()) AssertText(text);
            foreach (var image in ((Component)Hud).GetComponentsInChildren<Image>(true))
            {
                Assert.NotNull(image.sprite, image.name);
                Assert.IsFalse(image.raycastTarget, image.name);
            }
            foreach (string sample in Actions.Take(4))
            {
                Call(preview, sample);
                foreach (var text in ((Component)Hud).GetComponentsInChildren<TMP_Text>(true)) AssertText(text);
                var remaining = (TMP_Text)Field(Progress, "remainingText");
                remaining.ForceMeshUpdate();
                Assert.Greater(remaining.textInfo.characterCount, 0, "남은 거리 문장이 세로 Ellipsis로 통째로 사라지면 안 됩니다.");
            }
            var systems = Components<EventSystem>().Where(x => x.isActiveAndEnabled).ToArray();
            Assert.AreEqual(1, systems.Length);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled));
            Assert.NotNull(systems[0].GetComponent<InputSystemUIInputModule>());
            var buttons = Components<Button>().ToArray();
            Assert.AreEqual(8, buttons.Length);
            for (int i = 0; i < Actions.Length; i++)
            {
                Button button = ButtonFor(Actions[i]);
                Assert.AreEqual("Sample_" + i, button.name);
                Assert.IsTrue(button.interactable);
                Assert.NotNull(button.targetGraphic);
                Assert.IsTrue(button.targetGraphic.raycastTarget);
                Assert.NotNull(button.GetComponentInParent<GraphicRaycaster>());
                Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
                Assert.AreSame(preview, button.onClick.GetPersistentTarget(0));
            }
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/PerformanceHud/PerformanceHud");
            Assert.NotNull(prefab);
            AssertNoMissingScripts(prefab);
            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true)) AssertText(text);
            var clone = Object.Instantiate(prefab);
            instances.Add(clone);
            clone.SetActive(true);
            yield return null;
            object hud = clone.GetComponents<MonoBehaviour>().Single(x => x.GetType().FullName == HudNamespace + "PerformanceHUDView");
            object timer = Property(hud, "Timer"), progress = Property(hud, "Progress"), balance = Property(hud, "Balance");
            Assert.IsFalse(((Component)timer).gameObject.activeSelf);
            Assert.IsFalse(((Component)progress).gameObject.activeSelf);
            AssertUnavailable(timer);
            AssertUnavailable(progress);
            AssertUnavailable(balance);
            Assert.AreEqual("—", ((TMP_Text)Field(timer, "timeText")).text);
            Assert.AreEqual(0f, ((CanvasGroup)Field(balance, "visibility")).alpha);
        }

        [UnityTest]
        public IEnumerator TimerHandlesUnavailableRoundingThresholdsAndMaximum()
        {
            Call(preview, "Normal");
            foreach (double? invalid in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.01d, 5999.01d })
            {
                Assert.AreEqual(false, Call(Timer, "SetRemainingSeconds", new object[] { invalid }));
                AssertUnavailable(Timer);
                Assert.AreEqual("—", ((TMP_Text)Field(Timer, "timeText")).text);
                Assert.IsFalse(((Image)Field(Timer, "warningAccent")).enabled);
            }
            double[] seconds = { 61.01d, 30.01d, 31d, 30d, 10.01d, 10d, 0d, 5999d };
            string[] labels = { "1:02", "0:31", "0:31", "0:30", "0:11", "0:10", "0:00", "99:59" };
            for (int i = 0; i < seconds.Length; i++)
            {
                Assert.AreEqual(true, Call(Timer, "SetRemainingSeconds", (double?)seconds[i]));
                Assert.AreEqual(true, Property(Timer, "HasValidData"));
                var text = (TMP_Text)Field(Timer, "timeText");
                Assert.AreEqual(labels[i], text.text);
                var color = seconds[i] <= 10 ? new Color32(0xE0, 0x6C, 0x78, 255) : seconds[i] <= 30
                    ? new Color32(0xF2, 0xB4, 0x5B, 255) : new Color32(0xF0, 0xEE, 0xDC, 255);
                Assert.AreEqual(color, (Color32)text.color);
                Assert.AreEqual(color, (Color32)((Image)Field(Timer, "clockIcon")).color);
                Assert.AreEqual(seconds[i] <= 30, ((Image)Field(Timer, "warningAccent")).enabled);
                AssertText(text);
            }
            string last = ((TMP_Text)Field(Timer, "timeText")).text;
            yield return null;
            Assert.AreEqual(last, ((TMP_Text)Field(Timer, "timeText")).text, "표시 전용: 자동 카운트다운하지 않음");
        }

        [UnityTest]
        public IEnumerator ProgressSupportsPlayersOverlapClampingOptionsAndInvalidData()
        {
            Call(preview, "Normal");
            var roots = (RectTransform[])Field(Progress, "playerRoots");
            var images = (Image[])Field(Progress, "playerImages");
            var numbers = (TMP_Text[])Field(Progress, "playerNumbers");
            for (int count = 2; count <= 4; count++)
            {
                float[] distances = Enumerable.Range(0, count).Select(i => i * 40f).ToArray();
                AssertProgress(true, 160f, distances, 0, 36f, 140f, new[] { 54f, 80f, 90f }, true);
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(i < count, roots[i].gameObject.activeSelf);
                    if (i >= count) continue;
                    Assert.That(roots[i].anchoredPosition.x, Is.EqualTo(24f + 360f * distances[i] / 160f).Within(0.01f));
                    Assert.AreEqual(Vector2.one * (i == 0 ? 32 : 24), roots[i].sizeDelta);
                    Assert.AreSame(Field(Progress, i == 0 ? "selfSprite" : "teammateSprite"), images[i].sprite);
                    Assert.AreEqual((i + 1).ToString(), numbers[i].text);
                }
                Assert.AreEqual(3, ((List<Image>)Field(Progress, "fallPool")).Count(x => x.gameObject.activeSelf));
                Assert.IsTrue(((Image)Field(Progress, "warningAccent")).enabled);
            }
            AssertProgress(true, 160f, new[] { 98f, 98f, 98f, 98f }, 0, null, null, null, false);
            Assert.AreEqual("겹침 1·2·3·4", ((TMP_Text)Field(Progress, "overlapText")).text);
            Assert.AreEqual(1, numbers.Count(x => x.enabled));
            Assert.IsTrue(numbers[0].enabled);
            Assert.AreEqual(roots[0].parent.childCount - 1, roots[0].GetSiblingIndex());
            Assert.IsFalse(((RectTransform)Field(Progress, "fireRoot")).gameObject.activeSelf);
            Assert.IsFalse(((Image)Field(Progress, "maximumMarker")).enabled);
            Assert.IsFalse(((Image)Field(Progress, "warningAccent")).enabled);
            Assert.IsTrue(((Image)Field(Progress, "arrivalMarker")).enabled);
            Assert.IsTrue(((List<Image>)Field(Progress, "fallPool")).All(x => !x.gameObject.activeSelf));
            float[] raw = { -5f, 180f };
            AssertProgress(true, 160f, raw, -1, null, null, null, false);
            CollectionAssert.AreEqual(new[] { -5f, 180f }, raw, "표시 clamp는 원본 거리 불변");
            Assert.AreEqual(24f, roots[0].anchoredPosition.x);
            Assert.AreEqual(384f, roots[1].anchoredPosition.x);
            Assert.AreEqual("—", ((TMP_Text)Field(Progress, "remainingText")).text);
            Assert.IsTrue(images.Take(2).All(x => x.sprite == (Sprite)Field(Progress, "teammateSprite")));
            AssertProgress(true, 160f, new[] { 1.1f, 80f }, 0, null, null, null, false);
            Assert.AreEqual("남은 159 m", ((TMP_Text)Field(Progress, "remainingText")).text);
            AssertProgress(false, 0f, raw, 0, null, null, null, false);
            AssertProgress(false, float.NaN, raw, 0, null, null, null, false);
            AssertProgress(false, float.PositiveInfinity, raw, 0, null, null, null, false);
            AssertProgress(false, 160f, null, 0, null, null, null, false);
            AssertProgress(false, 160f, new[] { 1f }, 0, null, null, null, false);
            AssertProgress(false, 160f, new float[5], 0, null, null, null, false);
            AssertProgress(false, 160f, raw, -2, null, null, null, false);
            AssertProgress(false, 160f, raw, 2, null, null, null, false);
            AssertProgress(false, 160f, new[] { float.NaN, 0f }, 0, null, null, null, false);
            AssertProgress(false, 160f, raw, 0, float.PositiveInfinity, null, null, false);
            AssertProgress(false, 160f, raw, 0, null, float.NaN, null, false);
            AssertProgress(false, 160f, raw, 0, null, null, new[] { float.NegativeInfinity }, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BalanceBoundariesInvalidInputContextPriorityAndResetAreSafe()
        {
            Call(preview, "Normal");
            var group = (CanvasGroup)Field(Balance, "visibility");
            var needle = (RectTransform)Field(Balance, "needle");
            var zones = (RectTransform[])Field(Balance, "zoneRects");
            var keys = (TMP_Text[])Field(Balance, "keyLabels");
            foreach (float value in new[] { -1f, -0.55f, 0f, 0.55f, 1f })
            {
                AssertBalance(true, value, -0.55f, 0.55f, 1f, "Left", "Right", true, false);
                Assert.That(needle.anchoredPosition.x + 10f, Is.EqualTo(60f + (value + 1f) * 212f).Within(0.01f));
                Assert.That(zones.Sum(x => x.sizeDelta.x), Is.EqualTo(424f).Within(0.01f));
                Assert.AreEqual(1f, group.alpha);
                Assert.AreEqual(1f, ((Image)Field(Balance, "dwellFill")).fillAmount);
            }
            AssertBalance(true, 0f, -1f, 1f, 0f, null, " ", false, true);
            Assert.AreEqual("—", keys[0].text);
            Assert.AreEqual("—", keys[1].text);
            Assert.IsTrue(((RectTransform[])Field(Balance, "hatchMasks")).All(x => !x.gameObject.activeSelf));
            var bodies = (Image[])Field(Balance, "keyBodies");
            Assert.AreNotEqual(bodies[0].color, bodies[1].color, "누름 상태는 전달된 표시만 반영");
            foreach (float[] invalid in new[] {
                new[] { -1.01f, -0.5f, 0.5f, 0f }, new[] { float.NaN, -0.5f, 0.5f, 0f },
                new[] { 0f, -1.01f, 0.5f, 0f }, new[] { 0f, -0.5f, 1.01f, 0f },
                new[] { 0f, 0.5f, 0.5f, 0f }, new[] { 0f, 0.6f, 0.5f, 0f },
                new[] { 0f, -0.5f, 0.5f, -0.01f }, new[] { 0f, -0.5f, 0.5f, 1.01f },
                new[] { 0f, -0.5f, 0.5f, float.PositiveInfinity } })
            {
                AssertBalance(false, invalid[0], invalid[1], invalid[2], invalid[3], "A", "D", false, false);
                Assert.AreEqual(0f, group.alpha);
                Assert.AreEqual(0f, ((Image)Field(Balance, "dwellFill")).fillAmount);
            }
            Call(preview, "Normal");
            Call(preview, "ToggleObserving");
            Assert.AreEqual(0f, group.alpha);
            Assert.IsTrue(((Component)Timer).gameObject.activeSelf);
            Call(preview, "ToggleTightrope");
            Call(preview, "ToggleTightrope");
            Assert.AreEqual(0f, group.alpha, "관전 우선");
            Call(preview, "ToggleObserving");
            Assert.AreEqual(1f, group.alpha);
            Call(preview, "ToggleTightrope");
            Assert.AreEqual(0f, group.alpha);
            Call(preview, "TogglePerformance");
            Assert.IsFalse(((Component)Timer).gameObject.activeSelf);
            Assert.IsFalse(((Component)Progress).gameObject.activeSelf);
            Call(preview, "ResetPreview");
            AssertNormal();
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);
            Call(Hud, "ResetForRuntime");
            foreach (object view in new[] { Timer, Progress, Balance }) AssertUnavailable(view);
            Assert.IsFalse(((Component)Timer).gameObject.activeSelf);
            Assert.IsFalse(((Component)Progress).gameObject.activeSelf);
            Assert.AreEqual(0f, group.alpha);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EightButtonsReceiveRealMouseClicksAndEnterSubmit()
        {
            foreach (bool submit in new[] { false, true })
            {
                foreach (string action in Actions)
                {
                    // 이벤트 누락이 통과하지 않도록 각 동작의 시작 상태를 확실히 구분합니다.
                    Call(preview, action == "Normal" || action == "ResetPreview" ? "Warning" : "Normal");
                    Button button = ButtonFor(action);
                    if (submit) yield return Submit(button);
                    else yield return Click(button.transform as RectTransform);
                    switch (action)
                    {
                        case "Normal": case "ResetPreview": AssertNormal(); break;
                        case "Warning":
                            Assert.AreEqual(1, Property(preview, "SelectedSample"));
                            Assert.AreEqual("0:09", ((TMP_Text)Field(Timer, "timeText")).text); break;
                        case "Overlap":
                            Assert.AreEqual(2, Property(preview, "SelectedSample"));
                            Assert.AreEqual("겹침 1·2·3·4", ((TMP_Text)Field(Progress, "overlapText")).text); break;
                        case "Unavailable":
                            Assert.AreEqual(3, Property(preview, "SelectedSample"));
                            foreach (object view in new[] { Timer, Progress, Balance }) AssertUnavailable(view); break;
                        case "ToggleObserving":
                            Assert.AreEqual(true, Property(preview, "Observing"));
                            Assert.AreEqual(0f, ((CanvasGroup)Field(Balance, "visibility")).alpha); break;
                        case "ToggleTightrope":
                            Assert.AreEqual(false, Property(preview, "OnTightrope"));
                            Assert.AreEqual(0f, ((CanvasGroup)Field(Balance, "visibility")).alpha); break;
                        case "TogglePerformance":
                            Assert.AreEqual(false, Property(preview, "PerformanceActive"));
                            Assert.IsFalse(((Component)Timer).gameObject.activeSelf);
                            Assert.IsFalse(((Component)Progress).gameObject.activeSelf); break;
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator FiveBalanceZonesCoverTrackWithOrderedBoundariesColorsAndOutsideHatches()
        {
            Call(preview, "Normal");
            // Safe includes its endpoints; warning includes its outside endpoints.
            foreach (float value in new[] { -1f, -0.5001f, -0.5f, -0.4999f, -0.3001f, -0.3f,
                -0.2999f, 0f, 0.2999f, 0.3f, 0.3001f, 0.4999f, 0.5f, 0.5001f, 1f })
            {
                AssertBalanceZones(true, value, -0.3f, 0.3f, -0.5f, 0.5f, 0.37f);
                AssertZoneGeometry(-0.3f, 0.3f, -0.5f, 0.5f);
                Assert.That(((RectTransform)Field(Balance, "needle")).anchoredPosition.x + 10f,
                    Is.EqualTo(60f + (value + 1f) * 212f).Within(0.01f), "경계 전후 바늘 위치: " + value);
            }
            // Asymmetric, zero-width warning, full-safe, and zero-width danger cases.
            foreach (float[] bounds in new[] {
                new[] { -0.2f, 0.45f, -0.8f, 0.65f },
                new[] { -0.3f, 0.3f, -0.3f, 0.3f },
                new[] { -1f, 1f, -1f, 1f },
                new[] { -0.3f, 0.3f, -1f, 1f },
                new[] { -0.3f, 0.3f, -0.3f, 0.5f },
                new[] { -0.3f, 0.3f, -0.5f, 0.3f },
                new[] { -1f, 0.3f, -1f, 0.5f },
                new[] { -0.3f, 1f, -0.5f, 1f } })
            {
                AssertBalanceZones(true, 0f, bounds[0], bounds[1], bounds[2], bounds[3], 0f);
                AssertZoneGeometry(bounds[0], bounds[1], bounds[2], bounds[3]);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BalanceRejectsEveryNonfiniteAndInvalidBoundThenRecoversWithoutAccumulatingRisk()
        {
            Call(preview, "Normal");
            // Argument order: balance, safetyMin, safetyMax, warningMin, warningMax, riskFraction.
            float[] baseline = { 0.18f, -0.3f, 0.3f, -0.5f, 0.5f, 0.37f };
            for (int index = 0; index < baseline.Length; index++)
            {
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    var values = (float[])baseline.Clone();
                    values[index] = invalid;
                    AssertRejectedAndRecovered(values, baseline);
                }
            }
            for (int index = 1; index <= 4; index++)
            {
                foreach (float invalid in new[] { -1.01f, 1.01f })
                {
                    var values = (float[])baseline.Clone();
                    values[index] = invalid;
                    AssertRejectedAndRecovered(values, baseline);
                }
            }
            foreach (float[] invalid in new[] {
                new[] { -1.01f, -0.3f, 0.3f, -0.5f, 0.5f, 0f },
                new[] { 1.01f, -0.3f, 0.3f, -0.5f, 0.5f, 0f },
                new[] { 0f, -1.01f, 0.3f, -0.5f, 0.5f, 0f },
                new[] { 0f, -0.3f, 1.01f, -0.5f, 0.5f, 0f },
                new[] { 0f, -0.3f, 0.3f, -1.01f, 0.5f, 0f },
                new[] { 0f, -0.3f, 0.3f, -0.5f, 1.01f, 0f },
                new[] { 0f, 0.3f, 0.3f, -0.5f, 0.5f, 0f },
                new[] { 0f, 0.4f, 0.3f, -0.5f, 0.5f, 0f },
                new[] { 0f, -0.3f, 0.3f, -0.2f, 0.5f, 0f },
                new[] { 0f, -0.3f, 0.3f, -0.5f, 0.2f, 0f },
                new[] { 0f, -0.3f, 0.3f, 0.5f, -0.5f, 0f },
                new[] { 0f, -0.3f, 0.3f, -0.5f, 0.5f, -0.01f },
                new[] { 0f, -0.3f, 0.3f, -0.5f, 0.5f, 1.01f } })
                AssertRejectedAndRecovered(invalid, baseline);
            foreach (float risk in new[] { 0f, 0.37f, 1f })
            {
                AssertBalanceZones(true, 0.8f, -0.3f, 0.3f, -0.5f, 0.5f, risk);
                AssertZoneGeometry(-0.3f, 0.3f, -0.5f, 0.5f);
                yield return null;
                yield return null;
                Assert.That(((Image)Field(Balance, "dwellFill")).fillAmount, Is.EqualTo(risk).Within(0.0001f),
                    "표시 전용: 위험 구간에서도 전달한 riskFraction을 자동 누적하지 않음");
            }
        }

        [UnityTest]
        public IEnumerator LegacyBalanceOverloadDelegatesToZeroWidthWarningZones()
        {
            Call(preview, "Normal");
            foreach (float value in new[] { -1f, -0.3f, 0f, 0.3f, 1f })
            {
                AssertBalance(true, value, -0.3f, 0.3f, 0.42f, "Left", "Right", true, false);
                AssertZoneGeometry(-0.3f, 0.3f, -0.3f, 0.3f);
                var zones = (RectTransform[])Field(Balance, "zoneRects");
                Assert.IsFalse(zones[1].gameObject.activeSelf);
                Assert.IsFalse(zones[3].gameObject.activeSelf);
                var needle = (RectTransform)Field(Balance, "needle");
                Vector2 previousPosition = needle.anchoredPosition;
                Color[] previousColors = zones.Select(x => x.GetComponent<Image>().color).ToArray();
                AssertBalanceZones(true, value, -0.3f, 0.3f, -0.3f, 0.3f, 0.42f);
                Assert.AreEqual(previousPosition, needle.anchoredPosition);
                CollectionAssert.AreEqual(previousColors, zones.Select(x => x.GetComponent<Image>().color).ToArray());
                Assert.That(((Image)Field(Balance, "dwellFill")).fillAmount, Is.EqualTo(0.42f).Within(0.0001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarningButtonCyclesOrangeDangerOrangeWithMouseAndEnterAndResetsOnSampleEntry()
        {
            foreach (bool submit in new[] { false, true })
            {
                Call(preview, "Normal");
                AssertNormal();
                AssertZoneGeometry(-0.3f, 0.3f, -0.5f, 0.5f);
                Button button = ButtonFor("Warning");
                // Two complete toggles, using the actual UI input path each time.
                foreach (bool danger in new[] { false, true, false, true, false })
                {
                    if (submit) yield return Submit(button);
                    else yield return Click(button.transform as RectTransform);
                    AssertWarningSample(danger);
                }
            }
            foreach (string entry in new[] { "Normal", "Overlap", "Unavailable" })
            {
                Call(preview, "Normal");
                Call(preview, "Warning");
                Call(preview, "Warning");
                AssertWarningSample(true);
                Call(preview, entry);
                Assert.AreEqual(false, Property(preview, "DangerSample"), entry + " 순환 초기화");
                Call(preview, "Warning");
                AssertWarningSample(false);
            }
            Call(preview, "Warning");
            AssertWarningSample(true);
            Call(preview, "ShowSample", 1);
            AssertWarningSample(false);
            Call(preview, "ShowSample", 1);
            AssertWarningSample(false);
            for (int sample = 0; sample < 4; sample++)
            {
                Call(preview, "ShowSample", sample);
                Assert.AreEqual(sample, Property(preview, "SelectedSample"));
                Assert.AreEqual(false, Property(preview, "DangerSample"));
            }
            Call(preview, "ResetPreview");
            AssertNormal();
        }

        void AssertWarningSample(bool danger)
        {
            Assert.AreEqual(1, Property(preview, "SelectedSample"));
            Assert.AreEqual(danger, Property(preview, "DangerSample"));
            float value = danger ? 0.8f : 0.4f;
            Assert.That(((RectTransform)Field(Balance, "needle")).anchoredPosition.x + 10f,
                Is.EqualTo(60f + (value + 1f) * 212f).Within(0.01f));
            string label = danger ? "Danger" : "Warning";
            Assert.That(((TMP_Text)Field(preview, "status")).text, Does.Contain("P1 " + label));
            Assert.AreEqual("경고/위험", ButtonFor("Warning").GetComponentInChildren<TMP_Text>(true).text);
            foreach (var text in ((Component)Hud).GetComponentsInChildren<TMP_Text>(true)) AssertText(text);
        }

        void AssertRejectedAndRecovered(float[] invalid, float[] baseline)
        {
            AssertBalanceZones(false, invalid[0], invalid[1], invalid[2], invalid[3], invalid[4], invalid[5]);
            Assert.AreEqual(0f, ((CanvasGroup)Field(Balance, "visibility")).alpha);
            Assert.AreEqual(0f, ((Image)Field(Balance, "dwellFill")).fillAmount);
            AssertBalanceZones(true, baseline[0], baseline[1], baseline[2], baseline[3], baseline[4], baseline[5]);
            Assert.AreEqual(1f, ((CanvasGroup)Field(Balance, "visibility")).alpha);
            AssertZoneGeometry(baseline[1], baseline[2], baseline[3], baseline[4]);
        }

        void AssertBalanceZones(bool valid, float balance, float safetyMin, float safetyMax,
            float warningMin, float warningMax, float risk)
        {
            Assert.AreEqual(valid, Call(Balance, "SetBalance", balance, safetyMin, safetyMax,
                warningMin, warningMax, risk, "Left", "Right", true, false));
            Assert.AreEqual(valid, Property(Balance, "HasValidData"));
            if (valid)
                Assert.That(((Image)Field(Balance, "dwellFill")).fillAmount, Is.EqualTo(risk).Within(0.0001f));
        }

        void AssertZoneGeometry(float safetyMin, float safetyMax, float warningMin, float warningMax)
        {
            Canvas.ForceUpdateCanvases();
            var zones = (RectTransform[])Field(Balance, "zoneRects");
            var masks = (RectTransform[])Field(Balance, "hatchMasks");
            var boundaries = (RectTransform[])Field(Balance, "boundaries");
            Assert.AreEqual(5, zones.Length);
            Assert.AreEqual(2, masks.Length);
            Assert.AreEqual(4, boundaries.Length);
            float[] edges = { -1f, warningMin, safetyMin, safetyMax, warningMax, 1f };
            Color32 danger = new Color32(0xE0, 0x6C, 0x78, 255);
            Color32 warning = new Color32(0xF2, 0xB4, 0x5B, 255);
            Color32 safe = new Color32(0x73, 0xC7, 0xA1, 255);
            Color32[] colors = { danger, warning, safe, warning, danger };
            var space = ((Component)Balance).transform;
            float start = RectEdgeX(zones[0], space, false);
            float total = 0f;
            for (int i = 0; i < zones.Length; i++)
            {
                float width = (edges[i + 1] - edges[i]) * 212f;
                Assert.AreEqual(width > 0f, zones[i].gameObject.activeSelf, "zero 구간 비활성: " + i);
                Assert.That(zones[i].rect.width, Is.EqualTo(width).Within(0.01f), "구간 폭: " + i);
                Assert.That(RectEdgeX(zones[i], space, false),
                    Is.EqualTo(start + (edges[i] + 1f) * 212f).Within(0.01f), "구간 시작/틈 없음: " + i);
                Assert.That(RectEdgeX(zones[i], space, true),
                    Is.EqualTo(start + (edges[i + 1] + 1f) * 212f).Within(0.01f), "구간 끝: " + i);
                Assert.AreEqual(colors[i], (Color32)zones[i].GetComponent<Image>().color, "구간 색: " + i);
                total += zones[i].rect.width;
            }
            Assert.That(total, Is.EqualTo(424f).Within(0.01f));
            for (int i = 0; i < boundaries.Length; i++)
            {
                var boundary = boundaries[i];
                Assert.NotNull(boundary, "경계 RectTransform: " + i);
                bool visible = i == 0 ? safetyMin > warningMin : i == 3 ? warningMax > safetyMax : true;
                Assert.AreEqual(visible, boundary.gameObject.activeSelf, "경고 폭 0이면 외곽 경계만 숨김: " + i);
                Assert.That(space.InverseTransformPoint(boundary.TransformPoint(boundary.rect.center)).x,
                    Is.EqualTo(start + (edges[i + 1] + 1f) * 212f).Within(0.01f), "경계 순서: " + i);
            }
            for (int i = 0; i < masks.Length; i++)
            {
                var outside = zones[i == 0 ? 0 : 4];
                Assert.AreEqual(outside.gameObject.activeSelf, masks[i].gameObject.activeSelf, "바깥 위험 해칭만 표시");
                if (!outside.gameObject.activeSelf) continue;
                Assert.That(RectEdgeX(masks[i], space, false), Is.EqualTo(RectEdgeX(outside, space, false)).Within(0.01f));
                Assert.That(RectEdgeX(masks[i], space, true), Is.EqualTo(RectEdgeX(outside, space, true)).Within(0.01f));
            }
        }

        static float RectEdgeX(RectTransform rect, Transform space, bool right) =>
            space.InverseTransformPoint(rect.TransformPoint(new Vector3(right ? rect.rect.xMax : rect.rect.xMin, rect.rect.center.y, 0f))).x;

        void AssertNormal()
        {
            Assert.AreEqual(0, Property(preview, "SelectedSample"));
            Assert.AreEqual(false, Property(preview, "DangerSample"));
            Assert.That(((RectTransform)Field(Balance, "needle")).anchoredPosition.x + 10f,
                Is.EqualTo(60f + 1.18f * 212f).Within(0.01f));
            Assert.AreEqual(false, Property(preview, "Observing"));
            Assert.AreEqual(true, Property(preview, "OnTightrope"));
            Assert.AreEqual(true, Property(preview, "PerformanceActive"));
            Assert.AreEqual("2:41", ((TMP_Text)Field(Timer, "timeText")).text);
            foreach (object view in new[] { Timer, Progress, Balance }) Assert.AreEqual(true, Property(view, "HasValidData"));
            Assert.AreEqual(1f, ((CanvasGroup)Field(Balance, "visibility")).alpha);
        }

        void AssertProgress(bool valid, float length, float[] distances, int local, float? fire, float? maximum, float[] falls, bool near)
        {
            Assert.AreEqual(valid, Call(Progress, "SetProgress", length, distances, local, fire, maximum, falls, near));
            Assert.AreEqual(valid, Property(Progress, "HasValidData"));
            if (valid) return;
            Assert.IsTrue(((RectTransform[])Field(Progress, "playerRoots")).All(x => !x.gameObject.activeSelf));
            Assert.AreEqual("—", ((TMP_Text)Field(Progress, "remainingText")).text);
            Assert.IsFalse(((Image)Field(Progress, "arrivalMarker")).enabled);
            Assert.IsFalse(((Image)Field(Progress, "fireMarker")).enabled);
            Assert.IsFalse(((Image)Field(Progress, "maximumMarker")).enabled);
        }

        void AssertBalance(bool valid, float balance, float min, float max, float dwell, string left, string right, bool leftPressed, bool rightPressed)
        {
            Assert.AreEqual(valid, Call(Balance, "SetBalance", balance, min, max, dwell, left, right, leftPressed, rightPressed));
            Assert.AreEqual(valid, Property(Balance, "HasValidData"));
        }

        static void AssertUnavailable(object view) => Assert.AreEqual(false, Property(view, "HasValidData"));
        static void AssertTimerBindings(object timer)
        {
            foreach (string name in new[] { "timeText", "clockIcon", "warningAccent" }) Assert.NotNull(Field(timer, name), name);
        }
        static void AssertText(TMP_Text text)
        {
            Assert.NotNull(text.font, text.name);
            Assert.IsTrue(text.font.HasCharacters(text.text), text.name + ": " + text.text + " 글리프 누락");
            Assert.IsFalse(text.raycastTarget, text.name);
        }
        static void AssertNoMissingScripts(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>()) Assert.NotNull(component, transform.name);
        }
        IEnumerable<T> Components<T>() where T : Component => testScene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true));
        Button ButtonFor(string method) => Components<Button>().Single(x => x.onClick.GetPersistentEventCount() == 1 &&
            x.onClick.GetPersistentTarget(0) == preview && x.onClick.GetPersistentMethodName(0) == method);
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
            // Nullable<T> with a value is boxed as T; an empty nullable is boxed as null.
            // Match the entire signature, never GetMethod(name), because SetBalance has two overloads.
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == name && !x.ContainsGenericParameters)
                .Where(x => x.GetParameters().Length == args.Length)
                .Where(x => x.GetParameters().Select((parameter, index) =>
                    MatchesArgument(parameter.ParameterType, args[index])).All(matches => matches))
                .ToArray();
            Assert.AreEqual(1, methods.Length, target.GetType().Name + "." + name + "(" +
                string.Join(", ", args.Select(x => x == null ? "null" : x.GetType().Name)) + ") 서명 일치");
            return methods[0].Invoke(target, args);
        }
        static bool MatchesArgument(Type parameterType, object argument)
        {
            Type underlying = Nullable.GetUnderlyingType(parameterType);
            if (argument == null) return !parameterType.IsValueType || underlying != null;
            Type argumentType = argument.GetType();
            return parameterType == argumentType || underlying == argumentType || parameterType.IsAssignableFrom(argumentType);
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
            Components<EventSystem>().Single(x => x.isActiveAndEnabled).SetSelectedGameObject(button.gameObject);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }
    }
}
