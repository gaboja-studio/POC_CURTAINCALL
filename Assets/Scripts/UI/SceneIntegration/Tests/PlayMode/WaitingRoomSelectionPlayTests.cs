using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator WaitingRoomSelection_DevHostLoadsWaitingRoomWithCommonServices()
        {
            var session = T("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            var hosting = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", (ushort)0);
            while (!hosting.IsCompleted) yield return null;
            Assert.IsTrue(hosting.Result);
            const string sceneName = "WaitingRoom";
            yield return Launch(2, "Morning", 0, "Tightrope", sceneName);
            var scene = SceneManager.GetSceneByName(sceneName);
            Assert.IsTrue((bool)T("CurtainCall.Cameras.CameraViewDirector").GetMethod("UsesWallCollision").Invoke(null, new object[] { scene }));
            Assert.AreEqual("WAITINGROOM", T("CurtainCall.Telemetry.Pipeline.TelemetryRuntime").GetMethod("SceneType").Invoke(null, new object[] { sceneName }));
            Assert.IsTrue((bool)Static("CurtainCall.UI.SceneIntegration.CommonMenuUI", "HasSupportedSpace"));
            Assert.AreEqual(sceneName, ((Scene)Static("CurtainCall.UI.SceneIntegration.CommonMenuUI", "CurrentScene")).name);
        }
    }
}
