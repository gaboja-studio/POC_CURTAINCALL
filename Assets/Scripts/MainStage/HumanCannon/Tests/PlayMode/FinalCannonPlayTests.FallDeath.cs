using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        // 2026-11-03 담당자 결정: 코스 밖 낭떠러지 추락은 외줄타기 낙사와 같이 공용 사망. 출발점으로 되살려 올리지 않는다.
        [UnityTest]
        public IEnumerator FallBelowCourse_DiesLikeTightrope_WithoutRespawningAtStart()
        {
            DisableEnhancementThreats();
            // 1구간 바닥(x ±22) 밖 허공에서 떨어뜨린다.
            Call(mover, "SetStartPose", new Vector3(40, 2, 30), Quaternion.identity, true);
            // 공용 사망 상태는 장소 규칙(SurvivalRules.DeathState)을 따르므로 Normal을 벗어났는지로 본다.
            yield return WaitUntil(() => Get(player, "State").ToString() != "Normal", 6, "낭떠러지 추락 즉시 공용 사망");
            Assert.AreEqual(0, System.Convert.ToInt32(Get(player, "LostParts")), "절단 2곳이 아니라 바로 사망한다.");
            Assert.That(player.transform.position.y, Is.LessThan(-10f), "출발점·대포 앞으로 되돌리지 않는다.");
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(player.transform.position.y, Is.LessThan(-10f), "사망 뒤에도 코스 위로 올라오지 않는다.");
            Call(player, "RequestRestartAll");
            yield return WaitUntil(() => Get(player, "State").ToString() == "Normal", 3, "재시작 후 생존");
        }
    }
}
