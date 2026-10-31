using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        /// <summary>
        /// Task-090: 게임 설정이 켜져 있으면 그래픽 탭에 '혈흔 연출 표시'가 있고 적용하면 이 PC에 저장된다.
        /// 게임 설정이 꺼져 있으면 항목이 없고 개인 설정과 상관없이 아무에게도 보이지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator SettingsUpgrade_BloodEffectToggle_HiddenAndForcedOffWhenGameDisables()
        {
            var saved = CaptureGraphics(); var restoreExtras = CaptureSettingsExtras();
            var bodyDamage = T("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null).GetType().GetProperty("BodyDamage")
                .GetValue(T("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null));
            var gameSwitch = bodyDamage.GetType().GetField("bloodEffectEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
            object before = gameSwitch.GetValue(bodyDamage);
            var client = T("CurtainCall.StartLobby.ClientSettings").GetProperty("ShowBloodEffect");
            var effects = T("CurtainCall.Damage.InjuryBloodEffects");
            bool Visible() => (bool)effects.GetProperty("IsVisible").GetValue(null);
            try
            {
                gameSwitch.SetValue(bodyDamage, true);
                client.SetValue(null, true);
                yield return SceneManager.LoadSceneAsync("Start"); yield return MenuReady();
                var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                var settings = Get(owner, "Settings");
                var start = Find("CurtainCall.StartLobby.StartMenu");
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                yield return new WaitForSecondsRealtime(.25f);
                var view = Get(settings, "Main");
                Call(view, "ShowPage", 1); yield return null;
                Assert.IsTrue(SettingsToggle(view, "ShowBloodEffect").isOn);
                Assert.IsTrue(Visible());
                SettingsToggle(view, "ShowBloodEffect").isOn = false;
                Assert.IsTrue((bool)client.GetValue(null), "적용 전에는 저장하지 않는다");
                Call(view, "RequestApply"); yield return null;
                Assert.IsFalse((bool)client.GetValue(null));
                Assert.IsFalse(Visible(), "개인 설정을 끄면 이 화면에서 보이지 않는다");
                yield return MenuEscape();

                // 게임 설정에서 끄면 항목이 사라지고, 개인 설정이 켜져 있어도 보이지 않는다.
                gameSwitch.SetValue(bodyDamage, false);
                client.SetValue(null, true);
                Call(start, "Show", Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings"));
                yield return new WaitForSecondsRealtime(.25f);
                Call(view, "ShowPage", 1); yield return null;
                Assert.IsNull(SettingsContent(view).Find("ShowBloodEffect_Row"), "게임 설정이 꺼지면 항목을 표시하지 않는다");
                Assert.IsNotNull(SettingsContent(view).Find("HideNearbyCharacter_Row"));
                Assert.IsFalse(Visible(), "게임 설정이 꺼지면 모두 꺼진다");
                Call(view, "RequestApply"); yield return null;
                Assert.IsTrue((bool)client.GetValue(null), "숨긴 항목은 개인 값을 바꾸지 않는다");
                yield return MenuEscape();
            }
            finally
            {
                gameSwitch.SetValue(bodyDamage, before);
                restoreExtras(); RestoreGraphics(saved);
            }
        }
    }
}
