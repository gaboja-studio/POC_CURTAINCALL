using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator W4_ShelfIgnoresOtherFloorAndItemsUnderShelfArePickable()
        {
            // 2026-10-31: 지하 관리실 문 앞 F가 바로 위층 소품실 선반에 보관되던 문제, 선반 밑에 버린 물건을 못 줍던 문제.
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            ulong me = (ulong)Get(carry, "LocalClientId");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var shelf = Find("CurtainCall.WaitingRoom.HandoverShelfView");
            var door = GameObject.Find("WaitingRoom_LayoutA/B1_Office/Exploration_A/management-door").transform;

            // 1) 관리실 문 앞(지하)에서는 위층 선반이 잡히지 않는다.
            Call(mover, "SetStartPose", door.position + new Vector3(.9f, .1f, 0f), Quaternion.Euler(0, 270, 0));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.Less((float)Call(shelf, "Reach", player.transform.position), 0f, "다른 층 선반은 닿지 않는다");
            Assert.AreNotSame(shelf, Static("CurtainCall.WaitingRoom.ShopReach", "Focused"));

            // 2) 선반 밑(선반 몸체 안 바닥)에 놓인 물건도 선반 앞에서 줍는다.
            var root = (Transform)Get(shelf, "VisualRoot");
            var frame = root.GetComponent<Collider>().bounds;
            var front = root.forward; var stand = new Vector3(frame.center.x, frame.min.y + .1f, frame.center.z) + front * (Vector3.Dot(frame.extents, new Vector3(Mathf.Abs(front.x), 0, Mathf.Abs(front.z))) + .55f);
            Call(mover, "SetStartPose", stand, Quaternion.LookRotation(-front));
            yield return new WaitForSecondsRealtime(.3f);
            var under = TestHairpin(980001);
            Assert.AreEqual("None", Call(carry, "PlaceFound", under, new Vector3(frame.center.x, frame.min.y, frame.center.z)).ToString());
            yield return null; yield return null;
            Assert.AreEqual(true, Static2("CurtainCall.WaitingRoom.FloorPropsView", "CanPickUp", 980001, player.transform.position), "선반 몸체 안 바닥 물건도 앞에서 닿는다");
            Call(carry, "RequestPickUp", 980001); yield return null;
            Assert.IsTrue(((IEnumerable)Call(carry, "Held", me)).Cast<object>().Any(o => (int)Get(o, "Uid") == 980001));

            // 3) 선반을 바라보고 버리면 선반 안으로 들어가지 않고 다시 주울 수 있다.
            Call(carry, "RequestDrop"); yield return null; yield return null;
            Assert.IsFalse(((IEnumerable)Call(carry, "Held", me)).Cast<object>().Any(o => (int)Get(o, "Uid") == 980001), "버렸다");
            Assert.AreEqual(true, Static2("CurtainCall.WaitingRoom.FloorPropsView", "CanPickUp", 980001, player.transform.position), "버린 자리에서 다시 줍는다");
            Call(carry, "RequestPickUp", 980001); yield return null;
            Assert.IsTrue(((IEnumerable)Call(carry, "Held", me)).Cast<object>().Any(o => (int)Get(o, "Uid") == 980001));
        }

        static object TestHairpin(int uid) => System.Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), uid, "magician-hairpin", "시험 머리핀", 5);
        static object Static2(string type, string method, params object[] args) => T(type).GetMethods().Single(m => m.Name == method && m.IsStatic && m.GetParameters().Length == args.Length).Invoke(null, args);
    }
}
