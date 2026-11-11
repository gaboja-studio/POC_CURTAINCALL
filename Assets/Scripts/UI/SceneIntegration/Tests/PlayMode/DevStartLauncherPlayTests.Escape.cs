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
        [UnityTest, Category("SpaceRoleHud")]
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
            Assert.IsNull(VisibleRole("Objective"), "시작 지점에서는 출구 임무가 보이지 않아야 한다");
            var content=Find("CurtainCall.EscapeRoute.EscapeCampaignContent");
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var saw=(Component)content.GetType().GetField("exitSaw",flags).GetValue(content);
            var socket=(Transform)Get(saw,"CableSocket");
            Call(mover,"SetStartPose",socket.TransformPoint(new Vector3(-18,-1.4f,0)),Quaternion.identity);
            until=Time.realtimeSinceStartup+5;
            while (!VisibleRole("Objective") && Time.realtimeSinceStartup<until) yield return null;
            Assert.That(Get(VisibleRole("Objective"),"Title"),Is.EqualTo("탈출구 열기"));
            Call(mover,"SetStartPose",spawn,Quaternion.identity); yield return null;
            Assert.That(Get(VisibleRole("Objective"),"Title"),Is.EqualTo("탈출구 열기"));
            saw.GetType().GetField("cutMask",flags).SetValue(saw,(uint)saw.GetType().GetProperty("RequiredMask",flags).GetValue(saw));
            yield return null; yield return null;
            Assert.That(Get(VisibleRole("Objective"),"Title"),Is.EqualTo("갈대밭 도착"));

            // 사망(탈출로 = 리타이어) 뒤 개발 세션이 출발점에서 다시 시작한다.
            var setState=player.GetType().GetMethod("ServerSetState");
            setState.Invoke(player,new[]{Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"),"Retired"),Type.Missing});
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
            var ending=Find("CurtainCall.EscapeRoute.EscapeEndingPresentation");
            ending.GetType().GetMethod("ShowResult",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ending,null);
            var result=VisibleRole("Outcome");
            Assert.IsNotNull(result);
            Assert.That(Get(result,"Title"),Is.EqualTo("탈출 성공"));
            Assert.IsTrue(result.GetComponentInChildren<UnityEngine.UI.Button>().IsInteractable());
        }
    }
}
