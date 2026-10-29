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
        public bool Visible(double elapsed) => Live(elapsed) || showWhileWarning && Cycle(elapsed) >= 0 && Cycle(elapsed) < warningSeconds;
        public Vector3 Position(double elapsed)
        {
            float t = Mathf.Clamp01((float)(Cycle(elapsed) - warningSeconds) / Mathf.Max(.1f, activeSeconds));
            return Vector3.Lerp(origin, destination, falling ? t * t : t);
        }
        public float MaximumSpeed => Vector3.Distance(origin, destination) / Mathf.Max(.1f, activeSeconds) * (falling ? 2 : 1);
        public void Present(double elapsed, bool running)
        {
            double t = Cycle(elapsed);
            if (warning != null) warning.gameObject.SetActive(running && t >= 0 && t < warningSeconds
                && Math.Floor(t * 6) % 2 == 0);
            if (controlsVisual && visual != null) visual.gameObject.SetActive(running && Visible(elapsed));
        }
        void Update()
        {
            var course = HumanCannonHazardCourse.Current;
            Present(course != null ? course.Elapsed : 0, course != null && course.IsSynchronized);
        }
        void OnDisable() { if (warning != null) warning.gameObject.SetActive(false); }
    }
}
