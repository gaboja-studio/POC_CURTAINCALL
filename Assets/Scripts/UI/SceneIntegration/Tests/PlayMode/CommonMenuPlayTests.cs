using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        IEnumerator IsolatedMenuHost()
        {
            var session = (Component)T("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            yield return null;
            // 다른 worktree의 기본 LAN 포트와 충돌하지 않도록 OS가 빈 포트를 고르게 합니다.
            var hosting = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", (ushort)0);
            while (!hosting.IsCompleted) yield return null;
            Assert.IsTrue(hosting.Result);
        }
        IEnumerator MenuReady()
        {
            float until = Time.realtimeSinceStartup + 15;
            while (Find("CurtainCall.UI.SceneIntegration.CommonMenuUI") == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsNotNull(Find("CurtainCall.UI.SceneIntegration.CommonMenuUI"));
            yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator MenuEscape()
        {
            CursorTestKey("Escape"); yield return null; yield return null;
            CursorTestKey(null); yield return new WaitForSecondsRealtime(.25f);
        }
        [UnityTest]
        public IEnumerator CommonMenu_StartSettings_LobbyEscapeAndHostLeave()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return SceneManager.LoadSceneAsync("Start"); yield return MenuReady();
            var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
            var start = Find("CurtainCall.StartLobby.StartMenu");
            Assert.IsNull(start.GetType().GetField("settingsPage").GetValue(start));
            Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
            yield return null; yield return new WaitForSecondsRealtime(.25f);
            var settings = Get(owner, "Settings"); var view = Get(settings, "Main");
            Assert.IsTrue((bool)Get(view, "IsVisible"));
            Assert.AreEqual("CurtainCall.UI.SettingsV2.SettingsPanelView", view.GetType().FullName);
            Assert.IsFalse((bool)Call(view, "IsPageEnabled", 0));
            Assert.IsFalse(((UnityEngine.UI.Button)Get(view, "ApplyButton")).interactable);
            var prefs = new[] { "CurtainCall.Settings.Volume", "CurtainCall.Settings.Fullscreen", "CurtainCall.Voice.MicVolume", "CurtainCall.Voice.TeamVolume", "CurtainCall.Voice.MicMuted", "CurtainCall.Voice.TeamMuted" };
            bool[] existed = prefs.Select(PlayerPrefs.HasKey).ToArray();
            float volume = (float)Static("CurtainCall.StartLobby.ClientSettings", "Volume");
            var graphicsBefore = CaptureGraphics();
            var extraBefore = CaptureSettingsExtras();
            int previousTarget = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            var voice = Find("CurtainCall.Voice.VoiceChat");
            float mic = (float)Get(voice, "MicVolume"), team = (float)Get(voice, "TeamVolume");
            bool micMute = (bool)Get(voice, "IsMicMuted"), teamMute = (bool)Get(voice, "IsTeamMuted");
            try
            {
                Call(view, "ShowPage", 3); yield return null;
                var slider = SettingsSlider(view, "volume");
                slider.value = .23f;
                Assert.AreEqual(volume, Static("CurtainCall.StartLobby.ClientSettings", "Volume"), "Draft must not apply live.");
                yield return MenuEscape();
                Assert.AreEqual("Main", Get(start, "Current").ToString());
                Assert.IsFalse((bool)Get(owner, "IsOpen"));
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                yield return new WaitForSecondsRealtime(.25f);
                Call(view, "ShowPage", 3); yield return null;
                slider = SettingsSlider(view, "micVolume"); slider.value = .47f;
                SettingsToggle(view, "micEnabled").isOn = micMute;
                Assert.IsNull(SettingsContent(view).Find("FrameRate_Row"));
                Assert.IsTrue(((UnityEngine.UI.Button)Get(view, "ApplyButton")).interactable);
                yield return new WaitForSecondsRealtime(.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/c14-microphone.png")); yield return null;
                Call(view, "RequestApply"); yield return new WaitForSecondsRealtime(.25f);
                Assert.AreEqual(.47f, Get(voice, "MicVolume")); Assert.AreEqual(!micMute, Get(voice, "IsMicMuted"));
                Assert.AreEqual(.47f, PlayerPrefs.GetFloat(prefs[2]));
                Assert.AreEqual("Main", Get(start, "Current").ToString());
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                yield return new WaitForSecondsRealtime(.2f);
                Call(view, "ShowPage", 3); yield return null;
                SettingsSlider(view, "teamVolume").value = .36f;
                SettingsToggle(view, "teamMuted").isOn = !teamMute;
                yield return new WaitForSecondsRealtime(.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/c14-team.png")); yield return null;
                Call(view, "RequestApply"); yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(.36f, Get(voice, "TeamVolume")); Assert.AreEqual(!teamMute, Get(voice, "IsTeamMuted"));
                Call(voice, "SetParticipantPreference", "local-test-peer", .42f, true);
                Assert.AreEqual(.42f, Call(voice, "ParticipantVolume", "local-test-peer"));
                Assert.AreEqual(true, Call(voice, "ParticipantMuted", "local-test-peer"));
                Assert.AreEqual(1f, Call(voice, "ParticipantVolume", "other-peer"), "Do not reuse settings by seat/display name.");
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                yield return new WaitForSecondsRealtime(.2f);
                Call(view, "ShowPage", 3); yield return null;
                SettingsSlider(view, "volume").value = .23f;
                Call(view, "RequestApply"); yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(.23f, Static("CurtainCall.StartLobby.ClientSettings", "Volume"));
                Assert.AreEqual(.23f, AudioListener.volume);
                var session = Find("CurtainCall.Network.Session.NetworkSessionManager");
                var host = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", (ushort)17792);
                while (!host.IsCompleted) yield return null; Assert.IsTrue(host.Result);
                Assert.IsTrue((bool)Call(Find("CurtainCall.StartLobby.StartLobbyFlow"), "OpenLobbyAsHost"));
                float until = Time.realtimeSinceStartup + 25;
                while ((!SceneManager.GetSceneByName("Lobby").isLoaded || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")) && Time.realtimeSinceStartup < until) yield return null;
                yield return MenuReady(); owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                var menu = Get(owner, "Menu");
                Assert.IsTrue(Camera.allCameras.Any(c => c.gameObject.scene.name == "Lobby"));
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType(T("UnityEngine.EventSystems.EventSystem"), FindObjectsSortMode.None).Length);
                yield return MenuEscape(); Assert.IsTrue((bool)Get(menu, "IsMenuOpen"));
                Assert.IsTrue(Cursor.visible); Assert.IsTrue((bool)Static("CurtainCall.Player.InputGate", "IsRequested"));
                Call(menu, "RequestLeave"); yield return new WaitForSecondsRealtime(.2f);
                Assert.IsTrue((bool)Get(menu, "IsConfirmationOpen"));
                yield return MenuEscape(); Assert.IsTrue((bool)Get(menu, "IsMenuOpen")); Assert.IsFalse((bool)Get(menu, "IsConfirmationOpen"));
                Call(menu, "RequestSettings"); yield return new WaitForSecondsRealtime(.2f);
                yield return MenuEscape(); Assert.IsTrue((bool)Get(menu, "IsMenuOpen"));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/c13-lobby.png")); yield return null;
                Call(menu, "RequestLeave"); Call(menu, "ConfirmLeave");
                until = Time.realtimeSinceStartup + 25;
                while ((!SceneManager.GetSceneByName("Start").isLoaded || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")) && Time.realtimeSinceStartup < until) yield return null;
                yield return MenuReady();
                Assert.IsTrue(SceneManager.GetSceneByName("Start").isLoaded);
                Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsRequested"));
            }
            finally
            {
                T("CurtainCall.StartLobby.ClientSettings").GetProperty("Volume").SetValue(null, volume);
                RestoreGraphics(graphicsBefore); extraBefore();
                Application.targetFrameRate = previousTarget; QualitySettings.vSyncCount = previousVSync;
                if (voice != null) Call(voice, "ApplyAudioPreferences", mic, micMute, team, teamMute);
                for (int i = 0; i < prefs.Length; i++) if (!existed[i]) PlayerPrefs.DeleteKey(prefs[i]);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator CommonMenu_ActualSpaces_BlockInputWithoutPausing()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return IsolatedMenuHost();
            foreach (var scene in new[] { "WaitingRoomBase", "Tightrope", "HumanCannon" })
            {
                yield return Launch(2, scene == "WaitingRoomBase" ? "Morning" : "Performance", 0,
                    scene == "HumanCannon" ? "HumanCannon" : "Tightrope", scene);
                yield return MenuReady();
                var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI"); var menu = Get(owner, "Menu");
                yield return MenuEscape();
                Assert.IsTrue((bool)Get(menu, "IsMenuOpen"), scene);
                Assert.IsTrue((bool)Static("CurtainCall.Player.InputGate", "IsRequested"));
                Assert.IsTrue(Cursor.visible); Assert.AreEqual(1, Time.timeScale);
                float before = Time.time; int frame = Time.frameCount;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.Greater(Time.time, before); Assert.Greater(Time.frameCount, frame);
                Call(menu, "RequestSettings"); yield return new WaitForSecondsRealtime(.2f);
                yield return MenuEscape(); Assert.IsTrue((bool)Get(menu, "IsMenuOpen"));
                yield return MenuEscape(); Assert.IsFalse((bool)Get(owner, "IsOpen"));
                yield return CursorState(false);
                Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsRequested"));
                var hud = Find("CurtainCall.UI.SceneIntegration.VoiceStatusUI"); Assert.IsNotNull(hud);
                Assert.IsTrue(hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text == "M"));
            }
        }
    }
}
