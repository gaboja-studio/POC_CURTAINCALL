using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator OrdinaryJump_ReachesOnePointTwo_RespectsInjuryAndRestoresOnExit()
        {
            DisableEnhancementThreats();
            var settings = StaticGet("CurtainCall.Settings.GameSettings", "Base");
            var character = Get(settings, "Character");
            var damage = Get(settings, "BodyDamage");
            Assert.AreEqual(1.2f, Get(character, "JumpHeight"));
            for (int lost = 0; lost <= 2; lost++)
            {
                if (lost > 0) Call(player, "ServerCut", 1);
                Call(mover, "SetStartPose", new Vector3(0, 0.01f, 57), Quaternion.identity, true);
                yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "실제 바닥 접촉");
                yield return null;
                float expectedHeight = 1.2f * (float)Call(damage, "JumpHeightForLostLegs", lost);
                float gravity = (float)Get(character, "Gravity");
                Assert.That((float)Get(mover, "JumpSpeed"), Is.EqualTo(Mathf.Sqrt(2 * gravity * expectedHeight)).Within(0.001f));
                Vector3 start = player.transform.position;
                float highest = start.y;
                Call(mover, "SetMoveInput", 1f, 0f);
                Call(mover, "RequestJump");
                yield return WaitUntil(() => (bool)Get(mover, "IsAirborne"), 2, "일반 점프 시작");
                float deadline = Time.realtimeSinceStartup + 3;
                while ((bool)Get(mover, "IsAirborne") && Time.realtimeSinceStartup < deadline)
                {
                    highest = Mathf.Max(highest, player.transform.position.y);
                    yield return null;
                }
                Assert.IsFalse((bool)Get(mover, "IsAirborne"));
                Assert.That(highest - start.y, Is.EqualTo(expectedHeight).Within(0.12f), "실제 CharacterController 점프 최고점");
                Assert.That(player.transform.position.x - start.x, Is.GreaterThan(1f), "옆 이동 입력과 점프 관성 유지");
            }
            Call(player, "RequestRestartAll");
            yield return null; yield return null;
            Assert.AreEqual(1.2f, Get(character, "JumpHeight"), "R 전체 재시작에도 인간대포 높이 유지");
            ((Behaviour)system).enabled = false;
            Assert.AreEqual(0.8f, Get(character, "JumpHeight"), "콘텐츠 종료 후 공통 높이 복원");
        }

        [UnityTest]
        public IEnumerator GroundSaw_FourSecondSweep_CanBeJumped_ButCutsStandingPlayer()
        {
            DisableEnhancementThreats();
            var saw = Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonHazard"), FindObjectsSortMode.None)
                .Cast<Behaviour>().Single(h => h.name == "GroundSaw1A");
            Assert.AreEqual(4f, Get(saw, "PeriodSeconds"));
            var course = HazardCourse();
            Assert.AreEqual(1, Get(course, "Difficulty"));
            var feet = saw.transform.position; feet.y = 0.01f;
            Call(mover, "SetStartPose", feet, Quaternion.identity, true);
            yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "톱 경로 바닥에 서기");
            double now = (double)Get(course, "Elapsed");
            HazardSet(saw, "phase", (float)(-(now + 0.5) * Math.PI * 2 / 4));
            Call(course, "ClearContacts");
            saw.enabled = true;
            Call(mover, "RequestJump");
            yield return new WaitForSeconds(1.3f);
            AssertBody(0);
            Assert.IsFalse((bool)Get(mover, "IsAirborne"));
            yield return WaitUntil(() => Convert.ToInt32(Get(player, "LostParts")) != 0, 2, "다음 왕복에서 서 있는 캐릭터는 실제로 절단");
            AssertBody(4);
        }
    }
}
