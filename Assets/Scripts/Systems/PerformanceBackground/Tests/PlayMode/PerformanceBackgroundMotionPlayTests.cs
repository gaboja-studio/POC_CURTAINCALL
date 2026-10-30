using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.PerformanceBackground.Tests
{
    public sealed class PerformanceBackgroundMotionPlayTests
    {
        GameObject root;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingDuringPlayRestoresAuthoredPose()
        {
            root = new GameObject("CraneMotionTest");
            var arm = new GameObject("Arm").transform;
            arm.SetParent(root.transform, false);
            arm.localPosition = new Vector3(5f, 10f, 15f);
            arm.localRotation = Quaternion.Euler(0f, 32f, 0f);
            var track = new PerformanceBackgroundMotion.Motion
            {
                target = arm, rotationDegrees = new Vector3(0f, 10f, 0f), periodSeconds = 80f
            };
            track.CaptureRestPose();
            var motion = root.AddComponent<PerformanceBackgroundMotion>();
            motion.cranes = new[] { track };
            var light = root.AddComponent<Light>();
            light.intensity = 12f;
            motion.practicalLights = new[] { new PerformanceBackgroundMotion.LightPulse { target=light, restIntensity=12f, amount=.2f, periodSeconds=80f } };
            yield return null;
            motion.Sample(20);
            Assert.That(Quaternion.Angle(arm.localRotation, track.restRotation), Is.GreaterThan(9f));
            Assert.That(light.intensity, Is.EqualTo(14.4f).Within(.001f));
            motion.enabled = false;
            Assert.That(Vector3.Distance(arm.localPosition, track.restPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(arm.localRotation, track.restRotation), Is.LessThan(.01f));
            Assert.That(light.intensity, Is.EqualTo(12f));
        }
    }
}
