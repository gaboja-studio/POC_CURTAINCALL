using System.Reflection;
using CurtainCall.Damage;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("November11CommonFix")]
    public class TrampolineCarryPoseTests
    {
        [TestCase(false), TestCase(true), Category("CommonProgressionRegression")]
        public void DetachedLimbsPassThroughAllTrampolineShapesAndStillLandOnTheFloor(bool createdAfterTrampoline)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var physics = scene.GetPhysicsScene();
            var oldTrampoline = HumanCannonTrampoline.Current;
            typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, null);
            GameObject Make(string name)
            {
                var go = new GameObject(name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }
            try
            {
                Assert.That(physics, Is.Not.EqualTo(Physics.defaultPhysicsScene), "Never simulate the user's open scene.");
                var floor = Make("Solid floor").AddComponent<BoxCollider>();
                floor.transform.position = Vector3.down * 2; floor.size = new Vector3(10, .5f, 10);
                var limb = Make("Detached limb");
                var shape = limb.AddComponent<BoxCollider>(); shape.size = Vector3.one * .2f;
                var rb = limb.AddComponent<Rigidbody>(); rb.useGravity = true;
                limb.transform.position = Vector3.up * 3;
                void RegisterLimb()
                {
                    var marker = limb.AddComponent<DismemberedLimb>();
                    var active = (System.Collections.Generic.List<DismemberedLimb>)typeof(DismemberedLimb)
                        .GetField("active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    if (!active.Contains(marker)) typeof(DismemberedLimb).GetMethod("OnEnable", members).Invoke(marker, null);
                }
                if (!createdAfterTrampoline) RegisterLimb();
                var root = Make("Trampoline"); root.SetActive(false);
                var main = root.AddComponent<BoxCollider>();
                var child = Make("Child trampoline frame"); child.transform.SetParent(root.transform);
                var frame = child.AddComponent<BoxCollider>(); frame.size = new Vector3(4, .1f, 4); frame.center = Vector3.up * .6f;
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                root.SetActive(true);
                if (HumanCannonTrampoline.Current != trampoline)
                    typeof(HumanCannonTrampoline).GetMethod("OnEnable", members).Invoke(trampoline, null);
                if (createdAfterTrampoline) RegisterLimb();
                var tick = typeof(HumanCannonTrampoline).GetMethod("FixedUpdate", members);
                var ownership = (CannonSeatState)typeof(HumanCannonTrampoline).GetField("ownership", members).GetValue(trampoline);
                // Idle, carried, released and re-enabled shapes use the same physical exclusion.
                foreach (ulong actor in new[] { CannonSeatState.Nobody, 12UL, CannonSeatState.Nobody })
                {
                    ownership.Receive(ownership.Revision + 1, actor);
                    frame.enabled = false; frame.enabled = true;
                    Physics.SyncTransforms(); tick.Invoke(trampoline, null);
                    Assert.IsTrue(Physics.GetIgnoreCollision(main, shape));
                    Assert.IsTrue(Physics.GetIgnoreCollision(frame, shape));
                    Assert.IsFalse(Physics.GetIgnoreCollision(floor, shape));
                }
                for (int i = 0; i < 100; i++) { tick.Invoke(trampoline, null); physics.Simulate(.02f); }
                Assert.That(rb.position.y, Is.LessThan(-1f), "The limb must pass through, not land on the trampoline.");
                Assert.That(rb.position.y, Is.GreaterThan(-1.8f), "Ordinary floor collision must remain enabled.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                typeof(HumanCannonTrampoline).GetProperty("Current").SetValue(null, oldTrampoline);
            }
        }

        [Test]
        [Category("CommonProgressionRegression")]
        public void CarriedTrampolineClearsRingHeightButStopsAtWallAndLowCeiling()
        {
            var root = new GameObject("Carry step probe");
            var terrain = new GameObject("Ring lip");
            var ceiling = new GameObject("Overhead obstacle");
            try
            {
                root.transform.position = new Vector3(12000f, 100f, 12000f);
                var body = root.AddComponent<BoxCollider>();
                body.center = Vector3.up * .25f;
                body.size = new Vector3(1f, .5f, 1f);
                var step = terrain.AddComponent<BoxCollider>();
                terrain.transform.position = root.transform.position + Vector3.forward;
                step.center = Vector3.up * .3f;
                step.size = new Vector3(4f, .6f, 1f);
                var roof = ceiling.AddComponent<BoxCollider>();
                roof.size = new Vector3(2f, .1f, 2f);
                ceiling.SetActive(false);
                var destination = root.transform.position + Vector3.forward * .5f;
                var method = typeof(HumanCannonCarry).GetMethod("TryCarryOverStep", BindingFlags.Static | BindingFlags.NonPublic);
                bool Lift(out Vector3 position)
                {
                    object[] args = { root.transform, body, destination, null, 0f, .65f, Vector3.zero };
                    bool moved = (bool)method.Invoke(null, args);
                    position = (Vector3)args[6];
                    return moved;
                }
                Physics.SyncTransforms();
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, destination, Quaternion.identity, null));
                Assert.IsTrue(Lift(out var lifted));
                Assert.That(lifted.y, Is.EqualTo(destination.y + .65f).Within(.001f));

                ceiling.transform.position = root.transform.position + Vector3.up * .8f;
                ceiling.SetActive(true);
                Physics.SyncTransforms();
                Assert.IsFalse(Lift(out _), "The lifting segment must stop at the ceiling.");
                ceiling.SetActive(false);
                step.center = Vector3.up;
                step.size = new Vector3(4f, 2f, 1f);
                Physics.SyncTransforms();
                Assert.IsFalse(Lift(out _), "The raised horizontal segment must stop at a full wall.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(terrain);
                Object.DestroyImmediate(ceiling);
            }
        }

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
        [Category("CommonProgressionRegression")]
        public void CarryIgnoresDetachedLimbsAndCanLeaveAnExistingContact()
        {
            var root = new GameObject("Carry overlap probe");
            var obstacle = new GameObject("Carry obstruction");
            try
            {
                root.transform.position = new Vector3(12000f, 100f, 12000f);
                var body = root.AddComponent<BoxCollider>();
                var shape = obstacle.AddComponent<BoxCollider>();
                shape.size = new Vector3(.6f, 2f, 3f);
                obstacle.transform.position = root.transform.position + Vector3.right * .55f;
                Physics.SyncTransforms();
                Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.left, Quaternion.identity, null));
                var check = typeof(HumanCannonCarry).GetMethod("CheckMove", BindingFlags.NonPublic | BindingFlags.Static);
                bool Try(Vector3 travel) => (bool)check.Invoke(null, new object[] { root.transform, body,
                    root.transform.position + travel, Quaternion.identity, null, true, 0f, null, true, Vector3.zero });
                Assert.IsTrue(Try(Vector3.left));
                Assert.IsFalse(Try(Vector3.right));
                obstacle.AddComponent<DismemberedLimb>();
                Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, root.transform.position + Vector3.right, Quaternion.identity, null));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(obstacle); }
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
