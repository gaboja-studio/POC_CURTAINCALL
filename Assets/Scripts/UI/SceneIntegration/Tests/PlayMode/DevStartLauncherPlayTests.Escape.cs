using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        /// <summary>Task-089: Dev 시작 지점 "탈출로"는 대기실 합류 없이 탈출로 출발점에 세우고, 사망하면 출발점에서 다시 시작한다. F9 치트 순간이동도 확인한다.</summary>
        [UnityTest]
        public IEnumerator EscapeLaunch_PlacesAtEscapeStart_RecoversAfterRetire_AndCheatsTeleport()
        {
            Assert.IsTrue((bool)Call(launcher, "TryLaunch", Point(2, "Escape", 0, "")), (string)Get(launcher, "Status"));
            float until = Time.realtimeSinceStartup + 90;
            while ((bool)Get(launcher, "IsBusy") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Get(launcher, "LastLaunchSucceeded"), (string)Get(launcher, "Status"));
            Assert.IsTrue(SceneManager.GetSceneByName("escape_route").isLoaded);
            Assert.AreEqual("Escape", Field(State, "Phase").ToString());

            var session = Find("CurtainCall.EscapeRoute.EscapeDevelopmentSession");
            Assert.IsNotNull(session, "탈출로 개발 세션");
            until = Time.realtimeSinceStartup + 5;
            while (!((string)Get(session, "Status")).StartsWith("Dev 시작") && Time.realtimeSinceStartup < until) yield return null;
            StringAssert.StartsWith("Dev 시작", (string)Get(session, "Status"));

            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var args = new object[] { (int)Get(player, "Slot"), Get(mover, "LaneSpacing"), null, null };
            T("CurtainCall.Network.PlayerSync.PlayerSpawnPoints").GetMethod("GetPose").Invoke(null, args);
            var spawn = (Vector3)args[2];
            yield return new WaitForSecondsRealtime(.2f);
            Assert.Less(Vector3.Distance(player.transform.position, spawn), 1.5f, "탈출로 출발점에 선다");

            // 사망(탈출로 = 리타이어) 뒤 개발 세션이 출발점에서 다시 시작한다.
            Call(player, "ServerSetState", Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), "Retired"));
            until = Time.realtimeSinceStartup + 5;
            while (Get(player, "State").ToString() != "Normal" && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.GreaterOrEqual((int)Get(session, "RecoveryCount"), 1);

            // F9 치트: 지점 목록과 순간이동.
            var cheats = T("CurtainCall.Dev.DevCheats");
            var pointType = T("CurtainCall.Dev.DevCheats+TeleportPoint");
            var points = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pointType));
            cheats.GetMethod("CollectTeleportPoints").Invoke(null, new object[] { player, points });
            var labels = points.Cast<object>().Select(p => (string)pointType.GetField("Label").GetValue(p)).ToArray();
            CollectionAssert.Contains(labels, "씬 출발점");
            Assert.IsTrue(labels.Any(l => l.StartsWith("대포 ")), "탈출로 대포 탑승 위치: " + string.Join(", ", labels));
            var target = points.Cast<object>().First(p => ((string)pointType.GetField("Label").GetValue(p)).StartsWith("대포 "));
            var position = (Vector3)pointType.GetField("Position").GetValue(target);
            var teleport = new object[] { player, position, Quaternion.identity, null };
            Assert.IsTrue((bool)cheats.GetMethod("Teleport").Invoke(null, teleport), (string)teleport[3]);
            Assert.Less(Vector3.Distance(player.transform.position, position), .5f);

            cheats.GetProperty("SuppressFalls").SetValue(null, true);
            try
            {
                Assert.IsTrue((bool)T("CurtainCall.EscapeRoute.EscapeDevelopmentSession").GetMethod("FallConsequencesSuppressed").Invoke(null, new object[] { mover }));
            }
            finally { cheats.GetProperty("SuppressFalls").SetValue(null, false); }
            Assert.IsNotNull(Object.FindFirstObjectByType(T("CurtainCall.Dev.DevCheatPanel")) ?? T("CurtainCall.Dev.DevCheatPanel").GetMethod("EnsureExists").Invoke(null, null));
        }
    }
}
