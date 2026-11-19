using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class EscapeSwingDriveTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void ContinuousDriveKeepsRespondingAfterPointSixFiveSecondsAndReversesImmediately(int fps)
        {
            var go = new GameObject("Continuous swing fixture");
            try
            {
                var pole = go.AddComponent<HumanCannonPole>();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                foreach (var name in new[] { "continuousSwingInput", "swingAcceleration", "maxSwingAngle",
                    "maxSwingSpeed", "swingDamping", "hangLength" })
                    Assert.IsNull(typeof(HumanCannonPole).GetField(name, fields), name);

                var input = new PoleSwingInput();
                float dt = 1f / fps, gravity = GameSettings.Base.Character.Gravity;
                float angle = 0f, speed = 0f, earlyPeak = 0f, forwardPeak = 0f, reversePeak = 0f;
                for (int i = 1; i <= fps * 2; i++)
                {
                    float drive = pole.SwingDrive(ref input, 1f, dt);
                    Assert.That(drive, Is.EqualTo(1));
                    pole.Step(ref angle, ref speed, drive, dt, gravity);
                    forwardPeak = Mathf.Max(forwardPeak, angle);
                    if (i * dt <= .65f) earlyPeak = forwardPeak;
                }
                Assert.That(forwardPeak, Is.GreaterThan(earlyPeak), "A held key must continue building swing after .65 seconds.");
                Assert.That(pole.SwingDrive(ref input, -1f, dt), Is.EqualTo(-1));
                for (int i = 0; i < fps * 2; i++)
                {
                    pole.Step(ref angle, ref speed, pole.SwingDrive(ref input, -1f, dt), dt, gravity);
                    reversePeak = Mathf.Min(reversePeak, angle);
                    Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(GameSettings.Poles.MaxSwingAngle));
                    Assert.That(Mathf.Abs(speed), Is.LessThanOrEqualTo(GameSettings.Poles.MaxSwingSpeed));
                }
                Assert.That(reversePeak, Is.LessThan(-earlyPeak), "Reversed input must build a swing in the opposite direction.");
                Assert.That(pole.SwingDrive(ref input, float.NaN, dt), Is.Zero);
                Assert.That(pole.SwingDrive(ref input, 1f, 0f), Is.Zero);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
