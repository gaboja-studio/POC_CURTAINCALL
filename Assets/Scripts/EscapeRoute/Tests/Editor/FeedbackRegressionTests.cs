using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class FeedbackRegressionTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

        [TestCase(0, 1)] [TestCase(45, 1)] [TestCase(135, -1)] [TestCase(180, -1)]
        public void RopeControlsFollowTheViewAtBothEnds(float angle, int expected)
        {
            var resolve = T("CurtainCall.Player.TightropeControlScheme").GetMethod("ResolveViewSign");
            foreach (float courseYaw in new[] { 0f, 48f, 90f, 180f, 271f })
            {
                Vector3 rope = Quaternion.Euler(0, courseYaw, 0) * Vector3.forward;
                Vector3 view = Quaternion.Euler(0, angle, 0) * rope;
                int sign = (int)resolve.Invoke(null, new object[] { view, rope, 1 });
                Assert.That(sign, Is.EqualTo(expected));
                Assert.That(Vector3.Dot(view, rope * sign), Is.GreaterThan(0));
                Assert.That(Vector3.Dot(Vector3.Cross(Vector3.up, view), Vector3.Cross(Vector3.up, rope) * sign), Is.GreaterThan(0));
            }
        }

        [Test]
        public void LookingAcrossTheRopeDoesNotFlipHeldControlsEveryFrame()
        {
            var resolve = T("CurtainCall.Player.TightropeControlScheme").GetMethod("ResolveViewSign");
            foreach (int previous in new[] { -1, 1 })
                foreach (float angle in new[] { 86f, 90f, 94f })
                    Assert.That(resolve.Invoke(null, new object[] { Quaternion.Euler(0, angle, 0) * Vector3.forward, Vector3.forward, previous }), Is.EqualTo(previous));
        }

        [Test]
        public void CameraSweepGrowsItsBufferAndDoesNotLoseWalls()
        {
            var objects = new List<GameObject>();
            try
            {
                var go = new GameObject("Camera sweep fixture"); go.SetActive(false); objects.Add(go);
                var director = go.AddComponent(T("CurtainCall.Cameras.CameraViewDirector"));
                Vector3 origin = new(10000, 10000, 10000);
                for (int i = 0; i < 48; i++)
                {
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall);
                    wall.transform.position = origin + Vector3.forward * (2 + i * .7f);
                    wall.transform.localScale = new Vector3(2, 2, .1f);
                }
                Physics.SyncTransforms();
                float expected = Physics.SphereCastAll(origin, .25f, Vector3.forward, 40,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Min(h => h.distance) - .05f;
                float actual = (float)director.GetType().GetMethod("WallDistance", Flags).Invoke(director, new object[] { origin, Vector3.forward, 40f });
                Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
                Assert.That(((Array)director.GetType().GetField("wallHits", Flags).GetValue(director)).Length, Is.GreaterThan(32));
            }
            finally { foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); Physics.SyncTransforms(); }
        }

        [Test]
        public void SuccessfulEndingRequiresHostConfirmationAfterTheShot()
        {
            var type = T("CurtainCall.EscapeRoute.EscapeEndingPresentation");
            var state = Activator.CreateInstance(T("CurtainCall.Campaign.CampaignSnapshot"));
            var phase = state.GetType().GetField("Phase"); var end = state.GetType().GetField("LastEnd");
            phase.SetValue(state, Enum.Parse(phase.FieldType, "Ending")); end.SetValue(state, Enum.Parse(end.FieldType, "EscapeSucceeded"));
            bool Can(bool host, bool requested, double elapsed) => (bool)type.GetMethod("CanConfirmResult").Invoke(null, new[] { state, (object)host, requested, elapsed, 6f });
            Assert.IsFalse(Can(false, false, 30)); Assert.IsFalse(Can(true, false, 5.99));
            Assert.IsFalse(Can(true, true, 30)); Assert.IsFalse(Can(true, false, double.NaN));
            Assert.IsTrue(Can(true, false, 6)); Assert.IsTrue(Can(true, false, 30));
            phase.SetValue(state, Enum.Parse(phase.FieldType, "Escape")); Assert.IsFalse(Can(true, false, 30));
        }
    }
}
