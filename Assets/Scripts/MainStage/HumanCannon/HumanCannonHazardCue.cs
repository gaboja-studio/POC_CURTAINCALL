using System;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>기존 HazardCourse의 공유 시각으로 예고/작동/휴지를 표시한다. 피해와 플레이어 이동은 원본 시스템이 담당한다.</summary>
    public sealed class HumanCannonHazardCue : MonoBehaviour
    {
        public Transform warning, visual;
        public Vector3 origin, destination;
        [Min(.1f)] public float warningSeconds = 1.4f, activeSeconds = 2f, restSeconds = 2f;
        [Min(0)] public float phase;
        public bool falling, showWhileWarning;
        public bool controlsVisual;
        [SerializeField] ParticleSystem ignitionSparks;
        [SerializeField, Tooltip("World-space movement of the falling load's visual during the harmless rest phase.")]
        Vector3 landingVisualTransfer;
        [SerializeField] Transform leftChuteDoor, rightChuteDoor;
        [SerializeField] float chuteDoorTravel;
        CurtainCall.WorldIndicators.HazardWorldIndicator worldIndicator;
        public double Cycle(double elapsed)
        {
            double period = warningSeconds + activeSeconds + restSeconds;
            return double.IsFinite(elapsed) && elapsed >= 0 && period > 0 ? (elapsed + phase) % period : -1;
        }
        public bool Live(double elapsed)
        {
            double t = Cycle(elapsed);
            return t >= warningSeconds && t < warningSeconds + activeSeconds;
        }
        public bool Visible(double elapsed)
        {
            double t = Cycle(elapsed);
            return t >= 0 && (t < warningSeconds
                ? showWhileWarning
                : t < warningSeconds + activeSeconds || HasLandingTransfer);
        }

        bool HasLandingTransfer => falling && restSeconds > 0 && landingVisualTransfer != Vector3.zero;

        public Vector3 VisualPosition(double elapsed)
        {
            double restTime = Cycle(elapsed) - (warningSeconds + activeSeconds);
            if (!HasLandingTransfer || restTime < 0) return Position(elapsed);

            // Leave a short beat for impact before the rollers take the load.
            float progress = Mathf.InverseLerp(.12f * restSeconds, restSeconds, (float)restTime);
            return destination + landingVisualTransfer * Mathf.SmoothStep(0, 1, progress);
        }
        public Vector3 Position(double elapsed)
        {
            float t = Mathf.Clamp01((float)(Cycle(elapsed) - warningSeconds) / Mathf.Max(.1f, activeSeconds));
            return Vector3.Lerp(origin, destination, falling ? t * t : t);
        }
        public float MaximumSpeed => Vector3.Distance(origin, destination) / Mathf.Max(.1f, activeSeconds) * (falling ? 2 : 1);
        void OnEnable() { if (warning != null) warning.gameObject.SetActive(false); }
        public void Present(double elapsed, bool running)
        {
            double t = Cycle(elapsed);
            if (leftChuteDoor && rightChuteDoor)
            {
                float opening = Mathf.InverseLerp(warningSeconds - .55f, warningSeconds, (float)t);
                float closing = Mathf.InverseLerp(warningSeconds + activeSeconds, warningSeconds + activeSeconds + .5f, (float)t);
                float slide = running ? chuteDoorTravel * Mathf.SmoothStep(0, 1, opening - closing) : 0;
                leftChuteDoor.localPosition = Vector3.left * slide;
                rightChuteDoor.localPosition = Vector3.right * slide;
            }
            if (ignitionSparks)
            {
                bool heating = running && t >= Math.Max(0, warningSeconds - .7f) && t < warningSeconds;
                if (heating && !ignitionSparks.isEmitting) ignitionSparks.Play(false);
                else if (!heating && ignitionSparks.isEmitting) ignitionSparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            bool modern = false;
            if (Application.isPlaying && TryGetComponent<HumanCannonHazard>(out var hazard))
            {
                if (!worldIndicator) worldIndicator = GetComponent<CurtainCall.WorldIndicators.HazardWorldIndicator>();
                if (!worldIndicator) worldIndicator = gameObject.AddComponent<CurtainCall.WorldIndicators.HazardWorldIndicator>();
                modern = worldIndicator.Present(hazard, this, running, elapsed);
            }
            if (warning != null) warning.gameObject.SetActive(!modern && running && t >= 0 && t < warningSeconds
                && Math.Floor(t * 6) % 2 == 0);
            if (controlsVisual && visual != null) visual.gameObject.SetActive(running && Visible(elapsed));
        }
        void Update()
        {
            var course = HumanCannonHazardCourse.Current;
            Present(course != null ? course.Elapsed : 0, course != null && course.IsSynchronized);
        }
        void OnDisable()
        {
            if (warning != null) warning.gameObject.SetActive(false);
            if (worldIndicator) worldIndicator.Hide();
            if (leftChuteDoor) leftChuteDoor.localPosition = Vector3.zero;
            if (rightChuteDoor) rightChuteDoor.localPosition = Vector3.zero;
            if (ignitionSparks) ignitionSparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
