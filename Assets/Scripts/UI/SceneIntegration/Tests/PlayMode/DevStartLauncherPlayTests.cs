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
    public sealed class DevStartLauncherPlayTests
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
