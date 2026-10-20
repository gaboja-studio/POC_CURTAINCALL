using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods().Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static Component Find(string name) => Object.FindFirstObjectByType(T(name)) as Component;
        static object Static(string type, string prop) => T(type).GetProperty(prop).GetValue(null);
        static FieldInfo DevCache => T("CurtainCall.Dev.DevMode").GetField("_enabled", BindingFlags.NonPublic | BindingFlags.Static);
        object previousDev;
        Component launcher;
        object cursorTestKeyboard;
        Component cursorTestExtraPanel;
        void CursorTestKey(string key)
        {
            var input = T("UnityEngine.InputSystem.InputSystem");
            cursorTestKeyboard ??= input.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) }).Invoke(null, new object[] { "Keyboard", null, null });
            var keys = Array.CreateInstance(T("UnityEngine.InputSystem.Key"), key == null ? 0 : 1);
            if (key != null) keys.SetValue(Enum.Parse(T("UnityEngine.InputSystem.Key"), key), 0);
            var stateType = T("UnityEngine.InputSystem.LowLevel.KeyboardState");
            var state = Activator.CreateInstance(stateType, new object[] { keys });
            input.GetMethods().Single(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition).MakeGenericMethod(stateType)
                .Invoke(null, new[] { cursorTestKeyboard, state, -1d });
        }
        IEnumerator CursorState(bool show)
        {
            float until = Time.realtimeSinceStartup + 3;
            while ((Cursor.visible != show || Cursor.lockState != (show ? CursorLockMode.None : CursorLockMode.Locked)) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(show, Cursor.visible, "Cursor visibility must follow UI lifetime.");
            Assert.AreEqual(show ? CursorLockMode.None : CursorLockMode.Locked, Cursor.lockState);
        }
        object Point(int day, string phase, int slot, string trick) => Activator.CreateInstance(T("CurtainCall.Campaign.RunStartPoint"), day, Enum.Parse(T("CurtainCall.Campaign.RunStartPhase"), phase), slot, trick);
        object State => Static("CurtainCall.Campaign.CampaignRun", "Instance").GetType().GetProperty("State").GetValue(Static("CurtainCall.Campaign.CampaignRun", "Instance"));
        [UnitySetUp]
        public IEnumerator Open()
        {
            previousDev = DevCache.GetValue(null);
            DevCache.SetValue(null, true);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            launcher = (Component)T("CurtainCall.Dev.DevStartLauncher").GetMethod("EnsureExists").Invoke(null, null);
            yield return null;
        }
        IEnumerator Launch(int day, string phase, int slot, string trick, string scene)
        {
            Assert.IsTrue((bool)Call(launcher, "TryLaunch", Point(day, phase, slot, trick)), (string)Get(launcher, "Status"));
            float until = Time.realtimeSinceStartup + 75;
            while ((bool)Get(launcher, "IsBusy") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse((bool)Get(launcher, "IsBusy"));
            Assert.IsTrue((bool)Get(launcher, "LastLaunchSucceeded"), (string)Get(launcher, "Status"));
            Assert.IsTrue(SceneManager.GetSceneByName(scene).isLoaded);
            Assert.AreEqual(day, Field(State, "Day"));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.IsFalse((bool)Get(launcher, "IsOpen"));
            Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"));
            Assert.AreEqual(1, Object.FindObjectsByType(T(scene == "WaitingRoomBase" ? "CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI" : "CurtainCall.UI.SceneIntegration.CommonPerformanceUI"), FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType(T("CurtainCall.UI.SceneIntegration.CommonInteractionUI"), FindObjectsSortMode.None).Length);
        }
        [UnityTest]
        public IEnumerator WaitingRoomPanels_AutomaticallyReleaseAndRestoreCursor()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            CursorTestKey(null);
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var director = Find("CurtainCall.Cameras.CameraViewDirector");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(true, Get(director, "UsesMouseLook"));
            yield return CursorState(false);
            var phone = Find("CurtainCall.WaitingRoom.ShopPhone");
            var schedule = Find("CurtainCall.WaitingRoom.ScheduleBoardView");
            foreach (var panel in new[] { phone, schedule })
            {
                Vector3 point = (Vector3)Get(panel, "PresentationPoint");
                Call(mover, "SetStartPose", new Vector3(point.x + (panel == phone ? 1f : 0), .1f, point.z), Quaternion.identity);
                yield return new WaitForSecondsRealtime(.1f);
                Call(panel, "Interact");
                yield return CursorState(true);
                Assert.AreEqual(true, Static("CurtainCall.Player.InputGate", "IsRequested"));
                CursorTestKey("Escape"); yield return null; yield return null;
                CursorTestKey(null);
                yield return CursorState(false);
                Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));
                // Button-close path, also usable by a future Canvas button.
                Call(panel, "Interact"); yield return CursorState(true);
                panel.GetType().GetMethod("SetOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, new object[] { false });
                yield return CursorState(false);
            }
            // Multiple panels: closing one must not lock the cursor over another open panel.
            cursorTestExtraPanel = new GameObject("CursorTestCanvasPanel").AddComponent(T("CurtainCall.Player.UIInputBlocker"));
            yield return CursorState(true);
            Call(schedule, "Interact");
            Call(schedule, "SetOpen", false);
            yield return null; yield return null;
            Assert.IsTrue(Cursor.visible);
            Object.Destroy(cursorTestExtraPanel.gameObject); cursorTestExtraPanel = null;
            yield return CursorState(false);
            // Outside a UI, the existing manual ESC cursor release must still work.
            CursorTestKey("Escape"); yield return null; yield return null;
            CursorTestKey(null); yield return CursorState(true);
        }

        [UnityTest]
        public IEnumerator DirectHostLaunch_ReentryAndBothPerformances_ThenNightAndFinalMorning()
        {
            yield return Launch(2, "Performance", 1, "HumanCannon", "HumanCannon");
            Assert.AreEqual("HumanCannon", Field(State, "TrickId"));
            Assert.AreEqual(1, Field(State, "PerformanceIndex"));
            Assert.IsTrue((bool)Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance").GetType().GetProperty("IsHost").GetValue(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance")));
            int generation = (int)Field(State, "Generation");
            int oldHandle = SceneManager.GetSceneByName("HumanCannon").handle;
            var player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Call(player, "ServerSetOneArm");
            yield return null;
            yield return Launch(2, "Performance", 1, "HumanCannon", "HumanCannon");
            Assert.Greater((int)Field(State, "Generation"), generation);
            Assert.AreNotEqual(oldHandle, SceneManager.GetSceneByName("HumanCannon").handle, "Same-scene retry must reconstruct the course.");
            player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Assert.AreEqual(0L, Get(Find("CurtainCall.Reward.PerformanceRewards"), "Balance"));
            yield return Launch(3, "Performance", 0, "Tightrope", "Tightrope");
            Assert.AreEqual("Tightrope", Field(State, "TrickId"));
            yield return Launch(3, "Night", 0, "", "WaitingRoomBase");
            Assert.AreEqual("Night", Field(State, "Phase").ToString());
            Assert.AreEqual(0, Field(State, "ReadyCount"));
            yield return Launch(4, "Morning", 0, "", "WaitingRoomBase");
            Assert.IsTrue((bool)Field(State, "IsFinalMorning"));
            Assert.AreEqual("Morning", Field(State, "Rest").ToString());
            Assert.AreEqual(1, Object.FindObjectsByType(T("CurtainCall.Dev.DevStartLauncher"), FindObjectsSortMode.None).Length);
        }
        [UnityTest]
        public IEnumerator PanelBlocksInput_AndInvalidOrDisabledRequestsLeaveStateUntouched()
        {
            Call(launcher, "Toggle");
            Assert.IsTrue((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"));
            Call(launcher, "Toggle");
            yield return new WaitForSecondsRealtime(.1f);
            _ = Static("CurtainCall.Player.InputGate", "IsBlocked");
            yield return null;
            Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"));
            Assert.IsFalse((bool)Call(launcher, "TryLaunch", Point(1, "Morning", 0, "Tightrope")));
            var session = Find("CurtainCall.Network.Session.NetworkSessionManager");
            var connection = session.GetType().GetField("<ConnectionState>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var before = connection.GetValue(session);
            // 권한 거부 분기만 주입 검사한다. 실제 다중 클라이언트 검증을 대체하지 않는다.
            try
            {
                foreach (var name in new[] { "Client", "Connecting" })
                {
                    connection.SetValue(session, Enum.Parse(connection.FieldType, name));
                    Assert.IsFalse((bool)Call(launcher, "TryLaunch", Point(2, "Performance", 0, "Tightrope")));
                }
            }
            finally { connection.SetValue(session, before); }
            DevCache.SetValue(null, false);
            Assert.IsFalse((bool)Call(launcher, "TryLaunch", Point(2, "Performance", 0, "Tightrope")));
            Assert.IsFalse((bool)Get(launcher, "IsBusy"));
            Assert.IsTrue(SceneManager.GetSceneByName("WaitingRoomBase").isLoaded);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (cursorTestKeyboard != null) T("UnityEngine.InputSystem.InputSystem").GetMethod("RemoveDevice").Invoke(null, new[] { cursorTestKeyboard });
            cursorTestKeyboard = null;
            if (cursorTestExtraPanel != null) Object.Destroy(cursorTestExtraPanel.gameObject);
            cursorTestExtraPanel = null;
            var flow = Find("CurtainCall.StartLobby.StartLobbyFlow");
            if (flow != null) Object.Destroy(flow.gameObject);
            yield return null;
            var session = Find("CurtainCall.Network.Session.NetworkSessionManager");
            if (session != null)
            {
                var leaving = (System.Threading.Tasks.Task)Call(session, "LeaveAsync");
                while (!leaving.IsCompleted) yield return null;
            }
            var loader = Find("CurtainCall.UI.SceneIntegration.CommonUiSceneLoader");
            if (loader != null) Object.Destroy(loader.gameObject);
            yield return null;
            var empty = SceneManager.CreateScene("DevStartTestCleanup"); SceneManager.SetActiveScene(empty);
            foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != empty).ToArray())
                if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (string type in new[] { "CurtainCall.WaitingRoom.ScheduleBoard", "CurtainCall.WaitingRoom.WaitingRoomShop", "CurtainCall.WaitingRoom.PropCarry",
                "CurtainCall.Campaign.CampaignRun", "CurtainCall.Reward.PerformanceRewards", "CurtainCall.SpaceLink.SpaceLinkDirector", "CurtainCall.Network.Session.NetworkSessionManager",
                "CurtainCall.UI.SceneIntegration.CommonUiSceneLoader", "CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation", "CurtainCall.Dev.DevStartLauncher", "CurtainCall.Dev.DevModeBadge" })
            {
                var target = Find(type); if (target != null) Object.Destroy(target.gameObject);
            }
            yield return null;
            DevCache.SetValue(null, previousDev);
        }
    }
}
