using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class PoleGatePassageTests
    {
        const float Radius = 0.4f;
        const BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject gateObject;
        HumanCannonSawGate gate;

        [SetUp]
        public void SetUp()
        {
            gateObject = new GameObject("PoleGatePassageTest");
            gate = gateObject.AddComponent<HumanCannonSawGate>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gateObject);
        }

        [Test]
        public void FlyingAboveRetractedSawStillWaitsForGroundCarrier()
        {
            Present(PoleSawPhase.Retracted, false);
            Vector3 from = new Vector3(0f, 30f, -3f);

            Vector3 movement = Constrain(from, new Vector3(0f, 0f, 10f));

            AssertVector(new Vector3(0f, 0f, 2.6f), movement);
            Assert.That(gate.Side(from + movement), Is.EqualTo(-Radius).Within(0.0001f));
        }

        [Test]
        public void FlyingBeyondSideFenceCannotBypassRequiredPassage()
        {
            Present(PoleSawPhase.Active, false);
            Vector3 from = new Vector3(100f, 5f, -3f);

            Vector3 movement = Constrain(from, new Vector3(0f, 0f, 10f));

            AssertVector(new Vector3(0f, 0f, 2.6f), movement);
            Assert.That(gate.Side(from + movement), Is.LessThan(0f));
        }

        [Test]
        public void BlockedDiagonalFlightKeepsLateralAndVerticalMovement()
        {
            Present(PoleSawPhase.Warning, false);

            Vector3 movement = Constrain(new Vector3(5f, 9f, -3f), new Vector3(7f, -2f, 10f));

            AssertVector(new Vector3(7f, -2f, 2.6f), movement);
        }

        [Test]
        public void TranslatedQuarterTurnGateConstrainsItsOwnForwardAxis()
        {
            gateObject.transform.SetPositionAndRotation(new Vector3(12f, 0f, 8f), Quaternion.Euler(0f, 90f, 0f));
            Present(PoleSawPhase.Retracted, false);
            Vector3 from = new Vector3(9f, 5f, 8f);

            Vector3 movement = Constrain(from, new Vector3(10f, 2f, 7f));

            AssertVector(new Vector3(2.6f, 2f, 7f), movement);
            Assert.That(gate.Side(from + movement), Is.EqualTo(-Radius).Within(0.0001f));
        }

        [Test]
        public void FlightBackTowardTrampolineMayCrossBeforeCarrierPasses()
        {
            Present(PoleSawPhase.Active, false);
            Vector3 requested = new Vector3(-2f, 1f, -8f);

            Vector3 movement = Constrain(new Vector3(4f, 5f, 3f), requested);

            AssertVector(requested, movement);
        }

        [Test]
        public void StartingInsideRadiusMarginDoesNotPermitForwardBypass()
        {
            Present(PoleSawPhase.Retracted, false);
            Vector3 from = new Vector3(0f, 5f, -0.1f);

            Vector3 movement = Constrain(from, new Vector3(2f, 3f, 4f));

            AssertVector(new Vector3(2f, 3f, 0f), movement);
            Assert.That(gate.Side(from + movement), Is.EqualTo(-0.1f).Within(0.0001f));
        }

        [Test]
        public void CarrierPassUnlocksForwardFlightEvenAfterSawReactivates()
        {
            Present(PoleSawPhase.Active, true);
            Vector3 requested = new Vector3(2f, 3f, 10f);

            Vector3 movement = Constrain(new Vector3(0f, 5f, -3f), requested);

            AssertVector(requested, movement);
            Assert.That(gate.CarrierPassed, Is.True);
        }

        [Test]
        public void ResetRequiresGroundCarrierPassAgain()
        {
            Present(PoleSawPhase.Retracted, true);
            typeof(HumanCannonSawGate).GetMethod("ResetGate", InternalInstance).Invoke(gate, null);

            Vector3 movement = Constrain(new Vector3(0f, 5f, -3f), new Vector3(0f, 0f, 10f));

            AssertVector(new Vector3(0f, 0f, 2.6f), movement);
            Assert.That(gate.CarrierPassed, Is.False);
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Active));
        }

        void Present(PoleSawPhase phase, bool passed)
        {
            typeof(HumanCannonSawGate).GetMethod("Present", InternalInstance)
                .Invoke(gate, new object[] { phase, passed, 0d });
        }

        Vector3 Constrain(Vector3 from, Vector3 displacement)
        {
            return (Vector3)typeof(HumanCannonSawGate).GetMethod("ConstrainFlight", InternalInstance)
                .Invoke(gate, new object[] { from, displacement, Radius });
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f), "x movement");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f), "y movement");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f), "z movement");
        }
    }
}
