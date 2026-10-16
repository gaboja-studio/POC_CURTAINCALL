using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonHazardGeometryTests
    {
        [TestCase(0.75f, 1f, true)]
        [TestCase(0.751f, 1f, false)]
        [TestCase(0f, 2.25f, true)]
        [TestCase(0f, 2.251f, false)]
        [TestCase(0f, -0.25f, true)]
        [TestCase(0f, -0.251f, false)]
        public void Sphere_IncludesSideAndCapContact(float x, float y, bool expected) =>
            Assert.That(CannonHazardGeometry.SphereTouches(new Vector3(x, y, 0), 0.25f,
                Vector3.zero, 2, 0.5f), Is.EqualTo(expected));

        [Test]
        public void CrawlingCapsule_DoesNotRetainStandingHeight()
        {
            var hazard = new Vector3(0, 1.5f, 0);
            Assert.IsTrue(CannonHazardGeometry.SphereTouches(hazard, 0.25f, Vector3.zero, 2, 0.5f));
            Assert.IsFalse(CannonHazardGeometry.SphereTouches(hazard, 0.25f, Vector3.zero, 0.6f, 0.5f));
            Assert.IsTrue(CannonHazardGeometry.SphereTouches(Vector3.up, 0, Vector3.zero, 0.6f, 0.5f));
        }

        [Test]
        public void Segment_CrossesCapsuleBetweenEndpoints_AndIsOrderIndependent()
        {
            var start = new Vector3(-2, 0, 0);
            var end = new Vector3(2, 2, 0);
            Assert.IsTrue(Touches(start, end));
            Assert.IsTrue(Touches(end, start));
            Assert.IsFalse(Touches(start + Vector3.forward, end + Vector3.forward));
        }

        [TestCase(0.75f, true)]
        [TestCase(0.751f, false)]
        public void ParallelSegments_IncludeEdgeContact(float x, bool expected) =>
            Assert.That(Touches(new Vector3(x, -1, 0), new Vector3(x, 3, 0)), Is.EqualTo(expected));

        [Test]
        public void Segment_ClampsToEndpoint_AndHandlesBothDegenerateSegments()
        {
            Assert.IsTrue(Touches(new Vector3(0, 2.25f, 0), new Vector3(0, 4, 0)));
            Assert.IsFalse(Touches(new Vector3(0, 2.251f, 0), new Vector3(0, 4, 0)));
            Assert.IsTrue(CannonHazardGeometry.SegmentTouches(Vector3.one, Vector3.one, 0, Vector3.one, 0, 0));
            Assert.IsFalse(CannonHazardGeometry.SegmentTouches(Vector3.one, Vector3.one, 0, Vector3.zero, 0, 0));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteInputs_NeverDamagePlayer(float value)
        {
            var bad = new Vector3(0, value, 0);
            Assert.IsFalse(Touches(bad, Vector3.zero));
            Assert.IsFalse(Touches(Vector3.zero, bad));
            Assert.IsFalse(CannonHazardGeometry.SphereTouches(Vector3.zero, 1, bad, 2, 0.5f));
            foreach (var sizes in new[] { new Vector3(value, 2, 0.5f), new Vector3(1, value, 0.5f), new Vector3(1, 2, value) })
                Assert.IsFalse(CannonHazardGeometry.SphereTouches(Vector3.zero, sizes.x, Vector3.zero, sizes.y, sizes.z));
        }

        [Test]
        public void InvalidDimensionsAreRejected_AndLargeFiniteDistancesDoNotOverflow()
        {
            Assert.IsFalse(CannonHazardGeometry.SphereTouches(Vector3.zero, -1, Vector3.zero, 2, 0.5f));
            Assert.IsFalse(CannonHazardGeometry.SphereTouches(Vector3.zero, 1, Vector3.zero, -2, 0.5f));
            Assert.IsFalse(CannonHazardGeometry.SphereTouches(Vector3.zero, 1, Vector3.zero, 2, -0.5f));
            Assert.IsFalse(CannonHazardGeometry.SphereTouches(Vector3.right * float.MaxValue, float.MaxValue,
                Vector3.left * float.MaxValue, 0, 0));
        }

        static bool Touches(Vector3 start, Vector3 end) =>
            CannonHazardGeometry.SegmentTouches(start, end, 0.25f, Vector3.zero, 2, 0.5f);
    }
}
