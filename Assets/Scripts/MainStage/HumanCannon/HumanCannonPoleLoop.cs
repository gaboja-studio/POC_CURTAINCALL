using System;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>고정된 레일 기준으로 원본 그네 전체를 운반한다. 잡기·점유·반동은 PoleSystem이 맡는다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-80)]
    public sealed class HumanCannonPoleLoop : MonoBehaviour
    {
        [SerializeField] Transform carriage;
        [SerializeField] Vector3 localCenter;
        [Min(0f), SerializeField] float straightLength = 12f;
        [Min(.1f), SerializeField] float radius = 4f;
        [Min(0f), SerializeField] float metersPerSecond = 4f;
        [Range(0f, 1f), SerializeField] float phase01;
        [SerializeField] float yawOffsetDegrees;

        public Transform Carriage => carriage;
        public float Length => 2f * straightLength + 2f * Mathf.PI * radius;
        public bool IsConfigured => carriage != null && carriage != transform && carriage.IsChildOf(transform)
            && Finite(localCenter) && float.IsFinite(straightLength) && straightLength >= 0f
            && float.IsFinite(radius) && radius >= .1f && float.IsFinite(metersPerSecond) && metersPerSecond >= 0f
            && float.IsFinite(phase01) && float.IsFinite(yawOffsetDegrees) && float.IsFinite(Length);
        public bool IsSynchronized => HumanCannonHazardCourse.Current != null && HumanCannonHazardCourse.Current.IsSynchronized;
        public double Elapsed => IsSynchronized ? HumanCannonHazardCourse.Current.Elapsed : 0d;

        /// <summary>XZ 레일의 phase 0은 (+radius, +straight/2), 진행은 -Z다. 서측은 +Z로 돌아온다.</summary>
        public void Configure(Transform carriage, Vector3 localCenter, float straightLength, float radius,
            float metersPerSecond, float phase01 = 0f, float yawOffsetDegrees = 0f)
        {
            if (carriage == null || carriage == transform || !carriage.IsChildOf(transform)
                || !Finite(localCenter) || !float.IsFinite(straightLength) || straightLength < 0f
                || !float.IsFinite(radius) || radius < .1f || !float.IsFinite(metersPerSecond) || metersPerSecond < 0f
                || !float.IsFinite(phase01) || !float.IsFinite(yawOffsetDegrees))
                throw new ArgumentException("순환 레일에는 자식 carriage와 유효한 경로 치수가 필요합니다.");
            this.carriage = carriage; this.localCenter = localCenter; this.straightLength = straightLength; this.radius = radius;
            this.metersPerSecond = metersPerSecond; this.phase01 = Mathf.Repeat(phase01, 1f); this.yawOffsetDegrees = yawOffsetDegrees;
            RefreshPose();
        }

        /// <summary>공유 경과시간의 월드 위치/회전, 선속도(m/s), 각속도(rad/s). 위치는 적분하지 않는다.</summary>
        public void EvaluatePose(double elapsed, out Vector3 position, out Quaternion rotation,
            out Vector3 linearVelocity, out Vector3 angularVelocity)
        {
            if (!IsConfigured)
            { position = transform.position; rotation = transform.rotation; linearVelocity = angularVelocity = Vector3.zero; return; }
            double length = Length;
            double distance = ((Math.Max(0d, double.IsFinite(elapsed) ? elapsed : 0d) * metersPerSecond
                + Mathf.Repeat(phase01, 1f) * length) % length + length) % length;
            float s = (float)distance, arc = Mathf.PI * radius, half = straightLength * .5f;
            Vector3 point, tangent;
            bool turning;
            if (s < straightLength)
            { point = new Vector3(radius, 0, half - s); tangent = Vector3.back; turning = false; }
            else if (s < straightLength + arc)
            {
                float angle = (s - straightLength) / radius;
                point = new Vector3(radius * Mathf.Cos(angle), 0, -half - radius * Mathf.Sin(angle));
                tangent = new Vector3(-Mathf.Sin(angle), 0, -Mathf.Cos(angle)); turning = true;
            }
            else if (s < 2f * straightLength + arc)
            { point = new Vector3(-radius, 0, -half + (s - straightLength - arc)); tangent = Vector3.forward; turning = false; }
            else
            {
                float angle = (s - 2f * straightLength - arc) / radius;
                point = new Vector3(-radius * Mathf.Cos(angle), 0, half + radius * Mathf.Sin(angle));
                tangent = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)); turning = true;
            }
            position = transform.TransformPoint(localCenter + point);
            rotation = transform.rotation * Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0, yawOffsetDegrees, 0);
            linearVelocity = transform.TransformVector(tangent * metersPerSecond);
            angularVelocity = turning ? transform.up * (metersPerSecond / radius) : Vector3.zero;
        }

        public bool Contains(Transform target) => isActiveAndEnabled && IsConfigured
            && target != null && (target == carriage || target.IsChildOf(carriage));

        public void RefreshPose()
        {
            if (!isActiveAndEnabled || !IsConfigured) return;
            EvaluatePose(Elapsed, out var position, out var rotation, out _, out _);
            carriage.SetPositionAndRotation(position, rotation);
        }

        /// <summary>현재 운반체에 붙은 점을 다른 공유 시각에 평가한다. Grip의 중립점/원본 줄 길이는 보존한다.</summary>
        public Vector3 PointAt(Vector3 currentWorldPoint, double elapsed)
        {
            Vector3 offset = Quaternion.Inverse(carriage.rotation) * (currentWorldPoint - carriage.position);
            EvaluatePose(elapsed, out var position, out var rotation, out _, out _);
            return position + rotation * offset;
        }

        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            if (!IsSynchronized || !IsConfigured) return Vector3.zero;
            EvaluatePose(Elapsed, out var position, out _, out var velocity, out var angular);
            return velocity + Vector3.Cross(angular, worldPoint - position);
        }

        void OnEnable() => RefreshPose();
        void Update() => RefreshPose();
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
