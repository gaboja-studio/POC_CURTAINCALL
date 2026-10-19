using System;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>테스트 봇의 수평 이동과 트램폴린 받기 위치 계산. 상태나 입력 장치를 변경하지 않는다.</summary>
    public static class HumanCannonBotSteering
    {
        const float DefaultStopDistance = 0.35f;

        /// <summary>정지 반경 밖의 목표 방향을 정면 기준 (우측, 전진) 입력으로 변환한다.</summary>
        public static Vector2 PlanarInput(Vector3 from, Vector3 target, Vector3 forward,
            float stopDistance = DefaultStopDistance)
        {
            if (!Finite(from) || !Finite(target) || !TryPlanarForward(forward, out var planar))
                return Vector2.zero;

            double x = (double)target.x - from.x;
            double z = (double)target.z - from.z;
            double distance = Math.Sqrt(x * x + z * z);
            double stop = Finite(stopDistance) ? Math.Max(0, stopDistance) : DefaultStopDistance;
            if (distance <= stop) return Vector2.zero;

            x /= distance;
            z /= distance;
            return Vector2.ClampMagnitude(new Vector2(
                (float)(x * planar.z - z * planar.x),
                (float)(x * planar.x + z * planar.z)), 1f);
        }

        /// <summary>양수 중력에서 표면을 하강 통과하는 미래 수평 위치. 예측 시간은 maxSeconds까지다.</summary>
        public static Vector3 PredictCatch(Vector3 feet, Vector3 velocity, float surfaceY,
            float gravity, float maxSeconds = 2f)
        {
            Vector3 fallback = SafeVector(feet);
            if (Finite(surfaceY)) fallback.y = surfaceY;
            if (!Finite(feet) || !Finite(velocity) || !Finite(surfaceY)
                || !Finite(gravity) || gravity <= 0 || !Finite(maxSeconds) || maxSeconds <= 0)
                return fallback;

            double height = (double)feet.y - surfaceY;
            double vertical = velocity.y;
            double discriminant = vertical * vertical + 2.0 * gravity * height;
            if (discriminant < 0) return fallback;

            double root = Math.Sqrt(discriminant);
            // 급강하 중 두 큰 수가 상쇄되는 오차를 피하고, 항상 나중의 하강 해를 고른다.
            double time = vertical < 0 && height > 0
                ? 2.0 * height / (root - vertical)
                : (vertical + root) / gravity;
            if (time <= 0 || double.IsNaN(time) || double.IsInfinity(time)) return fallback;
            time = Math.Min(time, maxSeconds);

            double x = feet.x + velocity.x * time;
            double z = feet.z + velocity.z * time;
            if (!FitsFloat(x) || !FitsFloat(z)) return fallback;
            return new Vector3((float)x, surfaceY, (float)z);
        }

        /// <summary>장치 중심보다 손잡이 거리만큼 뒤에 설 위치. 목표 높이는 그대로 유지한다.</summary>
        public static Vector3 CarrierFeet(Vector3 desiredSurface, Vector3 facing, float handleDistance)
        {
            Vector3 fallback = SafeVector(desiredSurface);
            if (!Finite(desiredSurface) || !Finite(handleDistance) || handleDistance < 0
                || !TryPlanarForward(facing, out var planar)) return fallback;

            double x = desiredSurface.x - (double)planar.x * handleDistance;
            double z = desiredSurface.z - (double)planar.z * handleDistance;
            if (!FitsFloat(x) || !FitsFloat(z)) return fallback;
            return new Vector3((float)x, desiredSurface.y, (float)z);
        }

        static bool TryPlanarForward(Vector3 forward, out Vector3 planar)
        {
            planar = Vector3.zero;
            if (!Finite(forward)) return false;
            double length = Math.Sqrt((double)forward.x * forward.x + (double)forward.z * forward.z);
            if (length <= 0) return false;
            planar = new Vector3((float)(forward.x / length), 0, (float)(forward.z / length));
            return true;
        }

        static Vector3 SafeVector(Vector3 value) => new Vector3(
            Finite(value.x) ? value.x : 0,
            Finite(value.y) ? value.y : 0,
            Finite(value.z) ? value.z : 0);

        static bool FitsFloat(double value) => !double.IsNaN(value) && Math.Abs(value) <= float.MaxValue;
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
