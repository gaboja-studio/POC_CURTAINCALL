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
            float gravity, float length, float acceleration, float maxAngle, float maxSpeed, float damping, bool braking = false)
        {
            if (!Finite(angleDegrees)) angleDegrees = 0;
            if (!Finite(angularSpeedDegrees)) angularSpeedDegrees = 0;
            if (!Finite(input) || !Finite(dt) || dt <= 0 || !Finite(gravity) || gravity < 0
                || !Finite(length) || length <= 0 || !Finite(acceleration) || acceleration < 0
                || !Finite(maxAngle) || maxAngle < 0 || !Finite(maxSpeed) || maxSpeed < 0
                || !Finite(damping) || damping < 0) return;
            if ((braking || Math.Abs(input) < .01f) && Math.Abs(angleDegrees) < .01f && Math.Abs(angularSpeedDegrees) < .01f)
            {
                angleDegrees = angularSpeedDegrees = 0;
                return;
            }

            // 긴 프레임은 최대 0.25초/30회로 제한해 적분 폭주와 무한 반복을 막는다.
            double elapsed = Math.Min(dt, 0.25);
            int count = (int)Math.Ceiling(elapsed * 120);
            double step = elapsed / count;
            double angle = Clamp(angleDegrees, maxAngle);
            double speed = Clamp(angularSpeedDegrees, maxSpeed);
            double requestedDrive = Clamp(input, 1) * acceleration;
            double decay = Math.Exp(-damping * step);
            double gravityOverLength = (double)gravity / length;
            double maximumEnergy = gravityOverLength * (1 - Math.Cos(maxAngle * DegreesToRadians));
            double brakeStep = Math.Abs(requestedDrive) * step;
            // Cap brake torque near rest to the pendulum's critical damping, so it
            // can return to center under gravity instead of locking at an angle.
            double brakeFraction = braking && gravity > 0 ? 1 - Math.Exp(-2 * Math.Sqrt(gravityOverLength) * step) : 1;
            for (int i = 0; i < count; i++)
            {
                // Push during the matching half of the swing, then let gravity bring it back.
                // Holding a direction builds energy over successive passes instead of pinning
                // the bar at a tilted equilibrium. Alternating W/S can pump both halves.
                double drive = !braking && requestedDrive * speed >= 0
                    ? requestedDrive * Math.Max(0, Math.Cos(angle * DegreesToRadians)) : 0;
                double restoring = -gravityOverLength * Math.Sin(angle * DegreesToRadians) * RadiansToDegrees;
                speed += (restoring + drive) * step;
                if (braking)
                    speed -= Math.Sign(speed) * Math.Min(brakeStep, Math.Abs(speed) * brakeFraction);
                speed = Clamp(speed * decay, maxSpeed);
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
                if (gravity > 0)
                {
                    // Limit total pendulum energy, not only the angle. Excess input cannot
                    // bank a hidden launch boost or slam into the end stop at full speed.
                    double potential = gravityOverLength * (1 - Math.Cos(angle * DegreesToRadians));
                    speed = Clamp(speed, EnergySpeedLimit(maximumEnergy, potential));
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

        public static float AddCatchMomentum(float angleDegrees, float angularSpeedDegrees, float approachSpeed,
            float length, float gravity, float motionSpeed, float maxAngle, float maxSpeed)
        {
            if (!Finite(approachSpeed) || approachSpeed == 0f || !Finite(angleDegrees) || !Finite(angularSpeedDegrees)
                || !Finite(length) || length <= 0 || !Finite(gravity) || gravity < 0
                || !Finite(motionSpeed) || motionSpeed <= 0 || !Finite(maxAngle) || maxAngle < 0
                || !Finite(maxSpeed) || maxSpeed < 0) return angularSpeedDegrees;
            double angle = Clamp(angleDegrees, maxAngle) * DegreesToRadians;
            // Project horizontal approach onto the pendulum tangent and convert world speed to the swing clock.
            double delta = approachSpeed * Math.Cos(angle) / ((double)length * motionSpeed) * RadiansToDegrees;
            double limit = maxSpeed;
            if (gravity > 0)
            {
                double gravityOverLength = (double)gravity / length;
                double maximum = gravityOverLength * (1 - Math.Cos(maxAngle * DegreesToRadians));
                double potential = gravityOverLength * (1 - Math.Cos(angle));
                limit = Math.Min(limit, EnergySpeedLimit(maximum, potential));
            }
            return (float)Clamp(angularSpeedDegrees + delta, limit);
        }

        static double EnergySpeedLimit(double maximumEnergy, double potential) =>
            Math.Sqrt(Math.Max(0, 2 * (maximumEnergy - potential))) * RadiansToDegrees;

        /// <summary>현재 운동 에너지와 올라간 높이를 최저점에서의 속도로 환산한다.</summary>
        public static float StoredSpeed(float angleDegrees, float angularSpeedDegrees, float length,
            float gravity, float motionSpeed, float maxAngle, float maxSpeed)
        {
            if (!Finite(angleDegrees) || !Finite(angularSpeedDegrees) || !Finite(length) || length <= 0
                || !Finite(gravity) || gravity < 0 || !Finite(motionSpeed) || motionSpeed <= 0
                || !Finite(maxAngle) || maxAngle < 0 || !Finite(maxSpeed) || maxSpeed < 0) return 0;
            double angle = Clamp(angleDegrees, maxAngle) * DegreesToRadians;
            double speed = Clamp(angularSpeedDegrees, maxSpeed) * DegreesToRadians * length;
            double squared = speed * speed + 2d * gravity * length * (1 - Math.Cos(angle));
            if (gravity > 0)
                squared = Math.Min(squared, 2d * gravity * length * (1 - Math.Cos(maxAngle * DegreesToRadians)));
            double result = Math.Sqrt(Math.Max(0, squared)) * motionSpeed;
            return result <= float.MaxValue ? (float)result : 0;
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
