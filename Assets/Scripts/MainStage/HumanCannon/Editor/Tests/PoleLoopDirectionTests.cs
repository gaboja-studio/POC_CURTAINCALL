using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class PoleLoopDirectionTests
    {
        GameObject root;
        HumanCannonPoleLoop loop;
        Transform carriage;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Conveyor direction fixture");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(new Vector3(51, -17, -32), Quaternion.Euler(0, 23, 0));
            carriage = new GameObject("Carriage").transform;
            carriage.SetParent(root.transform, false);
            loop = root.AddComponent<HumanCannonPoleLoop>();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(.1f)] [TestCase(.3f)] [TestCase(.6f)] [TestCase(.9f)]
        public void Reverse_PreservesInitialRailPositionAndReversesFacingAndMomentum(float phase)
        {
            loop.Configure(carriage, Vector3.zero, 34, 16, 5, phase);
            loop.EvaluatePose(0, out var position, out var rotation, out var velocity, out var angular);
            loop.Configure(carriage, Vector3.zero, 34, 16, 5, phase, 0, true);
            loop.EvaluatePose(0, out var reversed, out var facing, out var reverseVelocity, out var reverseAngular);
            Assert.That(Vector3.Distance(position, reversed), Is.LessThan(.001f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, facing * Vector3.forward), Is.EqualTo(-1).Within(.001f));
            Assert.That((velocity + reverseVelocity).magnitude, Is.LessThan(.001f));
            Assert.That((angular + reverseAngular).magnitude, Is.LessThan(.001f));
        }

        [TestCase(3d)] [TestCase(9d)] [TestCase(19d)] [TestCase(31d)]
        public void Reverse_ReleaseVelocityMatchesActualRailMotionAndClosesSeam(double time)
        {
            loop.Configure(carriage, Vector3.zero, 34, 16, 5, 0, 0, true);
            const double dt = .01d;
            loop.EvaluatePose(time, out var p, out var r, out var v, out var w);
            loop.EvaluatePose(time + dt, out var next, out var nextR, out _, out _);
            Vector3 seat = new Vector3(1.2f, -1.5f, .4f);
            Vector3 pointVelocity = v + Vector3.Cross(w, r * seat);
            Assert.That(((next + nextR * seat - p - r * seat) / (float)dt - pointVelocity).magnitude, Is.LessThan(.025f));
            loop.EvaluatePose(time + loop.Length / 5d, out var wrapped, out var wrappedR, out var wrappedV, out _);
            Assert.That(Vector3.Distance(p, wrapped), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(r, wrappedR), Is.LessThan(.05f));
            Assert.That(Vector3.Distance(v, wrappedV), Is.LessThan(.001f));
        }
    }
}
