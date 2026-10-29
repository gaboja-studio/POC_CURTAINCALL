using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    /// <summary>빈 시험 공간에서 시작/실패/복구/종료만 검증한다. 레벨 이동·발사 입력은 주입하지 않는다.</summary>
    public sealed class DevelopmentSessionTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject fixture, floor;
        Component session, practice;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object value, string name) => value.GetType().GetProperty(name, Flags)?.GetValue(value)
            ?? value.GetType().GetField(name, Flags)?.GetValue(value);
        static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
        static object Local => T("CurtainCall.Network.PlayerSync.NetworkPlayer").GetProperty("Local").GetValue(null);

        IEnumerator Until(Func<bool> ready)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(ready(), "Timed out waiting for development state");
        }

        [UnityTest]
        public IEnumerator SoloStartRecoversDeathsAndEndWithoutRoomsAndReleasesSession()
        {
            session = (Component)T("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            int minimumBefore = (int)Get(session, "MinPlayers");
            var originalApproval = session.GetComponent<NetworkManager>().ConnectionApprovalCallback;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Development session test floor";
            floor.transform.position = new Vector3(0, -1, 0); floor.transform.localScale = new Vector3(200, 1, 200);
            fixture = new GameObject("Isolated development session fixture");
            practice = fixture.AddComponent(T("CurtainCall.EscapeRoute.EscapeDevelopmentSession"));
            yield return Until(() => (bool)Get(practice, "Ready"));
            var manager = session.GetComponent<NetworkManager>();
            Assert.AreEqual(1, manager.ConnectedClientsIds.Count);
            Assert.AreEqual("Playing", Get(session, "GameState").ToString());
            Assert.AreEqual("EscapeLocalPracticeConnector", Get(session, "ActiveConnector").GetType().Name);
            Assert.IsNull(Get(session, "JoinKey"));
            var transport = manager.GetComponent<UnityTransport>();
            Assert.AreEqual("127.0.0.1", transport.ConnectionData.Address);
            Assert.AreEqual("127.0.0.1", transport.ConnectionData.ServerListenAddress);
            Assert.AreEqual(0, transport.ConnectionData.Port);
            var rejected = new NetworkManager.ConnectionApprovalResponse();
            manager.ConnectionApprovalCallback(new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 99 }, rejected);
            Assert.IsFalse(rejected.Approved);
            Assert.IsFalse((bool)((System.Threading.Tasks.Task<bool>)Call(Get(session, "ActiveConnector"), "JoinAsync", "unused")).Result);
            Assert.IsNull(T("CurtainCall.Campaign.CampaignRun").GetProperty("Instance").GetValue(null));
            Assert.IsEmpty(Object.FindObjectsByType(T("CurtainCall.Network.Session.NetworkSessionTestUI"), FindObjectsSortMode.None));
            var player = (Component)Local;
            // 실제 입력은 시험하지 않는다. 공용 플레이어의 상태·복구 이벤트만 시험한다.
            foreach (string name in new[] { "CurtainCall.Player.PlayerInputReader", "CurtainCall.Player.PlayerController" })
                ((Behaviour)player.GetComponent(T(name))).enabled = false;
            var stateType = T("CurtainCall.Network.PlayerSync.PlayerState");
            foreach (string state in new[] { "Fallen", "Retired" })
            {
                int before = (int)Get(practice, "RecoveryCount");
                Call(player, "ServerSetState", Enum.Parse(stateType, state));
                Assert.AreEqual(state, Get(player, "State").ToString());
                yield return Until(() => (int)Get(practice, "RecoveryCount") > before);
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.AreEqual("None", Get(player, "LostParts").ToString());
                Assert.AreEqual("Playing", Get(session, "GameState").ToString());
                Assert.AreSame(player, Local);
            }
            Assert.IsTrue((bool)Call(session, "EndGame"));
            yield return Until(() => Get(session, "GameState").ToString() == "Playing");
            Assert.IsTrue((bool)Call(practice, "Restart"));
            Assert.AreSame(player, Local);
            ((Behaviour)practice).enabled = false;
            yield return Until(() => !manager.IsListening && !manager.ShutdownInProgress);
            Assert.AreEqual(minimumBefore, Get(session, "MinPlayers"));
            Assert.AreEqual(originalApproval, manager.ConnectionApprovalCallback);
            Assert.IsFalse((bool)Get(practice, "Ready"));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (fixture != null) Object.Destroy(fixture);
            if (floor != null) Object.Destroy(floor);
            yield return null;
            if (session != null)
            {
                var manager = session.GetComponent<NetworkManager>();
                if (manager != null && manager.IsListening) manager.Shutdown();
                double deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (manager != null && manager.ShutdownInProgress && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (session != null) Object.Destroy(session.gameObject);
            }
            yield return null;
        }
    }
}
