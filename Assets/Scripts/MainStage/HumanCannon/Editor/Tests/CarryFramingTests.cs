using CurtainCall.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CarryFramingTests
    {
        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(9f / 16f)]
        public void CarrierTrampolineAndHighFlyer_FitDuringFreeOrbit(float aspect)
        {
            var subjects = new Bounds(new Vector3(3, 8, 2), new Vector3(12, 16, 10));
            var preset = ScriptableObject.CreateInstance<CameraViewPreset>();
            try
            {
                preset.FieldOfView = 68;
                preset.LookHeight = 8;
                preset.LookAhead = 0;
                preset.Height = 11.5f;
                preset.Distance = HumanCannonCarryFraming.FitDistance(subjects, Vector3.up * 8, 68, aspect);
                for (int yaw = 0; yaw < 360; yaw += 45)
                    foreach (float pitch in new[] { -35f, 0f, 35f })
                    {
                        Vector3 camera = CameraViewDirector.FreeThirdPersonPose(preset, Vector3.zero, yaw, pitch, out var rotation);
                        for (int x = -1; x <= 1; x += 2)
                            for (int y = -1; y <= 1; y += 2)
                                for (int z = -1; z <= 1; z += 2)
                                {
                                    var corner = subjects.center + Vector3.Scale(subjects.extents, new Vector3(x, y, z));
                                    var view = Quaternion.Inverse(rotation) * (corner - camera);
                                    Assert.That(view.z, Is.GreaterThan(0));
                                    float halfHeight = view.z * Mathf.Tan(34 * Mathf.Deg2Rad);
                                    Assert.That(Mathf.Abs(view.y), Is.LessThan(halfHeight));
                                    Assert.That(Mathf.Abs(view.x), Is.LessThan(halfHeight * aspect));
                                }
                    }
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void FlyerEnteringGroup_WidensCamera_AndInvalidScreenSizeStaysFinite()
        {
            var carrier = new Bounds(Vector3.up, new Vector3(4, 2, 4));
            float near = HumanCannonCarryFraming.FitDistance(carrier, Vector3.up, 68, 1.78f);
            carrier.Encapsulate(new Vector3(8, 20, 5));
            Assert.That(HumanCannonCarryFraming.FitDistance(carrier, Vector3.up, 68, 1.78f), Is.GreaterThan(near));
            Assert.That(float.IsInfinity(HumanCannonCarryFraming.FitDistance(carrier, Vector3.up, 0, 0)), Is.False);
        }

        [Test]
        public void LookingBack_ReversesForwardCorrection_ButCannotAlterVerticalFlight()
        {
            var control = new CannonAirControl();
            control.SetInput(Vector2.up, 1);
            Assert.That(control.ConsumeVelocity(1, false, new Vector3(0, 10, -1), 4.5f), Is.EqualTo(Vector3.back * 4.5f));
            control.SetInput(Vector2.one, 2);
            var diagonal = control.ConsumeVelocity(2, false, Vector3.back, 4.5f);
            Assert.That(diagonal.magnitude, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(diagonal.y, Is.Zero);
            Assert.That(control.ConsumeVelocity(3, false, Vector3.back, 4.5f), Is.EqualTo(Vector3.zero));
        }

        [TestCase(7.5f)]
        [TestCase(20f)]
        [TestCase(50f)]
        public void LowestCarryOrbit_StaysAboveCarrierFeet(float distance)
        {
            var preset = ScriptableObject.CreateInstance<CameraViewPreset>();
            try
            {
                float elevation = Mathf.Max(3.5f, distance * 0.45f);
                preset.Distance = distance;
                preset.LookHeight = 1.4f;
                preset.Height = preset.LookHeight + elevation;
                preset.LookAhead = 0;
                float limit = HumanCannonCarryFraming.SafePitchLimit(distance, elevation);
                for (int yaw = 0; yaw < 360; yaw += 45)
                {
                    var position = CameraViewDirector.FreeThirdPersonPose(preset, Vector3.zero, yaw, -limit, out _);
                    Assert.That(position.y, Is.GreaterThanOrEqualTo(1.39f));
                }
            }
            finally { Object.DestroyImmediate(preset); }
        }
    }
}
