using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonGroundMarkerTests
    {
        GameObject root;
        HumanCannonGroundMarkers markers;
        readonly Vector3 origin = new(1000, 0, 1000);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("GroundMarkerTest");
            markers = root.AddComponent<HumanCannonGroundMarkers>();
            markers.enabled = false;
            Floor("Lower", 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var zone in root.GetComponentsInChildren<HumanCannonLandingZone>()) Lifecycle(zone, "OnDisable");
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Projection_UsesNearestFloorDirectlyBelowAndFollowsItsHeight()
        {
            var upper = Floor("Upper", 6);
            Physics.SyncTransforms();
            Assert.IsTrue(markers.TryProjectGround(origin + new Vector3(1, 12, 2), out var point, out var normal));
            Assert.That(Vector3.Distance(point, origin + new Vector3(1, 6, 2)), Is.LessThan(0.001f));
            Assert.That(normal, Is.EqualTo(Vector3.up));
            upper.transform.position += Vector3.up * 2;
            Physics.SyncTransforms();
            Assert.IsTrue(markers.TryProjectGround(origin + new Vector3(1, 12, 2), out point, out _));
            Assert.That(point.y, Is.EqualTo(8).Within(0.001f));
            upper.enabled = false;
            Physics.SyncTransforms();
            Assert.IsTrue(markers.TryProjectGround(origin + new Vector3(1, 12, 2), out point, out _));
            Assert.That(point.y, Is.EqualTo(0).Within(0.001f), "바닥이 열리면 아래쪽 실제 바닥을 사용한다.");
        }

        [Test]
        public void Projection_IgnoresTriggerAndNonLandingObstacle_AndDoesNotInventGroundInVoid()
        {
            var trigger = Floor("Trigger", 6); trigger.isTrigger = true;
            var obstacle = new GameObject("Decoration"); obstacle.transform.SetParent(root.transform);
            obstacle.transform.position = origin + Vector3.up * 3;
            obstacle.AddComponent<BoxCollider>().size = new Vector3(8, 1, 8);
            Physics.SyncTransforms();
            Assert.IsTrue(markers.TryProjectGround(origin + Vector3.up * 12, out var point, out _));
            Assert.That(point.y, Is.EqualTo(0).Within(0.001f));
            Assert.IsFalse(markers.TryProjectGround(origin + new Vector3(20, 12, 0), out _, out _));
            Assert.IsFalse(markers.TryProjectGround(origin + Vector3.down, out _, out _), "발보다 높은 층으로 표시를 끌어올리지 않는다.");
        }

        [Test]
        public void Projection_IncludesCourseFloorWithoutTurningItIntoSafeLandingZone()
        {
            var course = new GameObject("UnsafeCourseFloor"); course.transform.SetParent(root.transform);
            course.transform.position = origin + new Vector3(20, -0.25f, 0);
            var box = course.AddComponent<BoxCollider>(); box.size = new Vector3(8, 0.5f, 8);
            typeof(HumanCannonGroundMarkers).GetField("courseFloors", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(markers, new Collider[] { box });
            Physics.SyncTransforms();
            Assert.IsTrue(markers.TryProjectGround(origin + new Vector3(20, 10, 0), out var point, out _));
            Assert.That(point.y, Is.EqualTo(0).Within(0.001f));
            Assert.IsNull(course.GetComponent<HumanCannonLandingZone>());
            box.enabled = false;
            Physics.SyncTransforms();
            Assert.IsFalse(markers.TryProjectGround(origin + new Vector3(20, 10, 0), out _, out _));
        }

        BoxCollider Floor(string name, float height)
        {
            var floor = new GameObject(name);
            floor.transform.SetParent(root.transform);
            floor.transform.position = origin + Vector3.up * (height - 0.25f);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(8, 0.5f, 8);
            // EditMode에서는 MonoBehaviour의 Play 수명 콜백을 실행하지 않으므로 이 fixture의 면만 등록한다.
            Lifecycle(floor.AddComponent<HumanCannonLandingZone>(), "OnEnable");
            return box;
        }

        static void Lifecycle(HumanCannonLandingZone zone, string method) => typeof(HumanCannonLandingZone)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(zone, null);
    }
}
