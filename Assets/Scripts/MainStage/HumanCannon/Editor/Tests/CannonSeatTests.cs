using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("CommonProgressionRegression")]
    public class CannonSeatTests
    {
        [Test]
        public void RestartEmptySeat_RejectsDelayedBoardRequest()
        {
            var seat = new CannonSeatState();
            uint beforeRestart = seat.Revision;
            seat.Reset();
            Assert.IsFalse(seat.IsOccupied);
            Assert.IsFalse(seat.TryApply(CannonAction.Board, 1, beforeRestart));
            Assert.IsTrue(seat.TryApply(CannonAction.Board, 1, seat.Revision));
        }

        [Test]
        public void RestartOccupiedSeat_RejectsDelayedLaunchAndCancel()
        {
            var seat = new CannonSeatState();
            seat.TryApply(CannonAction.Board, 1, 0);
            uint beforeRestart = seat.Revision;
            seat.Reset();
            Assert.IsFalse(seat.TryApply(CannonAction.Launch, 1, beforeRestart));
            Assert.IsFalse(seat.TryApply(CannonAction.Cancel, 1, beforeRestart));
            Assert.IsTrue(seat.TryApply(CannonAction.Board, 2, seat.Revision));
        }

        [Test]
        public void RestartAfterLaunch_OldSnapshotCannotReplaceReset()
        {
            var host = new CannonSeatState();
            host.TryApply(CannonAction.Board, 1, 0);
            host.TryApply(CannonAction.Launch, 1, host.Revision);
            uint oldLaunch = host.Revision;
            host.Reset();
            var client = new CannonSeatState();
            Assert.IsTrue(client.Receive(host.Revision, host.Occupant));
            Assert.IsFalse(client.Receive(oldLaunch, CannonSeatState.Nobody));
            Assert.IsFalse(client.Receive(oldLaunch - 1, 1));
            Assert.IsFalse(client.IsOccupied);
        }

        [TestCase(1f, 0f, 3f, 0f)]
        [TestCase(-1f, 0f, -3f, 0f)]
        [TestCase(0f, 1f, 0f, 3f)]
        [TestCase(0f, -1f, 0f, -3f)]
        public void AirControl_UsesGroundDirections_AndCannotAddHeight(float side, float forward, float x, float z)
        {
            var air = new CannonAirControl();
            air.SetInput(new Vector2(side, forward), 10);
            var correction = air.ConsumeVelocity(10, false, new Vector3(0, 2, 1), 3f);
            Assert.That(Vector3.Distance(correction, new Vector3(x, 0, z)), Is.LessThan(0.0001f));
        }

        [Test]
        public void AirControl_RotatedCourseAndDiagonal_KeepHorizontalSpeedLimit()
        {
            var air = new CannonAirControl();
            air.SetInput(Vector2.up, 10);
            Assert.That(air.ConsumeVelocity(10, false, Vector3.right, 3f), Is.EqualTo(Vector3.right * 3f));
            air.SetInput(Vector2.right, 11);
            Assert.That(air.ConsumeVelocity(11, false, Vector3.right, 3f), Is.EqualTo(Vector3.back * 3f));
            air.SetInput(Vector2.one, 12);
            var diagonal = air.ConsumeVelocity(12, false, Vector3.right, 3f);
            Assert.That(diagonal.magnitude, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(diagonal.y, Is.Zero);
        }

        [Test]
        public void PoleReleaseInheritedPumpHasShortGraceButNewSideInputWorks()
        {
            var air = new CannonAirControl();
            var velocity = new Vector3(0, 5, 8);
            air.BeginPoleFlight(-1);
            for (int i = 0; i < 8; i++)
            {
                air.SetInput(Vector2.down, i);
                Assert.That(air.ConsumePoleVelocity(i, false, Vector3.forward, 3, velocity, 8, 1f / 60), Is.EqualTo(Vector3.zero));
            }
            air.SetInput(new Vector2(1, -1), 8);
            var correction = air.ConsumePoleVelocity(8, false, Vector3.forward, 3, velocity, 8, 1f / 60);
            Assert.That(correction.x, Is.GreaterThan(0));
            for (int i = 9; i < 30; i++)
            {
                air.SetInput(Vector2.down, i);
                correction = air.ConsumePoleVelocity(i, false, Vector3.forward, 3, velocity, 8, 1f / 60);
            }
            Assert.That(correction.z, Is.LessThan(-2), "Held reverse becomes braking after the grace.");
            air.BeginPoleFlight(-1);
            air.SetInput(Vector2.up, 30);
            correction = air.ConsumePoleVelocity(30, false, Vector3.forward, 3, Vector3.zero, 8, 1f / 60);
            Assert.That(correction.z, Is.GreaterThan(0), "New direction bypasses the grace, including a stationary launch.");
        }

        [TestCase(4.5f)]
        [TestCase(3.6f)]
        public void PoleAirControl_SideAdjustsLandingAndReverseBrakesWithoutFlipping(float controlSpeed)
        {
            var launched = new Vector3(0, 12, 8);
            foreach (var input in new[] { Vector2.right, Vector2.down })
            {
                var air = new CannonAirControl();
                air.BeginPoleFlight(0);
                Vector3 correction = Vector3.zero;
                for (int i = 0; i < 60; i++)
                {
                    air.SetInput(input, i);
                    correction = air.ConsumePoleVelocity(i, false, Vector3.forward, controlSpeed, launched, 8, 1f / 60);
                }
                var effective = launched + correction;
                Assert.That(effective.y, Is.EqualTo(12));
                Assert.That(effective.z, Is.GreaterThan(3));
                Assert.That(Vector3.ProjectOnPlane(effective, Vector3.up).magnitude, Is.LessThanOrEqualTo(8.001f));
                if (input.x > 0) Assert.That(effective.x, Is.GreaterThan(2.9f));
                else Assert.That(effective.z, Is.LessThan(5));
            }
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void PoleAirControl_FrameRateIndependentAndReleaseRemovesSideDrift(int fps)
        {
            var air = new CannonAirControl();
            var launched = new Vector3(0, -4, 8);
            air.BeginPoleFlight(0);
            Vector3 correction = Vector3.zero;
            for (int i = 0; i < fps; i++)
            {
                air.SetInput(Vector2.right, i);
                correction = air.ConsumePoleVelocity(i, false, Vector3.forward, 3, launched, 8, 1f / fps);
            }
            Assert.That(correction.x, Is.EqualTo(2.809f).Within(.002f));
            for (int i = fps; i < fps * 2; i++)
                correction = air.ConsumePoleVelocity(i, false, Vector3.forward, 3, launched, 8, 1f / fps);
            Assert.That(correction.magnitude, Is.LessThan(.001f));
            air.SetInput(Vector2.right, fps * 2);
            Assert.That(air.ConsumePoleVelocity(fps * 2, true, Vector3.forward, 3, launched, 8, .1f), Is.EqualTo(Vector3.zero));
            Assert.That(launched, Is.EqualTo(new Vector3(0, -4, 8)));
        }

        [Test]
        public void PoleAirControl_CameraRotationAndInjuryScaleAffectOnlyCorrection()
        {
            var air = new CannonAirControl();
            air.BeginPoleFlight(0);
            var launched = Vector3.up * 5;
            air.SetInput(Vector2.up, 1);
            var correction = air.ConsumePoleVelocity(1, false, Vector3.right, 1.5f, launched, 8, 1);
            Assert.That(correction.x, Is.EqualTo(1.5f).Within(.001f));
            Assert.That(correction.y, Is.Zero);
            Assert.That(correction.z, Is.Zero);
            air.BeginPoleFlight(0);
            Assert.That(air.ConsumePoleVelocity(2, false, Vector3.forward, 3, launched, 8, .1f), Is.EqualTo(Vector3.zero));
        }

        [TestCase(0f, 14f, 8f)]
        [TestCase(0f, -14f, 8f)]
        [TestCase(14f, 14f, 8f)]
        [TestCase(0f, 4f, 4f)]
        [TestCase(0f, 0f, 0f)]
        public void PoleJumpSpeedLimitPreservesHeightDirectionAndWeakLaunches(float x, float z, float expectedSpeed)
        {
            var velocity = new Vector3(x, 12f, z);
            var limited = CannonAirControl.LimitPoleHorizontalSpeed(velocity, 8f);
            Assert.That(limited.y, Is.EqualTo(12f));
            Assert.That(Vector3.ProjectOnPlane(limited, Vector3.up).magnitude, Is.EqualTo(expectedSpeed).Within(.0001f));
            if (expectedSpeed > 0f)
                Assert.That(Vector3.Dot(Vector3.ProjectOnPlane(limited, Vector3.up).normalized, new Vector3(x, 0f, z).normalized), Is.EqualTo(1f).Within(.0001f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void ZeroPoleJumpLimitRemovesHorizontalMotionOnly(float limit)
        {
            Assert.That(CannonAirControl.LimitPoleHorizontalSpeed(new Vector3(4f, -6f, 12f), limit), Is.EqualTo(Vector3.down * 6f));
        }

        [Test]
        public void AirControl_ReleasedOrStaleInput_DoesNotCarryIntoNextFrame()
        {
            var air = new CannonAirControl();
            air.SetInput(Vector2.right, 10);
            Assert.That(air.ConsumeVelocity(10, false, Vector3.forward, 3f), Is.EqualTo(Vector3.right * 3f));
            Assert.That(air.ConsumeVelocity(11, false, Vector3.forward, 3f), Is.EqualTo(Vector3.zero));
            air.SetInput(Vector2.right, 11);
            Assert.That(air.ConsumeVelocity(12, false, Vector3.forward, 3f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void AirControl_BlockOrRestart_DiscardsPendingCorrection()
        {
            var air = new CannonAirControl();
            air.SetInput(Vector2.one, 10);
            Assert.That(air.ConsumeVelocity(10, true, Vector3.forward, 3f), Is.EqualTo(Vector3.zero));
            Assert.That(air.ConsumeVelocity(10, false, Vector3.forward, 3f), Is.EqualTo(Vector3.zero));
            air.SetInput(Vector2.one, 11);
            air.Clear(); // 탑승·발사·ReleaseMovement(사망/R/Detach)의 입력 정리.
            Assert.That(air.ConsumeVelocity(11, false, Vector3.forward, 3f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Board_OnlyClaimsSeat_WithoutLaunching()
        {
            var state = new CannonSeatState();
            Assert.IsTrue(state.TryApply(CannonAction.Board, 1, 0));
            Assert.AreEqual(1ul, state.Occupant);
            Assert.AreEqual(1u, state.Revision);
            Assert.IsFalse(state.TryApply(CannonAction.Launch, 1, 0), "탑승 전 revision으로 발사하지 않는다");
            Assert.IsTrue(state.IsOccupied);
        }

        [Test]
        public void SimultaneousBoard_OnlyOnePlayerClaimsSeat()
        {
            var state = new CannonSeatState();
            Assert.IsTrue(state.TryApply(CannonAction.Board, 1, 0));
            Assert.IsFalse(state.TryApply(CannonAction.Board, 2, 0));
            Assert.IsFalse(state.TryApply(CannonAction.Board, 2, 1));
            Assert.AreEqual(1ul, state.Occupant);
        }

        [TestCase(CannonAction.Launch)]
        [TestCase(CannonAction.Cancel)]
        [TestCase(CannonAction.Release)]
        public void OnlyOccupantCanRelease_AndDuplicateRequestIsIgnored(CannonAction action)
        {
            var state = new CannonSeatState();
            state.TryApply(CannonAction.Board, 1, 0);
            Assert.IsFalse(state.TryApply(action, 2, 1));
            Assert.IsTrue(state.TryApply(action, 1, 1));
            Assert.IsFalse(state.TryApply(action, 1, 1));
            Assert.IsFalse(state.IsOccupied);
            Assert.AreEqual(2u, state.Revision);
        }

        [Test]
        public void OldCancel_CannotCancelLaterBoardingBySamePlayer()
        {
            var state = new CannonSeatState();
            state.TryApply(CannonAction.Board, 1, 0);
            state.TryApply(CannonAction.Cancel, 1, 1);
            state.TryApply(CannonAction.Board, 1, 2);
            Assert.IsFalse(state.TryApply(CannonAction.Cancel, 1, 1));
            Assert.AreEqual(1ul, state.Occupant);
        }

        [Test]
        public void DeathRelease_AllowsAnotherPlayerToBoard()
        {
            var state = new CannonSeatState();
            state.TryApply(CannonAction.Board, 1, 0);
            state.TryApply(CannonAction.Release, 1, 1);
            Assert.IsTrue(state.TryApply(CannonAction.Board, 2, 2));
            Assert.AreEqual(2ul, state.Occupant);
        }

        [Test]
        public void OldSnapshot_DoesNotRestoreReleasedSeat()
        {
            var state = new CannonSeatState();
            Assert.IsTrue(state.Receive(2, CannonSeatState.Nobody));
            Assert.IsFalse(state.Receive(1, 1));
            Assert.IsFalse(state.IsOccupied);
        }

        [Test]
        public void InputBeforeBoardingAcknowledgement_IsNotReused()
        {
            var input = new CannonInputLatch();
            Assert.AreEqual(CannonAction.Sync, input.Read(true, false, 0));
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0.4f));
            Assert.AreEqual(CannonAction.Sync, input.Read(false, true, 1));
            Assert.AreEqual(CannonAction.Sync, input.Read(true, false, 0));
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0));
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0));
            Assert.AreEqual(CannonAction.Launch, input.Read(true, false, 0));
        }

        [Test]
        public void LongPress_CancelsWithoutLaunch_AndNextBoardNeedsNewInput()
        {
            var input = new CannonInputLatch();
            input.Read(false, false, 0);
            input.Read(false, false, 0);
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0.5f));
            Assert.AreEqual(CannonAction.Cancel, input.Read(false, true, 1));
            Assert.AreEqual(CannonAction.Cancel, input.Read(true, true, 1));
            input.Reset();
            Assert.AreEqual(CannonAction.Sync, input.Read(true, false, 0));
        }

        [Test, Category("CommonProgressionRegression")]
        public void SpaceLaunchUsesTheSameSeatLatchAndNeedsNeutralInputAfterBoarding()
        {
            var input = new CannonInputLatch();
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0, true, true));
            for (int i = 0; i < 4; i++) Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0, false, true));
            input.Read(false, false, 0);
            input.Read(false, false, 0);
            Assert.AreEqual(CannonAction.Launch, input.Read(false, false, 0, true, true));
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0, false, true));
            Assert.AreEqual(CannonAction.Launch, input.Read(true, false, 0, true, true));
            Assert.AreEqual(CannonAction.Cancel, input.Read(false, true, 1, true, true));
            input.Reset();
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0, true, true));
        }

        [Test]
        public void InvalidActorAndSyncRequest_DoNotChangeSeat()
        {
            var state = new CannonSeatState();
            Assert.IsFalse(state.TryApply(CannonAction.Board, CannonSeatState.Nobody, 0));
            Assert.IsFalse(state.TryApply(CannonAction.Sync, 1, 0));
            Assert.IsFalse(state.TryApply(CannonAction.Launch, 1, 0));
            Assert.AreEqual(0u, state.Revision);
        }

        [Test]
        public void Landing_RequiresMatchingPlayerStationAndLaunch_AndIsAcceptedOnce()
        {
            var flights = new CannonFlightState();
            flights.Begin(1, 0, 2);
            Assert.IsFalse(flights.TryComplete(2, 0, 2));
            Assert.IsFalse(flights.TryComplete(1, 1, 2));
            Assert.IsFalse(flights.TryComplete(1, 0, 1));
            Assert.IsTrue(flights.Contains(1));
            Assert.IsTrue(flights.TryComplete(1, 0, 2));
            Assert.IsFalse(flights.TryComplete(1, 0, 2));
        }

        [Test]
        public void Restart_RejectsOldLandingEvenAfterSamePlayerLaunchesAgain()
        {
            var flights = new CannonFlightState();
            flights.Begin(1, 0, 2);
            flights.Remove(1); // HumanCannonSystem.HandleRestart가 제거한다.
            Assert.IsFalse(flights.TryComplete(1, 0, 2));
            flights.Begin(1, 0, 4);
            Assert.IsFalse(flights.TryComplete(1, 0, 2));
            Assert.IsTrue(flights.Contains(1));
            Assert.IsTrue(flights.TryComplete(1, 0, 4));
        }

        [Test]
        public void AnotherPlayerUsingSameCannon_DoesNotInvalidateFirstPlayersLanding()
        {
            var flights = new CannonFlightState();
            flights.Begin(1, 0, 2);
            flights.Begin(2, 0, 4);
            Assert.IsTrue(flights.TryComplete(1, 0, 2));
            Assert.IsTrue(flights.TryComplete(2, 0, 4));
        }

        [Test]
        public void HoldStartedOnAcknowledgementFrame_DoesNotArmInput()
        {
            var input = new CannonInputLatch();
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0));
            Assert.AreEqual(CannonAction.Sync, input.Read(false, false, 0.1f));
            Assert.AreEqual(CannonAction.Sync, input.Read(true, false, 0));
        }

        [Test]
        public void Rebound_IndependentForEachPlayer_AndRejectsDuplicateOrOldLanding()
        {
            var flights = new CannonFlightState();
            flights.Begin(1, 0, 2);
            flights.Begin(2, 0, 4);
            Assert.IsTrue(flights.TryRebound(1, 0, 2, 0));
            Assert.IsTrue(flights.TryRebound(2, 0, 4, 0));
            Assert.IsFalse(flights.TryRebound(1, 0, 2, 0));
            Assert.IsFalse(flights.TryComplete(1, 0, 2, 0));
            Assert.IsTrue(flights.TryRebound(1, 0, 2, 1), "다음 진입에서는 다시 반동할 수 있다");
            Assert.IsTrue(flights.TryComplete(1, 0, 2, 2));
            Assert.IsTrue(flights.TryComplete(2, 0, 4, 1));
        }

        [Test]
        public void RestartOrRoleHandoff_InvalidatesPendingRebound()
        {
            var flights = new CannonFlightState();
            flights.Begin(1, 0, 2);
            flights.Remove(1);
            Assert.IsFalse(flights.TryRebound(1, 0, 2, 0));
            flights.Begin(1, 0, 4);
            Assert.IsFalse(flights.TryRebound(1, 0, 2, 0));
            Assert.IsTrue(flights.TryRebound(1, 0, 4, 0));
        }
    }
}
