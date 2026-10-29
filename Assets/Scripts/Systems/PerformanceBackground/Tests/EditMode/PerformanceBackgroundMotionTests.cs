using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.PerformanceBackground.Tests
{
    public sealed class PerformanceBackgroundMotionTests
    {
        GameObject root;
        Transform arm;
        PerformanceBackgroundMotion.Motion track;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MotionTest");
            root.transform.SetPositionAndRotation(new Vector3(21f, 32f, 43f), Quaternion.Euler(0f, 67f, 0f));
            root.transform.localScale = Vector3.one / 3f;
            arm = new GameObject("Arm").transform;
            arm.SetParent(root.transform, false);
            arm.localPosition = new Vector3(4f, 8f, 12f);
            arm.localRotation = Quaternion.Euler(12f, 24f, 36f);
            track = new PerformanceBackgroundMotion.Motion
            {
                target = arm, rotationDegrees = new Vector3(0f, 8f, 0f),
                travel = new Vector3(0f, 2f, 0f), periodSeconds = 80f, phase = .17f
            };
            track.CaptureRestPose();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void FullPeriodReturnsToSamePoseWithoutAccumulatedDrift()
        {
            track.Sample(11.25);
            var position = arm.localPosition;
            var rotation = arm.localRotation;
            for (int i = 0; i < 1000; i++) track.Sample(i * .027);
            track.Sample(11.25 + 80.0 * 1000);
            Assert.That(Vector3.Distance(arm.localPosition, position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(arm.localRotation, rotation), Is.LessThan(.01f));
        }

        [Test]
        public void SweepStaysWithinConfiguredRangeUnderScaledParent()
        {
            for (int second = 0; second <= 160; second++)
            {
                track.Sample(second);
                Assert.That(Quaternion.Angle(track.restRotation, arm.localRotation), Is.LessThanOrEqualTo(8.01f));
                Assert.That(Vector3.Distance(track.restPosition, arm.localPosition), Is.LessThanOrEqualTo(2.001f));
            }
        }

        [Test]
        public void RestoreReturnsAuthoredPose()
        {
            var motion = root.AddComponent<PerformanceBackgroundMotion>();
            motion.cranes = new[] { track };
            motion.Sample(23);
            Assert.That(Vector3.Distance(track.restPosition, arm.localPosition), Is.GreaterThan(.1f));
            motion.RestoreRestPose();
            Assert.That(arm.localPosition, Is.EqualTo(track.restPosition));
            Assert.That(Quaternion.Angle(arm.localRotation, track.restRotation), Is.LessThan(.01f));
        }

        [Test]
        public void SawSpinKeepsDirectionAcrossMultipleRevolutions()
        {
            track.rotationDegrees = Vector3.zero;
            track.spinDegreesPerSecond = new Vector3(0f, 0f, 90f);
            track.Sample(1);
            var expected = track.restRotation * Quaternion.Euler(0f, 0f, 90f);
            Assert.That(Quaternion.Angle(arm.localRotation, expected), Is.LessThan(.01f));
            track.Sample(40001);
            Assert.That(Quaternion.Angle(arm.localRotation, expected), Is.LessThan(.01f));
            track.Restore();
            Assert.That(Quaternion.Angle(arm.localRotation, track.restRotation), Is.LessThan(.01f));
        }

        [Test]
        public void PracticalLightPulseRestoresOriginalIntensity()
        {
            var light = root.AddComponent<Light>();
            light.intensity = 12f;
            var pulse = new PerformanceBackgroundMotion.LightPulse { target = light, restIntensity = 12f, amount = .2f, periodSeconds = 8f };
            pulse.Sample(2);
            Assert.That(light.intensity, Is.EqualTo(14.4f).Within(.001f));
            pulse.Restore();
            Assert.That(light.intensity, Is.EqualTo(12f));
        }

        [Test]
        public void GlowPreviewPreservesUnrelatedPropertiesAndRestoresOverrides()
        {
            var renderer = root.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                material.SetColor("_BaseColor", new Color(.2f, .4f, .8f, 1f));
                renderer.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Unrelated", 37f);
                renderer.SetPropertyBlock(block);
                var surface = new PerformanceBackgroundMotion.SurfaceMotion { target = renderer, amount = .2f, phase = .25f };
                surface.Sample(0);
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor").b, Is.EqualTo(.96f).Within(.001f));
                Assert.That(block.GetFloat("_Unrelated"), Is.EqualTo(37f));
                surface.Restore();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_Unrelated"), Is.EqualTo(37f));
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.clear));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void MissingTargetAndZeroPeriodDoNotBreakOtherTracks()
        {
            track.periodSeconds = 0f;
            Assert.DoesNotThrow(() => track.Sample(15));
            track.target = null;
            Assert.DoesNotThrow(() => { track.Sample(15); track.Restore(); });
        }
    }
}
