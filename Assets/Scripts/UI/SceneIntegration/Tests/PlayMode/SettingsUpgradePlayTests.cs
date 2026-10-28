using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static readonly string[] GraphicsKeys = { "Fullscreen", "ScreenMode", "ResolutionWidth", "ResolutionHeight", "FrameRateLimit" };
        static int?[] CaptureGraphics() => GraphicsKeys.Select(k => PlayerPrefs.HasKey("CurtainCall.Settings." + k) ? (int?)PlayerPrefs.GetInt("CurtainCall.Settings." + k) : null).ToArray();
        static void RestoreGraphics(int?[] values)
        {
            for (int i = 0; i < GraphicsKeys.Length; i++)
            {
                string key = "CurtainCall.Settings." + GraphicsKeys[i];
                if (values[i].HasValue) PlayerPrefs.SetInt(key, values[i].Value); else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }
        UnityEngine.UI.Button ExtensionButton(Component owner, string name) => owner.GetComponentsInChildren<UnityEngine.UI.Button>(true)
            .Single(b => b.name == name && b.transform.root.GetComponentsInChildren<Canvas>(true).Any(c => c.name == "SettingsExtensionCanvas")
                && b.GetComponentInParent<Canvas>().name == "SettingsExtensionCanvas");

        UnityEngine.UI.Slider SettingsSlider(object view, string id) => ((Component)view).GetComponentsInChildren<UnityEngine.UI.Slider>(true).Single(s => s.transform.parent.name == id);
        UnityEngine.UI.Toggle SettingsToggle(object view, string id) => ((Component)view).GetComponentsInChildren<UnityEngine.UI.Toggle>(true).Single(s => s.transform.parent.name == id);
        Transform SettingsRow(object view, int page, string id) => ((Component)view).transform.Find($"Panel/Page_{page}/Viewport/Content/{id}");
        Action CaptureSettingsExtras()
        {
            string[] floats = { "CurtainCall.Settings.Volume", "CurtainCall.Settings.BgmVolume", "CurtainCall.Settings.SfxVolume", "CurtainCall.Voice.MicVolume", "CurtainCall.Voice.TeamVolume" };
            string[] ints = { "CurtainCall.Voice.MicMuted", "CurtainCall.Voice.TeamMuted" };
            var fv = floats.Select(k => PlayerPrefs.HasKey(k) ? (float?)PlayerPrefs.GetFloat(k) : null).ToArray();
            var iv = ints.Select(k => PlayerPrefs.HasKey(k) ? (int?)PlayerPrefs.GetInt(k) : null).ToArray();
            const string key = "CurtainCall.Input.CommonBindings.v1";
            string binding = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            float overall = (float)Static("CurtainCall.StartLobby.ClientSettings", "Volume");
            float bgm = (float)Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume"), sfx = (float)Static("CurtainCall.StartLobby.ClientSettings", "SfxVolume");
            return () =>
            {
                T("CurtainCall.StartLobby.ClientSettings").GetProperty("Volume").SetValue(null, overall);
                T("CurtainCall.StartLobby.ClientSettings").GetProperty("BgmVolume").SetValue(null, bgm);
                T("CurtainCall.StartLobby.ClientSettings").GetProperty("SfxVolume").SetValue(null, sfx);
                for (int i = 0; i < floats.Length; i++) { if (fv[i].HasValue) PlayerPrefs.SetFloat(floats[i], fv[i].Value); else PlayerPrefs.DeleteKey(floats[i]); }
                for (int i = 0; i < ints.Length; i++) { if (iv[i].HasValue) PlayerPrefs.SetInt(ints[i], iv[i].Value); else PlayerPrefs.DeleteKey(ints[i]); }
                if (binding != null) PlayerPrefs.SetString(key, binding); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            };
        }
        [UnityTest]
        public IEnumerator SettingsUpgrade_GraphicsDraftCancelApplyAndPersistence()
        {
            var saved = CaptureGraphics(); var restoreExtras = CaptureSettingsExtras();
            int previousTarget = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            try
            {
                yield return SceneManager.LoadSceneAsync("Start"); yield return MenuReady();
                var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                var settings = Get(owner, "Settings");
                var start = Find("CurtainCall.StartLobby.StartMenu");
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                yield return new WaitForSecondsRealtime(.25f);
                Assert.IsFalse(owner.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "AbandonRun").gameObject.activeInHierarchy);
                var view = Get(settings, "Main");
                Assert.IsFalse((bool)Call(view, "IsPageEnabled", 0));
                Assert.AreEqual(5, ((Component)view).GetComponentsInChildren<UnityEngine.UI.Button>(true).Count(b => b.name.StartsWith("Tab_")));
                Call(settings, "OpenGraphics"); yield return null;
                Assert.AreEqual(1, Get(view, "CurrentPage"));
                var fpsNext = SettingsRow(view, 1, "FrameRate").Find("Next").GetComponent<UnityEngine.UI.Button>();
                int original = (int)Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit");
                fpsNext.onClick.Invoke();
                Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit"));
                yield return MenuEscape();
                Assert.IsFalse((bool)Get(owner, "IsOpen"));
                Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit"));
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                fpsNext = SettingsRow(view, 1, "FrameRate").Find("Next").GetComponent<UnityEngine.UI.Button>();
                fpsNext.onClick.Invoke();
                string shown = SettingsRow(view, 1, "FrameRate").Find("Value").GetComponent<TMP_Text>().text;
                int expected = shown == "제한 없음" ? -1 : int.Parse(shown.Split(' ')[0]);
                Call(view, "RequestApply"); yield return null;
                Assert.AreEqual(expected, Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit"));
                Assert.AreEqual(expected, Application.targetFrameRate);
                Assert.AreEqual(0, QualitySettings.vSyncCount);
                Assert.IsFalse((bool)Get(settings, "IsExtensionOpen"));
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                Assert.AreEqual(shown, SettingsRow(view, 1, "FrameRate").Find("Value").GetComponent<TMP_Text>().text);
                var resolution = SettingsRow(view, 1, "Resolution").Find("Next").GetComponent<UnityEngine.UI.Button>();
                for (int i = 0; i < 3; i++)
                {
                    var value = SettingsRow(view, 1, "ScreenMode").Find("Value").GetComponent<TMP_Text>().text;
                    Assert.AreEqual(value != "전체 화면 창", resolution.interactable);
                    SettingsRow(view, 1, "ScreenMode").Find("Next").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                }
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-upgrade-graphics.png"));
                yield return null;
                yield return MenuEscape(); yield return MenuEscape();
                Assert.IsFalse((bool)Get(owner, "IsOpen"));
            }
            finally
            {
                RestoreGraphics(saved); restoreExtras(); Application.targetFrameRate = previousTarget; QualitySettings.vSyncCount = previousVSync;
            }
        }

        [UnityTest]
        public IEnumerator SettingsUpgrade_FiveTabsSoundControlsAndCredits()
        {
            var restore = CaptureSettingsExtras(); var graphics = CaptureGraphics();
            int target = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            try
            {
                UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
                yield return SceneManager.LoadSceneAsync("Start"); yield return MenuReady();
                var start = Find("CurtainCall.StartLobby.StartMenu");
                var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                var settings = Get(owner, "Settings"); var view = Get(settings, "Main");
                var labels = ((Component)view).GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => b.name.StartsWith("Tab_")).OrderBy(b => b.name).Select(b => b.GetComponentInChildren<TMP_Text>().text).ToArray();
                CollectionAssert.AreEqual(new[] { "게임", "그래픽", "조작", "사운드", "기타" }, labels);
                Assert.IsFalse((bool)Call(view, "IsPageEnabled", 0));
                Call(view, "ShowPage", 3); yield return null;
                float original = (float)Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume");
                SettingsSlider(view, "bgmVolume").value = .31f; SettingsSlider(view, "sfxVolume").value = .42f;
                Call(view, "ShowPage", 1); Call(view, "ShowPage", 3);
                Assert.AreEqual(.31f, SettingsSlider(view, "bgmVolume").value);
                Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume"));
                Assert.IsNotNull(SettingsRow(view, 3, "InputDevice")); Assert.IsNotNull(SettingsRow(view, 3, "OutputDevice"));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-tabs-sound.png")); yield return null;
                Call(view, "ShowPage", 2); yield return null;
                var binding = ((Component)view).GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Action" && b.transform.parent.name.Contains(":"));
                string keyBefore = binding.GetComponentInChildren<TMP_Text>().text;
                binding.onClick.Invoke(); yield return null;
                Assert.IsTrue((bool)Get(settings, "IsCapturingBinding"));
                CursorTestKey("F10"); yield return null; yield return null; CursorTestKey(null); yield return null;
                Assert.IsFalse((bool)Get(settings, "IsCapturingBinding"));
                Assert.AreNotEqual(keyBefore, binding.GetComponentInChildren<TMP_Text>().text);
                binding.onClick.Invoke(); yield return MenuEscape();
                Assert.IsFalse((bool)Get(settings, "IsCapturingBinding")); Assert.IsTrue((bool)Get(view, "IsVisible"));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-tabs-controls.png")); yield return null;
                Call(view, "ShowPage", 4); yield return null;
                var license = SettingsRow(view, 4, "PretendardLicense").GetComponentInChildren<TMP_Text>();
                StringAssert.Contains("SIL OPEN FONT LICENSE", license.text);
                StringAssert.Contains("MoHoDu", SettingsRow(view, 4, "Developers").GetComponentInChildren<TMP_Text>().text);
                var scroll = license.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-tabs-credits.png")); yield return null;
                yield return MenuEscape(); Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume"));
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                Call(view, "ShowPage", 3); SettingsSlider(view, "bgmVolume").value = .31f; SettingsSlider(view, "sfxVolume").value = .42f;
                Call(view, "RequestApply"); yield return null;
                Assert.AreEqual(.31f, Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume"));
                Assert.AreEqual(.42f, Static("CurtainCall.StartLobby.ClientSettings", "SfxVolume"));
                Assert.IsFalse((bool)Get(owner, "IsOpen"));
            }
            finally { CursorTestKey(null); restore(); RestoreGraphics(graphics); Application.targetFrameRate = target; QualitySettings.vSyncCount = vsync; }
        }

        [UnityTest]
        public IEnumerator SettingsUpgrade_HostAbandonCancelConfirmAndClientRejection()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase"); yield return MenuReady();
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var run = Find("CurtainCall.Campaign.CampaignRun");
            Assert.IsTrue((bool)Get(run, "CanAbandonRun"));
            int generation = (int)Field(State, "Generation");
            var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
            yield return MenuEscape();
            Call(Get(owner, "Menu"), "RequestSettings"); yield return new WaitForSecondsRealtime(.25f);
            var settings = Get(owner, "Settings");
            Call(settings, "RequestAbandon"); yield return null;
            Assert.IsTrue((bool)Get(settings, "IsExtensionOpen"));
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-upgrade-abandon.png"));
            yield return null;
            ExtensionButton(owner, "Cancel").onClick.Invoke(); yield return null;
            Assert.AreEqual(generation, Field(State, "Generation"));
            Assert.IsFalse((bool)Get(settings, "IsExtensionOpen"));
            Call(settings, "RequestAbandon"); yield return null; yield return MenuEscape();
            Assert.AreEqual(generation, Field(State, "Generation"));
            var hostField = run.GetType().GetField("host", BindingFlags.Instance | BindingFlags.NonPublic);
            var host = hostField.GetValue(run);
            var context = State;
            try
            {
                hostField.SetValue(run, null);
                Assert.IsFalse((bool)Get(run, "CanAbandonRun"));
                Assert.IsFalse((bool)Call(run, "AbandonRun", context));
                Call(settings, "RequestAbandon");
                Assert.IsFalse((bool)Get(settings, "IsExtensionOpen"), "클라이언트 권한 분기 주입 검사이며 실제 2인 검증은 아니다.");
            }
            finally { hostField.SetValue(run, host); }
            var player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Call(player, "ServerSetOneArm"); yield return null;
            Call(settings, "RequestAbandon"); yield return null;
            Call(settings, "ConfirmAbandon"); Call(settings, "ConfirmAbandon");
            float until = Time.realtimeSinceStartup + 60;
            while ((!SceneManager.GetSceneByName("Tightrope").isLoaded || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")
                || (bool)Get(Find("CurtainCall.SpaceLink.SpaceLinkDirector"), "IsTransitioning")) && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(SceneManager.GetSceneByName("Tightrope").isLoaded);
            Assert.AreEqual(generation + 1, Field(State, "Generation"));
            Assert.AreEqual(1, Field(State, "Day"));
            Assert.AreEqual(0, Field(State, "PerformanceIndex"));
            Assert.AreEqual("Abandoned", Field(State, "LastEnd").ToString());
            Assert.IsTrue((bool)Get(Find("CurtainCall.Network.Session.NetworkSessionManager"), "IsHost"));
            player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Assert.AreEqual(0L, Get(Find("CurtainCall.Reward.PerformanceRewards"), "Balance"));
            Assert.IsFalse((bool)Get(run, "CanAbandonRun"));
            Assert.IsFalse((bool)Call(run, "AbandonRun", context));
            until = Time.realtimeSinceStartup + 3;
            while ((bool)Static("CurtainCall.Player.InputGate", "IsRequested") && Time.realtimeSinceStartup < until) yield return null;
            var blockers = (IEnumerable)T("CurtainCall.Player.InputGate").GetField("blockers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsRequested"), string.Join(", ", blockers.Cast<object>().Select(b => b.ToString())));
        }
    }
}
