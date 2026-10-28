using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.Telemetry.Pipeline.PlayTests
{
    public sealed class FrameRateSettingsTests
    {
        const string Prefix = "CurtainCall.Settings.";
        static readonly string[] IntegerKeys = { "Fullscreen", "ScreenMode", "ResolutionWidth", "ResolutionHeight", "FrameRateLimit" };
        readonly Dictionary<string, int> savedIntegers = new Dictionary<string, int>();
        Type settingsType;
        int previousTarget;
        int previousVSync;
        float previousAudio;
        float previousVolume;
        bool hadVolume;

        [SetUp]
        public void PreserveLocalPreferences()
        {
            // 실제 플레이어 창 전환 검증은 빌드에서 사람이 수행한다.
            if (!Application.isEditor) Assert.Ignore("Local graphics regression tests must not change a player's display.");
            settingsType = Type.GetType("CurtainCall.StartLobby.ClientSettings, Assembly-CSharp", true);
            previousTarget = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            previousAudio = AudioListener.volume;
            hadVolume = PlayerPrefs.HasKey(Prefix + "Volume");
            previousVolume = PlayerPrefs.GetFloat(Prefix + "Volume");
            savedIntegers.Clear();
            foreach (string name in IntegerKeys)
            {
                string key = Prefix + name;
                if (PlayerPrefs.HasKey(key)) savedIntegers[key] = PlayerPrefs.GetInt(key);
                PlayerPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void RestoreLocalPreferences()
        {
            if (settingsType == null) return;
            foreach (string name in IntegerKeys)
            {
                string key = Prefix + name;
                if (savedIntegers.TryGetValue(key, out int value)) PlayerPrefs.SetInt(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
            if (hadVolume) PlayerPrefs.SetFloat(Prefix + "Volume", previousVolume);
            else PlayerPrefs.DeleteKey(Prefix + "Volume");
            PlayerPrefs.Save();
            Application.targetFrameRate = previousTarget;
            QualitySettings.vSyncCount = previousVSync;
            AudioListener.volume = previousAudio;
        }

        object Get(string name) => settingsType.GetProperty(name).GetValue(null);
        void Set(string name, object value) => settingsType.GetProperty(name).SetValue(null, value);
        void ApplySaved() => settingsType.GetMethod("ApplySaved").Invoke(null, null);
        void SetGraphics(FullScreenMode mode, int width, int height, int fps) =>
            settingsType.GetMethod("SetGraphics").Invoke(null, new object[] { mode, width, height, fps });

        [Test]
        public void MissingPreferencesDefaultToSixtyAndBorderless()
        {
            Assert.That(Get("FrameRateLimit"), Is.EqualTo(60));
            Assert.That(Get("ScreenMode"), Is.EqualTo(FullScreenMode.FullScreenWindow));
            QualitySettings.vSyncCount = 1;
            ApplySaved();
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(90)]
        [TestCase(120)]
        [TestCase(144)]
        [TestCase(165)]
        [TestCase(240)]
        [TestCase(-1)]
        public void SupportedFrameRateIsStoredAndApplied(int fps)
        {
            Set("FrameRateLimit", fps);
            Assert.That(Get("FrameRateLimit"), Is.EqualTo(fps));
            Assert.That(PlayerPrefs.GetInt(Prefix + "FrameRateLimit"), Is.EqualTo(fps));
            Assert.That(Application.targetFrameRate, Is.EqualTo(fps));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-2)]
        [TestCase(59)]
        [TestCase(int.MaxValue)]
        public void InvalidFrameRateFallsBackToSixty(int fps)
        {
            PlayerPrefs.SetInt(Prefix + "FrameRateLimit", fps);
            Assert.That(Get("FrameRateLimit"), Is.EqualTo(60));
            ApplySaved();
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(PlayerPrefs.GetInt(Prefix + "FrameRateLimit"), Is.EqualTo(60));
        }

        [TestCase(0, FullScreenMode.Windowed)]
        [TestCase(1, FullScreenMode.FullScreenWindow)]
        public void LegacyFullscreenMigratesWithoutChangingMeaning(int legacy, FullScreenMode expected)
        {
            PlayerPrefs.SetInt(Prefix + "Fullscreen", legacy);
            ApplySaved();
            Assert.That(Get("ScreenMode"), Is.EqualTo(expected));
            Assert.That(Get("Fullscreen"), Is.EqualTo(legacy == 1));
            Assert.That(PlayerPrefs.GetInt(Prefix + "ScreenMode"), Is.EqualTo((int)expected));
        }

        [Test]
        public void NewScreenModeTakesPriorityOverLegacyBool()
        {
            PlayerPrefs.SetInt(Prefix + "Fullscreen", 0);
            PlayerPrefs.SetInt(Prefix + "ScreenMode", (int)FullScreenMode.ExclusiveFullScreen);
            ApplySaved();
            Assert.That(Get("ScreenMode"), Is.EqualTo(FullScreenMode.ExclusiveFullScreen));
            Assert.That(PlayerPrefs.GetInt(Prefix + "Fullscreen"), Is.EqualTo(1));
        }

        [TestCase(FullScreenMode.ExclusiveFullScreen)]
        [TestCase(FullScreenMode.FullScreenWindow)]
        [TestCase(FullScreenMode.Windowed)]
        public void ScreenModesAndCompatibilityApiArePreserved(FullScreenMode mode)
        {
            var resolution = ((Resolution[])Get("AvailableResolutions"))[0];
            var actualMode = Screen.fullScreenMode;
            int actualWidth = Screen.width;
            int actualHeight = Screen.height;
            SetGraphics(mode, resolution.width, resolution.height, 90);
            Assert.That(Get("ScreenMode"), Is.EqualTo(mode));
            Assert.That(Get("Fullscreen"), Is.EqualTo(mode != FullScreenMode.Windowed));
            Assert.That(Get("ResolutionWidth"), Is.EqualTo(resolution.width));
            Assert.That(Get("ResolutionHeight"), Is.EqualTo(resolution.height));
            Assert.That(Screen.fullScreenMode, Is.EqualTo(actualMode), "Editor screen must not change.");
            Assert.That(Screen.width, Is.EqualTo(actualWidth));
            Assert.That(Screen.height, Is.EqualTo(actualHeight));
            Set("Fullscreen", false);
            Assert.That(Get("ScreenMode"), Is.EqualTo(FullScreenMode.Windowed));
            Set("Fullscreen", true);
            Assert.That(Get("ScreenMode"), Is.EqualTo(FullScreenMode.FullScreenWindow));
            Set("Volume", .23f);
            Assert.That(AudioListener.volume, Is.EqualTo(.23f).Within(.0001f));
        }

        [Test]
        public void ResolutionChoicesAreValidDistinctSortedAndInvalidSavedValuesRecover()
        {
            var available = (Resolution[])Get("AvailableResolutions");
            Assert.That(available, Is.Not.Empty);
            var sizes = new HashSet<Vector2Int>();
            Resolution previous = default;
            foreach (var resolution in available)
            {
                Assert.That(resolution.width, Is.GreaterThan(0));
                Assert.That(resolution.height, Is.GreaterThan(0));
                Assert.That(sizes.Add(new Vector2Int(resolution.width, resolution.height)), Is.True);
                Assert.That(resolution.width > previous.width ||
                    (resolution.width == previous.width && resolution.height > previous.height), Is.True);
                previous = resolution;
            }
            PlayerPrefs.SetInt(Prefix + "ResolutionWidth", -10);
            PlayerPrefs.SetInt(Prefix + "ResolutionHeight", int.MaxValue);
            PlayerPrefs.SetInt(Prefix + "ScreenMode", (int)FullScreenMode.MaximizedWindow);
            ApplySaved();
            Assert.That(Get("ScreenMode"), Is.EqualTo(FullScreenMode.FullScreenWindow));
            var recovered = new Vector2Int((int)Get("ResolutionWidth"), (int)Get("ResolutionHeight"));
            Assert.That(sizes.Contains(recovered), Is.True);
            Assert.That(PlayerPrefs.GetInt(Prefix + "ResolutionWidth"), Is.EqualTo(recovered.x));
            Assert.That(PlayerPrefs.GetInt(Prefix + "ResolutionHeight"), Is.EqualTo(recovered.y));
        }

        [UnityTest]
        public IEnumerator LocalLimitSurvivesSceneChangesWithoutPerFrameEnforcement()
        {
            var type = Type.GetType("CurtainCall.Settings.FrameRateController, Assembly-CSharp", true);
            var instance = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            Scene temporary = default;
            try
            {
                yield return null;
                var controller = instance.GetValue(null);
                Assert.That(controller, Is.Not.Null, "Runtime bootstrap must create the controller.");
                Set("FrameRateLimit", 120);
                yield return null;
                Assert.That(Application.targetFrameRate, Is.EqualTo(120));
                Set("FrameRateLimit", -1);
                temporary = SceneManager.CreateScene("LocalFrameRateSettingsTest");
                yield return SceneManager.UnloadSceneAsync(temporary);
                yield return null;
                Assert.That(instance.GetValue(null), Is.SameAs(controller));
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);

                // 개발 도구의 일시 조정은 허용하되 저장 설정 자체는 바꾸지 않는다.
                Application.targetFrameRate = 37;
                QualitySettings.vSyncCount = 1;
                yield return null;
                yield return null;
                Assert.That(Application.targetFrameRate, Is.EqualTo(37), "No per-frame global override.");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
                Assert.That(Get("FrameRateLimit"), Is.EqualTo(-1));
                ApplySaved();
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
            }
            finally
            {
                if (temporary.IsValid() && temporary.isLoaded) SceneManager.UnloadSceneAsync(temporary);
            }
        }
    }
}
