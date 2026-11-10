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
        [UnityTest, Category("CommonInspectorSettings")]
        public IEnumerator InspectorPoles_FourRidersDriveAndMiddleRelease() => ConfigurablePoleCrew(false);
        [UnityTest, Category("CommonInspectorSettings")]
        public IEnumerator InspectorPoles_FourRidersExpireTogether() => ConfigurablePoleCrew(true);

        IEnumerator ConfigurablePoleCrew(bool expire)
        {
            DisableEnhancementThreats();
            var settings = (Object)StaticGet("CurtainCall.Settings.GameSettings", "Poles");
            string saved = JsonUtility.ToJson(settings);
            var poles = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var fixture = new GameObject("Inspector pole fixture");
            var poleObject = new GameObject("four person pole"); poleObject.transform.SetParent(fixture.transform);
            poleObject.transform.position = new Vector3(9000, 9008, 9000);
            var pole = poleObject.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonPole"));
            var remotes = new Component[3];
            int index = -1;
            try
            {
                JsonUtility.FromJsonOverwrite("{\"capacity\":4}", settings);
                index = (int)Call(poles, "Register", pole);
                var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
                Call(mover, "SetStartPose", poleObject.transform.position - Vector3.up, Quaternion.identity, true);
                Physics.SyncTransforms();
                Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyPole"));
                for (int seat = 1; seat < 4; seat++)
                {
                    var instance = Object.Instantiate(prefab);
                    remotes[seat - 1] = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                    Call(remotes[seat - 1], "SpawnWithOwnership", (ulong)(776 + seat), true);
                    var remoteMover = instance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                    var feet = (Vector3)Call(pole, "HangingPosition", 0f, seat);
                    Call(remoteMover, "SetStartPose", feet, Quaternion.identity, true);
                    Physics.SyncTransforms();
                    CommonPoleRequest(poles, (ulong)(776 + seat), "PlatformGrab", index);
                    Assert.AreEqual(index, Call(poles, "HeldBy", (ulong)(776 + seat)), "seat " + seat);
                }
                Assert.AreEqual(4, Call(poles, "OccupantCount", index));
                Assert.IsFalse((bool)Call(poles, "HasFreeSeat", index));
                var state = Call(poles, "State", index);
                for (int seat = 1; seat < 4; seat++)
                    Assert.AreEqual(Call(state, "DeadlineAt", 0), Call(state, "DeadlineAt", seat));
                if (expire)
                {
                    var deadlines = (double[])Get(state, "deadlines");
                    for (int seat = 0; seat < 4; seat++) deadlines[seat] = (double)Get(poles, "Clock") - .01;
                    Call(poles, "TickState", index);
                    Assert.AreEqual(0, Call(poles, "OccupantCount", index));
                    Assert.AreEqual(-1, Call(poles, "HeldBy", Actor));
                }
                else
                {
                    CommonPoleRequest(poles, 779UL, "Swing", index, 1, 1);
                    yield return new WaitForSeconds(.12f);
                    Assert.That((float)Call(poles, "SpeedAt", index), Is.EqualTo(0).Within(.01));
                    JsonUtility.FromJsonOverwrite("{\"topRiderOnlySwing\":false}", settings);
                    CommonPoleRequest(poles, 779UL, "Swing", index, 1, 2);
                    yield return new WaitForSeconds(.12f);
                    Assert.That(Mathf.Abs((float)Call(poles, "SpeedAt", index)), Is.GreaterThan(.1f));
                    CommonPoleRequest(poles, 777UL, "Release", index);
                    Assert.AreEqual(4, Call(poles, "OccupantCount", index), "중간 지지자의 기본 놓기 제한");
                    JsonUtility.FromJsonOverwrite("{\"preventReleaseWithPassengers\":false}", settings);
                    CommonPoleRequest(poles, 777UL, "Release", index);
                    Assert.AreEqual(1, Call(poles, "OccupantCount", index), "중간 지지자 아래만 연쇄 낙하");
                    Assert.AreEqual(index, Call(poles, "HeldBy", Actor));
                }
                for (ulong actor = 777; actor <= 779; actor++) Assert.AreEqual(-1, Call(poles, "HeldBy", actor));
            }
            finally
            {
                if (index >= 0) Call(poles, "Deactivate", index);
                Call(pilot, "ClearMotionForInspection");
                foreach (var remote in remotes) if (remote != null) Call(remote, "Despawn", true);
                Object.Destroy(fixture);
                JsonUtility.FromJsonOverwrite(saved, settings);
            }
        }

        [UnityTest, Category("CommonInspectorSettings")]
        public IEnumerator InspectorDamage_UsesConfiguredHazardAndFallCounts()
        {
            DisableEnhancementThreats();
            var rules = Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "BodyDamage");
            string saved = JsonUtility.ToJson(rules);
            try
            {
                Call(mover, "SetStartPose", new Vector3(9500, 9500, 9500), Quaternion.identity, true);
                JsonUtility.FromJsonOverwrite("{\"hazardCutCount\":2,\"fallInjuryCutCount\":1}", rules);
                var origin = Enum.Parse(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer+DeathOrigin"), "Hazard");
                Call(player, "ServerHazardCut", "inspector-test", origin);
                var condition = player.GetComponent(GameType("CurtainCall.Player.PlayerCondition"));
                Assert.AreEqual(2, Get(condition, "LostCount"));
                Call(player, "CompleteFall", (float)Get(rules, "FallInjuryHeight") + .5f, player.transform.position, null, null);
                Assert.AreEqual(3, Get(condition, "LostCount"));
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                yield return null;
            }
            finally { JsonUtility.FromJsonOverwrite(saved, rules); }
        }
    }
}
