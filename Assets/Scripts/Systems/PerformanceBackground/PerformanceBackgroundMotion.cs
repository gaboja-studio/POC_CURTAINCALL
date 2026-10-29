using System;
using UnityEngine;

namespace CurtainCall.PerformanceBackground
{
    /// <summary>Local, decorative motion. No physics or network authority is changed.</summary>
    [DisallowMultipleComponent]
    public sealed class PerformanceBackgroundMotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Motion
        {
            public Transform target;
            public Vector3 restPosition;
            public Quaternion restRotation = Quaternion.identity;
            public Vector3 restScale = Vector3.one;
            [Tooltip("각 축의 최대 회전각(도). 원래 자세를 중심으로 왕복합니다.")]
            public Vector3 rotationDegrees;
            public Vector3 travel;
            [Tooltip("톱날처럼 계속 회전하는 축별 속도(도/초)")]
            public Vector3 spinDegreesPerSecond;
            public Vector3 scaleBreathing;
            [Min(1f)] public float periodSeconds = 60f;
            [Range(0f, 1f)] public float phase;

            public void CaptureRestPose()
            {
                if (target == null) return;
                restPosition = target.localPosition;
                restRotation = target.localRotation;
                restScale = target.localScale;
            }

            public void Sample(double seconds)
            {
                if (target == null) return;
                double cycles = seconds / Math.Max(1.0, periodSeconds) + phase;
                float wave = (float)Math.Sin((cycles % 1.0) * Math.PI * 2.0);
                target.localPosition = restPosition + travel * wave;
                var spin = new Vector3((float)(spinDegreesPerSecond.x * seconds % 360.0), (float)(spinDegreesPerSecond.y * seconds % 360.0), (float)(spinDegreesPerSecond.z * seconds % 360.0));
                target.localRotation = restRotation * Quaternion.Euler(rotationDegrees * wave) * Quaternion.Euler(spin);
                target.localScale = Vector3.Scale(restScale, Vector3.one + scaleBreathing * wave);
            }

            public void Restore()
            {
                if (target == null) return;
                target.localPosition = restPosition;
                target.localRotation = restRotation;
                target.localScale = restScale;
            }
        }

        [Serializable]
        public sealed class LightPulse
        {
            public Light target;
            public float restIntensity;
            [Range(0f, .5f)] public float amount = .14f;
            public float periodSeconds = 9f;
            public float phase;
            public void Sample(double seconds)
            {
                if (target != null) target.intensity = restIntensity * (1f + amount * (float)Math.Sin(seconds * Math.PI * 2 / Math.Max(1f, periodSeconds) + phase * Math.PI * 2));
            }
            public void Restore() { if (target != null) target.intensity = restIntensity; }
        }

        [Serializable]
        public sealed class SurfaceMotion
        {
            public Renderer target;
            public bool flowingFog;
            public float amount = .2f;
            public float periodSeconds = 11f;
            public float phase;
            [NonSerialized] MaterialPropertyBlock original, block;
            [NonSerialized] Color baseColor;
            static readonly int ColorId = Shader.PropertyToID("_BaseColor");
            static readonly int TimeId = Shader.PropertyToID("_MotionTime");

            public void Sample(double seconds)
            {
                if (target == null || target.sharedMaterial == null) return;
                if (block == null)
                {
                    original = new MaterialPropertyBlock();
                    block = new MaterialPropertyBlock();
                    target.GetPropertyBlock(original);
                    baseColor = target.sharedMaterial.GetColor(ColorId);
                }
                target.GetPropertyBlock(block);
                if (flowingFog) block.SetFloat(TimeId, (float)seconds);
                else
                {
                    float factor = 1f + amount * (float)Math.Sin(seconds * Math.PI * 2 / Math.Max(1f, periodSeconds) + phase * Math.PI * 2);
                    var color = baseColor * factor;
                    color.a = baseColor.a;
                    block.SetColor(ColorId, color);
                }
                target.SetPropertyBlock(block);
            }

            public void Restore()
            {
                if (target != null && original != null) target.SetPropertyBlock(original);
                block = original = null;
            }
        }

        [Range(0f, 3f)] public float speed = 1f;
        [Header("Crane arms")]
        public Motion[] cranes = Array.Empty<Motion>();
        [Header("Show spotlights and visible beams")]
        public Motion[] showLights = Array.Empty<Motion>();
        [Header("Continuous saw blades / gears")]
        public Motion[] machinery = Array.Empty<Motion>();
        [Header("Fog banks, curtains, banners and cables")]
        public Motion[] atmosphere = Array.Empty<Motion>();
        public LightPulse[] practicalLights = Array.Empty<LightPulse>();
        public SurfaceMotion[] surfaces = Array.Empty<SurfaceMotion>();
        [Header("Airborne dust / furnace embers / grinding sparks")]
        public ParticleSystem[] particles = Array.Empty<ParticleSystem>();

        double startedAt;

        void OnEnable()
        {
            startedAt = Time.timeAsDouble;
            if (Application.isPlaying)
                foreach (var system in particles) if (system != null) system.Play(true);
        }
        void Update() => Sample(Time.timeAsDouble - startedAt);
        void OnDisable() => RestoreRestPose();

        public void Sample(double seconds)
        {
            Sample(cranes, seconds * speed);
            Sample(showLights, seconds * speed);
            Sample(machinery, seconds * speed);
            Sample(atmosphere, seconds * speed);
            foreach (var light in practicalLights) light?.Sample(seconds * speed);
            foreach (var surface in surfaces) surface?.Sample(seconds * speed);
        }

        static void Sample(Motion[] motions, double seconds)
        {
            if (motions == null) return;
            foreach (var motion in motions) motion?.Sample(seconds);
        }

        public void RestoreRestPose()
        {
            Restore(cranes);
            Restore(showLights);
            Restore(machinery);
            Restore(atmosphere);
            foreach (var light in practicalLights) light?.Restore();
            foreach (var surface in surfaces) surface?.Restore();
            foreach (var system in particles) if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void PreviewParticles(float seconds, bool restart)
        {
            if (Application.isPlaying) return;
            foreach (var system in particles)
                if (system != null) system.Simulate(seconds * speed, true, restart, true);
        }

        static void Restore(Motion[] motions)
        {
            if (motions == null) return;
            foreach (var motion in motions) motion?.Restore();
        }
    }
}
