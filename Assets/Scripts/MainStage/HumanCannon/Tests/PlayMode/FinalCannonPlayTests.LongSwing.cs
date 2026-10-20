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

        public IEnumerator OrdinaryJump_CanStartAndReverseMovementWhileAirborne_AndJumpAgain()
        {
            DisableEnhancementThreats();
            Assert.IsTrue((bool)Get(mover, "FreeAirControl"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            var control = Get(controller, "Scheme");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "normal ground");
                NormalInput(control, context, 0, true);
                yield return WaitUntil(() => (bool)Get(mover, "IsAirborne"), 1, "ordinary Space jump");
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 before = player.transform.position;
                    float deadline = Time.time + .1f;
                    while (Time.time < deadline)
                    {
                        Assert.IsTrue((bool)Get(mover, "IsAirborne"), "방향 전환은 착지 전에 일어난다.");
                        NormalInput(control, context, side, false);
                        yield return null;
                    }
                    var view = Get(pilot, "cannonView");
                    Vector3 forward = Vector3.ProjectOnPlane((Vector3)Get(view, "ViewForward"), Vector3.up).normalized;
                    Assert.That(Vector3.Dot(player.transform.position - before, Vector3.Cross(Vector3.up, forward)) * side,
                        Is.GreaterThan(.1f), "제자리 이륙 뒤에도 좌우 입력과 역방향 전환을 반영한다.");
                }
                NormalInput(control, context, 0, false);
                yield return WaitUntil(() => !(bool)Get(mover, "IsAirborne"), 3, "land before next Space");
            }
            // 콘텐츠를 나갈 때 다른 공간에 옵션을 남기지 않는다.
            Call(control, "Exit", context);
            Assert.IsFalse((bool)Get(mover, "FreeAirControl"));
            Call(control, "Enter", context);
        }

        void NormalInput(object control, object context, float side, bool jump)
        {
            var frame = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
            frame.GetType().GetField("Posture").SetValue(frame, side);
            frame.GetType().GetField("ActionPressed").SetValue(frame, jump);
            Call(control, "Tick", context, frame);
        }

        [UnityTest]
        public IEnumerator CannonLaunch_DirectionInputPreservesMomentum_ReleaseKeepsFlight_ThreeRestarts()
        {
            DisableEnhancementThreats();
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.right })
            {
                yield return Board(0);
                yield return ReleaseThenArm();
                Input(tapped: true);
                Assert.IsTrue(Flying);
                Vector3 launchHorizontal = Vector3.ProjectOnPlane((Vector3)Get(pilot, "velocity"), Vector3.up);
                yield return null;
                Vector3 before = player.transform.position;
                PoleFlightDirection(direction);
                yield return new WaitForEndOfFrame();
                var horizontalSpeed = Vector3.ProjectOnPlane(player.transform.position - before, Vector3.up) / Time.deltaTime;
                Assert.That(Vector3.Distance(horizontalSpeed, launchHorizontal + direction * 4.5f), Is.LessThan(.05f),
                    "이전 방식대로 발사 관성 위에 방향키 보정만 더한다.");
                Assert.That(((Vector3)Get(pilot, "velocity")).y, Is.GreaterThan(0), "수직 반동은 유지한다.");
                yield return null;
                before = player.transform.position;
                Input();
                yield return new WaitForEndOfFrame();
                var releasedSpeed = Vector3.ProjectOnPlane(player.transform.position - before, Vector3.up) / Time.deltaTime;
                Assert.That(Vector3.Distance(releasedSpeed, launchHorizontal),
                    Is.LessThan(.05f), "키를 놓아도 원래 발사 관성으로 계속 비행해야 한다.");
                Call(player, "RequestRestartAll");
                yield return WaitUntil(() => !Flying && !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"),
                    3, "restart before next launch");
            }
        }

        [UnityTest]

        public IEnumerator PoleHeldInput_ReturnsThroughCenterWithoutReleasingKey_AndRearmsAfterRestart()
        {
            DisableEnhancementThreats();
            // 같은 W를 R 뒤 다시 눌러도 새 반동을 얻고, S도 대칭으로 돌아와야 한다.
            foreach (float direction in new[] { 1f, 1f, -1f })
            {
                const int index = 1;
                yield return GrabEnhancementPole(index);
                var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
                var runtime = ((Array)Get(poles, "states")).GetValue(index);
                float peak = 0f;
                bool returnedThroughCenter = false;
                double deadline = (double)Get(poles, "Clock") + 5;
                while ((double)Get(poles, "Clock") < deadline)
                {
                    PoleEnhancementInput(direction);
                    yield return null;
                    Assert.AreEqual(index, Get(pilot, "HeldPole"));
                    float angle = (float)Get(runtime, "Angle") * direction;
                    peak = Mathf.Max(peak, angle);
                    if (peak >= 20f && angle < -2f) returnedThroughCenter = true;
                }
                TestContext.WriteLine($"Held {direction}: peak={peak}, returnedThroughCenter={returnedThroughCenter}");
                Assert.That(peak, Is.GreaterThanOrEqualTo(40f), "처음 누른 반동의 넓이는 유지한다.");
                Assert.IsTrue(returnedThroughCenter, "W/S를 계속 눌러도 끝각에 버티지 않고 중립을 지나 돌아와야 한다.");
                // 같은 프레임에 놓았다 다시 누르는 빠른 입력도 50ms 전송 제한에 삼켜지지 않는다.
                PoleEnhancementInput(0);
                PoleEnhancementInput(direction);
                peak = 0f;
                deadline = (double)Get(poles, "Clock") + 1.5;
                while ((double)Get(poles, "Clock") < deadline)
                {
                    PoleEnhancementInput(direction);
                    yield return null;
                    peak = Mathf.Max(peak, (float)Get(runtime, "Angle") * direction);
                }
                Assert.That(peak, Is.GreaterThanOrEqualTo(40f), "짧게 놓았다 다시 누르면 새 반동을 준다.");
                AssertBody(0);
                Call(player, "RequestRestartAll");
                yield return WaitUntil(() => !Flying && !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"),
                    3, "R 뒤 봉 반동 재입력 준비");
            }
        }

        [UnityTest]
        public IEnumerator LinkedPole_ForwardJumpAllowsImmediateReverseAndSideSteering_ThreeRestarts()
        {
            DisableEnhancementThreats();
            var reverseSpeeds = new float[3];
            var forwardSpeeds = new float[3];
            var lateralDrift = new float[3];
            for (int attempt = 0; attempt < 3; attempt++)
            {
                for (int lost = 0; lost < attempt; lost++) Call(player, "ServerCut", 1);
                yield return GrabEnhancementPole(1);
                var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
                var pole = (Component)Call(poles, "Pole", 1);
                Vector3 neutral = ((Transform)Get(pole, "Grip")).position;
                Vector3 forward = (Vector3)Get(pole, "Forward");
                float deadline = Time.realtimeSinceStartup + 3;
                while (Vector3.Dot(player.transform.position - neutral, forward) < 3f && Time.realtimeSinceStartup < deadline)
                {
                    PoleEnhancementInput(1);
                    yield return null;
                }
                Assert.That(Vector3.Dot(player.transform.position - neutral, forward), Is.GreaterThanOrEqualTo(3f));
                PoleEnhancementInput(1, release: true);
                Assert.IsTrue(Flying);
                Assert.AreEqual(-1, Get(pilot, "HeldPole"));
                forwardSpeeds[attempt] = Vector3.Dot((Vector3)Get(pilot, "velocity"), forward);
                Assert.That(forwardSpeeds[attempt], Is.GreaterThan(5f), "긴 줄의 반동을 충분히 받으며 출발");
                yield return null;
                Vector3 before = player.transform.position;
                PoleFlightDirection(-forward);
                yield return new WaitForEndOfFrame();
                reverseSpeeds[attempt] = Vector3.Dot(player.transform.position - before, forward) / Time.deltaTime;
                yield return null;
                before = player.transform.position;
                PoleFlightDirection(Vector3.Cross(Vector3.up, forward));
                yield return new WaitForEndOfFrame();
                lateralDrift[attempt] = Mathf.Abs(Vector3.Dot(player.transform.position - before, forward)) / Time.deltaTime;
                Assert.That(Vector3.Dot(player.transform.position - before, Vector3.Cross(Vector3.up, forward)) / Time.deltaTime,
                    Is.GreaterThan(0.1f), "앞으로 멈출 뿐 아니라 실제 옆 방향으로 움직인다.");
                yield return null;
                before = player.transform.position;
                Input();
                yield return new WaitForEndOfFrame();
                var coast = Vector3.ProjectOnPlane(player.transform.position - before, Vector3.up) / Time.deltaTime;
                Assert.That(Vector3.Dot(coast, Vector3.Cross(Vector3.up, forward)),
                    Is.EqualTo(forwardSpeeds[attempt]).Within(.1f), "키를 놓아도 마지막 방향의 반동 속력을 유지한다.");
                Assert.That(Vector3.ProjectOnPlane((Vector3)Get(pilot, "velocity"), Vector3.up).magnitude,
                    Is.EqualTo(forwardSpeeds[attempt]).Within(.1f), "뒤/옆 방향 전환으로 반동이 걷기 속도로 줄지 않는다.");
                Call(player, "RequestRestartAll");
                yield return WaitUntil(() => !Flying && !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"),
                    3, "R 재시작으로 다음 재현 준비");
                Assert.IsFalse((bool)Get(pilot, "steerPoleMomentum"), "봉 도약 제동 상태는 R에서 정리한다.");
            }
            TestContext.WriteLine("release forward speeds: " + string.Join(", ", forwardSpeeds));
            TestContext.WriteLine("back-input forward speeds: " + string.Join(", ", reverseSpeeds));
            TestContext.WriteLine("side-input forward drift: " + string.Join(", ", lateralDrift));
            foreach (float speed in reverseSpeeds)
                Assert.That(speed, Is.LessThan(-0.1f), "봉 도약 후 뒤 입력을 주면 실제 몸도 바로 뒤로 이동해야 한다.");
            foreach (float drift in lateralDrift)
                Assert.That(drift, Is.LessThan(0.1f), "옆 방향 입력에 원치 않는 전진 관성이 남으면 안 된다.");
        }

        void PoleFlightDirection(Vector3 direction)
        {
            var view = Get(pilot, "cannonView");
            Vector3 forward = view != null && (bool)Get(view, "UsesMouseLook")
                ? (Vector3)Get(view, "ViewForward") : (Vector3)Get(pilot, "flightForward");
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            var frame = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
            frame.GetType().GetField("Posture").SetValue(frame, Vector3.Dot(direction, Vector3.Cross(Vector3.up, forward)));
            frame.GetType().GetField("Move").SetValue(frame, Vector3.Dot(direction, forward));
            Call(pilot, "ReadInput", frame);
        }

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
