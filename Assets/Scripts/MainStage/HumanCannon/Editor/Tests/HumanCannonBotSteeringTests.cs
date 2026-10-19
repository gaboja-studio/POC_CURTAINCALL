using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class HumanCannonBotSteeringTests
    {
        [TestCase(1f, 0f, 1f, 0f)]
        [TestCase(-1f, 0f, -1f, 0f)]
        [TestCase(0f, 1f, 0f, 1f)]
        [TestCase(0f, -1f, 0f, -1f)]
        public void PlanarInput_ProducesSideAndForwardInput(float x, float z, float side, float forward)
        {
            Vector2 input = HumanCannonBotSteering.PlanarInput(new Vector3(3, 2, 7),
                new Vector3(3 + x, 90, 7 + z), Vector3.forward);
            Assert.That(input.x, Is.EqualTo(side).Within(0.00001));
            Assert.That(input.y, Is.EqualTo(forward).Within(0.00001));
        }

        [Test]
        public void PlanarInput_RotatesWithFacingAndIgnoresPitch()
        {
            Vector2 input = HumanCannonBotSteering.PlanarInput(Vector3.zero,
                new Vector3(1, 30, 1), new Vector3(5, 12, 0));
            Assert.That(input.x, Is.EqualTo(-Mathf.Sqrt(0.5f)).Within(0.00001));
            Assert.That(input.y, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.00001));
            Assert.That(input.magnitude, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void PlanarInput_StopsAtBoundaryAndDoesNotChaseHeight()
        {
            Assert.That(HumanCannonBotSteering.PlanarInput(Vector3.zero,
                new Vector3(0.35f, 100, 0), Vector3.forward), Is.EqualTo(Vector2.zero));
            Assert.That(HumanCannonBotSteering.PlanarInput(Vector3.zero,
                Vector3.up * 100, Vector3.forward, 0), Is.EqualTo(Vector2.zero));
            Assert.That(HumanCannonBotSteering.PlanarInput(Vector3.zero,
                Vector3.right * 0.36f, Vector3.forward), Is.EqualTo(Vector2.right));
        }

        [Test]
        public void PlanarInput_RejectsInvalidPositionsOrMissingHorizontalFacing()
        {
            foreach (Vector3 facing in new[] { Vector3.zero, Vector3.up,
                new Vector3(float.NaN, 0, 1), new Vector3(0, float.PositiveInfinity, 1) })
                Assert.That(HumanCannonBotSteering.PlanarInput(Vector3.zero, Vector3.right, facing),
                    Is.EqualTo(Vector2.zero));
            Assert.That(HumanCannonBotSteering.PlanarInput(new Vector3(float.NaN, 0, 0),
                Vector3.right, Vector3.forward), Is.EqualTo(Vector2.zero));
            Assert.That(HumanCannonBotSteering.PlanarInput(Vector3.zero,
                new Vector3(1, 0, float.PositiveInfinity), Vector3.forward), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void PlanarInput_LargeFiniteCoordinatesStillProduceBoundedDirection()
        {
            Vector2 input = HumanCannonBotSteering.PlanarInput(
                new Vector3(-float.MaxValue, 0, -float.MaxValue),
                new Vector3(float.MaxValue, 0, float.MaxValue),
                new Vector3(0, 0, float.MaxValue), float.NaN);
            Assert.That(input.x, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.00001));
            Assert.That(input.y, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.00001));
            Assert.That(input.magnitude, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void PredictCatch_UsesFallingIntersectionInWorldCoordinates()
        {
            Near(HumanCannonBotSteering.PredictCatch(new Vector3(3, 12, 7),
                new Vector3(4, -5, -2), 2, 10), new Vector3(7, 2, 5));
        }

        [Test]
        public void PredictCatch_RisingFromSurfaceUsesReturnInsteadOfImmediateIntersection()
        {
            Near(HumanCannonBotSteering.PredictCatch(new Vector3(3, 2, 7),
                new Vector3(4, 10, -2), 2, 10, 5), new Vector3(11, 2, 3));
        }

        [Test]
        public void PredictCatch_RisingFromBelowSurfaceChoosesLaterDescendingRoot()
        {
            // y(t) = -10 + 15t - 5t²: ascending t=1, descending t=2.
            Near(HumanCannonBotSteering.PredictCatch(new Vector3(3, -10, 7),
                new Vector3(4, 15, -2), 0, 10, 5), new Vector3(11, 0, 3));
        }

        [Test]
        public void PredictCatch_LimitsHorizontalLeadToPredictionHorizon()
        {
            Near(HumanCannonBotSteering.PredictCatch(new Vector3(3, 80, 7),
                new Vector3(4, 0, -2), 0, 10), new Vector3(11, 0, 3));
            Near(HumanCannonBotSteering.PredictCatch(new Vector3(3, 80, 7),
                new Vector3(4, 0, -2), 0, 10, 0.5f), new Vector3(5, 0, 6));
        }

        [Test]
        public void PredictCatch_NoFutureIntersectionKeepsCurrentHorizontalPosition()
        {
            var feet = new Vector3(3, -10, 7);
            Near(HumanCannonBotSteering.PredictCatch(feet, new Vector3(4, -5, -2), 0, 10),
                new Vector3(3, 0, 7));
            Near(HumanCannonBotSteering.PredictCatch(feet, new Vector3(4, 1, -2), 0, 10),
                new Vector3(3, 0, 7));
        }

        [Test]
        public void PredictCatch_FastDescentDoesNotLoseSmallPositiveTime()
        {
            Near(HumanCannonBotSteering.PredictCatch(new Vector3(3, 1, 7),
                new Vector3(1e20f, -1e20f, 0), 0, 10), new Vector3(4, 0, 7));
        }

        [Test]
        public void PredictCatch_InvalidPhysicsOrHorizonDoesNotExtrapolate()
        {
            var feet = new Vector3(3, 12, 7);
            var velocity = new Vector3(4, -5, -2);
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Near(HumanCannonBotSteering.PredictCatch(feet, velocity, 2, invalid), new Vector3(3, 2, 7));
                Near(HumanCannonBotSteering.PredictCatch(feet, velocity, 2, 10, invalid), new Vector3(3, 2, 7));
            }
            Near(HumanCannonBotSteering.PredictCatch(feet, new Vector3(float.NaN, -5, -2), 2, 10),
                new Vector3(3, 2, 7));
        }

        [Test]
        public void CatchAndCarrier_MalformedOrOverflowingCoordinatesStayFinite()
        {
            var malformed = new Vector3(float.NaN, float.PositiveInfinity, 7);
            Near(HumanCannonBotSteering.PredictCatch(malformed, Vector3.zero, float.NaN, 10),
                new Vector3(0, 0, 7));
            Near(HumanCannonBotSteering.CarrierFeet(malformed, Vector3.forward, 3),
                new Vector3(0, 0, 7));
            var extreme = new Vector3(float.MaxValue, 20, 7);
            Assert.That(HumanCannonBotSteering.PredictCatch(extreme,
                new Vector3(float.MaxValue, 0, 0), 0, 10).x, Is.EqualTo(float.MaxValue));
            Assert.That(HumanCannonBotSteering.CarrierFeet(extreme, Vector3.left, float.MaxValue),
                Is.EqualTo(extreme));
        }

        [Test]
        public void CarrierFeet_PreservesHeightAndPlacesSurfaceAheadAtHandleDistance()
        {
            var surface = new Vector3(10, 4, 20);
            Vector3 facing = new Vector3(3, 100, 4);
            Vector3 feet = HumanCannonBotSteering.CarrierFeet(surface, facing, 2.5f);
            Near(feet, new Vector3(8.5f, 4, 18));
            Assert.That(TrampolineCarryPose.TryCalculate(feet, facing, new Vector3(-2.5f, 0, 0),
                out Vector3 carriedSurface, out _), Is.True);
            Near(carriedSurface, surface);
        }

        [Test]
        public void CarrierFeet_InvalidFacingOrDistanceDoesNotInventAnOffset()
        {
            var surface = new Vector3(10, 4, 20);
            foreach (Vector3 facing in new[] { Vector3.zero, Vector3.up, new Vector3(float.NaN, 0, 1) })
                Near(HumanCannonBotSteering.CarrierFeet(surface, facing, 2.5f), surface);
            foreach (float distance in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Near(HumanCannonBotSteering.CarrierFeet(surface, Vector3.forward, distance), surface);
        }

        static void Near(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
