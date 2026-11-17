using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    [Serializable]
    public sealed class PerformanceLightingSettings
    {
        [SerializeField] bool enabled = true;
        [SerializeField, Range(.1f, 1)] float ambientScale = .45f;
        [SerializeField, Range(0, 1)] float reflectionScale = .3f;
        [SerializeField, Range(.1f, 1)] float mainLightScale = .35f;
        [SerializeField, Range(.1f, 1)] float sceneryColorScale = .65f;
        [SerializeField, Range(0, 1)] float sceneryEmissionScale = .25f;
        [SerializeField, Range(1, 4)] float lampEmissionScale = 2.5f;
        [SerializeField, Min(0)] float readabilityIntensity = 250;
        [SerializeField, Range(10, 60)] float readabilityRange = 42;
        [SerializeField] Vector3 readabilityOffset = new(0, 8, 10);
        public bool Enabled => enabled;
        public float AmbientScale => Mathf.Clamp(ambientScale, .1f, 1);
        public float ReflectionScale => Mathf.Clamp01(reflectionScale);
        public float MainLightScale => Mathf.Clamp(mainLightScale, .1f, 1);
        public float SceneryColorScale => Mathf.Clamp(sceneryColorScale, .1f, 1);
        public float SceneryEmissionScale => Mathf.Clamp01(sceneryEmissionScale);
        public float LampEmissionScale => Mathf.Clamp(lampEmissionScale, 1, 4);
        public float ReadabilityIntensity => Mathf.Max(0, readabilityIntensity);
        public float ReadabilityRange => Mathf.Clamp(readabilityRange, 10, 60);
        public Vector3 ReadabilityOffset => readabilityOffset;
    }
}
