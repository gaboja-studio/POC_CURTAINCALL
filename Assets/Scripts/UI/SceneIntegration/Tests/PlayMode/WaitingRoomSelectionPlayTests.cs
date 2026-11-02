using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator WaitingRoomSelection_DevHostLoadsBothScenesWithCommonServices()
        {
            var spaces = Static("CurtainCall.Settings.GameSettings", "Base").GetType().GetProperty("SpaceTransition").GetValue(Static("CurtainCall.Settings.GameSettings", "Base"));
            var field = spaces.GetType().GetField("waitingRoomVariant", BindingFlags.Instance | BindingFlags.NonPublic);
            var previous = field.GetValue(spaces);
            try {
                var session = T("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
                var hosting = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", (ushort)0);
                while (!hosting.IsCompleted) yield return null;
                Assert.IsTrue(hosting.Result);
                foreach (var name in new[] { "Base", "Pursuit" }) {
                    field.SetValue(spaces, Enum.Parse(field.FieldType, name));
                    string sceneName = "WaitingRoom" + name;
                    yield return Launch(2, "Morning", 0, "Tightrope", sceneName);
                    var scene = SceneManager.GetSceneByName(sceneName);
                    Assert.IsTrue((bool)T("CurtainCall.Cameras.CameraViewDirector").GetMethod("UsesWallCollision").Invoke(null, new object[] { scene }));
                    Assert.AreEqual("WAITINGROOM", T("CurtainCall.Telemetry.Pipeline.TelemetryRuntime").GetMethod("SceneType").Invoke(null, new object[] { sceneName }));
                    Assert.IsTrue((bool)Static("CurtainCall.UI.SceneIntegration.CommonMenuUI", "HasSupportedSpace"));
                    Assert.AreEqual(sceneName, ((Scene)Static("CurtainCall.UI.SceneIntegration.CommonMenuUI", "CurrentScene")).name);
                    Assert.AreEqual(name == "Pursuit", (bool)Static("CurtainCall.Settings.WaitingRoomScenes", "PursuitEnabled"));
                }
            } finally { field.SetValue(spaces, previous); }
        }
    }
}