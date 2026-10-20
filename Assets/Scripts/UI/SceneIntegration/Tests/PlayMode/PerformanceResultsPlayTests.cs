using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator StartLobbyCurtain_KeepsLobbyCameraAndUi_WithAdditiveUi()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Start");
            yield return new WaitForSecondsRealtime(.5f);
            var session = Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance");
            var host = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", (ushort)17791);
            while (!host.IsCompleted) yield return null;
            Assert.IsTrue(host.Result);
            var flow = Find("CurtainCall.StartLobby.StartLobbyFlow");
            Assert.IsTrue((bool)Call(flow, "OpenLobbyAsHost"));
            float until = Time.realtimeSinceStartup + 20;
            while ((!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Lobby").isLoaded
                || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"))
                && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("Lobby").isLoaded);
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("CommonInteractionUI").isLoaded);
            Assert.IsFalse((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"));
            Assert.IsTrue(Camera.allCameras.Any(c => c.gameObject.scene.name == "Lobby"), "Lobby must keep rendering with additive UI loaded.");
            Assert.IsTrue(GameObject.Find("Lobby Canvas") != null, "Lobby UI must remain active.");
            Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"));
            var room = Find("CurtainCall.StartLobby.LobbyRoom");
            var board = Find("CurtainCall.StartLobby.LobbyReadyBoard");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var interaction = Find("CurtainCall.UI.SceneIntegration.CommonInteractionUI");
            CursorTestKey(null);
            Call(mover, "SetStartPose", board.transform.position + new Vector3(0, .1f, -1), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(board, Get(room, "FocusedInteraction"));
            Assert.IsTrue((bool)Get(Get(interaction, "Prompt"), "IsVisible"), "C6 prompt must accompany the lobby F target.");
            for (int i = 0; i < 3; i++)
            {
                CursorTestKey("F"); yield return null; yield return null;
                CursorTestKey(null); yield return null; yield return null;
                Assert.AreEqual(i % 2 == 0, Get(board, "LocalReady"), "F must toggle the actual ready state exactly once.");
            }
            ScreenCapture.CaptureScreenshot(".harness/local/start-lobby-curtain.png");
            yield return null;
            var lever = Find("CurtainCall.StartLobby.LobbyCurtainLever");
            Call(mover, "SetStartPose", lever.transform.position + new Vector3(0, .1f, -1), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(lever, Get(room, "FocusedInteraction"));
            Assert.IsTrue((bool)Get(Get(interaction, "Prompt"), "IsVisible"));
            CursorTestKey("F"); yield return null; yield return null;
            CursorTestKey(null); yield return null; yield return null;
            Assert.AreEqual("Intro", Field(State, "Phase").ToString(), "Ready host must start the campaign with F at the lever.");
            Call(flow, "LeaveToStart");
            until = Time.realtimeSinceStartup + 20;
            while ((!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Start").isLoaded
                || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"))
                && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("Start").isLoaded);
            Assert.IsFalse((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"));
            Assert.IsTrue(Camera.allCameras.Any(c => c.gameObject.scene.name == "Start"));
        }

        [UnityTest]
        public IEnumerator C12_BothActualPerformances_RecordAndDisplay()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            foreach (string trick in new[] { "Tightrope", "HumanCannon" })
            {
                yield return Launch(2, "Performance", 0, trick, trick);
                Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("performance_background").isLoaded,
                    "Stage background must be ready when the curtain opens.");
                var rewards = Find("CurtainCall.Reward.PerformanceRewards");
                float until = Time.realtimeSinceStartup + 10;
                while ((Get(rewards, "State") == null || !(bool)Get(Get(rewards, "State"), "IsOpen")) && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Get(Get(rewards, "State"), "IsOpen"));
                var binding = Find("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding");
                if (trick == "Tightrope")
                    Find("CurtainCall.Tightrope.TightropeRun").GetType().GetMethod("ServerStartPerformance", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(Find("CurtainCall.Tightrope.TightropeRun"), null);
                else
                {
                    var route = Find("CurtainCall.HumanCannon.HumanCannonRouteSystem");
                    var wall = ((Array)Private(binding, "cannonFires")).Cast<Component>().First();
                    Call(wall, "Arm", (double)Get(route, "Clock"));
                }
                yield return new WaitForSecondsRealtime(.3f);
                Assert.IsTrue((bool)Call(rewards, "FinishPerformance", false, "C12Verification", null));
                var result = Get(rewards, "Latest");
                Assert.Greater(Convert.ToDouble(Get(result, "ElapsedSeconds")), 0);
                Assert.Greater(Convert.ToDouble(Get(result, "CourseLength")), 0);
                var people = ((IEnumerable)Get(result, "Participants")).Cast<object>().ToArray();
                Assert.IsNotNull(Get(people[0], "MaximumDistance"));
                var dto = T("CurtainCall.Reward.RewardResultData");
                var wire = dto.GetMethod("From").Invoke(null, new[] { result });
                var copy = Call(JsonUtility.FromJson(JsonUtility.ToJson(wire), dto), "Read");
                Assert.AreEqual(Convert.ToDouble(Get(result, "ElapsedSeconds")), Convert.ToDouble(Get(copy, "ElapsedSeconds")), .000001);
                Assert.AreEqual(Convert.ToDouble(Get(people[0], "MaximumDistance")), Convert.ToDouble(Get(((IEnumerable)Get(copy, "Participants")).Cast<object>().First(), "MaximumDistance")), .000001);
                var ending = Find("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation");
                Assert.IsNotNull(Private(ending, "resultsView"));
                yield return new WaitForSecondsRealtime(1.7f);
                Assert.IsTrue((bool)Get(ending, "ResultVisible"));
                var view = Private(ending, "resultsView");
                Assert.IsTrue((bool)Get(view, "IsVisible"));
                var layers = (Array)Private(view, "memberLayers");
                Assert.AreEqual(1, layers.Cast<object>().Count(l => ((Component)Field(l, "root")).gameObject.activeSelf));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($".harness/local/c12-solo-{trick}.png"));
                bool covered = false;
                until = Time.realtimeSinceStartup + 20;
                while (Time.realtimeSinceStartup < until)
                {
                    covered |= (bool)Get(Get(ending, "Curtain"), "IsFullyCovered");
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "WaitingRoomBase"
                        && !(bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")) break;
                    yield return null;
                }
                Assert.IsTrue(covered, "Curtain must render a fully closed frame before loading.");
                Assert.AreEqual("WaitingRoomBase", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                Assert.IsFalse(UnityEngine.SceneManagement.SceneManager.GetSceneByName("performance_background").isLoaded);
                Assert.IsFalse((bool)Get(Get(ending, "Curtain"), "IsBusy"));
            }
        }
    }
}
