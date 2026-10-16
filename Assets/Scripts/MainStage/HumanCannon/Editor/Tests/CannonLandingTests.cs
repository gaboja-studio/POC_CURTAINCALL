using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class CannonLandingTests
    {
        [Test]
        public void TopAndSkinOffset_AreSafe()
        {
            var center = new Vector3(0, 0.1f, 0);
            var size = new Vector3(4, 0.2f, 4);
            Assert.IsTrue(CannonLandingSurface.Contains(new Vector3(0, 0.2f, 0), center, size, 0.12f));
            Assert.IsTrue(CannonLandingSurface.Contains(new Vector3(0, 0.28f, 0), center, size, 0.12f));
        }

        [TestCase(2f, 2f, true)]
        [TestCase(2.01f, 0f, false)]
        [TestCase(0f, -2.01f, false)]
        public void Boundary_SeparatesCushionFromOrdinaryFloor(float x, float z, bool expected)
        {
            Assert.That(CannonLandingSurface.Contains(new Vector3(x, 0.2f, z), new Vector3(0, 0.1f, 0),
                new Vector3(4, 0.2f, 4), 0.12f), Is.EqualTo(expected));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(2f)]
        public void UnderOrAboveCushion_IsNotSafe(float height)
        {
            Assert.IsFalse(CannonLandingSurface.Contains(new Vector3(0, height, 0), new Vector3(0, 0.1f, 0),
                new Vector3(4, 0.2f, 4), 0.12f));
        }

        [Test]
        public void InvalidGeometry_IsRejected()
        {
            Assert.IsFalse(CannonLandingSurface.Contains(Vector3.zero, Vector3.zero, Vector3.zero, 0.12f));
            Assert.IsFalse(CannonLandingSurface.Contains(Vector3.zero, Vector3.zero, Vector3.one, -1f));
            Assert.IsFalse(CannonLandingSurface.Contains(new Vector3(float.NaN, 0, 0), Vector3.zero, Vector3.one, 0.12f));
            Assert.IsFalse(CannonLandingSurface.Contains(Vector3.zero, Vector3.zero, Vector3.one, float.PositiveInfinity));
        }

        [Test]
        public void ScaledRotatedZone_UsesItsActualColliderTop()
        {
            var obj = new GameObject("LandingZoneTest") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                obj.transform.SetPositionAndRotation(new Vector3(6, 0.1f, 25), Quaternion.Euler(0, 45, 0));
                obj.transform.localScale = new Vector3(4, 0.2f, 2);
                var zone = obj.AddComponent<HumanCannonLandingZone>();
                Assert.IsTrue(zone.SupportsLanding(obj.transform.TransformPoint(new Vector3(0.4f, 0.5f, 0.4f))));
                Assert.IsFalse(zone.SupportsLanding(obj.transform.TransformPoint(new Vector3(0.6f, 0.5f, 0))));
                Assert.IsFalse(zone.SupportsLanding(new Vector3(6, 0, 25)));
                obj.GetComponent<BoxCollider>().isTrigger = true;
                Assert.IsFalse(zone.SupportsLanding(new Vector3(6, 0.2f, 25)));
                obj.GetComponent<BoxCollider>().isTrigger = false;
                zone.enabled = false;
                Assert.IsFalse(zone.SupportsLanding(new Vector3(6, 0.2f, 25)));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void SafeLanding_CompletesOnlyCurrentFlightOnce()
        {
            var flights = new CannonFlightState();
            flights.Begin(4, -1, 12);
            Assert.IsTrue(flights.TryRebound(4, -1, 12, 0));
            Assert.IsFalse(flights.TryComplete(4, -1, 12, 0));
            Assert.IsTrue(flights.TryComplete(4, -1, 12, 1));
            Assert.IsFalse(flights.Contains(4));
            Assert.IsFalse(flights.TryComplete(4, -1, 12, 1));
            flights.Begin(4, 0, 13);
            Assert.IsFalse(flights.TryComplete(4, -1, 12, 1));
            Assert.IsTrue(flights.Matches(4, 0, 13, 0));
            flights.Clear();
            Assert.IsFalse(flights.TryComplete(4, 0, 13, 0));
        }
    }
}
