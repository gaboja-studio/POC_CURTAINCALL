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

        [Test]
        public void RopeViewSignDoesNotChangeWithCameraOrCharacterYaw()
        {
            var go = new GameObject("Rope facing fixture");
            var mover = go.AddComponent(T("CurtainCall.Player.PlayerMover"));
            try
            {
                foreach (float yaw in new[] { 0f, 90f, 135f, 180f, 270f })
                {
                    go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    Assert.That(T("CurtainCall.Player.TightropeControlScheme").GetMethod("LocalViewSign")
                        .Invoke(null, new object[] { mover }), Is.EqualTo(1));
                }
            }
            finally { Object.DestroyImmediate(go); }
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
