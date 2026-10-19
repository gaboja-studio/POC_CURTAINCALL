using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
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

        [TestCase(4.5f)]
        [TestCase(3.6f)]
        public void PoleAirControl_OppositeOrSideInputRemovesUnwantedMomentum(float controlSpeed)
        {
            var launched = new Vector3(0, 12, 14);
            var reverse = CannonAirControl.SteerPoleMomentum(launched, Vector3.back * controlSpeed);
            Assert.That(reverse, Is.EqualTo(Vector3.up * 12));
            Assert.That((reverse + Vector3.back * controlSpeed).z, Is.LessThan(0));
            var sideways = CannonAirControl.SteerPoleMomentum(launched, Vector3.right * controlSpeed);
            Assert.That(sideways, Is.EqualTo(Vector3.up * 12));
            Assert.That(CannonAirControl.SteerPoleMomentum(reverse, Vector3.forward * controlSpeed),
                Is.EqualTo(reverse), "입력 방향을 되돌려도 제동한 반동이 되살아나지 않는다.");
        }

        [Test]
        public void PoleAirControl_NoInputOrForwardInputPreservesLongJump_AndTurningCannotAddEnergy()
        {
            var launched = new Vector3(0, 12, 14);
            Assert.That(CannonAirControl.SteerPoleMomentum(launched, Vector3.zero), Is.EqualTo(launched));
            Assert.That(CannonAirControl.SteerPoleMomentum(launched, Vector3.forward * 4.5f), Is.EqualTo(launched));
            var diagonal = new Vector3(1, 0, 1).normalized;
            var turned = CannonAirControl.SteerPoleMomentum(launched, diagonal * 4.5f);
            Assert.That(turned.y, Is.EqualTo(12));
            Assert.That(Vector3.Dot(Vector3.ProjectOnPlane(turned, Vector3.up).normalized, diagonal), Is.EqualTo(1).Within(0.001f));
            Assert.That(Vector3.ProjectOnPlane(turned, Vector3.up).magnitude, Is.LessThan(14));
            var rotation = Quaternion.Euler(0, 135, 0);
            Assert.That(Vector3.Distance(CannonAirControl.SteerPoleMomentum(rotation * launched, rotation * diagonal),
                rotation * turned), Is.LessThan(0.001f), "카메라/코스가 돌아가도 같은 세계 방향으로 제동");
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
