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
        [SerializeField] Vector3 travel = new Vector3(4, 0, 0);
        [SerializeField, Min(0.1f)] float periodSeconds = 8f;
        [SerializeField] float phase;
        [SerializeField, Min(0.01f)] float radius = 0.65f;
        [SerializeField, Min(0.1f)] float bladeHalfLength = 1.3f;
        [SerializeField, Min(0.01f)] float bladeHalfThickness = 0.12f;
        [SerializeField, Min(1f)] float rotationSpeed = 60f;
        public CannonHazardKind Kind => kind;
        public float Radius => radius;
        public Vector3 Travel => transform.TransformVector(travel);
        public Vector3 Position(double elapsed, float speed) => transform.position + Travel
            * Mathf.Sin((float)(elapsed * speed * Mathf.PI * 2 / periodSeconds + phase));
        float Angle(double elapsed, float speed) => (float)(elapsed * speed * rotationSpeed % 360);

        public bool Touches(Vector3 feet, float height, float bodyRadius, double elapsed, float speed)
        {
            if (kind == CannonHazardKind.SpikeMace)
                return CannonHazardGeometry.SphereTouches(Position(elapsed, speed), radius, feet, height, bodyRadius);
            if (kind == CannonHazardKind.GroundSaw)
                return SawMath.TryGetContact(new SawDisc(Position(elapsed, speed), transform.forward, radius, bladeHalfThickness),
                    new BodyCapsule(feet, height, bodyRadius), out _);
            Quaternion rotation = transform.rotation * Quaternion.Euler(0, 0, Angle(elapsed, speed));
            for (int i = 0; i < 2; i++)
            {
                Vector3 axis = i == 0 ? Vector3.right : Vector3.up;
                Vector3 half = rotation * axis * bladeHalfLength;
                if (CannonHazardGeometry.SegmentTouches(transform.position - half, transform.position + half,
                    bladeHalfThickness, feet, height, bodyRadius)) return true;
            }
            return false;
        }

        public int SweepSteps(Vector3 before, Vector3 after, double dt, float speed)
        {
            float motion = kind == CannonHazardKind.RotatingBlades ? bladeHalfLength * rotationSpeed * Mathf.Deg2Rad
                : Travel.magnitude * Mathf.PI * 2 / periodSeconds;
            return Mathf.Clamp(Mathf.CeilToInt((Vector3.Distance(before, after) + motion * speed * (float)dt) / 0.12f), 1, 64);
        }

        public void Present(double elapsed, float speed)
        {
            if (visual == null) return;
            visual.SetPositionAndRotation(kind == CannonHazardKind.RotatingBlades ? transform.position : Position(elapsed, speed),
                transform.rotation * Quaternion.Euler(0, 0, Angle(elapsed, speed)));
        }
    }
}
