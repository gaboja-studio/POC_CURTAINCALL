using System;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>수평 정면을 따라 움직이는 단진자. 각도·각속도는 degree, 입력 가속도는 degree/s²다.</summary>
    public static class PoleSwingMotion
    {
        const double DegreesToRadians = Math.PI / 180.0;
        const double RadiansToDegrees = 180.0 / Math.PI;

        public static void Step(ref float angleDegrees, ref float angularSpeedDegrees, float input, float dt,
            float gravity, float length, float acceleration, float maxAngle, float maxSpeed, float damping)
        {
            if (!Finite(angleDegrees)) angleDegrees = 0;
            if (!Finite(angularSpeedDegrees)) angularSpeedDegrees = 0;
            if (!Finite(input) || !Finite(dt) || dt <= 0 || !Finite(gravity) || gravity < 0
                || !Finite(length) || length <= 0 || !Finite(acceleration) || acceleration < 0
                || !Finite(maxAngle) || maxAngle < 0 || !Finite(maxSpeed) || maxSpeed < 0
                || !Finite(damping) || damping < 0) return;

            // 긴 프레임은 최대 0.25초/30회로 제한해 적분 폭주와 무한 반복을 막는다.
            double elapsed = Math.Min(dt, 0.25);
            int count = (int)Math.Ceiling(elapsed * 120);
            double step = elapsed / count;
            double angle = Clamp(angleDegrees, maxAngle);
            double speed = Clamp(angularSpeedDegrees, maxSpeed);
            double drive = Clamp(input, 1) * acceleration;
            double decay = Math.Exp(-damping * step);
            for (int i = 0; i < count; i++)
            {
                double restoring = -(double)gravity / length * Math.Sin(angle * DegreesToRadians) * RadiansToDegrees;
                speed = Clamp((speed + (restoring + drive) * step) * decay, maxSpeed);
                angle += speed * step;
                if (angle >= maxAngle)
                {
                    angle = maxAngle;
                    if (speed > 0) speed = 0;
                }
                if (angle <= -maxAngle)
                {
                    angle = -maxAngle;
                    if (speed < 0) speed = 0;
                }
            }
            angleDegrees = (float)angle;
            angularSpeedDegrees = (float)speed;
        }

        public static Vector3 Offset(float angleDegrees, float length, Vector3 forward)
        {
            if (!Finite(angleDegrees) || !Finite(length) || length < 0
                || !TryPlanarForward(forward, out Vector3 planar)) return Vector3.zero;
            double angle = angleDegrees * DegreesToRadians;
            double horizontal = length * Math.Sin(angle);
            return FiniteVector(planar.x * horizontal, -length * Math.Cos(angle), planar.z * horizontal);
        }

        public static Vector3 Velocity(float angleDegrees, float angularSpeedDegrees, float length, Vector3 forward)
        {
            if (!Finite(angleDegrees) || !Finite(angularSpeedDegrees) || !Finite(length) || length < 0
                || !TryPlanarForward(forward, out Vector3 planar)) return Vector3.zero;
            double angle = angleDegrees * DegreesToRadians;
            double speed = angularSpeedDegrees * DegreesToRadians * length;
            double horizontal = speed * Math.Cos(angle);
            return FiniteVector(planar.x * horizontal, speed * Math.Sin(angle), planar.z * horizontal);
        }

        static bool TryPlanarForward(Vector3 forward, out Vector3 planar)
        {
            planar = Vector3.zero;
            if (!Finite(forward.x) || !Finite(forward.y) || !Finite(forward.z)) return false;
            double magnitude = Math.Sqrt((double)forward.x * forward.x + (double)forward.z * forward.z);
            if (magnitude <= 0) return false;
            planar = new Vector3((float)(forward.x / magnitude), 0, (float)(forward.z / magnitude));
            return true;
        }

        static Vector3 FiniteVector(double x, double y, double z)
        {
            if (Math.Abs(x) > float.MaxValue || Math.Abs(y) > float.MaxValue || Math.Abs(z) > float.MaxValue)
                return Vector3.zero;
            return new Vector3((float)x, (float)y, (float)z);
        }

        static double Clamp(double value, double limit) => Math.Max(-limit, Math.Min(limit, value));
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
