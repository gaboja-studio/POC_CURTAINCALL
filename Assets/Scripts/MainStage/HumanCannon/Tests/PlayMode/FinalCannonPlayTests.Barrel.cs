using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator BarrelMotion_ExternalOnSkipsRestAfterLaunch()
        {
            DisableEnhancementThreats();
            var station = (Component)Call(system, "Station", 0);
            yield return Board(0); yield return ReleaseThenArm(); Input(tapped: true); yield return new WaitForSeconds(1.35f);
            Assert.AreEqual("Inactive", Get(station, "BarrelPhase").ToString());
            var gate = station.GetType().GetProperty("InteractionAvailable");
            gate.SetValue(station, false); yield return null;
            gate.SetValue(station, true); yield return null;
            Assert.AreEqual("Recover", Get(station, "BarrelPhase").ToString());
            Assert.IsFalse((bool)Get(station, "ReadyToUse"));
            yield return WaitUntil(() => (bool)Get(station, "ReadyToUse"), 4, "external activation recovery");
        }

        [UnityTest]
        public IEnumerator BarrelMotion_ActualLaunchCyclesOnceKeepsAnchorsAndBecomesBoardable()
        {
            DisableEnhancementThreats();
            var station = (Component)Call(system, "Station", 0);
            var view = station.GetComponentInChildren(GameType("CurtainCall.HumanCannon.CannonBarrelPresentation"));
            Assert.IsNotNull(view); Assert.IsTrue((bool)Get(station, "BarrelMotionEnabled"));
            var muzzle = (Transform)Get(station, "Muzzle"); var seat = (Transform)Get(station, "Seat");
            Vector3 muzzlePosition = muzzle.position, seatPosition = seat.position;
            yield return Board(0); yield return ReleaseThenArm(); Input(tapped: true); yield return null;
            Assert.IsTrue(Flying); Assert.IsFalse((bool)Get(station, "ReadyToUse"));
            Assert.AreEqual("FireDisable", Get(station, "BarrelPhase").ToString());
            double fired = (double)Get(station, "LastFiredAt");
            var phases = new HashSet<string>(); float deadline = Time.realtimeSinceStartup + 6;
            var pivot = (Transform)Get(view, "BarrelPivot"); Quaternion first = pivot.localRotation; bool moved = false;
            do
            {
                phases.Add(Get(view, "Phase").ToString()); moved |= Quaternion.Angle(first, pivot.localRotation) > 5;
                Assert.Less(Vector3.Distance(muzzlePosition, muzzle.position), .001f);
                Assert.Less(Vector3.Distance(seatPosition, seat.position), .001f);
                Assert.AreEqual(fired, (double)Get(station, "LastFiredAt"), "No retrigger while the same flight continues");
                yield return null;
            } while (!(bool)Get(station, "ReadyToUse") && Time.realtimeSinceStartup < deadline);
            yield return null; phases.Add(Get(view, "Phase").ToString());
            Assert.IsTrue(moved); Assert.IsTrue((bool)Get(station, "ReadyToUse"));
            CollectionAssert.IsSubsetOf(new[] { "FireDisable", "Inactive", "Recover", "Active" }, phases);
        }

        [UnityTest]
        public IEnumerator BarrelMotion_ExternalLockWaitsForRecoveryAndRepeatedUpdatesDoNotRestart()
        {
            DisableEnhancementThreats();
            var station = (Component)Call(system, "Station", 0);
            var gate = station.GetType().GetProperty("InteractionAvailable");
            gate.SetValue(station, false); yield return new WaitForSeconds(.2f);
            Assert.AreEqual("Inactive", Get(station, "BarrelPhase").ToString());
            Assert.IsFalse((bool)Get(station, "ReadyToUse"));
            var boarding = (Transform)Get(station, "BoardingPoint");
            Call(mover, "SetStartPose", boarding.position, Quaternion.identity, true); yield return null;
            Assert.AreEqual(-1, Call(system, "FindBoardableStation", player));
            gate.SetValue(station, true); yield return null;
            Assert.AreEqual("Recover", Get(station, "BarrelPhase").ToString());
            Assert.AreEqual(-1, Call(system, "FindBoardableStation", player));
            float deadline = Time.realtimeSinceStartup + 4;
            while (!(bool)Get(station, "ReadyToUse") && Time.realtimeSinceStartup < deadline)
            {
                gate.SetValue(station, true); yield return null;
            }
            Assert.IsTrue((bool)Get(station, "ReadyToUse"));
            Assert.AreEqual(0, Call(system, "FindBoardableStation", player));
            yield return Board(0);
            Assert.IsTrue((bool)Call(system, "IsSeated", Actor));
        }

        [UnityTest]
        public IEnumerator BarrelMotion_DisabledStationRecoversWhenEnabledAgain()
        {
            DisableEnhancementThreats();
            var station = (Behaviour)Call(system, "Station", 0);
            station.enabled = false; yield return new WaitForSeconds(.2f);
            Assert.AreEqual("Inactive", Get(station, "BarrelPhase").ToString());
            Assert.IsFalse((bool)Get(station, "ReadyToUse"));
            Assert.IsTrue((bool)Get(station, "InteractionAvailable"), "Component disable must not replace the external safety lock");
            station.enabled = true; yield return null;
            Assert.AreEqual("Recover", Get(station, "BarrelPhase").ToString());
            yield return WaitUntil(() => (bool)Get(station, "ReadyToUse"), 4, "re-enable recovery");
            Assert.AreEqual("Active", Get(station, "BarrelPhase").ToString());
        }
    }
}
