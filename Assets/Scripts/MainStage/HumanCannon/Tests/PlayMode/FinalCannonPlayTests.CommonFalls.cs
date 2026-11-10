using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest, Category("CommonRulesFollowup")]
        public IEnumerator CommonSpringShoesReboundFromOrdinaryFatalHeightAndConsumeOnce()
        {
            DisableEnhancementThreats();
            var equipment = new GameObject("ordinary spring equipment");
            var carry = equipment.AddComponent(GameType("CurtainCall.WaitingRoom.PropCarry"));
            var floor = new GameObject("spring test floor");
            floor.AddComponent<BoxCollider>().size = new Vector3(20, 1, 20);
            floor.transform.position = new Vector3(3000, 99.5f, 3000);
            try
            {
                campaign = (Component)GameType("CurtainCall.Campaign.CampaignRun").GetMethod("EnsureExists").Invoke(null, null);
                rewards = (Component)GameType("CurtainCall.Reward.PerformanceRewards").GetMethod("EnsureExists").Invoke(null, null);
                yield return null;
                // 장비 장부의 판정 권한만 준비한다. 방 시작/장면 전환/최소 인원 설정은 바꾸지 않는다.
                Put(campaign, "host", Activator.CreateInstance(GameType("CurtainCall.Campaign.RunState")));
                yield return WaitUntil(() => (bool)Get(carry, "HasAuthority"), 3, "equipment authority");
                var item = Activator.CreateInstance(GameType("CurtainCall.WaitingRoom.PropItem"), 987654, "spring-shoes", "test shoes", 2);
                Assert.AreEqual("None", Call(carry, "Receive", Actor, item).ToString());
                Call(mover, "SetStartPose", new Vector3(3000, 105.3f, 3000), Quaternion.identity, true);
                Physics.SyncTransforms();
                yield return WaitUntil(() => (int)Get(Call(carry, "Equipped", Actor, "spring-shoes"), "Uses") == 1, 4, "ordinary fall consumes one spring use");
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
                yield return new WaitForSeconds(.2f);
                Assert.Greater(player.transform.position.y, 100.5f, "실제 위쪽 반동");
                Assert.AreEqual(1, Get(Call(carry, "Equipped", Actor, "spring-shoes"), "Uses"), "같은 접촉의 중복 소모 금지");
            }
            finally { Object.Destroy(floor); Object.Destroy(equipment); }
        }
        [UnityTest, Category("CommonRulesFollowup")]
        public IEnumerator CommonFallOrdinaryFloorUsesActualHeightAndTeleportClearsPreviousPeak()
        {
            DisableEnhancementThreats();
            var floor = new GameObject("ordinary untagged fall floor");
            var box = floor.AddComponent<BoxCollider>(); box.size = new Vector3(20, 1, 20);
            floor.transform.position = new Vector3(3000, 99.5f, 3000);
            try
            {
                Call(mover, "SetStartPose", new Vector3(3000, 103.3f, 3000), Quaternion.identity, true);
                Physics.SyncTransforms();
                yield return WaitUntil(() => player.transform.position.y < 102f, 2, "fall is already in progress");
                Call(player, "RequestNaturalFall");
                yield return WaitUntil(() => Convert.ToInt32(Get(player, "LostParts")) != 0, 4, "3m ordinary fall injury");
                var condition = player.GetComponent(GameType("CurtainCall.Player.PlayerCondition"));
                Assert.AreEqual(2, Get(condition, "LostCount"));
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                var before = Get(player, "LostParts");
                Call(mover, "SetStartPose", new Vector3(3000, 120, 3000), Quaternion.identity, true);
                yield return null;
                Call(mover, "SetStartPose", new Vector3(3000, 100.5f, 3000), Quaternion.identity, true);
                yield return new WaitForSeconds(.7f);
                Assert.AreEqual(before, Get(player, "LostParts"), "순간이동 전 높이를 다음 낙하에 더하지 않는다");
                Call(mover, "SetStartPose", new Vector3(3000, 105.3f, 3000), Quaternion.identity, true);
                yield return WaitUntil(() => Get(player, "State").ToString() == "Fallen", 4, "5m ordinary fall death");
            }
            finally { Object.Destroy(floor); }
        }

        [UnityTest, Category("CommonRulesFollowup")]
        public IEnumerator CommonHazardCooldownIsSharedAcrossSourcesAndFatalFireBypassesIt()
        {
            DisableEnhancementThreats();
            Call(mover, "SetStartPose", new Vector3(0, .01f, 57), Quaternion.identity, true);
            yield return null;
            var origin = Enum.Parse(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer+DeathOrigin"), "Hazard");
            Call(player, "ServerHazardCut", "test-saw-a", origin);
            var first = Get(player, "LostParts");
            Assert.AreNotEqual(0, Convert.ToInt32(first));
            Call(player, "ServerHazardCut", "test-saw-b", origin);
            Assert.AreEqual(first, Get(player, "LostParts"));
            yield return new WaitForSeconds(1.05f);
            Call(player, "ServerHazardCut", "test-saw-a", origin);
            Assert.AreNotEqual(first, Get(player, "LostParts"));
            var fatal = Enum.Parse(GameType("CurtainCall.Damage.HazardDamageKind"), "FatalFire");
            Assert.IsTrue((bool)Call(player, "ServerHazardContact", fatal, "test-fire-wall", origin));
            Assert.AreEqual("Fallen", Get(player, "State").ToString());
        }
    }
}
