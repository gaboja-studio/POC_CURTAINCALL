using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        /// <summary>실제 호스트의 W/S 프레임을 주입해 몸·봉·줄과 도약을 검사한다. 실제 키보드/화면 조작은 아니다.</summary>
        [UnityTest]
        public IEnumerator CeilingSwing_MovesBodyAndBarTogether_AndReleasesWithLongRopeMomentum()
        {
            DisableEnhancementThreats();
            const int index = 1;
            yield return GrabEnhancementPole(index);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", index);
            var bar = (Transform)Get(pole, "visualBar");
            var grip = (Transform)Get(pole, "Grip");
            var leftAnchor = (Transform)Get(pole, "leftAnchor");
            var rightAnchor = (Transform)Get(pole, "rightAnchor");
            var leftCable = (Transform)Get(pole, "leftCable");
            var rightCable = (Transform)Get(pole, "rightCable");
            Vector3 neutral = grip.position;
            Vector3 pivot = (leftAnchor.position + rightAnchor.position) * 0.5f;
            Vector3 forward = (Vector3)Get(pole, "Forward");
            float length = (float)Get(pole, "SwingLength");
            float hangLength = (float)Get(pole, "hangLength");
            Assert.That(length, Is.EqualTo(8).Within(0.01f));
            var runtime = ((Array)Get(poles, "states")).GetValue(index);
            float deadline = Time.realtimeSinceStartup + 3;
            while (Vector3.Dot(player.transform.position - neutral, forward) < 3f && Time.realtimeSinceStartup < deadline)
            {
                PoleEnhancementInput(1);
                yield return null;
                Assert.AreEqual(index, Get(pilot, "HeldPole"));
            }
            Assert.That(Vector3.Dot(player.transform.position - neutral, forward), Is.GreaterThanOrEqualTo(3f),
                "기존 1.5m 진자로 도달할 수 없는 거리까지 실제 몸이 반동한다.");
            Assert.That(Vector3.Distance(grip.position, neutral), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(bar.position, player.transform.position + Vector3.up * hangLength), Is.LessThan(0.02f));
            Assert.That(Vector3.Distance(bar.position, pivot), Is.EqualTo(length).Within(0.02f));
            AssertSwingCable(leftCable, leftAnchor, bar.position, pivot, length);
            AssertSwingCable(rightCable, rightAnchor, bar.position, pivot, length);

            float angle = (float)Get(runtime, "Angle"), speed = (float)Get(runtime, "Speed");
            Vector3 expectedPosition = (Vector3)Call(pole, "HangingPosition", angle);
            Vector3 expectedVelocity = (Vector3)Call(pole, "ReleaseVelocity", angle, speed);
            Assert.That(Vector3.Dot(expectedVelocity, forward), Is.GreaterThan(5f), "실제 긴 줄의 전진 접선 속도가 충분히 남은 상태");
            PoleEnhancementInput(0, release: true);
            Assert.AreEqual(-1, Get(pilot, "HeldPole"));
            Assert.IsTrue(Flying);
            Assert.That(Vector3.Distance(player.transform.position, expectedPosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance((Vector3)Get(pilot, "velocity"), expectedVelocity), Is.LessThan(0.001f));
            Assert.IsFalse((bool)Get(Call(poles, "State", index), "IsOccupied"));
            yield return WaitUntil(() => Vector3.Dot(player.transform.position - expectedPosition, forward) > 0.25f,
                2, "놓은 뒤에도 반동 방향의 실제 비행이 이어진다.");
        }

        static void AssertSwingCable(Transform cable, Transform anchor, Vector3 bar, Vector3 pivot, float length)
        {
            Vector3 top = cable.TransformPoint(Vector3.up * 0.5f), bottom = cable.TransformPoint(Vector3.down * 0.5f);
            Assert.That(Vector3.Distance(top, anchor.position), Is.LessThan(0.02f));
            Assert.That(Vector3.Distance(bottom, bar + anchor.position - pivot), Is.LessThan(0.02f));
            Assert.That(Vector3.Distance(top, bottom), Is.EqualTo(length).Within(0.02f));
        }
    }
}
