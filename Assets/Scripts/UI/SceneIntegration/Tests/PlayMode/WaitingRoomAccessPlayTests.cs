using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator WaitingRoomDeathShowsOneUpperRightWarning()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            yield return new WaitForSecondsRealtime(.5f);
            var kill = player.GetType().GetMethod("ServerKill");
            var origin=System.Enum.Parse(kill.GetParameters()[0].ParameterType,"Debug");
            Assert.IsTrue((bool)kill.Invoke(player,new object[]{origin,null}));
            yield return null;
            yield return null;
            var manager=(Component)Static("CurtainCall.UI.SceneIntegration.HudUIManager","Current");
            var cards=manager.GetComponentsInChildren(T("CurtainCall.UI.SceneIntegration.HudRoleCard"))
                .Where(c=>Get(c,"Title").ToString().Contains("사망")).ToArray();
            Assert.AreEqual(1,cards.Length);
            Assert.AreEqual(Vector2.one,((RectTransform)cards[0].transform).anchorMin);
            Assert.AreEqual("Warning",Get(cards[0].GetComponent(T("CurtainCall.UI.SceneIntegration.HudEventAppearance")),"Kind").ToString());
            Call(cards[0],"Close",true);
            yield return null;
            yield return null;
            Assert.IsFalse(manager.GetComponentsInChildren(T("CurtainCall.UI.SceneIntegration.HudRoleCard"))
                .Any(c=>Get(c,"Title").ToString().Contains("사망")));
        }

        [UnityTest]
        public IEnumerator M2_Access_BedHairpinEdgeCollectsThroughF()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            yield return new WaitForSecondsRealtime(.8f);
            var layout = GameObject.Find("WaitingRoom_LayoutA").transform;
            var spot = layout.Find("S02_Dormitory/Exploration_A/bed-hairpin").GetComponent(T("CurtainCall.WaitingRoom.FoundItemSpot"));
            var bed = layout.Find("S02_Dormitory/Dormitory/Beds/Bed_B").GetComponent<Collider>().bounds;
            Assert.Less(spot.transform.position.x, bed.min.x);
            Assert.Less(bed.min.x - spot.transform.position.x, .15f, "Still tucked beneath the bed edge");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "SetStartPose", new Vector3(spot.transform.position.x - .6f, .05f, spot.transform.position.z), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.2f); yield return FlowTap();
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var item = Call(spot, "MakeItem", (int)Field(State, "Generation"));
            Assert.AreEqual(Get(item, "Uid"), Get(Call(carry, "Selected", (ulong)Get(carry, "LocalClientId")), "Uid"));
        }
        [UnityTest]
        public IEnumerator M2_Access_StairClosedLeavesLockPointAndKeyFRelease()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var layout = GameObject.Find("WaitingRoom_LayoutA").transform;
            var door = Placed("stairs-door"); var blocker = door.GetComponent<BoxCollider>();
            var assembly = layout.Find("ST_Stairwell/StairwellDoorway");
            foreach (string name in new[] { "LeafHingeL", "LeafHingeR" })
                Assert.Less(Quaternion.Angle(Quaternion.identity, assembly.Find(name).localRotation), .01f);
            var leaves = assembly.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "LockedDoorLeafL" || r.name == "LockedDoorLeafR").ToArray();
            var b = leaves[0].bounds; b.Encapsulate(leaves[1].bounds);
            Assert.Less(b.size.z, .08f, "Closed leaves must form a thin plane across the doorway");
            Assert.Less(Vector3.Distance(b.center, blocker.bounds.center), .01f);
            var point = (Vector3)Get(door, "PresentationPoint");
            Assert.Less(Mathf.Abs(point.z - b.min.z), .1f);
            foreach (var r in layout.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.name == "SteelLedger" && Mathf.Abs(r.bounds.center.y + 2.8f) < .02f && Mathf.Abs(r.bounds.center.z - 101.85f) < .2f))
                Assert.IsTrue(r.bounds.max.x <= 3.5f || r.bounds.min.x >= 12.499f, "No active wall ledger across the stair entrance: " + r.transform.parent.name + " " + r.bounds);
            // 위층 계단 디딤판 위 1.9m 안에 지붕 보가 지나가지 않는다.
            var stairwell = layout.Find("ST_Stairwell");
            foreach (var tread in stairwell.GetComponentsInChildren<Renderer>().Where(r => r.name == "Tread"))
            {
                var head = new Bounds(tread.bounds.center + Vector3.up * 1f, new Vector3(tread.bounds.size.x - .05f, 1.8f, tread.bounds.size.z - .3f));
                Assert.IsFalse(stairwell.GetComponentsInChildren<Renderer>().Any(r => r.name.StartsWith("RoofGirder") && r.bounds.Intersects(head)), "Roof girder over stair tread " + tread.bounds.center);
            }
            NearPlaced(door, 0); yield return new WaitForSecondsRealtime(.2f); yield return FlowTap();
            Assert.IsFalse((bool)Get(door, "Opened")); Assert.IsTrue(blocker.enabled);
            yield return PickPlaced("magician-hairpin", "S10_Workshop");
            for (int i = 0; i < 3 && !(bool)Call(Static("CurtainCall.WaitingRoom.ExplorationState", "Instance"), "IsOpen", "structure-a:locked-box-1"); i++) yield return UsePlaced("locked-box-1");
            yield return UsePlaced("stairs-key");
            if (HoldFor(door)) yield return null; // Task-092: 머리핀을 든 채 얻은 열쇠는 1·2로 꺼내 든다.
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover")); mover.GetType().GetProperty("Simulated").SetValue(mover, true);
            Call(mover, "SetStartPose", new Vector3(b.center.x, .05f, b.min.z - .8f), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.2f); yield return FlowTap(); yield return null;
            Assert.IsTrue((bool)Get(door, "Opened")); Assert.IsFalse(blocker.enabled);
            Assert.IsTrue(leaves.All(r => !r.gameObject.activeInHierarchy), "Opening removes the actual visible closed leaves");
            Assert.IsTrue(new[] { "TransomPanel", "Threshold" }.All(n => assembly.Find(n).gameObject.activeInHierarchy), "Transom and threshold stay after opening");
            Assert.IsFalse(assembly.Find("DoorLockPoint/OpenPadlock").gameObject.activeInHierarchy);
            yield return FlowWalk(player, mover, new Vector3(b.center.x, 0, b.max.z + .55f));
        }
    }
}
