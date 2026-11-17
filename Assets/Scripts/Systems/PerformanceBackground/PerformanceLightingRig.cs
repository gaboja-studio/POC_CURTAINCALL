using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.PerformanceBackground
{
    /// <summary>Local presentation only. Shared clones preserve batching; original private materials stay untouched.</summary>
    sealed class PerformanceLightingRig
    {
        readonly Dictionary<Material, Material> materials = new();
        readonly List<(MeshRenderer renderer, Material[] originals)> renderers = new();
        readonly List<(Light light, float intensity)> directional = new();
        Transform root;
        Light fill;
        float colorScale = -1, emissionScale = -1, lampScale = -1;

        public void Update(Scene scene, PerformanceLightingProfile settings)
        {
            var background = PerformanceBackgroundLoader.BackgroundRoot;
            if (!settings.Enabled || background == null) { Restore(); return; }
            if (root != background)
            {
                Restore(); root = background;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var originals = renderer.sharedMaterials;
                    var copies = (Material[])originals.Clone();
                    for (int i = 0; i < copies.Length; i++)
                    {
                        var original = originals[i]; if (original == null) continue;
                        if (!materials.TryGetValue(original, out var copy))
                        {
                            copy = new Material(original) { name = original.name + " (Performance lighting)", hideFlags = HideFlags.DontSave };
                            materials.Add(original, copy);
                        }
                        copies[i] = copy;
                    }
                    renderers.Add((renderer, originals)); renderer.sharedMaterials = copies;
                }
                foreach (var sceneRoot in scene.GetRootGameObjects())
                    foreach (var light in sceneRoot.GetComponentsInChildren<Light>(true))
                        if (light.type == LightType.Directional) directional.Add((light, light.intensity));
                fill = new GameObject("PerformanceReadabilityFill").AddComponent<Light>();
                fill.transform.SetParent(root, false); fill.type = LightType.Point;
                fill.shadows = LightShadows.None; fill.color = new Color(1, .84f, .64f);
            }
            if (colorScale != settings.SceneryColorScale || emissionScale != settings.SceneryEmissionScale || lampScale != settings.LampEmissionScale)
            {
                colorScale = settings.SceneryColorScale; emissionScale = settings.SceneryEmissionScale; lampScale = settings.LampEmissionScale;
                foreach (var pair in materials)
                {
                    var original = pair.Key; var copy = pair.Value;
                    var emission = original.HasProperty("_EmissionColor") ? original.GetColor("_EmissionColor") : Color.black;
                    bool lamp = emission.maxColorComponent >= .6f || original.name.IndexOf("Lamp", StringComparison.OrdinalIgnoreCase) >= 0 || original.name.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (copy.HasProperty("_EmissionColor")) copy.SetColor("_EmissionColor", emission * (lamp ? lampScale : emissionScale));
                    if (!lamp && copy.HasProperty("_BaseColor"))
                    {
                        var color = original.GetColor("_BaseColor"); var dark = color * colorScale; dark.a = color.a;
                        copy.SetColor("_BaseColor", dark);
                    }
                }
            }
            foreach (var entry in directional) if (entry.light != null) entry.light.intensity = entry.intensity * settings.MainLightScale;
            var position = PerformanceLightingConfiguration.ReadPosition?.Invoke();
            fill.enabled = position.HasValue;
            if (position.HasValue) fill.transform.position = position.Value + settings.ReadabilityOffset;
            fill.intensity = settings.ReadabilityIntensity; fill.range = settings.ReadabilityRange;
        }
        public void Restore()
        {
            foreach (var entry in renderers) if (entry.renderer != null) entry.renderer.sharedMaterials = entry.originals;
            foreach (var entry in directional) if (entry.light != null) entry.light.intensity = entry.intensity;
            foreach (var material in materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            if (fill != null) UnityEngine.Object.Destroy(fill.gameObject);
            fill = null; root = null; renderers.Clear(); directional.Clear(); materials.Clear();
            colorScale = emissionScale = lampScale = -1;
        }
    }
}
