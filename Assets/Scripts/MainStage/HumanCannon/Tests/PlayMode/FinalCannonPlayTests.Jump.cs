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
        public IEnumerator OrdinaryFloorMovement_UsesCommonWalkSprintAndCrouch_WithActiveCannon()
        {
            DisableEnhancementThreats();
            var character = Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "Character");
            Assert.IsTrue(((Behaviour)system).isActiveAndEnabled);
            for (int posture = 0; posture < 3; posture++)
            {
                Call(mover, "SetStartPose", new Vector3(0, 0.01f, 57), Quaternion.identity, true);
                yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "일반 바닥 접촉");
                float multiplier = posture == 1 ? (float)Get(character, "SprintSpeedMultiplier")
                    : posture == 2 ? (float)Get(character, "CrouchSpeedMultiplier") : 1f;
                float expectedSpeed = (float)Get(character, "FreeMoveSpeed") * multiplier;
                float distance = 0f, duration = 0f;
                for (int frame = 0; frame < 12; frame++)
                {
                    Vector3 start = player.transform.position;
                    Call(mover, "SetMoveInput", 1f, 0f);
                    Call(mover, "SetLocomotionInput", posture == 1, posture == 2);
                    yield return null;
                    distance += Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude;
                    duration += Time.deltaTime;
                }
                Assert.That(distance / duration, Is.EqualTo(expectedSpeed).Within(0.15f), "실제 바닥 이동 속도, 자세=" + posture);
            }
        }

        [UnityTest]
        public IEnumerator OrdinaryJump_UsesCommonHeight_ThroughInjuryRestartAndContentExit()
        {
            DisableEnhancementThreats();
            var settings = StaticGet("CurtainCall.Settings.GameSettings", "Base");
            var character = Get(settings, "Character");
            var damage = Get(settings, "BodyDamage");
            float commonHeight = (float)Get(character, "jumpHeight");
            Assert.AreEqual(commonHeight, Get(character, "JumpHeight"), "활성 대포 시스템도 공통 바닥 높이를 바꾸지 않는다.");
            for (int lost = 0; lost <= 2; lost++)
            {
                if (lost > 0) Call(player, "ServerCut", 1);
                Call(mover, "SetStartPose", new Vector3(0, 0.01f, 57), Quaternion.identity, true);
                yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "실제 바닥 접촉");
                yield return null;
                float expectedHeight = commonHeight * (float)Call(damage, "JumpHeightForLostLegs", lost);
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
                    Call(mover, "SetMoveInput", 1f, 0f);
                    highest = Mathf.Max(highest, player.transform.position.y);
                    yield return null;
                }
                Assert.IsFalse((bool)Get(mover, "IsAirborne"));
                Assert.That(highest - start.y, Is.EqualTo(expectedHeight).Within(0.12f), "실제 CharacterController 점프 최고점");
                float expectedTravel = (float)Get(character,"FreeMoveSpeed") * (float)Call(damage,"MoveMultiplier",lost)
                    * 2f * Mathf.Sqrt(2f * expectedHeight / gravity);
                Assert.That(player.transform.position.x - start.x, Is.GreaterThan(expectedTravel * .7f), "공통 부상 속도로 체공 중 이동 입력 반영");
            }
            Call(player, "RequestRestartAll");
            yield return null; yield return null;
            Assert.AreEqual(commonHeight, Get(character, "JumpHeight"), "R 전체 재시작에도 공통 바닥 높이 유지");
            ((Behaviour)system).enabled = false;
            Assert.AreEqual(commonHeight, Get(character, "JumpHeight"), "콘텐츠 종료 전후에 같은 공통 바닥 높이 사용");
        }

        [UnityTest]
        public IEnumerator GroundSaw_FourSecondSweep_CanBeJumped_ButCutsStandingPlayer()
        {
            DisableEnhancementThreats();
            var course = HazardCourse();
            var saw = Enumerable.Range(0, (int)Get(course, "HazardCount"))
                .Where(i => (bool)Call(course, "IsHazardEnabled", i))
                .Select(i => (Behaviour)Call(course, "Hazard", i))
                .First(h => Get(h, "Kind").ToString() == "GroundSaw" && !(bool)Get(h, "IsPoleSaw"));
            Assert.AreEqual(4f, Get(saw, "PeriodSeconds"));
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
