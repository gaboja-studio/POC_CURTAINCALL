using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        // Task-079 3단계: 작업실 빠루를 주워 화장실 벽을 F 6번에 무너뜨리고, 그때만 사용 횟수가 1 준다.
        [UnityTest]
        public IEnumerator M2_CrowbarBreaksRestroomWallOnSixthHit()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            const string target = "restroom-breach";
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            Assert.IsNotNull(state, "탐색 진행 상태가 있어야 한다");
            var wall = Find("CurtainCall.WaitingRoom.BreakableWall");
            var blocker = (Collider)wall.GetComponent<BoxCollider>();
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            ulong me = (ulong)Get(carry, "LocalClientId");

            // 발견품: 세대마다 작업실 바닥에 빠루 하나.
            object crowbar = null;
            float until = Time.realtimeSinceStartup + 5;
            while (crowbar == null && Time.realtimeSinceStartup < until)
            {
                foreach (var prop in (IEnumerable)Get(carry, "Floor"))
                    if ((string)Get(Get(prop, "Item"), "ItemId") == "stake-puller") crowbar = prop;
                yield return null;
            }
            Assert.IsNotNull(crowbar, "작업실 바닥에 빠루가 놓여 있어야 한다");
            var item = Get(crowbar, "Item");
            int found = (int)Get(item, "Uses");
            Assert.AreEqual(T("CurtainCall.WaitingRoom.ItemCatalog").GetMethod("FoundUses").Invoke(null, new object[] { "stake-puller" }), found, "발견품 빠루는 기물 목록의 발견품 내구도로 시작");

            // 빈손으로 치면 진행이 쌓이지 않는다.
            Stand(mover, wall.transform.position + Vector3.forward * 1.1f, Vector3.back);
            yield return new WaitForSecondsRealtime(.3f);
            Call(wall, "Interact");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(0, Call(state, "Hits", target), "빠루 없이는 칠 수 없다");

            // 줍기(F).
            var at = new Vector3((float)Get(crowbar, "X"), .1f, (float)Get(crowbar, "Z"));
            Stand(mover, at, Vector3.forward);
            yield return new WaitForSecondsRealtime(.3f);
            Call(carry, "RequestPickUp", (int)Get(item, "Uid"));
            yield return null;
            Assert.AreEqual("stake-puller", Get(Call(carry, "Selected", me), "ItemId"), "주운 빠루를 손에 든다");

            // 여섯 번째 타격에 무너진다. 그 전에는 길이 막혀 있고 사용 횟수도 그대로다.
            Stand(mover, wall.transform.position + Vector3.forward * 1.1f, Vector3.back);
            yield return new WaitForSecondsRealtime(.3f);
            for (int i = 1; i <= 5; i++)
            {
                Call(wall, "Interact");
                yield return new WaitForSecondsRealtime(.12f);
                Assert.AreEqual(i, Call(state, "Hits", target));
            }
            Assert.AreEqual(false, Call(state, "IsOpen", target));
            Assert.IsTrue(blocker.enabled, "무너지기 전에는 막혀 있다");
            Assert.AreEqual(found, Get(Call(carry, "Selected", me), "Uses"), "연타 중간에는 사용 횟수가 줄지 않는다");

            Call(wall, "Interact");
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(true, Call(state, "IsOpen", target));
            Assert.IsFalse(blocker.enabled, "무너지면 지나갈 수 있다");
            int left = found - 1; var held = ((IEnumerable)Call(carry, "Held", me)).Cast<object>().Where(o => (string)Get(o, "ItemId") == "stake-puller").ToList();
            if (left > 0) Assert.AreEqual(left, Get(held.Single(), "Uses"), "벽이 무너진 때 한 번만 준다");
            else Assert.IsEmpty(held, "마지막 사용이면 손에서 사라진다");
            Physics.SyncTransforms();
            var centre = wall.transform.position;
            Assert.IsFalse(Physics.CheckCapsule(centre + Vector3.up * .4f, centre + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore), "무너진 자리를 지나갈 수 있다");

        }

        static void Stand(Component mover, Vector3 at, Vector3 facing) =>
            Call(mover, "SetStartPose", new Vector3(at.x, .1f, at.z), Quaternion.LookRotation(facing));
    }
}
