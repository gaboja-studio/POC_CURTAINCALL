using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public enum CannonHazardKind { SpikeMace, RotatingBlades, GroundSaw }

    /// <summary>고정 코스에 배치하는 일반 절단 함정. 움직임은 공유 시각, 피해는 HazardCourse 호스트가 담당한다.</summary>
    public sealed class HumanCannonHazard : MonoBehaviour
    {
        [SerializeField] CannonHazardKind kind;
        [SerializeField] Transform visual;
        [Tooltip("톱날을 바닥과 평행하게 눕힌다. 외형과 접촉 판정에 같은 회전을 적용한다.")]
        [SerializeField] bool horizontalBlade;
        [SerializeField] Vector3 travel = new Vector3(4, 0, 0);
        [SerializeField, Min(0.1f)] float periodSeconds = 8f;
        [SerializeField] float phase;
        [SerializeField, Min(0.01f)] float radius = 0.65f;
        [SerializeField, Min(0.1f)] float bladeHalfLength = 1.3f;
        [SerializeField, Min(0.01f)] float bladeHalfThickness = 0.12f;
        [SerializeField, Min(1f)] float rotationSpeed = 60f;
        public CannonHazardKind Kind => kind;
        public float Radius => radius;
        public float PeriodSeconds => periodSeconds;
        public Vector3 Travel => transform.TransformVector(travel);
        public Quaternion BladeRotation => transform.rotation * (horizontalBlade ? Quaternion.Euler(90, 0, 0) : Quaternion.identity);
        public Vector3 BladeNormal => BladeRotation * Vector3.forward;

        public void ConfigureHorizontalSweep(Vector3 worldDisplacement, float seconds)
        {
            ConfigureSweep(transform.InverseTransformVector(worldDisplacement), seconds);
            horizontalBlade = true;
        }
        /// <summary>고정 레일의 중심에서 끝까지 로컬 이동량과 왕복 주기를 설정한다.</summary>
        public void ConfigureSweep(Vector3 displacement, float seconds)
        {
            if (!float.IsFinite(displacement.x) || !float.IsFinite(displacement.y) || !float.IsFinite(displacement.z)
                || !float.IsFinite(seconds) || seconds < 0.1f)
                throw new System.ArgumentOutOfRangeException(nameof(seconds), "유한한 이동량과 0.1초 이상의 주기가 필요합니다.");
            travel = displacement;
            periodSeconds = seconds;
        }
        public Vector3 Position(double elapsed, float speed) => transform.position + Travel
            * Mathf.Sin((float)(elapsed * speed * Mathf.PI * 2 / periodSeconds + phase));
        float Angle(double elapsed, float speed) => (float)(elapsed * speed * rotationSpeed % 360);

        public bool Touches(Vector3 feet, float height, float bodyRadius, double elapsed, float speed)
        {
            if (kind == CannonHazardKind.SpikeMace)
                return CannonHazardGeometry.SphereTouches(Position(elapsed, speed), radius, feet, height, bodyRadius);
            if (kind == CannonHazardKind.GroundSaw)
                return SawMath.TryGetContact(new SawDisc(Position(elapsed, speed), BladeNormal, radius, bladeHalfThickness),
                    new BodyCapsule(feet, height, bodyRadius), out _);
            Quaternion rotation = BladeRotation * Quaternion.Euler(0, 0, Angle(elapsed, speed));
            for (int i = 0; i < 2; i++)
            {
                Vector3 axis = i == 0 ? Vector3.right : Vector3.up;
                Vector3 half = rotation * axis * bladeHalfLength;
                if (CannonHazardGeometry.SegmentTouches(Position(elapsed, speed) - half, Position(elapsed, speed) + half,
                    bladeHalfThickness, feet, height, bodyRadius)) return true;
            }
            return false;
        }

        public int SweepSteps(Vector3 before, Vector3 after, double dt, float speed)
        {
            float motion = Travel.magnitude * Mathf.PI * 2 / periodSeconds;
            if (kind == CannonHazardKind.RotatingBlades) motion += bladeHalfLength * rotationSpeed * Mathf.Deg2Rad;
            return Mathf.Clamp(Mathf.CeilToInt((Vector3.Distance(before, after) + motion * speed * (float)dt) / 0.12f), 1, 64);
        }

        public void Present(double elapsed, float speed)
        {
            if (visual == null) return;
            visual.SetPositionAndRotation(Position(elapsed, speed),
                BladeRotation * Quaternion.Euler(0, 0, Angle(elapsed, speed)));
        }
    }
}
