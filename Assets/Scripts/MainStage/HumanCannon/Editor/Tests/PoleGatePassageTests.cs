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
        public void FlyingAboveRetractedSawDoesNotWaitForGroundCarrier()
        {
            Present(PoleSawPhase.Retracted, false);
            Vector3 from = new Vector3(0f, 30f, -3f);

            Vector3 movement = Constrain(from, new Vector3(0f, 0f, 10f));

            AssertVector(new Vector3(0f, 0f, 10f), movement);
            Assert.That(gate.Side(from + movement), Is.GreaterThan(0f));
        }

        [Test]
        public void FlyingBeyondSideFenceHasNoInfiniteGatePlane()
        {
            Present(PoleSawPhase.Active, false);
            Vector3 from = new Vector3(100f, 5f, -3f);

            Vector3 movement = Constrain(from, new Vector3(0f, 0f, 10f));

            AssertVector(new Vector3(0f, 0f, 10f), movement);
            Assert.That(gate.Side(from + movement), Is.GreaterThan(0f));
        }

        [Test]
        public void DiagonalFlightKeepsItsEntireRequestedMovement()
        {
            Present(PoleSawPhase.Warning, false);

            Vector3 movement = Constrain(new Vector3(5f, 9f, -3f), new Vector3(7f, -2f, 10f));

            AssertVector(new Vector3(7f, -2f, 10f), movement);
        }

        [Test]
        public void TranslatedQuarterTurnGateHasNoVirtualForwardPlane()
        {
            gateObject.transform.SetPositionAndRotation(new Vector3(12f, 0f, 8f), Quaternion.Euler(0f, 90f, 0f));
            Present(PoleSawPhase.Retracted, false);
            Vector3 from = new Vector3(9f, 5f, 8f);

            Vector3 movement = Constrain(from, new Vector3(10f, 2f, 7f));

            AssertVector(new Vector3(10f, 2f, 7f), movement);
            Assert.That(gate.Side(from + movement), Is.GreaterThan(0f));
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
        public void StartingInsideFormerMarginKeepsForwardFlight()
        {
            Present(PoleSawPhase.Retracted, false);
            Vector3 from = new Vector3(0f, 5f, -0.1f);

            Vector3 movement = Constrain(from, new Vector3(2f, 3f, 4f));

            AssertVector(new Vector3(2f, 3f, 4f), movement);
            Assert.That(gate.Side(from + movement), Is.GreaterThan(0f));
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
        public void ResetPreservesGateStateWithoutRestoringAnAirPlane()
        {
            Present(PoleSawPhase.Retracted, true);
            typeof(HumanCannonSawGate).GetMethod("ResetGate", InternalInstance).Invoke(gate, null);

            Vector3 movement = Constrain(new Vector3(0f, 5f, -3f), new Vector3(0f, 0f, 10f));

            AssertVector(new Vector3(0f, 0f, 10f), movement);
            Assert.That(gate.CarrierPassed, Is.False);
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Active));
        }

        [TestCase(0f)]
        [TestCase(6f)]
        public void ReleasedAndWarningSaw_StopInPlaceAndAllowPassageBeforeActiveRotationReturns(float floorHeight)
        {
            gateObject.transform.position = Vector3.up * floorHeight;
            var visual = new GameObject("SawVisual").transform;
            visual.SetParent(gateObject.transform, false);
            visual.localPosition = new Vector3(0f, 1.2f, -0.4f);
            var rotor = new GameObject("GateSawDisc0").transform;
            rotor.SetParent(visual, false);
            var barrier = new GameObject("Barrier").AddComponent<BoxCollider>();
            var wallRenderer = barrier.gameObject.AddComponent<MeshRenderer>();
            var fenceRenderer = new GameObject("VisibleFence").AddComponent<MeshRenderer>();
            fenceRenderer.transform.SetParent(barrier.transform, false);
            barrier.transform.SetParent(gateObject.transform, false);
            barrier.center = Vector3.up * 1.2f;
            barrier.size = new Vector3(30f, 2.4f, 0.6f);
            typeof(HumanCannonSawGate).GetField("sawVisual", InternalInstance).SetValue(gate, visual);
            typeof(HumanCannonSawGate).GetField("barrier", InternalInstance).SetValue(gate, barrier);
            typeof(HumanCannonSawGate).GetMethod("Awake", InternalInstance).Invoke(gate, null);

            Present(PoleSawPhase.Active, false, 0.25d);
            Assert.That(visual.position.y, Is.EqualTo(floorHeight + 1.2f).Within(0.001f));
            Assert.That(barrier.enabled, Is.True);
            Assert.That(wallRenderer.enabled, Is.False);
            Assert.That(fenceRenderer.enabled, Is.True);
            var stoppedRotation = rotor.localRotation;

            Present(PoleSawPhase.Retracted, false, 1d);
            Assert.That(visual.position.y, Is.EqualTo(floorHeight + 1.2f).Within(0.001f));
            Assert.That(Quaternion.Angle(rotor.localRotation, stoppedRotation), Is.LessThan(0.001f));
            Assert.That(barrier.enabled, Is.False);
            Assert.That(fenceRenderer.enabled, Is.False);

            Present(PoleSawPhase.Warning, true, 2d);
            Assert.That(visual.position.y, Is.EqualTo(floorHeight + 1.2f).Within(0.001f));
            Assert.That(Quaternion.Angle(rotor.localRotation, stoppedRotation), Is.LessThan(0.001f));
            Assert.That(barrier.enabled, Is.False, "예고 중에는 지상 담당자에게 피해를 주지 않는다.");
            Assert.That(fenceRenderer.enabled, Is.False);

            Present(PoleSawPhase.Active, true, 2.5d);
            Assert.That(barrier.enabled, Is.True);
            Assert.That(visual.position.y, Is.EqualTo(floorHeight + 1.2f).Within(0.001f));
            Assert.That(Quaternion.Angle(rotor.localRotation, stoppedRotation), Is.GreaterThan(1f));
            Assert.That(wallRenderer.enabled, Is.False);
            Assert.That(fenceRenderer.enabled, Is.True);
        }

        void Present(PoleSawPhase phase, bool passed, double time = 0d)
        {
            typeof(HumanCannonSawGate).GetMethod("Present", InternalInstance)
                .Invoke(gate, new object[] { phase, passed, time });
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
