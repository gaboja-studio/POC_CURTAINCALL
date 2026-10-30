using System;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuitFire
    {
        sealed class Effect
        {
            public ParticleSystem system;
            public float overTime, overDistance;
            public bool loop, emissionEnabled;
        }
        sealed class Presentation
        {
            public Effect[] effects;
            public float[] intensities;
            public Color[] colors;
            public FirePhase phase;
        }
        Presentation[] presentation;

        void CachePresentation()
        {
            if (presentation != null && presentation.Length == bays.Length) return;
            presentation = new Presentation[bays.Length];
            for (int i = 0; i < bays.Length; i++)
            {
                var bay = bays[i];
                if (bay == null) continue;
                var view = new Presentation { effects = new Effect[bay.effects.Length],
                    intensities = new float[bay.lights.Length], colors = new Color[bay.lights.Length] };
                presentation[i] = view;
                for (int e = 0; e < bay.effects.Length; e++)
                {
                    var effect = bay.effects[e]; if (effect == null) continue;
                    effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var emission = effect.emission;
                    view.effects[e] = new Effect { system = effect, overTime = emission.rateOverTimeMultiplier,
                        overDistance = emission.rateOverDistanceMultiplier, loop = effect.main.loop, emissionEnabled = emission.enabled };
                }
                for (int l = 0; l < bay.lights.Length; l++) if (bay.lights[l] != null)
                { view.intensities[l] = bay.lights[l].intensity; view.colors[l] = bay.lights[l].color; }
            }
        }

        void HidePresentation()
        {
            CachePresentation();
            for (int i = 0; i < bays.Length; i++)
            {
                var bay = bays[i]; var view = presentation[i]; if (bay == null || view == null) continue;
                foreach (var effect in view.effects) if (effect != null && effect.system != null)
                {
                    var emission = effect.system.emission; emission.enabled = false;
                    if (effect.system.isPlaying || effect.system.particleCount > 0)
                        effect.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                foreach (var lamp in bay.lights) if (lamp != null) lamp.enabled = false;
                if (bay.burstAudio != null && bay.burstAudio.isPlaying) bay.burstAudio.Stop();
                view.phase = FirePhase.Dormant;
            }
        }

        void Present(double now)
        {
            CachePresentation();
            for (int i = 0; i < bays.Length; i++)
            {
                var bay = bays[i]; var view = presentation[i]; if (bay == null || view == null) continue;
                FirePhase phase = PhaseAt(i, now);
                double age = now - armedAt - startDelay - i * (double)stepSeconds;
                bool burning = phase == FirePhase.Burst || phase == FirePhase.Burning;
                bool justIgnited = burning && view.phase != FirePhase.Burst && view.phase != FirePhase.Burning;
                float rate = phase == FirePhase.Burst ? Mathf.Max(0, burstEmissionMultiplier) : Mathf.Max(.01f, steadyEmissionMultiplier);
                foreach (var effect in view.effects)
                {
                    if (effect == null || effect.system == null) continue;
                    var system = effect.system;
                    if (!burning)
                    {
                        if (system.isPlaying || system.particleCount > 0) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        continue;
                    }
                    if (phase != view.phase)
                    {
                        var emission = system.emission;
                        emission.enabled = true;
                        emission.rateOverTimeMultiplier = effect.overTime * rate;
                        emission.rateOverDistanceMultiplier = effect.overDistance * rate;
                    }
                    if (justIgnited)
                    {
                        var main = system.main; main.loop = true;
                        // The whole authored bay is lethal at ignition. Fill at least one particle lifetime
                        // immediately so a 42m nozzle never damages the far wall before visible flame reaches it.
                        float fill = Mathf.Clamp(main.startLifetime.constantMax, .05f, 6);
                        system.Simulate(Mathf.Max(fill, (float)Math.Min(6, Math.Max(0, age))), false, true, false);
                        system.Play(false);
                    }
                }
                for (int l = 0; l < bay.lights.Length; l++)
                {
                    var lamp = bay.lights[l]; if (lamp == null) continue;
                    lamp.enabled = phase != FirePhase.Dormant;
                    float pulse = .82f + .18f * Mathf.Sin((float)(now % 1000) * (phase == FirePhase.Warning ? 11 : 23) + i);
                    lamp.intensity = view.intensities[l] * pulse * (phase == FirePhase.Burst ? 1 : phase == FirePhase.Burning ? .28f : .2f);
                    lamp.color = phase == FirePhase.Warning ? new Color(1, .48f, .06f) : view.colors[l];
                }
                if (bay.burstAudio != null && bay.burstAudio.clip != null)
                {
                    if (phase == FirePhase.Burst && view.phase != FirePhase.Burst && age < bay.burstAudio.clip.length)
                    {
                        bay.burstAudio.loop = false; bay.burstAudio.Play();
                        bay.burstAudio.time = Mathf.Clamp((float)Math.Max(0, age), 0, Mathf.Max(0, bay.burstAudio.clip.length - .01f));
                    }
                    else if (!burning && bay.burstAudio.isPlaying) bay.burstAudio.Stop();
                }
                view.phase = phase;
            }
        }

        void RestorePresentation()
        {
            HidePresentation();
            for (int i = 0; i < bays.Length; i++)
            {
                var view = presentation[i]; var bay = bays[i]; if (view == null || bay == null) continue;
                foreach (var effect in view.effects) if (effect != null && effect.system != null)
                {
                    var emission = effect.system.emission;
                    emission.rateOverTimeMultiplier = effect.overTime; emission.rateOverDistanceMultiplier = effect.overDistance;
                    emission.enabled = effect.emissionEnabled;
                    var main = effect.system.main; main.loop = effect.loop;
                }
                for (int l = 0; l < bay.lights.Length; l++) if (bay.lights[l] != null)
                { bay.lights[l].intensity = view.intensities[l]; bay.lights[l].color = view.colors[l]; }
            }
            presentation = null;
        }
    }
}
