using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Retires the sealed waiting room's graphics without stopping its shared gameplay services.</summary>
    public sealed class EscapeWaitingPresentation
    {
        readonly List<(Renderer renderer, bool forcedOff)> renderers = new();
        readonly List<Light> lights = new();
        readonly List<ParticleSystem> particles = new();
        readonly List<(GameObject root, Renderer renderer)> scenery = new();
        readonly List<(GameObject root, Light light)> roomLights = new();
        readonly List<(GameObject root, ParticleSystem system)> roomParticles = new();
        static readonly ProfilerMarker RetireMarker = new("Escape.RetireWaitingGraphics");
        bool prepared;
        bool hidden;
        int cursor, lastFrame = -1;

        // Traverse the large room while it loads, not while four players are departing.
        public void Prepare(GameObject[] roots)
        {
            if (prepared) return;
            prepared = true;
            if (roots == null) return;
            var seen = new HashSet<Renderer>();
            var seenLights = new HashSet<Light>();
            var seenParticles = new HashSet<ParticleSystem>();
            foreach (var root in roots)
            {
                if (root == null || !root.activeInHierarchy) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (seen.Add(renderer)) scenery.Add((root, renderer));
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (seenLights.Add(light)) roomLights.Add((root, light));
                foreach (var system in root.GetComponentsInChildren<ParticleSystem>(true))
                    if (seenParticles.Add(system)) roomParticles.Add((root, system));
            }
            renderers.Capacity = scenery.Count;
            lights.Capacity = roomLights.Count;
            particles.Capacity = roomParticles.Count;
        }

        static bool StillInRoom(GameObject root, Component item) => root != null && item != null
            && root.activeInHierarchy && item.gameObject.activeInHierarchy && item.transform.IsChildOf(root.transform);

        public void Hide(GameObject[] roots)
        {
            if (hidden) { Advance(); return; }
            Prepare(roots);
            hidden = true;
            foreach (var item in roomLights)
                if (StillInRoom(item.root, item.light) && item.light.enabled)
                { lights.Add(item.light); item.light.enabled = false; }
            foreach (var item in roomParticles)
                if (StillInRoom(item.root, item.system) && item.system.isPlaying)
                { particles.Add(item.system); item.system.Pause(false); }
            Advance();
        }

        void Advance()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            using var sample = RetireMarker.Auto();
            int end = Mathf.Min(cursor + 1024, scenery.Count);
            while (cursor < end)
            {
                var item = scenery[cursor++];
                if (!StillInRoom(item.root, item.renderer)) continue;
                renderers.Add((item.renderer, item.renderer.forceRenderingOff));
                item.renderer.forceRenderingOff = true;
            }
        }

        public void Restore()
        {
            if (!hidden) return;
            foreach (var item in renderers) if (item.renderer != null) item.renderer.forceRenderingOff = item.forcedOff;
            foreach (var light in lights) if (light != null) light.enabled = true;
            foreach (var system in particles) if (system != null) system.Play(false);
            renderers.Clear(); lights.Clear(); particles.Clear();
            cursor = 0; lastFrame = -1; hidden = false;
        }
    }
}
