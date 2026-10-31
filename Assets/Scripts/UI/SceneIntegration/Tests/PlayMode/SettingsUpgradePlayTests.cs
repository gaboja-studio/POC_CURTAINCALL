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
        static readonly string[] GraphicsKeys = { "Fullscreen", "ScreenMode", "ResolutionWidth", "ResolutionHeight", "FrameRateLimit", "HideNearbyCharacter", "ShowBloodEffect" };
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
        const string SettingsContentPath = "00_Visuals/40_Content/30_Scroll/00_Viewport/00_Content";
        Transform SettingsContent(object view) => ((Component)view).transform.Find(SettingsContentPath);
        UnityEngine.UI.Toggle[] SettingsTabs(object view) => ((Component)view).transform.Find("00_Visuals/40_Content/10_Tabs")
            .GetComponentsInChildren<UnityEngine.UI.Toggle>(true).OrderBy(t => t.name).ToArray();
        UnityEngine.UI.Button SettingsConfirmationButton(object view, string name) => ((Component)view).transform
            .Find("00_Visuals/90_ActionConfirmation/00_Dialog/40_Content/" + name).GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Slider SettingsSlider(object view, string id) => SettingsContent(view).Find(id + "_Row/10_Slider").GetComponent<UnityEngine.UI.Slider>();
        UnityEngine.UI.Toggle SettingsToggle(object view, string id) => SettingsContent(view).Find(id + "_Row/10_Toggle").GetComponent<UnityEngine.UI.Toggle>();
        Transform SettingsRow(object view, int page, string id)
        {
            // v2는 현재 탭만 생성하므로 호출자가 먼저 탭을 선택해야 합니다.
            Assert.AreEqual(page, Get(view, "CurrentPage"));
            var row = SettingsContent(view).Find(id + "_Row");
            Assert.IsNotNull(row, id);
            return row;
        }
        TMP_Text SettingsParagraph(object view, string id) => SettingsContent(view).Find(id + "_TMP").GetComponent<TMP_Text>();
        Action CaptureSettingsExtras()
        {
            string[] floats = { "CurtainCall.Settings.Volume", "CurtainCall.Settings.BgmVolume", "CurtainCall.Settings.SfxVolume", "CurtainCall.Voice.MicVolume", "CurtainCall.Voice.TeamVolume" };
            string[] ints = { "CurtainCall.Voice.MicMuted", "CurtainCall.Voice.TeamMuted" };
            var fv = floats.Select(k => PlayerPrefs.HasKey(k) ? (float?)PlayerPrefs.GetFloat(k) : null).ToArray();
            var iv = ints.Select(k => PlayerPrefs.HasKey(k) ? (int?)PlayerPrefs.GetInt(k) : null).ToArray();
            const string key = "CurtainCall.Input.CommonBindings.v1";
            string binding = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            string[] deviceKeys = { "CurtainCall.Voice.InputDevice", "CurtainCall.Voice.OutputDevice" };
            var devices = deviceKeys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
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
                for (int i = 0; i < deviceKeys.Length; i++) { if (devices[i] != null) PlayerPrefs.SetString(deviceKeys[i], devices[i]); else PlayerPrefs.DeleteKey(deviceKeys[i]); }
                PlayerPrefs.Save();
            };
        }
        UnityEngine.UI.Button TabReset(object view, string page)
        {
            var content = SettingsContent(view);
            Assert.AreEqual("Reset_" + page + "_Row", content.GetChild(content.childCount - 1).name, "초기화는 탭 본문 최하단이어야 합니다.");
            return content.Find("Reset_" + page + "_Row/10_Action").GetComponent<UnityEngine.UI.Button>();
        }
        [UnityTest]
        public IEnumerator SettingsUpgrade_DuplicateKeysAreConfirmedStagedAndSavedOnlyOnApply()
        {
            var restore = CaptureSettingsExtras(); var graphics = CaptureGraphics();
            int target = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            var preferences = T("CurtainCall.Player.InputBindingPreferences");
            const string preferenceKey = "CurtainCall.Input.CommonBindings.v1";
            try
            {
                preferences.GetMethod("ResetDefaults").Invoke(null, null);
                yield return SceneManager.LoadSceneAsync("Start"); yield return MenuReady();
                UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
                var start = Find("CurtainCall.StartLobby.StartMenu");
                var settings = Get(Find("CurtainCall.UI.SceneIntegration.CommonMenuUI"), "Settings");
                void OpenSettings() => Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                OpenSettings(); yield return null;
                var view = Get(settings, "Main"); Call(view, "ShowPage", 2); yield return null;
                object[] Snapshot() => ((IEnumerable)Call(view, "GetDraftSnapshot")).Cast<object>().ToArray();
                string RowFor(string path) => (string)Field(Snapshot().Single(v => (string)Field(v, "bindingId") == path), "rowId");
                string PathFor(string row) => (string)Field(Snapshot().Single(v => (string)Field(v, "rowId") == row), "bindingId");
                string forward = RowFor("<Keyboard>/w"), backward = RowFor("<Keyboard>/s"), jump = RowFor("<Keyboard>/space");
                var order = SettingsContent(view).Cast<Transform>().Where(t => t.name.EndsWith("_Row")).Select(t => t.name).ToArray();
                Assert.AreEqual(forward + "_Row", order[0]); Assert.AreEqual(backward + "_Row", order[1]);
                UnityEngine.UI.Button Rebind(string row) => SettingsRow(view, 2, row).Find("10_Rebind").GetComponent<UnityEngine.UI.Button>();
                IEnumerator Press(string row, string key)
                {
                    Rebind(row).onClick.Invoke(); yield return null;
                    CursorTestKey(key); yield return null; yield return null; CursorTestKey(null); yield return null;
                }
                yield return Press(jump, "W");
                Assert.IsTrue((bool)Get(view, "IsConfirmationOpen"));
                var dialog = ((Component)view).transform.Find("00_Visuals/90_ActionConfirmation/00_Dialog/40_Content");
                Assert.AreEqual("이미 앞으로 이동에 할당된 키입니다. 키 설정 시 할당된 조작을 다시 설정해야 합니다.", dialog.Find("10_BodyTMP").GetComponent<TMP_Text>().text);
                Assert.AreEqual("예", SettingsConfirmationButton(view, "30_Confirm").GetComponentInChildren<TMP_Text>().text);
                Assert.AreEqual("아니오", SettingsConfirmationButton(view, "20_Back").GetComponentInChildren<TMP_Text>().text);
                SettingsConfirmationButton(view, "20_Back").onClick.Invoke(); yield return null;
                Assert.AreEqual("<Keyboard>/w", PathFor(forward)); Assert.AreEqual("<Keyboard>/space", PathFor(jump));
                yield return Press(jump, "W"); Call(view, "Cancel"); yield return null;
                Assert.IsFalse((bool)Get(view, "IsConfirmationOpen")); Assert.IsTrue((bool)Get(view, "IsVisible"));
                Assert.AreEqual("<Keyboard>/space", PathFor(jump));
                yield return Press(jump, "W"); SettingsConfirmationButton(view, "30_Confirm").onClick.Invoke(); yield return null;
                Assert.AreEqual("", PathFor(forward)); Assert.AreEqual("<Keyboard>/w", PathFor(jump));
                StringAssert.Contains("미할당", Rebind(forward).GetComponentInChildren<TMP_Text>().text);
                Assert.IsFalse(((UnityEngine.UI.Button)Get(view, "ApplyButton")).interactable);
                Call(view, "RequestApply"); yield return null;
                Assert.IsTrue((bool)Get(view, "IsVisible")); Assert.IsFalse(PlayerPrefs.HasKey(preferenceKey));
                // 다른 탭 편집이나 초기화도 빈 키가 남으면 적용할 수 없다.
                Call(view, "ShowPage", 3); yield return null; SettingsSlider(view, "bgmVolume").value = .39f;
                TabReset(view, "audio").onClick.Invoke(); yield return null;
                Assert.IsFalse(((UnityEngine.UI.Button)Get(view, "ApplyButton")).interactable);
                Call(view, "ShowPage", 2); yield return null;
                Assert.AreEqual("", PathFor(forward));
                yield return Press(forward, "F10");
                Assert.IsTrue(((UnityEngine.UI.Button)Get(view, "ApplyButton")).interactable);
                Assert.IsFalse(PlayerPrefs.HasKey(preferenceKey));
                var savedDraft = (UnityEngine.InputSystem.InputActionAsset)preferences.GetMethod("CreateDraft").Invoke(null, null);
                try { Assert.AreEqual("<Keyboard>/space", savedDraft.FindAction("Common/Action").bindings[0].effectivePath); }
                finally { UnityEngine.Object.Destroy(savedDraft); }
                // 완성된 편집도 닫으면 폐기한다.
                Call(view, "Cancel"); yield return null; OpenSettings(); yield return null;
                Call(view, "ShowPage", 2); yield return null;
                Assert.AreEqual("<Keyboard>/w", PathFor(forward)); Assert.AreEqual("<Keyboard>/space", PathFor(jump));
                yield return Press(jump, "W"); SettingsConfirmationButton(view, "30_Confirm").onClick.Invoke(); yield return null;
                yield return Press(forward, "F10");
                Call(view, "RequestApply"); yield return null;
                Assert.IsFalse((bool)Get(view, "IsVisible")); Assert.IsTrue(PlayerPrefs.HasKey(preferenceKey));
                OpenSettings(); yield return null; Call(view, "ShowPage", 2); yield return null;
                Assert.AreEqual("<Keyboard>/f10", PathFor(forward)); Assert.AreEqual("<Keyboard>/w", PathFor(jump));
                TabReset(view, "controls").onClick.Invoke(); yield return null;
                Assert.AreEqual("<Keyboard>/w", PathFor(forward)); Assert.AreEqual("<Keyboard>/space", PathFor(jump));
                Call(view, "Cancel"); yield return null;
            }
            finally { CursorTestKey(null); restore(); RestoreGraphics(graphics); Application.targetFrameRate = target; QualitySettings.vSyncCount = vsync; }
        }

        [UnityTest]
        public IEnumerator SettingsUpgrade_TabResetsAreIsolatedStagedAndSaved()
        {
            var graphics = CaptureGraphics(); var restore = CaptureSettingsExtras();
            int target = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            try
            {
                yield return SceneManager.LoadSceneAsync("Start"); yield return MenuReady();
                var client = T("CurtainCall.StartLobby.ClientSettings");
                client.GetMethod("SetGraphics").Invoke(null, new object[] { FullScreenMode.Windowed, (int)Static(client.FullName, "ResolutionWidth"), (int)Static(client.FullName, "ResolutionHeight"), 144 });
                client.GetProperty("Volume").SetValue(null, .23f); client.GetProperty("BgmVolume").SetValue(null, .27f);
                var voice = Find("CurtainCall.Voice.VoiceChat");
                // VoiceChat은 씬 전환 후에도 앞선 검사의 SDK 초기화 상태를 유지한다.
                bool devicesWereReady = (bool)Get(voice, "AreAudioDevicesReady");
                Call(voice, "ApplyAudioPreferences", .24f, true, .35f, true);
                Assert.AreEqual(devicesWereReady, Get(voice, "AreAudioDevicesReady"), "음량 변경은 장치 서비스를 새로 초기화하거나 종료하지 않습니다.");
                // 준비된 SDK에서는 실제 선택과 일치하는 저장값을 사용한다(가짜 ID는 오프라인 경로만).
                string inputBefore = devicesWereReady ? (string)Get(Get(voice, "ActiveInputDevice"), "DeviceID") : "reset-test-input";
                string outputBefore = devicesWereReady ? (string)Get(Get(voice, "ActiveOutputDevice"), "DeviceID") : "reset-test-output";
                PlayerPrefs.SetString("CurtainCall.Voice.InputDevice", inputBefore); PlayerPrefs.SetString("CurtainCall.Voice.OutputDevice", outputBefore); PlayerPrefs.Save();
                var start = Find("CurtainCall.StartLobby.StartMenu"); var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                var settings = Get(owner, "Settings");
                void OpenSettings() => Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                OpenSettings(); yield return null; var view = Get(settings, "Main");
                Call(view, "ShowPage", 3); yield return null; SettingsSlider(view, "bgmVolume").value = .42f;
                Call(view, "ShowPage", 1); yield return null; TabReset(view, "graphics").onClick.Invoke(); yield return null;
                StringAssert.StartsWith("60 FPS", SettingsRow(view, 1, "FrameRate").Find("20_ValueTMP").GetComponent<TMP_Text>().text);
                Assert.AreEqual("전체 화면 창", SettingsRow(view, 1, "ScreenMode").Find("20_ValueTMP").GetComponent<TMP_Text>().text);
                Assert.AreEqual(144, Static(client.FullName, "FrameRateLimit"));
                Call(view, "ShowPage", 3); yield return null;
                Assert.AreEqual(.42f, SettingsSlider(view, "bgmVolume").value, "다른 탭 편집값은 초기화하지 않습니다.");
                TabReset(view, "audio").onClick.Invoke(); yield return null;
                Assert.AreEqual(.8f, SettingsSlider(view, "volume").value); Assert.AreEqual(1f, SettingsSlider(view, "bgmVolume").value);
                Assert.AreEqual(1f, SettingsSlider(view, "micVolume").value); Assert.IsTrue(SettingsToggle(view, "micEnabled").isOn); Assert.IsFalse(SettingsToggle(view, "teamMuted").isOn);
                Assert.AreEqual(.27f, Static(client.FullName, "BgmVolume")); Assert.IsTrue((bool)Get(voice, "IsMicMuted"));
                Assert.AreEqual(inputBefore, PlayerPrefs.GetString("CurtainCall.Voice.InputDevice"));
                Call(view, "ShowPage", 4); yield return null;
                Assert.IsFalse(TabReset(view, "other").interactable); Assert.IsFalse((bool)Get(settings, "IsExtensionOpen"));
                yield return MenuEscape();
                Assert.AreEqual(144, Static(client.FullName, "FrameRateLimit")); Assert.AreEqual(.27f, Static(client.FullName, "BgmVolume"));
                Assert.AreEqual(outputBefore, PlayerPrefs.GetString("CurtainCall.Voice.OutputDevice"));
                OpenSettings(); yield return null; Call(view, "ShowPage", 3); yield return null;
                TabReset(view, "audio").onClick.Invoke(); yield return null;
                var resetRow = SettingsRow(view, 3, "Reset_audio"); Call(view, "Reveal", (RectTransform)resetRow); yield return new WaitForSecondsRealtime(.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-v2-reset-audio.png")); yield return null;
                Call(view, "RequestApply"); yield return null;
                // 이미 초기화된 SDK의 기본 기기 선택은 비동기로 완료된다.
                double applyDeadline = Time.realtimeSinceStartupAsDouble + 15;
                while ((bool)Get(owner, "IsOpen") && Time.realtimeSinceStartupAsDouble < applyDeadline) yield return null;
                Assert.IsFalse((bool)Get(owner, "IsOpen")); Assert.AreEqual(.8f, Static(client.FullName, "Volume")); Assert.AreEqual(1f, Static(client.FullName, "BgmVolume"));
                Assert.AreEqual(1f, Get(voice, "MicVolume")); Assert.AreEqual(1f, Get(voice, "TeamVolume")); Assert.IsFalse((bool)Get(voice, "IsMicMuted")); Assert.IsFalse((bool)Get(voice, "IsTeamMuted"));
                Assert.AreEqual(devicesWereReady, Get(voice, "AreAudioDevicesReady"), "오디오 초기화는 SDK 준비 상태를 유지하며 미초기화 상태에서는 네트워크 초기화를 시작하지 않습니다.");
                if (devicesWereReady)
                {
                    string defaultDevice = (string)T("CurtainCall.Voice.VoiceChat").GetField("SystemDefaultDeviceId").GetRawConstantValue();
                    Assert.AreEqual(defaultDevice, PlayerPrefs.GetString("CurtainCall.Voice.InputDevice"));
                    Assert.AreEqual(defaultDevice, PlayerPrefs.GetString("CurtainCall.Voice.OutputDevice"));
                    Assert.AreEqual(defaultDevice, Get(Get(voice, "ActiveInputDevice"), "DeviceID"));
                    Assert.AreEqual(defaultDevice, Get(Get(voice, "ActiveOutputDevice"), "DeviceID"));
                }
                else
                {
                    Assert.IsFalse(PlayerPrefs.HasKey("CurtainCall.Voice.InputDevice"));
                    Assert.IsFalse(PlayerPrefs.HasKey("CurtainCall.Voice.OutputDevice"));
                }
                Assert.AreEqual(144, Static(client.FullName, "FrameRateLimit"));
                OpenSettings(); yield return null; Call(view, "ShowPage", 3); yield return null; Assert.AreEqual(1f, SettingsSlider(view, "bgmVolume").value);
                Call(view, "ShowPage", 1); yield return null; TabReset(view, "graphics").onClick.Invoke(); yield return null; Call(view, "RequestApply"); yield return null;
                Assert.AreEqual(60, Static(client.FullName, "FrameRateLimit")); Assert.AreEqual(60, Application.targetFrameRate); Assert.AreEqual(FullScreenMode.FullScreenWindow, Static(client.FullName, "ScreenMode"));
                OpenSettings(); yield return null;
                Assert.AreEqual("60 FPS (기본)", SettingsRow(view, 1, "FrameRate").Find("20_ValueTMP").GetComponent<TMP_Text>().text);
                yield return MenuEscape();
            }
            finally { restore(); RestoreGraphics(graphics); Application.targetFrameRate = target; QualitySettings.vSyncCount = vsync; }
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
                var view = Get(settings, "Main");
                Assert.AreEqual("CurtainCall.UI.SettingsV2.SettingsPanelView", view.GetType().FullName);
                Assert.IsNotNull(Resources.Load<GameObject>("Prefabs/UIs/SettingsUiV2/C14_Settings_v2"));
                var body = (RectTransform)((Component)view).transform.Find("00_Visuals/10_Body");
                Assert.AreEqual(new Vector2(944, 552), body.rect.size);
                Assert.IsFalse((bool)Call(view, "IsPageEnabled", 0));
                Assert.IsFalse(SettingsTabs(view)[0].interactable);
                Assert.AreEqual(5, SettingsTabs(view).Length);
                Assert.IsNull(SettingsContent(view).Find("RunAbandon_Row"));
                Assert.IsFalse(((UnityEngine.UI.Button)Get(view, "ApplyButton")).interactable, "변경 전에는 적용할 수 없습니다.");
                Call(settings, "OpenGraphics"); yield return null;
                Assert.AreEqual(1, Get(view, "CurrentPage"));
                bool originalHide = (bool)Static("CurtainCall.StartLobby.ClientSettings", "HideNearbyCharacter");
                SettingsToggle(view, "HideNearbyCharacter").isOn = !originalHide;
                var fpsNext = SettingsRow(view, 1, "FrameRate").Find("30_Next").GetComponent<UnityEngine.UI.Button>();
                int original = (int)Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit");
                fpsNext.onClick.Invoke();
                Assert.IsTrue(((UnityEngine.UI.Button)Get(view, "ApplyButton")).interactable);
                Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit"));
                Assert.AreEqual(originalHide, Static("CurtainCall.StartLobby.ClientSettings", "HideNearbyCharacter"));
                yield return MenuEscape();
                Assert.IsFalse((bool)Get(owner, "IsOpen"));
                Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit"));
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                Call(view, "ShowPage", 1); yield return null;
                fpsNext = SettingsRow(view, 1, "FrameRate").Find("30_Next").GetComponent<UnityEngine.UI.Button>();
                SettingsToggle(view, "HideNearbyCharacter").isOn = !originalHide;
                fpsNext.onClick.Invoke();
                string shown = SettingsRow(view, 1, "FrameRate").Find("20_ValueTMP").GetComponent<TMP_Text>().text;
                int expected = shown == "제한 없음" ? -1 : int.Parse(shown.Split(' ')[0]);
                Call(view, "RequestApply"); yield return null;
                Assert.AreEqual(expected, Static("CurtainCall.StartLobby.ClientSettings", "FrameRateLimit"));
                Assert.AreEqual(!originalHide, Static("CurtainCall.StartLobby.ClientSettings", "HideNearbyCharacter"));
                Assert.AreEqual(expected, Application.targetFrameRate);
                Assert.AreEqual(0, QualitySettings.vSyncCount);
                Assert.IsFalse((bool)Get(settings, "IsExtensionOpen"));
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                Call(view, "ShowPage", 1); yield return null;
                Assert.AreEqual(!originalHide, SettingsToggle(view, "HideNearbyCharacter").isOn);
                Assert.AreEqual(shown, SettingsRow(view, 1, "FrameRate").Find("20_ValueTMP").GetComponent<TMP_Text>().text);
                for (int i = 0; i < 3; i++)
                {
                    var resolution = SettingsRow(view, 1, "Resolution").Find("30_Next").GetComponent<UnityEngine.UI.Button>();
                    var value = SettingsRow(view, 1, "ScreenMode").Find("20_ValueTMP").GetComponent<TMP_Text>().text;
                    Assert.AreEqual(value != "전체 화면 창", resolution.interactable);
                    SettingsRow(view, 1, "ScreenMode").Find("30_Next").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    yield return null;
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
                var labels = SettingsTabs(view).Select(t => t.GetComponentInChildren<TMP_Text>().text).ToArray();
                CollectionAssert.AreEqual(new[] { "게임", "그래픽", "조작", "사운드", "기타" }, labels);
                Assert.IsFalse((bool)Call(view, "IsPageEnabled", 0));
                Call(view, "ShowPage", 3); yield return null;
                float original = (float)Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume");
                SettingsSlider(view, "bgmVolume").value = .31f; SettingsSlider(view, "sfxVolume").value = .42f;
                Call(view, "ShowPage", 1); yield return null; Call(view, "ShowPage", 3); yield return null;
                Assert.AreEqual(.31f, SettingsSlider(view, "bgmVolume").value);
                Assert.AreEqual(1, SettingsTabs(view).Count(t => t.isOn));
                foreach (var tab in SettingsTabs(view))
                {
                    var visual = tab.GetComponent(T("CurtainCall.UI.SettingsV2.CurtainCallTabVisual"));
                    var line = (UnityEngine.UI.Image)visual.GetType().GetField("selectedLine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual);
                    Assert.AreEqual(tab.isOn ? 1f : 0f, line.color.a, "선택 강조는 현재 탭 한 곳에만 표시합니다.");
                }
                Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume"));
                Assert.IsNotNull(SettingsRow(view, 3, "InputDevice")); Assert.IsNotNull(SettingsRow(view, 3, "OutputDevice"));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-tabs-sound.png")); yield return null;
                Call(view, "ShowPage", 2); yield return null;
                Assert.IsNull(SettingsContent(view).Find("bgmVolume_Row"), "다른 탭 컨트롤은 동시에 존재하지 않습니다.");
                var binding = SettingsContent(view).GetComponentsInChildren<UnityEngine.UI.Button>()
                    .First(b => b.name == "10_Rebind" && Guid.TryParse(b.transform.parent.name.Replace("_Row", ""), out _));
                string keyBefore = binding.GetComponentInChildren<TMP_Text>().text;
                binding.onClick.Invoke(); yield return null;
                Assert.IsTrue((bool)Get(settings, "IsCapturingBinding"));
                CursorTestKey("F10"); yield return null; yield return null; CursorTestKey(null); yield return null;
                Assert.IsFalse((bool)Get(settings, "IsCapturingBinding"));
                Assert.AreNotEqual(keyBefore, binding.GetComponentInChildren<TMP_Text>().text);
                binding.onClick.Invoke(); yield return MenuEscape();
                Assert.IsFalse((bool)Get(settings, "IsCapturingBinding")); Assert.IsTrue((bool)Get(view, "IsVisible"));
                string bindingId = binding.transform.parent.name.Replace("_Row", "");
                string savedBindings = PlayerPrefs.GetString("CurtainCall.Input.CommonBindings.v1", "");
                TabReset(view, "controls").onClick.Invoke(); yield return null;
                var preferences = T("CurtainCall.Player.InputBindingPreferences");
                var defaultDraft = (UnityEngine.Object)preferences.GetMethod("CreateDefaultDraft").Invoke(null, null);
                try
                {
                    var defaults = ((IEnumerable)preferences.GetMethod("GetBindings").Invoke(null, new object[] { defaultDraft })).Cast<object>();
                    var expected = defaults.Single(b => Field(b, "BindingId").ToString() == bindingId);
                    var resetBinding = SettingsContent(view).Find(bindingId + "_Row/10_Rebind").GetComponent<UnityEngine.UI.Button>();
                    Assert.AreEqual(Field(expected, "DisplayName"), resetBinding.GetComponentInChildren<TMP_Text>().text);
                }
                finally { UnityEngine.Object.Destroy(defaultDraft); }
                Assert.AreEqual(savedBindings, PlayerPrefs.GetString("CurtainCall.Input.CommonBindings.v1", ""), "키 초기화도 적용 전에는 저장하지 않습니다.");
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-tabs-controls.png")); yield return null;
                Call(view, "ShowPage", 4); yield return null;
                var license = SettingsParagraph(view, "PretendardLicense");
                StringAssert.Contains("SIL OPEN FONT LICENSE", license.text);
                StringAssert.Contains("MoHoDu", SettingsParagraph(view, "Developers").text);
                var scroll = license.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-tabs-credits.png")); yield return null;
                yield return MenuEscape(); Assert.AreEqual(original, Static("CurtainCall.StartLobby.ClientSettings", "BgmVolume"));
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                Call(view, "ShowPage", 3); yield return null; SettingsSlider(view, "bgmVolume").value = .31f; SettingsSlider(view, "sfxVolume").value = .42f;
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
            yield return IsolatedMenuHost();
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase"); yield return MenuReady();
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var run = Find("CurtainCall.Campaign.CampaignRun");
            Assert.IsTrue((bool)Get(run, "CanAbandonRun"));
            int generation = (int)Field(State, "Generation");
            var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
            yield return MenuEscape();
            Call(Get(owner, "Menu"), "RequestSettings"); yield return new WaitForSecondsRealtime(.25f);
            var settings = Get(owner, "Settings");
            Assert.IsFalse(TabReset(Get(settings, "Main"), "game").interactable);
            Assert.IsFalse((bool)Get(settings, "IsExtensionOpen"));
            Call(settings, "RequestAbandon"); yield return null;
            Assert.IsTrue((bool)Get(settings, "IsExtensionOpen"));
            var confirmationTitle = ((Component)Get(settings, "Main")).transform
                .Find("00_Visuals/90_ActionConfirmation/00_Dialog/40_Content/00_TitleTMP").GetComponent<TMP_Text>();
            Assert.IsTrue(confirmationTitle.font.HasCharacters(confirmationTitle.text), "확인창 제목의 모든 글리프가 있어야 합니다.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/settings-upgrade-abandon.png"));
            yield return null;
            Assert.IsTrue((bool)Get(Get(settings, "Main"), "IsConfirmationOpen"));
            SettingsConfirmationButton(Get(settings, "Main"), "20_Back").onClick.Invoke(); yield return null;
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
