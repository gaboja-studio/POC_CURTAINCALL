using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator PursuitRoom_FromStart_InventoryAndEscapeMenuSurviveReload()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return SceneManager.LoadSceneAsync("Start");
            yield return IsolatedMenuHost();
            yield return new WaitForSecondsRealtime(.5f);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.IsTrue((bool)Call(launcher, "TryLaunch", Point(2, "Morning", 0, "Tightrope")));
                float until = Time.realtimeSinceStartup + 75;
                while ((bool)Get(launcher, "IsBusy") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Get(launcher, "LastLaunchSucceeded"), (string)Get(launcher, "Status"));
                Assert.IsTrue(SceneManager.GetSceneByName("WaitingRoomPursuit").isLoaded);
                yield return MenuReady();
                Assert.AreEqual("WaitingRoomPursuit", ((Scene)Static("CurtainCall.UI.SceneIntegration.CommonMenuUI", "CurrentScene")).name);
                var inventory = Find("CurtainCall.UI.SceneIntegration.CommonInventoryUI");
                Assert.IsNotNull(inventory);
                Assert.AreEqual(1f, ((CanvasGroup)Field(inventory, "group")).alpha);
                var slots = Field(inventory, "slots");
                Assert.IsNotNull(Call(slots, "GetSlot", 0));
                Assert.IsNotNull(Call(slots, "GetSlot", 1));
                var menu = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                yield return MenuEscape();
                Assert.IsTrue((bool)Get(menu, "IsOpen"), "ESC must open in a copied waiting room.");
                Assert.IsTrue((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"));
                yield return MenuEscape();
                Assert.IsFalse((bool)Get(menu, "IsOpen"));
                Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"));
                Assert.AreEqual(1f, ((CanvasGroup)Field(inventory, "group")).alpha);
            }
        }
    }
}
