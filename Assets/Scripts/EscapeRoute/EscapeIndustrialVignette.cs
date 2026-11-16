using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Cosmetic companions to EscapeConveyorProcession. Assign only dedicated scenery,
    // never its existing saw pivot, player objects, or machinery with gameplay motion.
    [DisallowMultipleComponent]
    public sealed class EscapeIndustrialVignette : MonoBehaviour
    {
        [Serializable]
        public struct Rotor
        {
            public Transform target;
            public Vector3 localAxis;
            public float degreesPerSecond;
        }

        [Serializable]
        public struct EmissionWindow
        {
            public ParticleSystem particles;
            [Range(0, 1)] public float from;
            [Range(0, 1)] public float until;
        }

        [Min(.5f)] public float cycleSeconds = 8;
        // Authored per vignette, so all peers use the same stagger without random state.
        public float phaseOffsetSeconds;
        public Rotor[] rotors = Array.Empty<Rotor>();
        public EmissionWindow[] emissions = Array.Empty<EmissionWindow>();
        public Light pulseLight;
        [Range(0, .25f)] public float lightPulseFraction = .08f;
        public AudioSource cycleAudio;
        [Range(0, 1)] public float audioPhase = .62f;

        readonly Dictionary<Transform, Quaternion> initialRotations = new();
        readonly HashSet<ParticleSystem> controlledParticles = new();
        Light capturedLight;
        AudioSource playedAudio;
        float initialLightIntensity;
        double previousClock = double.NaN;
        bool captured;

        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            Capture();
            Sample(HumanCannonStation.MotionTime);
        }

        void Capture()
        {
            initialRotations.Clear(); controlledParticles.Clear();
            foreach (var rotor in rotors)
                if (rotor.target && !initialRotations.ContainsKey(rotor.target))
                    initialRotations.Add(rotor.target, rotor.target.localRotation);
            foreach (var window in emissions) if (window.particles) controlledParticles.Add(window.particles);
            capturedLight = pulseLight;
            if (capturedLight) initialLightIntensity = capturedLight.intensity;
            previousClock = double.NaN; captured = true;
        }

        void Update()
        {
            if (captured) Sample(HumanCannonStation.MotionTime);
        }

        static float PhaseAt(double clock, float seconds, float offset)
        {
            if (!double.IsFinite(clock) || !float.IsFinite(seconds) || !float.IsFinite(offset)) return 0;
            double cycles = (clock + offset) / Math.Max(.5, seconds);
            return (float)(cycles - Math.Floor(cycles));
        }

        static bool InsideWindow(float phase, float from, float until)
        {
            from = Mathf.Clamp01(from); until = Mathf.Clamp01(until);
            return from < until ? phase >= from && phase < until : from > until && (phase >= from || phase < until);
        }

        void Sample(double clock)
        {
            if (!captured || !double.IsFinite(clock)) return;
            float phase = PhaseAt(clock, cycleSeconds, phaseOffsetSeconds);
            foreach (var rotor in rotors)
            {
                if (!rotor.target || !float.IsFinite(rotor.degreesPerSecond)
                    || !initialRotations.TryGetValue(rotor.target, out var rotation)) continue;
                Vector3 axis = rotor.localAxis.sqrMagnitude > .0001f ? rotor.localAxis.normalized : Vector3.forward;
                float angle = (float)(((clock + phaseOffsetSeconds) * rotor.degreesPerSecond) % 360d);
                rotor.target.localRotation = rotation * Quaternion.AngleAxis(angle, axis);
            }
            // Several windows may share one emitter, e.g. four bodies reaching the same saw.
            foreach (var particles in controlledParticles)
            {
                if (!particles) continue;
                bool emit = false;
                foreach (var window in emissions)
                    if (window.particles == particles && InsideWindow(phase, window.from, window.until)) { emit = true; break; }
                if (emit && !particles.isEmitting) particles.Play(false);
                else if (!emit && particles.isEmitting) particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (capturedLight)
            {
                float pulse = (Mathf.Sin(phase * Mathf.PI * 2) + .35f * Mathf.Sin(phase * Mathf.PI * 14)) / 1.35f;
                capturedLight.intensity = initialLightIntensity * (1 + Mathf.Clamp(lightPulseFraction, 0, .25f) * pulse);
            }
            if (cycleAudio && cycleAudio.clip && cycleAudio.isActiveAndEnabled && !cycleAudio.isPlaying
                && CrossedCue(previousClock, clock, cycleSeconds, phaseOffsetSeconds, audioPhase))
            {
                cycleAudio.PlayOneShot(cycleAudio.clip); playedAudio = cycleAudio;
            }
            previousClock = clock;
        }

        static bool CrossedCue(double previous, double clock, float seconds, float offset, float cue)
        {
            if (!double.IsFinite(previous) || !double.IsFinite(clock) || clock <= previous
                || !float.IsFinite(seconds) || !float.IsFinite(offset) || !float.IsFinite(cue)) return false;
            double period = Math.Max(.5, seconds), at = Mathf.Clamp01(cue) * period;
            return Math.Floor((clock + offset - at) / period) > Math.Floor((previous + offset - at) / period);
        }

        void OnDisable()
        {
            if (!captured) return;
            foreach (var pair in initialRotations) if (pair.Key) pair.Key.localRotation = pair.Value;
            foreach (var particles in controlledParticles)
                if (particles) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (capturedLight) capturedLight.intensity = initialLightIntensity;
            if (playedAudio) playedAudio.Stop();
            playedAudio = null; previousClock = double.NaN; captured = false;
            initialRotations.Clear(); controlledParticles.Clear();
        }
    }
}
