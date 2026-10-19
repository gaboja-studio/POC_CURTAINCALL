using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>자유 회전 중에도 운반자·장치·주변 비행자가 화면 안에 들어오는 보수적 구면 구도.</summary>
    public static class HumanCannonCarryFraming
    {
        public static float SafePitchLimit(float distance, float elevation) =>
            Mathf.Min(35f, Mathf.Atan2(Mathf.Max(0f, elevation), Mathf.Max(0.01f, distance)) * Mathf.Rad2Deg);

        public static float FitDistance(Bounds subjects, Vector3 pivot, float verticalFov, float aspect)
        {
            var far = subjects.extents + Abs(subjects.center - pivot);
            float radius = far.magnitude + 0.7f;
            float halfVertical = Mathf.Clamp(verticalFov, 20f, 100f) * Mathf.Deg2Rad * 0.5f;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * Mathf.Max(0.25f, aspect));
            float halfAngle = Mathf.Min(halfVertical, halfHorizontal);
            return Mathf.Max(7.5f, radius / Mathf.Sin(halfAngle));
        }

        static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
