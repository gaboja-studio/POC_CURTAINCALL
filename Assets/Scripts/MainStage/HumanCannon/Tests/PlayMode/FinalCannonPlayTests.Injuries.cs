using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator OriginalCourse_InjuryPolicyAndFlightCorrection_RestoreOnExit()
        {
            var effects = player.GetComponent(GameType("CurtainCall.Damage.BodyDamageEffects"));
            var settings = StaticGet("CurtainCall.Settings.GameSettings", "Base");
            var damage = Get(settings, "BodyDamage");
            var previous = ((IDictionary)Get(system, "previousPolicies"))[effects];
            Assert.AreEqual("HumanCannonBodyPenalty", Get(effects, "Policy").GetType().Name);
            Assert.IsNull(GameObject.Find("HumanCannonSoloPractice"));
            ((Behaviour)pilot).enabled = false; // 명시적인 한 프레임 물리 측정 중 자동 LateUpdate 중복 방지.
            for (int lost = 0; lost <= 3; lost++)
            {
                if (lost > 0) Call(player, "ServerCut", 1);
                yield return null;
                yield return null;
                float expectedMove = lost == 0 ? 1f : lost == 1 ? 0.8f : (float)Get(damage, "CrawlSpeedMultiplier");
                Assert.That((float)Get(mover, "SpeedMultiplier"), Is.EqualTo(expectedMove).Within(0.001f));
                Assert.That((float)Get(mover, "JumpHeightMultiplier"), Is.EqualTo(
                    (float)Call(damage, "JumpHeightForLostLegs", Math.Min(2, lost))).Within(0.001f));
                Assert.AreEqual(1f, Get(player.GetComponent(GameType("CurtainCall.Player.PlayerInteraction")), "ReachMultiplier"));
                if (lost >= 2) Assert.AreEqual(Get(damage, "CrawlCapsuleHeight"), Get(mover, "BodyHeightOverride"));

                Call(pilot, "Launch", 6, 1u, new Vector3(-40, 20, 0), Quaternion.identity, Vector3.up * 12);
                var frame = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
                frame.GetType().GetField("Posture").SetValue(frame, 1f);
                Call(pilot, "ReadInput", frame);
                float x = player.transform.position.x;
                Call(pilot, "SimulateFlight");
                float air = lost == 0 ? 1f : lost == 1 ? 0.8f : 0.5f;
                Assert.That(player.transform.position.x - x, Is.EqualTo(3f * air * Time.deltaTime).Within(0.002f));
                x = player.transform.position.x;
                Input(); Call(pilot, "SimulateFlight");
                Assert.That(player.transform.position.x, Is.EqualTo(x).Within(0.001f), "입력 해제 즉시 공중 보정 정지");
                Call(pilot, "Rebound", 6, 1u, 0u, new Vector3(-40, 20, 0), 12f);
                Assert.AreEqual(Vector3.up * 12, Get(pilot, "velocity"), "외팔도 반동 즉시·높이 유지");
                Call(pilot, "ReleaseMovement");
            }
            ((Behaviour)system).enabled = false;
            Assert.AreSame(previous, Get(effects, "Policy"), "코스 종료 시 기존 콘텐츠 정책 복원");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OriginalCourse_OneArmGrabWaits_LeavingRangeCancels_DropIsImmediate()
        {
            Call(player, "ServerSetOneArm");
            yield return null;
            var trampoline = StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current") as Component;
            var handle = (Transform)Get(trampoline, "Handle");
            Call(mover, "SetStartPose", handle.position, Quaternion.identity, true);
            yield return null;
            Assert.IsTrue((bool)Call(trampoline, "CanGrab", player), "외팔도 기존 낮은 손잡이에 접근 가능");
            Input(tapped: true);
            Assert.AreNotEqual(Actor, Get(trampoline, "Carrier"));
            Call(mover, "SetStartPose", handle.position + Vector3.right * 6, Quaternion.identity, true);
            yield return new WaitForSeconds(0.7f);
            Assert.AreNotEqual(Actor, Get(trampoline, "Carrier"), "대기 중 거리 이탈 취소");
            Call(mover, "SetStartPose", handle.position, Quaternion.identity, true);
            yield return null;
            double started = Time.timeAsDouble;
            Input(tapped: true);
            yield return new WaitForSeconds(0.25f);
            Assert.AreNotEqual(Actor, Get(trampoline, "Carrier"));
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 2, "외팔 잡기 확정");
            Assert.That(Time.timeAsDouble - started, Is.GreaterThanOrEqualTo(0.59));
            Call(trampoline, "RequestDrop");
            Assert.AreNotEqual(Actor, Get(trampoline, "Carrier"), "놓기는 추가 지연 없음");
            AssertBody(13);
        }
    }
}
