using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class HangingCameraRegressionTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        GameObject root;
        Component director, course;
        FieldInfo currentCourse;
        object previousCourse;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        object Call(string method, params object[] args) => director.GetType().GetMethod(method, Fields).Invoke(director, args);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Hanging camera physics fixture");
            root.transform.position = new Vector3(10000, 10000, 10000);
            var rig = new GameObject("Inactive camera and course");
            rig.SetActive(false);
            rig.transform.SetParent(root.transform, false);
            director = rig.AddComponent(T("CurtainCall.Cameras.CameraViewDirector"));
            course = rig.AddComponent(T("CurtainCall.Tightrope.TightropeCourse"));
            currentCourse = course.GetType().GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            previousCourse = currentCourse.GetValue(null);
            currentCourse.SetValue(null, course);
            Set(course, "useSceneGeometry", true);
        }

        [TearDown]
        public void TearDown()
        {
            currentCourse?.SetValue(null, previousCourse);
            if (root != null) Object.DestroyImmediate(root);
            Physics.SyncTransforms();
        }

        BoxCollider Box(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        [TestCase(.95f)]
        [TestCase(1.9f)]
        public void RopeSupportDoesNotCollapseCatchOrHangCameraButLandingStillBlocks(float drop)
        {
            var rope = Box("Rope support", Vector3.zero, new Vector3(1f, .2f, 20f));
            Set(course, "sceneRopes", new Collider[] { rope });
            Vector3 pivot = root.transform.position + Vector3.up * (.1f - drop + 1.2f);
            Vector3 direction = new Vector3(0, 2.55f, -4.5f).normalized;
            Physics.SyncTransforms();
            Assert.That((float)Call("WallDistance", pivot, direction, 5f), Is.EqualTo(5f));
            Box("Solid landing", pivot - root.transform.position + direction * 2f, Vector3.one * .4f);
            Physics.SyncTransforms();
            Assert.That((float)Call("WallDistance", pivot, direction, 5f), Is.InRange(.8f, 1.9f));
            Assert.That(rope.enabled, Is.True, "Camera exclusions must not remove player support.");
        }

        [TestCase(0f, true)]
        [TestCase(73f, true)]
        [TestCase(180f, true)]
        [TestCase(0f, false)]
        [TestCase(73f, false)]
        public void HangingPivotClearsEmbeddedLandingAndNearbyRotatedBeam(float yaw, bool inside)
        {
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var box = Box("Overhead landing", new Vector3(0, 1.3f, 0), new Vector3(6, 1.2f, 8));
            var pivot = root.transform.TransformPoint(inside ? new Vector3(0, 1f, 0) : new Vector3(-3.015f, 1.3f, 0));
            Physics.SyncTransforms();
            var adjusted = (Vector3)Call("HangingCameraPivot", pivot);
            Assert.That(Vector3.Distance(adjusted, box.ClosestPoint(adjusted)), Is.GreaterThanOrEqualTo(.299f));
            Assert.That(Vector3.Distance(adjusted, pivot), Is.LessThan(.7f));
        }

        [TestCase(false, .7f)]
        [TestCase(true, .7f)]
        [TestCase(true, .3f)]
        [TestCase(true, 1.3f)]
        public void FullCollisionPassKeepsHangerOutsideBodyAndLandingWithoutChangingInput(bool hanging, float belowLip)
        {
            Box("Deep landing at rope end", new Vector3(12, -1.25f, 0), new Vector3(24, 2.5f, 14));
            var actor = new GameObject("Inactive hanging owner");
            actor.SetActive(false);
            actor.transform.SetParent(root.transform, false);
            var player = actor.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var preset = Resources.Load("Settings/Camera/FreeThirdPerson");
            Set(director, "local", player);
            Set(director, "selected", preset);
            Set(director, "yaw", 270f);
            Set(director, "pitch", 0f);
            float lookHeight = (float)preset.GetType().GetField("LookHeight").GetValue(preset);
            actor.transform.localPosition = new Vector3(-.32f, -belowLip - lookHeight, 0);
            Vector3 pivot = actor.transform.position + Vector3.up * lookHeight;
            var hangType = T("CurtainCall.Tightrope.TightropeHangSystem");
            var current = hangType.GetProperty("Current");
            var previous = current.GetValue(null);
            try
            {
                var hangs = actor.AddComponent(hangType);
                var book = hangType.GetField("book", Fields).GetValue(hangs);
                book.GetType().GetMethod("Begin").Invoke(book, new object[] { 0UL, 0, 1f, 7d, 0d });
                current.SetValue(null, hanging ? hangs : null);
                Physics.SyncTransforms();
                var stateType = T("Unity.Cinemachine.CameraState");
                var state = stateType.GetProperty("Default").GetValue(null);
                Vector3 position = pivot + new Vector3(4.5f, 2.55f, 0);
                Quaternion before = Quaternion.LookRotation(pivot - position, Vector3.up);
                Set(state, "RawPosition", position);
                Set(state, "RawOrientation", before);
                object[] args = { state };
                Call("ApplyWallCollision", args);
                state = args[0];
                var extensions = T("Unity.Cinemachine.CameraStateExtensions");
                Vector3 finalPosition = (Vector3)extensions.GetMethod("GetFinalPosition").Invoke(null, new[] { state });
                Quaternion finalRotation = (Quaternion)extensions.GetMethod("GetFinalOrientation").Invoke(null, new[] { state });
                float distance = Vector3.Distance(finalPosition, pivot);
                if (hanging)
                {
                    Assert.That(distance, Is.GreaterThanOrEqualTo(1.5f), "Do not trigger nearby-character hiding.");
                    Assert.That(Physics.CheckSphere(finalPosition, .249f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.False, "Camera stays outside the landing.");
                }
                else
                {
                    Assert.That(distance, Is.LessThan(1f), "Ordinary wall retraction remains unchanged.");
                    Assert.That(Quaternion.Angle(finalRotation, before), Is.LessThan(.01f));
                }
                Assert.That(director.GetType().GetField("yaw", Fields).GetValue(director), Is.EqualTo(270f));
                Assert.That(director.GetType().GetField("pitch", Fields).GetValue(director), Is.EqualTo(0f));
            }
            finally { current.SetValue(null, previous); }
        }
    }
}
