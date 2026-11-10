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
        public void GrabOpensWallAndReleaseRestoresClosedWallAndFallenBlades(float floorHeight)
        {
            gateObject.transform.position = Vector3.up * floorHeight;
            var visual = new GameObject("SawVisual").transform;
            visual.SetParent(gateObject.transform, false);
            visual.localPosition = new Vector3(0f, 1.2f, 0f);
            var left = Half(visual, "LeftBlades", -1f, out var leftRotor, out var leftOuter);
            var right = Half(visual, "RightBlades", 1f, out _, out var rightOuter);
            // 지상 바닥은 x -10~10. 열린 뒤 바깥 톱(±20)만 바닥 밖에 놓인다.
            var floor = new GameObject("Floor").AddComponent<BoxCollider>();
            floor.center = new Vector3(0f, floorHeight - 0.5f, 0f);
            floor.size = new Vector3(20f, 1f, 4f);
            Set("sawVisual", visual);
            Set("leftParts", new[] { left });
            Set("rightParts", new[] { right });
            Set("openGap", 6f);
            typeof(HumanCannonSawGate).GetMethod("Awake", InternalInstance).Invoke(gate, null);

            Present(PoleSawPhase.Active, false, 0.25d);
            Animate(10f);
            Assert.That(gate.IsOpen, Is.False);
            Assert.That(Inner(right, -1f) - Inner(left, 1f), Is.LessThan(0.01f), "닫힌 벽은 가운데 틈이 없다.");
            var spun = leftRotor.localRotation;

            Present(PoleSawPhase.Retracted, false, 1d);
            Assert.That(gate.IsOpen, Is.True);
            Assert.That(gate.IsSpinning, Is.False, "잡는 순간 톱이 멈춘다.");
            Assert.That(Quaternion.Angle(leftRotor.localRotation, spun), Is.LessThan(.001f), "멈춘 각도를 유지한다.");
            Animate(0.5f);
            Assert.That(gate.OpenAmount, Is.Zero, "멈춘 뒤 0.5초 동안은 갈라지지 않는다.");
            Animate(0.5f);
            Assert.That(gate.OpenAmount, Is.EqualTo(0.5f).Within(0.001f), "그 다음 1초에 걸쳐 갈라진다.");
            Assert.That((left.localPosition - new Vector3(-1.5f, 0f, 0f)).magnitude, Is.GreaterThan(0.001f), "갈라지는 동안 덜컹거린다.");
            Present(PoleSawPhase.Retracted, false, 1.6d);
            Assert.That(Quaternion.Angle(leftRotor.localRotation, spun), Is.LessThan(.001f), "갈라지는 동안에도 멈춰 있다.");
            Animate(1f);
            Assert.That(Inner(left, 1f), Is.EqualTo(-3f).Within(0.01f));
            Assert.That(Inner(right, -1f), Is.EqualTo(3f).Within(0.01f));
            Assert.That(gate.IsSpinning, Is.True, "다 열리면 다시 돈다.");
            Present(PoleSawPhase.Retracted, false, 1.8d);
            Assert.That(Quaternion.Angle(leftRotor.localRotation, spun), Is.LessThan(.001f), "멈췄던 각도에서 이어서 돈다.");
            Present(PoleSawPhase.Retracted, false, 2.05d);
            Assert.That(Quaternion.Angle(leftRotor.localRotation, spun), Is.GreaterThan(1f), "갈라진 톱은 다시 돈다.");
            Assert.That(gate.FallingBladeCount, Is.EqualTo(2), "바닥 밖 바깥 톱만 떨어진다.");
            Assert.That(leftOuter.position.y, Is.LessThan(floorHeight), "바깥 톱은 아래로 떨어진다.");
            Assert.That(leftRotor.position.y, Is.EqualTo(floorHeight + 1.2f).Within(.001f), "바닥 위 톱은 제자리를 지킨다.");
            Assert.That(left.position.y, Is.EqualTo(floorHeight + 1.2f).Within(0.001f));

            Present(PoleSawPhase.Active, false, 2.5d);
            Animate(10f);
            Assert.That(gate.IsOpen, Is.False, "직접 잡은 사람이 놓으면 스위치가 즉시 해제된다.");
            Assert.That(Inner(right, -1f) - Inner(left, 1f), Is.LessThan(.01f));
            Assert.That(gate.GoneBladeCount, Is.Zero);
            Assert.That(leftOuter.gameObject.activeSelf && rightOuter.gameObject.activeSelf, Is.True);
            Assert.That(leftRotor.gameObject.activeSelf, Is.True);

            typeof(HumanCannonSawGate).GetMethod("ResetGate", InternalInstance).Invoke(gate, null);
            Assert.That(gate.IsOpen, Is.False, "재시작하면 닫힌 벽으로 돌아온다.");
            Assert.That(left.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(right.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(leftOuter.gameObject.activeSelf, Is.True, "재시작하면 떨어진 톱도 제자리로 돌아온다.");
            Assert.That(leftOuter.localPosition.x, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(leftOuter.position.y, Is.EqualTo(floorHeight + 1.2f).Within(0.001f));
            Assert.That(gate.FallingBladeCount + gate.GoneBladeCount, Is.Zero);
            Object.DestroyImmediate(floor.gameObject);
        }

        [Test]
        public void ClientOpenFlagOpensTheWallWhileThePhaseIsAlreadyActive()
        {
            Present(PoleSawPhase.Active, false, true);
            Assert.That(gate.IsOpen, Is.True, "늦게 들어온 클라이언트도 열린 벽을 본다.");
        }

        Transform Half(Transform visual, string name, float sign, out Transform rotor, out Transform outer)
        {
            var half = new GameObject(name).transform;
            half.SetParent(visual, false);
            var wall = half.gameObject.AddComponent<BoxCollider>();
            wall.center = new Vector3(sign * 11f, 1.2f, 0f);
            wall.size = new Vector3(22f, 4.8f, 0.6f);
            rotor = new GameObject("GateSawDisc" + (sign < 0 ? 0 : 1)).transform;
            rotor.SetParent(half, false);
            rotor.localPosition = new Vector3(sign * 0.5f, 0f, 0f);
            outer = new GameObject("GateSawDisc" + (sign < 0 ? 2 : 3)).transform;
            outer.SetParent(half, false);
            outer.localPosition = new Vector3(sign * 20f, 0f, 0f);
            return half;
        }

        float Inner(Transform half, float sign)
        {
            Physics.SyncTransforms();
            var bounds = half.GetComponent<Collider>().bounds;
            return sign > 0 ? bounds.max.x : bounds.min.x;
        }

        void Set(string field, object value) =>
            typeof(HumanCannonSawGate).GetField(field, InternalInstance).SetValue(gate, value);

        void Animate(float seconds) =>
            typeof(HumanCannonSawGate).GetMethod("Animate", InternalInstance).Invoke(gate, new object[] { seconds });

        void Present(PoleSawPhase phase, bool passed, bool open) =>
            typeof(HumanCannonSawGate).GetMethod("Present", InternalInstance).Invoke(gate, new object[] { phase, passed, open, 0d });

        void Present(PoleSawPhase phase, bool passed, double time = 0d)
        {
            typeof(HumanCannonSawGate).GetMethod("Present", InternalInstance)
                .Invoke(gate, new object[] { phase, passed, phase == PoleSawPhase.Retracted, time });
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
