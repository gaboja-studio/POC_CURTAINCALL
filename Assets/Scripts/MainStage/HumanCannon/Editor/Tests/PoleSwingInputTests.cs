using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class PoleSwingInputTests
    {
        [TestCase(30, 1f)]
        [TestCase(60, 1f)]
        [TestCase(120, 1f)]
        [TestCase(30, -1f)]
        [TestCase(60, -1f)]
        [TestCase(120, -1f)]
        public void HeldDirection_ReachesWideSwingThenReturnsAcrossNeutralWithoutReleasingKey(int framesPerSecond, float direction)
        {
            var input = new PoleSwingInput();
            float angle = 0f, speed = 0f, peak = 0f, returnedMinimum = 50f, totalPushSeconds = 0f;
            float firstReturnAt = -1f;
            float dt = 1f / framesPerSecond;
            // 현재 코스의 8m 줄과 실제 반동 수치를 사용한다. 입력은 끝까지 같은 방향으로 유지한다.
            for (int frame = 0; frame < 12 * framesPerSecond; frame++)
            {
                float push = input.Step(direction, dt);
                totalPushSeconds += push * direction * dt;
                PoleSwingMotion.Step(ref angle, ref speed, push, dt, 9.81f, 8f, 180f, 50f, 100f, .4f);
                Assert.IsTrue(float.IsFinite(angle) && float.IsFinite(speed));
                Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(50f));
                Assert.That(Mathf.Abs(speed), Is.LessThanOrEqualTo(100f));
                float along = angle * direction;
                peak = Mathf.Max(peak, along);
                if (peak >= 40f)
                {
                    returnedMinimum = Mathf.Min(returnedMinimum, along);
                    if (along < 0f && firstReturnAt < 0f) firstReturnAt = (frame + 1) * dt;
                }
                if (frame >= framesPerSecond)
                    Assert.That(push, Is.Zero, "유지 입력이 매 프레임 새 반동으로 충전되면 안 된다.");
            }
            Assert.That(totalPushSeconds, Is.EqualTo(.65f).Within(.00002f), "프레임 속도와 무관하게 한 번의 반동만 소비한다.");
            Assert.That(peak, Is.GreaterThanOrEqualTo(40f), "첫 반동의 넓은 이동은 유지한다.");
            Assert.That(firstReturnAt, Is.GreaterThan(0f).And.LessThan(5f), "키를 계속 눌러도 중립점을 넘어 돌아와야 한다.");
            Assert.That(returnedMinimum, Is.LessThan(-5f), "최대 각도에서 한쪽 자세로 버티지 않는다.");
        }

        [TestCase(0f)]
        [TestCase(.009f)]
        [TestCase(-.009f)]
        public void NeutralInput_RearmsSameDirectionAfterExpiredPush(float neutral)
        {
            var input = new PoleSwingInput();
            Exhaust(ref input);
            Assert.That(input.Step(1f, .05f), Is.Zero);
            Assert.That(input.Step(neutral, .05f), Is.Zero);
            Assert.That(input.Step(1f, .05f), Is.EqualTo(1f));
        }

        [Test]
        public void OppositeDirection_StartsFullPushWithoutWaitingForNeutral()
        {
            var input = new PoleSwingInput();
            input.Step(1f, .25f);
            input.Step(1f, .25f);
            Assert.That(input.Step(-1f, .25f), Is.EqualTo(-1f));
            Assert.That(input.Step(-1f, .25f), Is.EqualTo(-1f));
            Assert.That(input.Step(-1f, .25f), Is.EqualTo(-.6f).Within(.00001f));
            Assert.That(input.Step(-1f, .05f), Is.Zero);
            Assert.That(input.Step(1f, .05f), Is.EqualTo(1f));
        }

        [Test]
        public void SameDirectionPacketsAndAnalogChanges_DoNotRechargePush()
        {
            var input = new PoleSwingInput();
            float[] strengths = { .2f, .9f, .4f, 1f };
            for (int packet = 0; packet < 40; packet++)
            {
                float push = input.Step(strengths[packet % strengths.Length], .05f);
                if (packet >= 20) Assert.That(push, Is.Zero, "50ms마다 입력 세기가 바뀌어도 같은 방향이면 재충전하지 않는다.");
            }
            Assert.That(input.Step(1f, .05f), Is.Zero);
        }

        [Test]
        public void NeutralThenPressBetweenSimulationSteps_RearmsObservedInput()
        {
            var input = new PoleSwingInput();
            Exhaust(ref input);
            Assert.That(input.Step(1f, .05f), Is.Zero);
            input.Observe(0f);
            input.Observe(1f);
            Assert.That(input.Step(1f, .05f), Is.EqualTo(1f), "같은 호스트 프레임 사이에 놓고 다시 눌러도 전환을 보존한다.");
        }

        [Test]
        public void DefaultReset_AllowsSameDirectionForNextHold()
        {
            var input = new PoleSwingInput();
            Exhaust(ref input);
            Assert.That(input.Step(1f, .05f), Is.Zero);
            input = default;
            Assert.That(input.Step(1f, .05f), Is.EqualTo(1f), "재잡기나 R의 초기화 뒤에는 새 반동을 사용할 수 있다.");
        }

        [Test]
        public void FinalPartialFrame_ReturnsAverageForceWithoutExtraPushTime()
        {
            var input = new PoleSwingInput();
            Assert.That(input.Step(.5f, .25f), Is.EqualTo(.5f));
            Assert.That(input.Step(.5f, .25f), Is.EqualTo(.5f));
            Assert.That(input.Step(.5f, .25f), Is.EqualTo(.3f).Within(.00001f));
            Assert.That(input.Step(.5f, .01f), Is.Zero);
        }

        [Test]
        public void LongFrame_ConsumesOnlyTheQuarterSecondSimulatedBySwingMotion()
        {
            var input = new PoleSwingInput();
            Assert.That(input.Step(1f, float.MaxValue), Is.EqualTo(1f));
            Assert.That(input.Step(1f, float.MaxValue), Is.EqualTo(1f));
            Assert.That(input.Step(1f, float.MaxValue), Is.EqualTo(.6f).Within(.00001f));
            Assert.That(input.Step(1f, float.MaxValue), Is.Zero);
        }

        [Test]
        public void InvalidDeltaTime_DoesNotConsumeOrRearmPendingPush()
        {
            var input = new PoleSwingInput();
            Assert.That(input.Step(1f, .25f), Is.EqualTo(1f));
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.That(input.Step(-1f, dt), Is.Zero, "유효하지 않은 시간의 반대 입력으로 재충전하지 않는다.");
                Assert.That(input.Step(0f, dt), Is.Zero);
            }
            Assert.That(input.Step(1f, .25f), Is.EqualTo(1f));
            Assert.That(input.Step(1f, .25f), Is.EqualTo(.6f).Within(.00001f));
            Assert.That(input.Step(1f, .05f), Is.Zero);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteInput_ActsAsNeutralAndAllowsNextValidPush(float invalid)
        {
            var input = new PoleSwingInput();
            Exhaust(ref input);
            Assert.That(input.Step(invalid, .05f), Is.Zero);
            Assert.That(input.Step(1f, .05f), Is.EqualTo(1f));
        }

        static void Exhaust(ref PoleSwingInput input)
        {
            for (int packet = 0; packet < 20; packet++) input.Step(1f, .05f);
        }
    }
}
