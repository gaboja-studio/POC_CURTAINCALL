using System;
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
        public IEnumerator M2_Review_DebugSuppliesTwelveUniqueAndFCollectible()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            yield return new WaitForSecondsRealtime(1);
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var spots = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.FoundItemSpot"), FindObjectsSortMode.None).Cast<Component>().ToArray();
            var debug = spots.Where(s => ((string)Get(s, "SpotId")).StartsWith("debug-lobby-")).ToArray();
            Assert.AreEqual(12, debug.Length);
            int generation = (int)Field(State, "Generation");
            Assert.AreEqual(spots.Length, spots.Select(s => Get(Call(s, "MakeItem", generation), "Uid")).Distinct().Count());
            foreach (var group in debug.GroupBy(s => (string)Get(Call(s, "MakeItem", generation), "ItemId")))
            {
                Assert.AreEqual(4, group.Count());
                foreach (var spot in group)
                {
                    var item = Call(spot, "MakeItem", generation);
                    Assert.AreEqual(true, Call(carry, "Exists", Get(item, "Uid")));
                    // 디버그 보급품은 새 제품 내구도(머리핀5·말뚝뽑개4·UV등3). 정식 발견품 수치는 그대로.
                    Assert.AreEqual(T("CurtainCall.WaitingRoom.ItemCatalog").GetMethod("NewUses").Invoke(null, new object[] { group.Key }), Get(item, "Uses"));
                }
            }
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            foreach (string kind in new[] { "stake-puller", "magician-hairpin", "uv-lamp" })
            {
                var spot = debug.Single(s => (string)Get(s, "SpotId") == "debug-lobby-" + kind + "-1");
                var item = Call(spot, "MakeItem", generation);
                Call(mover, "SetStartPose", spot.transform.position + new Vector3(-.55f, .05f, 0), Quaternion.identity);
                yield return new WaitForSecondsRealtime(.2f); yield return FlowTap();
                var held = Call(carry, "Selected", (ulong)Get(carry, "LocalClientId"));
                Assert.AreEqual(Get(item, "Uid"), Get(held, "Uid"), "Real F should collect the debug item");
                Call(mover, "SetStartPose", new Vector3(-10f, .05f, 73f), Quaternion.identity);
                yield return null; Call(carry, "RequestDrop"); yield return new WaitForSecondsRealtime(.2f);
            }
        }
        [UnityTest]
        public IEnumerator M2_Review_PropDoorwayVisualFloorAndInputWalkBothWays()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var patch = GameObject.Find("WaitingRoom_LayoutA").transform.Find("S04_PropRoom/Shell/DoorwayFloorInfill");
            Assert.IsNotNull(patch); Assert.IsNull(patch.GetComponent<Collider>(), "Reuse existing continuous collision");
            var bounds = patch.GetComponent<Renderer>().bounds;
            Assert.LessOrEqual(bounds.min.z, 95.401f); Assert.GreaterOrEqual(bounds.max.z, 95.899f);
            Assert.AreEqual(0f, bounds.max.y, .005f);
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "SetStartPose", new Vector3(-8.2f, .05f, 94.8f), Quaternion.identity);
            yield return null;
            yield return FlowWalk(player, mover, new Vector3(-8.2f, 0, 96.6f));
            yield return FlowWalk(player, mover, new Vector3(-8.2f, 0, 94.8f));
            Assert.IsTrue((bool)Get(mover, "Simulated"));
        }
    }
}
