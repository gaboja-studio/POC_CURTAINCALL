using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("November11CommonFix")]
    public class TrampolineCarryPoseTests
    {
        [Test]
        public void CarriedBodyCannotSkipAcrossAThinWallBetweenClearEndpoints()
        {
            var root = new GameObject("Carry sweep probe");
            var wall = new GameObject("Thin wall");
            try {
                root.transform.position = new Vector3(12000, 100, 12000);
                var body = root.AddComponent<BoxCollider>(); body.size = new Vector3(1, .5f, 1);
                var obstacle = wall.AddComponent<BoxCollider>(); obstacle.size = new Vector3(4, 4, .05f);
                wall.transform.position = root.transform.position + Vector3.forward * 2;
                Physics.SyncTransforms();
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.forward, Quaternion.identity, null));
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.forward * 4, Quaternion.identity, null));
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.forward * 4, Quaternion.identity, null, false));
            } finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wall); }
        }

        [Test]
        public void RotatingCarriedBodyCannotSweepThroughAnObstacle()
        {
            var root = new GameObject("Turning carry probe");
            var wall = new GameObject("Corner obstacle");
            try {
                root.transform.position = new Vector3(12000, 100, 12000);
                var body = root.AddComponent<BoxCollider>(); body.size = new Vector3(4, .5f, .3f);
                wall.AddComponent<BoxCollider>().size = Vector3.one * .2f;
                wall.transform.position = root.transform.position + new Vector3(1.3f, 0, -1.3f);
                Physics.SyncTransforms();
                var turn = Quaternion.Euler(0, 90, 0);
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position, turn, null, false));
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, root.transform.position, turn, null));
            } finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wall); }
        }

        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        public void CardinalFacing_PutsCenterInFrontAndHandleAtCarrier(float x, float z)
        {
            var carrier = new Vector3(3, 2, 7);
            var forward = new Vector3(x, 0, z);
            var handle = new Vector3(-2.8f, 0, 0);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier, forward, handle, out var position, out var rotation));
            Near(position, carrier + forward * 2.8f);
            Near(position + rotation * handle, carrier);
            Near(rotation * Vector3.up, Vector3.up);
        }

        [Test]
        public void DifferentApproachPositions_KeepTheHandleDefinedDistance()
        {
            var firstCarrier = new Vector3(-13, 4, 11);
            var secondCarrier = new Vector3(7, -2, -8);
            var forward = new Vector3(1, 5, 1);
            var handle = new Vector3(-2.8f, 0, 0);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(firstCarrier, forward, handle, out var first, out _));
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(secondCarrier, forward, handle, out var second, out _));
            Vector3 expected = new Vector3(1, 0, 1).normalized * 2.8f;
            Near(first - firstCarrier, expected);
            Near(second - secondCarrier, expected);
        }

        [Test]
        public void JumpHeight_MovesDeviceBySameHeightWithoutChangingRotation()
        {
            var carrier = new Vector3(1, 0, 2);
            var handle = new Vector3(-2.8f, 0.4f, 0);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier, Vector3.forward, handle, out var grounded, out var groundedRotation));
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier + Vector3.up * 1.5f, Vector3.forward, handle,
                out var airborne, out var airborneRotation));
            Near(airborne - grounded, Vector3.up * 1.5f);
            Assert.That(airborneRotation, Is.EqualTo(groundedRotation));
            Near(airborne + airborneRotation * handle, carrier + Vector3.up * 1.5f);
        }

        [Test]
        public void OffsetHandleAndPitchedForward_KeepAnchorExactAndDeviceLevel()
        {
            var carrier = new Vector3(4, 2, -3);
            var handle = new Vector3(-2, 0.3f, 1.5f);
            Assert.IsTrue(TrampolineCarryPose.TryCalculate(carrier, new Vector3(-2, 20, 3), handle,
                out var position, out var rotation));
            Near(position + rotation * handle, carrier);
            Near(rotation * Vector3.up, Vector3.up);
            Near(Vector3.ProjectOnPlane(position - carrier, Vector3.up), new Vector3(-2, 0, 3).normalized * 2.5f);
        }

        [Test]
        public void MissingHorizontalDirectionOrNonFiniteVectors_AreRejected()
        {
            var handle = new Vector3(-2.8f, 0, 0);
            Invalid(Vector3.zero, Vector3.zero, handle);
            Invalid(Vector3.zero, Vector3.up, handle);
            Invalid(Vector3.zero, Vector3.forward, Vector3.zero);
            Invalid(Vector3.zero, Vector3.forward, Vector3.up);
            Invalid(new Vector3(float.NaN, 0, 0), Vector3.forward, handle);
            Invalid(Vector3.zero, new Vector3(0, float.PositiveInfinity, 1), handle);
            Invalid(Vector3.zero, Vector3.forward, new Vector3(-2.8f, 0, float.NegativeInfinity));
        }

        static void Near(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));

        static void Invalid(Vector3 carrier, Vector3 forward, Vector3 handle)
        {
            Assert.IsFalse(TrampolineCarryPose.TryCalculate(carrier, forward, handle, out var position, out var rotation));
            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(rotation, Is.EqualTo(Quaternion.identity));
        }
    }
}
