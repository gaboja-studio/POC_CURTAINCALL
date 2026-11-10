using System.Collections;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest, Category("CommonLandingSafety")]
        public IEnumerator CommonSafeLandingPreservesBodyAndRespectsSettingAndActualSurface()
        {
            DisableEnhancementThreats();
            var rules = Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "BodyDamage");
            string saved = JsonUtility.ToJson(rules);
            var floor = new GameObject("shared safe landing fixture");
            Vector3 contact = new Vector3(9400, 100, 9400);
            floor.transform.position = contact - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(2, 1, 2);
            var zone = (Behaviour)floor.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonLandingZone"));
            try
            {
                JsonUtility.FromJsonOverwrite("{\"safeLandingZones\":true}", rules);
                Call(mover, "SetStartPose", contact, Quaternion.identity, true);
                Physics.SyncTransforms();
                Call(player, "ServerSetOneArm");
                int parts = Convert.ToInt32(Get(player, "LostParts"));
                float height = (float)Get(rules, "FallDeathHeight") + 20f;
                Call(player, "CompleteFall", height, contact, null, null);
                AssertBody(parts); // 일반 낙하도 같은 착지대에서 보호하되 이미 잃은 사지는 복구하지 않는다.

                zone.enabled = false;
                Call(player, "CompleteFall", height, contact, null, null);
                Assert.AreEqual("Fallen", Get(player, "State").ToString(), "비활성 착지대는 일반 바닥");
                Call(player, "ServerRestoreAll");
                zone.enabled = true;
                JsonUtility.FromJsonOverwrite("{\"safeLandingZones\":false}", rules);
                Call(player, "CompleteFall", height, contact, null, null);
                Assert.AreEqual("Fallen", Get(player, "State").ToString(), "공통 설정을 끄면 높이 피해 적용");
                Call(player, "ServerRestoreAll");
                JsonUtility.FromJsonOverwrite("{\"safeLandingZones\":true}", rules);
                Vector3 outside = contact + Vector3.right * 4f;
                Call(mover, "SetStartPose", outside, Quaternion.identity, true);
                Call(player, "CompleteFall", height, outside, null, null);
                Assert.AreEqual("Fallen", Get(player, "State").ToString(), "착지대 옆 추락은 보호하지 않는다");
                yield return null;
            }
            finally { JsonUtility.FromJsonOverwrite(saved, rules); UnityEngine.Object.Destroy(floor); }
        }

        [UnityTest, Category("TutorialFallTracking")]
        public IEnumerator ConfirmedFlightLandingClearsOwnersOrdinaryFallHistory()
        {
            DisableEnhancementThreats();
            Vector3 contact = new Vector3(9500, 9500, 9500);
            Call(mover, "SetStartPose", contact, Quaternion.identity, true);
            Call(player, "BeginFallAt", contact + Vector3.up * 20f);
            Put(player, "fallAirborne", true);
            Call(Get(system, "flying"), "Begin", Actor, 0, 351u);
            Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(20f));
            // 원격 소유자가 LandingMessage를 받은 경로: 서버 CompleteFall 호출 없이 확인만 적용.
            Call(system, "ApplyLanding", Actor, 0, 351u, 0u, true);
            Assert.IsFalse((bool)Get(player, "fallAirborne"));
            Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(0f));
            Call(player, "BeginFallAt", contact + Vector3.up * 2f);
            Call(system, "ApplyLanding", Actor, 0, 351u, 0u, true);
            Assert.That(Call(player, "FallHeightAt", contact), Is.EqualTo(2f), "중복/지난 비행의 응답은 새 낙하를 지우지 않는다.");
            yield return null;
        }
    }
}
